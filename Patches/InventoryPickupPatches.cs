using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using InventoryExpansion.Config;
using MelonLoader;
using ReluProtocol.Enum;

namespace InventoryExpansion.Patches
{
	// While the backpack is open, place picked-up items into the backpack slots before the
	// standard inventory. Placement is server-authoritative (InventoryController.HandleAddItem),
	// so this only takes effect when you are the host or in single-player; on a remote client
	// the prefix simply does nothing and the original runs.
	[HarmonyPatch(typeof(InventoryController))]
	internal static class InventoryPickupPatches
	{
		private static FieldInfo _slotsField;
		private static MethodInfo _onAddItemByLooting;
		private static bool _reflectionInitialized;

		private static void EnsureReflection()
		{
			if (_reflectionInitialized)
			{
				return;
			}
			_reflectionInitialized = true;

			_slotsField = typeof(InventoryController).GetField("_inventorySlots", BindingFlags.Instance | BindingFlags.NonPublic);
			_onAddItemByLooting = typeof(InventoryController).GetMethod("OnAddItemByLooting", BindingFlags.Instance | BindingFlags.NonPublic);
		}

		[HarmonyPrefix]
		[HarmonyPatch("HandleAddItem")]
		private static bool HandleAddItem_Prefix(InventoryController __instance, ItemElement itemElement, ref int addedSlotIndex, bool sync, bool byLooting, ref MsgErrorCode __result)
		{
			try
			{
				if (!InventoryExpansionPreferences.Enabled || !InventoryExpansionPreferences.FillBackpackFirst)
				{
					return true;
				}

				// Only redirect while the backpack is actually open.
				if (!BackpackPanelPatch.IsBackpackFullyVisible)
				{
					return true;
				}

				EnsureReflection();
				if (_slotsField == null)
				{
					return true;
				}

				if (_slotsField.GetValue(__instance) is not IDictionary slots)
				{
					return true;
				}

				// If the active slot is empty, let the original handle it: its Phase 1 places into
				// the active slot (a backpack slot while open, thanks to the cursor handoff) and
				// runs the equip/turn-on setup we intentionally do not replicate here.
				int currentSlot = __instance.CurrentInventorySlot;
				if (slots.Contains(currentSlot) && slots[currentSlot] == null)
				{
					return true;
				}

				// Find the lowest empty backpack slot. Server slot keys are 1-based: 1-4 are the
				// standard inventory, 5..N are the backpack slots.
				int targetKey = -1;
				foreach (var keyObj in slots.Keys)
				{
					if (keyObj is int key && key >= 5 && slots[keyObj] == null)
					{
						if (targetKey < 0 || key < targetKey)
						{
							targetKey = key;
						}
					}
				}

				if (targetKey < 0)
				{
					// No empty backpack slot; let the original fill the standard inventory or
					// report InvenFull.
					return true;
				}

				if (byLooting && _onAddItemByLooting != null)
				{
					_onAddItemByLooting.Invoke(__instance, new object[] { itemElement });
				}

				__instance.AddInvenItem(targetKey, itemElement, sync);
				addedSlotIndex = targetKey;
				__result = MsgErrorCode.Success;
				return false;
			}
			catch (Exception ex)
			{
				MelonLogger.Error($"[InventoryExpansion][Pickup] HandleAddItem prefix failed, falling back to original: {ex}");
				return true;
			}
		}
	}
}

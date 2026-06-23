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
	// which on a host runs for EVERY player's pickups - including remote clients. The redirect is
	// driven by the LOCAL client's backpack UI flag, so it must only ever touch the LOCAL player's
	// own inventory; otherwise a host would redirect a remote client's pickup based on the host's
	// backpack state (the wrong player), which desyncs that client and shows up as duplicated items.
	[HarmonyPatch(typeof(InventoryController))]
	internal static class InventoryPickupPatches
	{
		private static FieldInfo _slotsField;
		private static FieldInfo _selfField;
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
			_selfField = typeof(InventoryController).GetField("_self", BindingFlags.Instance | BindingFlags.NonPublic);
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

				EnsureReflection();
				if (_slotsField == null)
				{
					return true;
				}

				// HandleAddItem is server-authoritative: on a host it runs for every player's pickups.
				// The backpack-first redirect is gated on a LOCAL client UI flag, so resolve who owns
				// this inventory and only redirect the LOCAL player's own pickups. Requiring a confirmed
				// id mismatch keeps single-player/host behaviour unchanged and never blocks the feature
				// if an id cannot be read.
				int ownerId = (_selfField?.GetValue(__instance) as VActor)?.ObjectID ?? 0;
				int localId = Hub.Main?.GetMyAvatar()?.ActorID ?? 0;
				bool ownerIsLocal = ownerId != 0 && ownerId == localId;

#if DEBUG
				MelonLogger.Msg(
					$"[InventoryExpansion][DUPDIAG] HandleAddItem item={itemElement?.ItemMasterID}#{itemElement?.ItemID} " +
					$"byLooting={byLooting} sync={sync} ownerId={ownerId} localId={localId} ownerIsLocal={ownerIsLocal} " +
					$"backpackOpen={BackpackPanelPatch.IsBackpackFullyVisible} currentSlot={__instance.CurrentInventorySlot}");
#endif

				// Confirmed cross-player mismatch (e.g. a remote client's pickup processed on the host):
				// never redirect, let the game place it normally. This is the duplication fix.
				if (ownerId != 0 && localId != 0 && !ownerIsLocal
#if DEBUG
					&& !InventoryExpansionPreferences.DebugDisableOwnerGate
#endif
				)
				{
					return true;
				}

				// Only redirect while the backpack is actually open.
				if (!BackpackPanelPatch.IsBackpackFullyVisible)
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

#if DEBUG
		// Diagnostic only (compiled out of Release). After every successful add, dump the server
		// inventory and count how many slots hold the just-added item. occurrences > 1 means a REAL
		// server-side duplication; occurrences == 1 means there is NO server dup (so any perceived
		// duplication is client-side / visual). This is the decisive runtime check for this bug.
		[HarmonyPostfix]
		[HarmonyPatch("HandleAddItem")]
		private static void HandleAddItem_Postfix_Diag(InventoryController __instance, ItemElement itemElement, MsgErrorCode __result)
		{
			try
			{
				if (itemElement == null || __result != MsgErrorCode.Success)
				{
					return;
				}

				EnsureReflection();
				if (_slotsField == null || _slotsField.GetValue(__instance) is not IDictionary slots)
				{
					return;
				}

				int occurrences = 0;
				var dump = new System.Text.StringBuilder();
				foreach (var keyObj in slots.Keys)
				{
					if (slots[keyObj] is not ItemElement slotItem)
					{
						continue;
					}
					if (dump.Length > 0)
					{
						dump.Append(", ");
					}
					dump.Append(keyObj).Append(':').Append(slotItem.ItemMasterID).Append('#').Append(slotItem.ItemID);
					if (slotItem.ItemID == itemElement.ItemID)
					{
						occurrences++;
					}
				}

				MelonLogger.Msg(
					$"[InventoryExpansion][DUPDIAG] post-add item#{itemElement.ItemID} occurrences={occurrences}" +
					(occurrences > 1 ? "  <-- REAL SERVER DUPLICATE" : "") +
					$" slots=[{dump}]");
			}
			catch (Exception ex)
			{
				MelonLogger.Warning($"[InventoryExpansion][DUPDIAG] postfix diagnostic failed: {ex.Message}");
			}
		}
#endif
	}
}

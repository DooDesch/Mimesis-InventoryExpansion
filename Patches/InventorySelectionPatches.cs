using System;
using System.Reflection;
using HarmonyLib;
using InventoryExpansion.Config;
using InventoryExpansion.Patches;
using MelonLoader;
using Mimic.Actors;

namespace InventoryExpansion.Patches
{
	internal static class InventorySelectionHelper
	{
		internal static Type GetInventoryType()
		{
			return typeof(ProtoActor).GetNestedType("Inventory", BindingFlags.Public | BindingFlags.NonPublic);
		}

		internal static FieldInfo GetSelectedSlotIndexField(Type inventoryType)
		{
			return inventoryType?.GetField("selectedSlotIndex", BindingFlags.Instance | BindingFlags.NonPublic);
		}

		internal static FieldInfo GetSlotSizeField(Type inventoryType)
		{
			return inventoryType?.GetField("slotSize", BindingFlags.Instance | BindingFlags.NonPublic);
		}
	}

	[HarmonyPatch]
	internal static class InventorySelectionNextPatches
	{
		private static MethodBase TargetMethod()
		{
			var inventoryType = InventorySelectionHelper.GetInventoryType();
			return inventoryType?.GetMethod("SelectNextSlot", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		}

		[HarmonyPrefix]
		private static bool SelectNextSlot_Prefix(object __instance)
		{
			return InventorySelectionCommon.HandleSlotSelection(__instance, true);
		}
	}

	[HarmonyPatch]
	internal static class InventorySelectionPreviousPatches
	{
		private static MethodBase TargetMethod()
		{
			var inventoryType = InventorySelectionHelper.GetInventoryType();
			return inventoryType?.GetMethod("SelectPreviousSlot", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		}

		[HarmonyPrefix]
		private static bool SelectPreviousSlot_Prefix(object __instance)
		{
			return InventorySelectionCommon.HandleSlotSelection(__instance, false);
		}
	}

	internal static class InventorySelectionCommon
	{
		internal static bool HandleSlotSelection(object __instance, bool forward)
		{
			try
			{
				if (!InventoryExpansionPreferences.Enabled)
				{
					return true;
				}

				if (__instance == null)
				{
					return true;
				}

				var inventoryType = __instance.GetType();
				var selectedSlotIndexField = InventorySelectionHelper.GetSelectedSlotIndexField(inventoryType);
				var slotSizeField = InventorySelectionHelper.GetSlotSizeField(inventoryType);

				if (selectedSlotIndexField == null || slotSizeField == null)
				{
					return true;
				}

				int currentSlot = (int)(selectedSlotIndexField.GetValue(__instance) ?? 0);
				int slotSize = (int)(slotSizeField.GetValue(__instance) ?? 4);

				bool isBackpackVisible = BackpackPanelPatch.IsBackpackVisible;
				int minSlot, maxSlot;

				if (isBackpackVisible)
				{
					minSlot = 4;
					maxSlot = slotSize - 1;
				}
				else
				{
					minSlot = 0;
					maxSlot = 3;
				}

				if (minSlot > maxSlot || currentSlot < minSlot || currentSlot > maxSlot)
				{
					var selectSlotMethod = inventoryType.GetMethod("SelectSlot", BindingFlags.Instance | BindingFlags.NonPublic);
					if (selectSlotMethod != null)
					{
						selectSlotMethod.Invoke(__instance, new object[] { minSlot });
						return false;
					}
					return true;
				}

				int newSlot;
				if (forward)
				{
					newSlot = currentSlot + 1;
					if (newSlot > maxSlot)
					{
						newSlot = minSlot;
					}
				}
				else
				{
					newSlot = currentSlot - 1;
					if (newSlot < minSlot)
					{
						newSlot = maxSlot;
					}
				}

				var selectSlotMethod2 = inventoryType.GetMethod("SelectSlot", BindingFlags.Instance | BindingFlags.NonPublic);
				if (selectSlotMethod2 != null)
				{
					selectSlotMethod2.Invoke(__instance, new object[] { newSlot });
					return false;
				}

				return true;
			}
			catch (Exception ex)
			{
				MelonLogger.Error($"[InventoryExpansion][Selection] Slot selection prefix failed: {ex}");
				return true;
			}
		}
	}
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using ReluProtocol;
using ReluProtocol.Enum;

namespace InventoryExpansion.Patches
{
	[HarmonyPatch]
	internal static class InventoryDebugControllerPatches
	{
		[HarmonyPostfix]
		[HarmonyPatch(typeof(InventoryController), "Initialize")]
		private static void InventoryController_Initialize_Postfix(InventoryController __instance)
		{
			try
			{
				var field = typeof(InventoryController).GetField("_inventorySlots", BindingFlags.Instance | BindingFlags.NonPublic);
				if (field?.GetValue(__instance) is IDictionary dict)
				{
					var keys = string.Join(",", dict.Keys.Cast<object>());
					MelonLogger.Msg("[InventoryExpansion][Debug] InventoryController.Initialize: slot dict count = " + dict.Count + ", keys = [" + keys + "]");
				}
			}
			catch (Exception ex)
			{
				MelonLogger.Error($"[InventoryExpansion][Debug] InventoryController.Initialize debug failed: {ex}");
			}
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(InventoryController), "GetInventoryInfos")]
		private static void InventoryController_GetInventoryInfos_Postfix(Dictionary<int, ItemInfo> __result)
		{
			try
			{
				if (__result == null)
				{
					MelonLogger.Msg("[InventoryExpansion][Debug] GetInventoryInfos: result is null.");
					return;
				}

				int count = __result.Count;
				int maxKey = __result.Count > 0 ? __result.Keys.Max() : 0;
				MelonLogger.Msg($"[InventoryExpansion][Debug] GetInventoryInfos: {count} entries, max key = {maxKey}.");
			}
			catch (Exception ex)
			{
				MelonLogger.Error($"[InventoryExpansion][Debug] GetInventoryInfos debug failed: {ex}");
			}
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(InventoryController), "HandleAddItem")]
		private static void InventoryController_HandleAddItem_Postfix(InventoryController __instance, MsgErrorCode __result, int addedSlotIndex)
		{
			try
			{
				var field = typeof(InventoryController).GetField("_inventorySlots", BindingFlags.Instance | BindingFlags.NonPublic);
				var dict = field?.GetValue(__instance) as IDictionary;
				int slotCount = dict?.Count ?? -1;
				string keys = dict != null ? string.Join(",", dict.Keys.Cast<object>()) : "null";

				if (__result == MsgErrorCode.InvenFull)
				{
					MelonLogger.Msg("[InventoryExpansion][Debug] HandleAddItem: INVENTORY FULL. slotCount=" + slotCount + ", keys=[" + keys + "]");
				}
				else if (__result == MsgErrorCode.Success)
				{
					MelonLogger.Msg("[InventoryExpansion][Debug] HandleAddItem: Success, addedSlotIndex=" + addedSlotIndex + ", slotCount=" + slotCount + ".");
				}
				else
				{
					MelonLogger.Msg("[InventoryExpansion][Debug] HandleAddItem: Result=" + __result + ", addedSlotIndex=" + addedSlotIndex + ", slotCount=" + slotCount + ".");
				}
			}
			catch (Exception ex)
			{
				MelonLogger.Error($"[InventoryExpansion][Debug] HandleAddItem debug failed: {ex}");
			}
		}

	}

	[HarmonyPatch]
	internal static class InventoryDebugClientPatches
	{
		private static MethodBase TargetMethod()
		{
			try
			{
				var inventoryType = typeof(Mimic.Actors.ProtoActor).GetNestedType("Inventory", BindingFlags.Public | BindingFlags.NonPublic);
				if (inventoryType == null)
				{
					MelonLogger.Error("[InventoryExpansion][Debug] Failed to find nested type ProtoActor.Inventory for ResolveInventoryInfos debug patch.");
					return null;
				}

				var method = inventoryType.GetMethod("ResolveInventoryInfos", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (method == null)
				{
					MelonLogger.Error("[InventoryExpansion][Debug] Failed to find ResolveInventoryInfos method on ProtoActor.Inventory.");
				}
				return method;
			}
			catch (Exception ex)
			{
				MelonLogger.Error("[InventoryExpansion][Debug] TargetMethod for ResolveInventoryInfos failed: " + ex);
				return null;
			}
		}

		[HarmonyPostfix]
		private static void Inventory_ResolveInventoryInfos_Postfix(object __instance, Dictionary<int, ItemInfo> serverItemInfos, int? currentServerSlotIndex)
		{
			try
			{
				if (__instance == null)
				{
					return;
				}

				var type = __instance.GetType();

				var slotSizeField = type.GetField("slotSize", BindingFlags.Instance | BindingFlags.NonPublic);
				int slotSize = (int)(slotSizeField?.GetValue(__instance) ?? 0);

				var slotItemsField = type.GetField("slotItems", BindingFlags.Instance | BindingFlags.NonPublic);
				var slotItems = slotItemsField?.GetValue(__instance) as IList;
				int localItemCount = 0;
				if (slotItems != null)
				{
					foreach (var item in slotItems)
					{
						if (item != null)
						{
							localItemCount++;
						}
					}
				}

				int serverCount = serverItemInfos?.Count ?? 0;
				int serverMaxKey = serverCount > 0 ? serverItemInfos.Keys.Max() : 0;
				string currentSlotStr = currentServerSlotIndex.HasValue ? currentServerSlotIndex.Value.ToString() : "null";

				MelonLogger.Msg("[InventoryExpansion][Debug] ResolveInventoryInfos: slotSize=" + slotSize
					+ ", serverItems=" + serverCount
					+ ", serverMaxKey=" + serverMaxKey
					+ ", localNonNullItems=" + localItemCount
					+ ", currentServerSlotIndex=" + currentSlotStr + ".");
			}
			catch (Exception ex)
			{
				MelonLogger.Error("[InventoryExpansion][Debug] ResolveInventoryInfos debug failed: " + ex);
			}
		}
	}
}



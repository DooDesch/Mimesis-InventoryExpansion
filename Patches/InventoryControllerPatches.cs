using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using InventoryExpansion.Config;
using MelonLoader;

namespace InventoryExpansion.Patches
{
	[HarmonyPatch(typeof(InventoryController))]
	internal static class InventoryControllerPatches
	{
		[HarmonyPostfix]
		[HarmonyPatch("Initialize")]
		private static void Initialize_Postfix(InventoryController __instance)
		{
			try
			{
				if (!InventoryExpansionPreferences.Enabled)
				{
					return;
				}

				var hub = Hub.s;

				FieldInfo gameConfigField = typeof(Hub).GetField("gameConfig", BindingFlags.Instance | BindingFlags.NonPublic);
				object gameConfig = gameConfigField?.GetValue(hub);
				if (gameConfig == null)
				{
					return;
				}

				FieldInfo playerActorField = gameConfig.GetType().GetField("playerActor", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				object playerActor = playerActorField?.GetValue(gameConfig);
				if (playerActor == null)
				{
					return;
				}

				FieldInfo maxSlotField = playerActor.GetType().GetField("maxGenericInventorySlot", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (maxSlotField == null)
				{
					return;
				}

				int maxSlots = (int)(maxSlotField.GetValue(playerActor) ?? 4);
				if (maxSlots <= 4)
				{
					return;
				}

				var field = typeof(InventoryController).GetField("_inventorySlots", BindingFlags.Instance | BindingFlags.NonPublic);
				if (field == null)
				{
					MelonLogger.Warning("InventoryExpansion: failed to find InventoryController._inventorySlots field.");
					return;
				}

				if (field.GetValue(__instance) is not IDictionary dict)
				{
					MelonLogger.Warning("InventoryExpansion: InventoryController._inventorySlots is not an IDictionary.");
					return;
				}

				for (int i = 5; i <= maxSlots; i++)
				{
					if (!dict.Contains(i))
					{
						dict[i] = null;
					}
				}

				MelonLogger.Msg($"InventoryExpansion: InventoryController extended to {maxSlots} slots.");
			}
			catch (Exception ex)
			{
				MelonLogger.Error($"InventoryExpansion: failed to extend InventoryController slots: {ex}");
			}
		}

		[HarmonyPrefix]
		[HarmonyPatch("InvenFull")]
		private static bool InvenFull_Prefix(InventoryController __instance, ref bool __result)
		{
			try
			{
				if (!InventoryExpansionPreferences.Enabled)
				{
					return true;
				}

				var hub = Hub.s;
				if (hub == null)
				{
					return true;
				}

				FieldInfo gameConfigField = typeof(Hub).GetField("gameConfig", BindingFlags.Instance | BindingFlags.NonPublic);
				object gameConfig = gameConfigField?.GetValue(hub);
				FieldInfo playerActorField = gameConfig?.GetType().GetField("playerActor", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				object playerActor = playerActorField?.GetValue(gameConfig);
				FieldInfo maxSlotField = playerActor?.GetType().GetField("maxGenericInventorySlot", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

				int maxSlots = (int)(maxSlotField?.GetValue(playerActor) ?? 4);

				var field = typeof(InventoryController).GetField("_inventorySlots", BindingFlags.Instance | BindingFlags.NonPublic);
				if (field?.GetValue(__instance) is not IDictionary dict)
				{
					return true;
				}

				int filled = 0;
				foreach (DictionaryEntry entry in dict)
				{
					if (entry.Value != null)
					{
						filled++;
					}
				}

				__result = filled >= maxSlots;
				return false;
			}
			catch (Exception ex)
			{
				MelonLogger.Error($"InventoryExpansion: InvenFull prefix failed, falling back to original. {ex}");
				return true;
			}
		}
	}
}

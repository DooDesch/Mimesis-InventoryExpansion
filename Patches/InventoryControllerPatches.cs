using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using InventoryExpansion.Config;
using MelonLoader;

namespace InventoryExpansion.Patches
{
	/// <summary>
	/// Extends the internal InventoryController slot dictionary to match the expanded slot count
	/// and adjusts the InvenFull check to honor the expanded slot count instead of the hardcoded 4.
	/// </summary>
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

				// Access Hub.gameConfig via reflection (internal field)
				FieldInfo gameConfigField = typeof(Hub).GetField("gameConfig", BindingFlags.Instance | BindingFlags.NonPublic);
				object gameConfig = gameConfigField?.GetValue(hub);
				if (gameConfig == null)
				{
					return;
				}

				// Access GameConfig.playerActor
				FieldInfo playerActorField = gameConfig.GetType().GetField("playerActor", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				object playerActor = playerActorField?.GetValue(gameConfig);
				if (playerActor == null)
				{
					return;
				}

				// Access PlayerActor.maxGenericInventorySlot
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

				// Access private Dictionary<int, ItemElement> _inventorySlots via non-generic IDictionary
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

				// Original Initialize adds slots 1..4; we only append missing higher indices.
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

		/// <summary>
		/// Replace the hardcoded "4 slots" InvenFull check with a check based on maxGenericInventorySlot.
		/// This runs on the server/host side and controls whether HandleGrapLootingObject() will even
		/// try to add an item.
		/// </summary>
		[HarmonyPrefix]
		[HarmonyPatch("InvenFull")]
		private static bool InvenFull_Prefix(InventoryController __instance, ref bool __result)
		{
			try
			{
				if (!InventoryExpansionPreferences.Enabled)
				{
					// Let the original logic run unchanged.
					return true;
				}

				var hub = Hub.s;
				if (hub == null)
				{
					return true;
				}

				// Access Hub.gameConfig.playerActor.maxGenericInventorySlot
				FieldInfo gameConfigField = typeof(Hub).GetField("gameConfig", BindingFlags.Instance | BindingFlags.NonPublic);
				object gameConfig = gameConfigField?.GetValue(hub);
				FieldInfo playerActorField = gameConfig?.GetType().GetField("playerActor", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				object playerActor = playerActorField?.GetValue(gameConfig);
				FieldInfo maxSlotField = playerActor?.GetType().GetField("maxGenericInventorySlot", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

				int maxSlots = (int)(maxSlotField?.GetValue(playerActor) ?? 4);

				// Access _inventorySlots and count non-null entries.
				var field = typeof(InventoryController).GetField("_inventorySlots", BindingFlags.Instance | BindingFlags.NonPublic);
				if (field?.GetValue(__instance) is not IDictionary dict)
				{
					return true; // fall back to original if something is off
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
				// Skip original method.
				return false;
			}
			catch (Exception ex)
			{
				MelonLogger.Error($"InventoryExpansion: InvenFull prefix failed, falling back to original. {ex}");
				// Let original run on error.
				return true;
			}
		}
	}
}



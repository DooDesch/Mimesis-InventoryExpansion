using System;
using System.Reflection;
using HarmonyLib;
using InventoryExpansion.Config;
using MelonLoader;

namespace InventoryExpansion.Patches
{
	[HarmonyPatch(typeof(Hub))]
	internal static class GameConfigPatches
	{
		private static bool _slotsAlreadyExpanded;

		[HarmonyPostfix]
		[HarmonyPatch("Awake")]
		private static void Hub_Awake_Postfix(Hub __instance)
		{
			try
			{
				if (!InventoryExpansionPreferences.Enabled)
				{
					return;
				}

				if (_slotsAlreadyExpanded)
				{
					return;
				}

				FieldInfo gameConfigField = typeof(Hub).GetField("gameConfig", BindingFlags.Instance | BindingFlags.NonPublic);
				object gameConfig = gameConfigField?.GetValue(__instance);
				if (gameConfig == null)
				{
					MelonLogger.Warning("InventoryExpansion: gameConfig is null, cannot adjust inventory slot count.");
					return;
				}

				FieldInfo playerActorField = gameConfig.GetType().GetField("playerActor", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				object playerActor = playerActorField?.GetValue(gameConfig);
				if (playerActor == null)
				{
					MelonLogger.Warning("InventoryExpansion: gameConfig.playerActor is null, cannot adjust inventory slot count.");
					return;
				}

				FieldInfo maxSlotField = playerActor.GetType().GetField("maxGenericInventorySlot", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (maxSlotField == null)
				{
					MelonLogger.Warning("InventoryExpansion: PlayerActor.maxGenericInventorySlot field not found.");
					return;
				}

				int baseSlots = (int)(maxSlotField.GetValue(playerActor) ?? 4);
				int additionalSlots = InventoryExpansionPreferences.AdditionalSlots;
				if (additionalSlots <= 0)
				{
					MelonLogger.Msg($"InventoryExpansion: keeping base inventory slots = {baseSlots}.");
					return;
				}

				int newSlots = baseSlots + additionalSlots;
				maxSlotField.SetValue(playerActor, newSlots);
				_slotsAlreadyExpanded = true;
				MelonLogger.Msg($"InventoryExpansion: expanded inventory slots from {baseSlots} to {newSlots}.");
			}
			catch (Exception ex)
			{
				MelonLogger.Error($"InventoryExpansion: failed to adjust inventory slot size: {ex}");
			}
		}
	}
}

using System;
using System.Reflection;
using HarmonyLib;
using InventoryExpansion.Config;
using MelonLoader;
using Mimic.Actors;
using UnityEngine;

namespace InventoryExpansion.Patches
{
	// Block "large" items from being looted into the backpack. A large item is one the game marks
	// ForbidChange - the items you cannot wheel/switch away from while holding them (e.g. two-handed
	// items). While the backpack is open the active slot is a backpack slot, so grabbing such an item
	// would store/hold it from the backpack. We refuse the grab on the CLIENT (LootingLevelObject.TryInteract
	// returns false BEFORE ProtoActor.GrapLootingObject), so the looting object is never assigned and nothing
	// is consumed, and we show a short message in the same style as the game's own "you cannot do that" notices.
	// Controlled by the BlockLargeItemsInBackpack preference (default on); when off, vanilla behaviour is kept.
	[HarmonyPatch(typeof(LootingLevelObject))]
	internal static class LargeItemBackpackPatch
	{
		private static PropertyInfo _tablemanProp;
		private static FieldInfo _uiprefabsField;
		private static bool _reflectionInitialized;
		private static float _lastMessageTime;

		private static void EnsureReflection()
		{
			if (_reflectionInitialized)
			{
				return;
			}
			_reflectionInitialized = true;

			// Hub.tableman is internal, so reach it (and its public uiprefabs field) via reflection.
			_tablemanProp = typeof(Hub).GetProperty("tableman", BindingFlags.Instance | BindingFlags.NonPublic);
		}

		[HarmonyPrefix]
		[HarmonyPatch("TryInteract")]
		private static bool TryInteract_Prefix(LootingLevelObject __instance, ref bool __result)
		{
			try
			{
				if (!InventoryExpansionPreferences.Enabled || !InventoryExpansionPreferences.BlockLargeItemsInBackpack)
				{
					return true;
				}

				// Only relevant while the backpack is open - that is when the active slot is a backpack slot
				// and a picked-up item would end up stored/held from the backpack. Closed backpack: vanilla grab.
				if (!BackpackPanelPatch.IsBackpackFullyVisible)
				{
					return true;
				}

				var masterInfo = ProtoActor.Inventory.GetItemMasterInfo(__instance.itemMasterID);
				if (masterInfo == null || !masterInfo.ForbidChange)
				{
					return true; // not a large item -> normal grab
				}

				ShowTooLargeMessage();
				__result = false; // block the grab; the looting object is not assigned, nothing is consumed
				return false;
			}
			catch (Exception ex)
			{
				MelonLogger.Warning($"[InventoryExpansion][LargeItem] TryInteract prefix failed, allowing default grab: {ex.Message}");
				return true;
			}
		}

		// True when the local avatar is currently holding a large item - one that forbids slot changes
		// (e.g. a two-handed item). Such items occupy the hand, so the backpack should be inaccessible.
		internal static bool IsLargeHeldItem(ProtoActor actor)
		{
			try
			{
				return actor?.GetSelectedInventoryItem()?.MasterInfo?.ForbidChange == true;
			}
			catch
			{
				return false;
			}
		}

		private static void ShowTooLargeMessage()
		{
			ShowBackpackBlockedMessage("This is too large for your backpack.");
		}

		// Shows a short toast in the same style as the game's own "you cannot do that" notices.
		internal static void ShowBackpackBlockedMessage(string message)
		{
			try
			{
				// Light throttle so a held key cannot spam the toast.
				float now = Time.unscaledTime;
				if (now - _lastMessageTime < 1f)
				{
					return;
				}
				_lastMessageTime = now;

				EnsureReflection();
				object hub = Hub.s;
				object tableman = (hub != null) ? _tablemanProp?.GetValue(hub) : null;
				if (tableman == null)
				{
					return;
				}

				if (_uiprefabsField == null)
				{
					_uiprefabsField = tableman.GetType().GetField("uiprefabs", BindingFlags.Instance | BindingFlags.Public);
				}

				if (_uiprefabsField?.GetValue(tableman) is MMUIPrefabTable prefabs)
				{
					prefabs.ShowTimerDialog("ToastSimple", 0f, message);
				}
			}
			catch (Exception ex)
			{
				MelonLogger.Warning($"[InventoryExpansion][LargeItem] showing message failed: {ex.Message}");
			}
		}
	}
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using InventoryExpansion.Config;
using MelonLoader;
using Mimic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InventoryExpansion.Patches
{
	[HarmonyPatch(typeof(UIPrefab_Inventory))]
	internal static class InventoryUiPatches
	{
		[HarmonyPostfix]
		[HarmonyPatch("Awake")]
		private static void Awake_Postfix(UIPrefab_Inventory __instance)
		{
			try
			{
				if (!InventoryExpansionPreferences.Enabled)
				{
					return;
				}

				int maxLogicalSlots = 4;
				try
				{
					var hub = Hub.s;
					if (hub != null)
					{
						var gameConfigField = typeof(Hub).GetField("gameConfig", BindingFlags.Instance | BindingFlags.NonPublic);
						object gameConfig = gameConfigField?.GetValue(hub);
						var playerActorField = gameConfig?.GetType().GetField("playerActor", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
						object playerActor = playerActorField?.GetValue(gameConfig);
						var maxSlotField = playerActor?.GetType().GetField("maxGenericInventorySlot", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
						if (maxSlotField != null)
						{
							maxLogicalSlots = (int)(maxSlotField.GetValue(playerActor) ?? 4);
						}
					}
				}
				catch
				{
					maxLogicalSlots = 4;
				}

				int desiredVisibleSlots = Math.Min(maxLogicalSlots, 4 + InventoryExpansionPreferences.AdditionalSlots);

				var slotsField = typeof(UIPrefab_Inventory).GetField("inventorySlots", BindingFlags.Instance | BindingFlags.NonPublic);
				if (slotsField == null)
				{
					return;
				}

				if (slotsField.GetValue(__instance) is not IList inventorySlots)
				{
					return;
				}

				int existingCount = inventorySlots.Count;
				if (existingCount == 0 || existingCount >= desiredVisibleSlots)
				{
					return;
				}

				var slotType = inventorySlots[0].GetType();
				var frameField = slotType.GetField("frame", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				var imageField = slotType.GetField("image", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				var stackField = slotType.GetField("stackCount", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				var waitEventField = slotType.GetField("waitEvent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

				if (frameField == null || imageField == null || stackField == null || waitEventField == null)
				{
					return;
				}

				var firstFrame = frameField.GetValue(inventorySlots[0]) as Image;
				var firstImage = imageField.GetValue(inventorySlots[0]) as Image;
				var firstStack = stackField.GetValue(inventorySlots[0]) as TMP_Text;
				var firstWait = waitEventField.GetValue(inventorySlots[0]) as Transform;

				if (firstFrame == null || firstImage == null || firstStack == null || firstWait == null)
				{
					return;
				}

				var firstFrameRT = firstFrame.rectTransform;
				var firstImageRT = firstImage.rectTransform;
				var firstStackRT = firstStack.rectTransform;

				float horizontalSpacing = 0f;
				if (existingCount > 1)
				{
					var secondFrame = frameField.GetValue(inventorySlots[1]) as Image;
					if (secondFrame != null)
					{
						horizontalSpacing = secondFrame.rectTransform.anchoredPosition.x - firstFrameRT.anchoredPosition.x;
					}
				}
				if (Mathf.Approximately(horizontalSpacing, 0f))
				{
					horizontalSpacing = firstFrameRT.sizeDelta.x + 10f;
				}

				float verticalSpacing = firstFrameRT.sizeDelta.y + 10f;
				const int maxSlotsPerRow = 8;

				var slotsParent = firstFrameRT.parent as RectTransform;

				for (int index = existingCount; index < desiredVisibleSlots; index++)
				{
					int templateIndex = existingCount - 1;
					var templateSlot = inventorySlots[templateIndex];
					var templateFrame = frameField.GetValue(templateSlot) as Image;
					var templateImage = imageField.GetValue(templateSlot) as Image;
					var templateStack = stackField.GetValue(templateSlot) as TMP_Text;
					var templateWait = waitEventField.GetValue(templateSlot) as Transform;

					if (templateFrame == null || templateImage == null || templateStack == null || templateWait == null)
					{
						continue;
					}

					int extraIndex = index - existingCount;
					int row = extraIndex / maxSlotsPerRow;
					int column = extraIndex % maxSlotsPerRow;

					var frameGO = new GameObject(templateFrame.gameObject.name + "_Extra" + index);
					frameGO.transform.SetParent(slotsParent, false);
					var newFrame = frameGO.AddComponent<Image>();
					newFrame.sprite = templateFrame.sprite;
					newFrame.type = templateFrame.type;
					newFrame.material = templateFrame.material;
					newFrame.color = templateFrame.color;

					var frameRT = newFrame.rectTransform;
					frameRT.anchorMin = firstFrameRT.anchorMin;
					frameRT.anchorMax = firstFrameRT.anchorMax;
					frameRT.pivot = firstFrameRT.pivot;
					frameRT.sizeDelta = firstFrameRT.sizeDelta;

					var lastFrameRT = (frameField.GetValue(inventorySlots[existingCount - 1]) as Image)?.rectTransform ?? firstFrameRT;
					float baseX = lastFrameRT.anchoredPosition.x + horizontalSpacing;
					float baseY = firstFrameRT.anchoredPosition.y;

					float x = baseX - column * horizontalSpacing;
					float y = baseY + row * verticalSpacing;
					frameRT.anchoredPosition = new Vector2(x, y);

					var iconGO = new GameObject(templateImage.gameObject.name + "_Extra" + index);
					iconGO.transform.SetParent(frameGO.transform, false);
					var iconImage = iconGO.AddComponent<Image>();
					iconImage.sprite = templateImage.sprite;
					iconImage.type = templateImage.type;
					iconImage.color = templateImage.color;
					iconImage.material = templateImage.material;

					var iconRT = iconImage.rectTransform;
					var templateImageRT = templateImage.rectTransform;
					iconRT.anchorMin = templateImageRT.anchorMin;
					iconRT.anchorMax = templateImageRT.anchorMax;
					iconRT.pivot = templateImageRT.pivot;
					iconRT.sizeDelta = templateImageRT.sizeDelta;
					iconRT.anchoredPosition = templateImageRT.anchoredPosition;

					var stackGO = new GameObject(templateStack.gameObject.name + "_Extra" + index);
					stackGO.transform.SetParent(frameGO.transform, false);
					var stackComponent = stackGO.AddComponent(templateStack.GetType()) as TMP_Text;
					if (stackComponent == null)
					{
						MelonLogger.Error("[InventoryExpansion][UI] Failed to create TMP_Text for extra slot " + index + "; skipping this slot.");
						UnityEngine.Object.Destroy(frameGO);
						continue;
					}
					stackComponent.text = string.Empty;
					stackComponent.font = templateStack.font;
					stackComponent.fontSize = templateStack.fontSize;
					stackComponent.alignment = templateStack.alignment;
					stackComponent.color = templateStack.color;

					var templateStackRT = templateStack.rectTransform;
					var stackRT = stackComponent.rectTransform;
					stackRT.anchorMin = templateStackRT.anchorMin;
					stackRT.anchorMax = templateStackRT.anchorMax;
					stackRT.pivot = templateStackRT.pivot;
					stackRT.sizeDelta = templateStackRT.sizeDelta;
					stackRT.anchoredPosition = templateStackRT.anchoredPosition;

					var waitGO = new GameObject(templateWait.gameObject.name + "_Extra" + index);
					waitGO.transform.SetParent(frameGO.transform, false);
					var waitRT = waitGO.AddComponent<RectTransform>();
					var templateWaitRT = templateWait as RectTransform;
					if (templateWaitRT != null)
					{
						waitRT.anchorMin = templateWaitRT.anchorMin;
						waitRT.anchorMax = templateWaitRT.anchorMax;
						waitRT.pivot = templateWaitRT.pivot;
						waitRT.sizeDelta = templateWaitRT.sizeDelta;
						waitRT.anchoredPosition = templateWaitRT.anchoredPosition;
					}

					var newSlot = Activator.CreateInstance(slotType);
					frameField.SetValue(newSlot, newFrame);
					imageField.SetValue(newSlot, iconImage);
					stackField.SetValue(newSlot, stackComponent);
					waitEventField.SetValue(newSlot, waitRT.transform);

					inventorySlots.Add(newSlot);
				}

				MelonLogger.Msg("[InventoryExpansion][UI] Extended inventorySlots from " + existingCount + " to " + inventorySlots.Count + " visual slots.");
			}
			catch (Exception ex)
			{
				MelonLogger.Error("[InventoryExpansion][UI] Awake postfix failed while extending slots: " + ex);
			}
		}
	}
}

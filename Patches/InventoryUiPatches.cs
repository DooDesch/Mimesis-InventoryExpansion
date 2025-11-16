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
	/// <summary>
	/// Prevents crashes in the inventory UI when the logical slot count exceeds
	/// the number of visual slots defined in the prefab.
	/// For now this keeps the UI at its original size and simply ignores
	/// any extra logical slots beyond the available UI elements.
	/// </summary>
	[HarmonyPatch(typeof(UIPrefab_Inventory))]
	internal static class InventoryUiPatches
	{
		private static bool _loggedInitialLayout;

		private static string GetPath(Transform t)
		{
			if (t == null)
			{
				return "<null>";
			}

			var names = new System.Collections.Generic.List<string>();
			while (t != null)
			{
				names.Add(t.name);
				t = t.parent;
			}
			names.Reverse();
			return string.Join("/", names);
		}

		private static void LogSlotLayout(UIPrefab_Inventory ui)
		{
			try
			{
				if (_loggedInitialLayout)
				{
					return;
				}

				_loggedInitialLayout = true;

				MelonLogger.Msg("[InventoryExpansion][UI Debug] --- Logging original inventory slot layout ---");

				void LogFrame(string label, Image frame, Image image, TMP_Text stack, Transform waitEvent)
				{
					if (frame == null || image == null || stack == null || waitEvent == null)
					{
						MelonLogger.Msg($"[UI Debug] {label}: frame/image/stack/waitEvent is null.");
						return;
					}

					var frt = frame.rectTransform;
					var irt = image.rectTransform;
					var srt = stack.rectTransform;
					var wrt = waitEvent as RectTransform;

					MelonLogger.Msg(
						$"[UI Debug] {label}: frame='{frame.name}' pos={frt.anchoredPosition} size={frt.sizeDelta} " +
						$"anchorMin={frt.anchorMin} anchorMax={frt.anchorMax} pivot={frt.pivot} path={GetPath(frame.transform)}");

					MelonLogger.Msg(
						$"[UI Debug] {label}: image='{image.name}' pos={irt.anchoredPosition} size={irt.sizeDelta} path={GetPath(image.transform)}");

					MelonLogger.Msg(
						$"[UI Debug] {label}: stack='{stack.name}' pos={srt.anchoredPosition} size={srt.sizeDelta} path={GetPath(stack.transform)}");

					if (wrt != null)
					{
						MelonLogger.Msg(
							$"[UI Debug] {label}: waitEvent='{waitEvent.name}' pos={wrt.anchoredPosition} size={wrt.sizeDelta} path={GetPath(waitEvent)}");
					}
					else
					{
						MelonLogger.Msg(
							$"[UI Debug] {label}: waitEvent='{waitEvent.name}' (no RectTransform) path={GetPath(waitEvent)}");
					}
				}

				LogFrame("Slot1", ui.UE_InvenFrame1, ui.UE_InvenImage1, ui.UE_stackCount1, ui.UE_InvenWaitEvent1);
				LogFrame("Slot2", ui.UE_InvenFrame2, ui.UE_InvenImage2, ui.UE_stackCount2, ui.UE_InvenWaitEvent2);
				LogFrame("Slot3", ui.UE_InvenFrame3, ui.UE_InvenImage3, ui.UE_stackCount3, ui.UE_InvenWaitEvent3);
				LogFrame("Slot4", ui.UE_InvenFrame4, ui.UE_InvenImage4, ui.UE_stackCount4, ui.UE_InvenWaitEvent4);

				MelonLogger.Msg("[InventoryExpansion][UI Debug] --- End of original layout dump ---");
			}
			catch (Exception ex)
			{
				MelonLogger.Error("[InventoryExpansion][UI Debug] Failed to log original layout: " + ex);
			}
		}

		/// <summary>
		/// After the original Awake has created the initial 4 slots, extend the visual slot list
		/// by cloning the existing UI elements so we can display additional inventory slots
		/// (up to a fixed maximum).
		/// </summary>
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

				// Log original layout once so we can reason about anchors/positions in logs.
				LogSlotLayout(__instance);

				// Determine how many slots the game wants logically.
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
					// fall back to 4 if anything goes wrong
					maxLogicalSlots = 4;
				}

				// Visible slots match the logical slot count, but we still keep a hard cap
				// so the UI can't explode completely if someone configures something silly.
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

				// Use the first slot's components as a template for extra slots.
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

				// Ensure we have a dedicated "backpack" panel in the bottom-right corner of
				// the screen that we can later use as anchor for additional inventory UI.
				// TEMPORARILY DISABLED - wird in einem separaten Patch erstellt, um die Inventar-UI nicht zu stören
				/*
				if (_backpackPanel == null)
				{
					// Finde das Canvas im Hierarchiebaum.
					Canvas canvas = null;
					Transform current = firstFrameRT.transform;
					while (current != null)
					{
						canvas = current.GetComponent<Canvas>();
						if (canvas != null)
						{
							break;
						}
						current = current.parent;
					}

					if (canvas != null)
					{
						var panelGO = new GameObject("InventoryExpansion_BackpackPanel");
						panelGO.transform.SetParent(canvas.transform, false);

						var image = panelGO.AddComponent<Image>();
						// Schwarzes Rechteck unten rechts
						image.color = new Color(0f, 0f, 0f, 0.8f); // schwarz, 80% Alpha
						// WICHTIG: raycastTarget = false, damit das Panel keine Input-Events blockiert!
						image.raycastTarget = false;

						_backpackPanel = panelGO.AddComponent<RectTransform>();
						
						// Positionierung unten rechts: Anchors auf (1,0) = rechts unten
						_backpackPanel.anchorMin = new Vector2(1f, 0f);
						_backpackPanel.anchorMax = new Vector2(1f, 0f);
						_backpackPanel.pivot = new Vector2(1f, 0f); // Pivot rechts unten
						
						// Größe des Panels
						_backpackPanel.sizeDelta = new Vector2(450f, 200f);
						
						// Position: Von rechts 40px, von unten 40px
						// anchoredPosition ist relativ zu den Anchors, also negative X-Werte = links vom Anchor
						_backpackPanel.anchoredPosition = new Vector2(-40f, 40f);

						// Stelle sicher, dass das Panel NICHT die Input-Events blockiert
						// Setze es als erstes Child (hinten), nicht als letztes (vorne)
						panelGO.transform.SetAsFirstSibling();

						MelonLogger.Msg("[InventoryExpansion][UI] Created BLACK BACKPACK PANEL at bottom-right. Canvas: {0}, Size: {1}, Position: {2}, Anchors: min={3} max={4}", 
							canvas.name, _backpackPanel.sizeDelta, _backpackPanel.anchoredPosition, 
							_backpackPanel.anchorMin, _backpackPanel.anchorMax);
					}
					else
					{
						MelonLogger.Error("[InventoryExpansion][UI] Failed to find Canvas in hierarchy! Path: {0}", GetPath(firstFrameRT.transform));
					}
				}
				else
				{
					MelonLogger.Msg("[InventoryExpansion][UI] Backpack panel already exists, reusing it.");
				}
				*/
				// Determine horizontal spacing between the original slots so we can
				// append extra slots to the right in the same area.
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
					// Fallback: use frame width plus a small gap.
					horizontalSpacing = firstFrameRT.sizeDelta.x + 10f;
				}

				// Spacing for additional rows (above the main row).
				float verticalSpacing = firstFrameRT.sizeDelta.y + 10f;
				const int maxSlotsPerRow = 8;

				// Extra slots share the same parent as the original four so they stay
				// anchored in the same HUD area.
				var slotsParent = firstFrameRT.parent as RectTransform;

				// Create additional slots so that inventorySlots.Count == desiredVisibleSlots.
				// These extra slots are laid out to the right of the existing bar; rows go
				// from bottom to top, and within each row from right to left.
				for (int index = existingCount; index < desiredVisibleSlots; index++)
				{
					// Use the last existing slot as template for visuals & behaviour.
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

					// Index of the extra slot relative to the first extra slot.
					int extraIndex = index - existingCount;
					int row = extraIndex / maxSlotsPerRow;      // 0 = bottom row
					int column = extraIndex % maxSlotsPerRow;   // 0 = rightmost slot

					// Create a new frame GameObject under the same inventory parent.
					var frameGO = new GameObject(templateFrame.gameObject.name + "_Extra" + index);
					frameGO.transform.SetParent(slotsParent, false);
					var newFrame = frameGO.AddComponent<Image>();
					newFrame.sprite = templateFrame.sprite;
					newFrame.type = templateFrame.type;
					newFrame.material = templateFrame.material;

					// Visuelles Debugging: Extra-Slots farbig und halbtransparent hinterlegen,
					// damit wir ihre Position im HUD besser erkennen.
					// (Wir ändern nur die Farbe, nicht das Sprite/Material.)
					Color debugColor = new Color(1f, 0f, 0f, 0.2f); // leicht rötlich, ~20% Alpha
					newFrame.color = debugColor;

					var frameRT = newFrame.rectTransform;
					// Use the same anchoring as the original frames so they move with the
					// same HUD area.
					frameRT.anchorMin = firstFrameRT.anchorMin;
					frameRT.anchorMax = firstFrameRT.anchorMax;
					frameRT.pivot = firstFrameRT.pivot;
					frameRT.sizeDelta = firstFrameRT.sizeDelta;

					// Base position: start just to the right of the last original slot.
					var lastFrameRT = (frameField.GetValue(inventorySlots[existingCount - 1]) as Image)?.rectTransform ?? firstFrameRT;
					float baseX = lastFrameRT.anchoredPosition.x + horizontalSpacing;
					float baseY = firstFrameRT.anchoredPosition.y; // align with main row for first extra row

					// Columns extend to the left, rows upwards.
					float x = baseX - column * horizontalSpacing;
					float y = baseY + row * verticalSpacing;
					frameRT.anchoredPosition = new Vector2(x, y);

					// Create icon image as child of the frame.
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
					// Keep the same local offset inside the frame as the template.
					iconRT.anchoredPosition = templateImageRT.anchoredPosition;

					// Create stack count text as child of the frame.
					var stackGO = new GameObject(templateStack.gameObject.name + "_Extra" + index);
					stackGO.transform.SetParent(frameGO.transform, false);
					// Use the same concrete TMP_Text type as the template (e.g. TextMeshProUGUI)
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
					// Note: enableWordWrapping is obsolete, but we skip it as it's not critical

					var templateStackRT = templateStack.rectTransform;
					var stackRT = stackComponent.rectTransform;
					stackRT.anchorMin = templateStackRT.anchorMin;
					stackRT.anchorMax = templateStackRT.anchorMax;
					stackRT.pivot = templateStackRT.pivot;
					stackRT.sizeDelta = templateStackRT.sizeDelta;
					stackRT.anchoredPosition = templateStackRT.anchoredPosition;

					// Create waitEvent transform as child of the frame.
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

					// Create a new Slot instance and wire it up.
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

		// Note: We rely on the original UpdateSlot implementation.
		// Our Awake postfix extends inventorySlots so that the original logic
		// can safely iterate up to Hub.s.gameConfig.playerActor.maxGenericInventorySlot
		// without hitting IndexOutOfRange.
	}
}



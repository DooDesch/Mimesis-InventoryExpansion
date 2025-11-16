using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using InventoryExpansion.Config;
using MelonLoader;
using Mimic.Actors;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace InventoryExpansion.Patches
{
	[HarmonyPatch]
	internal static class BackpackPanelPatch
	{
		private static GameObject _rootObj;
		private static GameObject _canvasObj;
		private static RectTransform _backpackPanel;
		private static bool _slotsMoved = false;
		private static bool _backpackVisible = false;

		[HarmonyPostfix]
		[HarmonyPatch(typeof(UIPrefab_Inventory), "Awake")]
		private static void UIPrefab_Inventory_Awake_Postfix(UIPrefab_Inventory __instance)
		{
			try
			{
				if (!InventoryExpansionPreferences.Enabled)
				{
					return;
				}

				if (_backpackPanel != null)
				{
					if (!_slotsMoved)
					{
						MelonCoroutines.Start(MoveSlotsToPanelCoroutine(__instance));
					}
					return;
				}

				CreateRoot();
				CreateUI();
				MelonCoroutines.Start(MoveSlotsToPanelCoroutine(__instance));

				SetBackpackVisibility(false);
				MelonLogger.Msg("[InventoryExpansion][BackpackPanel] Created BLACK BACKPACK PANEL at bottom-right.");
			}
			catch (Exception ex)
			{
				MelonLogger.Error("[InventoryExpansion][BackpackPanel] Failed to create backpack panel: " + ex);
			}
		}

		private static void CreateRoot()
		{
			if (_rootObj != null) return;

			_rootObj = new GameObject("InventoryExpansion_Root");
			UnityEngine.Object.DontDestroyOnLoad(_rootObj);
		}

		private static void CreateUI()
		{
			if (_canvasObj != null) return;
			if (_rootObj == null) CreateRoot();

			_canvasObj = new GameObject("InventoryExpansion_Canvas");
			_canvasObj.transform.SetParent(_rootObj.transform, false);

			var canvas = _canvasObj.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			
			var scaler = _canvasObj.AddComponent<CanvasScaler>();
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
			scaler.scaleFactor = 1f;
			
			Canvas mainCanvas = null;
			var allCanvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
			foreach (var c in allCanvases)
			{
				if (c.renderMode == RenderMode.ScreenSpaceOverlay && c.name.Contains("Canvas") && !c.name.Contains("InventoryExpansion"))
				{
					mainCanvas = c;
					break;
				}
			}
			
			if (mainCanvas != null)
			{
				canvas.sortingOrder = mainCanvas.sortingOrder + 1;
				MelonLogger.Msg("[InventoryExpansion][BackpackPanel] Using Constant Pixel Size scaling. Main canvas: {0}, Screen resolution: {1}x{2}", 
					mainCanvas.name, Screen.width, Screen.height);
			}
			
			_canvasObj.AddComponent<GraphicRaycaster>();

			var panelObj = new GameObject("InventoryExpansion_BackpackPanel");
			panelObj.transform.SetParent(_canvasObj.transform, false);

			var image = panelObj.AddComponent<Image>();
			image.color = new Color(0f, 0f, 0f, 0.8f);
			image.raycastTarget = false;

			_backpackPanel = panelObj.GetComponent<RectTransform>();
			_backpackPanel.anchorMin = new Vector2(1f, 0f);
			_backpackPanel.anchorMax = new Vector2(1f, 0f);
			_backpackPanel.pivot = new Vector2(1f, 0f);
			_backpackPanel.sizeDelta = new Vector2(450f, 200f);
			_backpackPanel.anchoredPosition = new Vector2(-40f, 40f);
		}


		internal static void SetBackpackVisibility(bool visible)
		{
			if (_backpackPanel == null) return;

			_backpackVisible = visible;
			_backpackPanel.gameObject.SetActive(visible);
		}

		internal static bool IsBackpackVisible => _backpackVisible;

		private static IEnumerator MoveSlotsToPanelCoroutine(UIPrefab_Inventory inventoryUI)
		{
			yield return null;

			try
			{
				if (_backpackPanel == null || _slotsMoved)
				{
					yield break;
				}

				var slotsField = typeof(UIPrefab_Inventory).GetField("inventorySlots", BindingFlags.Instance | BindingFlags.NonPublic);
				if (slotsField == null)
				{
					MelonLogger.Error("[InventoryExpansion][BackpackPanel] Could not find inventorySlots field!");
					yield break;
				}

				var inventorySlots = slotsField.GetValue(inventoryUI) as System.Collections.IList;
				if (inventorySlots == null || inventorySlots.Count <= 4)
				{
					yield break;
				}

				var slotType = inventorySlots[0].GetType();
				var frameField = slotType.GetField("frame", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (frameField == null)
				{
					MelonLogger.Error("[InventoryExpansion][BackpackPanel] Could not find frame field in slot!");
					yield break;
				}

				var firstSlot = inventorySlots[0];
				var firstFrame = frameField.GetValue(firstSlot) as Image;
				if (firstFrame == null)
				{
					MelonLogger.Error("[InventoryExpansion][BackpackPanel] Could not get first frame!");
					yield break;
				}

				var firstFrameRT = firstFrame.rectTransform;
				float frameWidth = firstFrameRT.sizeDelta.x;
				float frameHeight = firstFrameRT.sizeDelta.y;
				
				const float scaleFactor = 0.75f;
				frameWidth *= scaleFactor;
				frameHeight *= scaleFactor;
				
				float containerWidth = frameWidth;
				float containerHeight = frameHeight;
				Image templateBG = null;
				var originalSlotContainer = firstFrameRT.parent;
				if (originalSlotContainer != null)
				{
					for (int bgIdx = 0; bgIdx < originalSlotContainer.childCount; bgIdx++)
					{
						var child = originalSlotContainer.GetChild(bgIdx);
						if (child.name.Contains("InvenBG") || child.name.Contains("BG"))
						{
							templateBG = child.GetComponent<Image>();
							if (templateBG != null)
							{
								var bgRT = templateBG.rectTransform;
								if (bgRT != null)
								{
									containerWidth = bgRT.sizeDelta.x;
									containerHeight = bgRT.sizeDelta.y;
								}
								break;
							}
						}
					}
				}
				
				float slotSpacing = 10f;
				int additionalSlots = inventorySlots.Count - 4;
				
				int slotsPerRow;
				if (additionalSlots == 4)
				{
					slotsPerRow = 2;
				}
				else if (additionalSlots == 9)
				{
					slotsPerRow = 3;
				}
				else if (additionalSlots == 16)
				{
					slotsPerRow = 4;
				}
				else
				{
					slotsPerRow = Mathf.CeilToInt(Mathf.Sqrt(additionalSlots));
				}

				int rows = Mathf.CeilToInt((float)additionalSlots / slotsPerRow);

				float panelWidth = slotsPerRow * frameWidth + (slotsPerRow + 1) * slotSpacing;
				float panelHeight = rows * frameHeight + (rows + 1) * slotSpacing;
				_backpackPanel.sizeDelta = new Vector2(panelWidth, panelHeight);

				var imageField = slotType.GetField("image", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				var stackField = slotType.GetField("stackCount", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

				var templateImage = imageField?.GetValue(firstSlot) as Image;
				var templateStack = stackField?.GetValue(firstSlot) as TMP_Text;
				var templateImageRT = templateImage?.rectTransform;
				var templateStackRT = templateStack?.rectTransform;

				for (int i = 4; i < inventorySlots.Count; i++)
				{
					var slot = inventorySlots[i];
					var frame = frameField.GetValue(slot) as Image;
					if (frame == null) continue;

					var frameRT = frame.rectTransform;
					bool isExtraSlot = frame.gameObject.name.Contains("_Extra");
					
					if (!isExtraSlot)
					{
						continue;
					}
					
					var slotContainerGO = new GameObject("InventoryExpansion_SlotContainer_" + i);
					slotContainerGO.transform.SetParent(_backpackPanel, false);
					var containerRT = slotContainerGO.AddComponent<RectTransform>();
					
					int slotIndex = i - 4;
					int row = slotIndex / slotsPerRow;
					int col = slotIndex % slotsPerRow;

					containerRT.anchorMin = new Vector2(0f, 1f);
					containerRT.anchorMax = new Vector2(0f, 1f);
					containerRT.pivot = new Vector2(0f, 1f);

					float x = slotSpacing + col * (frameWidth + slotSpacing);
					float y = -(slotSpacing + row * (frameHeight + slotSpacing));
					containerRT.anchoredPosition = new Vector2(x, y);
					containerRT.sizeDelta = new Vector2(frameWidth, frameHeight);
					
					if (templateBG != null)
					{
						var bgGO = new GameObject("InvenBG_Extra" + i);
						bgGO.transform.SetParent(slotContainerGO.transform, false);
						var bgImage = bgGO.AddComponent<Image>();
						bgImage.sprite = templateBG.sprite;
						bgImage.material = templateBG.material;
						bgImage.color = templateBG.color;
						bgImage.type = templateBG.type;
						bgImage.preserveAspect = templateBG.preserveAspect;
						bgImage.fillMethod = templateBG.fillMethod;
						bgImage.fillAmount = templateBG.fillAmount;
						bgImage.fillCenter = templateBG.fillCenter;
						bgImage.fillClockwise = templateBG.fillClockwise;
						bgImage.fillOrigin = templateBG.fillOrigin;
						bgImage.raycastTarget = templateBG.raycastTarget;
						bgImage.maskable = templateBG.maskable;
						
						var bgRT = bgImage.rectTransform;
						var templateBGRT = templateBG.rectTransform;
						bgRT.anchorMin = templateBGRT.anchorMin;
						bgRT.anchorMax = templateBGRT.anchorMax;
						bgRT.pivot = templateBGRT.pivot;
						bgRT.sizeDelta = templateBGRT.sizeDelta * scaleFactor;
						bgRT.anchoredPosition = templateBGRT.anchoredPosition * scaleFactor;
					}
					
					frameRT.SetParent(slotContainerGO.transform, false);

					var templateFrameRT = firstFrame.rectTransform;
					frameRT.anchorMin = new Vector2(0.5f, 0.5f);
					frameRT.anchorMax = new Vector2(0.5f, 0.5f);
					frameRT.pivot = templateFrameRT.pivot;
					frameRT.sizeDelta = templateFrameRT.sizeDelta * scaleFactor;
					frameRT.anchoredPosition = Vector2.zero;

					frame.sprite = firstFrame.sprite;
					frame.material = firstFrame.material;
					frame.color = firstFrame.color;
					frame.type = firstFrame.type;
					frame.preserveAspect = firstFrame.preserveAspect;
					frame.fillMethod = firstFrame.fillMethod;
					frame.fillAmount = firstFrame.fillAmount;
					frame.fillCenter = firstFrame.fillCenter;
					frame.fillClockwise = firstFrame.fillClockwise;
					frame.fillOrigin = firstFrame.fillOrigin;

					if (imageField != null && templateImage != null && templateImageRT != null)
					{
						var iconImage = imageField.GetValue(slot) as Image;
						if (iconImage != null)
						{
							var iconRT = iconImage.rectTransform;
							iconImage.sprite = templateImage.sprite;
							iconImage.material = templateImage.material;
							iconImage.color = templateImage.color;
							iconImage.type = templateImage.type;
							iconImage.preserveAspect = templateImage.preserveAspect;
							iconImage.fillMethod = templateImage.fillMethod;
							iconImage.fillAmount = templateImage.fillAmount;
							iconImage.fillCenter = templateImage.fillCenter;
							iconImage.fillClockwise = templateImage.fillClockwise;
							iconImage.fillOrigin = templateImage.fillOrigin;
							
							iconRT.anchorMin = templateImageRT.anchorMin;
							iconRT.anchorMax = templateImageRT.anchorMax;
							iconRT.pivot = templateImageRT.pivot;
							iconRT.sizeDelta = templateImageRT.sizeDelta * scaleFactor;
							iconRT.anchoredPosition = templateImageRT.anchoredPosition * scaleFactor;
						}
					}

					if (stackField != null && templateStackRT != null)
					{
						var stackText = stackField.GetValue(slot) as TMP_Text;
						if (stackText != null)
						{
							var stackRT = stackText.rectTransform;
							stackRT.anchorMin = templateStackRT.anchorMin;
							stackRT.anchorMax = templateStackRT.anchorMax;
							stackRT.pivot = templateStackRT.pivot;
							stackRT.sizeDelta = templateStackRT.sizeDelta * scaleFactor;
							stackRT.anchoredPosition = templateStackRT.anchoredPosition * scaleFactor;
							stackText.fontSize = templateStack.fontSize * scaleFactor;
						}
					}
				}

				_slotsMoved = true;
				MelonLogger.Msg("[InventoryExpansion][BackpackPanel] Moved {0} additional slots to panel. Panel size: {1}", 
					additionalSlots, _backpackPanel.sizeDelta);
			}
			catch (Exception ex)
			{
				MelonLogger.Error("[InventoryExpansion][BackpackPanel] Failed to move slots to panel: " + ex);
			}
		}
	}

	[HarmonyPatch(typeof(ProtoActor), "Update")]
	internal static class BackpackInputUpdatePatch
	{
		private static bool wasKeyPressedLastFrame = false;

		[HarmonyPostfix]
		private static void Update_Postfix(ProtoActor __instance)
		{
			try
			{
				if (!InventoryExpansionPreferences.Enabled)
				{
					return;
				}

				if (!__instance.AmIAvatar())
				{
					return;
				}

				Keyboard keyboard = Keyboard.current;
				if (keyboard == null)
				{
					wasKeyPressedLastFrame = false;
					return;
				}

				KeyCode targetKey = InventoryExpansionPreferences.BackpackKey;
				Key key = Key.None;

				try
				{
					key = (Key)Enum.Parse(typeof(Key), targetKey.ToString());
				}
				catch
				{
					MelonLogger.Warning($"[InventoryExpansion][BackpackPanel] Could not convert KeyCode {targetKey} to Input System Key");
					return;
				}

				bool isKeyPressed = keyboard[key].isPressed;
				bool wasKeyPressedThisFrame = isKeyPressed && !wasKeyPressedLastFrame;
				wasKeyPressedLastFrame = isKeyPressed;

				if (wasKeyPressedThisFrame)
				{
					bool currentVisibility = BackpackPanelPatch.IsBackpackVisible;
					MelonLogger.Msg($"[InventoryExpansion][BackpackPanel] Toggle key pressed. Current visibility: {currentVisibility}");
					BackpackPanelPatch.SetBackpackVisibility(!currentVisibility);
					MelonLogger.Msg($"[InventoryExpansion][BackpackPanel] New visibility: {!currentVisibility}");
				}
			}
			catch (Exception ex)
			{
				MelonLogger.Error($"[InventoryExpansion][BackpackPanel] Update postfix failed: {ex}");
			}
		}
	}
}
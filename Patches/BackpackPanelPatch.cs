using System;
using System.Collections;
using System.IO;
using System.Reflection;
using HarmonyLib;
using InventoryExpansion.Config;
using MelonLoader;
using Mimic;
using Mimic.Actors;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
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
		private static bool _backpackFullyVisible = false;
		private static Sprite _backpackSprite;
		private static object _animationCoroutine;
		private static float _panelHeight = 0f;
		private static float _initialPanelY = 0f;
		private static TMP_Text _keyHintText;

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

				// Initialize scene change handler if not already done
				BackpackSceneChangeHandler.Initialize();

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
			}
			
			_canvasObj.AddComponent<GraphicRaycaster>();

			var panelObj = new GameObject("InventoryExpansion_BackpackPanel");
			panelObj.transform.SetParent(_canvasObj.transform, false);

			var image = panelObj.AddComponent<Image>();
			
			LoadBackpackSprite();
			if (_backpackSprite != null)
			{
				image.sprite = _backpackSprite;
				image.type = Image.Type.Simple;
				image.preserveAspect = false;
			}
			else
			{
				image.color = new Color(0f, 0f, 0f, 0.8f);
			}
			
			image.raycastTarget = false;

			_backpackPanel = panelObj.GetComponent<RectTransform>();
			_backpackPanel.anchorMin = new Vector2(1f, 0f);
			_backpackPanel.anchorMax = new Vector2(1f, 0f);
			_backpackPanel.pivot = new Vector2(1f, 0f);
			_backpackPanel.sizeDelta = new Vector2(450f, 200f);
			_backpackPanel.anchoredPosition = new Vector2(-40f, 40f);
			_initialPanelY = 40f;

			try
			{
				CreateKeyHintText(panelObj);
			}
			catch (Exception ex)
			{
				MelonLogger.Warning($"[InventoryExpansion][BackpackPanel] Failed to create key hint text during UI creation: {ex}");
			}
			
			_backpackFullyVisible = false;
			_backpackPanel.gameObject.SetActive(true);
		}

		private static void LoadBackpackSprite()
		{
			try
			{
				string assetsPath = Path.Combine(Path.GetDirectoryName(typeof(BackpackPanelPatch).Assembly.Location), "Assets", "Backpack.png");
				if (!File.Exists(assetsPath))
				{
					MelonLogger.Warning($"[InventoryExpansion][BackpackPanel] Backpack asset not found at: {assetsPath}");
					return;
				}

				byte[] fileData = File.ReadAllBytes(assetsPath);
				Texture2D texture = new Texture2D(2, 2);
				
				bool loaded = false;
				try
				{
					loaded = texture.LoadImage(fileData);
				}
				catch
				{
					try
					{
						loaded = UnityEngine.ImageConversion.LoadImage(texture, fileData);
					}
					catch
					{
					}
				}
				
				if (!loaded)
				{
					MelonLogger.Error("[InventoryExpansion][BackpackPanel] Failed to load Backpack.png as texture");
					UnityEngine.Object.Destroy(texture);
					return;
				}

				_backpackSprite = Sprite.Create(
					texture,
					new Rect(0, 0, texture.width, texture.height),
					new Vector2(0.5f, 0.5f),
					100f
				);

				MelonLogger.Msg($"[InventoryExpansion][BackpackPanel] Loaded Backpack sprite: {texture.width}x{texture.height}");
			}
			catch (Exception ex)
			{
				MelonLogger.Error($"[InventoryExpansion][BackpackPanel] Failed to load Backpack sprite: {ex}");
			}
		}


		internal static void ToggleBackpack()
		{
			if (_backpackPanel == null)
			{
				return;
			}

			if (IsInLoadingScreen())
			{
				return;
			}

			// Hide backpack if game is paused (ESC menu open)
			if (IsGamePaused())
			{
				return;
			}

			bool targetVisible = !_backpackFullyVisible;
			
			if (_animationCoroutine != null)
			{
				MelonCoroutines.Stop(_animationCoroutine);
				_animationCoroutine = null;
			}
			
			_backpackPanel.gameObject.SetActive(true);
			
			if (_panelHeight == 0f)
			{
				_panelHeight = _backpackPanel.sizeDelta.y;
			}
			
			if (_initialPanelY == 0f)
			{
				_initialPanelY = 40f;
			}
			
			_animationCoroutine = MelonCoroutines.Start(AnimateBackpackVisibility(targetVisible));
		}

		internal static bool IsGamePaused()
		{
			try
			{
				// Check if time is scaled (game is paused)
				if (Time.timeScale <= 0.01f)
				{
					return true;
				}
				
				// Also check if cursor is unlocked (usually means menu is open)
				if (Cursor.lockState == CursorLockMode.None && Cursor.visible)
				{
					// But make sure we're actually in game, not just at title screen
					if (IsInGame())
					{
						return true;
					}
				}
				
				return false;
			}
			catch
			{
				return false;
			}
		}

		internal static bool IsInGame()
		{
			try
			{
				var hub = Hub.s;
				if (hub == null)
				{
					return false;
				}

				var protoActorField = typeof(Hub).GetField("protoActor", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				ProtoActor protoActor = null;

				if (protoActorField == null)
				{
					var allFields = typeof(Hub).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					foreach (var field in allFields)
					{
						if (field.FieldType == typeof(ProtoActor) || field.FieldType.IsSubclassOf(typeof(ProtoActor)))
						{
							protoActor = field.GetValue(hub) as ProtoActor;
							if (protoActor != null && protoActor.AmIAvatar())
							{
								return true;
							}
						}
					}
					return false;
				}

				protoActor = protoActorField.GetValue(hub) as ProtoActor;
				if (protoActor == null)
				{
					return false;
				}

				bool isAvatar = protoActor.AmIAvatar();
				return isAvatar;
			}
			catch (Exception ex)
			{
				MelonLogger.Warning($"[InventoryExpansion][BackpackPanel] IsInGame check failed: {ex}");
				return false;
			}
		}

		private static bool IsInLoadingScreen()
		{
			try
			{
				var hub = Hub.s;
				if (hub == null)
				{
					return true;
				}

				var protoActorField = typeof(Hub).GetField("protoActor", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (protoActorField == null)
				{
					return false;
				}

				var protoActor = protoActorField.GetValue(hub) as ProtoActor;
				if (protoActor == null)
				{
					return true;
				}

				return !protoActor.AmIAvatar();
			}
			catch
			{
				return false;
			}
		}

		internal static bool IsBackpackFullyVisible => _backpackFullyVisible;

		internal static void HideBackpackCompletely()
		{
			if (_backpackPanel != null && _backpackPanel.gameObject != null)
			{
				_backpackPanel.gameObject.SetActive(false);
			}
		}

		private static IEnumerator AnimateBackpackVisibility(bool targetVisible)
		{
			if (_backpackPanel == null) yield break;
			
			if (_panelHeight == 0f)
			{
				_panelHeight = _backpackPanel.sizeDelta.y;
			}
			
			if (_initialPanelY == 0f)
			{
				_initialPanelY = 40f;
			}
			
			float hiddenY = _initialPanelY - (_panelHeight * 0.75f);
			float visibleY = _initialPanelY;
			
			Vector2 startPos = _backpackPanel.anchoredPosition;
			Vector2 targetPos = targetVisible ? new Vector2(startPos.x, visibleY) : new Vector2(startPos.x, hiddenY);
			
			float distance = Mathf.Abs(startPos.y - targetPos.y);
			if (distance < 1f)
			{
				_backpackFullyVisible = targetVisible;
				UpdateKeyHintVisibility();
				_animationCoroutine = null;
				yield break;
			}
			
			const float animationDuration = 0.3f;
			float elapsed = 0f;
			
			_backpackPanel.gameObject.SetActive(true);
			
			while (elapsed < animationDuration)
			{
				if (_backpackPanel == null || _backpackPanel.gameObject == null)
				{
					yield break;
				}
				
				elapsed += Time.deltaTime;
				float t = Mathf.Clamp01(elapsed / animationDuration);
				t = 1f - Mathf.Pow(1f - t, 3f);
				
				_backpackPanel.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
				UpdateKeyHintVisibility();
				yield return null;
			}
			
			if (_backpackPanel != null && _backpackPanel.gameObject != null)
			{
				_backpackPanel.anchoredPosition = targetPos;
				_backpackFullyVisible = targetVisible;
				UpdateKeyHintVisibility();
			}
			
			_animationCoroutine = null;
		}

		private static void CreateKeyHintText(GameObject parent)
		{
			try
			{
				if (parent == null)
				{
					MelonLogger.Warning("[InventoryExpansion][BackpackPanel] Cannot create key hint text: parent is null");
					return;
				}

				// Find an existing TMP_Text to get the correct type
				Type textComponentType = null;
				TMP_FontAsset fontToUse = null;
				try
				{
					var allTexts = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None);
					if (allTexts != null && allTexts.Length > 0)
					{
						foreach (var text in allTexts)
						{
							if (text != null && !text.Equals(null) && text.font != null)
							{
								textComponentType = text.GetType();
								fontToUse = text.font;
								break;
							}
						}
					}
				}
				catch (Exception ex)
				{
					MelonLogger.Warning($"[InventoryExpansion][BackpackPanel] Error finding TMP_Text type: {ex}");
				}

				if (textComponentType == null)
				{
					// Fallback to TextMeshProUGUI if available
					textComponentType = typeof(TMP_Text);
				}

				var textGO = new GameObject("InventoryExpansion_KeyHint");
				if (textGO == null)
				{
					MelonLogger.Warning("[InventoryExpansion][BackpackPanel] Failed to create key hint GameObject");
					return;
				}

				textGO.transform.SetParent(parent.transform, false);
				// Make sure text is rendered on top by setting it as last sibling
				textGO.transform.SetAsLastSibling();
				
				_keyHintText = textGO.AddComponent(textComponentType) as TMP_Text;
				if (_keyHintText == null)
				{
					MelonLogger.Warning("[InventoryExpansion][BackpackPanel] Failed to add TMP_Text component");
					UnityEngine.Object.Destroy(textGO);
					return;
				}

				// Set font first
				if (fontToUse != null)
				{
					_keyHintText.font = fontToUse;
				}
				else
				{
					var defaultFont = TMPro.TMP_Settings.defaultFontAsset;
					if (defaultFont != null)
					{
						_keyHintText.font = defaultFont;
					}
				}

				_keyHintText.text = InventoryExpansionPreferences.BackpackKey.ToString();
				_keyHintText.fontSize = 24f; // Larger font size for better visibility
				_keyHintText.alignment = TextAlignmentOptions.Center;
				_keyHintText.color = new Color(1f, 1f, 1f, 1f); // White color for maximum visibility
				_keyHintText.fontStyle = FontStyles.Bold;
				
				// Add outline for better visibility on any background
				var outline = textGO.AddComponent<Outline>();
				if (outline != null)
				{
					outline.effectColor = new Color(0f, 0f, 0f, 1f); // Black outline
					outline.effectDistance = new Vector2(2f, 2f);
				}
				
				if (_keyHintText.rectTransform == null)
				{
					MelonLogger.Warning("[InventoryExpansion][BackpackPanel] Key hint text has no RectTransform");
					return;
				}

				var textRT = _keyHintText.rectTransform;
				// Anchor at top of panel
				textRT.anchorMin = new Vector2(0.5f, 1f);
				textRT.anchorMax = new Vector2(0.5f, 1f);
				textRT.pivot = new Vector2(0.5f, 0.5f);
				textRT.sizeDelta = new Vector2(60f, 35f); // Larger size for better visibility
				textRT.anchoredPosition = new Vector2(0f, -95f); // Positioned ~95px from top of backpack
				
				_keyHintText.raycastTarget = false;
				// Keep key hint visible at all times on the backpack
				_keyHintText.gameObject.SetActive(true);
				
				MelonLogger.Msg("[InventoryExpansion][BackpackPanel] Key hint text created successfully");
			}
			catch (Exception ex)
			{
				MelonLogger.Error($"[InventoryExpansion][BackpackPanel] Failed to create key hint text: {ex}");
				_keyHintText = null;
			}
		}

		private static void UpdateKeyHintVisibility()
		{
			if (_keyHintText == null || _keyHintText.gameObject == null) return;
			
			try
			{
				// Keep key hint visible at all times on the backpack
				_keyHintText.gameObject.SetActive(true);
			}
			catch
			{
			}
		}


		private static (float padding, float paddingTop, float paddingBottom) GetPaddingForSlotCount(int additionalSlots)
		{
			return additionalSlots switch
			{
				4 => (90f, 130f, 90f),
				9 => (160f, 200f, 140f),
				_ => (200f, 240f, 180f)
			};
		}

		private static (float horizontal, float top) GetSlotPaddingForSlotCount(int additionalSlots)
		{
			return additionalSlots switch
			{
				4 => (90f, 170f),
				9 => (160f, 260f),
				_ => (200f, 320f)
			};
		}

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
				
				const float scaleFactor = 0.5f;
				frameWidth *= scaleFactor;
				frameHeight *= scaleFactor;
				
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

				float slotsAreaWidth = slotsPerRow * frameWidth + (slotsPerRow + 1) * slotSpacing;
				float slotsAreaHeight = rows * frameHeight + (rows + 1) * slotSpacing;
				
				var (padding, paddingTop, paddingBottom) = GetPaddingForSlotCount(additionalSlots);
				
				float panelWidth = slotsAreaWidth + padding * 2f;
				float panelHeight = slotsAreaHeight + paddingTop + paddingBottom;
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

					var (slotPaddingHorizontal, slotPaddingTop) = GetSlotPaddingForSlotCount(additionalSlots);
					
					float x = slotPaddingHorizontal + slotSpacing + col * (frameWidth + slotSpacing);
					float y = -(slotPaddingTop + slotSpacing + row * (frameHeight + slotSpacing));
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
				_panelHeight = _backpackPanel.sizeDelta.y;
				
				if (_initialPanelY == 0f)
				{
					_initialPanelY = 40f;
				}
				
				float hiddenY = _initialPanelY - (_panelHeight * 0.75f);
				_backpackPanel.anchoredPosition = new Vector2(_backpackPanel.anchoredPosition.x, hiddenY);
				_backpackFullyVisible = false;
				UpdateKeyHintVisibility();
				
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

				// Hide backpack if game is paused (ESC menu open)
				if (BackpackPanelPatch.IsGamePaused())
				{
					if (BackpackPanelPatch.IsBackpackFullyVisible)
					{
						BackpackPanelPatch.ToggleBackpack();
					}
					else
					{
						BackpackPanelPatch.HideBackpackCompletely();
					}
					wasKeyPressedLastFrame = false;
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
					BackpackPanelPatch.ToggleBackpack();
				}
			}
			catch (Exception ex)
			{
				MelonLogger.Error($"[InventoryExpansion][BackpackPanel] Update postfix failed: {ex}");
			}
		}
	}

	[HarmonyPatch(typeof(ProtoActor))]
	internal static class BackpackMovementSpeedPatch
	{
		private static FieldInfo _netSyncActorDataField;
		private static bool _fieldInitialized = false;
		private static long _originalMoveSpeedWalk = 0L;
		private static long _originalMoveSpeedRun = 0L;
		private static bool _speedReduced = false;

		[HarmonyPostfix]
		[HarmonyPatch("Update")]
		private static void Update_Postfix(ProtoActor __instance)
		{
			try
			{
				if (!InventoryExpansionPreferences.Enabled)
				{
					RestoreMoveSpeed(__instance);
					return;
				}

				if (!__instance.AmIAvatar())
				{
					return;
				}

				if (!_fieldInitialized)
				{
					var protoActorType = typeof(ProtoActor);
					_netSyncActorDataField = protoActorType.GetField("netSyncActorData", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					_fieldInitialized = true;
				}

				if (_netSyncActorDataField == null)
				{
					return;
				}

				var netSyncActorData = _netSyncActorDataField.GetValue(__instance);
				if (netSyncActorData == null)
				{
					return;
				}

				var moveSpeedWalkField = netSyncActorData.GetType().GetField("MoveSpeedWalk", BindingFlags.Instance | BindingFlags.Public);
				var moveSpeedRunField = netSyncActorData.GetType().GetField("MoveSpeedRun", BindingFlags.Instance | BindingFlags.Public);

				if (moveSpeedWalkField == null || moveSpeedRunField == null)
				{
					return;
				}

				if (BackpackPanelPatch.IsBackpackFullyVisible && InventoryExpansionPreferences.ReduceMovementSpeed)
				{
					if (!_speedReduced)
					{
						_originalMoveSpeedWalk = (long)(moveSpeedWalkField.GetValue(netSyncActorData) ?? 350L);
						_originalMoveSpeedRun = (long)(moveSpeedRunField.GetValue(netSyncActorData) ?? 700L);
						_speedReduced = true;
					}

					moveSpeedWalkField.SetValue(netSyncActorData, (long)(_originalMoveSpeedWalk * 0.5f));
					moveSpeedRunField.SetValue(netSyncActorData, (long)(_originalMoveSpeedRun * 0.5f));
				}
				else
				{
					RestoreMoveSpeed(netSyncActorData, moveSpeedWalkField, moveSpeedRunField);
				}
			}
			catch (Exception ex)
			{
				MelonLogger.Error($"[InventoryExpansion][Movement] Movement speed patch failed: {ex}");
			}
		}

		private static void RestoreMoveSpeed(ProtoActor instance)
		{
			if (!_speedReduced || _netSyncActorDataField == null)
			{
				return;
			}

			var netSyncActorData = _netSyncActorDataField.GetValue(instance);
			if (netSyncActorData == null)
			{
				return;
			}

			var moveSpeedWalkField = netSyncActorData.GetType().GetField("MoveSpeedWalk", BindingFlags.Instance | BindingFlags.Public);
			var moveSpeedRunField = netSyncActorData.GetType().GetField("MoveSpeedRun", BindingFlags.Instance | BindingFlags.Public);

			if (moveSpeedWalkField != null && moveSpeedRunField != null)
			{
				RestoreMoveSpeed(netSyncActorData, moveSpeedWalkField, moveSpeedRunField);
			}
		}

		private static void RestoreMoveSpeed(object netSyncActorData, FieldInfo moveSpeedWalkField, FieldInfo moveSpeedRunField)
		{
			if (_speedReduced && _originalMoveSpeedWalk > 0L && _originalMoveSpeedRun > 0L)
			{
				moveSpeedWalkField.SetValue(netSyncActorData, _originalMoveSpeedWalk);
				moveSpeedRunField.SetValue(netSyncActorData, _originalMoveSpeedRun);
				_speedReduced = false;
			}
		}
	}

	[HarmonyPatch(typeof(ProtoActor), "OnDestroy")]
	internal static class BackpackProtoActorDestroyPatch
	{
		[HarmonyPostfix]
		private static void OnDestroy_Postfix(ProtoActor __instance)
		{
			try
			{
				if (!InventoryExpansionPreferences.Enabled)
				{
					return;
				}

				if (__instance.AmIAvatar())
				{
					BackpackSceneChangeHandler.HideBackpack();
				}
			}
			catch (Exception ex)
			{
				MelonLogger.Error($"[InventoryExpansion][BackpackPanel] ProtoActor OnDestroy patch failed: {ex}");
			}
		}
	}

	internal static class BackpackSceneChangeHandler
	{
		private static bool _initialized = false;

		internal static void Initialize()
		{
			if (_initialized) return;
			
			SceneManager.activeSceneChanged += OnActiveSceneChanged;
			_initialized = true;
		}

		private static void OnActiveSceneChanged(Scene previousScene, Scene newScene)
		{
			try
			{
				if (!InventoryExpansionPreferences.Enabled)
				{
					return;
				}

				// Hide backpack when scene changes (e.g., returning to title screen)
				if (!IsInGame())
				{
					HideBackpack();
				}
			}
			catch (Exception ex)
			{
				MelonLogger.Error($"[InventoryExpansion][BackpackPanel] Scene change handler failed: {ex}");
			}
		}

		private static bool IsInGame()
		{
			try
			{
				var hub = Hub.s;
				if (hub == null)
				{
					return false;
				}

				var protoActorField = typeof(Hub).GetField("protoActor", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				ProtoActor protoActor = null;

				if (protoActorField == null)
				{
					var allFields = typeof(Hub).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					foreach (var field in allFields)
					{
						if (field.FieldType == typeof(ProtoActor) || field.FieldType.IsSubclassOf(typeof(ProtoActor)))
						{
							protoActor = field.GetValue(hub) as ProtoActor;
							if (protoActor != null && protoActor.AmIAvatar())
							{
								return true;
							}
						}
					}
					return false;
				}

				protoActor = protoActorField.GetValue(hub) as ProtoActor;
				if (protoActor == null)
				{
					return false;
				}

				return protoActor.AmIAvatar();
			}
			catch
			{
				return false;
			}
		}

		internal static void HideBackpack()
		{
			if (BackpackPanelPatch.IsBackpackFullyVisible)
			{
				BackpackPanelPatch.ToggleBackpack();
			}
			else
			{
				// Hide the panel completely if it exists
				BackpackPanelPatch.HideBackpackCompletely();
			}
		}
	}
}
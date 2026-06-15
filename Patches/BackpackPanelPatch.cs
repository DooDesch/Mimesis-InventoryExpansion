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
		// The standard-inventory slot (0-3) that was selected before the backpack was opened,
		// restored when the backpack is closed so the cursor never stays on a hidden slot.
		private static int _savedStandardSlot = 0;

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

				BackpackSceneChangeHandler.Initialize();

				if (_backpackPanel != null)
				{
					// A new inventory UI instance was created (e.g. after a map change).
					// The game recreates UIPrefab_Inventory and its slots per map, but our
					// panel persists (DontDestroyOnLoad). Without rebuilding, the panel keeps
					// the previous map's now-stale slot containers, the new extra slots stay
					// in their default inventory positions, and the scene-change handler left
					// the panel hidden. Drop the old containers, re-attach this UI's extra
					// slots, and restore the panel to its peek state.
					CleanupMovedSlots();
					_slotsMoved = false;
					MelonCoroutines.Start(MoveSlotsToPanelCoroutine(__instance));
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
			
			// Start hidden. The per-frame in-game check (EnsurePeekIfHidden) reveals the
			// panel once the player is actually in interactive gameplay, so it never shows
			// over the loading screen.
			_backpackFullyVisible = false;
			_backpackPanel.gameObject.SetActive(false);
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

		// Move the inventory selection when the backpack is toggled with the key, so the
		// cursor follows the visible inventory instead of getting stuck on a hidden slot.
		// On open: jump to the first backpack slot (remembering the standard slot). On close:
		// restore the remembered standard slot. Each direction is independently configurable.
		internal static void HandleCursorHandoff(ProtoActor avatar, bool opening)
		{
			try
			{
				if (avatar == null)
				{
					return;
				}
				if (opening && !InventoryExpansionPreferences.SelectBackpackSlotOnOpen)
				{
					return;
				}
				if (!opening && !InventoryExpansionPreferences.RestoreStandardSlotOnClose)
				{
					return;
				}

				var inventoryField = InventorySelectionHelper.GetActorInventoryField();
				var inventory = inventoryField?.GetValue(avatar);
				if (inventory == null)
				{
					return;
				}

				var inventoryType = inventory.GetType();
				var slotSizeField = InventorySelectionHelper.GetSlotSizeField(inventoryType);
				var selectedSlotIndexField = InventorySelectionHelper.GetSelectedSlotIndexField(inventoryType);
				var selectSlotMethod = InventorySelectionHelper.GetSelectSlotMethod(inventoryType);
				if (slotSizeField == null || selectedSlotIndexField == null || selectSlotMethod == null)
				{
					return;
				}

				int slotSize = (int)(slotSizeField.GetValue(inventory) ?? 4);
				if (slotSize <= 4)
				{
					// No backpack slots exist, nothing to hand off to.
					return;
				}

				int currentSlot = (int)(selectedSlotIndexField.GetValue(inventory) ?? 0);

				if (opening)
				{
					if (currentSlot >= 0 && currentSlot <= 3)
					{
						_savedStandardSlot = currentSlot;
					}
					selectSlotMethod.Invoke(inventory, new object[] { 4 });
				}
				else
				{
					int target = Mathf.Clamp(_savedStandardSlot, 0, 3);
					selectSlotMethod.Invoke(inventory, new object[] { target });
				}
			}
			catch (Exception ex)
			{
				MelonLogger.Error($"[InventoryExpansion][BackpackPanel] Cursor handoff failed: {ex}");
			}
		}

		internal static bool IsGamePaused()
		{
			try
			{
				if (Time.timeScale <= 0.01f)
				{
					return true;
				}
				
				if (Cursor.lockState == CursorLockMode.None && Cursor.visible)
				{
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
				ProtoActor protoActor = Hub.Main?.GetMyAvatar();
				if (protoActor == null)
				{
					return false;
				}

				return protoActor.AmIAvatar();
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

				var protoActor = Hub.Main?.GetMyAvatar();
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

		// Destroy the slot containers we previously parented under the panel. Called
		// before re-attaching a freshly created inventory UI's slots (e.g. after a map
		// change) so the persistent panel does not accumulate stale/destroyed slots.
		// The key-hint child is left intact (different name prefix).
		private static void CleanupMovedSlots()
		{
			if (_backpackPanel == null) return;

			for (int i = _backpackPanel.childCount - 1; i >= 0; i--)
			{
				var child = _backpackPanel.GetChild(i);
				if (child != null && child.name.StartsWith("InventoryExpansion_SlotContainer_"))
				{
					UnityEngine.Object.Destroy(child.gameObject);
				}
			}
		}

		private static FieldInfo _uimanField;

		// True while the game's full-screen scene loading UI is shown. Used to keep the
		// backpack hidden (and non-interactive) during map changes / loading screens.
		internal static bool IsLoadingScreenActive()
		{
			try
			{
				var hub = Hub.s;
				if (hub == null)
				{
					return false;
				}
				// Hub.uiman is internal; reach it via its auto-property backing field.
				if (_uimanField == null)
				{
					_uimanField = typeof(Hub).GetField("<uiman>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
				}
				var uiman = _uimanField?.GetValue(hub) as UIManager;
				var loading = uiman?.ui_sceneloading;
				return loading != null && loading.isActiveAndEnabled;
			}
			catch
			{
				return false;
			}
		}

		// Restore the panel to its resting "peek" state if it is currently hidden (e.g.
		// after a map change or loading screen deactivated it). No-op while an open/close
		// animation is running or when the panel is already visible.
		internal static void EnsurePeekIfHidden()
		{
			if (_backpackPanel == null || !_slotsMoved)
			{
				return;
			}
			if (_animationCoroutine != null)
			{
				return;
			}
			if (_backpackPanel.gameObject.activeSelf)
			{
				return;
			}

			if (_panelHeight == 0f)
			{
				_panelHeight = _backpackPanel.sizeDelta.y;
			}
			if (_initialPanelY == 0f)
			{
				_initialPanelY = 40f;
			}
			float hiddenY = _initialPanelY - (_panelHeight * 0.75f);
			_backpackPanel.anchoredPosition = new Vector2(_backpackPanel.anchoredPosition.x, hiddenY);
			_backpackFullyVisible = false;
			_backpackPanel.gameObject.SetActive(true);
			UpdateKeyHintVisibility();
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
					textComponentType = typeof(TMP_Text);
				}

				var textGO = new GameObject("InventoryExpansion_KeyHint");
				if (textGO == null)
				{
					MelonLogger.Warning("[InventoryExpansion][BackpackPanel] Failed to create key hint GameObject");
					return;
				}

				textGO.transform.SetParent(parent.transform, false);
				textGO.transform.SetAsLastSibling();
				
				_keyHintText = textGO.AddComponent(textComponentType) as TMP_Text;
				if (_keyHintText == null)
				{
					MelonLogger.Warning("[InventoryExpansion][BackpackPanel] Failed to add TMP_Text component");
					UnityEngine.Object.Destroy(textGO);
					return;
				}

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
				_keyHintText.fontSize = 24f;
				_keyHintText.alignment = TextAlignmentOptions.Center;
				_keyHintText.color = new Color(1f, 1f, 1f, 1f);
				_keyHintText.fontStyle = FontStyles.Bold;
				
				var outline = textGO.AddComponent<Outline>();
				if (outline != null)
				{
					outline.effectColor = new Color(0f, 0f, 0f, 1f);
					outline.effectDistance = new Vector2(2f, 2f);
				}
				
				if (_keyHintText.rectTransform == null)
				{
					MelonLogger.Warning("[InventoryExpansion][BackpackPanel] Key hint text has no RectTransform");
					return;
				}

				var textRT = _keyHintText.rectTransform;
				textRT.anchorMin = new Vector2(0.5f, 1f);
				textRT.anchorMax = new Vector2(0.5f, 1f);
				textRT.pivot = new Vector2(0.5f, 0.5f);
				textRT.sizeDelta = new Vector2(60f, 35f);
				textRT.anchoredPosition = new Vector2(0f, -95f);
				
				_keyHintText.raycastTarget = false;
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

		// Place a backpack slot's durability/stack text ("99%") at the bottom-right of the slot
		// so it matches the original hotbar.
		//
		// The text is parented under the frame (see InventoryUiPatches), but in the game prefab
		// the standard slot's stackCount is a SIBLING of its frame, so its anchoredPosition is
		// slot-container-relative - copying that onto a frame-child threw the text off the slot.
		// We give it a slightly-inset, frame-sized box centered on the frame and bottom-right
		// text alignment; the text then sits in the bottom-right corner of its slot (matching the
		// original) and inherits the frame's uniform localScale. This is deterministic - measuring
		// the live slot's transform proved unreliable (it flipped the text to the top corner).
		internal static void ApplyStackPlacement(TMP_Text stackText, RectTransform templateFrameRT, TMP_Text templateStack)
		{
			if (stackText == null || templateFrameRT == null)
			{
				return;
			}

			var stackRT = stackText.rectTransform;
			Vector2 frameSize = templateFrameRT.sizeDelta;

			stackRT.anchorMin = new Vector2(0.5f, 0.5f);
			stackRT.anchorMax = new Vector2(0.5f, 0.5f);
			stackRT.pivot = new Vector2(0.5f, 0.5f);
			// Inset slightly so the text keeps a small margin from the slot edges, like the original.
			stackRT.sizeDelta = new Vector2(frameSize.x * 0.85f, frameSize.y * 0.85f);
			stackRT.anchoredPosition = Vector2.zero;
			stackText.alignment = TextAlignmentOptions.BottomRight;
			if (templateStack != null)
			{
				stackText.fontSize = templateStack.fontSize;
			}
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
					// Scale the whole slot (frame + its icon/stack/wait children) uniformly via
					// localScale instead of scaling each child's sizeDelta/anchoredPosition. The
					// per-child math misplaced stretch-anchored children like the durability/stack
					// count text ("100%"); localScale is anchor-agnostic and keeps every child on the slot.
					frameRT.sizeDelta = templateFrameRT.sizeDelta;
					frameRT.anchoredPosition = Vector2.zero;
					frameRT.localScale = new Vector3(scaleFactor, scaleFactor, 1f);

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
							iconRT.sizeDelta = templateImageRT.sizeDelta;
							iconRT.anchoredPosition = templateImageRT.anchoredPosition;
						}
					}

					if (stackField != null)
					{
						var stackText = stackField.GetValue(slot) as TMP_Text;
						if (stackText != null)
						{
							ApplyStackPlacement(stackText, templateFrameRT, templateStack);
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
				// Left inactive on purpose: the per-frame in-game check reveals it so it
				// never shows over the loading screen after a map change.

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

				// During a map change / loading screen the panel must be fully hidden and
				// non-interactive (it is on a separate DontDestroyOnLoad canvas, so it does
				// not follow the game HUD's visibility on its own).
				if (BackpackPanelPatch.IsLoadingScreenActive())
				{
					BackpackPanelPatch.HideBackpackCompletely();
					wasKeyPressedLastFrame = false;
					return;
				}

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

				// In interactive gameplay (not loading, not paused): make sure the panel is
				// at least at its resting peek state, e.g. after a map change had hidden it.
				BackpackPanelPatch.EnsurePeekIfHidden();

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
					// Capture the toggle direction before flipping (fully visible -> closing).
					bool opening = !BackpackPanelPatch.IsBackpackFullyVisible;
					BackpackPanelPatch.ToggleBackpack();
					BackpackPanelPatch.HandleCursorHandoff(__instance, opening);
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
				ProtoActor protoActor = Hub.Main?.GetMyAvatar();
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
				BackpackPanelPatch.HideBackpackCompletely();
			}
		}
	}
}
using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using InventoryExpansion.Config;
using MelonLoader;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace InventoryExpansion.Patches
{
	/// <summary>
	/// Erstellt ein schwarzes Rechteck unten rechts im Screen mit den zusätzlichen Inventar-Slots.
	/// Orientiert sich an der Minimap-Mod: eigenes Canvas mit ScreenSpaceOverlay.
	/// </summary>
	[HarmonyPatch]
	internal static class BackpackPanelPatch
	{
		private static GameObject _rootObj;
		private static GameObject _canvasObj;
		private static RectTransform _backpackPanel;
		private static bool _slotsMoved = false;

		/// <summary>
		/// Hook in die Scene-Loading, um das Panel zu erstellen, wenn der Spieler auf die Map lädt.
		/// Wir verwenden einen Postfix auf UIPrefab_Inventory.Awake, da das sicherstellt,
		/// dass die UI bereits initialisiert ist.
		/// </summary>
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

				// Erstelle das Panel nur einmal
				if (_backpackPanel != null)
				{
					// Verschiebe die Slots, falls noch nicht geschehen
					if (!_slotsMoved)
					{
						MelonCoroutines.Start(MoveSlotsToPanelCoroutine(__instance));
					}
					return;
				}

				CreateRoot();
				CreateUI();

				// Verschiebe die Slots in das Panel
				MelonCoroutines.Start(MoveSlotsToPanelCoroutine(__instance));

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

			// Erstelle Canvas (wie in Minimap-Mod)
			_canvasObj = new GameObject("InventoryExpansion_Canvas");
			_canvasObj.transform.SetParent(_rootObj.transform, false);

			var canvas = _canvasObj.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			
			// Konfiguriere CanvasScaler mit Constant Pixel Size, um keine zusätzliche Skalierung zu haben
			// Die Größen werden 1:1 vom Haupt-Canvas übernommen
			var scaler = _canvasObj.AddComponent<CanvasScaler>();
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
			scaler.scaleFactor = 1f;
			
			// Stelle sicher, dass das Canvas die richtige Sortierreihenfolge hat
			Canvas mainCanvas = null;
			var allCanvases = UnityEngine.Object.FindObjectsOfType<Canvas>();
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
				canvas.sortingOrder = mainCanvas.sortingOrder + 1; // Etwas höher, damit es über dem Haupt-Canvas liegt
				MelonLogger.Msg("[InventoryExpansion][BackpackPanel] Using Constant Pixel Size scaling. Main canvas: {0}, Screen resolution: {1}x{2}", 
					mainCanvas.name, Screen.width, Screen.height);
			}
			
			_canvasObj.AddComponent<GraphicRaycaster>();

			// Erstelle das schwarze Panel
			var panelObj = new GameObject("InventoryExpansion_BackpackPanel");
			panelObj.transform.SetParent(_canvasObj.transform, false);

			var image = panelObj.AddComponent<Image>();
			image.color = new Color(0f, 0f, 0f, 0.8f); // schwarz, 80% Alpha
			image.raycastTarget = false; // WICHTIG: keine Input-Events blockieren

			_backpackPanel = panelObj.GetComponent<RectTransform>();

			// Positionierung unten rechts (wie in Minimap-Mod)
			_backpackPanel.anchorMin = new Vector2(1f, 0f);
			_backpackPanel.anchorMax = new Vector2(1f, 0f);
			_backpackPanel.pivot = new Vector2(1f, 0f); // Pivot rechts unten

			// Initiale Größe - wird später basierend auf Slot-Anzahl angepasst
			_backpackPanel.sizeDelta = new Vector2(450f, 200f);

			// Position: Von rechts 40px, von unten 40px
			_backpackPanel.anchoredPosition = new Vector2(-40f, 40f);
		}

		private static IEnumerator MoveSlotsToPanelCoroutine(UIPrefab_Inventory inventoryUI)
		{
			// Warte einen Frame, damit die Slots im InventoryUiPatches erstellt werden
			yield return null;

			try
			{
				if (_backpackPanel == null || _slotsMoved)
				{
					yield break;
				}

				// Hole die inventorySlots über Reflection
				var slotsField = typeof(UIPrefab_Inventory).GetField("inventorySlots", BindingFlags.Instance | BindingFlags.NonPublic);
				if (slotsField == null)
				{
					MelonLogger.Error("[InventoryExpansion][BackpackPanel] Could not find inventorySlots field!");
					yield break;
				}

				var inventorySlots = slotsField.GetValue(inventoryUI) as System.Collections.IList;
				if (inventorySlots == null || inventorySlots.Count <= 4)
				{
					// Keine zusätzlichen Slots vorhanden
					yield break;
				}

				// Hole Slot-Typ und Felder
				var slotType = inventorySlots[0].GetType();
				var frameField = slotType.GetField("frame", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (frameField == null)
				{
					MelonLogger.Error("[InventoryExpansion][BackpackPanel] Could not find frame field in slot!");
					yield break;
				}

				// Hole den ersten Slot als Template für Größe
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
				
				// Skalierungsfaktor: 25% kleiner = 0.75
				const float scaleFactor = 0.75f;
				frameWidth *= scaleFactor;
				frameHeight *= scaleFactor;
				
				// Hole die Container-Größe vom Hintergrund (InvenBG)
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
				
				float slotSpacing = 10f; // Abstand zwischen Slots
				const int slotsPerRow = 4;

				// Berechne Anzahl der zusätzlichen Slots (ab Slot 5)
				int additionalSlots = inventorySlots.Count - 4;
				int rows = Mathf.CeilToInt((float)additionalSlots / slotsPerRow);

				// Passe Panel-Größe an (verwende Frame-Größe für die Positionierung)
				float panelWidth = slotsPerRow * frameWidth + (slotsPerRow + 1) * slotSpacing;
				float panelHeight = rows * frameHeight + (rows + 1) * slotSpacing;
				_backpackPanel.sizeDelta = new Vector2(panelWidth, panelHeight);

				// Hole weitere Slot-Felder für Icon und Stack-Count
				var imageField = slotType.GetField("image", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				var stackField = slotType.GetField("stackCount", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

				// Hole Template-Werte vom ersten Slot für korrekte Icon/Stack-Positionierung
				var templateImage = imageField?.GetValue(firstSlot) as Image;
				var templateStack = stackField?.GetValue(firstSlot) as TMP_Text;
				var templateImageRT = templateImage?.rectTransform;
				var templateStackRT = templateStack?.rectTransform;

				// Detailliertes Debugging: Durchgehe die komplette Hierarchie des originalen Slots
				MelonLogger.Msg("[InventoryExpansion][BackpackPanel] ========================================");
				MelonLogger.Msg("[InventoryExpansion][BackpackPanel] === DEEP SLOT ANALYSIS ===");
				MelonLogger.Msg("[InventoryExpansion][BackpackPanel] ========================================");
				
				// Analysiere Frame GameObject komplett
				var frameGO = firstFrame.gameObject;
				MelonLogger.Msg("[InventoryExpansion][BackpackPanel] Frame GameObject: {0}", frameGO.name);
				MelonLogger.Msg("[InventoryExpansion][BackpackPanel] Frame Transform Path: {0}", GetFullPath(frameGO.transform));
				
				// WICHTIG: Prüfe den PARENT des Frames - dort sind wahrscheinlich Icon und Stack!
				var frameParent = frameGO.transform.parent;
				if (frameParent != null)
				{
					MelonLogger.Msg("[InventoryExpansion][BackpackPanel] Frame Parent: {0}", frameParent.name);
					MelonLogger.Msg("[InventoryExpansion][BackpackPanel] Frame Parent Path: {0}", GetFullPath(frameParent));
					MelonLogger.Msg("[InventoryExpansion][BackpackPanel] Frame Parent Children ({0}):", frameParent.childCount);
					
					for (int siblingIdx = 0; siblingIdx < frameParent.childCount; siblingIdx++)
					{
						var sibling = frameParent.GetChild(siblingIdx);
						MelonLogger.Msg("[InventoryExpansion][BackpackPanel]   Sibling {0}: {1}", siblingIdx, sibling.name);
						
						var siblingComponents = sibling.GetComponents<Component>();
						foreach (var comp in siblingComponents)
						{
							if (comp == null) continue;
							MelonLogger.Msg("[InventoryExpansion][BackpackPanel]     Component: {0}", comp.GetType().FullName);
							
							if (comp is Image img)
							{
								LogImageDetails("      " + sibling.name + " Image", img);
							}
							if (comp is RectTransform rt)
							{
								LogRectTransformDetails("      " + sibling.name + " RectTransform", rt);
							}
							if (comp is TMP_Text tmp)
							{
								LogTMPTextDetails("      " + sibling.name + " TMP_Text", tmp);
							}
						}
						
						// Prüfe auch Children der Siblings
						if (sibling.childCount > 0)
						{
							MelonLogger.Msg("[InventoryExpansion][BackpackPanel]     Sub-Children ({0}):", sibling.childCount);
							for (int subIdx = 0; subIdx < sibling.childCount; subIdx++)
							{
								var subChild = sibling.GetChild(subIdx);
								MelonLogger.Msg("[InventoryExpansion][BackpackPanel]       Sub-Child {0}: {1}", subIdx, subChild.name);
								
								var subComponents = subChild.GetComponents<Component>();
								foreach (var subComp in subComponents)
								{
									if (subComp == null) continue;
									if (subComp is Image subImg)
									{
										LogImageDetails("        " + subChild.name + " Image", subImg);
									}
									if (subComp is RectTransform subRT)
									{
										LogRectTransformDetails("        " + subChild.name + " RectTransform", subRT);
									}
								}
							}
						}
					}
				}
				
				// Alle Komponenten am Frame
				var frameComponents = frameGO.GetComponents<Component>();
				MelonLogger.Msg("[InventoryExpansion][BackpackPanel] Frame Components ({0}):", frameComponents.Length);
				foreach (var comp in frameComponents)
				{
					if (comp == null) continue;
					MelonLogger.Msg("  - {0}", comp.GetType().FullName);
					if (comp is Image img)
					{
						LogImageDetails("Frame Image", img);
					}
					if (comp is RectTransform rt)
					{
						LogRectTransformDetails("Frame RectTransform", rt);
					}
				}
				
				MelonLogger.Msg("[InventoryExpansion][BackpackPanel] ========================================");
				MelonLogger.Msg("[InventoryExpansion][BackpackPanel] === END DEEP ANALYSIS ===");
				MelonLogger.Msg("[InventoryExpansion][BackpackPanel] ========================================");

				// originalSlotContainer und templateBG wurden bereits oben definiert
				RectTransform originalContainerRT = originalSlotContainer as RectTransform;

				// Verschiebe zusätzliche Slots (ab Index 4 = Slot 5) in das Panel
				// Die zusätzlichen Slots haben eine andere Struktur: Frame direkt, kein Container
				for (int i = 4; i < inventorySlots.Count; i++)
				{
					var slot = inventorySlots[i];
					var frame = frameField.GetValue(slot) as Image;
					if (frame == null) continue;

					var frameRT = frame.rectTransform;
					
					// Prüfe, ob dieser Slot ein zusätzlicher Slot ist
					// Zusätzliche Slots haben Frame-Namen mit "_Extra" (z.B. "InvenFrame1_Extra4")
					// Originale Slots haben Frame-Namen ohne "_Extra" (z.B. "InvenFrame1")
					bool isExtraSlot = frame.gameObject.name.Contains("_Extra");
					
					if (!isExtraSlot)
					{
						// Das ist ein originaler Slot - NICHT verschieben!
						MelonLogger.Msg("[InventoryExpansion][BackpackPanel] Skipping original slot {0} (frame name: {1})", i, frame.gameObject.name);
						continue;
					}
					
					// Erstelle einen Container für diesen zusätzlichen Slot (wie bei originalen Slots)
					var slotContainerGO = new GameObject("InventoryExpansion_SlotContainer_" + i);
					slotContainerGO.transform.SetParent(_backpackPanel, false);
					var containerRT = slotContainerGO.AddComponent<RectTransform>();
					
					// Berechne Position in der Reihe
					int slotIndex = i - 4; // 0-based für zusätzliche Slots
					int row = slotIndex / slotsPerRow; // 0 = erste Reihe
					int col = slotIndex % slotsPerRow; // 0-3

					// Positionierung: von oben nach unten, von links nach rechts
					containerRT.anchorMin = new Vector2(0f, 1f);
					containerRT.anchorMax = new Vector2(0f, 1f);
					containerRT.pivot = new Vector2(0f, 1f); // Pivot links oben

					// Position berechnen (verwende Frame-Größe für die Positionierung)
					float x = slotSpacing + col * (frameWidth + slotSpacing);
					float y = -(slotSpacing + row * (frameHeight + slotSpacing)); // negativ, da von oben nach unten
					containerRT.anchoredPosition = new Vector2(x, y);
					// Container-Größe = Frame-Größe (der Hintergrund wird innerhalb zentriert)
					containerRT.sizeDelta = new Vector2(frameWidth, frameHeight);
					
					// Erstelle den Hintergrund (InvenBG) wenn Template vorhanden
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
						// Skaliere die Hintergrund-Größe
						bgRT.sizeDelta = templateBGRT.sizeDelta * scaleFactor;
						bgRT.anchoredPosition = templateBGRT.anchoredPosition * scaleFactor;
					}
					
					// Verschiebe den Frame in den Container
					frameRT.SetParent(slotContainerGO.transform, false);

					// Stelle sicher, dass der Frame korrekt im Container zentriert ist
					// Da der Container jetzt die Frame-Größe hat, zentrieren wir den Frame
					var templateFrameRT = firstFrame.rectTransform;
					frameRT.anchorMin = new Vector2(0.5f, 0.5f);
					frameRT.anchorMax = new Vector2(0.5f, 0.5f);
					frameRT.pivot = templateFrameRT.pivot;
					// Skaliere die Frame-Größe (das ist die Selection-Border)
					frameRT.sizeDelta = templateFrameRT.sizeDelta * scaleFactor;
					// Frame zentriert im Container
					frameRT.anchoredPosition = Vector2.zero;

					// Kopiere ALLE Eigenschaften vom originalen Frame
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

					// Stelle sicher, dass Icon korrekt positioniert ist (relativ zum Frame)
					if (imageField != null && templateImage != null && templateImageRT != null)
					{
						var iconImage = imageField.GetValue(slot) as Image;
						if (iconImage != null)
						{
							var iconRT = iconImage.rectTransform;
							// Kopiere ALLE Eigenschaften vom originalen Icon
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
							
							// Position und Größe (skaliert)
							iconRT.anchorMin = templateImageRT.anchorMin;
							iconRT.anchorMax = templateImageRT.anchorMax;
							iconRT.pivot = templateImageRT.pivot;
							iconRT.sizeDelta = templateImageRT.sizeDelta * scaleFactor;
							iconRT.anchoredPosition = templateImageRT.anchoredPosition * scaleFactor;
						}
					}

					// Stelle sicher, dass Stack-Count korrekt positioniert ist
					if (stackField != null && templateStackRT != null)
					{
						var stackText = stackField.GetValue(slot) as TMP_Text;
						if (stackText != null)
						{
							var stackRT = stackText.rectTransform;
							// Stack-Count sollte bereits als Child des Frames sein (skaliert)
							stackRT.anchorMin = templateStackRT.anchorMin;
							stackRT.anchorMax = templateStackRT.anchorMax;
							stackRT.pivot = templateStackRT.pivot;
							stackRT.sizeDelta = templateStackRT.sizeDelta * scaleFactor;
							stackRT.anchoredPosition = templateStackRT.anchoredPosition * scaleFactor;
							// Skaliere auch die Schriftgröße
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

		private static string GetFullPath(Transform t)
		{
			if (t == null) return "<null>";
			var path = t.name;
			var parent = t.parent;
			while (parent != null)
			{
				path = parent.name + "/" + path;
				parent = parent.parent;
			}
			return path;
		}

		private static void LogImageDetails(string label, Image img)
		{
			MelonLogger.Msg("[InventoryExpansion][BackpackPanel] {0}:", label);
			MelonLogger.Msg("[InventoryExpansion][BackpackPanel]   Sprite: {0} (texture: {1})", 
				img.sprite != null ? img.sprite.name : "NULL",
				img.sprite != null && img.sprite.texture != null ? img.sprite.texture.name : "NULL");
			MelonLogger.Msg("[InventoryExpansion][BackpackPanel]   Material: {0}", img.material != null ? img.material.name : "NULL");
			MelonLogger.Msg("[InventoryExpansion][BackpackPanel]   Color: {0}", img.color);
			MelonLogger.Msg("[InventoryExpansion][BackpackPanel]   Type: {0}", img.type);
			MelonLogger.Msg("[InventoryExpansion][BackpackPanel]   PreserveAspect: {0}", img.preserveAspect);
			MelonLogger.Msg("[InventoryExpansion][BackpackPanel]   FillMethod: {0}", img.fillMethod);
			MelonLogger.Msg("[InventoryExpansion][BackpackPanel]   FillAmount: {0}", img.fillAmount);
			MelonLogger.Msg("[InventoryExpansion][BackpackPanel]   RaycastTarget: {0}", img.raycastTarget);
			MelonLogger.Msg("[InventoryExpansion][BackpackPanel]   Maskable: {0}", img.maskable);
		}

		private static void LogRectTransformDetails(string label, RectTransform rt)
		{
			MelonLogger.Msg("[InventoryExpansion][BackpackPanel] {0}:", label);
			MelonLogger.Msg("[InventoryExpansion][BackpackPanel]   AnchorMin: {0}", rt.anchorMin);
			MelonLogger.Msg("[InventoryExpansion][BackpackPanel]   AnchorMax: {0}", rt.anchorMax);
			MelonLogger.Msg("[InventoryExpansion][BackpackPanel]   Pivot: {0}", rt.pivot);
			MelonLogger.Msg("[InventoryExpansion][BackpackPanel]   SizeDelta: {0}", rt.sizeDelta);
			MelonLogger.Msg("[InventoryExpansion][BackpackPanel]   AnchoredPosition: {0}", rt.anchoredPosition);
			MelonLogger.Msg("[InventoryExpansion][BackpackPanel]   OffsetMin: {0}", rt.offsetMin);
			MelonLogger.Msg("[InventoryExpansion][BackpackPanel]   OffsetMax: {0}", rt.offsetMax);
		}

		private static void LogTMPTextDetails(string label, TMP_Text tmp)
		{
			MelonLogger.Msg("[InventoryExpansion][BackpackPanel] {0}:", label);
			MelonLogger.Msg("[InventoryExpansion][BackpackPanel]   Font: {0}", tmp.font != null ? tmp.font.name : "NULL");
			MelonLogger.Msg("[InventoryExpansion][BackpackPanel]   FontSize: {0}", tmp.fontSize);
			MelonLogger.Msg("[InventoryExpansion][BackpackPanel]   Color: {0}", tmp.color);
			MelonLogger.Msg("[InventoryExpansion][BackpackPanel]   Alignment: {0}", tmp.alignment);
			MelonLogger.Msg("[InventoryExpansion][BackpackPanel]   Text: '{0}'", tmp.text);
		}
	}
}


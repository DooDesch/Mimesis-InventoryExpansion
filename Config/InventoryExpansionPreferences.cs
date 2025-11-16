using System;
using MelonLoader;
using UnityEngine;

namespace InventoryExpansion.Config
{
	internal static class InventoryExpansionPreferences
	{
		private const string CategoryId = "InventoryExpansion";

		private static MelonPreferences_Category _category;
		private static MelonPreferences_Entry<bool> _enabled;
		private static MelonPreferences_Entry<int> _additionalSlots;
		private static MelonPreferences_Entry<string> _backpackKey;
		private static MelonPreferences_Entry<bool> _reduceMovementSpeed;

		internal static void Initialize()
		{
			if (_category != null)
			{
				return;
			}

			_category = MelonPreferences.CreateCategory(CategoryId, "InventoryExpansion");
			_enabled = CreateEntry("Enabled", true, "Enabled", "Enable InventoryExpansion functionality. When disabled, the mod will not modify game behavior.");
			_additionalSlots = CreateEntry(
				"AdditionalSlots",
				4,
				"Additional Inventory Slots",
				"Number of extra inventory slots to add on top of the game's default inventory size. Valid values: 4, 9, or 16 (for square grids: 2x2, 3x3, or 4x4). Other values will be rounded to the nearest valid option."
			);
			_backpackKey = CreateEntry(
				"BackpackKey",
				"C",
				"Backpack Toggle Key",
				"Key to toggle backpack visibility. Press to switch between standard inventory and backpack."
			);
			_reduceMovementSpeed = CreateEntry(
				"ReduceMovementSpeed",
				true,
				"Reduce Movement Speed",
				"When enabled, player movement speed is reduced to 50% while the backpack is fully open."
			);
		}

		private static MelonPreferences_Entry<T> CreateEntry<T>(string identifier, T defaultValue, string displayName, string description = null)
		{
			if (_category == null)
			{
				throw new InvalidOperationException("Preference category not initialized.");
			}

			return _category.CreateEntry(identifier, defaultValue, displayName, description);
		}

		internal static bool Enabled => _enabled.Value;

		private static int ClampToValidBackpackSize(int value)
		{
			if (value <= 0)
			{
				return 4;
			}

			if (value <= 6)
			{
				return 4;
			}

			if (value <= 12)
			{
				return 9;
			}

			return 16;
		}

		internal static int AdditionalSlots
		{
			get
			{
				if (_additionalSlots == null)
				{
					return 4;
				}

				int value = _additionalSlots.Value;
				int clampedValue = ClampToValidBackpackSize(value);

				if (clampedValue != value)
				{
					_additionalSlots.Value = clampedValue;
				}

				return clampedValue;
			}
		}

		internal static KeyCode BackpackKey
		{
			get
			{
				if (_backpackKey == null)
				{
					return KeyCode.C;
				}

				string keyString = _backpackKey.Value;
				if (string.IsNullOrEmpty(keyString))
				{
					return KeyCode.C;
				}

				if (Enum.TryParse<KeyCode>(keyString, true, out KeyCode keyCode))
				{
					return keyCode;
				}

				return KeyCode.C;
			}
		}

		internal static bool ReduceMovementSpeed => _reduceMovementSpeed?.Value ?? true;
	}
}


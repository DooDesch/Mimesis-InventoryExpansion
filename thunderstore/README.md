# MIMESIS - InventoryExpansion

Expand your inventory with additional backpack slots that can be toggled on and off.

![Version](https://img.shields.io/badge/version-1.1.0-blue)
![Game](https://img.shields.io/badge/game-MIMESIS-purple)
![MelonLoader](https://img.shields.io/badge/MelonLoader-0.7.1+-green)
![Status](https://img.shields.io/badge/status-working-brightgreen)

## Features

- Add 4, 9, or 16 additional inventory slots (2x2, 3x3, or 4x4 grid)
- Toggle between standard inventory and backpack with a configurable key (default: C)
- Backpack slots are displayed in a custom backpack UI panel with animated slide-in/out
- Visual key hint displayed on the backpack showing the toggle key
- Backpack automatically hides when leaving the game or returning to the title screen
- Optional movement speed reduction (50%) while backpack is open for better gameplay balance
- Slots are properly integrated with the game's inventory system
- Fully configurable via MelonPreferences

## Configuration

- `Enabled`: Enable/disable the mod (default: `true`)
- `AdditionalSlots`: Number of extra inventory slots (4, 9, or 16 - will be rounded to nearest valid option)
- `BackpackKey`: Key to toggle backpack visibility (default: C)
- `ReduceMovementSpeed`: Reduce player movement speed to 50% while backpack is fully open (default: `true`)

## Installation

1. Install via Thunderstore Mod Manager
2. Or manually download and extract to `Mimesis/MelonLoader/Mods`

## Usage

1. Configure the number of additional slots in the mod settings
2. Press the configured toggle key (default: C) to show/hide the backpack
3. When the backpack is visible, scrolling will only affect backpack slots
4. When the backpack is hidden, scrolling will only affect the standard 4 slots
5. The backpack automatically hides when you leave the game or return to the title screen


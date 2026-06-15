# MIMESIS - InventoryExpansion

> Adds extra backpack inventory slots you toggle on demand with a configurable key, shown in a custom animated panel that slides in and out.
> Carry more loot without permanently cluttering the standard 4-slot hotbar. Standalone, no MimicAPI required.

![Version](https://img.shields.io/badge/version-1.3.1-blue)
![Game](https://img.shields.io/badge/game-MIMESIS-purple)
![MelonLoader](https://img.shields.io/badge/MelonLoader-0.7.3+-green)
![Status](https://img.shields.io/badge/status-working-brightgreen)

## Features

- Adds 4, 9, or 16 extra inventory slots laid out as a square grid (2x2, 3x3, or 4x4); the count is configurable. The new slots are real, usable inventory, not just a visual overlay.
- Toggle between the standard hotbar and the backpack with a configurable key (default `C`); the mod reads the key live each frame.
- Extra slots render in a custom backpack UI panel that uses the bundled sprite and slides in and out with a short eased animation. If the image is missing it falls back to a translucent black panel.
- Shows a visual key hint on the panel displaying the currently configured toggle key.
- Context-aware slot scrolling: while the backpack is open, slot cycling affects only the backpack slots; while it is closed, it affects only the standard 4 slots.
- Optional movement-speed reduction to 50% while the backpack is fully open, restored automatically when it closes.
- Automatically hides the panel during loading screens and map changes, and when you leave the game or return to the title screen.

## Requirements

| Component | Version |
|-----------|---------|
| MIMESIS | 0.3.0 (current Steam build) |
| MelonLoader | 0.7.3+ |

Dependency: [LavaGang-MelonLoader-0.7.3](https://thunderstore.io/c/mimesis/p/LavaGang/MelonLoader/).

## Installation

### Recommended: Thunderstore mod manager

Install through a Thunderstore mod manager such as [r2modman](https://thunderstore.io/package/ebkr/r2modman/) or [Gale](https://thunderstore.io/package/Kesomannen/GaleModManager/). The MelonLoader dependency and the backpack asset are handled automatically.

### Manual

1. Install [MelonLoader 0.7.3](https://melonwiki.xyz/#/) into MIMESIS.
2. Download this package and place `InventoryExpansion.dll` into `MIMESIS/Mods/`.
3. Place the bundled `Backpack.png` at `MIMESIS/Mods/Assets/Backpack.png` (an `Assets` subfolder next to the DLL). Without it the panel still works but renders as a plain translucent box.
4. Launch the game once to generate the configuration file at `UserData/MelonPreferences.cfg`.

## Configuration

Stored in `UserData/MelonPreferences.cfg` under the `[InventoryExpansion]` category. You can also edit these through a MelonPreferences UI.

| Option | Description | Default | Values/Range |
|--------|-------------|---------|--------------|
| `Enabled` | Enable InventoryExpansion functionality. When disabled, the mod will not modify game behavior. | `true` | `true` / `false` |
| `AdditionalSlots` | Number of extra inventory slots to add on top of the game's default inventory size. Valid values: 4, 9, or 16 (square grids 2x2, 3x3, 4x4). Other values are rounded to the nearest valid option. | `4` | `4`, `9`, or `16` |
| `BackpackKey` | Key to toggle backpack visibility. Press to switch between standard inventory and backpack. | `C` | Any Unity key name (case-insensitive). Invalid or empty values fall back to `C`. |
| `ReduceMovementSpeed` | When enabled, player movement speed is reduced to 50% while the backpack is fully open. | `true` | `true` / `false` |

## Usage

- Press the configured toggle key (default `C`) to slide the backpack panel in and out. The panel sits at the bottom-right of the screen and shows a key hint.
- When the backpack is open, slot scrolling and selection cycle only the backpack slots; when it is closed, they cycle only the standard 4 hotbar slots.
- With `ReduceMovementSpeed` on, you move at 50% speed while the backpack is fully open, and full speed is restored when it closes.
- The panel auto-hides during loading screens and map changes, and when you leave the game or return to the title screen.

Source code and issues: [github.com/DooDesch/Mimesis-InventoryExpansion](https://github.com/DooDesch/Mimesis-InventoryExpansion).

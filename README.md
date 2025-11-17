# Mimesis InventoryExpansion Mod

A MelonLoader mod for Mimesis that expands your inventory with additional backpack slots.

---

## Table of Contents

- [Features](#features)
- [Requirements](#requirements)
- [Installation](#installation)
- [Configuration](#configuration)
- [Usage](#usage)
- [Development](#development)
- [License](#license)

---

## Features

- Add **4, 9, or 16 additional inventory slots** (2x2, 3x3, or 4x4 grid)
- **Toggle between standard inventory and backpack** with a configurable key (default: `C`)
- Backpack slots are displayed in a **custom backpack UI panel** with animated slide-in/out
- **Visual key hint** displayed on the backpack showing the toggle key
- Backpack **automatically hides** when leaving the game or returning to the title screen
- **Optional movement speed reduction** (50%) while backpack is open for better gameplay balance
- Slots are **properly integrated** with the game's inventory system
- **Fully configurable** via MelonPreferences

---

## Requirements

| Component | Version |
|-----------|---------|
| **Mimesis** | Latest Steam build |
| **MelonLoader** | 0.7.1 or higher |

---

## Installation

### Option 1: Thunderstore Mod Manager (Recommended)

1. Install via Thunderstore Mod Manager

### Option 2: Manual Installation

1. Download the latest release from the [releases page](../../releases)
2. Extract and place the files into your Mimesis mods directory:
   ```
   Mimesis/MelonLoader/Mods/InventoryExpansion.dll
   ```
3. Launch the game once to generate the configuration file

> **Note:** The configuration file will be created automatically on first launch at `UserData/MelonPreferences.cfg`

---

## Configuration

Configuration values are stored in `UserData/MelonPreferences.cfg` under the `InventoryExpansion` category.

### Available Options

| Option | Description | Default | Range |
|--------|-------------|---------|-------|
| `Enabled` | Enable/disable the mod | `true` | `true` / `false` |
| `AdditionalSlots` | Number of extra inventory slots | `9` | `4`, `9`, or `16` |
| `BackpackKey` | Key to toggle backpack visibility | `C` | Any valid key |
| `ReduceMovementSpeed` | Reduce player movement speed to 50% while backpack is fully open | `true` | `true` / `false` |

> **Note:** `AdditionalSlots` will be rounded to the nearest valid option (4, 9, or 16) if an invalid value is set.

---

## Usage

1. **Configure** the number of additional slots in the mod settings
2. **Press the configured toggle key** (default: `C`) to show/hide the backpack
3. **When the backpack is visible**, scrolling will only affect backpack slots
4. **When the backpack is hidden**, scrolling will only affect the standard 4 slots
5. The backpack **automatically hides** when you leave the game or return to the title screen

---

## Development

### Prerequisites

| Component | Version / Link |
|-----------|----------------|
| **Mimesis** | [Latest Steam build](https://store.steampowered.com/app/2827200/MIMESIS/) |
| **.NET SDK** | [8.0+](https://dotnet.microsoft.com/download) |
| **IDE** | [Visual Studio 2022](https://visualstudio.microsoft.com/) or similar |
| **MelonLoader** | [Latest version](https://melonwiki.xyz/#/) |
| **Workspace** | Access to a Workspace repository with game DLLs |

### Project Structure

```
InventoryExpansion/
├── Config/
│   └── InventoryExpansionPreferences.cs    # Configuration system
├── Patches/
│   ├── BackpackPanelPatch.cs              # Backpack UI panel
│   ├── InventoryControllerPatches.cs      # Inventory slot extension
│   ├── InventoryUiPatches.cs              # UI slot creation
│   ├── InventorySelectionPatches.cs       # Slot selection logic
│   └── GameConfigPatches.cs               # Game config modification
├── Assets/
│   └── Backpack.png                        # Backpack UI asset
├── Core.cs                                 # Main entry point
└── InventoryExpansion.csproj              # Project file
```

### Key Files

- **`Core.cs`** - Main entry point and mod initialization
- **`Config/InventoryExpansionPreferences.cs`** - Configuration management
- **`Patches/BackpackPanelPatch.cs`** - Backpack UI panel implementation
- **`Patches/InventoryControllerPatches.cs`** - Inventory slot extension logic
- **`Patches/InventoryUiPatches.cs`** - UI slot creation and management
- **`Patches/InventorySelectionPatches.cs`** - Slot selection and scrolling logic
- **`Patches/GameConfigPatches.cs`** - Game configuration modifications

### Build & Deploy

#### Local Build

The project is configured to automatically copy the built DLL to the game's Mods directory. Update the paths in `InventoryExpansion.csproj` to match your setup:

| Property | Description |
|----------|-------------|
| `ModsDirectory` | Path to `MIMESIS/Mods` folder |
| `GameExePath` | Path to `MIMESIS.exe` |

#### Automated Releases (GitHub Actions)

1. Update the version in `InventoryExpansion.csproj`
2. Commit and push the changes
3. Create a Git tag: `git tag v1.0.1`
4. Push the tag: `git push origin v1.0.1`

The GitHub Actions workflow will automatically:
- Build the project
- Create a GitHub Release
- Upload to Thunderstore (if configured)

---

## License

This project is provided as-is under the **MIT License**. Contributions are welcome via pull requests.

---

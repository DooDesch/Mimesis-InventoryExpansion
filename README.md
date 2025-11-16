# Mimesis InventoryExpansion Mod

A MelonLoader mod for Mimesis that expands your inventory with additional backpack slots.

## Features

- Add 4, 9, or 16 additional inventory slots (2x2, 3x3, or 4x4 grid)
- Toggle between standard inventory and backpack with a configurable key (default: C)
- Backpack slots are displayed in a custom backpack UI panel
- Slots are properly integrated with the game's inventory system
- Fully configurable via MelonPreferences

## Installation

1. Install via Thunderstore Mod Manager
2. Or manually download and extract to `Mimesis/MelonLoader/Mods`

## Configuration

- `Enabled`: Enable/disable the mod (default: `true`)
- `AdditionalSlots`: Number of extra inventory slots (4, 9, or 16 - will be rounded to nearest valid option)
- `BackpackKey`: Key to toggle backpack visibility (default: C)

## Usage

1. Configure the number of additional slots in the mod settings
2. Press the configured toggle key (default: C) to show/hide the backpack
3. When the backpack is visible, scrolling will only affect backpack slots
4. When the backpack is hidden, scrolling will only affect the standard 4 slots

## Development

### Prerequisites

- [Mimesis](https://store.steampowered.com/app/2827200/MIMESIS/) (latest Steam build)
- [.NET SDK 8.0+](https://dotnet.microsoft.com/download)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) or similar IDE
- [MelonLoader](https://melonwiki.xyz/#/)
- Access to a Workspace repository with game DLLs

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

### Build & Deploy

**Local Build:**
The project is configured to automatically copy the built DLL to the game's Mods directory. Update the paths in `InventoryExpansion.csproj` to match your setup:
- `ModsDirectory`: Path to MIMESIS/Mods folder
- `GameExePath`: Path to MIMESIS.exe

**Automated Releases (GitHub Actions):**
1. Update the version in `InventoryExpansion.csproj`
2. Commit and push the changes
3. Create a Git tag: `git tag v1.0.1`
4. Push the tag: `git push origin v1.0.1`

The GitHub Actions workflow will automatically build, create a GitHub Release, and upload to Thunderstore.

## License

Provided as-is under the MIT License. Contributions welcome via PR.

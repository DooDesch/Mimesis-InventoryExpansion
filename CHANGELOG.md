# Changelog

All notable changes to InventoryExpansion are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/), and this
project adheres to Semantic Versioning.

## [1.4.5] - 2026-09-04

### Changed
- The mod is built against MIMESIS 0.3.1 and tested in a running game on that build. Inventory behaviour does not change.

### Fixed
- The log says why the backpack image did not load. Before, two failures were silent, thus a missing backpack image looked the same as a mod that never started.

## [1.4.4] - 2026-06-23

### Fixed
- The backpack image shows up again after installing from Thunderstore or Nexus. The
  release packages did not include Backpack.png, so the backpack panel had no image.
  The image is now bundled inside the mod itself, so no separate file is needed. You
  can still override it by placing your own Backpack.png in an "Assets" folder next to
  the mod DLL.

## [1.4.3] - 2026-06-23

### Fixed
- Multiplayer: fixed a backpack pickup issue that could make items appear duplicated
  on clients. The backpack-first pickup runs on the host for every player, but was
  decided from the local backpack state, so another player's pickup could be redirected
  based on the host's backpack. It now only applies to your own inventory.

### Added
- Large items (the ones you cannot switch away from while holding them, such as
  two-handed items) can no longer be picked up into the backpack while it is open; a
  short message is shown instead.
- The backpack can no longer be opened while you are holding a large item.
- New "Block Large Items In Backpack" setting (on by default) controls both of the
  above. Disable it to allow large items in the backpack again.

## [1.4.2] - 2026-06-22

### Fixed
- The mod's Harmony patches were applied twice (MelonLoader auto-applies them, and the mod also called PatchAll() itself), so every patch ran twice. The redundant call was removed; patches now apply exactly once.

## [1.4.1] - 2026-06-17

### Changed
- Started maintaining a full changelog that is now published on GitHub, Thunderstore
  and Nexus. No gameplay changes compared to 1.4.0.

## [1.4.0] - 2026-06-15

### Added
- Configurable cursor handoff so the mouse cursor behaves predictably when the
  backpack is open.
- Backpack-first pickup: picked-up items now go into the backpack slots first.

### Fixed
- Corrected the position of durability and stack-count labels on backpack slots.
- Fixed the slot label offset by scaling slots through their local scale, so labels
  line up at every grid size.

## [1.3.1] - 2026-06-15

### Fixed
- Compatibility with the Mimesis 0.3.0 game update (avatar detection now resolves
  again on the new game build).
- The backpack is now hidden during loading screens.
- Fixed the backpack panel breaking after a map change.

### Changed
- Pinned the MelonLoader dependency to 0.7.3.
- Refreshed the README with structured content, badges, and detailed configuration
  options.

## [1.1.0] - 2025-11-17

### Added
- Movement speed is now reduced while the backpack is open.
- Backpack visibility now respects the game's pause state, and the key hint text was
  improved.

## [1.0.0] - 2025-11-16

### Added
- Initial release. Expand your inventory with additional backpack slots and toggle
  between the standard inventory and the backpack with a configurable key. Includes
  the backpack image, UI layout, and inventory slot selection handling.

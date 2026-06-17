# Changelog

All notable changes to InventoryExpansion are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/), and this
project adheres to Semantic Versioning.

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

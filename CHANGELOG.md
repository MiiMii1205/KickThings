# Changelog

## [1.2.0] - 2026-04-06

### Changed

- Fixed a bug with the mod registry
- Replaced Item, Mobs and Rope RPCs with ownership transfer. 
  - This makes it compatible with clients not using the mod

## [1.1.2] - 2026-04-06

### Changed

- Added descriptive logs for when mod support is lacking.
- Changed mob kick RPCs

## [1.1.1] - 2026-04-05

### Fixed

- Fixed bugs with item kicking.

## [1.1.0] - 2026-04-05

### Added

- Berry bearing object support.
    - Palmtrees, willows, cactuses and nests can now react to getting kicked. 
    - Berries on vines will also drop when kicking vines.
- Spider support.
  - Spiders can now be kicked, getting stunned for a couple of seconds.
  
### Fixed

- Fixed a bug for kinematic items that dont get dynamic after being kicked.

### Changed

- Small refactors/cleanup

## [1.0.0] - 2026-04-03

- Initial Release
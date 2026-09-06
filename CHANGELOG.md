# Changelog

## 0.1.2 - 2026-09-06

- Fixed Contract Configurator passenger lookup by reading loaded
  `SpawnPassengers` dictionaries as well as `KerbalNames()`.
- Contract Configurator groups now use their real title when available, with a
  descriptive ID fallback instead of the generic label.
- Clarified that destination ship-view picking requires right-clicking the part.

## 0.1.1 - 2026-09-06

- Fixed identification for Contract Configurator contracts with spawned passengers.
- Reordered the window so tourist selection comes before destination selection.
- Added destination picking from the ship view via its part-action window.

## 0.1.0 - 2026-09-06 (initial alpha)

- Added a crew-compartment Tourist Transfer part action via ModuleManager.
- Added active stock tourism-contract grouping with group/individual selection.
- Added same-vessel destination selection, free-seat counts and capacity-limited
  bulk transfers in source order.
- Added live eligibility checks, rollback on rejected moves, per-crew stock
  notifications, portrait refresh and window/input-lock cleanup.
- Added standalone transaction regression tests and a local KSP build/package
  script with warnings treated as errors.
- Established README, SPEC and DEVELOPMENT continuation documentation.
- In-game acceptance testing remains pending; not yet a validated game release.

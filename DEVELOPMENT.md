# Development continuity

## Starting state inspected - 2026-09-06

Repository: https://github.com/maddavo/TouristTransfer

GitHub and a fresh clone both showed an empty repository: no commits, branches,
source, docs, issues or pull requests. There was no earlier implementation to
preserve. The referenced KSP Tourist Transfer Mod conversation supplied the user
workflow; API claims were checked independently against local KSP assemblies.
No repository AGENTS.md was present. The user's requested continuity documents
are maintained here, in README.md, SPEC.md and CHANGELOG.md.

## Architecture and decisions

- `ModuleTouristTransfer`: stateless PAW entry point, attached by a ModuleManager
  patch. No save migration and no custom persistent contract/roster state.
- `StockContracts`: stock active-contract adapter. Contract GUID plus exact
  tourist name preserves membership identity even with duplicate titles.
- `TouristTransferWindow`: flight-only Unity IMGUI, source/destination selection,
  periodically refreshed snapshots, deferred execution and lifecycle cleanup.
- `KspTransferContext`: validates actual parts/vessel/contracts and adapts native
  KSP crew operations to the independently testable transaction engine.
- `TransferBatch`: order-preserving, capacity-limited transaction runner with
  deduplication, live checks, restoration and post-commit notification handling.
- `build.ps1`: uses Windows' .NET Framework compiler, runs tests with warnings as
  errors, then builds against local KSP/Unity and packages only mod artifacts.
  No SDK/NuGet dependency. C# 5 syntax and .NET Framework 4.x runtime target.

## API evidence

Inspected installed KSP 1.12.5 Assembly-CSharp.dll metadata and relevant method
bodies locally using its bundled Mono.Cecil. Confirmed stock TourismContract
Tourists, contract GUID/title and active-contract query, crewTransferAvailable,
Part.RemoveCrewmember/AddCrewmember, crew notifications and Vessel crew refresh.
Stock CrewTransfer.MoveCrewTo removes/adds crew, fires onCrewTransferred, calls
CrewWasModified, despawns crew and spawns them on a later frame. This plugin uses
those operations with additional validation, capacity handling and restoration.
No proprietary assembly content is included in this repository or package.

Also inspected the public [ShipManifest source](https://github.com/PapaJoesSoup/ShipManifest)
for context. This is an independent implementation; no ShipManifest code or
assets are distributed. ModuleManager is an external dependency.

## Verification and current status

Verified on Windows on 2026-09-06:

- `build.ps1 -KspRoot <local Steam KSP>` passed against the installed game's
  Assembly-CSharp and Unity assemblies (KSP build 03190; ModuleManager 4.2.3 is
  present). The initial missing UnityEngine.UI compiler reference was corrected.
- Plugin and both test executables compile with warnings treated as errors.
- **15 transaction tests passed**: whole/partial/full transfers, occupancy,
  ordering, deduplication, stale source/eligibility, capacity changes, rejected or
  throwing adds/removes, partial-add rollback, failed restoration, listener
  failure after commit and empty selection.
- **17 adapter tests passed** using the actual StockContracts and
  KspTransferContext source with explicitly simulated KSP models: exact names,
  duplicate titles, stock-only type filtering, contract identity changes,
  same-part/cross-vessel/packed/removed-part restrictions, source/destination
  flags, partial execution and notification endpoints.
- Adapter models are test doubles, not a running KSP instance. They do not
  validate Unity destroyed-object behavior, native roster side effects,
  ModuleManager, IVA, UI layout, portraits or persistence.
- Installation ZIP contents and packaged DLL hash are checked by build.ps1.
- Follow-up 0.1.1 addresses a real-save finding: the inspected save uses
  Contract Configurator `SpawnPassengers` contracts, whose names come from
  `ConfiguredContract.KerbalNames()` rather than stock `TourismContract.Tourists`.
  The UI now selects tourists before destinations and supports ship-view picking.
- Follow-up 0.1.2 reads Contract Configurator `SpawnPassengers.passengers`
  dictionaries when `KerbalNames()` is incomplete, and resolves the title from
  the runtime contract type. This targets the Hotel/quicksave #99 observation.
- Follow-up 0.1.3 includes completed stock and Contract Configurator tourism
  contracts when their named tourists are still aboard, labelled `(completed)`.
- Follow-up 0.1.4 fixes the screenshot's hash-only Contract Configurator label by
  reading the inherited public KSP `Title` property first.
- Follow-up 0.1.5 removes a tourist from completed groups when that tourist is
  also present in any current group, matching the active-return-contract rule.
- Follow-up 0.1.6 resolves every duplicate membership: a current contract wins;
  otherwise the later completed contract wins. This targets Rogas in quicksave
  #109, who is retained only under the later 14-tourist Hotel contract.
- Follow-up 0.1.7 adjusts only the window presentation: narrower width, smaller
  local font and taller tourist/destination lists. Installation is intentionally
  pending until KSP is confirmed closed.

In-game tests have not been performed. The game installation and saves have not
been modified. This is an initial alpha, not a flight-validated release.

## Next required verification (same feature scope)

Use a copied test save and install the alpha package with ModuleManager 4.2.3.
Test the acceptance scenarios in SPEC.md, especially:

1. Existing docked craft shows the part action; select stock contract tourists.
2. Transfer full and partial groups; verify roster and remaining selections.
3. Check IVA seats/portraits, contract progress, then save/reload the test save.
4. Change vessel/undock/remove destination while the window is open; ensure stale
   operations cannot transfer to another vessel.
5. Close/hide/switch scenes and check mouse/flight controls are released.

Record KSP version, installed mod versions, actions and observed results here.
Treat mixed-mod compatibility as unverified until tested. Keep Contract
Configurator support outside this version. Do not install into or edit the
player's live career as part of an automated build.

## Continuation procedure

Inspect git status/history and all four continuity documents before editing.
Keep SPEC aligned with implemented behavior, CHANGELOG with versioned changes,
README with user instructions and DEVELOPMENT with actual checks and remaining
limitations. Run build.ps1 against KSP 1.12.5 before packaging. Never replace
game-only verification with stub/test success or claim a runtime test from a
successful compilation.

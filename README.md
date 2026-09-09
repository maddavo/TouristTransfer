# TouristTransfer

A KSP 1 mod for moving tourists between crew compartments on the same vessel,
grouped by their **active or completed tourism contracts**. Docked ships count as one
vessel. Select a whole contract or individual tourists, choose a destination,
and transfer as many as will fit. Remaining tourists stay in the source.

**Version 0.1.7: initial alpha update.** Compiled against KSP 1.12.5; automated transfer
tests pass. In-game acceptance testing is still required; this is not yet a
flight-validated release.

## Requirements and installation

- KSP **1.12.5** (initial supported target).
- Source code is licensed under the MIT License.
- [ModuleManager](https://github.com/sarbian/ModuleManager) **4.2.3**, installed
  separately. Do not install a second copy if your game already has it.
- Extract the package's `GameData/TouristTransfer` folder into KSP's `GameData`.
  The DLL belongs at `GameData/TouristTransfer/Plugins/TouristTransfer.dll`.
- Restart KSP. Use a backed-up test save for this initial alpha.

## Use

1. In flight, right-click the source crew compartment on the active vessel.
2. Click **Tourist Transfer...**.
3. Select a contract checkbox, or individual tourists beneath it.
4. Select a destination compartment. Its numeric part ID distinguishes identical
   modules; its available-seat count is shown.
5. Click **Transfer selected**. Tourists move in source crew-list order until
   seats run out. The result reports how many moved and how many did not.

Multiple contracts can be selected together. Other crew and tourists without an
active stock contract are listed but cannot be selected. After a partial move,
remaining eligible tourists stay selected so you can choose another destination.
Each tourist appears in one group. A current contract takes precedence over a
completed one; if every matching contract is completed, the latest completed
contract is shown.

The vessel must be active, loaded and unpacked; both compartments must permit
crew transfer. Finish any stock item/crew transfer before using the button.
Switching vessels or losing the source closes the window. F2 hides it and
releases its mouse-over flight-control lock.

## Scope and limitations

- Stock `FinePrint.Contracts.TourismContract` is supported. Contract Configurator
  contracts with spawned passengers are also read when CC is installed; CC is
  optional.
- One source and one destination per operation; no EVA, undocked-vessel transfer,
  swaps, overflow redistribution, or ordinary crew transfers.
- Honors the stock part `crewTransferAvailable` flag. Does not implement
  Connected Living Space routing or third-party transfer-selection interception.
  Compatibility with crew/inventory mods is not yet tested.
- Uses stock roster operations and per-tourist crew-transferred notifications,
  then refreshes crew portraits. These effects still need an in-game check.
- The initial UI is English. Very small screen layouts are not yet validated.

To uninstall, close KSP and remove only `GameData/TouristTransfer`. No contracts,
roster entries or custom scenario state are created. Saves may retain a harmless
missing PartModule warning after removal; do not remove unrelated mod files.

## Build and verify

On Windows with .NET Framework 4.x installed:

```powershell
.\build.ps1 -KspRoot 'C:\Games\Kerbal Space Program'
# Only run the standalone transaction tests (no KSP needed):
.\build.ps1 -TestOnly
```

The script compiles and runs tests first, compiles the plugin against your local
KSP assemblies, and creates `dist/TouristTransfer-0.1.0.zip`. It never copies to
your game. KSP/Unity assemblies and ModuleManager are not redistributed.

See [SPEC.md](SPEC.md) for behavior and acceptance criteria,
[DEVELOPMENT.md](DEVELOPMENT.md) for continuation evidence and remaining checks,
and [CHANGELOG.md](CHANGELOG.md) for version history.

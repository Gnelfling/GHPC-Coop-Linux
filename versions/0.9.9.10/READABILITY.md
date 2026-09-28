# Local readability review — 0.9.9.9

This directory is an unpublished readability edition of the latest 0.9.9.9 source.
The archived releases and running game installations were not changed.

## Scope and changes

- All 51 C# files (40 production files and 11 test/probe files): consistent indentation, separate statements and using directives, expanded blocks, and continuation lines for long declarations, argument lists and conditions.
- All 17 PowerShell files: consistent indentation and spacing, expanded control-flow blocks, and separate statements. UTF-8 with BOM preserves non-ASCII text under Windows PowerShell 5.1.
- All five JSON files: parsed and inspected. The four updater script hashes were regenerated because formatting changes file bytes; release URLs and DLL hashes are unchanged.
- Markdown, command launcher, checksums, TSV and licensing files: inspected. Existing readable content, third-party notices and binary assets are preserved.

The initial formatting pass preserved all syntax tokens. A subsequent readability refactor renamed reviewed identifiers in five modules, extracted the ammo catalog helper, and named infantry interpolation thresholds. This does not redesign networking or claim to fix additional bugs. Some long literal strings are intentionally retained to avoid altering messages, paths or embedded content.

## Suggested reading order

1. `src/CoopLabMod.cs` — mod entry point and lifecycle.
2. `src/MultiCoop.cs` — multiplayer message dispatch and update flow.
3. `src/Protocol.cs` — wire messages and validation.
4. `src/GameBridge.cs` — connection to native game objects.
5. Individual synchronization modules, then their tests.

### Connection and sessions

`ILink`, `Transport`, `SteamLink`, `DirectConnection`, `MultiRoom`, `RoomJoin`,
`RoomSeats`, `SteamRoomPolicy`, `PeerIsolation`, `SessionPause`.

### Simulation and presentation

`AmmoSync`, `ReloadPolicy`, `ShotInputLatch`, `DamageSync`, `EquipmentSync`,
`HelicopterSync`, `InfantrySync`, `InfantryHealth`, `InfantryMotion`,
`ObjectiveSync`, `SupportSync`, `CombatVisuals`, `TracerSync`, `RenderingSync`,
`VehicleAudioSync`, `ReplicaCrewAudio`, `WeatherSync`, `Nameplates`.

### Menus, missions and diagnostics

`CoopMenu`, `SteamMenu`, `MissionChoices`, `MissionConfiguration`, `MissionSweep`,
`ShotAudit`, `DamageRegression`, `SupportRegression`.

### Installation and updating

Start with `Install.cmd` / `Setup.ps1`, then `Install.ps1` and `Install-Updater.ps1`.
`Launcher.ps1` supplies the launcher UI; `Update-Core.ps1` implements updates and recovery.
The `*Test.ps1` files exercise temporary fixture installations, not a real game session.

## Verification

- C# and PowerShell formatting checks preserve ordered syntax tokens, excluding PowerShell newline tokens.
- The original formatting-only build was byte-identical to the published DLL:
  `F1120E59F69C1D9DA5724993ADB7946F0EA2BF02708FC93884B57F2972D1C671`.
- All 361 existing C# automated checks passed.
- All 28 updater checks passed under Windows PowerShell 5.1.
- Launcher installation checks passed.
- Installer validation, backup, preservation of other mods, and injected-failure rollback checks passed.

Logs are in the parent directory. `FILE-REVIEW.csv` records every file from the original source bundle.
No live gameplay retest or GitHub publication was performed for this formatting-only edition.

## Readability refactor (local only)

See DEVELOPMENT.md for the current refactor scope and validation. Renamed parameters and private members change assembly metadata, so the refactored DLL is not expected to retain the release hash. The build lock is pinned to the new local output after verification. The release payloads and updater manifests retained in this folder describe the published release, not this development build. Do not distribute this directory as a new installer.

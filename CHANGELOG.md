# 0.9.9.6 — Experimental fixes for reported co-op issues

This update adds code fixes for reported issues. **GHPC was not launched for this release, at the user's request. The new gameplay behavior is not runtime-verified.** The 3/4-instance gameplay tests documented for 0.9.9.5 must not be interpreted as tests of this build.

## Changes
- Add a separate infantry health/death replication path. Only matching squad/member/faction/model/damage-layout identities are applied; ambiguous or unmatched identities are skipped and logged. Changes are sent in bounded batches and periodically refreshed. Infantry movement, spawning, disembarkation and ragdoll positions are **not** synchronized by this change; the reported infantry issue needs live retesting.
- Transfer flex mission unit replacements, ammunition selections and infantry army overrides before loading the guest mission. Restore the guest's previous in-memory configuration when leaving. Preserve mission/vehicle roster validation. This does not transfer third-party mission files, arbitrary editor settings or campaign saves. Configuration size and missing local resources may still prevent joining.
- Restore the native local-player reload rules instead of forcing manual reload on human-loaded weapons. Remote vehicle feeds use the guest's reload preference and native forced modes. This deliberately honors the game's automatic/manual setting rather than applying one rule to every tank.
- Send host pause/AAR notices to guests and suppress guest driving/fire/reload requests while paused. Stop transmitting vehicle snapshots during host AAR/pause. Full AAR shot-history replay remains host-only; guest vehicle control pausing while the host simulation is paused is expected.
- Handle a missing platoon in mission-offer diagnostics without dereferencing null.
- Refactor objective display synchronization, preserve failure strikethroughs, cache objective key ordering, reuse ID sets, and make detailed objective diagnostics opt-in with `--coop-objective-diagnostics`. Snapshot buffers remain independently owned by queued sends.

## Updating
**All players must update together.** Network protocol 17 rejects older protocol-16 clients rather than connecting incompatible mission formats.

For an intact official installation, close GHPC and use the existing desktop launcher: **Check Updates / Update and Play**. The manifest and DLL remain compatible with the existing updater format. Old launcher scripts are not replaced by a DLL update.

If you installed a manually modified/test DLL or an old broken launcher, extract **GHPC-Coop-0.9.9.6-Setup.zip** into a new folder and run `Install.cmd` once. Integrity checks remain enabled; the updater will not silently overwrite an unrecognized local DLL.

## Verification and remaining work
- 265 existing protocol/transport, Steam-adapter, firing-input, peer-isolation and multi-peer fixture checks.
- 30 native reload-rule combinations, 8 infantry health-parser cases, 13 mission-configuration fixture checks.
- Updater integrity, backup and rollback fixture tests.
- Native game assembly compilation. Separate-path reproducible build comparison is recorded in REPRODUCIBLE.md.

These are code/build/fixture checks, **not** real gameplay, real Steam relay, or proof that all reports are fixed. Infantry identity matching, custom mission loading, actual auto-reloader behavior, pause/resume input behavior and objective UI all require live host/guest testing. Mod Manager compatibility remains unconfirmed.

Please report version, mission and changed settings, vehicle, player count, host/guest role, and relevant logs (remove personal information). Developed with ChatGPT/Codex assistance; unofficial, unsigned, and not independently security-audited.

# 0.9.9.5 — Multiplayer fixes, Direct IP and nameplates

I have addressed as many of the reported issues as I could reproduce or identify, and tested this build with three and four local game instances. This is still an experimental mod, not a claim that every multiplayer bug is fixed. If you encounter another issue, please report it with the mission, vehicle, player count, host/guest role and logs. I will investigate and aim to release follow-up fixes as soon as practical.

## Changes

- Steam and Direct IP use the same multiplayer host/session, vehicle allocation, movement, firing and damage code. Direct IP is an additional connection option, not a separate simplified simulation.
- Preserve the complete firing input when a click and subsequent changed aim/release arrive in the same host frame.
- Apply authoritative damage on receipt instead of waiting for visual interpolation.
- Isolate a remote player's vehicle-processing failure so other players can remain connected.
- Reset remote smoke-request tracking when vehicle control changes hands.
- Text-only vehicle nameplates: F9 toggles all names; Shift+F9 toggles your own name. Profile-image loading has been removed from the invitation menu.
- Smaller F8 menu positioned at the upper left.
- Preserve the 0.9.9.4 launcher compatibility, English errors and integrity checks.

## Actual local game tests

| Test | Result |
|---|---|
| 3 players, Slow Raid, PT-76B guests | Both guests joined; 8 shots each; 19 impact events matched between guests; M60A3 TTS destruction state matched. |
| 4 players, Under Pressure, M60A1 RISE P guests | All three guests joined; 6 shots each; 18 impact events matched on all three; T-55A destruction state matched. |
| One guest leaves a 4-player session | Remaining guests continued receiving matching state for over a minute. |
| Damage-state comparison | No mismatches in the completed repeat runs. |

Important limits: these were separate running games on one PC using TCP loopback and generated control inputs. Enemy destruction was deliberately triggered on the host to isolate state propagation; it was not a player-shell kill test. Impact positions were compared in replay logs. Manual sight/laser accuracy, distant targets, visual wreck appearance, real Steam relay latency/loss, every mission and other mods still need further testing. A first 3-player run left one vehicle unable to fire; that result was inconclusive and was not counted as a pass. Both vehicles fired in the repeat run.

265 protocol, adapter and regression checks also passed. These are separate from the actual game tests. Steam adapter checks use fixtures, not live Steam accounts.

## Updating

Close GHPC. If you already installed the working 0.9.9.4 launcher, use the desktop GHPC Co-op launcher **Check Updates / Update and Play**. The normal updater replaces the DLL. All players should update together.

If an old launcher fails, download **GHPC-Coop-0.9.9.5-Setup.zip**, extract into a new folder and run `Install.cmd` once. DLL updates do not replace old launcher scripts. A manually modified experimental DLL may fail the launcher's integrity check; use the new Setup ZIP to reinstall the published build. Do not disable hash checks.

## Direct IP

Host enters a mission, opens F8, selects DIRECT IP, then CREATE DIRECT ROOM. Guests join from the main menu using the host address, port and matching room code. Same-PC address: `127.0.0.1`; default TCP port: `22395`. Host defaults to this-PC-only binding. LAN hosting requires selecting LAN / DIRECT IP and a room code of at least 8 characters. No firewall or router rules are added automatically. Direct IP does not provide Steam identity verification or transport encryption; internet-facing hosting has not been validated.

Full source, pinned build instructions and SHA-256 checksums are included. The DLL was rebuilt in two directories with identical bytes. This is a local reproducibility check, not an independent security audit. Developed with substantial ChatGPT/Codex assistance; unsigned and unofficial, with no endorsement by the GHPC developers.


---

# 0.9.9.4 — Launcher compatibility and English error messages

This is a launcher/installer update. The mod DLL and Steam protocol are unchanged from 0.9.9.3.

- Launcher failures display an English explanation instead of exposing localized Windows exception text.
- Original error details are retained in launcher-errors.log in the updater folder (accessible through Backups).
- The updater and installer use .NET SHA-256/SHA-512 directly, avoiding dependence on the Get-FileHash PowerShell command. Integrity checks remain enabled.

## How to install this update
Download GHPC-Coop-0.9.9.4-Setup.zip, extract it, close GHPC and its launcher, and run Install.cmd once. Existing automatic updates replace only the mod DLL, not launcher scripts. Clicking Check Updates in an older launcher does NOT install this launcher fix, even if its installed version number advances.

After installing this setup, use the desktop GHPC Co-op Manager / Check Updates or GHPC Co-op / Update and Play for future compatible DLL updates. No local PC installation is performed by publishing this release.

Source and SHA-256 checksums are included. No new gameplay fixes, signing or security certification are claimed. Existing gameplay limitations remain.


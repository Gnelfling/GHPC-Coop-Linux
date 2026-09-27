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

# 0.9.9.18 — PvE lobby and guest telemetry update

Published to the stable launcher update channel at the project owner's request. Gameplay verification remains incomplete; the known issues below still apply.

## Changes
- Pre-mission co-op lobby: create a room, select a mission, ready up and start, with teammate AI controls.
- Servers / Friends / Direct IP connection choices and readable lobby text.
- Host speed and gear telemetry for the guest HUD, without enabling guest vehicle physics.
- Reused tracer visuals, opt-in detailed shot logs and bounded guest impact-effect spawning.
- Same-account Steam join errors now explain Direct IP testing and preserve the host lobby membership.
- Nameplate toggles, revised visibility checks and diagnostic logs for missing guest names.

## Update
Close GHPC and use Check Updates or Update and Play in the existing GHPC Co-op launcher. The loader and launcher do not need replacing. Everyone in a room must update: protocol 29 and exact DLL matching are required.
For a new installation, extract GHPC-Coop-0.9.9.18-Setup.zip and run Install.cmd.

## Known issues and validation
Guest nicknames can still disappear; the latest visibility changes need in-game confirmation. Guest machine-gun firing can still cause frame drops. AAR hit-ray rendering and full independent guest history remain incomplete. Reload and crew speech need continued testing. No complete gameplay fix or measured latency improvement is claimed.
Automated build, protocol, transport and replication fixtures passed. Separate-account Steam joining and the new lobby flow still need live verification. Same-account two-window tests use Direct IP 127.0.0.1.
This public build omits locally extracted game satellite textures; some mission previews may be unavailable. Both peers must use the published DLL, not a previous local test DLL.

To roll back, close GHPC and reinstall the 0.9.9.17 setup. Include both players' relevant logs when reporting issues, removing personal information before public posting.

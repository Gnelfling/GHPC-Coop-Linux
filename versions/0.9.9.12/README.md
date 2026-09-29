# GHPC Unofficial Co-op 0.9.9.12

Download the Setup ZIP, extract it and run Install.cmd. All players need matching builds. Up to four players including the host, one per eligible friendly vehicle.

# 0.9.9.12 — Native TOW wire visuals and synchronization safeguards

## Changes since 0.9.9.10
- Replaced the guest TOW wire's black placeholder with the game's native wire materials, color and rendering settings. The host's wire width is retained. The local tester approved the Bradley mission result.
- Prevented remote aiming from rotating detached turrets and from overwriting mount rotations during AAR.
- Create tracer visuals inactive and remove native behaviours before activation; disable cloned audio to avoid duplicate playback.
- Stop tracer capture and release cached wire references on cleanup.
- Split input, replication, AAR and visual code into focused files; improved names, array documentation and exception-handling explanations.
- Added host shot diagnostics. This is diagnostic logging, not a claimed fix for every aiming issue.

## Verification and limits
The release build passes 396 automated protocol, transport and policy checks. These are not full Unity gameplay tests. Native TOW wire appearance was accepted in a local two-instance Bradley test. Detached-turret/AAR safeguards need broader gameplay testing. Complete independent guest AAR history and universal custom-mission compatibility are not claimed.

## Install/update
Close GHPC before updating. Download the Setup ZIP, extract it and run Install.cmd, or use the existing updater. All players should run this release and matching supported game files. Protocol remains 25.


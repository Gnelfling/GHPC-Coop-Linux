# 0.9.9.17 — Experimental PvE update

**Pre-release: gameplay verification is incomplete.** This is offered for volunteer testing, not as a fully validated replacement for 0.9.9.16. The existing launcher/loader is unchanged. Stable automatic updates remain on 0.9.9.16.

## Changes
- Public Steam room browser with room names, mission names and player counts; public or friends-only hosting. Hosts create rooms after entering a mission; guests can browse from the main menu.
- Connection view shows measured Steam RTT after joining. Pre-join ping is not implemented.
- Room search and room-ID joins require the same game build and exact mod DLL.
- Host-owned platoon leader replication; guest selection should no longer independently promote its tank.
- Reload preference/switch forwarding, native reload recovery and replica crew-event handling changes.
- AAR information-box refresh and overpressure display-size correction.
- Condensed white, outlined nameplates with a Korean-capable font fallback.
- Reduced repeated allocations/reflection lookup and opt-in detailed aim logging; scene discovery fallback coverage correction.
- Experimental guest tracer occlusion checks for obstacles.

## Known limitations / help testing
- **Guest AAR is not fully synchronized.** It mirrors the host-selected shot, not a complete independent shot-history database. Exact hit locations, articulated poses and all damage details still need work and comparison.
- Guest reload/hit speech and T-80B automatic reload fixes need gameplay confirmation.
- No verified FPS or input-latency improvement is claimed. Scene discovery can still cause spikes and delayed spawn discovery.
- Tracer occlusion uses conservative collider checks and can hide partly visible streaks; optics views and performance need testing.
- Steam public discovery and joining between separate accounts have not been validated live. Two windows on one Steam account are not a substitute.

## Install
Close GHPC. Download **GHPC-Coop-0.9.9.17-Setup.zip**, extract it, and run the included Install.cmd. Everyone in the room must install this build. Protocol 28 is incompatible with 0.9.9.16 (protocol 26). The in-game build label remains 0.9.9.17-dev to identify its experimental status.

The existing stable-channel updater will not automatically install this pre-release. To roll back, close GHPC and reinstall the 0.9.9.16 setup. Existing loader/launcher scripts and Steam ownership checks are retained.

## Validation
Compilation and automated protocol/transport/reload/replication fixtures passed, including 48 fake-Steam adapter checks and 30 multi-client synchronization checks. These are not live Steam, audio, graphics or full gameplay verification. A local two-instance M1 mission connected and completed initial synchronization.

For reports, include both host and guest MelonLoader/Latest.log, mission/vehicle, which side shows the problem, and the approximate time. Remove personal details before publicly sharing logs.

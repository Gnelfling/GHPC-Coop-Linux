# GHPC Unofficial Co-op — 0.9.9.7

[Download](https://github.com/dnjsxoq013-debug/GHPC-Coop/releases/tag/v0.9.9.7) | [Source](versions/0.9.9.7/) | [Build](BUILD.md) | [Test results](versions/0.9.9.7/TEST-RESULTS.md)

# 0.9.9.7 — Cross-platoon seats and guest ammunition fixes

## Changes
- Up to four players can now use eligible vehicles across friendly platoons. The host's platoon is preferred; other friendly vehicles fill remaining seats. Original platoon membership is preserved, with one player per vehicle.
- Send the guest's NEXT ammunition selection to the host using the selected weapon's native ammunition rack. An already loaded round is not magically replaced, and an in-progress reload follows the game's normal rules.
- Synchronize ballistic ammunition independently of the current breech contents. Previously, a reload followed immediately by firing could occur between snapshots, leaving the guest's fire-control system using the previous ammunition type. This could produce incorrect elevation when switching HEAT/APFSDS.
- Initialize replicated weapon ammunition notifications and sample guest firing input later in the frame.
- Steam and Direct IP use these same gameplay/session changes.

## Verification
Three- and four-instance local Direct IP tests used the mission Inconceivable Intermission, including cross-platoon allocation and an automated guest M60A3 TTS firing sequence alternating ammunition types. The test reproduced stale guest ballistic ammunition before the fix; afterward, HEAT/APFSDS changes reached the replicas even when the breech snapshot was empty. Shot-audit launch direction matched the transmitted barrel direction to the log's precision.

327 code/transport fixture checks passed, including the empty-breech ballistic-type case. A separate-directory rebuild produced the same DLL hash.

This does **not** establish perfect sight-to-impact accuracy at every range. The automated test directs the native fire-control system at a fixed point; it is not a complete human reticle/laser interaction test. Live multi-account Steam relay, WAN latency, every vehicle/mission and other mods remain unverified. Please report remaining aiming or synchronization issues with mission, vehicle, ammunition, range, host/guest role and logs.

## Updating
Close the game and use **Check Updates / Update and Play** in the existing launcher. All players must update together: protocol 18 is incompatible with older versions.

Modified/test installations or broken old launchers should use the Setup ZIP once. The updater's integrity checks remain enabled. This release does not silently replace launcher scripts.


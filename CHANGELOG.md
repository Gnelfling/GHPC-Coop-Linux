# Unreleased — guest AAR trace recovery

See [development snapshot](development/aar-trace-recovery/) for changes, build results and verification limits. Restores missing/disabled trace objects and avoids duplicate labels. The reported guest rendering problem is not yet confirmed resolved in-game.

# 0.9.9.16 — PvE discovery allocation and identity-cache improvements

- Reuse infantry emplacement and aircraft identity hashes only while their full identity inputs remain unchanged; recompute after changes.
- Reuse aircraft discovery scratch containers and clear cached entries for destroyed/unloaded objects and session teardown.
- Preserve discovery coverage and frequency, including inactive runtime units. Damage, roster handling and event ordering are unchanged.

Includes the 0.9.9.15 network scheduling, resynchronization and Steam queue diagnostics improvements. Wire protocol remains 26 (incompatible with 0.9.9.14/protocol 25). Update host and guests together for consistent diagnostics. PvP is not included.

Validation: pinned build and regression fixtures passed. Actual scan-time savings and remote input latency have NOT been measured in-game for this release. Global searches remain; this does not claim to eliminate the reported scan stalls or 250–500 ms input delay.

Close GHPC and update through the existing compatible manager, or extract the Setup ZIP and run Install.cmd. Use Steam and a valid GHPC license. For lag reports send Bin/MelonLoader/Latest.log from both host and guest after reproduction and before restarting; remove personal information before sharing.


# 0.9.9.15 — PvE network scheduling, recovery and diagnostics

All players must update. Wire protocol 26 is incompatible with 0.9.9.14 (protocol 25). PvP is separate.

- Keep snapshot scheduling near 20 Hz across rendering frame rates; skip missed slots instead of sending catch-up bursts.
- Bound Steam send retries per update, renew host/guest send budgets, and coalesce adjacent unsent snapshots while preserving event and recovery-message order.
- Validate and recover authoritative vehicle baselines; preserve destroyed-vehicle records and reconcile supported roster changes.
- Reduce repeated damage/visual/receive allocations and harden cleanup and AAR presentation retry paths.
- Add separate managed-send and Steam-native queue diagnostics, including ping, estimated pre-transmission wait and pending/unacknowledged bytes.
- Includes startup validation and compatibility improvements. Use Steam and a valid GHPC license.

Validation: pinned build and automated fixtures pass. Scheduling tests are synthetic, not measured internet latency. No live two-PC verification of this release has been completed. The reported 250–500 ms input delay is NOT confirmed resolved. Guest adaptive presentation buffering and Steam/network transit still contribute latency. Native queue samples every five seconds can miss shorter spikes.

Close GHPC before updating. Existing compatible managers can install the DLL update; every host and guest needs this version. For a fresh installation extract the Setup ZIP and run Install.cmd. To report lag, provide Bin/MelonLoader/Latest.log from both PCs after reproduction and before restarting. Review logs for personal information before sharing.


# 0.9.9.14 — Network smoothing and shared AAR improvements

- Adaptive guest movement buffering handles uneven snapshot arrivals without predicting hits or damage. It can add visual delay when timing varies.
- Reduced duplicate fire/smoke lookups and wheel transform reads; Steam broadcasts encode shared data once for multiple guests.
- AAR now shares the host-selected shot text, penetration story, native fragment/jet traces and blast visualization.
- Removed forced host-camera following: camera control remains local. X-ray is initialized once rather than overwritten on every update.
- Added timing diagnostics for host snapshots and periodic infantry/aircraft discovery.

Validation: build and regression fixtures passed. A local two-window test reported no movement stutter, but broader online testing is still needed. The latest independent-camera change has not yet been retested in-game. Shot selection remains host-controlled; independent guest shot history is not implemented. AAR is sampled every 250 ms and has bounded trace/text limits.

Install the same version on host and guests. This update does not change ownership checks or the launcher. PvP is separate and is not included.

# 0.9.9.13 — Steam launch and connection stability update

- Improved Steam launch handling and corrected the MelonLoader startup path.
- Improved handling of temporary network congestion.
- Improved connection diagnostics.

## Updating
**Close GHPC and install the complete Setup ZIP.** Existing users should reinstall this package to receive the updated launcher; the older automatic updater updates the mod DLL only. Keep Steam running and use a valid GHPC installation. All players should update.

## Validation
401 automated mod checks, 28 updater checks and the launcher installation fixture passed. A deterministic rebuild matched the pinned DLL hash. Automated checks do not establish correctness for every account, mission or network condition. Broader live gameplay testing remains necessary.

This is an unofficial community mod, not affiliated with or endorsed by the GHPC developers.

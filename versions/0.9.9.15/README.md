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

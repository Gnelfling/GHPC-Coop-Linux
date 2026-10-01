> Development: [guest AAR trace recovery source](development/aar-trace-recovery/) is available. Build checks passed; the missing-ray symptom is not yet visually verified. Stable launcher release remains 0.9.9.17.

> [0.9.9.17](https://github.com/dnjsxoq013-debug/GHPC-Coop/releases/tag/v0.9.9.17) is now the latest stable-channel release and is available through the existing launcher. Gameplay verification is incomplete; guest AAR remains limited. See the release notes for known limitations.

# GHPC Unofficial Co-op 0.9.9.16

[Download](https://github.com/dnjsxoq013-debug/GHPC-Coop/releases/tag/v0.9.9.16) | [Source](versions/0.9.9.16/)

# 0.9.9.16 — PvE discovery allocation and identity-cache improvements

- Reuse infantry emplacement and aircraft identity hashes only while their full identity inputs remain unchanged; recompute after changes.
- Reuse aircraft discovery scratch containers and clear cached entries for destroyed/unloaded objects and session teardown.
- Preserve discovery coverage and frequency, including inactive runtime units. Damage, roster handling and event ordering are unchanged.

Includes the 0.9.9.15 network scheduling, resynchronization and Steam queue diagnostics improvements. Wire protocol remains 26 (incompatible with 0.9.9.14/protocol 25). Update host and guests together for consistent diagnostics. PvP is not included.

Validation: pinned build and regression fixtures passed. Actual scan-time savings and remote input latency have NOT been measured in-game for this release. Global searches remain; this does not claim to eliminate the reported scan stalls or 250–500 ms input delay.

Close GHPC and update through the existing compatible manager, or extract the Setup ZIP and run Install.cmd. Use Steam and a valid GHPC license. For lag reports send Bin/MelonLoader/Latest.log from both host and guest after reproduction and before restarting; remove personal information before sharing.

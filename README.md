# GHPC Unofficial Co-op 0.9.9.14

[Download](https://github.com/dnjsxoq013-debug/GHPC-Coop/releases/tag/v0.9.9.14) | [Source](versions/0.9.9.14/)

# 0.9.9.14 — Network smoothing and shared AAR improvements

- Adaptive guest movement buffering handles uneven snapshot arrivals without predicting hits or damage. It can add visual delay when timing varies.
- Reduced duplicate fire/smoke lookups and wheel transform reads; Steam broadcasts encode shared data once for multiple guests.
- AAR now shares the host-selected shot text, penetration story, native fragment/jet traces and blast visualization.
- Removed forced host-camera following: camera control remains local. X-ray is initialized once rather than overwritten on every update.
- Added timing diagnostics for host snapshots and periodic infantry/aircraft discovery.

Validation: build and regression fixtures passed. A local two-window test reported no movement stutter, but broader online testing is still needed. The latest independent-camera change has not yet been retested in-game. Shot selection remains host-controlled; independent guest shot history is not implemented. AAR is sampled every 250 ms and has bounded trace/text limits.

Install the same version on host and guests. This update does not change ownership checks or the launcher. PvP is separate and is not included.

Close GHPC before installing the Setup ZIP. Keep Steam running and use a valid GHPC installation.

This is an unofficial community mod, not affiliated with or endorsed by the GHPC developers.

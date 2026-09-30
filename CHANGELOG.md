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


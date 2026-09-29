# Source maintenance — 0.9.9.13

## Source map
- GameBridge.cs: vehicle discovery, ownership and restoration.
- GameBridge.Input.cs: input sampling, remote driving and firing.
- GameBridge.Replication.cs: snapshots and replica presentation.
- CoopLabMod.cs: lifecycle and main update dispatch.
- CoopLabMod.Input.cs: input timing and late-frame dispatch.
- CoopLabMod.TestSetup.cs: explicitly requested custom mission test configuration.
- SessionPause.cs: session pause/AAR transition state.
- AarView.cs: host-selected AAR capture and presentation.
- TracerSync.cs: projectile/wire capture, buffering and disposal.
- TracerSync.Visuals.cs: visual creation and rendering on guests.

Partial files share the existing state; this is organizational separation, not a claim of complete architectural decoupling. Large update/serialization methods remain candidates for a later, separately tested refactor.

## Invariants
Preserve protocol 25, serialization order, native reflection strings, Harmony binding names, authority guards and callback order. Local identifiers may be clarified; native API names must not be renamed. WirePoints contains world-space XYZ triplets. Some legacy Pose arrays are shared across vehicle/infantry payloads; their layout cannot be changed by a formatting pass.

## Verification

Build and 401 automated checks passed. See TEST-RESULTS.md and CHANGELOG.md for gameplay limits. Build instructions and pinned hashes are in REPRODUCIBLE.md and build-lock.json.



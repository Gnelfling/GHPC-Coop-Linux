# Local regression fixes — 2026-10-01

This is an unpublished development build, not evidence that the reported gameplay
problems have all been resolved. Both test instances need the same rebuilt DLL.
Wire version 28 carries crew penetration counts and platoon leadership; older
builds must not connect to this build.

## Ownership and leader display

`Game/Replication/PlatoonSync.cs` applies the host's leader flag using the native
platoon setter only when the leader changes. Guest selection no longer calls that
setter independently. This preserves the native waypoint subscription changes
and lets the existing selection menu read the shared leader. Being the local
player and being the platoon leader are separate properties.

Verify two distinct claimed tanks in the four-M1 mission, switch guest tanks,
then incapacitate the leader. Both menus must identify the same surviving leader.
The protocol tests cover true/false leadership serialization, not the Unity menu
or native leadership handoff.

## Reload and crew speech

The guest sends its automatic-reload switch alongside ammo selection and reload
preference. The switch is applied even when no queued clip index is available.
An idle empty remote feed retries native reload only when the player's policy
allows it; native rack, damage, missile and reload-duration rules remain active.

The replica restores carousel start and native reload events while the host owns
ammo consumption. Shot voice context is applied before reload notifications: a
late packet may contain both the shot and its completed reload. Reflection
metadata is cached, but current weapon state is always read fresh.

Crew-compartment penetration is a monotonic host event counter. Initial and
resynchronization baselines seed it silently; new local hits invoke the native
voice handler without applying another impact. This does not implement every
possible crew voice event. The existing log shows accepted voice requests, which
does not prove audible playback on the user's output device.

Verify M1 reload speech, T-80B automatic reload for multiple rounds, manual reload
policy, ammo switching, guest penetration speech, and silence for remote crews.

## Tracer visibility

Replicated tracer meshes are checked from the camera actually rendering them.
Wall/terrain occlusion hides their visual; host ballistics and damage are unchanged.
Off-screen meshes and cameras that exclude their layer do not issue visibility
rays. The six frustum planes are reused rather than allocated each frame.

This is a conservative three-point collider check, not per-pixel depth rendering.
A partly obstructed streak may disappear entirely; unusual collision meshes may
also hide a visible streak. Native host tracers are not modified. Verify external,
day and thermal views on the guest, and compare the host. Measure cost during
sustained MG bursts before accepting this as a release fix.

## Frame-time work

Equipment state reuses private buffers; feed and voice reflection metadata is
cached. Detailed aim logging now requires `--coop-aim-diagnostics`, avoiding its
normal-session string construction and disk writes. Scene-query fallback returns
the complete results during the same pass that detects missing objects.

No measured FPS improvement is claimed. Compare the same mission, resolution,
vehicle count and host/guest roles. The scene-query coverage verification still
runs the expensive legacy search periodically; adaptive discovery can delay new
spawns up to three seconds. These remain known performance tradeoffs.

## Validation boundary

Build with `build.ps1 -GameBin <Bin> -CacheDir <cache> -RunTests`.
The automated checks exercise protocol/transport fixtures, reload policy and
replication helpers. They cannot establish audible speech, correct native AI
handoff, optical occlusion or actual frame time. No release should describe those
items as verified until the gameplay checks above pass.

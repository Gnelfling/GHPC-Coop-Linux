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

## Guest AAR trace recovery (2026-10-01)

Reported: host displays penetration/spall rays; guest displays x-ray models and hit text without rays. The existing log does not record segment or mesh counts, so the runtime cause is not yet proven.

Replica traces now use independent scene roots because the wire endpoints are world-space coordinates. Explicitly enable each native ShotTraceVisual and its object. Equal packet geometry no longer means presentation is healthy: destroyed objects are rebuilt and inactive renderers are restored. Renderer discovery runs once after native Start has had a frame to construct meshes, rather than scanning the scene. Camera controls remain local. Text fallback is used only when the corresponding native text box is unavailable or inactive, avoiding duplicate labels.

Diagnostics distinguish received segment count from generated renderer count. Validate with one main-gun hit and spall, then change the host selected shot and rotate the guest camera independently. Check both AAR entry with zero guest shot history and re-entry. A successful build/protocol test does not verify Unity rendering or prove this reported symptom resolved. Running games and published 0.9.9.17 assets have not been changed.

## Mechanical autoloader follow-up

Player-controlled carousel feeds now start the native reload cycle automatically on
the authoritative host, including its own vehicle. This intentionally differs from
the game's optional manual/switch-off player preference for mechanical loaders;
forced-manual feeds remain manual. Human-loaded guns retain the prior policy.
The existing native Reload method still handles ammunition selection, reserve stock,
reload duration and equipment restrictions. Idle recovery respects pause, restock,
missile wait and in-progress cycle guards. Guests do not simulate a second reload.
No vehicle-name string matching or instant ammunition refill is used.

Policy tests cover mechanical default-off/manual preference, forced-manual exemption
and unchanged human-loader behaviour. This is not confirmation of in-game T-80B audio,
carousel motion or repeated-shot playback; verify those on both peers before release.

## 2026-10-02 development follow-up (after stable 0.9.9.18)

- Mechanical autoload recovery also runs for a local solo player without a room. This supports one-vehicle training missions; replica clients still do not simulate a second reload. A local T-80B training test was reported working by the tester. Remote/guest behaviour is not yet verified.
- Mission selection now has search, readable mission/theater/day-night labels and a scrollable native faction briefing. Native installed assets determine briefing language (English originals or Korean-patched assets); English display has been launched for testing but not visually verified.
- Friendly formation entries distinguish platoons from individual vehicles. Missing pre-load data is described as unavailable rather than falsely reporting zero vehicles. Some mission previews and counts remain unavailable before loading.
- Known unresolved report: after reloading, third-person firing may intermittently fail until entering the sight and returning. It later recovered during testing. No fix or confirmed cause is claimed.

Build and automated fixtures passed, including 34 reload-policy combinations. These do not verify live Unity rendering, Steam multiplayer, or the intermittent firing symptom.
This folder includes a development DLL based on 0.9.9.18; it is not the stable release DLL. All test peers must use the same DLL hash. Stable release assets and launcher update metadata are unchanged.

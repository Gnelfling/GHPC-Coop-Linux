# PvE network smoothing test build

Based on PvE 0.9.9.13. This is a local candidate, not a published release. PvP and the published PvE source are unchanged. The build has not been installed in either running game.

## What changed and why

The guest previously rendered its vehicle 75 ms behind the estimated host clock regardless of arrival timing. When a host frame or packet burst took longer, interpolation ran out of samples and held the last position. The next packet then made movement resume abruptly.

`src/ReplicaTimeline.cs` measures host snapshot intervals and excess arrival gaps. It keeps the 75 ms minimum on a stable connection and increases the local presentation buffer up to 250 ms when timing varies. Remote vehicles retain a larger 300–400 ms buffer. Playback adjusts gradually and never runs backwards or extrapolates beyond authoritative positions. A longer buffer smooths motion at the expense of additional visual delay; it cannot repair sustained packet loss, insufficient bandwidth or low host FPS.

Damage, ammunition, firing authority, projectile handling and the wire format are unchanged. There is no client-side hit prediction or packet dropping in this patch.

Every five seconds, `HOST TIMING` reports worst snapshot capture/send time and scheduling gap. Guest `BUFFER` reports its current delay; existing `TIMING` reports rendering FPS, packet arrivals and starvation. These separate host work from guest presentation problems without claiming that either caused the reported incident.

## Host work reductions

- `CombatVisuals.Capture` performs one hierarchy search per vehicle instead of two, sharing that result between fire exits and smoke columns. It still searches every capture: detached and newly created components are not hidden by a persistent cache.
- Wheel capture reads each Unity local rotation once instead of four times. Each outgoing snapshot still owns its arrays; later captures cannot overwrite queued data.
- `MultiCoop.Broadcast` encodes a message once for all Steam guests. `SteamPeer.SendEncoded` treats those bytes as immutable and retains each peer's independent FIFO retry queue and backlog limit. TCP retains its existing worker queue. With one guest this does not reduce encoding count; with three guests it reduces three encodes to one.

The 20 Hz vehicle snapshot cadence, packet contents, firing/damage authority, reliable ordering and retry behavior are unchanged. No events are discarded. The periodic infantry/helicopter scans remain unchanged pending measurements and a safe invalidation design.

The Steam adapter fixture now checks shared payloads under backpressure: every peer receives identical bytes in order, and subsequent changes to the original message do not alter the queued payload. All 29 adapter checks passed, along with the full build test suite. These are correctness checks, not measured FPS or online performance improvements.

## Validation and next playtest

The pinned build and existing protocol, Steam adapter fixture, input, damage-related and mission fixture suites passed. The new timing suite passed 306 assertions. In a synthetic 20 Hz stream delivered in 150 ms bursts, starved frames decreased from 744 to 26. This is not an online latency benchmark.

For the next test, use the same candidate DLL on both PCs, start the same mission, and drive steadily for at least 30 seconds with each player hosting in turn. Compare both players' `MelonLoader/Latest.log` entries for `HOST TIMING`, `BUFFER` and `TIMING`. Check firing and destruction too. Real Steam gameplay has not yet been verified.

Candidate: `dist/GhpcCoopLab.dll`. Do not replace a DLL while its game is running. Keep the previous DLL for rollback. No launcher or entitlement behavior has been changed.

## AAR capture optimization

AAR now collects only the root and mount transforms that its receiver consumes, rather than building a full live snapshot with damage, ammo, fire and audio. Replay updates still run every 250 ms, with the same text and trajectory fields. Reflection metadata and line renderer references are reused; dynamic shot positions are still sampled each update. Protocol tests cover transform-only AAR round trips and reduced payload size.

The subsequent AAR presentation change transmits the host's visible native trace styles (including spall and shaped-charge jets), shot text, penetration story, X-ray setting, blast sphere. The guest follows the host's selected shot; it does not gain independent recorded shot history. A text fallback handles a guest with zero locally recorded shots. Geometry is compared before rebuilding native trace objects. Trace objects are released on session cleanup. Each viewer retains local camera movement and FOV; host camera poses are neither transmitted nor applied. The initial X-ray setting is shared once, then remains locally controllable. Shot selection is still host-controlled; independent guest shot selection is not implemented.

Presentation is sampled every 250 ms, with existing protocol limits of 512 trace segments and bounded text (3900 UTF-8 bytes per text field); very large shots can be truncated. Both test instances should use this candidate. Build and protocol regression checks pass, but actual Unity AAR appearance, camera behavior and session cleanup still require a two-window playtest. This build has not been applied to the currently running test games.

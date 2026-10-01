# PvE architecture and design reasons

This guide explains the local hardening candidate derived from 0.9.9.14. It describes implemented behavior, not a promise that every game mission or network has been verified.

## Authority and player ownership

The host runs native vehicle physics, AI, ammunition and damage. Each guest sends input for its assigned vehicle. The host validates ownership before applying that input. Guests reproduce authoritative state and visual effects. This avoids two independent damage simulations disagreeing about a penetration or kill.

`RoomSeats.cs` manages vehicle occupancy. `MultiCoop.cs` owns host peers and isolates a peer failure. `GameBridge.cs` captures native references and reserves vehicle controls. `GameBridge.Input.cs` applies authorized input. `Protocol.cs` encodes, decodes and validates bounded messages. Never use translated display names as network identities.

## Joining and mission loading

A host enters a mission and creates a room. A guest joins from the menu. `RoomJoin.cs` and mission configuration code load the offered mission before the handshake and vehicle claim. Both machines must agree on compatible game inputs and vehicle identity/layout. A successful lobby join alone does not prove mission synchronization.

## Movement and events

Vehicle snapshots are scheduled at 20 Hz; actual delivery depends on host frames and transport. `ReplicaTimeline.cs` adjusts the guest presentation delay when arrivals vary. It interpolates recorded host poses and does not predict authoritative hits. Smoothing adds visual delay and cannot cure low host FPS or sustained network congestion.

Snapshots include cumulative weapon-shot counters, ammunition and damage state. Separate messages carry impact and tracer effects. Event ordering matters: a vehicle assignment or impact must not be reordered around unrelated snapshots.

`SteamPeer` owns immutable encoded packets. Its send budget is 16 packets between Pump calls, shared by send and receive paths. It coalesces only adjacent unsent full vehicle snapshots. Any other message is an ordering barrier. This deliberately limited rule preserves all events; it does not remove a snapshot already accepted by Steam, or one separated from a newer snapshot by an event. The 8 MiB queue limit still applies.

## Dynamic vehicles

Vehicle recovery now uses a gated host baseline/guest acknowledgment handshake.
Authored dynamic spawn membership is discovered without a full scene scan, existing
native records and player reservations survive recovery, and input is accepted only
for the acknowledged baseline. Initial roster hash differences are reconciled after
mission authentication rather than rejected before the live baseline can arrive.
Technical layout mismatches remain errors. See [vehicle resynchronization](RESYNCHRONIZATION.md)
for the protocol, design reasons, native lifetime boundaries and verification steps.

## AAR presentation

The host shares its selected shot, text, penetration story, native trace styles and blast sphere every 250 ms. The guest camera belongs to that guest. X-ray is initialized once and then locally controlled. Independent guest shot selection/history is not implemented. Trace/text size limits can truncate unusually large shots.

`AarPresentation.cs` builds native visuals only when geometry changes. Its cache is committed after successful creation, so failure can retry. Partial objects are recorded immediately for cleanup. `AarView.cs` catches presentation errors separately from transport failures; a display error should not disconnect an otherwise valid session.

## Cleanup and native lifetime

Unity objects can disappear during scene teardown. `SafeCleanup.cs` lets cleanup continue after one native operation fails. `Stop()` releases peer controls, transports, effects, AAR state and mission overrides. Errors remain logged instead of being silently discarded. Native object destruction and reconnect still need live testing; a passing pure fixture cannot prove every Unity restoration path.

## Steam authorization

`StartupLicense.cs` gates mod updates while native Steam initializes, then verifies logged-in GHPC app context and current-app entitlement. Failure requests game quit. `SteamLink.CheckSteam()` also guards connection operations. Valid shared Steam licenses are accepted. A mod can guard its own installation and launcher path; it cannot enforce licensing on a copy from which the mod has been removed. This candidate has no runtime bypass switch.

## Discovery and performance

Infantry discovery remains live every second so deployed soldiers and seats can be found. Temporary maps are reused rather than caching an obsolete world. Aircraft damage topology is cached per unit, preserving detached native parts; new aircraft are still discovered. Snapshot, discovery and interpolation logs distinguish measured work from suspected causes. Keep performance claims conditional until both host roles are tested online.

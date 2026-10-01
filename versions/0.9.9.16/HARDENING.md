Release 0.9.9.16 includes the discovery identity-cache follow-up. Historical development status below is retained as an audit trail. Native performance verification remains pending.

﻿# PvE hardening candidate

Based on 0.9.9.14; not published or installed.

Implemented:
- Before mod update, startup waits briefly for native Steam initialization and verifies authenticated GHPC app context and current-app entitlement. Failure requests application quit. This guards mod-installed execution; it cannot enforce licensing on copies with this mod removed. Legitimate Steam shared licenses remain valid. No runtime entitlement bypass flag exists in this candidate.
- Session and GameBridge teardown isolate native-object failures so other components are still cleaned.
- AAR geometry is committed only after successful construction, partial objects are owned immediately, and presentation failure retries without disconnecting the session.
- Reliable Steam sends have a shared 16-packet budget between Pump calls, including Send/TryRead. FIFO order and all events remain intact. Congestion can still accumulate old snapshots; adjacent unsent full snapshots now coalesce, with all intervening messages acting as ordering barriers.
- Infantry scan scratch maps are reused while discovery remains live. Aircraft damage topology is cached per unit until destruction/disposal.
- Snapshot preflight checks every vehicle identity and component layout, including damage and scorch counts, before applying any vehicle. Missing vehicles are permitted only after an authoritative destroyed state, including on the first baseline. Wreck records remain as tombstones so delayed frames cannot restart their interpolation or firing effects. Unknown IDs and live-vehicle disappearance now request a gated authoritative baseline. Authored dynamic spawn additions and host removals are reconciled while seat ownership is retained. Exact native layout mismatches and missing native objects after the recovery deadline still fail explicitly.

Validation: pinned build plus fixtures, including cleanup continuation and bounded FIFO recovery. Native startup rejection and Unity AAR retry still require playtests. Ground-vehicle resynchronization is implemented with protocol/state-machine and adapter tests; native two-PC verification remains outstanding. See docs/RESYNCHRONIZATION.md for unsupported native creation cases. License enforcement outside mod execution is not provided.

Local test exception: not enabled. If a later two-instance test needs one, use a separate private build, never a public runtime switch or a release artifact.

Developer documentation: [docs/README.md](docs/README.md).

Wire protocol: 26. Public protocol-25 builds cannot join this local candidate. No installation or publication performed.

## 2026-10-01 send-backlog follow-up

The local candidate's per-Pump budget was not renewed by the Steam room wrapper: accepted host peers and the wrapped guest peer were never pumped. After sixteen accepted sends, their managed queues could stop draining. Both room paths now pump active peers once per room update. This defect was found in the unpublished candidate; it does not establish the cause of the public 0.9.9.14 latency report.

A saturated native send buffer is now probed once per Pump, instead of repeatedly from every Send/TryRead on the same frame. Adjacent pending full snapshots are replaced before flushing, avoiding a redundant obsolete send on recovery. Events and resynchronization barriers remain FIFO; inputs are not coalesced because they can contain one-shot actions. Already accepted Steam messages cannot be withdrawn by this change.

Tests cover eighty room updates for the guest and three host peers, a busy saturated frame, newest-state recovery, send-budget exhaustion, and the existing event/baseline ordering fixtures. No wire-format change, no prediction of hits or damage, and no change to interpolation delay. Build/fixtures must pass before installation. This candidate remains uninstalled and unpublished; real Steam latency and combat performance need two-PC measurement.

## 2026-10-01 additional latency audit

Host snapshot scheduling used `now + 0.05`, which loses cadence when a rendering frame overshoots the deadline. A shared phase-preserving scheduler now advances to the next future slot for host snapshots and the existing guest/direct send loop. It skips missed slots rather than sending catch-up bursts. On synthetic ten-second schedules at 30/45/60/144 FPS, the old host schedule produced 150/150/175/180 sends; the corrected schedule produced 200 at each rate. These are scheduling measurements, not measured internet latency or gameplay FPS.

Steam send diagnostics now aggregate managed queued bytes, oldest pending age, maximum queue wait, accepted bytes/packets, blocked pumps and coalesced snapshots every five seconds per connection. They deliberately do not call these values ping: accepted bytes can still wait inside Steam or on the network. Pair these with HOST TIMING and guest BUFFER/TIMING logs from the same session. Long managed waits indicate local send backpressure; high host snapshot gaps/work indicate host scheduling/capture cost; guest starvation with no managed backlog requires investigating transit/native buffering. No packet or damage events are discarded for diagnosis.

Validation: pinned DLL build and complete fixture suite passed; 39 Steam adapter checks and 1,111 timeline/scheduling assertions. No installation, publication or two-PC latency claim. The adaptive 75–250 ms local presentation buffer and reliable native Steam buffering remain possible contributors that require real-session measurements; this change does not eliminate network round-trip time.

## Steam native queue diagnostics

Every five seconds per connected peer, `STEAM NATIVE` now samples the bundled Steamworks `GetConnectionRealTimeStatus` API alongside `STEAM SEND` managed-queue statistics. Fields: `pingMs` (Steam ping), `queueEstimateMs` (microseconds converted to milliseconds, estimated wait before transmission), `pendingReliableBytes`, `pendingUnreliableBytes`, `unackedReliableBytes` (already transmitted, not acknowledged), and `sendRateBytesPerSec`. The connection uses one default lane. These samples are not interval maxima and can miss shorter spikes. The queue estimate is neither total input latency nor delivery time and excludes Nagle delay. Negative/unavailable native values must not be interpreted as zero latency.

API failures are logged as unavailable; diagnostic exceptions cannot disconnect the player. No routing, send limits, lane ordering or reliability settings are changed. No game was running at inspection time, so no live-session queue measurements were collected. The local diagnostic build must run on host and guest for correlated measurements.

Reference: https://partner.steamgames.com/doc/api/ISteamNetworkingSockets#GetConnectionRealTimeStatus and https://partner.steamgames.com/doc/api/steamnetworkingtypes . Unit tests exercise unit conversion, pending versus unacknowledged bytes, API errors and missing native entry points; native runtime measurement remains unverified.

## Additional allocation audit

Damage capture now fills newly owned health/scorch arrays directly, avoiding LINQ iterator allocations while preserving every component and each queued snapshot's independent storage. The guest's private previous-scorch comparison buffer is reused with Array.Copy, rather than cloned on every applied snapshot. It never aliases incoming protocol arrays. Combat visual cleanup constructs the vehicle prefix once per call rather than once for every visual in the scene. The serial Steam receive path reuses its one-element pointer array and clears the slot after releasing each native packet; received payloads remain independent.

These are allocation reductions, not measured FPS or RTT improvements. State capture frequency, hierarchy discovery, event delivery, damage checks and visual refresh remain unchanged. Avoid caching dynamic fire hierarchies or reusing outgoing snapshot buffers without lifecycle/ownership tests. Full snapshot bandwidth and the guest interpolation buffer remain larger possible contributors; native two-PC timing and profiler evidence are required before weakening synchronization or tuning those limits.

## Follow-up: discovery identity reuse after the September 30 logs

The submitted 0.9.9.14 logs contain costly infantry/aircraft discovery passes. Aircraft damage-topology reuse is already in 0.9.9.15; this follow-up additionally caches the computed hash for each live infantry emplacement and aircraft unit. Every pass still rebuilds and compares the full identity signature before reuse. Reparenting, changed spawn membership, scene/name/allegiance/layout changes represented by that signature therefore invalidate the hash. Removed/unloaded object cache entries and session disposal clear the caches. Aircraft discovery also reuses its temporary result and duplicate-ID containers.

Discovery frequency and Resources.FindObjectsOfTypeAll coverage are unchanged, including inactive/hidden runtime objects. A narrower FindObjectsByType replacement was not adopted because its exclusions could change discovery coverage. Consequently this is a reduction of repeated hashing/allocation work, not elimination of global searches or a claimed reduction of the observed 70 ms scan. Capture, damage and roster ordering remain unchanged. The full pinned build and regression fixtures are required, but native identity lifecycle and scan-time improvements still need an in-game test. This follow-up is not part of the published 0.9.9.15 DLL and has not been installed or uploaded.

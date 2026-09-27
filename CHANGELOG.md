# 0.9.9.9 — Experimental infantry synchronization

## Changes
- Synchronize matching friendly and enemy infantry positions, rotations, seating, visibility, health and death from the host.
- Forward guest Deploy Infantry and Recall Infantry requests to the host for the guest's assigned vehicle.
- Use mission spawn points and original member slots to identify soldiers, and vehicle/seat identities for embarkation and disembarkation.
- Stop independent movement decisions on matched guest infantry and reproduce the host's stance and walking speed.
- Smooth movement between received positions instead of visibly stepping at the network update rate. Death and seating transitions apply immediately.

## Testing and remaining work
Two local Direct IP instances were tested in Gunnery Duel. Host diagnostics tracked 42 infantry and guest diagnostic batches matched successfully. The tester reported that the earlier missing infantry, frozen poses and choppy movement appeared improved after the final candidate. This is a limited visual test, not exhaustive gameplay verification.

Native compilation and 361 automated protocol/transport/fixture checks passed. A separate-directory build produced the same release DLL hash. Live multi-account Steam/WAN testing, enemy infantry damage from guest fire, repeated deploy/recall, late joining and other missions still need retesting. Steam and Direct IP share this synchronization code, but local Direct IP testing does not verify real Steam transport.

Matching infantry must already exist in both mission copies. Host-only infantry spawning, exact ragdoll bones, infantry weapon-fire effects and arbitrary squad orders are not replicated. Deployment facing/side uses the host's settings. Movement smoothing adds roughly one update interval of visual delay; it is not hit rewind. Existing helicopter and effects limitations remain.

## Updating
All players must update together: protocol 20 rejects protocol 19 and older clients. Close GHPC and use Check Updates / Update and Play in the launcher. For a manually modified test DLL, use GHPC-Coop-0.9.9.9-Setup.zip and run Install.cmd once; integrity checks remain enabled. DLL updates do not replace launcher scripts.

If you find a bug, please leave a comment with the mission, vehicle, player count, host/guest role and relevant logs. I will investigate and work on follow-up fixes as soon as possible.


# 0.9.9.8 — Experimental helicopter synchronization

## Changes
- Add a separate host-to-guest helicopter state channel: position, rotation, component health, destruction flags, scorch/fire state and rotor RPM. Matching guest aircraft follow host state instead of independently running flight physics and AI.
- Replicate captured crash positions/effects and host inactivity/removal for matched aircraft. Aircraft remain separate from player vehicle seats.
- Recover ground-vehicle fire/smoke visuals when their Unity objects have disappeared, resetting cached intensity when recreating effects.
- Send guest click input in the same frame's LateUpdate instead of waiting for the next periodic input tick.
- Apply ammunition state on newly received host snapshots instead of repeatedly applying it every rendered frame.

## Testing
Two local Direct IP instances were tested in Bolder Limit. The helicopter candidate had 84 paired diagnostic samples with no position/state payload differences and two guest crash-effect invocations; visual helicopter synchronization was also confirmed during testing. These logs compare transmitted/received state, not independent rendered-transform measurements or pixel-identical particles.

After the subsequent input/ammunition changes, both helicopters matched again and one guest shot's launch direction matched its transmitted barrel direction. The tester subsequently confirmed ammunition switching and helicopter shoot-down on the final candidate. This is a user-observed two-instance test, not exhaustive validation of every weapon, mission or network condition.

Native compilation and 332 protocol/transport/fixture assertions passed. These are not a complete gameplay test or a live multi-account Steam/WAN test.

## Remaining limitations
- Aircraft must already exist locally with matching identity/layout; arbitrary host-only aircraft spawning is not implemented.
- Helicopter weapon/turret firing replication is not included. Latest-position placement may still look uneven with latency.
- Crash effects from before the session cannot be reconstructed exactly. Long-session ground-vehicle effects and every mission/vehicle remain unverified.
- This is not a claim that all aiming, damage, effects or multiplayer issues are resolved. Please report mission, vehicle, ammunition, host/guest role and logs for remaining problems.

## Updating
All players must update together: network protocol 19 rejects older clients. Close GHPC and use Check Updates / Update and Play in the existing launcher. Modified test DLLs or broken older launchers require the Setup ZIP; integrity checks remain enabled. DLL updates do not replace launcher scripts.



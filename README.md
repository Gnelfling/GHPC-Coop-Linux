# GHPC Unofficial Co-op 0.9.9.8

[Download](https://github.com/dnjsxoq013-debug/GHPC-Coop/releases/tag/v0.9.9.8) | [Source](versions/0.9.9.8/) | [Build](BUILD.md) | [Testing](VERIFICATION.md)

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



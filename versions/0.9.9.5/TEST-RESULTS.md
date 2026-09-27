# Verification for 0.9.9.5

Three-player Slow Raid (Red, PT-76B): both guests joined and fired 8 rounds each. Both replayed the same 19 impact IDs at the same logged coordinates. Host-triggered M60A3 TTS destruction reached both. At least 154 damage-state comparison samples per process had zero mismatches.

Four-player Under Pressure (Blue, M60A1 RISE P): all three guests joined and fired 6 rounds each. All replayed 18 impact IDs at matching coordinates. T-55A destruction flags and 6/6/6 shot counters matched the host. At least 148 damage-state samples per process had zero mismatches. A separate four-player run retained both surviving guest connections for over a minute after the third guest was terminated.

These were actual separate Unity games at 1024x576 / 15 FPS using localhost TCP and generated input. Destruction was deliberately invoked on the host, not caused by a player-fired kill shot. Terrain-impact event coordinates were compared in logs. Reported launch-direction differences are relative to input muzzle orientation, not sight/laser accuracy: max 0.0907 degrees in the initial four-player upward shot run, 0.2415 degrees in the ground-shot repeat. The first three-player run had an incapacitated guest and was inconclusive for that guest's firing; the repeat completed with both guest vehicles usable.

The tested gameplay source is unchanged in the release build; only the displayed version/log banner changed from the local test build. Test-only probes require explicit command-line flags.

Automated checks: 185 protocol/transport, 21 fake Steam adapter, 10 firing-sample latch, 27 multiple-client TCP and 22 peer isolation/control-transfer checks passed (265 total). These do not substitute for actual gameplay.

Not verified: remote Steam relay under latency/loss, manual aiming and laser ranging, full missions completed with real remote players, all missions, all vehicle types, infantry deployment, visual wreck appearance, and other-mod compatibility. No independent security audit.

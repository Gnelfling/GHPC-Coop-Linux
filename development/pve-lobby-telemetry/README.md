# PvE lobby, telemetry and rendering development snapshot

This snapshot follows 0.9.9.17 and the AAR trace recovery development work. It is not a stable release and does not change the launcher update channel. Both peers must use this exact build (wire protocol 29).

## Changes

- Pre-mission co-op lobby with mission selection, player readiness and teammate AI controls. Connection choices expose Servers, Steam Friends and Direct IP.
- Readable lobby typography, local mission titles and official promotional background imagery.
- Host speed and transmission gear now travel with vehicle snapshots. Guest-only native HUD getter patches display this telemetry while replica physics remains disabled.
- Ordinary tracer visuals are reused; verbose shot logging is opt-in. Guest impact effects are queued with a bounded per-frame spawn budget, independently of damage and AAR state.
- Same-account Steam joins report the actual restriction instead of a false host-departure error, and do not leave the hosting account's lobby. Same-PC testing uses Direct IP.
- Nameplate settings are visible in the lobby. Visibility uses two sight lines and logs the reason labels are suppressed.

## Verification and known issues

Build and automated protocol/adapter/regression fixtures are run for this snapshot. These are not live Steam or Unity visual tests. Guest nameplate disappearance remains under investigation; do not describe the visibility change as a confirmed fix. Guest machine-gun frame drops have improved according to local user testing but still occur. Independent Steam accounts, lobby flow, HUD telemetry and AAR hit rays require further runtime verification.

The slow-acceleration log inspected during development had host time scale 0.5. No vehicle engine power or transmission ratios were changed.

## Assets and build

This public snapshot omits satellite textures extracted from local game files, and its build manifest excludes those resources. Missing previews use the existing fallback. The resulting DLL therefore differs from the local test DLL; use the same public build on every peer. Refer to TEAMMATE-AI.md for image provenance and implementation notes; its local satellite-preview notes describe the local test build only.

Build with build.ps1 and the supported installed game's Bin directory. See REPRODUCIBLE.md for pinned build inputs. No original game assemblies, game logs or account credentials are included.

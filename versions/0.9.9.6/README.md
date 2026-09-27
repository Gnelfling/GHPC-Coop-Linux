# 0.9.9.6 — Experimental fixes for reported co-op issues

This update adds code fixes for reported issues. **GHPC was not launched for this release, at the user's request. The new gameplay behavior is not runtime-verified.** The 3/4-instance gameplay tests documented for 0.9.9.5 must not be interpreted as tests of this build.

## Changes
- Add a separate infantry health/death replication path. Only matching squad/member/faction/model/damage-layout identities are applied; ambiguous or unmatched identities are skipped and logged. Changes are sent in bounded batches and periodically refreshed. Infantry movement, spawning, disembarkation and ragdoll positions are **not** synchronized by this change; the reported infantry issue needs live retesting.
- Transfer flex mission unit replacements, ammunition selections and infantry army overrides before loading the guest mission. Restore the guest's previous in-memory configuration when leaving. Preserve mission/vehicle roster validation. This does not transfer third-party mission files, arbitrary editor settings or campaign saves. Configuration size and missing local resources may still prevent joining.
- Restore the native local-player reload rules instead of forcing manual reload on human-loaded weapons. Remote vehicle feeds use the guest's reload preference and native forced modes. This deliberately honors the game's automatic/manual setting rather than applying one rule to every tank.
- Send host pause/AAR notices to guests and suppress guest driving/fire/reload requests while paused. Stop transmitting vehicle snapshots during host AAR/pause. Full AAR shot-history replay remains host-only; guest vehicle control pausing while the host simulation is paused is expected.
- Handle a missing platoon in mission-offer diagnostics without dereferencing null.
- Refactor objective display synchronization, preserve failure strikethroughs, cache objective key ordering, reuse ID sets, and make detailed objective diagnostics opt-in with `--coop-objective-diagnostics`. Snapshot buffers remain independently owned by queued sends.

## Updating
**All players must update together.** Network protocol 17 rejects older protocol-16 clients rather than connecting incompatible mission formats.

For an intact official installation, close GHPC and use the existing desktop launcher: **Check Updates / Update and Play**. The manifest and DLL remain compatible with the existing updater format. Old launcher scripts are not replaced by a DLL update.

If you installed a manually modified/test DLL or an old broken launcher, extract **GHPC-Coop-0.9.9.6-Setup.zip** into a new folder and run `Install.cmd` once. Integrity checks remain enabled; the updater will not silently overwrite an unrecognized local DLL.

## Verification and remaining work
- 265 existing protocol/transport, Steam-adapter, firing-input, peer-isolation and multi-peer fixture checks.
- 30 native reload-rule combinations, 8 infantry health-parser cases, 13 mission-configuration fixture checks.
- Updater integrity, backup and rollback fixture tests.
- Native game assembly compilation. Separate-path reproducible build comparison is recorded in REPRODUCIBLE.md.

These are code/build/fixture checks, **not** real gameplay, real Steam relay, or proof that all reports are fixed. Infantry identity matching, custom mission loading, actual auto-reloader behavior, pause/resume input behavior and objective UI all require live host/guest testing. Mod Manager compatibility remains unconfirmed.

Please report version, mission and changed settings, vehicle, player count, host/guest role, and relevant logs (remove personal information). Developed with ChatGPT/Codex assistance; unofficial, unsigned, and not independently security-audited.

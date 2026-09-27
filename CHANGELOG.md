# Changelog

## v0.9.8 — published release

- Direct IP co-op supports up to four players, including the host, limited by eligible vehicles in the selected platoon.
- Synchronization covers movement, aiming, firing, tracers, smoke, objectives, mission results, fire support, vehicle damage and destruction.
- Burning/scorched appearance and engine, reload, and crew audio synchronization were improved.
- Redesigned F8 menu and a launcher that checks GitHub for updates.
- Experimental: not all missions, vehicles, or combinations with other mods have been verified. Steam rooms remain a separate two-player test mode.

## Source transparency publication — 2026-09-27

- Published the complete mod C# source, including networking/co-op code, installer/updater scripts, build instructions, tests, security disclosures, and release SHA-256 values.
- Added an explicit source snapshot ZIP for v0.9.8. The original release tag predates source publication; its automatic source archives may contain only the initial README.
- The published installer, mod DLL, and updater manifest are unchanged. This documentation/source publication does not trigger a gameplay update.
- Build succeeded; 170 protocol/transport tests and 28 updater fixture tests passed. These checks are not an independent security audit.

## Earlier development

Earlier builds were developed and play-tested before this repository's source history was published. A complete, independently verifiable per-version change log is not available here; no reconstructed commit history is claimed. Future published changes should be recorded here and in their release notes.

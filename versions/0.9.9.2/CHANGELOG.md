# 0.9.9.2 — Launcher hotfix

Fixes Check Updates / Update and Play failing with a missing Text property. The status label now has a distinct script-scoped name so the updater callback cannot shadow it.

IMPORTANT: Existing launchers update only the mod DLL, not launcher scripts. To receive this launcher fix, download GHPC-Coop-0.9.9.2-Setup.zip, extract it, close the game and launcher, and run Install.cmd once. Subsequent DLL update checks work normally. The mod DLL and protocol are unchanged from 0.9.9.1; this is a launcher/package update.

The real Check Updates execution path was tested on Windows PowerShell 5.1 against GitHub. Mod gameplay limitations from 0.9.9.1 remain. Source, pinned build instructions and SHA-256 checksums are included. Hashes verify integrity, not safety certification.

# 0.9.9.4 — Launcher compatibility and English error messages

This is a launcher/installer update. The mod DLL and Steam protocol are unchanged from 0.9.9.3.

- Launcher failures display an English explanation instead of exposing localized Windows exception text.
- Original error details are retained in launcher-errors.log in the updater folder (accessible through Backups).
- The updater and installer use .NET SHA-256/SHA-512 directly, avoiding dependence on the Get-FileHash PowerShell command. Integrity checks remain enabled.

## How to install this update
Download GHPC-Coop-0.9.9.4-Setup.zip, extract it, close GHPC and its launcher, and run Install.cmd once. Existing automatic updates replace only the mod DLL, not launcher scripts. Clicking Check Updates in an older launcher does NOT install this launcher fix, even if its installed version number advances.

After installing this setup, use the desktop GHPC Co-op Manager / Check Updates or GHPC Co-op / Update and Play for future compatible DLL updates. No local PC installation is performed by publishing this release.

Source and SHA-256 checksums are included. No new gameplay fixes, signing or security certification are claimed. Existing gameplay limitations remain.

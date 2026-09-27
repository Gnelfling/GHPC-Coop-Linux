# 0.9.9.4 — Launcher compatibility and English error messages

This is a launcher/installer update. The mod DLL and Steam protocol are unchanged from 0.9.9.3.

- Launcher failures display an English explanation instead of exposing localized Windows exception text.
- Original error details are retained in launcher-errors.log in the updater folder (accessible through Backups).
- The updater and installer use .NET SHA-256/SHA-512 directly, avoiding dependence on the Get-FileHash PowerShell command. Integrity checks remain enabled.

## How to install this update
Download GHPC-Coop-0.9.9.4-Setup.zip, extract it, close GHPC and its launcher, and run Install.cmd once. Existing automatic updates replace only the mod DLL, not launcher scripts. Clicking Check Updates in an older launcher does NOT install this launcher fix, even if its installed version number advances.

After installing this setup, use the desktop GHPC Co-op Manager / Check Updates or GHPC Co-op / Update and Play for future compatible DLL updates. No local PC installation is performed by publishing this release.

Source and SHA-256 checksums are included. No new gameplay fixes, signing or security certification are claimed. Existing gameplay limitations remain.

# GHPC Unofficial Co-op

**Current release source: [versions/0.9.9.4/](versions/0.9.9.4/)**

- [Download release 0.9.9.4](https://github.com/dnjsxoq013-debug/GHPC-Coop/releases/tag/v0.9.9.4)
- [Verify release files: SHA256SUMS.txt](SHA256SUMS.txt)
- [Build instructions](BUILD.md) · [Security and trust](SECURITY.md)
- [Historical 0.9.8 code](legacy/0.9.8/) — not shipped in the current release

## Installer execution policy

Install.cmd starts a local, readable Setup.ps1 using `powershell.exe -ExecutionPolicy Bypass`. This applies to that PowerShell process; it does not permanently change the system execution policy or disable antivirus. It is not a safety guarantee: review the scripts before running them. The updater downloads release metadata and a verified DLL, not remote PowerShell scripts.

# 0.9.9.4 — Steam friend portraits and invitation menu

- Steam profile pictures beside friends, with a placeholder while unavailable or loading.
- Persona status, aligned invitation buttons, clipped long names and an olive/dark GHPC-style panel.
- Only visible rows request portraits; a bounded 64-entry memory cache retries pending images and releases textures on eviction/shutdown. No separate login or web API key is required.

## Updating
Users who installed the 0.9.9.2 setup: close GHPC, open the desktop GHPC Co-op Manager and click Check Updates, or launch via GHPC Co-op / Update and Play. This update changes the DLL and can be delivered automatically. Players should update together.
Older broken launchers must install the full Setup ZIP once; DLL updates do not replace launcher scripts. Direct Steam or GHPC.exe launch does not run the updater.

## Verification
Compilation, 185 protocol/transport tests and 21 fake-Steam adapter tests passed. These do not verify live avatar appearance; in-game visual and remote invitation testing remain pending. Existing gameplay limitations remain. Smoke color/range have not been changed.
Full source and pinned build instructions are supplied. SHA256SUMS.txt covers uploaded payloads. Hashes verify integrity, not independent security certification.

Historical 0.9.8 source and installer scripts are archived in [legacy/0.9.8](legacy/0.9.8/). They are not part of the current release. Earlier 0.9.9.x snapshots remain under versions/.

# Security and trust — 0.9.9.8

This unsigned experimental mod has not received an independent security audit. No malware-free guarantee or antivirus certification is claimed. SHA-256 verifies matching bytes, not safety. Current networking and gameplay source is in [versions/0.9.9.8/src](versions/0.9.9.8/src/); pinned inputs and the expected DLL hash are in [build-lock.json](versions/0.9.9.8/build-lock.json).

Steam transport verifies peer identities and lobby membership. Direct IP uses TCP with a matching game build and room code; it does not authenticate Steam identities or encrypt the transport. Direct hosting binds only to loopback by default. LAN mode must be explicitly selected and requires a room code of at least 8 characters. Direct internet hosting has not been validated. Neither the setup nor the mod adds firewall or router/UPnP rules automatically. Previously installed legacy rules are not removed automatically.

The updater uses HTTPS, validates asset URL/version, checks SHA-256 and GitHub asset digests when available, verifies supported game/loader hashes, blocks updates while GHPC runs and backs up the old DLL. It downloads a DLL and metadata, not executable remote scripts. Publisher/account compromise is still a trust risk. DLL updates do not replace launcher scripts.

Install.cmd invokes local readable Setup.ps1 with ExecutionPolicy Bypass for that process only. It does not permanently alter execution policy or disable antivirus. Setup may request administrator access. Review the scripts before running them.

Only original mod materials are MIT licensed. GHPC/game assemblies remain proprietary and must come from a legally installed game. MelonLoader is bundled with its license, notices and archive checksum. This project is not endorsed by GHPC's developers.




# Security and trust: 0.9.9.1

This unsigned experimental mod and readable installer/updater are published for independent inspection. No independent security audit, malware-free guarantee, or antivirus certification is claimed. SHA-256 proves matching bytes, not safety. Mod and networking source: versions/0.9.9.1/src. Build inputs and expected DLL hash: versions/0.9.9.1/build-lock.json.

Steam matchmaking and relay-based networking are used. Steam peer identities and lobby membership are checked before accepting guests. The host authoritatively controls the simulation. Release 0.9.9.1 setup does not add firewall/UPnP rules; old root Configure-Network.ps1 is historical 0.9.8 code and is not shipped or executed by this setup. Previously installed rules are not removed automatically.

The updater trusts this publisher's latest GitHub release, uses HTTPS and exact asset URL/version checks, verifies SHA-256 and GitHub asset digests when present, checks game/loader hashes, blocks updates while GHPC runs, and backs up the previous DLL. It downloads a DLL and data manifest, not executable remote scripts. Publisher or account compromise remains a trust risk. Signing and independent audits are not provided.

Only original mod/source material is MIT licensed. Game assemblies must come from the user's legally installed game. MelonLoader is bundled under its own license with notices and original archive checksum. Original GHPC rights remain with their respective owners.

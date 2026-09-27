# GHPC Unofficial Co-op — public source and release transparency

Experimental, unofficial co-op for Gunner, HEAT, PC! Up to four players via Direct IP, limited by eligible vehicles in the host's platoon. This project is not endorsed by the GHPC developers.

**[Download the installer](https://github.com/dnjsxoq013-debug/GHPC-Coop/releases/tag/v0.9.8)** — choose `GHPC-Coop-0.9.8-Launcher-Setup.zip`.

## Review before running

The mod's C# source, readable installer/updater scripts, build instructions, tests, and SHA-256 values are published here. The code was developed with AI assistance and human gameplay testing. Publication is intended to enable independent review, not to substitute for it. There has been no independent security audit, and no claim of antivirus certification is made.

- [SECURITY.md](SECURITY.md): actual permissions, networking, update trust, limitations, and reporting.
- [BUILD.md](BUILD.md): build from source with your own installed game.
- [SHA256SUMS.txt](SHA256SUMS.txt): hashes of published release assets.
- [SOURCE-MATCH.json](SOURCE-MATCH.json): C# source hashes matched to the installer ZIP.
- [VERIFICATION.md](VERIFICATION.md): performed checks and their limits.
- [THIRD-PARTY.md](THIRD-PARTY.md): loader, game dependencies, and generated artwork.

Source files are kept in this repository's root for easy browsing. `CoopLabMod.cs` is the mod entry point; `CoopMenu.cs` is the F8 interface; `Protocol.cs`, `Transport.cs`, `MultiRoom.cs`, and `MultiCoop.cs` implement the multiplayer connection. Other C# files implement gameplay synchronization. `SelfTest.cs` is a test executable, not a mod component.

## Install and play

1. Close GHPC. Extract the full installer ZIP and run `Install.cmd`.
2. When asked, select `Bin/GHPC.exe` from your game installation. The full setup requests administrator access and configures a game-specific firewall rule and attempts UPnP port forwarding; see SECURITY.md before accepting.
3. Host: enter a mission, wait until the vehicle is controllable, press F8, then create a room.
4. Guest: remain at the main menu, press F8, enter the host's IP and matching port (default TCP 22222), then join.
5. Use the GHPC Co-op desktop shortcut to check for future updates before launching. Steam/direct EXE launch bypasses the update check.

Use 127.0.0.1 for same-PC tests, the host's local IP on a LAN, and the host's public IP across the internet. LAN success does not prove internet reachability. Use Direct IP for 3–4 players; Steam rooms are a separate two-player test mode. All participants need matching game and mod versions. Not all missions or other mods have been verified.

Existing installations connected to the previous repository can use GHPC Co-op Manager's repository field to connect to `https://github.com/dnjsxoq013-debug/GHPC-Coop`. Reinstallation preserves an already configured source, so check this field when migrating.

## License and original game rights

The original mod source and original installer/updater scripts in this repository are released under the [MIT License](LICENSE). This permission does not cover GHPC or third-party dependencies. GHPC's original content belongs to its developers and respective rights holders. No game assemblies or extracted game assets are included in this source repository.

At the GHPC developers' request, hosted distribution and download links may be discontinued or removed. Removal does not revoke rights already granted under applicable open-source licenses.

## Version history

See [CHANGELOG.md](CHANGELOG.md), [releases](https://github.com/dnjsxoq013-debug/GHPC-Coop/releases), and the repository commit history. Precompiled installers are available under Releases; building is optional.

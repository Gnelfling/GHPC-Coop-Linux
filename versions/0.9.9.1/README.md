# GHPC Unofficial Co-op 0.9.9.1

Experimental Steam co-op for up to four players, including the host. Each player controls a separate vehicle. All players must use this version and matching supported game files. This update replaces Direct IP play with Steam rooms and is incompatible with 0.9.8 sessions.

## Install and play
1. Close GHPC, download the Steam-Preview-Setup ZIP, and extract the entire archive.
2. Run Install.cmd. Select the installed game Bin folder containing GHPC.exe if prompted.
3. Sign in to Steam. Each remote player needs their own account and game installation.
4. Launch with the GHPC Co-op shortcut. Host: enter a mission, wait for control, press F8, then CREATE STEAM ROOM.
5. Guests: remain at the main menu, press F8 and join using the host's Steam Room ID or invitation.
6. Capacity is limited by eligible vehicles, up to four. Explicit platoons stay separate. Legacy missions without platoon data use eligible ungrouped vehicles of the same faction. Missions with only one eligible friendly vehicle cannot host co-op.
7. Disconnect before changing missions. Guests return to the main menu before joining the new room.

## Updating
The manager checks the latest published release of dnjsxoq013-debug/GHPC-Coop. Close GHPC before updating. It downloads the manifest and DLL over HTTPS, verifies hashes and game/loader compatibility, and backs up the previous DLL. Existing users whose manager still points to the old repository must change its repository field. Everyone must update together. Setup does not add firewall rules or router mappings, and does not remove old Direct IP rules.

## Changes
- Four-player Steam room structure with independent guest connections and slot release on departure.
- Disambiguate legacy spawn points using their static location and orientation, not moving vehicle positions.
- Support same-faction ungrouped vehicles in legacy missions with missing platoon data.
- Vehicle switching and ownership validation improvements; manual reload handling for human-loaded player weapons.
- Invite UI and diagnostic improvements.

## Verification and limitations
The included VALIDATION.tsv records 121 mission/faction cases and subsequent retests. HOST_ROOM_PASS means host lobby/listener creation only. It does not prove remote guest joining, combat synchronization, or four-PC gameplay. Earlier sandbox launch failures are excluded. Some missions have insufficient eligible vehicles; Steam timeouts and the Eastern Scramble vehicle-selection failure remain documented. M60 sight motion, T-72 smoke and all vehicle/mission combinations are not fully verified. No universal compatibility claim is made.

185 protocol/transport/seat checks and 21 fake-Steam adapter checks passed. These are offline tests. The same DLL SHA-256 was produced in separate build stages. Cross-machine reproducibility still depends on the exact pinned references; see REPRODUCIBLE.md.

## Source and integrity
The Source ZIP contains all original mod/network code, assets, tests, and installer/updater scripts. Game binaries and third-party loader binaries are excluded from the source archive. Build from your legally installed matching game with build.ps1; see REPRODUCIBLE.md. SHA256SUMS.txt in release assets verifies the uploaded files. Internal package SHA256SUMS.txt verifies extracted files. Hashes prove matching bytes, not that software is safe. The DLL is unsigned; no independent security audit or antivirus certification is claimed.

## Rights
Original mod code is MIT licensed. All rights to GHPC and its original content belong to its developers and respective rights holders. Third-party components retain their own licenses. This unofficial mod is not endorsed by the GHPC developers. At their request, distribution may be discontinued and distributed files/download links removed.

# Security and trust disclosure — v0.9.8

## What is and is not established

This is experimental, unsigned software. Source availability and matching SHA-256 values do **not** prove that a program is harmless. Hashes detect differences from the published files; they do not identify malicious behavior. An attacker who controls the release account could replace both a file and its hash. No independent audit, code-signing certificate, VirusTotal verdict, or clean antivirus scan is asserted here.

Do not disable antivirus or add blanket exclusions to install this mod. If a security product flags a file, record its filename, SHA-256, product name, and exact detection before deciding what to do. A detection should be investigated rather than automatically called a false positive.

## Installer behavior

- `Install.cmd` launches Windows PowerShell with `-NoProfile -STA -ExecutionPolicy Bypass`. The Bypass argument applies to that process; it is not a persistent machine-wide policy change.
- `Setup.ps1` reads Steam installation registry values and library manifests to locate GHPC, or opens a file picker. It checks package/game hashes and requests UAC elevation for the full installation/network setup.
- `Install.ps1` targets GHPC 20260814.1. It backs up and replaces `MelonLoader`, `version.dll`, `dobby.dll`, `NOTICE.txt`, and `Mods/GhpcCoopLab.dll` beneath the selected game folder. Existing backups are retained in `CoopBackup-*`. The bundled loader archive is checked against its SHA-512. Original game assemblies are read for compatibility checks, not patched on disk by this installer.
- `Install-Updater.ps1` installs readable scripts and JSON state under `Bin/UserData/GhpcCoop/Updater`. `Create-Launcher.ps1` creates two desktop shortcuts; older shortcuts are backed up.
- `Configure-Network.ps1` creates/enables a Windows inbound firewall allow rule for the selected `GHPC.exe`, TCP 22222 by default, on **all firewall profiles**. It attempts an enabled UPnP TCP mapping on the local router. These changes can persist after the game closes. It does not turn the firewall off. It does not test reachability from outside the network.
- Normal installation writes local setup logs and may include network addresses in those logs. Do not publish logs without reviewing them.

The full setup performs network configuration even for users intending to join rather than host. Advanced users can inspect and invoke `Install.ps1`, `Install-Updater.ps1`, and `Create-Launcher.ps1` individually instead; these do not call `Configure-Network.ps1`. Installation write permissions still apply. This is a disclosure of current behavior, not a claim that elevated privileges are needed for gameplay.

## Runtime networking and data

- Direct IP uses TCP sockets to the host address entered by the player. Hosting listens on all interfaces at the chosen port while a room is open. `Transport.cs` and `MultiRoom.cs` implement this.
- Direct IP traffic is **not TLS-encrypted**, and its empty room-code mode is **not strong authentication**. Build/mission/vehicle checks are compatibility checks, not proof of peer identity. Anyone who can reach the port may attempt to connect. Do not treat a room as a secured remote-access service or expose it on an untrusted network without understanding this limitation.
- Gameplay packets include input, vehicle state, mission identity, objectives, audio/effect events, and compatibility information. Steam test mode additionally uses Steam matchmaking/networking and peer/lobby identifiers. Steam may contact its relay infrastructure through the game's Steam integration.
- The published first-party source has no analytics service or credential-upload feature. The launcher does not ask for a GitHub password or access token; it uses public releases. This statement concerns the reviewed source, not a guarantee about all dependencies or all future releases.
- MelonLoader loads code into the game process and Harmony modifies game behavior at runtime. The mod executes with the game's privileges. Local diagnostics/configuration are written beneath the game directory and normal loader/game log locations. Treat logs as potentially sensitive.

## Updater trust and behavior

`Update-Core.ps1` queries `https://api.github.com/repos/dnjsxoq013-debug/GHPC-Coop/releases/latest`, then the configured repository's GitHub release assets. HTTPS redirects may use GitHub's asset delivery hosts. GitHub receives normal request metadata, including the client's IP and updater user agent.

The updater downloads a JSON manifest and `GhpcCoopLab.dll`. It checks versions, game/loader hashes, file size, and SHA-256, rejects incompatible releases, and backs up/replaces the mod DLL while GHPC is closed. It does not fetch and execute remote PowerShell scripts. The downloaded DLL **is executable code** when the game loads it: you must trust the configured publisher. This version has no detached publisher-signature verification or independent signing-key pinning. Script/loader updates require a full setup package.

## Removal and recovery

Close GHPC before changing installed files. Removing `Mods/GhpcCoopLab.dll` disables this mod. Preserve any other mods. Remove the two co-op desktop shortcuts and the co-op updater directory if no longer wanted. Restoring/removing MelonLoader needs care if other mods also use it; use the loader's own instructions or the saved pre-installation backup.

In Windows Firewall, review the rule named `GHPC-Coop-<path hash>-TCP-<port>` and remove only the matching co-op rule if no longer needed. Review the router's UPnP/port-forward list and remove the corresponding GHPC Co-op mapping. Do not delete unrelated rules, mappings, or game saves.

For bugs or security concerns, use this repository's Issues with a minimal, redacted report. Never post passwords, tokens, unredacted personal logs, or executable exploit payloads. No confidential reporting channel is currently configured.

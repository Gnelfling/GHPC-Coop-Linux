# Building v0.9.8

Prerequisites: Windows, Windows PowerShell 5.1, the .NET Framework 4.x C# compiler, your own GHPC 20260814.1 installation, and MelonLoader 0.6.1 x64 installed in that game's Bin directory. Game assemblies are proprietary and intentionally not distributed here. Third-party references are consumed from your installation, including Harmony, Unity, Steamworks, FMOD, and MelonLoader.

From this source folder, run:

```powershell
powershell.exe -NoProfile -File .\build.ps1 -GameBin 'D:\YourSteamLibrary\steamapps\common\Gunner HEAT PC\Bin'
```

The script compiles `dist/GhpcCoopLab.dll`, embeds `menu-hero.png`, compiles `SelfTest.cs`, and runs the protocol/transport tests. It does not install the result or alter a running game. Inspect all scripts before execution. Your local script execution policy may require an appropriate user-approved execution method.

Run the updater fixture tests independently:

```powershell
powershell.exe -NoProfile -File .\UpdaterTest.ps1
```

These use temporary mock game/release files, not your active game. Test fixtures are retained in the temporary directory. The source-checkout path adjustment in that test only changes the path to Update-Core.ps1.

The 26 mod C# files and hero image correspond to the installer contents. The portable build script adapts local dependency paths and keeps the compiler options, file order, and embedded resource name. The legacy compiler can produce different PE timestamps/module identifiers; a fresh build is **not claimed to be byte-for-byte reproducible**. SOURCE-MATCH.json permits comparison of the distributed C# source, but is not by itself proof that the executable was built from it. Build and review independently if that is your trust requirement.

## Hash verification

Download SHA256SUMS.txt from the same release and compute the downloaded file hash:

```powershell
Get-FileHash -Algorithm SHA256 .\GHPC-Coop-0.9.8-Launcher-Setup.zip
Get-FileHash -Algorithm SHA256 .\GhpcCoopLab.dll
```

Compare the complete 64-character values. Do not run a file if it differs. A matching hash establishes consistency with this publisher's file, not safety or authorship.

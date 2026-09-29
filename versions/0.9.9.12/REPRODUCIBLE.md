# Reproducible DLL build — 0.9.9.12

Use Windows with .NET Framework 4.7.2 or later, your legally installed GHPC 20260814.1 and MelonLoader 0.6.1 reference files. Every required reference hash is pinned in `build-lock.json`; proprietary game DLLs are not distributed.

From this source directory:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -GameBin "D:\SteamLibrary\steamapps\common\Gunner HEAT PC\Bin" -RunTests
Get-FileHash .\dist\GhpcCoopLab.dll -Algorithm SHA256
```

Expected DLL SHA-256:

`95414231d9fc7a1829ab1a62ead0d8e003286b59c3d2dbabcb19cf1800110bc1`

The build downloads hash-pinned Roslyn 4.8.0 and .NET Framework 4.7.2 reference packages from NuGet, verifies the game/loader references and embedded artwork, canonicalizes source encoding/newlines and uses deterministic compilation with normalized paths. No signing, obfuscation or post-build DLL edits are applied. A mismatched output stops the build.

The release is verified against its pinned DLL hash when rebuilt. This is a local reproducibility check, not independent reproduction on another person's computer. ZIP timestamps are not deterministic; compare the DLL hash rather than expecting newly created ZIPs to match.

Matching hashes establish matching bytes, not safety. Dependency trust, code review and independent security auditing remain separate matters.






# Reproducible build — 0.9.9.1 Steam preview

## What is verified
The preview DLL was built twice with the same pinned inputs in different source directories, including a UTF-8/LF checkout instead of UTF-8/BOM and CRLF. Both output files had exactly this SHA-256:

`f46322b87f2c5bf63e1b225fd18bc7d0239fee8f0a19fbb6d210fa395beaa2ca`

The installer contains that exact DLL. No edits, signing, obfuscation or other post-processing are applied after compilation. This is a local two-directory reproducibility check, not yet an independent person's reproduction on another PC.

## Build it yourself
1. Use Windows with .NET Framework 4.7.2 or later and Windows PowerShell 5.1. No paid IDE is needed.
2. Obtain your own GHPC 20260814.1 installation and MelonLoader 0.6.1 reference files. They must match every reference hash in build-lock.json. Proprietary game assemblies are not included in the source archive.
3. Extract GHPC-Coop-0.9.9.1-Source.zip. Keep its src, assets, build-lock.json and build scripts together.
4. Run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -GameBin "D:\SteamLibrary\steamapps\common\Gunner HEAT PC\Bin"
Get-FileHash .\dist\GhpcCoopLab.dll -Algorithm SHA256
```

The build downloads only two pinned packages from the official NuGet service: Microsoft.Net.Compilers.Toolset 4.8.0 and Microsoft.NETFramework.ReferenceAssemblies.net472 1.0.3. URLs and SHA-256 values are locked in build-lock.json. Cached archives are verified and freshly extracted for each build. Compiler/reference versions do not float to latest. A mismatched game/loader reference or output hash stops the build.

Build options include /deterministic+, /noconfig, /nostdlib+, /debug-, a fixed language version, fixed source/reference ordering, canonical UTF-8/LF sources and a normalized build path. The embedded menu asset is also hash-locked. The output file name is fixed. Build stages are retained under .build-cache for inspection; you may remove them yourself after verification.

## Limits
This does not reproduce the original v0.9.8 DLL, which used a different non-deterministic compiler setup. That existing release is not silently replaced. ZIP archive hashes are not claimed reproducible: packaging timestamps can differ. Compare the DLL hash, not a newly made ZIP's hash.

A matching hash proves your build matches the distributed bytes under these inputs. It does not prove the source or dependencies are free from vulnerabilities or malicious behavior. Compiler, dependency, game compatibility and gameplay review remain separate responsibilities.

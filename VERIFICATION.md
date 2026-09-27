# Verification record — v0.9.8

Public-source build: succeeded with the Windows .NET Framework C# compiler against the installed GHPC 20260814.1 and MelonLoader references. One existing unused-field warning remains.
Protocol/transport tests: 170 passed, including real TCP loopback checks. This is not a security audit or proof that all missions work.
Updater fixture tests: 28 passed, including hash rejection, incompatible builds, rollback and pending-state recovery. Networking is mocked in this test.
All 26 mod C# files were byte-compared to the src folder inside the local published installer package before publication.
Authenticode: the mod DLL is not signed. No antivirus-clean result or independent audit is claimed.
The existing installer ZIP, DLL and update manifest are not replaced by this transparency publication.
No game DLLs, personal gameplay logs, credentials, or decompiled game dumps are included in this source tree.

The original v0.9.8 tag predates this source publication. Use main or the explicitly attached GHPC-Coop-0.9.8-Source.zip for the full review snapshot; the automatically generated tag archives may contain only the original README.

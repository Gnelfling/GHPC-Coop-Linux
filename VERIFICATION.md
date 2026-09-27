# Verification, 2026-09-27

- 185 protocol/transport/seat checks passed.
- 21 fake-Steam adapter checks passed; these are not live remote-account tests.
- 28 updater fixture checks passed, including corrupted input handling and recovery.
- Launcher installation/state/source preservation checks passed.
- Independent build stages, including the packaged source/build scripts, produced DLL SHA-256 ad291b9d4a482b26d84f5a4b9b85858a8dbf058a4c93b4d02da06b294b0ff31b.
- 121 mission/faction cases were attempted, followed by targeted retests. Latest per-case records: versions/0.9.9.1/VALIDATION.tsv. HOST_ROOM_PASS only verifies host room creation. TIME_LIMIT means the latest retest was interrupted at the fixed deadline; earlier attempts may have completed.
- Cross-PC source reproducibility, guest combat/aim synchronization, four-PC sessions, M60 sight behavior and T-72 smoke remain incompletely verified.

No statement here guarantees safety or universal gameplay compatibility.

## Published-release verification
- Downloaded all four payload assets from the public v0.9.9.1 release and verified each against SHA256SUMS.txt.
- A real HTTPS updater run upgraded an isolated 0.9.8 installation fixture to 0.9.9.1 using GitHub latest-release metadata. The original 0.9.8 DLL was backed up with SHA-256 d2b6d820113b763d270828d50e8f6fc77a72f7595ec817b6a8c2a69316145a97.
- A second updater run returned Current without replacing the DLL.
- Installed the verified release and launcher on the local GHPC installation; its online update check returned Current, version 0.9.9.1.
- Cosmetic installer limitation: its completion message still says 0.9.8; the DLL, update manifest and installed state are 0.9.9.1. Published payload bytes have been retained to preserve the recorded hashes.

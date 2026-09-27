# Verification, 2026-09-27

- 185 protocol/transport/seat checks passed.
- 21 fake-Steam adapter checks passed; these are not live remote-account tests.
- 28 updater fixture checks passed, including corrupted input handling and recovery.
- Launcher installation/state/source preservation checks passed.
- Independent build stages, including the packaged source/build scripts, produced DLL SHA-256 ad291b9d4a482b26d84f5a4b9b85858a8dbf058a4c93b4d02da06b294b0ff31b.
- 121 mission/faction cases were attempted, followed by targeted retests. Latest per-case records: versions/0.9.9.1/VALIDATION.tsv. HOST_ROOM_PASS only verifies host room creation. TIME_LIMIT means the latest retest was interrupted at the fixed deadline; earlier attempts may have completed.
- Cross-PC source reproducibility, guest combat/aim synchronization, four-PC sessions, M60 sight behavior and T-72 smoke remain incompletely verified.

No statement here guarantees safety or universal gameplay compatibility.

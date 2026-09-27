# 0.9.9.7 verification

2026-09-28, Inconceivable Intermission (PA_inconceivable_intermission), Blue faction, local Direct IP, isolated copies at 15 FPS.

- Four-instance run: cross-platoon joining and guest M60A3 TTS automated firing completed. 12 host shot audits reported actualLaunchAngleDeg=0.0000 relative to transmitted barrel direction. All three replicas received HEAT/APFSDS/HEAT ballistic changes with an empty breech snapshot. This used the same gameplay source before the final version-label change (DLL 47b47c6b37928746777b4dac67e20063283fd74afae668a13f2ceb88e81c3314).
- Three-instance final release run: DLL 29a8ad6071cb0e9334b5354981b03d7a0af65a1427635a51a86c9c26ec216419. Automated sequence completed; 12 host shot audits reported 0.0000 degrees. Both guests received HEAT at 04:38:35, APFSDS at 04:39:00 and HEAT at 04:39:25 while breech snapshots were empty. Test completed 04:39:36 KST.
- The pre-fix test reproduced a host/guest ballistic ammunition mismatch after switching ammunition during repeated firing.
- 327 fixture checks passed. 28 updater checks passed. Launcher/installer guards, backups and rollback fixtures passed.
- Separate-directory rebuild matched the release DLL SHA-256.

The firing harness uses the native fire-control system with a fixed approximately 600 m aim point. This verifies ammunition propagation and launch-vector consistency, not complete human sight/laser operation or every impact location. No live multi-account Steam relay or WAN test was performed. Infantry, all mission configurations, AAR, every vehicle/range and other mods are not comprehensively retested. Remaining reports are welcome.

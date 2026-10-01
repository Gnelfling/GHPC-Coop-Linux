# AAR trace recovery development snapshot

This source snapshot follows the published 0.9.9.17 release. It is not a new stable release and does not change the launcher update channel. The assembly still identifies itself as 0.9.9.17-dev; use matching builds on all peers.

## Changes and rationale

- Replicated native AAR trace objects use independent scene roots for their world-space endpoints, avoiding dependence on local controller hierarchy visibility and scale.
- Explicitly activate trace objects and components. Unchanged network geometry no longer bypasses recovery of destroyed or disabled renderers.
- Inspect native meshes after Unity Start has had time to run; cache renderer references instead of scanning the scene every frame.
- Log received segment and generated mesh counts to distinguish missing data from missing rendering.
- Only draw fallback AAR labels when the corresponding native text box is unavailable or inactive, preventing duplicate hit descriptions.

## Verification and limits

Build and automated regression fixtures passed. Two local game instances loaded this DLL and joined the M1 Abrams mission. Guest hit/spall ray rendering has NOT yet been visually confirmed; the root cause of the reported missing rays remains unproven. This change must not be described as a verified AAR fix. Camera control remains local; the selected shot still follows the host. Full independent AAR history is not implemented.

Built DLL SHA-256: F6DC7D31090D94FBE6B5B5A7AE5B39807B1F6BF62642F98C5D4DAADF026F94A6

See build.ps1, build-lock.json and REPRODUCIBLE.md for build requirements. Do not replace game files while GHPC is running.

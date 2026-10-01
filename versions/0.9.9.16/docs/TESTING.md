# PvE testing and release checks

## Automated build

Run `build.ps1 -GameBin <your GHPC Bin directory> -CacheDir <tool cache> -RunTests`. The build pins native references and compiler inputs. Fixtures cover protocol validation, owned firing, independent peers, infantry replication, interpolation, Steam queue ordering, cleanup continuation and adjacent snapshot coalescing.

## Two computer checks

Use the same candidate on both PCs. Test both host roles in the same mission. Drive, aim, fire and destroy vehicles; compare damage, ammunition and impact positions. Check guest disconnect, host exit, reconnect and mission change. Then compare AAR traces and story while moving the two cameras independently. Exercise an AAR display failure only in a controlled test; verify it does not break the connection.

Test Steam authorization with a legitimate entitled account and a separate unentitled account. Confirm the latter exits before co-op activity. Mocked entitlement tests do not prove native initialization timing. No entitlement test exception is currently enabled. Any future private test build must have a distinct output and be excluded from release packaging.

## Logs

Compare `HOST TIMING`, `SCAN TIMING`, `BUFFER` and `TIMING` over at least 30 seconds of driving. A snapshot capture time does not measure the entire frame. Interpolation starvation of zero does not prove zero rendering hitches. Review personal data before sharing logs.

## Release boundaries

This candidate is local, not a new published release. Before packaging, verify ownership enforcement, production build flags, native gameplay and reconnect. Update the release version, manifest size/hash and reproducible build hash together. Retain a rollback copy. State unverified dynamic spawn recovery and independent AAR history honestly in release notes.

## Baseline validation regression

`SnapshotValidationTests` exercises first-baseline omissions, duplicate identities,
a late layout mismatch, live-vehicle disappearance, and preservation of destroyed
vehicle tombstones. Preflight must not mutate the previous-state dictionary.
The pinned build compiles the real Unity integration and damage-layout checks.
These tests do not simulate native damage callbacks: a native exception during
application is still handled by session teardown, not a rollback transaction.

Live verification remains necessary: destroy a vehicle, allow the host object to
be removed, and check that the guest wreck does not replay firing or movement.
Authored dynamic vehicle IDs use the baseline handshake described in RESYNCHRONIZATION.md.
A native vehicle that never appears on the guest must time out without substituting another vehicle.

`ResyncTests` and Steam adapter recovery barriers run in the standard `-RunTests` suite.
Use matching protocol-26 candidate builds for native validation.

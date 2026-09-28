# Reading and maintaining this code

This is an unpublished development copy based on 0.9.9.9.

## Changes in the first semantic readability pass

- AmmoSync: descriptive feed, weapon, snapshot and previous-state names; explicit reflection helper names; ammo catalog loading separated from snapshot application.
- InfantryMotion: receive time, blend duration and position deltas named explicitly; interpolation thresholds have named constants with units documented.
- ShotInputLatch: queued shots, held-fire state and previous weapon role are explicit.
- PeerIsolation: callback purpose and request-counter state are explicit.
- InfantryHealth: expected component count and encoded/decoded health values are explicit.

Native reflection strings, Harmony-injected parameter names, message fields and protocol version are unchanged. This pass does not imply the rest of the project has completed architectural refactoring.

## Review invariants

- Host simulation is authoritative. Do not re-enable independent replica AI or damage while tidying code.
- Preserve ordering of snapshot writes and native callbacks: ammo notifications affect sights and audio as well as presentation.
- Preserve ownership, sequence and layout validation when extracting networking helpers.
- Never rename native reflection strings or Harmony binding parameters as if they were ordinary local identifiers.
- Do not tune interpolation, timeout or queue constants as part of a readability change.
- Run the existing tests and review Unity-dependent paths separately; protocol fixtures do not prove live multiplayer behavior.

## Validation

`../refactor-tests.txt` contains the latest compile and 361-check fixture run.
The earlier installer/updater test results still cover unchanged scripts.
The development DLL lives in `dist`; it is not installed or published.

The original formatting-only source of the five modules is preserved in `../before-refactor-src` for comparison.

## Second readability pass

- SupportSync: descriptive method-local names for requests, results, batteries, munition prefabs and effects; faction ownership validation extracted into OwnsBattery. The handlingRequest guard is documented because it prevents duplicate result messages from native callbacks.
- WeatherSync: snapshot, weather, sky, ambient probe, cloud vectors and original restoration values are named explicitly. Packed weather field ordering and native reflection strings are preserved.
- The cumulative refactor covers seven modules. Build and all 361 existing automated checks pass; Unity fire-support/weather gameplay was not retested by these fixtures.

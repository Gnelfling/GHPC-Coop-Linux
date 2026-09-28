# GHPC Unofficial Co-op 0.9.9.10

Download the Setup ZIP, extract it, and run Install.cmd. All players need this version and matching supported game files. Up to four players including the host, one per eligible friendly vehicle.

# 0.9.9.10 — Guest synchronization fixes

## Fixes and improvements
- Corrected infantry and vehicle health conversion between the game's 0–100 scale and the network's 0–1 scale. This addresses incorrect injury/blood visuals and incapacitation on guests.
- Prevented guest-side vehicle collisions from independently killing or throwing replicated infantry into the air. Infantry movement and state remain host-authoritative.
- Added host-authorized platoon Deploy/Recall handling and detached turret/part position replication.
- Added guest TOW missile model and flight-effect replication. The tester confirmed that the missile launches and its model is now visible on the guest.
- Added replication of the host's guidance-wire path and thickness.
- Forwarded guest Bail Out commands to the host and blocked remote driving/firing after the vehicle becomes abandoned.
- Added a host-controlled AAR view that shares displayed vehicle positions and trajectory segments. This is not a complete, independently browsable guest replay history.
- Reformatted and reorganized the source for easier reading and maintenance.

## Known TOW visual issue
Due to a current technical limitation, the guidance wire for TOW missiles fired by a guest is rendered as a **black line instead of the original white line**. I plan to correct this in a future update.

## Verification and remaining work
- Native compilation and 396 automated protocol/transport/fixture checks passed. These checks do not establish full in-game correctness.
- Local testing confirmed improved infantry alignment and visible guest TOW missile models. The latest wire display, AAR view, Bail Out behavior, repeated platoon deployment/recall and detached-part alignment still need further gameplay verification.
- Two customized missions passed local joining and full roster validation. The general customizer-related kick report was not reproduced; it is not being declared universally fixed.
- Complete historical infantry, crew/damage and detached-part AAR replay is not included. Live multi-account Steam/WAN and all mission/vehicle combinations remain unverified.

## Updating
All players must update together. Protocol 25 is incompatible with earlier builds. Close the game before installing. Use the Setup ZIP for the updated launcher/scripts, or the existing updater for the DLL update.


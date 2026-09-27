# GHPC Unofficial Co-op 0.9.9.9

Install: extract Setup ZIP, close GHPC, run Install.cmd. Up to four players across eligible friendly platoons; one player per vehicle.

# 0.9.9.9 — Experimental infantry synchronization

## Changes
- Synchronize matching friendly and enemy infantry positions, rotations, seating, visibility, health and death from the host.
- Forward guest Deploy Infantry and Recall Infantry requests to the host for the guest's assigned vehicle.
- Use mission spawn points and original member slots to identify soldiers, and vehicle/seat identities for embarkation and disembarkation.
- Stop independent movement decisions on matched guest infantry and reproduce the host's stance and walking speed.
- Smooth movement between received positions instead of visibly stepping at the network update rate. Death and seating transitions apply immediately.

## Testing and remaining work
Two local Direct IP instances were tested in Gunnery Duel. Host diagnostics tracked 42 infantry and guest diagnostic batches matched successfully. The tester reported that the earlier missing infantry, frozen poses and choppy movement appeared improved after the final candidate. This is a limited visual test, not exhaustive gameplay verification.

Native compilation and 361 automated protocol/transport/fixture checks passed. A separate-directory build produced the same release DLL hash. Live multi-account Steam/WAN testing, enemy infantry damage from guest fire, repeated deploy/recall, late joining and other missions still need retesting. Steam and Direct IP share this synchronization code, but local Direct IP testing does not verify real Steam transport.

Matching infantry must already exist in both mission copies. Host-only infantry spawning, exact ragdoll bones, infantry weapon-fire effects and arbitrary squad orders are not replicated. Deployment facing/side uses the host's settings. Movement smoothing adds roughly one update interval of visual delay; it is not hit rewind. Existing helicopter and effects limitations remain.

## Updating
All players must update together: protocol 20 rejects protocol 19 and older clients. Close GHPC and use Check Updates / Update and Play in the launcher. For a manually modified test DLL, use GHPC-Coop-0.9.9.9-Setup.zip and run Install.cmd once; integrity checks remain enabled. DLL updates do not replace launcher scripts.

If you find a bug, please leave a comment with the mission, vehicle, player count, host/guest role and relevant logs. I will investigate and work on follow-up fixes as soon as possible.


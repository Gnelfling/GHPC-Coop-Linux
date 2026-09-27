# Two-player Steam preview validation

Use the same 0.9.9.1 preview on both PCs, separate Steam accounts, and matching GHPC builds. The host must have at least two eligible vehicles in one platoon.

1. Host loads a Blue mission and creates a Steam room. Guest joins from the main menu by invite. Confirm a different platoon vehicle is assigned.
2. Drive and turn the turret on both PCs. Fire the main gun and machine gun at a fixed landmark, first stationary and then moving. Compare impact positions; report weapon/vehicle and whether the host or guest view differs.
3. Guest disconnects. Host must remain in the mission. Create/rejoin as appropriate and verify released vehicle slots are usable. A reconnecting guest must first return to the main menu.
4. Disconnect, then host loads a Red mission and creates a fresh room. Repeat joining and firing. Test a different theater/vehicle family next.
5. Three-guest adapter fixture tests check slots 2/3/4, isolated message routing, full capacity, duplicate identity rejection and one peer leaving without closing other connections. This is not a live four-player result.

Logs: MelonLoader/Latest.log and UserData/GhpcCoop/last-error.txt when an error occurs. Relevant entries: STEAM connected relayed=True, ROOM capacity, ROOM claimed peer, ROOM departed peer, ROOM LOAD faction, SHOT alignment. Logs may contain Steam identifiers; redact before public sharing.

Current evidence: original 0.9.8 Steam invite was reported successful by the user. New 0.9.9.1 gameplay remains pending. Do not label all missions or four-player internet play verified until tested.

## Current regression retest
- Hillside Havoc and Diesel Fury: host creation, then matching guest roster on both clients.
- Invite Friend: built-in friend picker and invitation acceptance, including when the overlay does not appear.
- TAB: waiting host and connected host/guest may transfer to a free friendly vehicle in another platoon. Room capacity stays fixed; occupied/enemy vehicles remain blocked.
- M60 sight jitter and T-72 smoke: unresolved. AIM diagnostic and GRENADE detonation log entries added for the next run. No live fix claimed.

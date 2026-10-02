# PvE teammate AI setting and official background

The existing PvE co-op menu now provides TEAMMATE AI ON/OFF. Default is ON. Only the host can change it in a connected room; guests receive the current setting with the host session heartbeat, including after joining. The preference can be selected before creating a room. The four-player lobby supports creating a room before loading a mission, selecting a mission, readying all players, and host start. The server browser remains the existing implementation.

OFF suppresses UnitAI decisions and suspends driver/gunner/commander brains on unoccupied friendly ground vehicles in the replication roster. Vehicles are not removed and remain damageable and available for transfer. Player-owned vehicles, enemy AI, infantry and aircraft are excluded. Loader components are not disabled. Original brain states are restored when enabling AI, transferring a vehicle to a player, or leaving the session. No global scene searches are added. Mission scripts and existing momentum are not frozen; disabled teammates can still be destroyed and influence mission objectives.

The host is authoritative. Guests already suppress their local combat AI; the toggle is display-only there. The setting uses a distinct host Ping text value, preserving the wire layout. All peers should use the same build.

The menu background now embeds the original T-80B screenshot from the official GHPC gunnery-guide listing, not the generated tank art. Source: https://gunnerheatpc.com/news/guides . Image: https://static.manakeep.com/photos/2024/03/14/3h5ryob7kq_T_80B_proud.jpg . Image belongs to its original rights holder. JPEG bytes are embedded under the existing resource identifier; Unity LoadImage detects the format. build-repro.ps1 now reads the asset path and verifies its hash from build-lock.json.

Validation: compile and existing automated regression fixtures. These fixtures do not execute Unity AI or validate the visual menu. Live verification must check OFF stops unoccupied friendly driving/firing, enemies continue, ON resumes AI, transferring into a disabled vehicle restores controls/crew, late guests display the setting, and disconnect restores previous states. Publication is separate from local deployment.


## Pre-mission lobby

CoopStagingLobby retains the room transport while no game vehicles exist. Host snapshots contain the selected mission, AI setting, readiness revision and player names. Mission or AI changes and membership changes invalidate readiness so a player cannot approve stale settings. Only authenticated peers with the exact build may ready up; host Start requires every connected peer to be ready.

After Start, the host loads first. Deterministic vehicle IDs and RoomSeats are created only after the native mission is ready. Existing guest mission-loading and assignment then take over without replacing the lobby connections. The host pauses after capture until guests have claimed their vehicles. A mission without enough controllable friendly vehicles fails visibly instead of assigning duplicate seats.

The view uses the official T-80B screenshot across the background. The embedded map is the official Point Alpha preview, displayed only for that theater. Other theaters explicitly have no preview. Vehicle thumbnails are shown only for recognized matching vehicles after loading; lobby cards do not invent equipment assignments.

Automated fixtures cover serialization and existing protocol behavior, not native Unity mission transitions. Live validation is still required for Steam, each mission, disconnects while loading, and the rendered UI.

## Terrain preview update

The lobby now lazy-loads the installed GHPC terrain satellite sources GT01A_SatMap_Master_2048, GT02_SatMap_Master_2048, GT03_SatMap_Master_2048 and GT04_SatMap_Master_2048, keyed by the selected theater. These are satellite previews, not the native interactive topographic map, and are labeled accordingly. Point Alpha retains its official published map; unsupported terrain has an explicit unavailable state. Texture decoding is cached outside repeated GUI rendering. These extracted game assets are for this local build; public redistribution requires a separate asset packaging review.

Mission names are resolved from each client's local MissionMetaData using the shared theater/scene identity. A host's translated display name no longer replaces a guest's own language. Steam browser listings use the same identity fields.

## Firing performance

Normal play no longer formats and writes per-round HOST SHOT, SHOT STATE, VISUAL SHOT and IMPACT REPLAY diagnostics. Enable --coop-shot-audit or --coop-netverify to collect them. Actual firing, impact events and AAR data remain on their existing paths.

Replica tracer meshes are retained in a type-specific pool capped at 64 total idle objects. Missiles and guidance wires are not pooled. Expired IDs are removed after iteration using a reusable list instead of allocating a dictionary snapshot every rendered frame. Session disposal destroys active and pooled objects. Authoritative path endpoints, wall visibility checks and segment lifetime are unchanged; network-arrival gaps may still produce visible discontinuities. Build/protocol tests cannot establish a measured FPS improvement or validate Unity renderer lifetime.

### Guest speed and gear telemetry (development, protocol 29)

The guest deliberately disables vehicle physics and replays authoritative positions.
The native NwhVehicleHud reads NwhChassis.GetActualSpeed() and CurrentGear; those
local physics values therefore stayed zero even while the replica moved. Each vehicle
snapshot now includes signed speed in metres/second and the host transmission gear.
Guest-only getter patches expose these values to the native HUD without restarting
physics or changing host acceleration. The cache is cleared on session disposal.
The wire version is raised to 29: both peers must use the same new build.
Round-trip tests cover reverse, neutral, forward, nonfinite speed and invalid gear.
Unity runtime/HUD verification is still required after restarting both copies.

The inspected host driving log showed timeScale=0.5, throttle=1, and advancing gears.
Half-speed simulation contributes to slow acceleration in wall-clock time. No engine
power or gear ratio was changed; this observation does not exclude other driving issues.

Guest impact visuals are now queued separately from packet receipt, with at most four
spawns and a soft 1.5 ms processing budget per late frame. A single native effect spawn
cannot be preempted. The bounded queue may discard old cosmetic effects under overload;
authoritative damage and AAR records use separate paths and are unaffected.

### Steam same-account lobby joins

A lobby belongs to a Steam account, not to one local game window. Previously the
join path treated both a same-account join and an actual owner change as "Original
host has left". It also retained that lobby for disposal, risking a LeaveLobby call
against the hosting account's membership. The adapter now rejects its own lobby
before joining, and repeats the check in the enter callback before retaining the
lobby handle. The error directs single-PC testing to Direct IP / 127.0.0.1.
Ownership checks for other accounts remain in place; host migration is not supported.
The adapter fixture verifies the specific error and that rejection neither joins nor
leaves the existing lobby. Live Steam joining between separate accounts is not covered
by that fixture and still needs a two-account test.

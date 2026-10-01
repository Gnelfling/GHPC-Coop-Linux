# Steam room browser

This feature is in the mod DLL. It does not change the loader, installer or
Steam entitlement checks. Both players must install the same DLL.

## Player flow

Host: enter a mission, open F8, select Steam, enter ROOM NAME, select PUBLIC or
FRIENDS ONLY, and choose CREATE STEAM ROOM. Capacity comes from available friendly
vehicles, up to four. Mission selection still uses the game's existing menu.
Creating a pre-mission staging lobby is not part of this change.

Guest: open F8 in the main menu, choose BROWSE STEAM ROOMS, REFRESH, then JOIN.
The host's mission is loaded through the existing verified join flow. Invitations
and room IDs remain supported. Public search is limited to 50 matching Steam
results; friends-only and private test rooms are not public listings.

Rows show room name, mission name and Steam lobby membership/capacity. Membership
includes users loading the mission; it is not a count of vehicles already claimed.
Counts are a snapshot at refresh time. A room can fill or close before JOIN; the
existing Steam membership and host seat checks remain authoritative.

After connection, ROOM INFO / PING shows the room details. The host sees each
connected guest's ping; a guest sees its own ping to the host. This is Steam's
measured round-trip latency, not input delay or snapshot age. No pre-join latency
probe is implemented: the browser explicitly says 'After joining'. An unavailable
measurement displays 'Measuring...', never a fabricated zero.

## Implementation and reasons

`src/Networking/Lobby/RoomBrowser.cs` owns the asynchronous search and UI.
Searches happen on request, are throttled for three seconds and time out in the
open browser after 15 seconds. Callback objects are disposed on replacement and
shutdown. No per-frame or background lobby-list polling is added.

`SteamLink` publishes name, mission, capacity and exact mod DLL SHA-256 alongside
the existing protocol and game-build metadata. The file hash is computed once.
Search filters use those identities and each result is checked again before
display. Room-ID joins check the same mod identity, so a stale list cannot bypass
compatibility checks. The existing handshake still checks mission and vehicle
layouts; lobby metadata is discovery information, not gameplay authority.

Display strings strip control characters and rich-text delimiters and have bounded
lengths. SteamPeer reads real-time connection status at most once per second,
only when the connection view requests it; a failed statistics call cannot
disconnect a player.

## Validation

Build and existing automated suites pass, including 48 fake-Steam adapter checks.
New checks cover public/private visibility, advertised metadata, text sanitization,
measured RTT and rejection of a mismatched DLL on room-ID join.

These fixtures do not contact Steam. Live room discovery, GUI layout and joining
between two separate Steam accounts still require runtime verification. Running
game processes keep their previously loaded DLL until restarted; this change was
not hot-loaded into the current test session or published to GitHub.

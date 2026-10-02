using System;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;

namespace GhpcCoop
{
    public sealed partial class CoopLabMod
    {
        string roomDisplayName = "GHPC Co-op", roomListNotice = "Refresh to find public rooms.";
        bool listPublicRoom = true, roomBrowserOpen, roomListBusy;
        float roomListStarted, nextRoomRefresh;
        Vector2 roomListScroll;
        CallResult<LobbyMatchList_t> roomListRequest;
        readonly List<RoomListing> roomListings = new List<RoomListing>();

        sealed class RoomListing
        {
            public string Id, Name, Mission;
            public int Members, Capacity;
        }

        void RefreshRooms()
        {
            if (link != null || Time.realtimeSinceStartup < nextRoomRefresh) return;
            SteamLink.CheckSteam();
            if (roomListRequest != null) roomListRequest.Dispose();
            roomListings.Clear();
            roomListBusy = true;
            roomListStarted = Time.realtimeSinceStartup;
            nextRoomRefresh = roomListStarted + 3;
            roomListNotice = "Searching Steam for matching public rooms...";
            roomListRequest = CallResult<LobbyMatchList_t>.Create((result, failed) =>
            {
                roomListBusy = false;
                if (failed) { roomListNotice = "Steam room search failed. Refresh to retry."; return; }
                try
                {
                    for (int i = 0; i < Math.Min(result.m_nLobbiesMatching, 50u); i++)
                    {
                        var id = SteamMatchmaking.GetLobbyByIndex(i);
                        // Recheck returned metadata: a listing may change while Steam searches.
                        if (!SteamRoomPolicy.Compatible(SteamMatchmaking.GetLobbyData(id, "ghpc_coop"),
                            SteamMatchmaking.GetLobbyData(id, "wire"), SteamMatchmaking.GetLobbyData(id, "build"), build) ||
                            SteamMatchmaking.GetLobbyData(id, "mod_revision") != SteamLink.ModRevision ||
                            SteamMatchmaking.GetLobbyData(id, "owner") != SteamMatchmaking.GetLobbyOwner(id).ToString()) continue;
                        int capacity = SteamMatchmaking.GetLobbyMemberLimit(id);
                        if (capacity < 2 || capacity > 4) continue;
                        roomListings.Add(new RoomListing
                        {
                            Id = id.ToString(),
                            Name = SteamLink.DisplayText(SteamMatchmaking.GetLobbyData(id, "name"), 48),
                            Mission = SteamLink.DisplayText(LocalMissionTitle(
                                SteamMatchmaking.GetLobbyData(id, "mission_theater"),
                                SteamMatchmaking.GetLobbyData(id, "mission_key")), 80),
                            Members = SteamMatchmaking.GetNumLobbyMembers(id), Capacity = capacity
                        });
                    }
                    roomListNotice = roomListings.Count == 0 ? "No matching public rooms. Hosts must enable PUBLIC and use the same mod DLL." :
                        roomListings.Count + " rooms found. Counts include players loading the mission. Ping is measured after joining.";
                }
                catch (Exception e) { roomListings.Clear(); roomListNotice = "Room search failed: " + SteamLink.DisplayText(e.Message, 120); }
            });
            SteamMatchmaking.AddRequestLobbyListStringFilter("ghpc_coop", "0.9.9.1", ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter("wire", Wire.Version.ToString(), ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter("build", build, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter("mod_revision", SteamLink.ModRevision, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
            SteamMatchmaking.AddRequestLobbyListResultCountFilter(50);
            roomListRequest.Set(SteamMatchmaking.RequestLobbyList());
        }

        static string PingLabel(int value) { return value < 0 ? "Measuring..." : value + " ms"; }

        void DrawRoomBrowser()
        {
            GUI.DrawTexture(new Rect(0, 0, MenuWidth, MenuHeight), menuFrame);
            GUI.Label(new Rect(36, 26, 800, 44), link == null ? "PUBLIC CO-OP ROOMS" : "ROOM / CONNECTIONS", menuTitle);
            if (MenuAction(new Rect(880, 27, 122, 40), "BACK")) { roomBrowserOpen = false; return; }
            var steam = link as SteamLink;
            if (steam != null)
            {
                GUI.Label(new Rect(36, 94, 950, 35), steam.DisplayName, menuHeading);
                GUI.Label(new Rect(36, 137, 950, 60), "MISSION  " + steam.MissionName, menuText);
                var id = new CSteamID(ulong.Parse(steam.RoomId == "" ? "0" : steam.RoomId));
                if (id.m_SteamID != 0)
                    GUI.Label(new Rect(36, 201, 950, 35), "PLAYERS  " + SteamMatchmaking.GetNumLobbyMembers(id) + " / " + SteamMatchmaking.GetLobbyMemberLimit(id), menuText);
                GUI.Label(new Rect(36, 251, 950, 50), "PING TO HOST / Steam round-trip time (not total input delay)", menuSmall);
                if (hosting)
                {
                    GUI.Label(new Rect(36, 310, 950, 34), "YOU / HOST — local", menuText);
                    int row = 0;
                    foreach (var peer in guests)
                    {
                        var transport = peer.Link as SteamPeer;
                        string name = SteamLink.DisplayText(peer.Name, 48);
                        GUI.Label(new Rect(36, 359 + row++ * 48, 950, 38),
                            (name.Length == 0 ? "Guest " + peer.Id : name) + "    " + PingLabel(transport == null ? -1 : transport.PingMs), menuText);
                    }
                }
                else GUI.Label(new Rect(36, 310, 950, 38), "YOU → HOST    " + PingLabel(steam.PingMs), menuText);
                return;
            }
            if (link != null) { GUI.Label(new Rect(36, 105, 950, 60), "Disconnect before browsing Steam rooms.", menuText); return; }
            if (roomListBusy && Time.realtimeSinceStartup - roomListStarted > 15)
            {
                roomListBusy = false;
                if (roomListRequest != null) roomListRequest.Dispose();
                roomListNotice = "Search timed out. Refresh to retry.";
            }
            GUI.enabled = !roomListBusy && Time.realtimeSinceStartup >= nextRoomRefresh;
            if (MenuAction(new Rect(36, 90, 210, 40), "REFRESH", true))
            {
                try { RefreshRooms(); }
                catch (Exception e) { roomListBusy = false; roomListNotice = SteamLink.DisplayText(e.Message, 120); }
            }
            GUI.enabled = true;
            GUI.Label(new Rect(36, 145, 960, 66), roomListNotice, menuSmall);
            GUI.Label(new Rect(36, 214, 460, 30), "ROOM / MISSION", menuHeading);
            GUI.Label(new Rect(535, 214, 130, 30), "PLAYERS", menuHeading);
            GUI.Label(new Rect(675, 214, 160, 30), "PING", menuHeading);
            roomListScroll = GUI.BeginScrollView(new Rect(36, 253, 970, 510), roomListScroll,
                new Rect(0, 0, 945, Mathf.Max(500, roomListings.Count * 96)));
            try
            {
                for (int i = 0; i < roomListings.Count; i++)
                {
                    var room = roomListings[i]; float y = i * 96;
                    GUI.DrawTexture(new Rect(0, y, 939, 88), menuIdle);
                    GUI.Label(new Rect(12, y + 7, 480, 32), room.Name, menuText);
                    GUI.Label(new Rect(12, y + 42, 480, 38), room.Mission, menuSmall);
                    GUI.Label(new Rect(500, y + 22, 120, 32), room.Members + " / " + room.Capacity, menuText);
                    GUI.Label(new Rect(640, y + 22, 150, 40), "After joining", menuSmall);
                    GUI.enabled = room.Members < room.Capacity;
                    if (MenuAction(new Rect(798, y + 19, 128, 44), room.Members < room.Capacity ? "JOIN" : "FULL", true))
                    {
                        Guard(() => { steamRoom = room.Id; Start(false, true); roomBrowserOpen = false; });
                        if (roomBrowserOpen) roomListNotice = status;
                    }
                    GUI.enabled = true;
                }
            }
            finally { GUI.EndScrollView(); }
            GUI.Label(new Rect(36, 780, 960, 48), "Hosts: enter a mission, set a room name, choose PUBLIC, then CREATE STEAM ROOM.", menuSmall);
        }
    }
}

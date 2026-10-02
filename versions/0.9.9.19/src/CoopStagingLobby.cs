using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using GHPC;
using GHPC.Player;
using UnityEngine;

namespace GhpcCoop
{
    public sealed partial class CoopLabMod
    {
        bool stagingHost, stagingGuest, stagingLoading, stagingReady;
        long lobbyRevision;
        float nextLobbyState, stagingLoadAt;
        string stagingTheater = "", stagingMission = "", stagingTitle = "Choose a mission";
        string[] stagingPlayers = new string[0];
        readonly HashSet<int> lobbyAuthenticated = new HashSet<int>();
        readonly HashSet<int> lobbyReady = new HashSet<int>();
        bool missionPicker;
        bool waitingForLobbyPlayers;
        Vector2 missionScroll;

        string LocalMissionTitle(string theaterKey, string missionKey)
        {
            if (String.IsNullOrEmpty(missionKey)) return "Choose a mission";
            // Local metadata is English in the stock game and translated by the
            // installed language patch. Never use another player's translated title.
            var theater = Resources.FindObjectsOfTypeAll<GHPC.Mission.Data.MissionTheaterScriptable>()
                .FirstOrDefault(t => t.Key == theaterKey && t.Missions != null);
            var mission = theater == null ? null : theater.Missions.FirstOrDefault(m =>
                m != null && !m.IsCategory && m.MissionSceneReference.Name == missionKey);
            return mission == null ? missionKey : mission.MissionName;
        }

        void ResetStaging()
        {
            if (waitingForLobbyPlayers) GHPC.State.TimeController.Unpause();
            waitingForLobbyPlayers = false;
            stagingHost = stagingGuest = stagingLoading = stagingReady = false;
            lobbyAuthenticated.Clear(); lobbyReady.Clear(); stagingPlayers = new string[0];
            nextLobbyState = 0;
        }

        void InvalidateLobbyReady()
        {
            lobbyRevision++;
            stagingReady = false; lobbyReady.Clear(); nextLobbyState = 0;
        }

        void CreateStagingRoom()
        {
            if (link != null) return;
            SteamLink.CheckSteam();
            if (directMode && !directLocalOnly && directCode.Trim().Length < 8)
                throw new InvalidOperationException("LAN room code requires at least 8 characters.");
            token = directMode ? directCode.Trim() : Wire.NewToken();
            hosting = stagingHost = true;
            started = Time.realtimeSinceStartup;
            wasBackground = Application.runInBackground;
            Application.runInBackground = true;
            multiRoom = directMode ? (IRoom)new MultiRoom() : new SteamLink(token, build, false, roomDisplayName, "Choosing mission", listPublicRoom);
            link = multiRoom;
            multiRoom.Host(directMode && directLocalOnly ? System.Net.IPAddress.Loopback : System.Net.IPAddress.Any, directMode ? DirectPort() : 0);
            InvalidateLobbyReady();
            status = "Room open. Choose a mission and ready up.";
            panel = true;
        }

        void SendStagingState(float now)
        {
            if (now < nextLobbyState) return;
            nextLobbyState = now + .5f;
            var steam = link as SteamLink;
            if (steam != null) steam.UpdateMissionListing(stagingTitle, stagingTheater, stagingMission);
            var players = new List<string> { (stagingReady ? "READY|" : "WAITING|") + CleanPlayerName(LocalDisplayName()) };
            foreach (var peer in guests.Where(p => lobbyAuthenticated.Contains(p.Id)))
                players.Add((lobbyReady.Contains(peer.Id) ? "READY|" : "WAITING|") + CleanPlayerName(peer.Name));
            stagingPlayers = players.ToArray();
            foreach (var peer in guests.Where(p => lobbyAuthenticated.Contains(p.Id)))
                peer.Link.Send(new Message { Kind = Kind.Ping, Text = "coop-lobby-v1", Build = build,
                    World = stagingMission, Unit = stagingTheater, SupportId = stagingTitle, Roster = String.Join("\n", stagingPlayers),
                    Sequence = lobbyRevision, Fire = teammateAiEnabled, Reload = stagingLoading });
        }

        void ReceiveStagingState(Message message)
        {
            if (message.Build != build) throw new IOException("Lobby build mismatch");
            bool enteringLobby = !stagingGuest;
            if (message.Sequence != lobbyRevision) stagingReady = false;
            lobbyRevision = message.Sequence;
            stagingGuest = true;
            stagingLoading = message.Reload;
            bool missionChanged = enteringLobby || stagingMission != message.World || stagingTheater != message.Unit;
            stagingMission = message.World; stagingTheater = message.Unit;
            if (missionChanged) stagingTitle = LocalMissionTitle(stagingTheater, stagingMission);
            teammateAiEnabled = message.Fire;
            stagingPlayers = message.Roster.Split('\n').Take(4).ToArray();
            // A repeated state packet must not undo the user's Close/F8 action.
            if (enteringLobby) { ResetLobbyNavigation(); panel = true; }
            status = stagingLoading ? "Host is loading the selected mission..." : "Choose READY when you are ready to load the selected mission.";
        }

        void SetLobbyReady()
        {
            if (String.IsNullOrEmpty(stagingMission) || stagingLoading) return;
            stagingReady = !stagingReady;
            nextLobbyState = 0;
            if (stagingGuest) link.Send(new Message { Kind = Kind.Ping, Text = "coop-lobby-ready", Sequence = lobbyRevision, Fire = stagingReady });
        }

        bool EveryoneLobbyReady()
        {
            return stagingReady && !String.IsNullOrEmpty(stagingMission) &&
                guests.Count == lobbyAuthenticated.Count && lobbyAuthenticated.All(id => lobbyReady.Contains(id));
        }

        void LaunchStagingMission()
        {
            if (!stagingHost || !EveryoneLobbyReady() || stagingLoading) return;
            var theater = Resources.FindObjectsOfTypeAll<GHPC.Mission.Data.MissionTheaterScriptable>().First(t => t.Key == stagingTheater);
            var meta = theater.Missions.First(m => m != null && !m.IsCategory && m.MissionSceneReference.Name == stagingMission);
            var controller = UnityEngine.Object.FindObjectOfType<SceneController>();
            if (controller == null) throw new InvalidOperationException("Mission loader is not ready");
            int terrain = UnityEngine.SceneManagement.SceneUtility.GetBuildIndexByScenePath(theater.TerrainSceneReference.Path);
            if (terrain < 0) throw new InvalidOperationException("Mission terrain is missing");
            stagingLoading = true; stagingLoadAt = Time.realtimeSinceStartup;
            terrainName = theater.TerrainSceneReference.Name; sceneReadyAt = 0;
            GHPC.State.PersistentDataManager.PersistMissionInfo(new GHPC.State.Data.MissionInfoData(meta.MissionName, "Co-op"), meta, theater.Key);
            GHPC.Mission.DynamicMissionComposer.CampaignMode = false;
            GHPC.Mission.DynamicMissionComposer.DynamicMission = meta.IsFlexMission;
            GHPC.Mission.DynamicMissionComposer.NeedsFlexMissionSetup = meta.IsFlexMission;
            GHPC.Mission.DynamicMissionComposer.CurrentMissionNameKey = stagingMission;
            SceneController.IsDaytime = meta.IsDefaultDayMission;
            SceneController.AllowTimeChoice = false;
            SceneController.TargetSpawningFaction = MissionPolicy.DefaultFaction(meta);
            status = "Loading mission. Player vehicles will be assigned after loading.";
            controller.LoadSceneByMission(terrain + "," + stagingMission);
        }

        void UpdateStagingHost(float now)
        {
            multiRoom.Pump();
            if (!String.IsNullOrEmpty(multiRoom.Error)) throw new IOException(multiRoom.Error);
            ILink accepted;
            while ((accepted = multiRoom.Take()) != null)
            {
                if (stagingLoading || guests.Count >= 3) { accepted.Dispose(); continue; }
                guests.Add(new Peer { Id = ++nextPeer, Link = accepted, Seen = now });
                InvalidateLobbyReady();
            }
            foreach (var peer in guests.ToArray())
            {
                if (!String.IsNullOrEmpty(peer.Link.Error) || now - peer.Seen > 180)
                {
                    peer.Link.Dispose(); guests.Remove(peer); lobbyAuthenticated.Remove(peer.Id); InvalidateLobbyReady(); continue;
                }
                Message message; int budget = 0;
                while (budget++ < 32 && peer.Link.TryRead(out message))
                {
                    peer.Seen = now;
                    if (!lobbyAuthenticated.Contains(peer.Id))
                    {
                        if (message.Kind != Kind.JoinRoom || message.Token != token || message.Build != build)
                        {
                            // Reject this connection without tearing down the room.
                            peer.Link.Dispose(); guests.Remove(peer);
                            InvalidateLobbyReady();
                            break;
                        }
                        peer.Name = CleanPlayerName(message.Text); peer.NamesSupported = message.Roster == "names-v1";
                        lobbyAuthenticated.Add(peer.Id); nextLobbyState = 0;
                    }
                    else if (!stagingLoading && message.Kind == Kind.Ping && message.Text == "coop-lobby-ready" && message.Sequence == lobbyRevision)
                    {
                        if (message.Fire) lobbyReady.Add(peer.Id); else lobbyReady.Remove(peer.Id);
                        nextLobbyState = 0;
                    }
                }
            }
            SendStagingState(now);
            if (!stagingLoading) return;
            if (now - stagingLoadAt > 180) throw new IOException("Host mission loading timed out");
            var player = PlayerInput.Instance;
            if (player == null || !player.IsInitialized || player.CurrentPlayerUnit == null ||
                player.CurrentPlayerUnit.gameObject.scene.name != terrainName || sceneReadyAt <= 0 || now < sceneReadyAt) return;
            // Retain the lobby transports. Only the vehicle-dependent session is created
            // now, when deterministic vehicle identities actually exist on the host.
            game.Capture();
            initialRoomVehicles = game.RoomChoices().Concat(new[] { game.LocalId }).ToArray();
            seats = new RoomSeats(game.LocalId, initialRoomVehicles, game.FriendlyChoices());
            if (seats.Capacity < guests.Count + 1) throw new IOException("This mission has too few controllable friendly vehicles for the room.");
            support = new SupportSync(true); tracers = new TracerSync(true);
            waitingForLobbyPlayers = guests.Count > 0;
            if (waitingForLobbyPlayers) GHPC.State.TimeController.Pause();
            foreach (var peer in guests)
            {
                peer.Session = new HostSession(token, build, game.World, game.Roster, game.LocalId, game.FriendlyChoices());
                peer.Link.Send(new Message { Kind = Kind.Ping, Text = "coop-lobby-launch" });
            }
            stagingHost = stagingLoading = false;
            panel = false;
            status = "Host loaded; guests are loading their assigned vehicles.";
        }
    }
}

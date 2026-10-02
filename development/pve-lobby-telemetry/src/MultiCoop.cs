using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using GHPC;
using GHPC.Player;
using UnityEngine;

namespace GhpcCoop
{
    public sealed partial class CoopLabMod
    {
        sealed class Peer
        {
            public int Id;
            public readonly ResyncGate Resync = new ResyncGate();
            public string Name = "";
            public bool NamesSupported;
            public ILink Link;
            public HostSession Session;
            public bool Claimed;
            public float Seen, CloseAt;
        }

        readonly List<Peer> guests = new List<Peer>();
        RoomSeats seats;
        IRoom multiRoom;
        int nextPeer;
        float nextRoomSnapshot;
        readonly System.Diagnostics.Stopwatch snapshotTimer = new System.Diagnostics.Stopwatch();
        float nextHostTimingLog, lastSnapshotAt, worstSnapshotGap;
        double worstSnapshotWorkMs;
        string[] initialRoomVehicles = new string[0];
        void BeginMultiHost(bool steam = true)
        {
            var platoon = game.RoomChoices().Concat(new[] { game.LocalId }).ToArray();
            initialRoomVehicles = platoon;
            seats = new RoomSeats(game.LocalId, platoon, game.FriendlyChoices());
            GameBridge.Log("ROOM capacity=" + seats.Capacity + " host=" + game.LocalId + " platoon=" + String.Join(",", platoon));
            if (seats.Capacity < 2)
            {
                var host = game.Vehicles[game.LocalId].Unit;
                var rows = new List<string>
                {
                    "Vehicle\tFaction\tHost\tPlatoon\tPlatoonInitialized\tMembers\tAvailable\tSamePlatoon"
                };
                foreach (var item in game.Vehicles)
                {
                    var u = item.Value.Unit;
                    rows.Add(u.UniqueName + "\t" + u.Allegiance + "\t" + (u == host) + "\t" + (u.Platoon == null ? "null" : GameBridge.PathOf(u.Platoon.transform)) + "\t" + u.PlatoonInitialized + "\t" + (u.Platoon == null ||
                        u.Platoon.Units == null ? 0 : u.Platoon.Units.Count) + "\t" + game.Available(item.Key) + "\t" + (host.Platoon != null &&
                        u.Platoon == host.Platoon));
                }

                File.WriteAllLines(Path.Combine(DataDir, "platoon-capacity-diagnostic.tsv"), rows.ToArray());
                throw new InvalidOperationException("Your side needs at least two available controllable vehicles");
            }

            if (steam)
            {
                var missionState = GHPC.State.PersistentDataManager.GetStateData();
                var missionName = missionState != null && missionState.MetaData != null
                    ? missionState.MetaData.MissionName : "Unknown mission";
                var room = new SteamLink(token, build, sweepEnabled, roomDisplayName, missionName, listPublicRoom);
                room.SetCapacity(seats.Capacity);
                multiRoom = room;
            }
            else
                multiRoom = new MultiRoom();
            link = multiRoom;
            multiRoom.Host(steam || !directLocalOnly ? System.Net.IPAddress.Any : System.Net.IPAddress.Loopback, steam ? 0 : DirectPort());
            status = "Room open: 1/" + seats.Capacity + " players (including host). " + (steam ? "Creating Steam lobby..." : "Direct TCP port " + DirectPort());
            File.WriteAllLines(Path.Combine(DataDir, "room.txt"), new[] { token, build, game.World, game.Roster, game.LocalId, platoon.First(x => x != game.LocalId) });
        }

        void DropPeer(Peer peer)
        {
            guests.Remove(peer);
            seats.Release(peer.Id);
            peer.Session.Disconnect();
            try
            {
                peer.Link.Dispose();
            }
            catch (Exception e)
            {
                GameBridge.Log("ROOM close peer=" + peer.Id + ": " + e.Message);
            }

            try
            {
                game.ReleasePeer(peer.Id);
            }
            catch (Exception e)
            {
                GameBridge.Log("ROOM peer cleanup=" + peer.Id + ": " + e.Message);
            }

            claimed = guests.Any(x => x.Claimed);
            BroadcastOccupancy();
            GameBridge.Log("ROOM departed peer=" + peer.Id + " players=" + seats.Count);
        }

        void RunPeerPhase(string phase, Action<int> action)
        {
            PeerIsolation.Run(guests.Where(x => x.Claimed && x.CloseAt <= 0 && !x.Resync.Pending), p => action(p.Id), (p, e) =>
            {
                GameBridge.Log("ROOM isolated " + phase + " error peer=" + p.Id + ": " + e);
                try
                {
                    p.Link.Send(new Message { Kind = Kind.Error, Text = "Your vehicle encountered a host error. Rejoin the room." });
                }
                catch
                {
                // The connection is already failing; a failed notification or close must not prevent peer removal.
                }

                DropPeer(p);
            });
        }

        void Broadcast(Message m)
        {
            // Encode lazily once for Steam peers; TCP keeps its worker-owned queue.
            // Each peer still owns its retry queue and failure handling.
            byte[] encoded = null;
            foreach (var peer in guests.Where(x => x.Claimed).ToArray())
                try
                {
                    if (m.Kind == Kind.Snapshot && peer.Resync.Pending) continue;
                    var steamPeer = peer.Link as SteamPeer;
                    if (steamPeer == null)
                        peer.Link.Send(m);
                    else
                    {
                        if (encoded == null)
                            encoded = Wire.Encode(m);
                        steamPeer.SendEncoded(encoded, m.Kind);
                    }
                }
                catch (Exception error)
                {
                    GameBridge.Log("ROOM broadcast failed peer=" + peer.Id +
                        " kind=" + m.Kind + ": " + error);
                    DropPeer(peer);
                }
        }

        void BroadcastOccupancy()
        {
            if (seats == null)
                return;
            foreach (var peer in guests.Where(x => x.Claimed).ToArray())
                try
                {
                    peer.Link.Send(new Message { Kind = Kind.VehicleAssignment,
                        Unit = game.LocalId,
                        Text = seats.Vehicle(peer.Id),
                        Roster = String.Join("\n", seats.OccupiedVehicles()),
                        Role = seats.Capacity });
                    if (peer.NamesSupported)
                        peer.Link.Send(new Message { Kind = Kind.PlayerNames, Text = RoomNameRows() });
                }
                catch (Exception error)
                {
                    GameBridge.Log("ROOM occupancy update failed peer=" + peer.Id + ": " + error);
                    try
                    {
                        peer.Link.Dispose();
                    }
                    catch
                    {
                    // The connection is already failing; a failed notification or close must not prevent peer removal.
                    }
                }
        }

        void UpdateMultiHost(float now)
        {
            DiscoverRosterChanges(now);
            multiRoom.Pump();
            if (!String.IsNullOrEmpty(multiRoom.Error))
                throw new IOException(multiRoom.Error);
            ILink accepted;
            while ((accepted = multiRoom.Take()) != null)
            {
                if (guests.Count >= seats.Capacity - 1)
                {
                    accepted.Dispose();
                    continue;
                }

                guests.Add(new Peer { Id = ++nextPeer,
                    Link = accepted,
                    Seen = now,
                    Session = new HostSession(token, build, game.World, game.Roster, game.LocalId, game.FriendlyChoices()) });
            }

            foreach (var peer in guests.ToArray())
            {
                if (peer.CloseAt > 0)
                {
                    if (now >= peer.CloseAt)
                        DropPeer(peer);
                    continue;
                }

                if (!String.IsNullOrEmpty(peer.Link.Error) || now - peer.Seen > 180)
                {
                    GameBridge.Log("ROOM disconnect peer=" + peer.Id + " reason=" +
                        (String.IsNullOrEmpty(peer.Link.Error) ? "Receive timeout" : peer.Link.Error));
                    DropPeer(peer);
                    continue;
                }

                if (!peer.Link.Connected)
                    continue;
                try
                {
                    if (peer.Claimed)
                        RequireHostResync(peer.Link, peer.Resync, seats.Vehicle(peer.Id),
                            () => game.SuspendPeerInput(peer.Id), now);
                    Message m;
                    int budget = 0;
                    while (budget++ < 32 && peer.Link.TryRead(out m))
                    {
                        peer.Seen = now;
                        if (peer.Claimed)
                        {
                            if (HandleHostResync(m, peer.Link, peer.Resync, seats.Vehicle(peer.Id),
                                () => game.SuspendPeerInput(peer.Id), now)) continue;
                            if ((m.Kind == Kind.Input || m.Kind == Kind.SupportRequest || m.Kind == Kind.InfantryOrder) &&
                                !peer.Resync.AllowsInput(m)) continue;
                            if (peer.Resync.Pending && m.Kind == Kind.SwitchVehicle)
                            {
                                peer.Link.Send(new Message { Kind = Kind.SwitchRejected, Text = "Wait for vehicle resynchronization" });
                                continue;
                            }
                        }
                        if (m.Kind == Kind.Claim && m.Unit != seats.Vehicle(peer.Id))
                            throw new IOException("Vehicle was not reserved for this player");
                        if (m.Kind == Kind.SwitchVehicle)
                        {
                            if (!peer.Claimed || !game.Available(m.Unit) || !seats.CanMove(peer.Id, m.Unit))
                            {
                                peer.Link.Send(new Message { Kind = Kind.SwitchRejected, Text = "Vehicle occupied or unavailable for your side" });
                                continue;
                            }
                        }

                        var reply = peer.Session.Handle(m);
                        if (m.Kind == Kind.JoinRoom && reply.Kind == Kind.Mission)
                        {
                            peer.Name = CleanPlayerName(m.Text);
                            peer.NamesSupported = m.Roster == "names-v1";
                        }

                        if (reply.Kind == Kind.Mission)
                        {
                            var unit = seats.Reserve(peer.Id, initialRoomVehicles.Where(game.Available));
                            if (unit == null)
                                throw new IOException("Room full: no free friendly vehicle (capacity " + seats.Capacity + ")");
                            var state = GHPC.State.PersistentDataManager.GetStateData();
                            reply.Unit = unit;
                            reply.Text = state.TheaterKey + "\n" + state.MetaData.MissionSceneReference.Name + "\n" + MissionConfiguration.Export(state.MetaData.MissionSceneReference.Name) + "\n" + MissionChoices.Export();
                            reply.Role = (int)game.Vehicles[game.LocalId].Unit.Allegiance;
                            reply.Fire = SceneController.IsDaytime;
                        }

                        if (reply.Kind == Kind.Claimed)
                        {
                            if (!game.Available(reply.Unit))
                                throw new IOException("Reserved vehicle no longer available");
                            game.ReservePeer(peer.Id, reply.Unit);
                            peer.Claimed = true;
                            claimed = true;
                            infantry.ResendAll();
                            panel = false;
                            GameBridge.Log("ROOM claimed peer=" + peer.Id + " vehicle=" + reply.Unit + " players=" + seats.Count + "/" + seats.Capacity);
                        }

                        if (reply.Kind == Kind.VehicleAssignment)
                        {
                            if (!seats.Move(peer.Id, reply.Text))
                                throw new IOException("Vehicle reservation changed during transfer");
                            game.ReservePeer(peer.Id, seats.Vehicle(peer.Id));
                        }

                        if (m.Kind == Kind.InfantryOrder &&
                            reply.Kind == Kind.Ping &&
                            !GHPC.AarController.InAar &&
                            !GHPC.State.TimeController.Paused)
                            infantry.HandleRequest(m, game.Vehicles[seats.Vehicle(peer.Id)].Unit);
                        if (m.Kind == Kind.Input && reply.Kind == Kind.Ping)
                            game.ReceivePeer(peer.Id, m);
                        if (m.Kind == Kind.SupportRequest && reply.Kind == Kind.Ping)
                        {
                            reply = support.HandleRequest(m, game.Vehicles[seats.Vehicle(peer.Id)].Unit);
                            Broadcast(reply);
                        }
                        else
                            peer.Link.Send(reply);
                        if (reply.Kind == Kind.Claimed || reply.Kind == Kind.VehicleAssignment)
                            BroadcastOccupancy();
                        if (peer.Claimed)
                            RequireHostResync(peer.Link, peer.Resync, seats.Vehicle(peer.Id),
                                () => game.SuspendPeerInput(peer.Id), now);
                        if (reply.Kind == Kind.Error)
                        {
                            peer.CloseAt = now + .5f;
                            break;
                        }
                    }
                }
                catch (Exception e)
                {
                    GameBridge.Log("ROOM peer=" + peer.Id + " rejected: " + e.Message);
                    try
                    {
                        peer.Link.Send(new Message { Kind = Kind.Error, Text = e.Message });
                    }
                    catch
                    {
                    // The connection is already failing; a failed notification or close must not prevent peer removal.
                    }

                    peer.CloseAt = now + .5f;
                }
            }

            status = "Players: " + seats.Count + "/" + seats.Capacity + " (including host)";
            var input = PlayerInput.Instance;
            if (input != null && input.CurrentPlayerUnit != null && input.CurrentPlayerUnit != game.Vehicles[game.LocalId].Unit)
            {
                var id = game.Vehicles.FirstOrDefault(x => x.Value.Unit == input.CurrentPlayerUnit).Key;
                // Validate and transfer first; do not re-enter a destroyed vehicle before handling TAB.
                if (id != null)
                    RequestVehicle(id);
                var assigned = game.Vehicles[game.LocalId].Unit;
                if (input.CurrentPlayerUnit != assigned)
                    input.SetPlayerUnit(assigned);
            }

            if (waitingForLobbyPlayers)
            {
                if (guests.All(peer => peer.Claimed))
                {
                    waitingForLobbyPlayers = false;
                    GHPC.State.TimeController.Unpause();
                    nextPauseNotice = 0;
                }
                else
                {
                    GHPC.State.TimeController.Pause();
                    status = "Waiting for all players to finish loading...";
                    return;
                }
            }
            if (!claimed)
                return;
            if (NotifyHostPause(now))
                return;
            support.Ready = tracers.Ready = true;
            RunPeerPhase("fire", game.FirePeer);
            foreach (var result in support.Drain())
            {
                result.Sequence = ++seq;
                Broadcast(result);
            }

            if (now >= nextRoomSnapshot)
            {
                if (lastSnapshotAt > 0) worstSnapshotGap = Mathf.Max(worstSnapshotGap, now - lastSnapshotAt);
                lastSnapshotAt = now;
                snapshotTimer.Restart();
                nextRoomSnapshot = NetworkSendClock.Next(nextRoomSnapshot, now);
                var snap = new Message
                {
                    Kind = Kind.Snapshot,
                    Sequence = ++seq,
                    Poses = game.Snapshot(),
                    SyncRevision = game.RosterRevision,
                    Supports = support.Capture(),
                    Objectives = ObjectiveSync.Capture(),
                    Weather = WeatherSync.Capture()
                };
                WeatherSync.CaptureSky(snap);
                Broadcast(snap);
                snapshotTimer.Stop();
                worstSnapshotWorkMs = Math.Max(worstSnapshotWorkMs, snapshotTimer.Elapsed.TotalMilliseconds);
                if (now >= nextHostTimingLog)
                {
                    GameBridge.Log("HOST TIMING snapshotWorkMs=" + worstSnapshotWorkMs.ToString("F1") +
                        " snapshotGapMs=" + (worstSnapshotGap * 1000).ToString("F1") +
                        " vehicles=" + snap.Poses.Length + " guests=" + guests.Count);
                    nextHostTimingLog = now + 5;
                    worstSnapshotGap = 0;
                    worstSnapshotWorkMs = 0;
                }
                Broadcast(helicopters.Capture(++seq));
                var paths = tracers.Drain();
                if (paths.Length > 0)
                    Broadcast(new Message { Kind = Kind.Tracers, Sequence = ++seq, Tracers = paths });
            }

            if (now >= nextInfantry)
            {
                nextInfantry = now + .1f;
                foreach (var state in infantry.Capture())
                    Broadcast(state);
            }

            var impacts = game.Combat.Drain();
            if (impacts.Length > 0)
                Broadcast(new Message { Kind = Kind.Effects, Sequence = ++seq, Impacts = impacts });
        }
    }
}

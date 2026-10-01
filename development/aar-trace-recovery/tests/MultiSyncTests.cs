using System;
using System.Net;
using System.Threading;
using System.Collections.Generic;

namespace GhpcCoop
{
    class MultiSyncTests
    {
        static int checks;
        static void Check(bool ok, string name)
        {
            if (!ok)
                throw new Exception(name);
            checks++;
            Console.WriteLine("PASS " + name);
        }

        static Message Read(Link link)
        {
            var end = DateTime.UtcNow.AddSeconds(5);
            Message m;
            while (DateTime.UtcNow < end)
            {
                if (link.TryRead(out m))
                    return m;
                Thread.Sleep(1);
            }

            throw new Exception("receive timeout " + link.Error);
        }

        static Message Input(string unit, long sequence, bool fire, float aim)
        {
            return new Message
            {
                Kind = Kind.Input,
                Unit = unit,
                Sequence = sequence,
                Fire = fire,
                AimX = (float)Math.Sin(aim),
                AimZ = (float)Math.Cos(aim),
                Range = aim * 100,
                Poses = new[]
                {
                    new Pose
                    {
                        Id = unit,
                        Qw = 1,
                        Qy = aim / 100
                    }
                }
            };
        }

        static void Run(int players)
        {
            var clients = new List<Link>();
            var peers = new List<Link>();
            using (var server = new MultiRoom())
                try
                {
                    server.Host(IPAddress.Loopback, 0);
                    var units = new[]
                    {
                        "host",
                        "guest1",
                        "guest2",
                        "guest3"
                    };
                    var seats = new RoomSeats("host", units);
                    var sessions = new List<HostSession>();
                    var latches = new List<ShotInputLatch>();
                    for (int i = 1; i < players; i++)
                    {
                        var c = new Link();
                        clients.Add(c);
                        c.Join("127.0.0.1", server.Port);
                        Link p = null;
                        var end = DateTime.UtcNow.AddSeconds(5);
                        while (p == null && DateTime.UtcNow < end)
                        {
                            p = server.Take();
                            Thread.Sleep(1);
                        }

                        if (p == null)
                            throw new Exception("accept failed");
                        peers.Add(p);
                        var session = new HostSession("token", "build", "world", "roster", "host", units);
                        sessions.Add(session);
                        latches.Add(new ShotInputLatch());
                        seats.Reserve(i, units);
                        c.Send(new Message { Kind = Kind.Hello, Token = "token", Build = "build", World = "world", Roster = "roster" });
                        p.Send(session.Handle(Read(p)));
                        Check(Read(c).Kind == Kind.Welcome, "handshake " + players + "p guest" + i);
                        c.Send(new Message { Kind = Kind.Claim, Unit = seats.Vehicle(i) });
                        p.Send(session.Handle(Read(p)));
                        Check(Read(c).Kind == Kind.Claimed, "unique claim " + players + "p guest" + i);
                    }

                    // All guest streams enqueue click and changed-aim release before host drains them.
                    for (int i = 0; i < clients.Count; i++)
                    {
                        clients[i].Send(Input(units[i + 1], 1, true, i + 1));
                        clients[i].Send(Input(units[i + 1], 2, false, i + 11));
                    }

                    Thread.Sleep(150);
                    for (int i = 0; i < peers.Count; i++)
                    {
                        var click = Read(peers[i]);
                        var release = Read(peers[i]);
                        Check(sessions[i].Handle(click).Kind == Kind.Ping &&
                            sessions[i].Handle(release).Kind == Kind.Ping,
                            "validated burst " + players + "p guest" + (i + 1));
                        latches[i].Receive(click, 1);
                        latches[i].Receive(release, 1);
                        var shot = latches[i].Select(release, 1.01f);
                        Check(Math.Abs(shot.AimX - (float)Math.Sin(i + 1)) < .0001f &&
                            shot.Range == (i + 1) * 100,
                            "delayed burst preserves firing aim " + players + "p guest" + (i + 1));
                    }

                    for (int frame = 1; frame <= 20; frame++)
                    {
                        var snap = new Message
                        {
                            Kind = Kind.Snapshot,
                            Sequence = frame,
                            Poses = new[]
                            {
                                new Pose
                                {
                                    Id = "enemy",
                                    Qw = 1,
                                    Stamp = frame * .05f,
                                    Dead = frame >= 10,
                                    Flags = frame >= 10 ? 1 : 0,
                                    Health = new[]
                                    {
                                        frame >= 10 ? 0f : 1f
                                    }
                                }
                            }
                        };
                        foreach (var peer in peers)
                            peer.Send(snap);
                        for (int i = 0; i < clients.Count; i++)
                        {
                            var m = Read(clients[i]);
                            if (m.Sequence != frame || m.Poses[0].Dead != (frame >= 10) || m.Poses[0].Health[0] != (frame >= 10 ? 0 : 1))
                                throw new Exception("death state diverged");
                        }
                    }

                    Check(true, "20 ordered snapshots and death reach every guest " + players + "p");
                    clients[0].Dispose();
                    seats.Release(1);
                    sessions[0].Disconnect();
                    latches[0].Clear();
                    for (int i = 1; i < clients.Count; i++)
                    {
                        clients[i].Send(Input(units[i + 1], 3, true, 4));
                        Check(sessions[i].Handle(Read(peers[i])).Kind == Kind.Ping, "remaining guest input after departure " + players + "p guest" + (i + 1));
                    }

                    Check(seats.Reserve(9, units) == "guest1", "departed slot reusable " + players + "p");
                }
                finally
                {
                    foreach (var c in clients)
                        c.Dispose();
                    foreach (var p in peers)
                        p.Dispose();
                }
        }

        static void Main()
        {
            try
            {
                var voicePacket = Input("guest1", 1, false, 0);
                voicePacket.Poses[0].CrewPenetrations = 7;
                voicePacket.Poses[0].PlatoonLeader = true;
                Check(Wire.Decode(Wire.Encode(voicePacket)).Poses[0].CrewPenetrations == 7,
                    "authoritative crew penetration count survives wire roundtrip");
                Check(Wire.Decode(Wire.Encode(voicePacket)).Poses[0].PlatoonLeader,
                    "host leader survives wire roundtrip");
                voicePacket.Poses[0].PlatoonLeader = false;
                Check(!Wire.Decode(Wire.Encode(voicePacket)).Poses[0].PlatoonLeader,
                    "ordinary member survives wire roundtrip");
                Run(3);
                Run(4);
                Console.WriteLine("ALL " + checks + " MULTI SYNC CHECKS PASSED. Loopback protocol fixture, NOT Steam/Unity gameplay.");
            }
            catch (Exception e)
            {
                Console.WriteLine("FAIL " + e.Message);
                Environment.Exit(1);
            }
        }
    }
}

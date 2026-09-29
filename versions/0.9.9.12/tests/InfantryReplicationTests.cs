using System;
using System.IO;
using System.Linq;
using GhpcCoop;

class InfantryReplicationTests
{
    static int count;
    static void Check(bool value, string label)
    {
        if (!value)
            throw new Exception(label);
        count++;
    }

    static Pose Soldier(string id)
    {
        return new Pose
        {
            Id = id,
            Qw = 1,
            Flags = 1,
            X = 123,
            Y = 4,
            Z = 456,
            Health = new[]
            {
                1f,
                .4f
            },
            Ammo = new[]
            {
                new AmmoState
                {
                    Breech = "seat"
                }
            },
            Tracks = new[]
            {
                1f,
                0f,
                2f,
                0f
            }
        };
    }

    static void Reject(Message m)
    {
        bool failed = false;
        try
        {
            Wire.Encode(m);
        }
        catch (InvalidDataException)
        {
            failed = true;
        }

        Check(failed, "malformed state accepted");
    }

    static HostSession Session()
    {
        var s = new HostSession("t", "b", "w", "r", "host", new[] { "guest" });
        s.Handle(new Message { Kind = Kind.Hello, Token = "t", Build = "b", World = "w", Roster = "r" });
        s.Handle(new Message { Kind = Kind.Claim, Unit = "guest" });
        return s;
    }

    static void Main()
    {
        var m = new Message
        {
            Kind = Kind.Infantry,
            Sequence = 7,
            Poses = new[]
            {
                Soldier("a"),
                Soldier("b")
            }
        };
        m.Poses[1].Dead = true;
        m.Poses[1].Ammo[0].Breech = "";
        var copy = Wire.Decode(Wire.Encode(m));
        Check(copy.Sequence == 7 && copy.Poses.Length == 2, "batch");
        Check(copy.Poses[0].X == 123 && copy.Poses[0].Tracks[2] == 2, "motion");
        Check(copy.Poses[1].Dead && copy.Poses[1].Ammo[0].Breech == "", "dismounted death");
        Check(copy.Poses[0].Health[1] == .4f, "health");
        m.Poses[0].Audio[3] = 1;
        Check(Wire.Decode(Wire.Encode(m)).Poses[0].Audio[3] == 1, "host incapacitation preserved");
        m.Poses[0].Audio[3] = 0;
        Check(Wire.Decode(Wire.Encode(m)).Poses[0].Audio[3] == 0, "host recovery preserved");
        m.Poses[0].Audio[3] = .5f;
        Reject(m);
        m.Poses[0].Audio[3] = 0;
        m.Poses = Enumerable.Range(0, 33).Select(i => Soldier(i.ToString())).ToArray();
        Reject(m);
        m.Poses = new[]
        {
            Soldier("a")
        };
        m.Poses[0].Ammo = new AmmoState[0];
        Reject(m);
        m.Poses[0] = Soldier("a");
        m.Poses[0].Flags = 2;
        Reject(m);
        m.Poses[0] = Soldier("a");
        m.Poses[0].X = float.NaN;
        Reject(m);
        m.Poses[0] = Soldier("a");
        m.Poses[0].Health = new float[129];
        Reject(m);
        var s = Session();
        Check(s.Handle(new Message { Kind = Kind.InfantryOrder, Unit = "guest", Sequence = 1 }).Kind == Kind.Ping, "owned deploy");
        Check(s.Handle(new Message { Kind = Kind.InfantryOrder, Unit = "guest", Sequence = 1 }).Kind == Kind.Error, "replayed deploy");
        Check(s.Handle(new Message { Kind = Kind.InfantryOrder, Unit = "host", Sequence = 2 }).Kind == Kind.Error, "unowned deploy");
        Check(s.Handle(new Message { Kind = Kind.InfantryOrder, Unit = "guest", Sequence = 2, Fire = true }).Kind == Kind.Ping, "owned recall");
        var platoon = new Message
        {
            Kind = Kind.InfantryOrder,
            Unit = "guest",
            Sequence = 3,
            Text = "platoon",
            Fire = true
        };
        var platoonCopy = Wire.Decode(Wire.Encode(platoon));
        Check(platoonCopy.Text == "platoon" && platoonCopy.Fire, "platoon recall scope round trip");
        Check(s.Handle(platoonCopy).Kind == Kind.Ping, "platoon order requires owned source");
        Check(s.Handle(new Message { Kind = Kind.InfantryOrder, Unit = "host", Sequence = 4, Text = "platoon" }).Kind == Kind.Error,
            "platoon scope cannot bypass vehicle ownership");
        var pre = new HostSession("t", "b", "w", "r", "host", new[] { "guest" });
        Check(pre.Handle(new Message { Kind = Kind.InfantryOrder, Unit = "guest", Sequence = 1 }).Kind == Kind.Error, "pre handshake");
        var bailOut = new Message
        {
            Kind = Kind.InfantryOrder,
            Unit = "guest",
            Sequence = 5,
            Text = "bail-out"
        };
        Check(s.Handle(Wire.Decode(Wire.Encode(bailOut))).Kind == Kind.Ping, "owned crew evacuation");
        bailOut.Unit = "host";
        bailOut.Sequence = 6;
        Check(s.Handle(bailOut).Kind == Kind.Error, "crew evacuation cannot target another vehicle");
        var aar = new Message
        {
            Kind = Kind.AarView,
            Text = "Host-controlled AAR",
            Poses = new[]
            {
                new Pose
                {
                    Id = "vehicle",
                    Qw = 1,
                    X = 20,
                    Ammo = new[]
                    {
                        new AmmoState
                        {
                            TriggerHeldTime = .75f
                        }
                    }
                }
            },
            Tracers = new[]
            {
                new TracerState
                {
                    Id = 0,
                    Duration = 1,
                    X = 20,
                    EndX = 40
                }
            }
        };
        aar.Tracers[0].Ammo = "BGM-71 test";
        aar.Tracers[0].WirePoints = new float[]
        {
            1,
            2,
            3,
            4,
            5,
            6
        };
        aar.Tracers[0].WireWidth = .01f;
        var aarCopy = Wire.Decode(Wire.Encode(aar));
        Check(aarCopy.Tracers[0].WirePoints.SequenceEqual(aar.Tracers[0].WirePoints) &&
            aarCopy.Tracers[0].WireWidth == .01f, "guidance wire round trip");
        Check(aarCopy.Tracers[0].Ammo == "BGM-71 test", "custom projectile identity round trip");
        Check(aarCopy.Kind == Kind.AarView &&
            aarCopy.Poses[0].X == 20 &&
            aarCopy.Tracers[0].EndX == 40, "shared AAR pose and trajectory round trip");
        Check(aarCopy.Poses[0].Ammo[0].TriggerHeldTime == .75f, "launcher hold progress round trip");
        Check(s.Handle(aar).Kind == Kind.Error, "guest cannot publish authoritative AAR");
        aar.Poses[0].Ammo[0].TriggerHeldTime = float.NaN;
        Reject(aar);
        aar.Poses[0].Ammo[0].TriggerHeldTime = -1;
        Reject(aar);
        aar.Poses[0].Ammo[0].TriggerHeldTime = 0;
        aar.Tracers[0].WirePoints = new float[193];
        Reject(aar);
        aar.Tracers[0].WirePoints = new float[]
        {
            float.NaN,
            0,
            0
        };
        Reject(aar);
        aar.Tracers[0].WirePoints = new float[0];
        aar.Tracers[0].WireWidth = -1;
        Reject(aar);
        Console.WriteLine("PASS " + count + " infantry replication protocol checks; not Unity gameplay");
    }
}

using System;
using System.Collections.Generic;
using GhpcCoop;

class ResyncTests
{
    static int checks;
    static void Check(bool condition, string reason)
    {
        if (!condition) throw new Exception(reason);
        checks++;
        Console.WriteLine("PASS " + reason);
    }
    static void Reject(Action action, string reason)
    {
        try { action(); } catch (InvalidOperationException) { Check(true, reason); return; }
        throw new Exception(reason);
    }
    static Pose Vehicle(string id)
    {
        return new Pose { Id = id, Layout = Wire.Hash(id), Qw = 1, Stamp = 10,
            X = 123, Health = new[] { .25f }, Scorch = new[] { .75f }, Flags = 31, Dead = true,
            WeaponShots = new[] { 5 }, Ammo = new[] { new AmmoState { Reserve = 12 } },
            Debris = new[] { new DebrisState { Detached = true, X = 10, Qw = 1 } },
            Equipment = new EquipmentState { Salvos = new[] { 3 }, Lamps = new[] { true }, Materials = new[] { 2 } } };
    }
    static Message Baseline(long sequence, long revision)
    {
        return new Message { Kind = Kind.ResyncBaseline, Sequence = sequence, SyncRevision = revision,
            Unit = "guest", World = "mission|terrain|True", Poses = new[] { Vehicle("host"), Vehicle("guest") } };
    }
    static Message Ack(Message baseline)
    {
        return Wire.Decode(Wire.Encode(new Message { Kind = Kind.ResyncAck, Sequence = baseline.Sequence,
            SyncRevision = baseline.SyncRevision, Unit = baseline.Unit }));
    }
    static Message Input(Message baseline)
    {
        return new Message { Kind = Kind.Input, Sequence = 500, SyncRevision = baseline.SyncRevision,
            SyncBaseline = baseline.Sequence, Unit = baseline.Unit };
    }
    static void Main()
    {
        var first = Wire.Decode(Wire.Encode(Baseline(10, 1)));
        Check(first.Poses[0].Layout == Wire.Hash("host") && first.SyncRevision == 1, "baseline wire roundtrip");
        Check(first.Poses[0].Health[0] == .25f && first.Poses[0].Scorch[0] == .75f &&
            first.Poses[0].Ammo[0].Reserve == 12 && first.Poses[0].Debris[0].Detached &&
            first.Poses[0].Equipment.Salvos[0] == 3 && first.Poses[0].Flags == 31 && first.Poses[0].X == 123,
            "baseline carries damage ammo debris equipment and position");
        var gate = new ResyncGate();
        Check(!gate.AllowsInput(Input(first)), "uninitialized peer cannot drive");
        gate.Begin(first, 5);
        Check(!gate.AllowsInput(Input(first)), "baseline suspends input");
        var wrongSeat = Ack(first); wrongSeat.Unit = "host";
        Check(!gate.Accept(wrongSeat) && gate.Pending, "ack cannot steal host seat");
        var wrongRevision = Ack(first); wrongRevision.SyncRevision++;
        Check(!gate.Accept(wrongRevision), "wrong revision rejected");
        var second = Baseline(20, 2);
        gate.Begin(second, 6);
        Check(!gate.Accept(Ack(first)) && gate.Pending, "late ack cannot unlock replacement baseline");
        Check(!gate.Expired(21) && gate.Expired(21.01f), "recovery deadline bounded");
        Check(gate.Accept(Ack(second)), "exact ack completes recovery");
        Check(!gate.Accept(Ack(second)), "duplicate ack does not complete twice");
        Check(gate.AllowsInput(Input(second)), "current baseline resumes owned input");
        Check(!gate.AllowsInput(Input(first)), "old roster input rejected");
        var third = Baseline(30, 2);
        gate.Begin(third, 22); gate.Accept(Ack(third));
        Check(!gate.AllowsInput(Input(second)), "old input rejected even when roster revision is unchanged");
        var inputCopy = Wire.Decode(Wire.Encode(Input(third)));
        Check(gate.AllowsInput(inputCopy), "input transaction survives wire roundtrip");
        var otherPeer = new ResyncGate(); otherPeer.Begin(second, 6); otherPeer.Accept(Ack(second));
        gate.Begin(Baseline(40, 2), 23);
        Check(otherPeer.AllowsInput(Input(second)), "one peer recovery does not suspend another peer");

        var local = new Dictionary<string, string> { { "host", Wire.Hash("host") }, { "guest", Wire.Hash("guest") } };
        Func<string, string> lookup = id => local.ContainsKey(id) ? local[id] : null;
        string missing;
        var plan = RosterBaselinePlan.Prepare(first, "guest", lookup, p => true, out missing);
        Check(plan.SetEquals(new[] { "host", "guest" }), "baseline preserves exact host identities");
        local["extra"] = Wire.Hash("extra");
        plan = RosterBaselinePlan.Prepare(first, "guest", lookup, p => true, out missing);
        Check(!plan.Contains("extra") && local.ContainsKey("extra"), "guest-only vehicle excluded without mutating discovery");
        var newVehicle = Baseline(50, 3);
        newVehicle.Poses = new[] { Vehicle("host"), Vehicle("guest"), Vehicle("reinforcement") };
        Check(RosterBaselinePlan.Prepare(newVehicle, "guest", lookup, p => true, out missing) == null && missing == "reinforcement", "late native spawn waits without partial plan");
        local["reinforcement"] = Wire.Hash("reinforcement");
        plan = RosterBaselinePlan.Prepare(newVehicle, "guest", lookup, p => true, out missing);
        Check(plan.Contains("reinforcement"), "late native spawn completes matching plan");
        local["host"] = Wire.Hash("different vehicle");
        Reject(() => RosterBaselinePlan.Prepare(first, "guest", lookup, p => true, out missing), "same ID different topology rejected");
        local["host"] = Wire.Hash("host");
        Reject(() => RosterBaselinePlan.Prepare(first, "guest", lookup, p => p.Id != "guest", out missing), "late component mismatch rejects entire plan");
        Reject(() => RosterBaselinePlan.Prepare(first, "host", lookup, p => true, out missing), "baseline cannot reassign local ownership");
        var missingOwner = Baseline(60, 4); missingOwner.Poses = new[] { Vehicle("host") };
        Reject(() => RosterBaselinePlan.Prepare(missingOwner, "guest", lookup, p => true, out missing), "missing owned vehicle rejected");
        var duplicate = Baseline(70, 4); duplicate.Poses = new[] { Vehicle("guest"), Vehicle("guest") };
        Reject(() => RosterBaselinePlan.Prepare(duplicate, "guest", lookup, p => true, out missing), "duplicate identities rejected");
        var previous = new Dictionary<string, Pose> { { "guest", Vehicle("guest") } };
        Reject(() => SnapshotValidation.Validate(new[] { Vehicle("guest"), Vehicle("extra") }, new[] { "guest" }, previous, p => true), "ordinary snapshot cannot add unapproved identity");
        Check(previous.Count == 1, "rejected snapshot preserves previous state");
        var seats = new RoomSeats("host", new[] { "host", "guest" });
        seats.Reserve(1, new[] { "guest" });
        seats.AddVehicleChoices(new[] { "reinforcement" });
        Check(seats.Vehicle(1) == "guest" && seats.CanMove(1, "reinforcement"), "reinforcement discovery preserves existing seat");
        Check(!seats.CanMove(1, "host"), "expanded roster still protects host ownership");
        Console.WriteLine("PASS " + checks + " resynchronization checks; fixtures, not Unity gameplay");
    }
}

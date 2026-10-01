using System;
using System.Collections.Generic;
using GhpcCoop;

class SnapshotValidationTests
{
    static int checks;
    static void Reject(Action action)
    {
        try { action(); }
        catch (InvalidOperationException) { checks++; return; }
        throw new Exception("Invalid baseline was accepted");
    }

    static void Main()
    {
        var previous = new Dictionary<string, Pose>();
        var ids = new[] { "host", "guest" };
        var host = new Pose { Id = "host", Stamp = 2 };
        var guest = new Pose { Id = "guest", Stamp = 2 };
        SnapshotValidation.Validate(new[] { host, guest }, ids, previous, p => true);
        checks++;
        Reject(() => SnapshotValidation.Validate(new[] { host }, ids, previous, p => true));
        Reject(() => SnapshotValidation.Validate(new[] { host, host }, ids, previous, p => true));
        Reject(() => SnapshotValidation.Validate(new[] { host, guest }, ids, previous, p => p.Id != "guest"));
        if (previous.Count != 0) throw new Exception("Preflight changed replica state");
        checks++;
        previous["guest"] = new Pose { Id = "guest", Stamp = 1, Dead = false };
        Reject(() => SnapshotValidation.Validate(new[] { host }, ids, previous, p => true));
        previous["guest"].Dead = true;
        SnapshotValidation.Validate(new[] { host }, ids, previous, p => true);
        if (!previous["guest"].Dead || previous["guest"].Stamp != 1)
            throw new Exception("Wreck tombstone was modified");
        checks++;
        Reject(() => SnapshotValidation.Validate(null, ids, previous, p => true));
        Console.WriteLine("PASS " + checks + " snapshot preflight checks");
    }
}

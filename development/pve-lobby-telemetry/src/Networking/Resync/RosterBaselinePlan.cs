using System;
using System.Collections.Generic;

namespace GhpcCoop
{
    public static class RosterBaselinePlan
    {
        // Null means native spawning is still pending. A mismatched existing vehicle
        // is an error, never a reason to bind to a nearby or similarly named vehicle.
        public static HashSet<string> Prepare(Message baseline, string ownedUnit,
            Func<string, string> localLayout, Func<Pose, bool> componentCountsMatch, out string missing)
        {
            missing = "";
            if (baseline.Unit != ownedUnit) throw new InvalidOperationException("Baseline changed vehicle ownership");
            var active = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pose in baseline.Poses)
            {
                if (!active.Add(pose.Id)) throw new InvalidOperationException("Duplicate baseline identity");
                var layout = localLayout(pose.Id);
                if (layout == null) { missing = pose.Id; continue; }
                if (layout != pose.Layout || !componentCountsMatch(pose))
                    throw new InvalidOperationException("Resync component layout differs: " + pose.Id);
            }
            if (!active.Contains(ownedUnit)) throw new InvalidOperationException("Assigned vehicle removed from baseline");
            return missing == "" ? active : null;
        }
    }
}

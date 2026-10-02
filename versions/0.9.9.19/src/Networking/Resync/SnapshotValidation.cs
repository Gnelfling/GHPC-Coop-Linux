using System;
using System.Collections.Generic;

namespace GhpcCoop
{
    // Pure preflight: never mutate the replica while deciding whether a baseline fits.
    public static class SnapshotValidation
    {
        public static void Validate(Pose[] poses, IEnumerable<string> knownIds,
            IDictionary<string, Pose> previous, Func<Pose, bool> matchesLayout)
        {
            if (poses == null) throw new InvalidOperationException("Missing vehicle baseline");
            var known = new HashSet<string>(knownIds, StringComparer.Ordinal);
            var received = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pose in poses)
            {
                if (pose == null || String.IsNullOrEmpty(pose.Id) || !received.Add(pose.Id))
                    throw new InvalidOperationException("Invalid or duplicate vehicle identity");
                if (!known.Contains(pose.Id) || !matchesLayout(pose))
                    throw new InvalidOperationException("Vehicle layout changed: " + pose.Id);
            }
            foreach (var id in known)
            {
                if (received.Contains(id)) continue;
                Pose last;
                // Absence is only safe after the host explicitly reported destruction.
                // This also checks the first baseline, when previous is still empty.
                if (!previous.TryGetValue(id, out last) || !last.Dead)
                    throw new InvalidOperationException("Live vehicle disappeared; roster recovery required: " + id);
            }
        }
    }
}

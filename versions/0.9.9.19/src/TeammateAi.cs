using System;
using System.Collections.Generic;
using GHPC;
using GHPC.AI;
using GHPC.Crew;
using GHPC.Player;

namespace GhpcCoop
{
    // Host-only policy for unoccupied friendly vehicles. Never disable a whole
    // vehicle: it must remain visible, damageable and available for player transfer.
    static class TeammateAi
    {
        static readonly Dictionary<Unit, Action> paused = new Dictionary<Unit, Action>();
        static readonly List<Unit> releases = new List<Unit>();
        static readonly CrewPosition[] positions = { CrewPosition.Driver, CrewPosition.Gunner, CrewPosition.Commander };

        public static bool Blocked(Unit unit)
        {
            if (unit == null || !paused.ContainsKey(unit)) return false;
            // Vehicle transfers can happen between mod updates. Restore native crew
            // state before the new owner configures it, rather than one frame later.
            if (GameBridge.IsRemote(unit) ||
                (PlayerInput.Instance != null && PlayerInput.Instance.CurrentPlayerUnit == unit))
            {
                Release(unit);
                return false;
            }
            return true;
        }

        public static void Release(Unit unit)
        {
            Action restore;
            if (ReferenceEquals(unit, null) || !paused.TryGetValue(unit, out restore)) return;
            paused.Remove(unit);
            if (unit != null) restore();
        }

        public static void Reset()
        {
            releases.Clear();
            releases.AddRange(paused.Keys);
            foreach (var unit in releases) Release(unit);
            releases.Clear();
        }

        public static void Apply(GameBridge game, bool enabled)
        {
            if (enabled) { if (paused.Count != 0) Reset(); return; }
            VehicleRecord host;
            if (game.LocalId == null || !game.Vehicles.TryGetValue(game.LocalId, out host) || host.Unit == null) return;
            var local = PlayerInput.Instance != null ? PlayerInput.Instance.CurrentPlayerUnit : host.Unit;
            releases.Clear();
            foreach (var unit in paused.Keys)
                if (unit == null || unit == local || unit == host.Unit || GameBridge.IsRemote(unit) || unit.Allegiance != host.Unit.Allegiance)
                    releases.Add(unit);
            foreach (var unit in releases) Release(unit);

            foreach (var record in game.Vehicles.Values)
            {
                var unit = record.Unit;
                if (unit == null || unit == local || unit == host.Unit || GameBridge.IsRemote(unit) ||
                    unit.Allegiance != host.Unit.Allegiance || unit.CrewManager == null || paused.ContainsKey(unit)) continue;
                var undo = new List<Action>();
                foreach (var position in positions)
                {
                    var brain = unit.CrewManager.GetCrewBrain(position);
                    if (brain == null) continue;
                    bool autonomous = brain.IsAutonomous, suspended = brain.Suspended;
                    var driver = brain as IDriverBrain;
                    if (driver != null) { driver.ForceSpeed(0, false); driver.ForceSteerAmount(0); }
                    brain.IsAutonomous = false;
                    brain.Suspended = true;
                    undo.Add(() => { brain.IsAutonomous = autonomous; brain.Suspended = suspended; });
                }
                paused.Add(unit, () => { foreach (var restore in undo) SafeCleanup.Run("teammate AI crew", restore, GameBridge.Log); });
            }
        }
    }
}

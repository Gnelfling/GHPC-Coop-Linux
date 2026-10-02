using GHPC;
using GHPC.AI.Platoons;
using HarmonyLib;

namespace GhpcCoop
{
    // Selecting a tank locally must not promote it independently on each computer.
    // The host runs leadership changes; guests apply the native setter only on change
    // so that its waypoint subscriptions and the vehicle-selection label agree.
    static class PlatoonSync
    {
        static readonly System.Reflection.MethodInfo SetLeader =
            AccessTools.Method(typeof(PlatoonData), "SetPlatoonLeader");
        internal static bool Applying;

        public static void Apply(Unit unit, bool isLeader)
        {
            if (!isLeader || unit == null || unit.Platoon == null ||
                unit.Platoon.PlatoonLeader == null || unit.Platoon.PlatoonLeaderUnit == unit)
                return;
            Applying = true;
            try { SetLeader.Invoke(unit.Platoon, new object[] { unit }); }
            finally { Applying = false; }
        }
    }

    [HarmonyPatch(typeof(PlatoonData), "SetPlatoonLeader")]
    static class ReplicaPlatoonLeader
    {
        static bool Prefix()
        {
            return !GameBridge.ReplicaActive || PlatoonSync.Applying;
        }
    }
}

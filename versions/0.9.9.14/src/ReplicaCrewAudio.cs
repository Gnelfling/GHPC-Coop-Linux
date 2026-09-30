using System.Collections.Generic;
using GHPC;
using GHPC.Effects.Voices;
using GHPC.Weapons;
using HarmonyLib;

namespace GhpcCoop
{
    public static class ReplicaCrewAudio
    {
        static readonly Dictionary<Unit, CrewVoiceHandler> Voices = new Dictionary<Unit, CrewVoiceHandler>();
        static readonly HashSet<Unit> Ready = new HashSet<Unit>();
        static Unit selected;
        public static void Ensure(Unit unit, bool local)
        {
            if (!local)
                return;
            if (selected != unit)
            {
                selected = unit;
                Ready.Remove(unit);
            }

            if (Ready.Contains(unit))
                return;
            CrewVoiceHandler v;
            if (!Voices.TryGetValue(unit, out v))
            {
                v = unit.GetComponentInChildren<CrewVoiceHandler>(true);
                Voices[unit] = v;
            }

            if (v == null ||
                !v.IsInitialized ||
                !v.isActiveAndEnabled ||
                !v.IsCurrentUnit ||
                (!v.CommanderConscious &&
                !v.GunnerConscious &&
                !v.LoaderConcsious))
                return;
            // Selection can happen before voice initialization during an automatic join.
            // Unsubscribe before waking, so a normal native selection cannot double-register events.
            v.GoToSleep();
            v.SetUnitPlayer(true);
            Ready.Add(unit);
            GameBridge.Log("AUDIO crew ready " + unit.FriendlyName);
        }

        public static void Slept(CrewVoiceHandler voice)
        {
            // Native selection/death callbacks can put a handler to sleep after
            // initialization. A stale Ready entry must not suppress recovery.
            foreach (var pair in Voices)
                if (pair.Value == voice)
                    Ready.Remove(pair.Key);
        }

        public static void Shot(Unit unit, WeaponSystem w)
        {
            if (unit != selected || w.Feed == null || !w.Feed.AnnounceReloads)
                return;
            CrewVoiceHandler v;
            if (!Voices.TryGetValue(unit, out v) || v == null)
                return;
            var a = AmmoSync.LastAmmo(w.Feed);
            if (a != null)
                AccessTools.Field(typeof(CrewVoiceHandler), "_lastFiredRound").SetValue(v, a.ShortName);
            AccessTools.Field(typeof(CrewVoiceHandler), "_weaponFiredRecently").SetValue(v, true);
        // No artificial LiveRound: impact simulation and damage remain host-owned.
        }

        public static void Clear()
        {
            Voices.Clear();
            Ready.Clear();
            selected = null;
        }
    }

    [HarmonyPatch(typeof(CrewVoiceHandler), "GoToSleep")]
    static class ReplicaCrewSleep
    {
        static void Postfix(CrewVoiceHandler __instance)
        {
            if (GameBridge.ReplicaActive)
                ReplicaCrewAudio.Slept(__instance);
        }
    }
}

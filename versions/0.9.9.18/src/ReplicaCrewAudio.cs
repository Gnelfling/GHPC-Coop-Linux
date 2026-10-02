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
        static readonly System.Reflection.FieldInfo LastFiredRound = AccessTools.Field(typeof(CrewVoiceHandler), "_lastFiredRound");
        static readonly System.Reflection.FieldInfo WeaponFiredRecently = AccessTools.Field(typeof(CrewVoiceHandler), "_weaponFiredRecently");
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
            // A handler may be created after vehicle selection or replaced during scene loading.
            // Do not permanently cache a missing Unity component.
            if (!Voices.TryGetValue(unit, out v) || v == null)
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
                LastFiredRound.SetValue(v, a.ShortName);
            WeaponFiredRecently.SetValue(v, true);
            // No artificial LiveRound: impact simulation and damage remain host-owned.
        }

        static readonly Dictionary<Unit, int> Penetrations = new Dictionary<Unit, int>();
        static readonly Dictionary<Unit, int> SeenPenetrations = new Dictionary<Unit, int>();
        static readonly System.Reflection.MethodInfo PenetrationVoice = AccessTools.Method(typeof(CrewVoiceHandler), "CrewCompartmentPenetratedEvent");

        public static int CapturePenetrations(Unit unit)
        {
            int count;
            return Penetrations.TryGetValue(unit, out count) ? count : 0;
        }

        public static void RecordPenetration(Unit unit)
        {
            if (GameBridge.ReplicaActive || unit == null)
                return;
            int count = CapturePenetrations(unit);
            if (count < int.MaxValue) Penetrations[unit] = count + 1;
        }

        public static void ApplyPenetrations(Unit unit, int count, bool local)
        {
            int previous;
            bool known = SeenPenetrations.TryGetValue(unit, out previous);
            SeenPenetrations[unit] = count;
            // The first snapshot is history, not a new hit. Remote tanks must stay silent.
            if (!known || !local || count <= previous || GHPC.AarController.InAar)
                return;
            Ensure(unit, true);
            CrewVoiceHandler voice;
            if (Voices.TryGetValue(unit, out voice) && voice != null && Ready.Contains(unit))
            {
                // Native handler only chooses the penetration voice line; it does not use ammo.
                // Never simulate a second impact or apply damage on the guest.
                PenetrationVoice.Invoke(voice, new object[] { null });
                GameBridge.Log("VOICE replicated penetration count=" + count);
            }
        }

        public static void Clear()
        {
            Penetrations.Clear();
            SeenPenetrations.Clear();
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
    // Log actual native voice requests, without forcing playback or bypassing dead crew checks.
    [HarmonyPatch(typeof(CrewVoiceHandler), "PlayVoiceLine")]
    static class ReplicaVoiceRequestDiagnostic
    {
        static float nextLog;
        static void Postfix(CrewVoiceHandler __instance, VoiceLineReceipt __result)
        {
            if (!GameBridge.ReplicaActive || !__instance.IsCurrentUnit || UnityEngine.Time.realtimeSinceStartup < nextLog)
                return;
            nextLog = UnityEngine.Time.realtimeSinceStartup + 2f;
            GameBridge.Log("VOICE request initialized=" + __instance.IsInitialized + " accepted=" + (__result != null) + " aar=" + GHPC.AarController.InAar);
        }
    }

    [HarmonyPatch(typeof(UnitInfoBroker), "NotifyCrewCompartmentPenetrated")]
    static class CaptureCrewPenetration
    {
        static void Postfix(UnitInfoBroker __instance)
        {
            ReplicaCrewAudio.RecordPenetration(__instance.Unit);
        }
    }
}

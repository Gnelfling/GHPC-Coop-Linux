using System;
using System.Collections.Generic;
using System.Linq;
using GHPC;
using GHPC.Vehicle;
using UnityEngine;
using HarmonyLib;

namespace GhpcCoop
{
    // Separate aircraft channel: aircraft never become player vehicle seats.
    public sealed class HelicopterSync : IDisposable
    {
        sealed class Entry
        {
            public Unit Unit;
            public HelicopterController Controller;
            public DamageSync Damage;
            public Pose Latest;
            public bool Held;
            public bool CrashShown;
            public readonly List<Action> Restore = new List<Action>();
        }
        readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
        readonly HashSet<string> warned = new HashSet<string>();
        internal static readonly HashSet<HelicopterController> Held = new HashSet<HelicopterController>();
        internal static readonly Dictionary<HelicopterController, Vector3> CrashPoints = new Dictionary<HelicopterController, Vector3>();
        static readonly System.Reflection.FieldInfo Rpm = AccessTools.Field(typeof(HelicopterController), "_rpm");
        static readonly System.Reflection.FieldInfo Crashed = AccessTools.Field(typeof(HelicopterController), "_crashed");
        static readonly System.Reflection.MethodInfo CrashEffects = AccessTools.Method(typeof(HelicopterController), "DoCrashEffects");
        readonly CombatVisuals effects = new CombatVisuals();
        float nextScan;
        float nextDiagnostic;

        void Scan()
        {
            if (Time.realtimeSinceStartup < nextScan) return;
            nextScan = Time.realtimeSinceStartup + 1;
            var found = new Dictionary<string, Entry>();
            var ambiguous = new HashSet<string>();
            foreach (var controller in Resources.FindObjectsOfTypeAll<HelicopterController>())
            {
                var unit = controller.GetComponentInParent<Unit>();
                if (unit == null || !unit.gameObject.scene.IsValid() || !unit.gameObject.scene.isLoaded) continue;
                var damage = new DamageSync(unit);
                string id = Wire.Hash(unit.gameObject.scene.name + "|" + GameBridge.PathOf(unit.transform)
                    + "|" + unit.UniqueName + "|" + unit.Allegiance + "|" + damage.Layout);
                if (found.ContainsKey(id)) { ambiguous.Add(id); continue; }
                Entry entry;
                if (!entries.TryGetValue(id, out entry) || entry.Unit != unit)
                    entry = new Entry { Unit = unit, Controller = controller, Damage = damage };
                found.Add(id, entry);
            }
            foreach (string id in ambiguous) found.Remove(id);
            foreach (var pair in entries)
                if (!found.ContainsKey(pair.Key)) Release(pair.Value);
            entries.Clear();
            foreach (var pair in found) entries.Add(pair.Key, pair.Value);
        }

        public Message Capture(long sequence)
        {
            Scan();
            var poses = new List<Pose>();
            foreach (var pair in entries)
            {
                var u = pair.Value.Unit;
                if (u == null) continue;
                var t = u.RootTransform;
                var q = t.rotation; var p = t.position;
                Vector3 crashPoint;
                bool hasCrash = CrashPoints.TryGetValue(pair.Value.Controller, out crashPoint);
                poses.Add(new Pose { Id = pair.Key, X = p.x, Y = p.y, Z = p.z,
                    Qx = q.x, Qy = q.y, Qz = q.z, Qw = q.w,
                    Stamp = Time.realtimeSinceStartup, Dead = u.Destroyed,
                    Flags = (u.Destroyed ? 1 : 0) | (u.Abandoned ? 2 : 0) |
                        (u.CannotMove ? 4 : 0) | (u.CannotShoot ? 8 : 0) | (u.UnitIncapacitated ? 16 : 0),
                    Audio = new [] { Mathf.Max(0, (float)Rpm.GetValue(pair.Value.Controller)),
                        (bool)Crashed.GetValue(pair.Value.Controller) ? 1f : 0f,
                        u.gameObject.activeSelf ? 1f : 0f, 0f, 0f, 0f, 0f, 0f },
                    // Track channels carry crash-point coordinates for this message kind.
                    Tracks = new [] { crashPoint.x, crashPoint.y, crashPoint.z, hasCrash ? 1f : 0f },
                    Fires = effects.Capture(u),
                    Health = pair.Value.Damage.Capture(), Scorch = pair.Value.Damage.CaptureScorch() });
            }
            var message = new Message { Kind = Kind.Helicopters, Sequence = sequence, Poses = poses.ToArray() };
            Diagnose("host", message);
            return message;
        }

        public void Apply(Message message)
        {
            if (!GameBridge.ReplicaActive) return;
            Scan();
            foreach (var pose in message.Poses)
            {
                Entry entry;
                if (!entries.TryGetValue(pose.Id, out entry))
                {
                    if (warned.Add(pose.Id)) GameBridge.Log("HELICOPTER unmatched identity; aircraft not synchronized: " + pose.Id);
                    continue;
                }
                if (entry.Latest != null && pose.Stamp <= entry.Latest.Stamp) continue;
                if (!entry.Held)
                {
                    entry.Held = true;
                    Held.Add(entry.Controller);
                    bool active = entry.Unit.gameObject.activeSelf;
                    var originalUnit = entry.Unit;
                    entry.Restore.Add(() => { if(originalUnit != null) originalUnit.gameObject.SetActive(active); });
                    foreach (var behavior in entry.Unit.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if (!(behavior is GHPC.AI.HelicopterAIController)) continue;
                        var captured = behavior; bool enabled = behavior.enabled;
                        entry.Restore.Add(() => { if (captured != null) captured.enabled = enabled; });
                        behavior.enabled = false;
                    }
                    foreach (var body in entry.Unit.GetComponentsInChildren<Rigidbody>(true))
                    {
                        var captured = body; bool kinematic = body.isKinematic;
                        entry.Restore.Add(() => { if (captured != null) captured.isKinematic = kinematic; });
                        body.isKinematic = true;
                    }
                }
                entry.Latest = pose;
                entry.Damage.Apply(entry.Unit, pose);
                effects.Apply(pose.Id, entry.Unit, pose.Fires);
                if (pose.Audio[1] > 0 && !entry.CrashShown && pose.Tracks[3] > 0)
                {
                    entry.CrashShown = true;
                    CrashEffects.Invoke(entry.Controller, new object[] { new Vector3(pose.Tracks[0], pose.Tracks[1], pose.Tracks[2]) });
                    GameBridge.Log("HELICOPTER crash replicated id=" + pose.Id);
                }
                entry.Unit.gameObject.SetActive(pose.Audio[2] > 0);
            }
            var present = new HashSet<string>(message.Poses.Select(p => p.Id));
            foreach(var pair in entries)
                if(pair.Value.Held && !present.Contains(pair.Key) && pair.Value.Unit != null)
                    pair.Value.Unit.gameObject.SetActive(false);
            Render();
            Diagnose("guest", message);
        }

        void Diagnose(string role, Message message)
        {
            if(Time.realtimeSinceStartup < nextDiagnostic) return;
            nextDiagnostic = Time.realtimeSinceStartup + 5;
            foreach(var p in message.Poses)
                GameBridge.Log("HELICOPTER " + role + " seq=" + message.Sequence + " id=" + p.Id +
                    " pos=" + new Vector3(p.X,p.Y,p.Z).ToString("F2") + " flags=" + p.Flags +
                    " crashed=" + p.Audio[1] + " active=" + p.Audio[2] + " matched=" + entries.ContainsKey(p.Id));
        }

        public void Render()
        {
            foreach (var entry in entries.Values)
            {
                var p = entry.Latest;
                if (p == null || entry.Unit == null) continue;
                entry.Unit.RootTransform.SetPositionAndRotation(new Vector3(p.X, p.Y, p.Z), new Quaternion(p.Qx, p.Qy, p.Qz, p.Qw));
                Rpm.SetValue(entry.Controller, p.Audio[0]);
            }
        }
        static void Release(Entry entry)
        {
            Held.Remove(entry.Controller);
            for (int i = entry.Restore.Count - 1; i >= 0; i--) entry.Restore[i]();
            entry.Restore.Clear(); entry.Held = false; entry.Latest = null;
        }
        public void Dispose()
        {
            foreach (var entry in entries.Values) Release(entry);
            entries.Clear(); warned.Clear(); nextScan = 0; effects.Dispose(); CrashPoints.Clear();
        }
    }

    [HarmonyPatch(typeof(GHPC.AI.HelicopterAIController), "UpdateAI")]
    static class ReplicaAircraftAI
    {
        static bool Prefix(GHPC.AI.HelicopterAIController __instance)
        {
            return !GameBridge.ReplicaActive || !HelicopterSync.Held.Contains(__instance.GetComponent<HelicopterController>());
        }
    }
    [HarmonyPatch(typeof(HelicopterController), "FixedUpdate")]
    static class ReplicaAircraftPhysics
    {
        static bool Prefix(HelicopterController __instance) { return !HelicopterSync.Held.Contains(__instance); }
    }
    [HarmonyPatch(typeof(HelicopterController), "HandleTerrainStruck")]
    static class AircraftCrashState
    {
        static bool Prefix(HelicopterController __instance, Vector3 __0)
        {
            if(HelicopterSync.Held.Contains(__instance)) return false;
            return true;
        }
        static void Postfix(HelicopterController __instance, Vector3 __0, bool ____crashed)
        {
            if(!GameBridge.ReplicaActive && ____crashed) HelicopterSync.CrashPoints[__instance] = __0;
        }
    }
}

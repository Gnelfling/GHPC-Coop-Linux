using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using GHPC;
using GHPC.AI;
using GHPC.AI.Squads;
using GHPC.Infantry;
using GHPC.Player;
using HarmonyLib;
using UnityEngine;

namespace GhpcCoop
{
    // Identities are captured at initialization, before formation changes or reparenting.
    public sealed class InfantrySync : IDisposable
    {
        sealed class Identity
        {
            public string Key;
        }

        static readonly ConditionalWeakTable<InfantryUnit, Identity> identities = new ConditionalWeakTable<InfantryUnit, Identity>();
        public static void Register(InfantryUnit unit, SquadData squad, int index)
        {
            if (unit == null || squad == null || identities.TryGetValue(unit, out var existing))
                return;
            identities.Add(unit, new Identity { Key = unit.gameObject.scene.name + "|" + GameBridge.PathOf(squad.transform) + "|member:" + index });
        }

        sealed class Entry
        {
            public InfantryUnit Unit;
            public string Id;
            public GHPC.Equipment.DestructibleComponent[] Parts;
            public Pose State;
            public readonly InfantryMotion Motion = new InfantryMotion();
            public Quaternion FromRotation, ToRotation;
            public long Sequence = -1;
        }

        readonly Dictionary<string, Entry> units = new Dictionary<string, Entry>();
        readonly Dictionary<InfantryUnit, Entry> cached = new Dictionary<InfantryUnit, Entry>();
        readonly Dictionary<string, InfantryEmplacement> seats = new Dictionary<string, InfantryEmplacement>();
        readonly HashSet<SquadData> heldSquads = new HashSet<SquadData>();
        public bool Owns(InfantryUnit unit)
        {
            Entry entry;
            return GameBridge.ReplicaActive && unit != null && cached.TryGetValue(unit, out entry) && entry.State != null;
        }

        public bool Owns(SquadData squad)
        {
            return GameBridge.ReplicaActive && squad != null && heldSquads.Contains(squad);
        }

        readonly HashSet<string> warned = new HashSet<string>();
        readonly Queue<Message> requests = new Queue<Message>();
        float nextScan, nextDiagnostic;
        float nextAnimationDiagnostic;
        long sequence;
        public static InfantrySync Current;
        public InfantrySync()
        {
            Current = this;
        }

        readonly Dictionary<InfantryEmplacement, string> seatKeys = new Dictionary<InfantryEmplacement, string>();
        string SeatKey(InfantryEmplacement seat)
        {
            string key;
            return seat == null ? "" : seatKeys.TryGetValue(seat, out key) ? key : Wire.Hash("unresolved-seat");
        }

        static string Relative(Transform node, Transform root)
        {
            var names = new List<string>();
            while (node != null && node != root)
            {
                names.Add(node.name + "#" + node.GetSiblingIndex());
                node = node.parent;
            }

            names.Reverse();
            return String.Join("/", names.ToArray());
        }

        void Refresh()
        {
            if (Time.realtimeSinceStartup < nextScan)
                return;
            nextScan = Time.realtimeSinceStartup + 1;
            units.Clear();
            seats.Clear();
            seatKeys.Clear();
            var vehicleOrigins = new Dictionary<Unit, string>();
            // Runtime squad clones can share their initialization path. Use the authored
            // spawn point and its original member slot, as vehicle replication already does.
            foreach (var point in Resources.FindObjectsOfTypeAll<GHPC.Mission.DynamicSpawnPoint>())
            {
                if (!point.gameObject.scene.IsValid() || !point.gameObject.scene.isLoaded || point.SpawnedUnits == null)
                    continue;
                for (int slot = 0; slot < point.SpawnedUnits.Count; slot++)
                {
                    var spawned = point.SpawnedUnits[slot];
                    if (spawned != null && spawned.Chassis != null)
                        vehicleOrigins[spawned] = "spawn/" + point.gameObject.scene.name + "/" + GameBridge.PathOf(point.transform) + "/vehicle/" + slot;
                    var soldier = spawned as InfantryUnit;
                    if (soldier == null)
                        continue;
                    Identity identity;
                    string key = "spawn/" + point.gameObject.scene.name + "/" + GameBridge.PathOf(point.transform) + "/member/" + slot;
                    if (identities.TryGetValue(soldier, out identity))
                    {
                        if (!identity.Key.StartsWith("spawn/", StringComparison.Ordinal))
                            identity.Key = key;
                        else if (identity.Key != key)
                        {
                            Warn("conflicting spawn membership " + key);
                            continue;
                        }
                    }
                    else
                        identities.Add(soldier, new Identity { Key = key });
                }
            }

            var duplicate = new HashSet<string>();
            foreach (var unit in Resources.FindObjectsOfTypeAll<InfantryUnit>())
            {
                if (!unit.gameObject.scene.IsValid() ||
                    !unit.gameObject.scene.isLoaded ||
                    !unit.SquadInitialized ||
                    unit.Squad == null ||
                    unit.Human == null)
                    continue;
                Identity identity;
                // Never guess an initialization identity from a mutable formation index.
                if (!identities.TryGetValue(unit, out identity))
                {
                    Warn("missing initialization identity: " + unit.UniqueName);
                    continue;
                }

                Entry entry;
                if (!cached.TryGetValue(unit, out entry))
                {
                    var parts = unit.GetComponentsInChildren<GHPC.Equipment.DestructibleComponent>(true).OrderBy(p => Relative(p.transform, unit.transform), StringComparer.Ordinal).ThenBy(p => p.GetType().FullName, StringComparer.Ordinal).ToArray();
                    if (parts.Length > 128)
                        continue;
                    entry = new Entry
                    {
                        Unit = unit,
                        Parts = parts
                    };
                    cached.Add(unit, entry);
                }

                if (entry.Id == null)
                {
                    string layout = String.Join(";", entry.Parts.Select(p => p == null ? "missing" : Relative(p.transform, unit.transform) + ":" + p.GetType().FullName).ToArray());
                    entry.Id = Wire.Hash(identity.Key + "|" + unit.Allegiance + "|" + unit.UniqueName + "|" + layout);
                }

                string id = entry.Id;
                if (units.ContainsKey(id))
                    duplicate.Add(id);
                else
                    units.Add(id, entry);
            }

            foreach (var id in duplicate)
            {
                units.Remove(id);
                Warn("ambiguous identity " + id);
            }

            foreach (var unit in cached.Keys.Where(u => u == null).ToArray())
                cached.Remove(unit);
            foreach (var seat in Resources.FindObjectsOfTypeAll<InfantryEmplacement>())
            {
                if (!seat.gameObject.scene.IsValid() || !seat.gameObject.scene.isLoaded)
                    continue;
                var holder = seat.EmplacementHolder;
                var owner = holder != null &&
                    holder.AttachedVehicleInfoBroker != null ? holder.AttachedVehicleInfoBroker.Unit : seat.GetComponentInParent<Unit>();
                string origin;
                string key = Wire.Hash(owner != null &&
                    vehicleOrigins.TryGetValue(owner, out origin) ? origin + "/seat/" + Relative(seat.transform, holder.transform) + "/index/" + Array.IndexOf(holder.Emplacements, seat) : seat.gameObject.scene.name + "|" + GameBridge.PathOf(seat.transform));
                seatKeys[seat] = key;
                if (seats.ContainsKey(key))
                    seats[key] = null;
                else
                    seats.Add(key, seat);
            }
        }

        void Warn(string text)
        {
            if (warned.Count < 64 && warned.Add(text))
                GameBridge.Log("INFANTRY " + text);
        }

        public void ResendAll()
        {
            nextScan = 0;
        }

        public IEnumerable<Message> Capture()
        {
            Refresh();
            var batch = new List<Pose>(32);
            foreach (var pair in units)
            {
                var e = pair.Value;
                var u = e.Unit;
                if (u == null || u.Human == null)
                    continue;
                var p = u.transform.position;
                var q = u.transform.rotation;
                var v = u.Velocity;
                var animation = u.AI == null ? null : u.AI.Animator;
                batch.Add(new Pose { Id = pair.Key,
                    X = p.x,
                    Y = p.y,
                    Z = p.z,
                    Qx = q.x,
                    Qy = q.y,
                    Qz = q.z,
                    Qw = q.w,
                    Audio = new[] { animation == null ? 0f : (float)animation.CurrentStance, v.magnitude, u.IsSeated ? 1f : 0f, u.UnitIncapacitated ? 1f : 0f, 0f, 0f, 0f, 0f },
                    Dead = u.Human.IsDead,
                    Flags = u.gameObject.activeSelf ? 1 : 0,
                    Health = e.Parts.Select(part => part == null ? 0 : InfantryHealth.ToWire(part.HealthPercent)).ToArray(),
                    Ammo = new[] { new AmmoState { Breech = SeatKey(u.Emplacement) } },
                    Tracks = new[] { v.x, v.y, v.z, 0f } });
                if (batch.Count == 32)
                {
                    yield return new Message
                    {
                        Kind = Kind.Infantry,
                        Sequence = ++sequence,
                        Poses = batch.ToArray()
                    };
                    batch.Clear();
                }
            }

            if (batch.Count != 0)
                yield return new Message
                {
                    Kind = Kind.Infantry,
                    Sequence = ++sequence,
                    Poses = batch.ToArray()
                };
            if (Time.realtimeSinceStartup >= nextDiagnostic)
            {
                nextDiagnostic = Time.realtimeSinceStartup + 5;
                GameBridge.Log("INFANTRY host tracked=" + units.Count + " sequence=" + sequence);
            }
        }

        public void Apply(Message message)
        {
            if (!GameBridge.ReplicaActive)
                return;
            Refresh();
            int matched = 0;
            foreach (var pose in message.Poses)
            {
                Entry entry;
                if (!units.TryGetValue(pose.Id, out entry))
                {
                    Warn("unmatched identity " + pose.Id);
                    continue;
                }

                if (message.Sequence <= entry.Sequence || entry.Unit == null)
                    continue;
                if (pose.Health.Length != entry.Parts.Length)
                {
                    Warn("health layout mismatch " + pose.Id);
                    continue;
                }

                InfantryEmplacement resolvedSeat;
                if (pose.Ammo[0].Breech.Length != 0 && (!seats.TryGetValue(pose.Ammo[0].Breech, out resolvedSeat) || resolvedSeat == null))
                {
                    Warn("unmatched seat " + pose.Ammo[0].Breech);
                    continue;
                }

                var old = entry.State;
                bool transition = old == null || old.Ammo[0].Breech != pose.Ammo[0].Breech || old.Flags != pose.Flags || pose.Dead;
                var targetRotation = new Quaternion(pose.Qx, pose.Qy, pose.Qz, pose.Qw);
                bool snap = entry.Motion.Receive(pose.X, pose.Y, pose.Z, Time.realtimeSinceStartup, transition);
                entry.FromRotation = snap ? targetRotation : entry.Unit.transform.rotation;
                entry.ToRotation = targetRotation;
                entry.Sequence = message.Sequence;
                entry.State = pose;
                if (heldSquads.Add(entry.Unit.Squad))
                    entry.Unit.Squad.StopAllCoroutines();
                try
                {
                    ApplyEntry(entry, true);
                    matched++;
                }
                catch (Exception error)
                {
                    Warn("apply " + pose.Id + ": " + error.Message);
                }
            }

            if (Time.realtimeSinceStartup >= nextDiagnostic)
            {
                nextDiagnostic = Time.realtimeSinceStartup + 5;
                GameBridge.Log("INFANTRY guest matched=" + matched + "/" + message.Poses.Length + " local=" + units.Count + " sequence=" + message.Sequence);
            }
        }

        void ApplyEntry(Entry entry, bool health)
        {
            var u = entry.Unit;
            var p = entry.State;
            if (u == null || p == null || u.Human == null)
                return;
            string seatId = p.Ammo[0].Breech;
            InfantryEmplacement target = null;
            if (seatId.Length != 0 && (!seats.TryGetValue(seatId, out target) || target == null))
            {
                Warn("unmatched seat " + seatId);
                return;
            }

            if (u.Emplacement != target)
            {
                if (u.Emplacement != null)
                    u.Emplacement.UnassignUnit();
                if (target != null)
                    target.AssignUnit(u);
            }

            if (u.gameObject.activeSelf != (p.Flags == 1))
                u.gameObject.SetActive(p.Flags == 1);
            // Native SetSpeed refuses movement for incapacitated units. This flag
            // must follow the host, including clearing a stale local incapacitation.
            AccessTools.Field(typeof(Unit), "<UnitIncapacitated>k__BackingField")
                .SetValue(u, p.Dead || p.Audio[3] == 1f);
            // Keep dead ragdolls local; authoritative death is still applied below.
            if (!u.Human.IsDead)
            {
                entry.Motion.Render(Time.realtimeSinceStartup);
                // A seated unit follows its local vehicle seat, not a delayed world pose.
                if (target == null)
                    u.transform.SetPositionAndRotation(new Vector3(entry.Motion.X, entry.Motion.Y, entry.Motion.Z), Quaternion.Slerp(entry.FromRotation, entry.ToRotation, entry.Motion.Blend));
                var animation = u.AI == null ? null : u.AI.Animator;
                if (animation != null)
                {
                    if (animation.IsSeated != (p.Audio[2] == 1))
                        animation.SetSitting(p.Audio[2] == 1);
                    if (animation.CurrentStance != (IdleStance)(int)p.Audio[0])
                        animation.SetStance((IdleStance)(int)p.Audio[0], 0f, true);
                    animation.SetSpeed(p.Audio[1]);
                }

                var movement = u.AI == null ? null : u.AI.MovementController;
                if (movement != null && movement.Data != null)
                {
                    movement.Data.Velocity = new Vector3(p.Tracks[0], p.Tracks[1], p.Tracks[2]);
                    movement.Data.CurrentSpeed = movement.Data.Velocity.magnitude;
                }
            }

            if (!health)
                return;
            bool oldHealth = DamageSync.ApplyingHealth, oldFlags = DamageSync.ApplyingFlags;
            DamageSync.ApplyingHealth = DamageSync.ApplyingFlags = true;
            try
            {
                for (int i = 0; i < p.Health.Length; i++)
                    if (entry.Parts[i] != null && Mathf.Abs(InfantryHealth.ToWire(entry.Parts[i].HealthPercent) - p.Health[i]) > .0001f)
                        entry.Parts[i].SetHealthPercent(InfantryHealth.ToNative(p.Health[i]));
                if (p.Dead && !u.Human.IsDead)
                    u.Human.Kill();
            }
            finally
            {
                DamageSync.ApplyingHealth = oldHealth;
                DamageSync.ApplyingFlags = oldFlags;
            }
        }

        public void Render()
        {
            if (!GameBridge.ReplicaActive)
                return;
            if (Time.realtimeSinceStartup >= nextAnimationDiagnostic)
            {
                nextAnimationDiagnostic = Time.realtimeSinceStartup + 5;
                try
                {
                foreach (var sample in units.Values.Where(e => e.State != null && !e.State.Dead && e.State.Audio[2] == 0).Take(8))
                {
                    var unit = sample.Unit;
                    var animation = unit == null || unit.AI == null ? null : unit.AI.Animator;
                    if (animation == null)
                        continue;
                    var animator = AccessTools.Field(typeof(InfantryAnimation), "_animator").GetValue(animation) as Behaviour;
                    var playables = AccessTools.Field(typeof(InfantryAnimation), "_playablesController").GetValue(animation);
                    GameBridge.Log("INFANTRY ANIM id=" + sample.Id + " speed=" + sample.State.Audio[1] +
                        " stance=" + animation.CurrentStance + " seated=" + animation.IsSeated +
                        " active=" + animation.isActiveAndEnabled + " incapacitated=" + unit.UnitIncapacitated +
                        " destroyed=" + unit.Destroyed + " humanDead=" + unit.Human.IsDead +
                        " hostDead=" + sample.State.Dead + " animator=" + (animator == null ? "missing" :
                        (animator.isActiveAndEnabled + "/speed=" + AccessTools.Property(animator.GetType(), "speed").GetValue(animator, null) + "/culling=" + AccessTools.Property(animator.GetType(), "cullingMode").GetValue(animator, null))) +
                        " playables=" + (playables == null ? "missing" :
                        (AccessTools.Property(playables.GetType(), "IsInfantry").GetValue(playables, null) +
                        "/ragdoll=" + AccessTools.Property(playables.GetType(), "IsRagdoll").GetValue(playables, null))));
                }
                }
                catch (Exception error)
                {
                    Warn("animation diagnostic unavailable: " + error.Message);
                }
            }
            foreach (var entry in units.Values)
            {
                try
                {
                    ApplyEntry(entry, false);
                }
                catch (Exception error)
                {
                    Warn("render " + entry.Id + ": " + error.Message);
                }
            }
        }

        public bool Request(InfantryManager manager, bool recall)
        {
            var player = PlayerInput.Instance;
            var unit = player == null ? null : player.CurrentPlayerUnit;
            if (unit != null && unit.GetComponentInChildren<InfantryManager>(true) == manager && requests.Count < 8)
                requests.Enqueue(new Message { Kind = Kind.InfantryOrder, Fire = recall });
            return false;
        }

        public Message[] Drain()
        {
            var result = requests.ToArray();
            requests.Clear();
            return result;
        }

        public bool RequestPlatoon(bool recall)
        {
            if (requests.Count < 8)
                requests.Enqueue(new Message { Kind = Kind.InfantryOrder, Fire = recall, Text = "platoon" });
            return false;
        }

        public void HandleRequest(Message message, Unit owner)
        {
            if (message.Text == "bail-out")
            {
                // The network session has already resolved and checked ownership.
                // Execute the native evacuation on the authoritative vehicle only.
                if (owner == null || owner.CrewManager == null || owner.Abandoned || owner.Destroyed)
                    return;
                owner.ClearAllInputs();
                owner.CrewManager.DoCommandedEvacuation();
                GameBridge.Log("CREW evacuation requested vehicle=" + owner.UniqueName);
                return;
            }
            if (message.Text == "platoon")
            {
                // Resolve the scope on the host; never trust guest-supplied targets.
                var platoon = owner == null ? null : owner.Platoon;
                if (platoon == null || platoon.PlatoonLeaderUnit != owner || owner.UnitIncapacitated)
                    return;
                foreach (var unit in platoon.Units)
                    HandleRequest(new Message { Fire = message.Fire }, unit);
                return;
            }
            var manager = owner == null ? null : owner.GetComponentInChildren<InfantryManager>(true);
            if (manager == null || !manager.HasActiveSquad() || owner.UnitIncapacitated)
                return;
            if (message.Fire)
                manager.RecallInfantry();
            else
                manager.DeployInfantry();
            GameBridge.Log("INFANTRY order vehicle=" + owner.UniqueName + " recall=" + message.Fire);
        }

        public void Dispose()
        {
            requests.Clear();
            heldSquads.Clear();
            units.Clear();
            cached.Clear();
            seats.Clear();
            if (Current == this)
                Current = null;
        }

        public void RequestBailOut()
        {
            if (requests.Count < 8)
                requests.Enqueue(new Message { Kind = Kind.InfantryOrder, Text = "bail-out" });
        }
    }

    [HarmonyPatch(typeof(PlayerInput), "BailOut")]
    static class GuestBailOutRequest
    {
        static bool Prefix()
        {
            if (!GameBridge.ReplicaActive)
                return true;
            InfantrySync.Current?.RequestBailOut();
            return false;
        }
    }

    [HarmonyPatch(typeof(InfantryUnit), "InitializeFromSquad")]
    static class InfantryIdentityPatch
    {
        static void Postfix(InfantryUnit __instance, SquadData __0, int __1)
        {
            InfantrySync.Register(__instance, __0, __1);
        }
    }

    [HarmonyPatch(typeof(InfantryManager), "DeployInfantry")]
    static class InfantryDeployPatch
    {
        static bool Prefix(InfantryManager __instance)
        {
            if (!GameBridge.ReplicaActive)
                return true;
            return InfantrySync.Current != null && InfantrySync.Current.Request(__instance, false);
        }
    }

    [HarmonyPatch(typeof(PlayerInput), "DeployPlatoonInfantry")]
    static class InfantryDeployPlatoonPatch
    {
        static bool Prefix()
        {
            return !GameBridge.ReplicaActive ||
                (InfantrySync.Current != null && InfantrySync.Current.RequestPlatoon(false));
        }
    }

    [HarmonyPatch(typeof(PlayerInput), "RecallPlatoonInfantry")]
    static class InfantryRecallPlatoonPatch
    {
        static bool Prefix()
        {
            return !GameBridge.ReplicaActive ||
                (InfantrySync.Current != null && InfantrySync.Current.RequestPlatoon(true));
        }
    }

    [HarmonyPatch(typeof(InfantryManager), "RecallInfantry")]
    static class InfantryRecallPatch
    {
        static bool Prefix(InfantryManager __instance)
        {
            if (!GameBridge.ReplicaActive)
                return true;
            return InfantrySync.Current != null && InfantrySync.Current.Request(__instance, true);
        }
    }

    [HarmonyPatch(typeof(InfantryUnit), "OnCollisionWithVehicle")]
    static class InfantryVehicleCollisionGuard
    {
        static bool Prefix()
        {
            // Vehicle interpolation can overlap a soldier during dismount. Native
            // collision handling both pushes the soldier and calls Human.Kill
            // directly, bypassing the replicated component-damage guards.
            // Only the host may resolve these collisions, including while a guest
            // is loading and has not received the first infantry snapshot yet.
            return !DamageSync.IsGuest;
        }
    }

    [HarmonyPatch(typeof(InfantryUnitAI), "UpdateAI")]
    static class InfantryAiGuard
    {
        static bool Prefix(InfantryUnitAI __instance)
        {
            return InfantrySync.Current == null || !InfantrySync.Current.Owns(__instance.GetComponent<InfantryUnit>());
        }
    }

    [HarmonyPatch(typeof(InfantryMovementController), "Update")]
    static class InfantryMovementGuard
    {
        static bool Prefix(InfantryMovementController __instance)
        {
            return InfantrySync.Current == null || !InfantrySync.Current.Owns(__instance.GetComponent<InfantryUnit>());
        }
    }

    [HarmonyPatch(typeof(SquadData), "Update")]
    static class InfantrySquadGuard
    {
        static bool Prefix(SquadData __instance)
        {
            return InfantrySync.Current == null || !InfantrySync.Current.Owns(__instance);
        }
    }

    [HarmonyPatch(typeof(InfantryHeightAlignmentSystem), "CheckUnitRequirements")]
    static class InfantryHeightGuard
    {
        static bool Prefix(InfantryUnitAI __0, ref bool __result)
        {
            if (InfantrySync.Current == null || !InfantrySync.Current.Owns(__0.GetComponent<InfantryUnit>()))
                return true;
            __result = false;
            return false;
        }
    }
}

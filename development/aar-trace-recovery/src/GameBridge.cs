using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GHPC;
using GHPC.AI;
using GHPC.Crew;
using GHPC.Player;
using GHPC.Weapons;
using HarmonyLib;

namespace GhpcCoop
{
    public sealed class VehicleRecord
    {
        public DamageSync Damage;
        public VehicleAudioSync Audio;
        public EquipmentSync Equipment;
        public WeaponSystem[] Weapons;
        public int[] WeaponShots;
        public float[] SoundUntil;
        public NWH.VehiclePhysics.Tracks Tracks;
        public Transform[] Wheels;
        public GHPC.Effects.DetachableParent[] Debris;
        public readonly List<Pose> Frames = new List<Pose>();
        public Pose Previous;
        public float Arrival, RenderStamp;
        public string Id;
        public string Layout;
        public Unit Unit;
        public AimablePlatform[] Mounts;
        public int Shots;
        public Action<AmmoType, LiveRound> ShotHandler;
    }

    public sealed partial class GameBridge : IDisposable
    {
        static readonly System.Reflection.FieldInfo[] TrackFields = new[]
        {
            "leftTrackVel",
            "rightTrackVel",
            "maxLeftRpm",
            "maxRightRpm"
        }.Select(n => AccessTools.Field(typeof(NWH.VehiclePhysics.Tracks), n)).ToArray();
        static readonly System.Reflection.FieldInfo ProjectedGoal = AccessTools.Field(typeof(AimablePlatform), "_projectedGoalLook");
        public CombatVisuals Combat;
        public static bool ReplicaActive;
        public static Unit RemoteControlledUnit;
        public static bool ApplyingRemoteDrive;
        static readonly System.Reflection.MethodInfo FireMethod = typeof(CrewBrainWeaponsModule).GetMethod("Fire");
        public readonly Dictionary<string, VehicleRecord> Vehicles = new Dictionary<string, VehicleRecord>(StringComparer.Ordinal);
        readonly List<Action> restore = new List<Action>(), guestRestore = new List<Action>();
        readonly Dictionary<AimablePlatform, bool> replicaMountEnabled = new Dictionary<AimablePlatform, bool>();
        public string World, Roster, LocalId;
        public string GuestId;
        public bool Dirty;
        static readonly HashSet<Unit> remoteUnits = new HashSet<Unit>();
        readonly Dictionary<int, GameBridge> peers = new Dictionary<int, GameBridge>();
        public static bool IsRemote(Unit unit)
        {
            return unit != null && remoteUnits.Contains(unit);
        }

        public static bool IsRemoteChassis(NwhChassis chassis)
        {
            return remoteUnits.Any(u => u != null && object.ReferenceEquals(u.Chassis, chassis) && !u.CannotMove);
        }

        public void ReservePeer(int peer, string id)
        {
            ReleasePeer(peer);
            var driver = new GameBridge
            {
                LocalId = LocalId,
                Combat = Combat
            };
            foreach (var v in Vehicles)
                driver.Vehicles.Add(v.Key, v.Value);
            driver.ReserveGuest(id);
            peers.Add(peer, driver);
        }

        public void ReleasePeer(int peer)
        {
            GameBridge driver;
            if (peers.TryGetValue(peer, out driver))
            {
                peers.Remove(peer);
                driver.ReleaseGuest();
            }
        }

        public void ReceivePeer(int peer, Message m)
        {
            GameBridge driver;
            if (!peers.TryGetValue(peer, out driver))
                throw new InvalidOperationException("Peer has no vehicle");
            driver.ReceiveInput(m);
        }

        public void DrivePeer(int id)
        {
            GameBridge p;
            if (peers.TryGetValue(id, out p))
                p.DriveRemote();
        }

        public void FirePeer(int id)
        {
            GameBridge p;
            if (peers.TryGetValue(id, out p))
                p.FireRemote();
        }

        public void RenderPeer(int id)
        {
            GameBridge p;
            if (peers.TryGetValue(id, out p))
                p.RenderRemoteMounts();
        }

        float nextShotLog;
        float nextInputLog;
        readonly ReplicaTimeline replicaTimeline = new ReplicaTimeline();
        float statsStart, previousArrival, maxArrivalGap, maxFrame;
        int receivedFrames, renderedFrames, starvedFrames;
        Quaternion[] remoteStart;
        Message command;
        float received, nextDriveLog, nextAmmoLog;
        bool pendingReload;
        readonly ShotInputLatch shotInputs = new ShotInputLatch();
        readonly Dictionary<string, Pose> targets = new Dictionary<string, Pose>();
        public static Transform MountTransform(AimablePlatform m)
        {
            return m.Transform != null ? m.Transform : m.transform;
        }

        public static string PathOf(Transform t)
        {
            var parts = new List<string>();
            while (t != null)
            {
                parts.Add(t.name + "#" + t.GetSiblingIndex());
                t = t.parent;
            }

            parts.Reverse();
            return String.Join("/", parts.ToArray());
        }

        // Static mission spawn anchors disambiguate legacy root-level ported spawn points.
        // Never derive this key from a spawned vehicle, whose pose changes during play.
        static string SpawnAnchor(Transform t)
        {
            var p = t.position;
            var r = t.rotation;
            return String.Join(",", new[] { p.x, p.y, p.z, r.x, r.y, r.z, r.w }.Select(v => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).ToArray());
        }

        public void Capture()
        {
            var p = PlayerInput.Instance;
            if (p == null)
                throw new InvalidOperationException("Player controls are not ready. Wait for the mission to finish loading.");
            if (p.CurrentPlayerUnit == null)
                throw new InvalidOperationException("No player vehicle selected. Enter a mission and select a friendly vehicle.");
            var selected = p.CurrentPlayerUnit;
            if (selected.Chassis == null || !selected.gameObject.scene.IsValid() || !selected.gameObject.scene.isLoaded)
                throw new InvalidOperationException("Select a ground vehicle in a loaded mission.");
            if (selected.Destroyed)
                throw new InvalidOperationException("Select a surviving friendly vehicle.");
            // IsMainMenuActive also covers the in-mission pause menu; it is not a mission-readiness test.
            Log("CAPTURE ready initialized=" + p.IsInitialized + " menu=" + p.IsMainMenuActive + " scene=" + selected.gameObject.scene.name + " vehicle=" + selected.FriendlyName);
            Dispose();
            Combat = new CombatVisuals();
            Vehicles.Clear();
            LocalId = null;
            RosterRevision = 1;
            liveRosterSignature = "";
            acceptedRoster = null;
            World = SceneController.TargetMissionScene + "|" + p.CurrentPlayerUnit.gameObject.scene.name + "|" + SceneController.IsDaytime;
            var all = UnityEngine.Object.FindObjectsOfType<Unit>().Where(u => u.Chassis != null).ToArray();
            if (all.Length < 2)
                throw new InvalidOperationException("This mission has only " + all.Length + " active ground vehicle(s). Co-op needs at least two eligible vehicles.");
            if (all.Length > Wire.MaxUnits)
                throw new InvalidOperationException("Mission contains " + all.Length + " ground vehicles; supported limit is " + Wire.MaxUnits);
            spawnPoints = Resources.FindObjectsOfTypeAll<GHPC.Mission.DynamicSpawnPoint>()
                .Where(x => x.gameObject.scene.IsValid() && x.gameObject.scene.isLoaded).ToArray();
            spawnKeys.Clear();
            foreach (var point in spawnPoints)
                spawnKeys.Add(point, "spawn/" + point.gameObject.scene.name + "/" + PathOf(point.transform) +
                    "/anchor/" + SpawnAnchor(point.transform));
            var identities = DiscoverIdentities(all);
            var diagnostics = new List<string>();
            foreach (var u in all)
            {
                var identity = identities[u];
                var id = Wire.Hash(identity);
                if (Vehicles.ContainsKey(id))
                    throw new InvalidOperationException("Ambiguous vehicle identity: " + identity);
                var rec = RegisterVehicle(u, id);
                Vehicles.Add(id, rec);
                diagnostics.Add(id + "\t" + u.UniqueName + "\t" + u.Allegiance + "\t" + identity + "\t" + rec.Mounts.Length + "/" + rec.Weapons.Length + "/" + rec.Damage.Layout);
                if (u == p.CurrentPlayerUnit)
                    LocalId = id;
            }

            if (LocalId == null)
                throw new InvalidOperationException("Select a ground vehicle");
            System.IO.Directory.CreateDirectory("UserData/GhpcCoop");
            System.IO.File.WriteAllLines("UserData/GhpcCoop/roster-diagnostic.txt", diagnostics.OrderBy(x => x, StringComparer.Ordinal).ToArray());
            // Display names may be translated. Match the game's technical vehicle key instead.
            Roster = Wire.Hash(String.Join("\n",
                Vehicles.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => x.Key + ":" + x.Value.Unit.Allegiance + ":" + x.Value.Unit.UniqueName + ":" + x.Value.Mounts.Length + ":" + x.Value.Weapons.Length + ":" + x.Value.Damage.Layout).ToArray()));
            RefreshRoster();
        }

        public IEnumerable<string> FriendlyChoices()
        {
            var faction = Vehicles[LocalId].Unit.Allegiance;
            return Vehicles.Where(x => x.Value.Unit != null && x.Value.Unit.gameObject.activeInHierarchy && x.Value.Unit.Allegiance == faction &&
                x.Value.Unit.CrewManager != null &&
                x.Value.Unit.CrewManager.GetCrewBrain(CrewPosition.Driver) != null).Select(x => x.Key);
        }

        public IEnumerable<string> PlatoonChoices()
        {
            var host = Vehicles[LocalId].Unit;
            return FriendlyChoices().Where(id => id != LocalId &&
                (host.Platoon != null ? Vehicles[id].Unit.Platoon == host.Platoon : Vehicles[id].Unit.Platoon == null) &&
                !Vehicles[id].Unit.Destroyed &&
                !Vehicles[id].Unit.Abandoned &&
                !Vehicles[id].Unit.UnitIncapacitated).OrderBy(id => Vehicles[id].Unit.FriendlyName == host.FriendlyName ? 0 : 1).ThenBy(id => id, StringComparer.Ordinal);
        }

        // Preserve platoon membership; only expand the ordered pool of controllable seats.
        public IEnumerable<string> RoomChoices()
        {
            return PlatoonChoices().Concat(FriendlyChoices().Where(id => id != LocalId &&
                Available(id)).OrderBy(id => id, StringComparer.Ordinal)).Distinct(StringComparer.Ordinal);
        }

        public bool Available(string id)
        {
            VehicleRecord r;
            return Vehicles.TryGetValue(id, out r) &&
                r.Unit != null && r.Unit.gameObject.activeInHierarchy &&
                !r.Unit.Destroyed &&
                !r.Unit.Abandoned &&
                !r.Unit.UnitIncapacitated &&
                FriendlyChoices().Contains(id);
        }

        public static bool IsRemoteCrew(GHPC.Crew.CrewManager crew)
        {
            return remoteUnits.Any(u => u != null && u.CrewManager == crew);
        }

        void ManualCrew(Unit u, List<Action> undo = null)
        {
            if (undo == null)
                undo = restore;
            if (u.CrewManager == null)
                return;
            foreach (var pos in new[]
            {
                CrewPosition.Driver,
                CrewPosition.Gunner,
                CrewPosition.Commander
            }

            )
            {
                var brain = u.CrewManager.GetCrewBrain(pos);
                if (brain == null)
                    continue;
                bool old = brain.IsAutonomous, wasSuspended = brain.Suspended;
                brain.IsAutonomous = false;
                brain.Suspended = false;
                undo.Add(delegate
                {
                    try
                    {
                        brain.IsAutonomous = old;
                        brain.Suspended = wasSuspended;
                    }
                    catch
                    {
                    // Continue restoring other native objects if an earlier object cannot be restored during teardown.
                    }
                });
            }
        }

        public void ReserveGuest(string id)
        {
            if (GuestId == id)
                return;
            if (id == LocalId || !Available(id))
                throw new InvalidOperationException("Invalid remote vehicle");
            ReleaseGuest();
            GuestId = id;
            RemoteControlledUnit = Vehicles[id].Unit;
            remoteUnits.Add(RemoteControlledUnit);
            CombatVisuals.Host = Combat;
            Vehicles[id].Equipment.BeginRemoteControl();
            ManualCrew(Vehicles[id].Unit, guestRestore);
            foreach (var mount in Vehicles[id].Mounts)
            {
                var captured = mount;
                bool was = mount.enabled;
                mount.enabled = false;
                guestRestore.Add(delegate
                {
                    if (captured != null)
                        captured.enabled = was;
                });
            }

            Log("RESERVED " + Vehicles[id].Unit.FriendlyName);
        }

        void ReleaseGuest()
        {
            if (GuestId != null && Vehicles.ContainsKey(GuestId))
                remoteUnits.Remove(Vehicles[GuestId].Unit);
            RemoteControlledUnit = null;
            if (GuestId != null && Vehicles.ContainsKey(GuestId))
            {
                var r = Vehicles[GuestId];
                if (r.Unit != null)
                    r.Unit.ClearAllInputs();
                foreach (var w in r.Weapons)
                    if (w != null)
                        w.StopFiring();
            }

            for (int i = guestRestore.Count - 1; i >= 0; i--)
                try
                {
                    guestRestore[i]();
                }
                catch (Exception e)
                {
                    Log("PEER restore: " + e.Message);
                }

            guestRestore.Clear();
            RemoteControlledUnit = null;
            GuestId = null;
            command = null;
            remoteStart = null;
            pendingReload = false;
            shotInputs.Clear();
        }

        public void SelectReplicaVehicle(string id)
        {
            if (!Available(id))
                throw new InvalidOperationException("Vehicle unavailable");
            LocalId = id;
            foreach (var r in Vehicles.Values)
                foreach (var mount in r.Mounts)
                {
                    bool original;
                    if (replicaMountEnabled.TryGetValue(mount, out original))
                        mount.enabled = r.Id == id && original;
                }

            PlayerInput.Instance.SetPlayerUnit(Vehicles[id].Unit);
        }

        public void BeginReplica()
        {
            if (ReplicaActive)
                return;
            Dirty = true;
            ReplicaActive = true;
            foreach (var r in Vehicles.Values)
                PrepareReplica(r);

            Log("REPLICA started; local weapon fire and AI suppressed");
        }

        public void Dispose()
        {
            foreach (var peer in peers.Keys.ToArray())
                SafeCleanup.Run("peer vehicle", () => ReleasePeer(peer), Log);
            SafeCleanup.Run("guest vehicle", () => ReleaseGuest(), Log);
            foreach (var rec in Vehicles.Values)
                if (ReplicaActive || rec.Id == GuestId)
                    foreach (var weapon in rec.Weapons)
                        if (weapon != null)
                            SafeCleanup.Run("weapon", () => weapon.StopFiring(), Log);
            if (Combat != null)
            {
                SafeCleanup.Run("combat effects", () => Combat.Dispose(), Log);
                Combat = null;
            }

            ReplicaActive = false;
            acceptedRoster = null;
            replicaMountEnabled.Clear();
            AmmoSync.Clear();
            ReplicaCrewAudio.Clear();
            foreach (var rec in Vehicles.Values)
                if (rec.Audio != null)
                    SafeCleanup.Run("vehicle audio", () => rec.Audio.Dispose(), Log);
            replicaTimeline.Reset();
            command = null;
            remoteStart = null;
            pendingReload = false;
            shotInputs.Clear();
            if (GuestId != null && Vehicles.ContainsKey(GuestId))
            {
                var u = Vehicles[GuestId].Unit;
                if (u != null)
                    u.ClearAllInputs();
            }

            RemoteControlledUnit = null;
            GuestId = null;
            targets.Clear();
            for (int i = restore.Count - 1; i >= 0; i--)
                try
                {
                    restore[i]();
                }
                catch
                {
                // Continue restoring other native objects if an earlier object cannot be restored during teardown.
                }

            restore.Clear();
            foreach (var r in Vehicles.Values)
                if (r.Equipment != null)
                    SafeCleanup.Run("vehicle equipment", () => r.Equipment.Dispose(), Log);
            foreach (var r in Vehicles.Values)
                if (r.Unit != null && r.Unit.InfoBroker != null && r.ShotHandler != null)
                {
                    r.Unit.InfoBroker.WeaponFired -= r.ShotHandler;
                    r.ShotHandler = null;
                }
        }

        public float ReplicaError()
        {
            float max = 0;
            foreach (var pair in targets)
            {
                var p = pair.Value;
                var u = Vehicles[p.Id].Unit;
                if (u != null)
                    max = Mathf.Max(max, Vector3.Distance(u.RootTransform.position, new Vector3(p.X, p.Y, p.Z)));
            }

            return max;
        }

        public static void Log(string s)
        {
            MelonLoader.MelonLogger.Msg("COOP " + s);
        }
    }

    [HarmonyPatch(typeof(NwhChassis), "SetSpeed")]
    static class RemoteSpeedOwner
    {
        static float next;
        static bool Prefix(NwhChassis __instance, float __0)
        {
            if (!GameBridge.IsRemoteChassis(__instance) || GameBridge.ApplyingRemoteDrive)
                return true;
            if (Time.realtimeSinceStartup > next)
            {
                next = Time.realtimeSinceStartup + 3;
                GameBridge.Log("BLOCKED local speed overwrite=" + __0);
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(NwhChassis), "SetHeading")]
    static class RemoteHeadingOwner
    {
        static bool Prefix(NwhChassis __instance)
        {
            return !GameBridge.IsRemoteChassis(__instance) || GameBridge.ApplyingRemoteDrive;
        }
    }

    [HarmonyPatch(typeof(NwhChassis), "SetTurnAmount", new Type[] { typeof(bool), typeof(float), typeof(bool) })]
    static class RemoteSteerOwner
    {
        static bool Prefix(NwhChassis __instance)
        {
            return !GameBridge.IsRemoteChassis(__instance) || GameBridge.ApplyingRemoteDrive;
        }
    }

    [HarmonyPatch(typeof(WeaponSystem), "Fire")]
    static class ClientFireBlock
    {
        static bool Prefix(ref bool __result)
        {
            if (!DamageSync.IsGuest)
                return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(UnitAI), "UpdateAI")]
    static class ClientAiBlock
    {
        static bool Prefix(UnitAI __instance)
        {
            return !DamageSync.IsGuest && !GameBridge.IsRemote(__instance.Unit);
        }
    }
}


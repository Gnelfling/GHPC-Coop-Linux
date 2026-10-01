using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GHPC;
using GHPC.Weapons;
using GHPC.AI;

namespace GhpcCoop
{
    public sealed partial class GameBridge
    {
        GHPC.Mission.DynamicSpawnPoint[] spawnPoints = new GHPC.Mission.DynamicSpawnPoint[0];
        readonly Dictionary<GHPC.Mission.DynamicSpawnPoint, string> spawnKeys = new Dictionary<GHPC.Mission.DynamicSpawnPoint, string>();
        HashSet<string> acceptedRoster;
        string liveRosterSignature = "";
        public long RosterRevision { get; private set; } = 1;

        Dictionary<Unit, string> DiscoverIdentities(Unit[] all)
        {
            var spawnIds = new Dictionary<Unit, string>();
            foreach (var point in spawnPoints.Where(x => x != null && x.SpawnedUnits != null))
            {
                for (int i = 0; i < point.SpawnedUnits.Count; i++)
                {
                    var unit = point.SpawnedUnits[i];
                    if (unit == null || unit.Chassis == null)
                        continue;
                    // Authored spawn transforms can share names. Include hierarchy sibling indices;
                    // never use a moving vehicle position, instance ID, or runtime spawn counter.
                    var key = spawnKeys[point] + "/slot/" + i;
                    string previous;
                    if (spawnIds.TryGetValue(unit, out previous) && previous != key)
                        throw new InvalidOperationException("Vehicle belongs to multiple spawn points");
                    spawnIds[unit] = key;
                }
            }

            // Preflight the complete roster before registering any per-vehicle effects or handlers.
            var identities = new Dictionary<Unit, string>();
            foreach (var unit in all)
            {
                string key;
                if (!spawnIds.TryGetValue(unit, out key))
                {
                    var slot = unit.Platoon != null && unit.Platoon.Units != null ? unit.Platoon.Units.IndexOf(unit) : -1;
                    key = slot >= 0 ? "platoon/" + PathOf(unit.Platoon.transform) + "/slot/" + slot : "unit/" + PathOf(unit.transform);
                }

                identities.Add(unit, unit.gameObject.scene.name + "/" + key);
            }

            var duplicates = identities.GroupBy(x => x.Value, StringComparer.Ordinal).Where(x => x.Count() > 1).ToArray();
            if (duplicates.Length > 0)
            {
                var rows = duplicates.SelectMany(g => g.Select(x => g.Key + "\t" + x.Key.UniqueName + "\t" + PathOf(x.Key.transform))).ToArray();
                System.IO.Directory.CreateDirectory("UserData/GhpcCoop");
                System.IO.File.WriteAllLines("UserData/GhpcCoop/identity-conflicts.txt", rows);
                throw new InvalidOperationException("Ambiguous vehicle identity: " + duplicates[0].Key + ". See identity-conflicts.txt");
            }

            return identities;
        }

        VehicleRecord RegisterVehicle(Unit u, string id)
        {
            var rec = new VehicleRecord
            {
                Id = id,
                Unit = u,
                Mounts = (u.AimablePlatforms ?? new AimablePlatform[0]).Where(x => x != null).OrderBy(x => PathOf(MountTransform(x)), StringComparer.Ordinal).ToArray()
            };
            rec.Damage = new DamageSync(u);
            rec.Equipment = new EquipmentSync(u);
            rec.Audio = new VehicleAudioSync(u);
            rec.Weapons = u.GetComponentsInChildren<WeaponSystem>(true).OrderBy(w => PathOf(w.transform), StringComparer.Ordinal).ToArray();
            rec.WeaponShots = new int[rec.Weapons.Length];
            rec.SoundUntil = new float[rec.Weapons.Length];
            // Cache before detachment removes these components from the vehicle hierarchy.
            rec.Debris = u.GetComponentsInChildren<GHPC.Effects.DetachableParent>(true).OrderBy(d => PathOf(d.transform), StringComparer.Ordinal).ToArray();
            var controller = u.GetComponentInChildren<NWH.VehiclePhysics.VehicleController>();
            rec.Tracks = controller != null && controller.tracks != null && controller.tracks.trackedVehicle ? controller.tracks : null;
            rec.Wheels = u.GetComponentsInChildren<NWH.WheelController3D.WheelController>(true).Where(w => !w.trackedVehicle &&
                w.Visual != null).OrderBy(w => PathOf(w.transform), StringComparer.Ordinal).Select(w => w.Visual.transform).ToArray();
            for (int wi = 0; wi < rec.Weapons.Length; wi++)
            {
                int index = wi;
                var weapon = rec.Weapons[wi];
                Action<AmmoType, LiveRound> handler = delegate (AmmoType a, LiveRound round)
                {
                    rec.WeaponShots[index]++;
                    ShotAudit.Record(rec.Id, round);
                };
                weapon.Fired += handler;
                restore.Add(delegate
                {
                    if (weapon != null)
                        weapon.Fired -= handler;
                });
            }

            if (rec.Weapons.Length > 64 || rec.Mounts.Length > 64)
                throw new InvalidOperationException("Too many mounts");
            rec.ShotHandler = delegate (AmmoType a, LiveRound r)
            {
                rec.Shots++;
            };
            u.InfoBroker.WeaponFired += rec.ShotHandler;
            // Technical identity and topology, never localized display names or positions.
            rec.Layout = Wire.Hash(u.UniqueName + ":" + u.Allegiance + ":" +
                rec.Mounts.Length + ":" + rec.Weapons.Length + ":" + rec.Wheels.Length + ":" +
                rec.Debris.Length + ":" + rec.Damage.Layout);
            return rec;
        }

        void PrepareReplica(VehicleRecord r)
        {
            var u = r.Unit;
            ManualCrew(u);
            Log("BODY " + u.FriendlyName + " root=" + u.RootTransform.position + " body=" + (u.Chassis.Rigidbody != null ? u.Chassis.Rigidbody.position.ToString() : "none") + " same=" + (u.Chassis.Rigidbody != null &&
                u.Chassis.Rigidbody.transform == u.RootTransform));
            foreach (var platform in r.Mounts)
            {
                var captured = platform;
                bool was = platform.enabled;
                replicaMountEnabled[platform] = was;
                platform.enabled = r.Id == LocalId && was;
                restore.Add(delegate
                {
                    if (captured != null)
                        captured.enabled = was;
                });
            }

            // NWH must not write physical state over authoritative snapshots.
            foreach (var b in u.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (b is NwhChassis || b.GetType().FullName.StartsWith("NWH.VehiclePhysics.", StringComparison.Ordinal))
                {
                    var captured = b;
                    bool was = b.enabled;
                    b.enabled = false;
                    restore.Add(delegate
                    {
                        if (captured != null)
                            captured.enabled = was;
                    });
                }
            }

            foreach (var body in u.GetComponentsInChildren<Rigidbody>(true))
            {
                var captured = body;
                var oldInterpolation = body.interpolation;
                body.interpolation = RigidbodyInterpolation.None;
                bool was = body.isKinematic;
                if (!was)
                {
                    body.velocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }

                body.isKinematic = true;
                restore.Add(delegate
                {
                    if (captured != null)
                    {
                        captured.isKinematic = was;
                        captured.interpolation = oldInterpolation;
                    }
                });
            }
        }

        // Authored spawn lists are cached at mission capture. Polling their membership
        // avoids repeating a whole-scene Unity object search during ordinary driving.
        public bool RefreshRoster()
        {
            var knownUnits = new HashSet<Unit>(Vehicles.Values.Where(r => r.Unit != null).Select(r => r.Unit));
            var added = spawnPoints.Where(p => p != null && p.SpawnedUnits != null)
                .SelectMany(p => p.SpawnedUnits).Where(u => u != null && u.Chassis != null && u.gameObject.activeInHierarchy && !knownUnits.Contains(u))
                .Distinct().ToArray();
            var identities = added.Length == 0 ? null : DiscoverIdentities(added);
            foreach (var unit in added)
            {
                var id = Wire.Hash(identities[unit]);
                VehicleRecord existing;
                if (Vehicles.TryGetValue(id, out existing))
                    throw new InvalidOperationException("Spawn identity reused in this mission: " + id);
                if (Vehicles.Count >= Wire.MaxUnits)
                    throw new InvalidOperationException("Vehicle roster limit exceeded");
                var record = RegisterVehicle(unit, id);
                Vehicles.Add(id, record);
                foreach (var driver in peers.Values) driver.Vehicles[id] = record;
                if (ReplicaActive) PrepareReplica(record);
            }
            var signature = Wire.Hash(String.Join("\n", Vehicles.Values.Where(r => r.Unit != null && r.Unit.gameObject.activeInHierarchy)
                .Select(r => r.Id + ":" + r.Layout).OrderBy(id => id, StringComparer.Ordinal).ToArray()));
            if (signature == liveRosterSignature) return false;
            bool changed = liveRosterSignature != "";
            liveRosterSignature = signature;
            if (changed) RosterRevision++;
            return changed;
        }

        public Pose[] CaptureBaseline()
        {
            var poses = Snapshot();
            foreach (var pose in poses) pose.Layout = Vehicles[pose.Id].Layout;
            return poses;
        }

        public bool TryApplyBaseline(Message baseline, out string waitingFor)
        {
            RefreshRoster();
            var active = RosterBaselinePlan.Prepare(baseline, LocalId, id =>
            {
                VehicleRecord record;
                if (!Vehicles.TryGetValue(id, out record) || record.Unit == null) return null;
                var parent = record.Unit.transform.parent;
                // A disabled mission parent still owns native activation; do not ACK
                // a vehicle that would remain invisible after SetActive(true).
                return parent == null || parent.gameObject.activeInHierarchy ? record.Layout : null;
            }, MatchesSnapshotLayout, out waitingFor);
            if (active == null) return false;

            // Preflight completed. Preserve native records/handlers and seat ownership;
            // only reset presentation history, which belongs to the old baseline.
            foreach (var record in Vehicles.Values)
            {
                if (record.Unit != null && !active.Contains(record.Id) && record.Unit.gameObject.activeSelf)
                {
                    var unit = record.Unit;
                    unit.gameObject.SetActive(false);
                    restore.Add(() => { if (unit != null) unit.gameObject.SetActive(true); });
                }
                else if (record.Unit != null && active.Contains(record.Id))
                    record.Unit.gameObject.SetActive(true);
                record.Frames.Clear();
                record.Previous = null;
                record.RenderStamp = 0;
            }
            targets.Clear();
            replicaTimeline.Reset();
            acceptedRoster = active;
            LocalId = baseline.Unit;
            RosterRevision = baseline.SyncRevision;
            foreach (var pose in baseline.Poses)
            {
                var record = Vehicles[pose.Id];
                record.Equipment.ApplyBaseline(pose.Equipment);
                Combat.ApplyBaseline(record.Id, record.Unit, pose.Fires);
                for (int index = 0; index < record.Mounts.Length; index++)
                {
                    if (record.Mounts[index].IsDetached) continue;
                    var rotation = pose.Mounts[index];
                    var orientation = new Quaternion(rotation.X, rotation.Y, rotation.Z, rotation.W);
                    record.Mounts[index].LocalRotation = orientation;
                    MountTransform(record.Mounts[index]).localRotation = orientation;
                }
            }
            ReceiveSnapshot(baseline.Poses);
            if (!GHPC.AarController.InAar) RenderReplica();
            return true;
        }

        public void SuspendPeerInput(int peer)
        {
            GameBridge driver;
            if (peers.TryGetValue(peer, out driver)) driver.SuspendRemoteInput();
        }

        public void SuspendRemoteInput()
        {
            command = null;
            remoteStart = null;
            pendingReload = false;
            shotInputs.Clear();
            VehicleRecord record;
            if (GuestId == null || !Vehicles.TryGetValue(GuestId, out record)) return;
            if (record.Unit != null)
            {
                record.Unit.ClearAllInputs();
                DriveRemote(); // Reset driver cruise/steering targets as well as the raw inputs.
            }
            foreach (var weapon in record.Weapons)
                if (weapon != null) weapon.StopFiring();
        }
    }
}

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
    public sealed partial class GameBridge
    {
        public Pose[] Snapshot()
        {
            return Vehicles.Values.Where(r => r.Unit != null && r.Unit.gameObject.activeInHierarchy).Select(r =>
            {
                var u = r.Unit;
                var p = u.RootTransform.position;
                var q = u.RootTransform.rotation;
                return new Pose
                {
                    Scorch = r.Damage.CaptureScorch(),
                    Health = r.Damage.Capture(),
                    Audio = r.Audio.Capture(),
                    Equipment = r.Equipment.Capture(),
                    Id = r.Id,
                    X = p.x,
                    Y = p.y,
                    Z = p.z,
                    Qx = q.x,
                    Qy = q.y,
                    Qz = q.z,
                    Qw = q.w,
                    Dead = u.Destroyed,
                    Flags = (u.Destroyed ? 1 : 0) | (u.Abandoned ? 2 : 0) | (u.CannotMove ? 4 : 0) | (u.CannotShoot ? 8 : 0) | (u.UnitIncapacitated ? 16 : 0),
                    Shots = r.Shots,
                    CrewPenetrations = ReplicaCrewAudio.CapturePenetrations(u),
                    PlatoonLeader = u.Platoon != null && u.Platoon.PlatoonLeaderUnit == u,
                    Fires = Combat.Capture(u),
                    Ammo = r.Weapons.Select(AmmoSync.Capture).ToArray(),
                    Stamp = Time.realtimeSinceStartup,
                    Tracks = r.Tracks == null ? new float[4] : TrackFields.Select(f => (float)f.GetValue(r.Tracks)).ToArray(),
                    Wheels = r.Wheels.Select(w =>
                    {
                        // Read Unity's transform once for a consistent four-component sample.
                        var rotation = w.localRotation;
                        return new Rotation { X = rotation.x, Y = rotation.y, Z = rotation.z, W = rotation.w };
                    }).ToArray(),
                    Debris = r.Debris.Select(d =>
                    {
                        if (d == null || d.Transform == null)
                            return new DebrisState();
                        var point = d.Transform.position;
                        var orientation = d.Transform.rotation;
                        return new DebrisState
                        {
                            Detached = d.Detached,
                            X = point.x,
                            Y = point.y,
                            Z = point.z,
                            Qx = orientation.x,
                            Qy = orientation.y,
                            Qz = orientation.z,
                            Qw = orientation.w
                        };
                    }).ToArray(),
                    WeaponShots = (int[])r.WeaponShots.Clone(),
                    Mounts = r.Mounts.Select(m =>
                    {
                        var a = MountTransform(m).localRotation;
                        return new Rotation
                        {
                            X = a.x,
                            Y = a.y,
                            Z = a.z,
                            W = a.w
                        };
                    }).ToArray()
                };
            }).ToArray();
        }

        bool MatchesSnapshotLayout(Pose pose)
        {
            VehicleRecord record;
            return Vehicles.TryGetValue(pose.Id, out record) && record.Unit != null &&
                pose.Mounts.Length == record.Mounts.Length &&
                pose.Wheels.Length == record.Wheels.Length &&
                pose.Debris.Length == record.Debris.Length &&
                pose.WeaponShots.Length == record.Weapons.Length &&
                pose.Ammo.Length == record.Weapons.Length && record.Damage.MatchesLayout(pose) &&
                record.Equipment.MatchesLayout(pose.Equipment);
        }

        public void ValidateSnapshot(Pose[] poses)
        {
            SnapshotValidation.Validate(poses, acceptedRoster ?? (IEnumerable<string>)Vehicles.Keys,
                targets, MatchesSnapshotLayout);
        }

        public void ReceiveSnapshot(Pose[] poses)
        {
            // Validate the whole baseline before changing damage, ammo or interpolation.
            // Otherwise a mismatch late in the packet leaves earlier vehicles half-updated.
            ValidateSnapshot(poses);

            float arrival = Time.realtimeSinceStartup;
            if (previousArrival > 0)
                maxArrivalGap = Mathf.Max(maxArrivalGap, arrival - previousArrival);
            previousArrival = arrival;
            receivedFrames++;
            if (poses.Length > 0)
                replicaTimeline.Observe(poses[0].Stamp, arrival);
            // Keep the last authoritative wreck as a tombstone. Removing it would let a
            // delayed pre-destruction frame revive its interpolation and weapon effects.
            foreach (var p in poses)
            {
                var r = Vehicles[p.Id];
                Pose old;
                if (targets.TryGetValue(p.Id, out old))
                {
                    if (p.Stamp <= old.Stamp)
                        continue;
                    r.Previous = old;
                }
                else
                {
                    r.Previous = p;
                    Array.Copy(p.WeaponShots, r.WeaponShots, p.WeaponShots.Length);
                }

                r.Arrival = Time.realtimeSinceStartup;
                r.Frames.Add(p);
                if (r.Frames.Count > 32)
                    r.Frames.RemoveAt(0);
                targets[p.Id] = p;
                if (r.Unit != null)
                {
                    // Subscribe before applying transitions: the first damage/reload event
                    // must not arrive before the selected replica has a voice listener.
                    ReplicaCrewAudio.Ensure(r.Unit, r.Id == LocalId);
                    PlatoonSync.Apply(r.Unit, p.PlatoonLeader);

                    r.Damage.Apply(r.Unit, p);
                    ReplicaCrewAudio.ApplyPenetrations(r.Unit, p.CrewPenetrations, r.Id == LocalId);
                    for (int wi = 0; wi < r.Weapons.Length; wi++)
                        if (r.Weapons[wi] != null)
                        {
                            // A delayed snapshot can contain both a shot and a completed
                            // reload. Establish voice context before raising reload events,
                            // while LastAmmo still refers to the round that was fired.
                            if (old != null && p.WeaponShots[wi] > old.WeaponShots[wi])
                                ReplicaCrewAudio.Shot(r.Unit, r.Weapons[wi]);
                            AmmoSync.Apply(r.Weapons[wi], p.Ammo[wi]);
                        }
                }
            }
        }

        public void RenderReplica()
        {
            renderedFrames++;
            maxFrame = Mathf.Max(maxFrame, Time.unscaledDeltaTime);
            replicaTimeline.Advance(Time.realtimeSinceStartup, Time.unscaledDeltaTime);
            foreach (var pair in targets)
            {
                var latest = pair.Value;
                var r = Vehicles[latest.Id];
                var u = r.Unit;
                if (u == null || r.Frames.Count == 0)
                    continue;
                float renderTime = Mathf.Max(r.RenderStamp, replicaTimeline.RenderTime(r.Id == LocalId));
                r.RenderStamp = renderTime;
                while (r.Frames.Count > 2 && r.Frames[1].Stamp <= renderTime)
                    r.Frames.RemoveAt(0);
                if (r.Id == LocalId && renderTime >= latest.Stamp)
                    starvedFrames++;
                var previous = r.Frames[0];
                var p = r.Frames.Count > 1 ? r.Frames[1] : previous;
                var position = new Vector3(p.X, p.Y, p.Z);
                var rotation = new Quaternion(p.Qx, p.Qy, p.Qz, p.Qw);
                var t = u.RootTransform;
                float interval = Mathf.Max(0.001f, p.Stamp - previous.Stamp);
                float factor = Mathf.Clamp01((renderTime - previous.Stamp) / interval);
                var start = new Vector3(previous.X, previous.Y, previous.Z);
                if (Vector3.Distance(start, position) > 20)
                    factor = 1;
                var pos = Vector3.Lerp(start, position, factor);
                var rot = Quaternion.Slerp(new Quaternion(previous.Qx, previous.Qy, previous.Qz, previous.Qw), rotation, factor);
                t.SetPositionAndRotation(pos, rot);
                if (u.Chassis.Rigidbody != null && u.Chassis.Rigidbody.transform == t)
                {
                    u.Chassis.Rigidbody.position = pos;
                    u.Chassis.Rigidbody.rotation = rot;
                }

                if (p.Id != LocalId)
                    for (int i = 0; i < r.Mounts.Length; i++)
                    {
                        if (r.Mounts[i].IsDetached)
                            continue;
                        var q = p.Mounts[i];
                        var a = previous.Mounts[i];
                        var rotationLocal = Quaternion.Slerp(new Quaternion(a.X, a.Y, a.Z, a.W), new Quaternion(q.X, q.Y, q.Z, q.W), factor);
                        r.Mounts[i].LocalRotation = rotationLocal;
                        MountTransform(r.Mounts[i]).localRotation = rotationLocal;
                    }

                // Authoritative damage is applied on receive, independently of visual interpolation.
                r.Equipment.Apply(latest.Equipment);
                r.Audio.Apply(latest.Audio);
                ReplicaCrewAudio.Ensure(u, r.Id == LocalId);
                for (int i = 0; i < r.Debris.Length; i++)
                {
                    var part = r.Debris[i];
                    var state = p.Debris[i];
                    if (part == null || part.Transform == null || !state.Detached)
                        continue;
                    if (!part.Detached)
                    {
                        ReplicaDetachGuard.Applying = true;
                        try
                        {
                            part.Detach(0);
                        }
                        finally
                        {
                            ReplicaDetachGuard.Applying = false;
                        }
                    }

                    if (part.RBody != null)
                        part.RBody.isKinematic = true;
                    var before = previous.Debris[i].Detached ? previous.Debris[i] : state;
                    part.Transform.SetPositionAndRotation(Vector3.Lerp(new Vector3(before.X, before.Y, before.Z), new Vector3(state.X, state.Y, state.Z), factor),
                        Quaternion.Slerp(new Quaternion(before.Qx, before.Qy, before.Qz, before.Qw), new Quaternion(state.Qx, state.Qy, state.Qz, state.Qw), factor));
                }

                // Wheel visuals follow the host without enabling guest suspension/drive physics.
                if (p.Wheels.Length == r.Wheels.Length && previous.Wheels.Length == r.Wheels.Length)
                    for (int i = 0; i < r.Wheels.Length; i++)
                    {
                        var a = previous.Wheels[i];
                        var b = p.Wheels[i];
                        r.Wheels[i].localRotation = Quaternion.Slerp(new Quaternion(a.X, a.Y, a.Z, a.W), new Quaternion(b.X, b.Y, b.Z, b.W), factor);
                    }

                if (r.Tracks != null)
                {
                    for (int i = 0; i < 4; i++)
                        TrackFields[i].SetValue(r.Tracks, Mathf.Lerp(previous.Tracks[i], p.Tracks[i], factor));
                    r.Tracks.UpdateVisual();
                }

                for (int i = 0; i < r.Weapons.Length; i++)
                {
                    var weapon = r.Weapons[i];
                    if (weapon == null)
                        continue;
                    if (latest.WeaponShots[i] > r.WeaponShots[i])
                    {
                        ReplayShot(weapon);
                        r.SoundUntil[i] = Time.realtimeSinceStartup + 0.15f;
                        Log("VISUAL SHOT " + u.FriendlyName + " weapon=" + i + " count=" + latest.WeaponShots[i]);
                    }

                    if (r.SoundUntil[i] > 0 && Time.realtimeSinceStartup > r.SoundUntil[i])
                    {
                        if (weapon.SoundController != null)
                            weapon.SoundController.StopLoop();
                        if (weapon.WeaponSound != null)
                            weapon.WeaponSound.StopLoop();
                        if (weapon.UsesLoopingEffects)
                            foreach (var fx in weapon.MuzzleEffects)
                                if (fx != null)
                                    fx.Stop();
                        r.SoundUntil[i] = 0;
                    }

                    r.WeaponShots[i] = latest.WeaponShots[i];
                }

                Combat.Apply(latest.Id, u, latest.Fires);
                r.Shots = latest.Shots;
            }

            float now = Time.realtimeSinceStartup;
            if (statsStart == 0)
                statsStart = now;
            if (now - statsStart >= 5)
            {
                float elapsed = now - statsStart;
                Log("BUFFER localDelayMs=" + (replicaTimeline.LocalDelay * 1000).ToString("F1") +
                    " latestAgeMs=" + ((Time.realtimeSinceStartup - previousArrival) * 1000).ToString("F1"));
                Log("TIMING focused=" + Application.isFocused + " cap=" + Application.targetFrameRate + " size=" + Screen.width + "x" + Screen.height + " fps=" + (renderedFrames / elapsed).ToString("F1") + " snapshotsHz=" + (receivedFrames / elapsed).ToString("F1") + " worstFrameMs=" + (maxFrame * 1000).ToString("F1") + " worstReceiveGapMs=" + (maxArrivalGap * 1000).ToString("F1") + " starved=" + starvedFrames + "/" + renderedFrames);
                statsStart = now;
                receivedFrames = renderedFrames = starvedFrames = 0;
                maxFrame = maxArrivalGap = 0;
            }
        }

        static void ReplayShot(WeaponSystem weapon)
        {
            if (weapon.SoundController != null)
                weapon.SoundController.StartLoop();
            if (weapon.WeaponSound != null)
                weapon.WeaponSound.StartLoop();
            foreach (var fx in weapon.MuzzleEffects)
                if (fx != null)
                    fx.Play();
            var muzzle = weapon.MuzzleIdentity;
            if (muzzle != null)
                foreach (var prefab in weapon.MuzzleEffectPrefabs)
                    if (prefab != null)
                    {
                        var fx = UnityEngine.Object.Instantiate(prefab, muzzle.position, muzzle.rotation);
                        UnityEngine.Object.Destroy(fx, 15);
                    }
        }
    }
}

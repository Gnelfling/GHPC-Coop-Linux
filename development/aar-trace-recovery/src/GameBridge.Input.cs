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
        public Message ReadInput(long seq, bool reload, bool firePressed)
        {
            var p = PlayerInput.Instance;
            var w = p.CurrentPlayerUnit == Vehicles[LocalId].Unit ? p.CurrentPlayerWeapon : null;
            var aim = w != null && w.FCS != null ? w.FCS.AimWorldVector : p.CurrentPlayerUnit.transform.forward;
            if (aim.sqrMagnitude < 0.1f)
                aim = p.CurrentPlayerUnit.transform.forward;
            aim.Normalize();
            bool live = p.CurrentPlayerUnit == Vehicles[LocalId].Unit && p.AllowPlayerStrictLiveInput && !p.IsMenuOverridingAction;
            if (Time.realtimeSinceStartup >= nextInputLog &&
                (Input.GetKey(KeyCode.W) ||
                Input.GetKey(KeyCode.S) ||
                Input.GetMouseButton(0)))
            {
                nextInputLog = Time.realtimeSinceStartup + 2;
                Log("INPUT live=" + live + " strict=" + p.AllowPlayerStrictLiveInput + " menu=" + p.IsMenuOverridingAction + " focused=" + Application.isFocused + " fire=" + Input.GetMouseButton(0) + " pause=" + GHPC.State.TimeController.Paused + " scale=" + Time.timeScale);
            }

            var rec = Vehicles[LocalId];
            var muzzle = w != null && w.Weapon != null ? w.Weapon.MuzzleIdentity : null;
            var mq = muzzle != null ? muzzle.rotation : rec.Unit.RootTransform.rotation;
            var mp = muzzle != null ? muzzle.position : rec.Unit.RootTransform.position;
            if (firePressed && w != null && w.Weapon != null)
                Log("SHOT INPUT seq=" + seq + " unit=" + rec.Unit.UniqueName + " range=" + (w.FCS == null ? 0 : w.FCS.CurrentRange) + " weaponAmmo=" + (w.Weapon.CurrentAmmoType == null ? "none" : w.Weapon.CurrentAmmoType.Name) + " fcsAmmo=" + (w.FCS == null ||
                    w.FCS.CurrentAmmoType == null ? "none" : w.FCS.CurrentAmmoType.Name) + " muzzle=" + (mq * Vector3.forward).ToString("F5") + " aim=" + aim.ToString("F5"));
            return new Message
            {
                Kind = Kind.Input,
                Text = "reload:" + GHPC.Utility.SaveLoadUtility.PlayerConfigData.ReloadMode + ";auto:" + (w != null && w.Weapon != null && w.Weapon.Feed != null && w.Weapon.Feed.AutoReloadSwitchedOn ? "1" : "0") + ";clip:" + AmmoSync.NextClipIndex(w == null ? null : w.Weapon),
                Sequence = seq,
                Unit = LocalId,
                Poses = new[]
                {
                    new Pose
                    {
                        Id = LocalId,
                        X = mp.x,
                        Y = mp.y,
                        Z = mp.z,
                        Qx = mq.x,
                        Qy = mq.y,
                        Qz = mq.z,
                        Qw = mq.w,
                        Equipment = rec.Equipment.Input(),
                        Mounts = rec.Mounts.Select(m =>
                        {
                            var q = MountTransform(m).localRotation;
                            return new Rotation
                            {
                                X = q.x,
                                Y = q.y,
                                Z = q.z,
                                W = q.w
                            };
                        }).ToArray()
                    }
                },
                Throttle = live ? ((Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0)) : 0,
                Steer = live ? ((Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0)) : 0,
                AimX = aim.x,
                AimY = aim.y,
                AimZ = aim.z,
                Range = w != null && w.FCS != null ? Mathf.Clamp(w.FCS.CurrentRange, 0, 20000) : 0,
                Role = w != null ? (int)w.Role : 0,
                Fire = live && (firePressed || Input.GetMouseButton(0)),
                Reload = live && reload
            };
        }

        public void ReceiveInput(Message m)
        {
            if (m.Unit != GuestId ||
                m.Poses.Length != 1 ||
                m.Poses[0].Id != GuestId ||
                m.Poses[0].Mounts.Length != Vehicles[GuestId].Mounts.Length)
                throw new InvalidOperationException("Wrong vehicle");
            remoteStart = Vehicles[GuestId].Mounts.Select(x => MountTransform(x).localRotation).ToArray();
            int preference;
            if (m.Text != null &&
                m.Text.StartsWith("reload:", StringComparison.Ordinal) &&
                int.TryParse(m.Text.Substring(7).Split(';')[0], out preference) &&
                preference >= 0 &&
                preference <= 2)
                RemoteReloadSettings.Preferences[Vehicles[GuestId].Unit.CrewManager] = preference;
            var ammoGunner = Vehicles[GuestId].Unit.CrewManager.GetCrewBrain(CrewPosition.Gunner);
            if (ammoGunner != null && ammoGunner.WeaponsModule != null)
                AmmoSync.ApplyNextClip(ammoGunner.WeaponsModule.Weapons, m.Role, m.Text);
            shotInputs.Receive(m, Time.realtimeSinceStartup);
            command = m;
            received = Time.realtimeSinceStartup;
            pendingReload |= m.Reload;
            Vehicles[GuestId].Equipment.ApplyInput(m.Poses[0].Equipment);
        }

        public void RenderRemoteMounts(bool exact = false)
        {
            if (ReplicaActive || GHPC.AarController.InAar || GuestId == null || command == null || remoteStart == null)
                return;
            var mounts = Vehicles[GuestId].Mounts;
            float t = exact ? 1f : Mathf.Clamp01((Time.realtimeSinceStartup - received) / 0.05f);
            for (int i = 0; i < mounts.Length; i++)
            {
                // Detached mounts belong to wreck physics, not remote aiming.
                if (mounts[i].IsDetached)
                    continue;
                var q = command.Poses[0].Mounts[i];
                var rot = Quaternion.Slerp(remoteStart[i], new Quaternion(q.X, q.Y, q.Z, q.W), t);
                mounts[i].LocalRotation = rot;
                var mt = MountTransform(mounts[i]);
                mt.localRotation = rot;
                var target = new Quaternion(q.X, q.Y, q.Z, q.W);
                ProjectedGoal.SetValue(mounts[i], (mt.parent != null ? mt.parent.rotation * target : target) * Vector3.forward);
            }
        }

        public void DriveRemote()
        {
            if (GuestId == null)
                return;
            var u = Vehicles[GuestId].Unit;
            if (u == null)
                return;
            var driver = u.CrewManager.GetCrewBrain(CrewPosition.Driver) as IDriverBrain;
            bool fresh = command != null &&
                Time.realtimeSinceStartup - received < 0.5f &&
                !u.Destroyed &&
                !u.UnitIncapacitated &&
                !u.Abandoned;
            float gas = fresh && !u.CannotMove ? command.Throttle : 0, steer = fresh && !u.CannotMove ? command.Steer : 0;
            if (driver != null)
            {
                ApplyingRemoteDrive = true;
                try
                {
                    driver.ForceSpeed((gas < 0 ? u.Chassis.MinReverseSpeed : u.Chassis.MaxForwardSpeed) * gas * gas, gas < 0);
                    driver.ForceSteerAmount(steer);
                }
                finally
                {
                    ApplyingRemoteDrive = false;
                }
            }

            var nc = u.Chassis as NwhChassis;
            if (nc != null && (gas != 0 || steer != 0) && nc.Rigidbody != null && nc.Rigidbody.constraints != RigidbodyConstraints.None)
                nc.Unfreeze(3, false);
            if (fresh && command.Throttle != 0 && nc != null && Time.realtimeSinceStartup > nextDriveLog)
            {
                var vc = nc.VehicleController;
                Log("PHYSICS active=" + vc.Active + " enabled=" + vc.enabled + " gas=" + vc.input.Vertical + " handbrake=" + vc.input.Handbrake + " cruise=" + vc.drivingAssists.cruiseControl.enabled + " target=" + vc.drivingAssists.cruiseControl.targetSpeed + " constraints=" + nc.Rigidbody.constraints + " steering=" + vc.input.Horizontal + " fixed=" + Time.fixedDeltaTime);
            }

            if (fresh && command.Throttle != 0 && Time.realtimeSinceStartup > nextDriveLog)
            {
                nextDriveLog = Time.realtimeSinceStartup + 2;
                Log("DRIVE gas=" + gas + " intended=" + u.Chassis.IntendedSpeed + " actual=" + u.Chassis.ForwardVelocity + " rpm=" + u.Chassis.CurrentRpm + " gear=" + u.Chassis.CurrentGear + " cannotMove=" + u.CannotMove + " suspended=" + u.CrewManager.GetCrewBrain(CrewPosition.Driver).Suspended + " scale=" + Time.timeScale + " paused=" + GHPC.State.TimeController.Paused + " kinematic=" + u.Chassis.Rigidbody.isKinematic);
            }
        }

        // Weapon trigger release is counted in rendered frames, so firing must run in Update.
        public void FireRemote()
        {
            var latest = command;
            command = shotInputs.Select(latest, Time.realtimeSinceStartup);
            try
            {
                FireRemoteCore();
            }
            finally
            {
                command = latest;
            }
        }

        void FireRemoteCore()
        {
            if (GuestId == null)
                return;
            var u = Vehicles[GuestId].Unit;
            if (u == null)
                return;
            bool fresh = command != null &&
                Time.realtimeSinceStartup - received < 0.5f &&
                !u.Destroyed &&
                !u.UnitIncapacitated &&
                !u.Abandoned;
            var gunner = u.CrewManager.GetCrewBrain(CrewPosition.Gunner);
            if (gunner == null || gunner.WeaponsModule == null)
                return;
            bool requestedFire = fresh && command.Fire;
            var wm = gunner.WeaponsModule;
            var role = fresh ? (WeaponSystemRole)command.Role : WeaponSystemRole.MainGun;
            foreach (var other in wm.Weapons)
                if (other.Weapon != null && (!fresh || other.Role != role))
                    other.Weapon.StopFiring();
            var chosen = wm.Weapons.FirstOrDefault(x => x.Role == role && x.Weapon != null);
            if (chosen == null)
                return;
            if (fresh && !u.CannotShoot)
            {
                wm.ActiveWeapon = chosen;
                wm.SetCurrentWeaponRange(command.Range);
                if (chosen.Weapon.FCS != null)
                    chosen.Weapon.FCS.SetAimVector(new Vector3(command.AimX, command.AimY, command.AimZ), true);
                RenderRemoteMounts(true);
                if (pendingReload)
                {
                    if (chosen.Weapon.Feed != null && !chosen.Weapon.Feed.Reloading && !chosen.Weapon.Feed.Cycling)
                    {
                        chosen.Weapon.Feed.Reload();
                        pendingReload = false;
                        Log("RELOAD requested role=" + role + " reloading=" + chosen.Weapon.Feed.Reloading + " reserve=" + chosen.Weapon.Feed.ReserveCount);
                    }

                }

                var feed = chosen.Weapon.Feed as AmmoFeed;
                AmmoSync.RecoverRemoteReload(feed, u.CrewManager);
                if (feed != null && Time.realtimeSinceStartup > nextAmmoLog)
                {
                    nextAmmoLog = Time.realtimeSinceStartup + 2;
                    if (chosen.Weapon.TriggerHoldTime > 0)
                    {
                        var launcher = chosen.Weapon;
                        Log("LAUNCHER unit=" + u.UniqueName + " enabled=" + launcher.enabled + " guidanceBlocked=" + launcher.BlockedByMissileGuidance + " cycleRemaining=" + AccessTools.Field(typeof(WeaponSystem), "_cycleTimeRemaining").GetValue(launcher) + " breechDamaged=" + AccessTools.Field(typeof(WeaponSystem), "_breechDamaged").GetValue(launcher) + " barrelDamaged=" + AccessTools.Field(typeof(WeaponSystem), "_barrelDamaged").GetValue(launcher) + " held=" + AccessTools.Field(typeof(WeaponSystem), "_triggerHeldTime").GetValue(launcher));
                    }

                    Log("AMMO role=" + role + " able=" + chosen.Weapon.AbleToFire + " gate=" + (chosen.Weapon.FCS != null &&
                        chosen.Weapon.FCS.FiringBlocked) + " fire=" + command.Fire + " suspended=" + gunner.Suspended + " aimLocked=" + wm.AimLockedOutManually + " loaded=" + (feed.AmmoTypeInBreech != null) + " reload=" + feed.Reloading + " cycle=" + feed.Cycling + " reserve=" + feed.ReserveCount + " doctrine=" + feed.ReloadMode + " autoSwitch=" + feed.AutoReloadSwitchedOn + " pause=" + feed.ForcePauseReload + " restock=" + feed.WaitingOnRestock + " enabled=" + feed.enabled + " elapsed=" + AccessTools.Field(typeof(AmmoFeed), "_clipFeedTime").GetValue(feed) + " total=" + feed.TotalReloadTime + " stage=" + AccessTools.Field(typeof(AmmoFeed), "_clipFeedStage").GetValue(feed));
                }

                if (requestedFire)
                {
                    int before = Vehicles[GuestId].Shots;
                    var muzzle = chosen.Weapon.MuzzleIdentity;
                    var pose = command.Poses[0];
                    var rotation = new Quaternion(pose.Qx, pose.Qy, pose.Qz, pose.Qw);
                    // Use the guest's actual barrel direction (including elevation), not the sight
                    // aim vector or an interpolated host mount. Keep authoritative muzzle position.
                    var saved = muzzle != null ? muzzle.rotation : Quaternion.identity;
                    try
                    {
                        if (muzzle != null)
                        {
                            if (Time.realtimeSinceStartup > nextShotLog)
                            {
                                nextShotLog = Time.realtimeSinceStartup + 2;
                                Log("SHOT alignment role=" + role + " correctionDeg=" + Quaternion.Angle(muzzle.rotation, rotation.normalized));
                            }

                            muzzle.rotation = rotation.normalized;
                        }

                        Log("SHOT STATE unit=" + u.UniqueName + " input=" + command.Sequence + " range=" + command.Range + " weaponAmmo=" + (chosen.Weapon.CurrentAmmoType == null ? "none" : chosen.Weapon.CurrentAmmoType.Name) + " fcsAmmo=" + (chosen.Weapon.FCS == null ||
                            chosen.Weapon.FCS.CurrentAmmoType == null ? "none" : chosen.Weapon.FCS.CurrentAmmoType.Name));
                        ShotAudit.Begin(GuestId, command.Sequence, rotation.normalized * Vector3.forward);
                        FireMethod.Invoke(wm, new object[] { role, u });
                    }
                    finally
                    {
                        ShotAudit.End();
                        if (muzzle != null)
                            muzzle.rotation = saved;
                    }

                    if (Vehicles[GuestId].Shots > before)
                        shotInputs.Fired();
                }
                else
                {
                    chosen.Weapon.StopFiring();
                    wm.StopFiring(role);
                }
            }
            else
                foreach (var w in wm.Weapons)
                    if (w.Weapon != null)
                        w.Weapon.StopFiring();
        }
    }
}

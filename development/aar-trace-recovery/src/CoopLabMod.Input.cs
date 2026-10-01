using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Net;
using System.Security.Cryptography;
using MelonLoader;
using UnityEngine;
using GHPC;
using GHPC.Player;

namespace GhpcCoop
{
    public sealed partial class CoopLabMod
    {
        public override void OnFixedUpdate()
        {
            if (hosting && claimed)
                Guard(delegate
                {
                    if (multiRoom != null)
                        RunPeerPhase("drive", game.DrivePeer);
                    else if (!directResync.Pending)
                        game.DriveRemote();
                });
        }

        bool guestInputDue;
        int lastGuestThrottleKeys, lastGuestSteerKeys;

        // Raw key state only decides when to send; ReadInput still applies the live/menu gates.
        bool GuestDriveKeysChanged()
        {
            int throttle = (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0);
            int steer = (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0);
            bool changed = throttle != lastGuestThrottleKeys || steer != lastGuestSteerKeys;
            lastGuestThrottleKeys = throttle;
            lastGuestSteerKeys = steer;
            return changed;
        }
        void SendGuestInputLate()
        {
            if (!guestInputDue)
                return;
            guestInputDue = false;
            if (hosting || !claimed || recoveringReplica || link == null || pendingVehicle != "")
                return;
            float now = Time.realtimeSinceStartup;
            var input = game.ReadInput(++seq, reload, firePressed);
            if (autoDrive && now - claimedAt < 90)
            {
                float t = now - claimedAt;
                input.Throttle = t < 8 ? 1f : 0;
                input.Steer = 0;
                input.Fire = t > 12 && t < 85;
                input.Reload = t > 20 && ((int)t % 12) == 0;
                input.AimX = testAim.x;
                input.AimY = testAim.y;
                input.AimZ = testAim.z;
                input.Range = 0;
                input.Role = 0;
                var testRotation = Quaternion.LookRotation(testAim);
                input.Poses[0].Qx = testRotation.x;
                input.Poses[0].Qy = testRotation.y;
                input.Poses[0].Qz = testRotation.z;
                input.Poses[0].Qw = testRotation.w;
            }

            if (reloadProbe && now - claimedAt < 30)
            {
                input.Throttle = input.Steer = 0;
                input.Fire = now - claimedAt > 2;
                input.Role = 0;
            }

            if (aimTestFire)
            {
                input.Fire = true;
                input.Reload = true;
            }

            if (panel || hostPaused)
            {
                input.Throttle = input.Steer = 0;
                input.Fire = input.Reload = false;
            }

            // A UI click must not become a shot after the menu closes in this frame.
            if (GuestMenuBlocksFire() || suppressFireUntilRelease)
            {
                suppressFireUntilRelease = true;
                input.Fire = false;
            }

            input.SyncRevision = acceptedRevision;
            input.SyncBaseline = appliedBaselineSequence;
            link.Send(input);
            reload = firePressed = false;
        }

        public override void OnLateUpdate()
        {
            Guard(RunAimTest);
            Guard(SendGuestInputLate);
            if (claimed && !hosting && !GHPC.AarController.InAar)
                Guard(helicopters.Render);
            if (claimed && !hosting && !GHPC.AarController.InAar)
                Guard(infantry.Render);
            RecordAimDiagnostic();
            if (claimed && !hosting && tracers != null)
                tracers.Render();
            if (claimed)
                Guard(RunNetworkVerification);
            if (hosting && claimed)
                Guard(delegate
                {
                    if (multiRoom != null)
                        RunPeerPhase("mount", game.RenderPeer);
                    else if (!directResync.Pending)
                        game.RenderRemoteMounts();
                });
            if (!hosting && claimed)
                Guard(delegate
                {
                    if (!autoDrive)
                        return;
                    var r = game.Vehicles[game.LocalId];
                    var delta = r.Unit.RootTransform.position - testStart;
                    delta.y = 0;
                    if (driveDistance < 0 && Time.realtimeSinceStartup - claimedAt > 9)
                        driveDistance = delta.magnitude;
                    if (!reportWritten && Time.realtimeSinceStartup - claimedAt > 22)
                    {
                        reportWritten = true;
                        var inv = System.Globalization.CultureInfo.InvariantCulture;
                        var report = "{\"automated_input_probe\":true,\"snapshot_sequence\":" + lastSnapshot + ",\"planar_distance_before_fire\":" + driveDistance.ToString("F3", inv) + ",\"confirmed_shots\":" + r.Shots + ",\"max_replica_error_metres\":" + game.ReplicaError().ToString("F3", inv) + "}";
                        File.WriteAllText(Path.Combine(DataDir, "replica-report.json"), report);
                        GameBridge.Log(report);
                    }
                });
        }
    }
}

using GHPC;
using GHPC.Player;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GhpcCoop
{
    public sealed partial class CoopLabMod
    {
        Message CaptureAarView()
        {
            var controller = AarController.Instance;
            var segments = new List<TracerState>();
            string description = "Host-controlled AAR";
            int index = (int)AccessTools.Field(typeof(AarController), "_aarShotIndex").GetValue(controller);
            var shots = (List<GHPC.Weapons.ShotInfo>)AccessTools.Field(typeof(AarController), "CurrentAarShots").GetValue(controller);
            if (shots != null && index >= 0 && index < shots.Count)
            {
                var shot = shots[index];
                description += " | Shot " + (index + 1) + "/" + shots.Count;
                if (shot.TypeInfo != null)
                    description += " | " + shot.TypeInfo.Name;
                description += shot.IsKillShot ? " | Kill" : shot.IsHit ? " | Hit" : " | Miss";
                var previous = shot.StartPosition;
                foreach (var frame in shot.AllShotFrames.Take(512))
                {
                    var position = frame.ParentTransform == null ? frame.WorldPosition : frame.ParentTransform.TransformPoint(frame.LocalPosition);
                    segments.Add(new TracerState { Id = segments.Count,
                        Duration = 1,
                        X = previous.x,
                        Y = previous.y,
                        Z = previous.z,
                        EndX = position.x,
                        EndY = position.y,
                        EndZ = position.z });
                    previous = position;
                }
            }

            return new Message
            {
                Kind = Kind.AarView,
                Sequence = ++seq,
                Poses = game.Snapshot(),
                Tracers = segments.ToArray(),
                Text = description
            };
        }

        void ReceiveAarView(Message view)
        {
            if (!claimed || hosting || hostSessionState != "host-aar")
                return;
            EnterGuestAar();
            if (!AarController.InAar)
                return;
            // These are the host's currently displayed replay transforms. Do not
            // run live interpolation or apply damage while presenting an AAR frame.
            foreach (var pose in view.Poses)
            {
                VehicleRecord vehicle;
                if (!game.Vehicles.TryGetValue(pose.Id, out vehicle) || vehicle.Unit == null || pose.Mounts.Length != vehicle.Mounts.Length)
                    continue;
                vehicle.Unit.RootTransform.SetPositionAndRotation(new Vector3(pose.X, pose.Y, pose.Z), new Quaternion(pose.Qx, pose.Qy, pose.Qz, pose.Qw));
                for (int mountIndex = 0; mountIndex < vehicle.Mounts.Length; mountIndex++)
                {
                    if (vehicle.Mounts[mountIndex].IsDetached)
                        continue;
                    var rotation = pose.Mounts[mountIndex];
                    vehicle.Mounts[mountIndex].LocalRotation = new Quaternion(rotation.X, rotation.Y, rotation.Z, rotation.W);
                    GameBridge.MountTransform(vehicle.Mounts[mountIndex]).localRotation = vehicle.Mounts[mountIndex].LocalRotation;
                }
            }

            if (aarLineMaterial == null)
                aarLineMaterial = new Material(Shader.Find("Sprites/Default"));
            while (aarLines.Count < view.Tracers.Length)
            {
                var lineObject = new GameObject("Coop AAR trajectory");
                var line = lineObject.AddComponent<LineRenderer>();
                line.sharedMaterial = aarLineMaterial;
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.startWidth = line.endWidth = .025f;
                line.startColor = line.endColor = Color.red;
                aarLines.Add(lineObject);
            }

            for (int index = 0; index < aarLines.Count; index++)
            {
                bool visible = index < view.Tracers.Length;
                aarLines[index].SetActive(visible);
                if (!visible)
                    continue;
                var segment = view.Tracers[index];
                var line = aarLines[index].GetComponent<LineRenderer>();
                line.SetPosition(0, new Vector3(segment.X, segment.Y, segment.Z));
                line.SetPosition(1, new Vector3(segment.EndX, segment.EndY, segment.EndZ));
            }

            status = view.Text;
            AccessTools.Field(typeof(AarController), "_aarText").SetValue(AarController.Instance, view.Text);
        }

        void ClearAarView()
        {
            foreach (var line in aarLines)
                if (line != null)
                    UnityEngine.Object.Destroy(line);
            aarLines.Clear();
            if (aarLineMaterial != null)
                UnityEngine.Object.Destroy(aarLineMaterial);
            aarLineMaterial = null;
            nextAarView = 0;
        }
    }
}

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
        AarPresentation sharedAar;
        float nextAarFailureLog;
        static readonly System.Reflection.FieldInfo AarText = AccessTools.Field(typeof(AarController), "_aarText");

        Pose[] CaptureAarTransforms()
        {
            // AAR reception applies only transforms. Avoid collecting live damage,
            // ammo, audio and fire state that would be serialized and then ignored.
            return game.Vehicles.Values.Where(vehicle => vehicle.Unit != null).Select(vehicle =>
            {
                var root = vehicle.Unit.RootTransform;
                var position = root.position;
                var rotation = root.rotation;
                return new Pose
                {
                    Id = vehicle.Id,
                    X = position.x, Y = position.y, Z = position.z,
                    Qx = rotation.x, Qy = rotation.y, Qz = rotation.z, Qw = rotation.w,
                    Mounts = vehicle.Mounts.Select(mount =>
                    {
                        var local = GameBridge.MountTransform(mount).localRotation;
                        return new Rotation { X = local.x, Y = local.y, Z = local.z, W = local.w };
                    }).ToArray()
                };
            }).ToArray();
        }

        Message CaptureAarView()
        {
            var view = AarPresentation.Capture(CaptureAarTransforms());
            view.Sequence = ++seq;
            return view;
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

            if (view.SupportId == AarPresentation.Format)
            {
                if (sharedAar == null) sharedAar = new AarPresentation();
                try
                {
                    sharedAar.Apply(view);
                    status = "Viewing the host's selected AAR shot.";
                }
                catch (Exception error)
                {
                    // Presentation failure must not tear down the transport/session.
                    // The geometry cache commits only on success, so the next packet retries.
                    status = "AAR display is retrying.";
                    if (Time.realtimeSinceStartup >= nextAarFailureLog)
                    {
                        nextAarFailureLog = Time.realtimeSinceStartup + 5;
                        GameBridge.Log("AAR presentation retry: " + error);
                    }
                }
                return;
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
                aarLines.Add(line);
            }

            for (int index = 0; index < aarLines.Count; index++)
            {
                bool visible = index < view.Tracers.Length;
                if (aarLines[index].gameObject.activeSelf != visible)
                    aarLines[index].gameObject.SetActive(visible);
                if (!visible)
                    continue;
                var segment = view.Tracers[index];
                var line = aarLines[index];
                line.SetPosition(0, new Vector3(segment.X, segment.Y, segment.Z));
                line.SetPosition(1, new Vector3(segment.EndX, segment.EndY, segment.EndZ));
            }

            status = view.Text;
            AarText.SetValue(AarController.Instance, view.Text);
        }

        void ClearAarView()
        {
            if (sharedAar != null) sharedAar.Dispose();
            sharedAar = null;
            foreach (var line in aarLines)
                if (line != null)
                    UnityEngine.Object.Destroy(line.gameObject);
            aarLines.Clear();
            if (aarLineMaterial != null)
                UnityEngine.Object.Destroy(aarLineMaterial);
            aarLineMaterial = null;
            nextAarView = 0;
        }
    }
}

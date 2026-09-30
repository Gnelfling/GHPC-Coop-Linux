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
        InfantrySync infantry = new InfantrySync();
        float nextInfantry;
        bool hostPaused;
        string hostSessionState = "host-running";
        float nextPauseNotice;
        float nextAarView;
        // Retain renderer references so replay updates avoid component lookups.
        readonly List<LineRenderer> aarLines = new List<LineRenderer>();
        Material aarLineMaterial;
        bool NotifyHostPause(float now)
        {
            string state = AarController.InAar ? "host-aar" : GHPC.State.TimeController.Paused ? "host-paused" : "host-running";
            bool paused = state != "host-running";
            if (state != hostSessionState || now >= nextPauseNotice)
            {
                hostSessionState = state;
                hostPaused = paused;
                nextPauseNotice = now + 1;
                Broadcast(new Message { Kind = Kind.Ping, Text = state });
            }

            if (paused)
                status = state == "host-aar" ? "Mission ended. Viewing AAR." : "Host paused.";
            if (state == "host-aar" && now >= nextAarView)
            {
                nextAarView = now + .25f;
                Broadcast(CaptureAarView());
            }

            return paused;
        }

        void ReceiveHostPause(string state)
        {
            if (state != "host-aar" && state != "host-paused" && state != "host-running")
                return;
            bool paused = state != "host-running";
            if (state != hostSessionState)
            {
                hostSessionState = state;
                hostPaused = paused;
                reload = firePressed = false;
                status = paused ? "Host paused. Waiting for the host to resume." : "Host resumed. Vehicle controls are active.";
                GameBridge.Log("SESSION guest state=" + state);
            }

            // AAR is an end-of-mission transition, not an ordinary pause. A pause
            // can change to AAR without changing the boolean control lock.
            if (state == "host-aar")
                EnterGuestAar();
        }

        void EnterGuestAar()
        {
            if (AarController.InAar)
                return;
            var player = PlayerInput.Instance;
            var controller = AarController.Instance;
            if (player == null || !player.IsInitialized || controller == null)
                return;
            panel = false;
            if (controller.ShotCount > 0)
            {
                AccessTools.Method(typeof(PlayerInput), "StartAARMode").Invoke(player, null);
            }
            else
            {
                // Replicas may have no locally recorded ballistic shots. The native
                // AAR controller supports an empty history, unlike StartAARMode.
                // Do not invent hit records just to satisfy its non-empty check.
                var camera = GHPC.Camera.CameraManager.Instance;
                if (camera != null)
                    camera.ForceDetachCamera(0f);
                AarController.StartAar();
            }

            status = "Mission ended. Waiting for the host-controlled AAR view.";
            GameBridge.Log("SESSION guest entered AAR localShots=" + controller.ShotCount);
        }
    }
}

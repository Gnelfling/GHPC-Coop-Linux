using System;
using System.Collections.Generic;
using System.Text;
using GHPC;
using HarmonyLib;
using UnityEngine;

namespace GhpcCoop
{
    // Replays the host's visible AAR objects using native prefabs, including spall and shaped-charge jets.
    // This is a shared view of the host's selected shot, not a second local ballistic simulation.
    sealed class AarPresentation : IDisposable
    {
        public const string Format = "coop-native-aar-v1";
        readonly List<GameObject> traces = new List<GameObject>();
        string shotText = "", storyText = "";
        GUIStyle shotStyle, storyStyle;
        bool initializedView;
        TracerState[] previousSegments;
        readonly List<Renderer> traceRenderers = new List<Renderer>();
        bool checkedMeshes;
        float nextDiagnostic;

        static string BoundedText(object value)
        {
            string text = value as string ?? "";
            while (Encoding.UTF8.GetByteCount(text) > 3900)
                text = text.Substring(0, Math.Max(0, text.Length - 32));
            return text;
        }

        public static Message Capture(Pose[] transforms)
        {
            var controller = AarController.Instance;
            var objects = (List<GameObject>)AccessTools.Field(typeof(AarController), "_shotTraces").GetValue(controller);
            var segments = new List<TracerState>();
            foreach (var obj in objects)
            {
                if (obj == null || !obj.activeInHierarchy) continue;
                var visual = obj.GetComponent<ShotTraceVisual>();
                if (visual == null || visual.EndPoints == null) continue;
                foreach (var end in visual.EndPoints)
                {
                    if (segments.Count == 512) break;
                    // AAR-only encoding: flags select native trace style, width is radius,
                    // and duration carries normalized spall power (kept above the wire minimum).
                    segments.Add(new TracerState {
                        Id = segments.Count,
                        Visual = (visual.IsSpall ? 1 : 0) | (visual.IsJet ? 2 : 0) | (visual.IsAltColor ? 4 : 0),
                        WireWidth = Mathf.Clamp(visual.radius, 0, 1),
                        Duration = .0001f + Mathf.Clamp01(visual.PowerRatio) * .9999f,
                        X = visual.StartPoint.x, Y = visual.StartPoint.y, Z = visual.StartPoint.z,
                        EndX = end.x, EndY = end.y, EndZ = end.z
                    });
                }
            }
            // Camera pose/FOV belong to each viewer, never to the shared replay.
            var packet = new Message {
                Kind = Kind.AarView, SupportId = Format, Poses = transforms, Tracers = segments.ToArray(),
                Text = BoundedText(AccessTools.Field(typeof(AarController), "_aarText").GetValue(controller)),
                Roster = BoundedText(AccessTools.Field(typeof(AarController), "_aarShotStory").GetValue(controller)),
                Fire = AarController.ShowXray
            };
            var sphere = (GameObject)AccessTools.Field(typeof(AarController), "_overpressureSphere").GetValue(controller);
            if (sphere != null && sphere.activeInHierarchy)
            {
                packet.SupportAccepted = true;
                packet.SupportX = sphere.transform.position.x;
                packet.SupportY = sphere.transform.position.y;
                packet.SupportZ = sphere.transform.position.z;
                packet.Range = sphere.transform.localScale.x * .5f;
            }
            return packet;
        }

        public void Apply(Message packet)
        {
            var controller = AarController.Instance;
            shotText = packet.Text;
            storyText = packet.Roster;
            // Initialize the display once; later packets must not undo local X-ray input.
            if (!initializedView)
            {
                AarController.ShowXray = packet.Fire;
                initializedView = true;
            }
            AccessTools.Field(typeof(AarController), "_aarText").SetValue(controller, packet.Text);
            AccessTools.Field(typeof(AarController), "_aarShotStory").SetValue(controller, packet.Roster);
            // Updating the backing string does not update Unity's existing text box.
            // Guests with local history also need the selected host shot's hit information.
            if (controller.ShotInfoTextBox != null) controller.ShotInfoTextBox.text = packet.Text;
            var storyBox = AccessTools.Field(typeof(AarController), "ShotStoryTextBox").GetValue(controller);
            if (storyBox != null)
                AccessTools.Property(storyBox.GetType(), "text").SetValue(storyBox, packet.Roster, null);
            if (packet.SupportAccepted)
            {
                // The native argument is TNT mass, not radius. Initialize its object,
                // then copy the captured size directly rather than applying blast scaling twice.
                AccessTools.Method(typeof(AarController), "ShowOverpressureSphere").Invoke(controller,
                    new object[] { new Vector3(packet.SupportX, packet.SupportY, packet.SupportZ), 1f });
                var sphere = (GameObject)AccessTools.Field(typeof(AarController), "_overpressureSphere").GetValue(controller);
                if (sphere != null) sphere.transform.localScale = Vector3.one * packet.Range * 2f;
            }
            else
                AccessTools.Method(typeof(AarController), "HideOverpressureSphere").Invoke(controller, null);

            // Native trace meshes are generated on creation; rebuild only when the geometry changes.
            if (SameGeometry(previousSegments, packet.Tracers) && RestoreTraces()) return;
            ClearTraces();
            AarController.ClearShotTraces();
            var prefab = (GameObject)AccessTools.Field(typeof(AarController), "ShotTracePrefab").GetValue(controller);
            foreach (var segment in packet.Tracers)
            {
                // These endpoints are world coordinates. Keep the replicated geometry
                // outside the controller's hierarchy, whose visibility/scale is local.
                var obj = UnityEngine.Object.Instantiate(prefab);
                obj.transform.SetParent(null, false);
                obj.transform.localScale = Vector3.one;
                obj.name = "Co-op shared AAR trace";
                // Track ownership immediately so a failed initialization is cleaned on retry.
                traces.Add(obj);
                var visual = obj.GetComponent<ShotTraceVisual>();
                visual.StartPoint = new Vector3(segment.X, segment.Y, segment.Z);
                visual.EndPoints = new[] { new Vector3(segment.EndX, segment.EndY, segment.EndZ) };
                visual.IsSpall = (segment.Visual & 1) != 0;
                visual.IsJet = (segment.Visual & 2) != 0;
                visual.IsAltColor = (segment.Visual & 4) != 0;
                visual.radius = segment.WireWidth;
                visual.PowerRatio = Mathf.Clamp01((segment.Duration - .0001f) / .9999f);
                obj.transform.position = visual.EndPoints[0];
                visual.enabled = true;
                obj.SetActive(true);
            }
            previousSegments = packet.Tracers;
            checkedMeshes = false;
            if (Time.realtimeSinceStartup >= nextDiagnostic)
            {
                GameBridge.Log("AAR replica received segments=" + packet.Tracers.Length + " created=" + traces.Count);
                nextDiagnostic = Time.realtimeSinceStartup + 5;
            }
        }

        bool RestoreTraces()
        {
            if (traces.Count != previousSegments.Length) return false;
            foreach (var obj in traces)
            {
                if (obj == null) return false;
                if (!obj.activeSelf) obj.SetActive(true);
            }
            // Start creates the native meshes on the next Unity frame. Inspect once
            // on the next packet, not every frame or before Start has had a chance to run.
            if (!checkedMeshes)
            {
                traceRenderers.Clear();
                foreach (var obj in traces)
                    traceRenderers.AddRange(obj.GetComponentsInChildren<Renderer>(true));
                checkedMeshes = true;
                GameBridge.Log("AAR replica meshes=" + traceRenderers.Count + " segments=" + traces.Count);
            }
            foreach (var renderer in traceRenderers)
            {
                if (renderer == null) return false;
                renderer.enabled = true;
                renderer.gameObject.SetActive(true);
            }
            return traces.Count == 0 || traceRenderers.Count >= traces.Count;
        }

        public void DrawEmptyHistoryText()
        {
            // Native AAR may omit its labels when the replica recorded no local shots.
            // Show host-authored text without inventing guest ballistic history.
            if (!AarController.InAar || AarController.Instance == null || AarController.Instance.ShotCount != 0) return;
            if (shotStyle == null)
            {
                shotStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
                shotStyle.normal.textColor = Color.green;
                storyStyle = new GUIStyle(shotStyle);
                storyStyle.normal.textColor = Color.red;
            }
            var controller = AarController.Instance;
            if (controller.ShotInfoTextBox == null || !controller.ShotInfoTextBox.isActiveAndEnabled)
                GUI.Label(new Rect(20, Screen.height - 170, Screen.width * .48f, 160), shotText, shotStyle);
            if (controller.ShotStoryTextBox == null || !controller.ShotStoryTextBox.isActiveAndEnabled)
                GUI.Label(new Rect(Screen.width * .70f, 25, Screen.width * .29f, Screen.height - 40), storyText, storyStyle);
        }

        static bool SameGeometry(TracerState[] previous, TracerState[] current)
        {
            if (previous == null || previous.Length != current.Length) return false;
            for (int index = 0; index < current.Length; index++)
            {
                var a = previous[index];
                var b = current[index];
                if (a.X != b.X || a.Y != b.Y || a.Z != b.Z ||
                    a.EndX != b.EndX || a.EndY != b.EndY || a.EndZ != b.EndZ ||
                    a.WireWidth != b.WireWidth || a.Duration != b.Duration || a.Visual != b.Visual)
                    return false;
            }
            return true;
        }

        void ClearTraces()
        {
            foreach (var obj in traces) if (obj != null) UnityEngine.Object.Destroy(obj);
            traces.Clear();
            traceRenderers.Clear();
        }

        public void Dispose()
        {
            ClearTraces();
        }
    }
}

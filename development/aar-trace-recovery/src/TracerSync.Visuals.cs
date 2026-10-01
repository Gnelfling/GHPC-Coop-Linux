using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using GHPC.Weapons;
using GHPC.Effects;
using HarmonyLib;

namespace GhpcCoop
{
    public sealed partial class TracerSync
    {
        Visual CreateWire()
        {
            // Reuse the game's wire material (including its shader and texture),
            // rather than approximating the wire with a solid sprite line.
            var template = Resources.FindObjectsOfTypeAll<ATGMWire>()
                .Select(wire => wire.GetComponent<LineRenderer>())
                .FirstOrDefault(renderer => renderer != null && renderer.sharedMaterial != null);
            if (template == null)
                return null; // Retry on a later sample once native assets are available.

            var root = new GameObject("Coop guidance wire");
            root.layer = template.gameObject.layer;
            var line = root.AddComponent<LineRenderer>();
            line.sharedMaterials = template.sharedMaterials;
            line.colorGradient = template.colorGradient;
            line.alignment = template.alignment;
            line.textureMode = template.textureMode;
            line.numCapVertices = template.numCapVertices;
            line.numCornerVertices = template.numCornerVertices;
            line.shadowCastingMode = template.shadowCastingMode;
            line.receiveShadows = template.receiveShadows;
            line.generateLightingData = template.generateLightingData;
            line.sortingLayerID = template.sortingLayerID;
            line.sortingOrder = template.sortingOrder;
            line.useWorldSpace = true;
            // Native ATGMWire.Reset uses a uniform width. Receive applies the
            // host's current width without a minimum-size override.
            line.widthMultiplier = 1f;
            line.positionCount = 0;
            return new Visual
            {
                Object = root,
                Wire = line
            };
        }

        Visual Create(int type)
        {
            var m = LiveRoundMarshaller.Instance;
            if (m == null)
                return null;
            var info = m.LiveRoundVisuals.FirstOrDefault(x => (int)x.Type == type);
            if (info == null || info.Prefab == null)
                return null;
            var tracer = info.Prefab.GetComponentInChildren<DynamicTracer>(true);
            if (tracer == null || tracer.VisualTransform == null)
                return null;
            // Clone only the visual child, never a live projectile or damage component.
            // An inactive parent prevents Awake/OnEnable on the replica before
            // its native projectile behaviours have been removed.
            var root = new GameObject("Coop tracer visual");
            root.SetActive(false);
            var obj = UnityEngine.Object.Instantiate(tracer.VisualTransform.gameObject, root.transform, false);
            foreach (var b in obj.GetComponentsInChildren<MonoBehaviour>(true))
            {
                b.enabled = false;
                UnityEngine.Object.DestroyImmediate(b);
            }

            foreach (var c in obj.GetComponentsInChildren<Collider>(true))
                c.enabled = false;
            foreach (var body in obj.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.detectCollisions = false;
            }

            foreach (var source in obj.GetComponentsInChildren<AudioSource>(true))
                source.enabled = false;

            // Preserve the mesh's native orientation, but measure its actual extent. A
            // centered/offset mesh placed at the muzzle projects half its streak behind it.
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.Inverse(info.Prefab.transform.rotation) * tracer.VisualTransform.rotation;
            obj.transform.localScale = tracer.InitialLocalScale;
            obj.SetActive(true);
            // Bounds are measured only after all gameplay behaviours are gone.
            root.SetActive(true);
            var renderers = obj.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                UnityEngine.Object.Destroy(root);
                return null;
            }

            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
                bounds.Encapsulate(renderer.bounds);
            if (bounds.size.z < .00001f)
            {
                UnityEngine.Object.Destroy(root);
                return null;
            }

            root.SetActive(false);
            return new Visual
            {
                Object = root,
                Renderers = renderers,
                MeshLength = bounds.size.z,
                MeshFront = bounds.max.z,
                MinLength = tracer.MinimumLength,
                MaxLength = tracer.MaximumLength
            };
        }

        Visual CreateMissile(string ammoName)
        {
            var ammo = Resources.FindObjectsOfTypeAll<GHPC.Weaponry.AmmoCodexScriptable>().Select(codex => codex.AmmoType).Where(candidate => candidate != null).FirstOrDefault(candidate => candidate.Name == ammoName &&
                candidate.ShotVisual != null);
            if (ammo == null)
                return null;
            // Keep the clone inactive until native projectile scripts are removed.
            // This replica displays the host's path and never runs local ballistics.
            var root = new GameObject("Coop missile visual");
            root.SetActive(false);
            var model = UnityEngine.Object.Instantiate(ammo.ShotVisual, root.transform, false);
            foreach (var behaviour in model.GetComponentsInChildren<MonoBehaviour>(true))
            {
                behaviour.enabled = false;
                UnityEngine.Object.DestroyImmediate(behaviour);
            }

            foreach (var collider in model.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            foreach (var body in model.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.detectCollisions = false;
            }

            foreach (var source in model.GetComponentsInChildren<AudioSource>(true))
                source.enabled = false;
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.SetActive(true);
            return new Visual
            {
                Object = root,
                IsMissile = true
            };
        }

        // Defensive visibility check for replicated tracer meshes in optics views.
        // Test visibility from the camera actually rendering, rather than Camera.main (which
        // may be the external view while the player is looking through a thermal sight).
        // Only replicas are hidden: these rays never affect projectile physics or damage.
        static bool TracerPointVisible(Camera camera, Vector3 point)
        {
            var offset = point - camera.transform.position;
            float distance = offset.magnitude;
            if (distance <= camera.nearClipPlane)
                return true;
            var direction = offset / distance;
            var origin = camera.transform.position + direction * camera.nearClipPlane;
            return !Physics.Raycast(origin, direction, distance - camera.nearClipPlane,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        void BeforeTracerCamera(Camera camera)
        {
            if (camera == null || camera.cameraType != CameraType.Game)
                return;
            GeometryUtility.CalculateFrustumPlanes(camera, tracerFrustum);
            foreach (var visual in visuals.Values)
            {
                if (visual.Renderers == null || visual.Object == null || !visual.Object.activeInHierarchy)
                    continue;
                // Auxiliary cameras and off-screen rounds must not add physics queries.
                bool inView = false;
                foreach (var renderer in visual.Renderers)
                    if (renderer != null && (camera.cullingMask & (1 << renderer.gameObject.layer)) != 0 &&
                        GeometryUtility.TestPlanesAABB(tracerFrustum, renderer.bounds))
                    { inView = true; break; }
                if (!inView) continue;
                // Be conservative at an obstacle edge: one exposed endpoint must not
                // reveal the rest of a streak behind the wall. No local impact is created.
                bool visible = TracerPointVisible(camera, visual.VisibleTip) &&
                    TracerPointVisible(camera, visual.VisibleTail) &&
                    TracerPointVisible(camera, (visual.VisibleTip + visual.VisibleTail) * .5f);
                foreach (var renderer in visual.Renderers)
                    if (renderer != null) renderer.enabled = visible;
            }
        }

        void AfterTracerCamera(Camera camera)
        {
            // Restore for the next camera; one blocked sight must not hide another view.
            foreach (var visual in visuals.Values)
                if (visual.Renderers != null)
                    foreach (var renderer in visual.Renderers)
                        if (renderer != null) renderer.enabled = true;
        }

        readonly Plane[] tracerFrustum = new Plane[6];
        public void Receive(TracerState[] samples)
        {
            foreach (var sample in samples)
            {
                Visual visual;
                if (!visuals.TryGetValue(sample.Id, out visual))
                {
                    if (visuals.Count >= 512)
                        continue;
                    if (sample.WirePoints.Length > 0)
                        visual = CreateWire();
                    else if (String.IsNullOrEmpty(sample.Ammo))
                        visual = Create(sample.Visual);
                    else
                        visual = CreateMissile(sample.Ammo);
                    if (visual == null)
                        continue;
                    visuals.Add(sample.Id, visual);
                    if (++replayed % 20 == 1)
                        GameBridge.Log("TRACER visual replay total=" + replayed + " type=" + sample.Visual);
                }

                if (visual.Wire != null)
                {
                    visual.Wire.positionCount = sample.WirePoints.Length / CoordinatesPerPoint;
                    visual.Wire.startWidth = visual.Wire.endWidth = sample.WireWidth;
                    for (int index = 0; index < visual.Wire.positionCount; index++)
                        visual.Wire.SetPosition(index,
                            new Vector3(sample.WirePoints[index * CoordinatesPerPoint],
                            sample.WirePoints[index * CoordinatesPerPoint + 1],
                            sample.WirePoints[index * CoordinatesPerPoint + 2]));
                }

                visual.From = new Vector3(sample.X, sample.Y, sample.Z);
                visual.To = new Vector3(sample.EndX, sample.EndY, sample.EndZ);
                visual.Arrival = Time.realtimeSinceStartup;
                visual.Duration = Mathf.Clamp(sample.Duration, .02f, .1f);
            }
        }

        public void Render()
        {
            float now = Time.realtimeSinceStartup;
            foreach (var pair in visuals.ToArray())
            {
                var visual = pair.Value;
                float age = now - visual.Arrival;
                if (visual.Object == null || age > (visual.IsMissile || visual.Wire != null ? .3f : visual.Duration))
                {
                    if (visual.Object != null)
                        UnityEngine.Object.Destroy(visual.Object);
                    visuals.Remove(pair.Key);
                    continue;
                }

                if (visual.Wire != null)
                    continue;
                var displacement = visual.To - visual.From;
                float distance = displacement.magnitude;
                if (visual.IsMissile)
                {
                    visual.Object.transform.position = Vector3.Lerp(visual.From, visual.To, Mathf.Clamp01(age / visual.Duration));
                    if (distance > .001f)
                        visual.Object.transform.rotation = Quaternion.LookRotation(displacement / distance);
                    visual.Object.SetActive(true);
                    continue;
                }

                float travelled = distance * Mathf.Clamp01(age / visual.Duration);
                // Both ends must stay inside the authoritative flight segment, including
                // the first frame and short impact segments. Never extend behind the muzzle.
                float length = Mathf.Min(travelled, Mathf.Clamp(distance / visual.Duration * Time.unscaledDeltaTime, visual.MinLength, visual.MaxLength));
                if (distance < .001f || length < .001f)
                {
                    visual.Object.SetActive(false);
                    continue;
                }

                var direction = displacement / distance;
                float stretch = length / visual.MeshLength;
                visual.Object.transform.rotation = Quaternion.LookRotation(direction);
                visual.Object.transform.localScale = new Vector3(1, 1, stretch);
                visual.Object.transform.position = visual.From + direction * (travelled - visual.MeshFront * stretch);
                visual.VisibleTip = visual.From + direction * travelled;
                visual.VisibleTail = visual.VisibleTip - direction * length;
                visual.Object.SetActive(true);
            }
        }
    }
}

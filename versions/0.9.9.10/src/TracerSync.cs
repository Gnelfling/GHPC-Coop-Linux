using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using GHPC.Weapons;
using GHPC.Effects;
using HarmonyLib;

namespace GhpcCoop
{
    public sealed class TracerSync : IDisposable
    {
        public static TracerSync Current;
        public readonly bool Host;
        public bool Ready;
        readonly Dictionary<int, TracerState> pending = new Dictionary<int, TracerState>();
        sealed class Visual
        {
            public GameObject Object;
            public bool IsMissile;
            public LineRenderer Wire;
            public Vector3 From, To;
            public float Arrival, Duration, MinLength, MaxLength, MeshLength, MeshFront;
        }

        readonly Dictionary<int, Visual> visuals = new Dictionary<int, Visual>();
        int replayed;
        public TracerSync(bool host)
        {
            Host = host;
            Current = this;
        }

        public void Record(LiveRound round, Vector3 start, float dt)
        {
            if (!Host || !Ready || round == null || round.Info == null || (!round.Info.UseTracer && round.Info.ShotVisual == null) || round.IsSpall || dt <= 0)
                return;
            var end = round.Pooled && round.ShotInfo != null ? round.ShotInfo.StopPosition : round.transform.position;
            float distance = (end - start).sqrMagnitude;
            if (distance < .0001f || distance > 25000000)
                return;
            int id = round.ID;
            TracerState state;
            if (!pending.TryGetValue(id, out state))
            {
                if (pending.Count >= 512)
                    return;
                state = new TracerState
                {
                    Id = id,
                    Visual = (int)round.Info.VisualType,
                    Ammo = round.Info.ShotVisual != null ? round.Info.Name : "",
                    X = start.x,
                    Y = start.y,
                    Z = start.z
                };
                pending.Add(id, state);
            }

            state.EndX = end.x;
            state.EndY = end.y;
            state.EndZ = end.z;
            state.Duration = Mathf.Clamp(state.Duration + dt, .001f, 1f);
        }

        public TracerState[] Drain()
        {
            CaptureWires();
            var result = pending.Values.ToArray();
            pending.Clear();
            return result;
        }

        readonly Dictionary<int, int> wireIds = new Dictionary<int, int>();
        Material wireMaterial;
        ATGMWire[] cachedWires = new ATGMWire[0];
        float nextWireScan;

        void CaptureWires()
        {
            if (!Host || !Ready)
                return;
            int captured = 0;
            if (Time.realtimeSinceStartup >= nextWireScan)
            {
                cachedWires = UnityEngine.Object.FindObjectsOfType<ATGMWire>();
                nextWireScan = Time.realtimeSinceStartup + 1f;
            }
            foreach (var wire in cachedWires)
            {
                if (wire == null || !wire.gameObject.activeInHierarchy)
                    continue;
                var line = wire.GetComponent<LineRenderer>();
                if (line == null || !line.enabled || line.positionCount < 2)
                    continue;
                if (captured++ >= 16 || pending.Count >= 512)
                    break;
                int id;
                if (!wireIds.TryGetValue(wire.GetInstanceID(), out id))
                {
                    id = int.MaxValue - wireIds.Count;
                    wireIds.Add(wire.GetInstanceID(), id);
                }
                int count = Math.Min(64, line.positionCount);
                var coordinates = new float[count * 3];
                for (int index = 0; index < count; index++)
                {
                    int sourceIndex = index * (line.positionCount - 1) / (count - 1);
                    var position = line.GetPosition(sourceIndex);
                    if (!line.useWorldSpace)
                        position = line.transform.TransformPoint(position);
                    coordinates[index * 3] = position.x;
                    coordinates[index * 3 + 1] = position.y;
                    coordinates[index * 3 + 2] = position.z;
                }
                pending[id] = new TracerState
                {
                    Id = id, Duration = .1f, WirePoints = coordinates,
                    WireWidth = Mathf.Clamp(line.startWidth, .0001f, 1f)
                };
            }
        }

        Visual CreateWire()
        {
            if (wireMaterial == null)
                wireMaterial = new Material(Shader.Find("Sprites/Default"));
            var root = new GameObject("Coop guidance wire");
            var line = root.AddComponent<LineRenderer>();
            line.sharedMaterial = wireMaterial;
            line.useWorldSpace = true;
            line.startColor = line.endColor = new Color(.12f, .12f, .12f, 1f);
            return new Visual { Object = root, Wire = line };
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
            var obj = UnityEngine.Object.Instantiate(tracer.VisualTransform.gameObject);
            foreach (var b in obj.GetComponentsInChildren<MonoBehaviour>(true))
            {
                b.enabled = false;
                UnityEngine.Object.Destroy(b);
            }

            foreach (var c in obj.GetComponentsInChildren<Collider>(true))
                c.enabled = false;
            foreach (var body in obj.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.detectCollisions = false;
            }

            // Preserve the mesh's native orientation, but measure its actual extent. A
            // centered/offset mesh placed at the muzzle projects half its streak behind it.
            var root = new GameObject("Coop tracer visual");
            obj.transform.SetParent(root.transform, false);
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.Inverse(info.Prefab.transform.rotation) * tracer.VisualTransform.rotation;
            obj.transform.localScale = tracer.InitialLocalScale;
            obj.SetActive(true);
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
                MeshLength = bounds.size.z,
                MeshFront = bounds.max.z,
                MinLength = tracer.MinimumLength,
                MaxLength = tracer.MaximumLength
            };
        }

        Visual CreateMissile(string ammoName)
        {
            var ammo = Resources.FindObjectsOfTypeAll<GHPC.Weaponry.AmmoCodexScriptable>()
                .Select(codex => codex.AmmoType).Where(candidate => candidate != null)
                .FirstOrDefault(candidate => candidate.Name == ammoName && candidate.ShotVisual != null);
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
            return new Visual { Object = root, IsMissile = true };
        }

        public void Receive(TracerState[] samples)
        {
            foreach (var s in samples)
            {
                Visual v;
                if (!visuals.TryGetValue(s.Id, out v))
                {
                    if (visuals.Count >= 512)
                        continue;
                    v = s.WirePoints.Length > 0 ? CreateWire() :
                        String.IsNullOrEmpty(s.Ammo) ? Create(s.Visual) : CreateMissile(s.Ammo);
                    if (v == null)
                        continue;
                    visuals.Add(s.Id, v);
                    if (++replayed % 20 == 1)
                        GameBridge.Log("TRACER visual replay total=" + replayed + " type=" + s.Visual);
                }

                if (v.Wire != null)
                {
                    v.Wire.positionCount = s.WirePoints.Length / 3;
                    v.Wire.startWidth = v.Wire.endWidth = s.WireWidth;
                    for (int index = 0; index < v.Wire.positionCount; index++)
                        v.Wire.SetPosition(index, new Vector3(s.WirePoints[index * 3],
                            s.WirePoints[index * 3 + 1], s.WirePoints[index * 3 + 2]));
                }
                v.From = new Vector3(s.X, s.Y, s.Z);
                v.To = new Vector3(s.EndX, s.EndY, s.EndZ);
                v.Arrival = Time.realtimeSinceStartup;
                v.Duration = Mathf.Clamp(s.Duration, .02f, .1f);
            }
        }

        public void Render()
        {
            float now = Time.realtimeSinceStartup;
            foreach (var pair in visuals.ToArray())
            {
                var v = pair.Value;
                float age = now - v.Arrival;
                if (v.Object == null || age > (v.IsMissile || v.Wire != null ? .3f : v.Duration))
                {
                    if (v.Object != null)
                        UnityEngine.Object.Destroy(v.Object);
                    visuals.Remove(pair.Key);
                    continue;
                }

                if (v.Wire != null)
                    continue;
                var d = v.To - v.From;
                float distance = d.magnitude;
                if (v.IsMissile)
                {
                    v.Object.transform.position = Vector3.Lerp(v.From, v.To, Mathf.Clamp01(age / v.Duration));
                    if (distance > .001f)
                        v.Object.transform.rotation = Quaternion.LookRotation(d / distance);
                    v.Object.SetActive(true);
                    continue;
                }
                float travelled = distance * Mathf.Clamp01(age / v.Duration);
                // Both ends must stay inside the authoritative flight segment, including
                // the first frame and short impact segments. Never extend behind the muzzle.
                float length = Mathf.Min(travelled, Mathf.Clamp(distance / v.Duration * Time.unscaledDeltaTime, v.MinLength, v.MaxLength));
                if (distance < .001f || length < .001f)
                {
                    v.Object.SetActive(false);
                    continue;
                }

                var direction = d / distance;
                float stretch = length / v.MeshLength;
                v.Object.transform.rotation = Quaternion.LookRotation(direction);
                v.Object.transform.localScale = new Vector3(1, 1, stretch);
                v.Object.transform.position = v.From + direction * (travelled - v.MeshFront * stretch);
                v.Object.SetActive(true);
            }
        }

        public void Dispose()
        {
            if (Current == this)
                Current = null;
            foreach (var v in visuals.Values)
                if (v.Object != null)
                    UnityEngine.Object.Destroy(v.Object);
            visuals.Clear();
            wireIds.Clear();
            if (wireMaterial != null) UnityEngine.Object.Destroy(wireMaterial);
            pending.Clear();
        }
    }

    [HarmonyPatch(typeof(LiveRound), "DoUpdate")]
    static class CaptureTracerPath
    {
        struct Sample
        {
            public Vector3 Position;
            public bool Active;
        }

        static void Prefix(LiveRound __instance, out Sample __state)
        {
            __state = new Sample
            {
                Position = __instance.transform.position,
                Active = !__instance.Pooled
            };
        }

        static void Postfix(LiveRound __instance, float __0, Sample __state)
        {
            var s = TracerSync.Current;
            if (s != null && s.Host && __state.Active)
                s.Record(__instance, __state.Position, __0);
        }
    }
}






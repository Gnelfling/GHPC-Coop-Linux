using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using GHPC.Weapons;
using GHPC.Effects;
using HarmonyLib;

namespace GhpcCoop
{
    public sealed partial class TracerSync : IDisposable
    {
        public static TracerSync Current;
        public readonly bool Host;
        public bool Ready;
        const int CoordinatesPerPoint = 3;
        readonly Dictionary<int, TracerState> pending = new Dictionary<int, TracerState>();
        sealed class Visual
        {
            public GameObject Object;
            public bool IsMissile;
            public LineRenderer Wire;
            public Vector3 From, To;
            public Vector3 VisibleTip, VisibleTail;
            public Renderer[] Renderers;
            public float Arrival, Duration, MinLength, MaxLength, MeshLength, MeshFront;
        }

        readonly Dictionary<int, Visual> visuals = new Dictionary<int, Visual>();
        int replayed;
        public TracerSync(bool host)
        {
            Host = host;
            Current = this;
            if (!host)
            {
                Camera.onPreCull += BeforeTracerCamera;
                Camera.onPostRender += AfterTracerCamera;
            }
        }

        public void Record(LiveRound round, Vector3 start, float dt)
        {
            if (!Host ||
                !Ready ||
                round == null ||
                round.Info == null ||
                (!round.Info.UseTracer &&
                round.Info.ShotVisual == null) ||
                round.IsSpall ||
                dt <= 0)
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
        ATGMWire[] cachedWires = new ATGMWire[0];
        float nextWireScan;
        void CaptureWires()
        {
            if (!Host || !Ready)
                return;
            int captured = 0;
            if (Time.realtimeSinceStartup >= nextWireScan)
            {
                cachedWires = SceneQuery.Active<ATGMWire>();
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
                var coordinates = new float[count * CoordinatesPerPoint];
                for (int index = 0; index < count; index++)
                {
                    int sourceIndex = index * (line.positionCount - 1) / (count - 1);
                    var position = line.GetPosition(sourceIndex);
                    if (!line.useWorldSpace)
                        position = line.transform.TransformPoint(position);
                    coordinates[index * CoordinatesPerPoint] = position.x;
                    coordinates[index * CoordinatesPerPoint + 1] = position.y;
                    coordinates[index * CoordinatesPerPoint + 2] = position.z;
                }

                pending[id] = new TracerState
                {
                    Id = id,
                    Duration = .1f,
                    WirePoints = coordinates,
                    WireWidth = Mathf.Clamp(line.startWidth, 0f, 1f)
                };
            }
        }

        public void Dispose()
        {
            Ready = false;
            Camera.onPreCull -= BeforeTracerCamera;
            Camera.onPostRender -= AfterTracerCamera;
            if (Current == this)
                Current = null;
            foreach (var v in visuals.Values)
                if (v.Object != null)
                    UnityEngine.Object.Destroy(v.Object);
            visuals.Clear();
            wireIds.Clear();
            cachedWires = new ATGMWire[0];
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

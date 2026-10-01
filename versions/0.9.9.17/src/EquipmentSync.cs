using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using GHPC;
using GHPC.Equipment;
using GHPC.Effects;
using GHPC.Player;
using HarmonyLib;

namespace GhpcCoop
{
    public sealed class EquipmentSync : IDisposable
    {
        static readonly Dictionary<VehicleSmokeManager, EquipmentSync> Registry = new Dictionary<VehicleSmokeManager, EquipmentSync>();
        static readonly System.Reflection.FieldInfo[] LampArrays = new[]
        {
            "_renderers",
            "_lights",
            "_particles"
        }.Select(n => AccessTools.Field(typeof(LightBandExclusiveItem), n)).ToArray();
        readonly HashSet<int> pendingLamps = new HashSet<int>(), initialLamps = new HashSet<int>();
        readonly Unit unit;
        readonly VehicleSmokeManager smoke;
        readonly LightBandExclusiveItem[] lamps;
        readonly DecorationsManager.MaterialVariant[] materials;
        readonly int[] salvos;
        readonly int[] materialChoices;
        int[] appliedMaterials;
        int[] seen;
        int request;
        readonly RemoteRequestCounter remoteRequests = new RemoteRequestCounter();
        bool desiredScreen, applying;
        public EquipmentSync(Unit u)
        {
            unit = u;
            smoke = u.GetComponentInChildren<VehicleSmokeManager>(true);
            lamps = u.GetComponentsInChildren<LightBandExclusiveItem>(true).OrderBy(x => GameBridge.PathOf(x.transform), StringComparer.Ordinal).ToArray();
            materials = u.GetComponentsInChildren<DecorationsManager>(true).OrderBy(x => GameBridge.PathOf(x.transform), StringComparer.Ordinal).SelectMany(x => x.MaterialVariants ?? new DecorationsManager.MaterialVariant[0]).Where(x => x != null).ToArray();
            materialChoices = materials.Select(MaterialIndex).ToArray();
            salvos = new int[smoke != null && smoke.SmokeGroups != null ? smoke.SmokeGroups.Length : 0];
            if (smoke != null)
            {
                Registry[smoke] = this;
                desiredScreen = smoke.SmokeScreenOn;
            }
        }

        bool Local
        {
            get
            {
                return PlayerInput.Instance != null && PlayerInput.Instance.CurrentPlayerUnit == unit;
            }
        }

        static int MaterialIndex(DecorationsManager.MaterialVariant m)
        {
            // ActiveVariantIndex is not updated by the game's SetMaterialVariant method.
            if (m.VariableSlots == null || m.MaterialVariantPresets == null)
                return -1;
            foreach (var slot in m.VariableSlots)
            {
                if (slot.Renderer == null)
                    continue;
                var a = slot.Renderer.sharedMaterials;
                if (slot.MaterialSlot < 0 || slot.MaterialSlot >= a.Length || a[slot.MaterialSlot] == null)
                    continue;
                string name = a[slot.MaterialSlot].name.Replace(" (Instance)", "");
                for (int i = 0; i < m.MaterialVariantPresets.Length; i++)
                {
                    var candidate = m.MaterialVariantPresets[i].Material;
                    if (candidate != null && candidate.name == name)
                        return i;
                }
            }

            return -1;
        }

        public EquipmentState Capture()
        {
            return new EquipmentState
            {
                Screen = smoke != null && smoke.SmokeScreenOn,
                Salvos = (int[])salvos.Clone(),
                Lamps = lamps.Select(x => x.SwitchedOn).ToArray(),
                Materials = (int[])materialChoices.Clone()
            };
        }

        public EquipmentState Input()
        {
            return new EquipmentState
            {
                SmokeRequest = request,
                Screen = desiredScreen,
                Lamps = lamps.Select(x => x.SwitchedOn).ToArray()
            };
        }

        void ApplyLamp(int index, bool value)
        {
            var lamp = lamps[index];
            if (lamp == null)
                return;
            // Inactive equipment variants have not run Awake. Their native setter dereferences null arrays.
            // Keep the network slot, and retry after native initialization instead of forcing Awake twice.
            if (LampArrays.Any(f => f.GetValue(lamp) == null))
            {
                if (pendingLamps.Add(index))
                    GameBridge.Log("LAMP deferred until Awake: " + unit.UniqueName + "/" + GameBridge.PathOf(lamp.transform));
                return;
            }

            pendingLamps.Remove(index);
            initialLamps.Add(index);
            if (lamp.SwitchedOn != value)
                lamp.SwitchedOn = value;
        }

        public void BeginRemoteControl()
        {
            remoteRequests.Reset();
        }

        public void ApplyInput(EquipmentState e)
        {
            if (e.Lamps.Length != lamps.Length)
                throw new InvalidOperationException("Vehicle lamp layout differs");
            bool fireSmoke = remoteRequests.Accept(e.SmokeRequest);
            for (int i = 0; i < lamps.Length; i++)
                ApplyLamp(i, e.Lamps[i]);
            if (smoke != null)
            {
                if (smoke.SmokeScreenOn != e.Screen)
                    smoke.ToggleSmokeScreen(e.Screen, false);
                if (fireSmoke)
                    smoke.FireSmokes(-1, true, false);
            }
        }

        public bool MatchesLayout(EquipmentState state)
        {
            return state != null && state.Lamps.Length == lamps.Length &&
                state.Salvos.Length == salvos.Length && state.Materials.Length == materials.Length;
        }

        public void ApplyBaseline(EquipmentState state)
        {
            // Counts in a baseline describe history, not new smoke-launch events.
            seen = null;
            initialLamps.Clear();
            appliedMaterials = null;
            Apply(state);
        }

        public void Apply(EquipmentState e)
        {
            if (e.Lamps.Length != lamps.Length || e.Salvos.Length != salvos.Length || e.Materials.Length != materials.Length)
                throw new InvalidOperationException("Vehicle equipment layout differs");
            applying = true;
            try
            {
                if (!Local || seen == null)
                    desiredScreen = e.Screen;
                for (int i = 0; i < lamps.Length; i++)
                    if (!Local || !initialLamps.Contains(i))
                        ApplyLamp(i, e.Lamps[i]);
                if (smoke != null)
                {
                    if (smoke.SmokeScreenOn != e.Screen)
                        smoke.ToggleSmokeScreen(e.Screen, true);
                    if (seen != null)
                        for (int i = 0; i < e.Salvos.Length; i++)
                            if (e.Salvos[i] > seen[i])
                            {
                                smoke.FireSmokes(i, true, true);
                                GameBridge.Log("SMOKE replica vehicle=" + unit.FriendlyName + " local=" + Local + " group=" + i + " count=" + e.Salvos[i]);
                            }
                }

                // Own the history buffer, but reuse it instead of allocating every render frame.
                if (seen == null || seen.Length != e.Salvos.Length)
                    seen = new int[e.Salvos.Length];
                Array.Copy(e.Salvos, seen, seen.Length);
                for (int i = 0; i < materials.Length; i++)
                {
                    int n = e.Materials[i];
                    if (n >= 0 &&
                        materials[i].MaterialVariantPresets != null &&
                        n < materials[i].MaterialVariantPresets.Length &&
                        (appliedMaterials == null ||
                        appliedMaterials[i] != n))
                        materials[i].SetMaterialVariant(n);
                }

                if (appliedMaterials == null || appliedMaterials.Length != e.Materials.Length)
                    appliedMaterials = new int[e.Materials.Length];
                Array.Copy(e.Materials, appliedMaterials, appliedMaterials.Length);
            }
            finally
            {
                applying = false;
            }
        }

        public void Dispose()
        {
            if (smoke != null)
                Registry.Remove(smoke);
        }

        // Read-only probes must never interrupt the game's grenade lifecycle.
        public static void ResetDiagnostics()
        {
            SmokeProbe.Reset();
        }

        static class SmokeProbe
        {
            static readonly System.Reflection.FieldInfo Life = AccessTools.Field(typeof(GHPC.Weaponry.Grenade), "_lifeTime"),
                Collision = AccessTools.Field(typeof(GHPC.Weaponry.Grenade), "_validCollision"),
                Requires = AccessTools.Field(typeof(GHPC.Weaponry.Grenade), "_requiresCollision"),
                Exploded = AccessTools.Field(typeof(GHPC.Weaponry.Grenade), "_hasExploded"),
                Effect = AccessTools.Field(typeof(GHPC.Weaponry.Grenade), "_effectPrefab");
            static readonly Dictionary<int, float> next = new Dictionary<int, float>();
            static int samples;
            internal static void Reset()
            {
                next.Clear();
                samples = 0;
            }

            internal static void Record(GHPC.Weaponry.Grenade grenade, string stage)
            {
                try
                {
                    if (samples >= 160 || !(grenade is GHPC.Weaponry.SmokeGrenade) || grenade.Owner == null)
                        return;
                    int id = grenade.GetInstanceID();
                    float time = Time.realtimeSinceStartup, at;
                    if (stage == "flight" && next.TryGetValue(id, out at) && time < at)
                        return;
                    if (next.Count >= 64 && !next.ContainsKey(id))
                        return;
                    next[id] = time + 1;
                    samples++;
                    var effect = Effect.GetValue(grenade) as GameObject;
                    GameBridge.Log("SMOKE probe stage=" + stage + " id=" + id + " owner=" + grenade.Owner.UniqueName + " guest=" + GameBridge.ReplicaActive + " life=" + Life.GetValue(grenade) + " collision=" + Collision.GetValue(grenade) + " requiresCollision=" + Requires.GetValue(grenade) + " exploded=" + Exploded.GetValue(grenade) + " effect=" + (effect != null ? effect.name : "missing") + " position=" + grenade.transform.position + " velocity=" + (grenade.RigidBody != null ? grenade.RigidBody.velocity.ToString() : "missing"));
                }
                catch
                { /* Diagnostics cannot suppress smoke or disconnect a room. */
                }
            }
        }

        [HarmonyPatch(typeof(GHPC.Weaponry.Grenade), "Update")]
        static class SmokeFlightDiagnostic
        {
            static void Postfix(GHPC.Weaponry.Grenade __instance)
            {
                SmokeProbe.Record(__instance, "flight");
            }
        }

        [HarmonyPatch(typeof(GHPC.Weaponry.Grenade), "Explode")]
        static class SmokeDetonationDiagnostic
        {
            static void Prefix(GHPC.Weaponry.Grenade __instance)
            {
                SmokeProbe.Record(__instance, "detonate-before");
            }

            static void Postfix(GHPC.Weaponry.Grenade __instance)
            {
                SmokeProbe.Record(__instance, "detonate-after");
            }
        }

        [HarmonyPatch(typeof(VehicleSmokeManager), "FireSmokes")]
        static class RequestSmoke
        {
            static bool Prefix(VehicleSmokeManager __instance)
            {
                EquipmentSync e;
                if (!GameBridge.ReplicaActive || !Registry.TryGetValue(__instance, out e) || e.applying)
                    return true;
                if (e.Local)
                    e.request++;
                return false;
            }
        }

        [HarmonyPatch(typeof(VehicleSmokeManager), "ToggleSmokeScreen")]
        static class RequestScreen
        {
            static bool Prefix(VehicleSmokeManager __instance, bool __0)
            {
                EquipmentSync e;
                if (!GameBridge.ReplicaActive || !Registry.TryGetValue(__instance, out e) || e.applying)
                    return true;
                if (e.Local)
                    e.desiredScreen = __0;
                return false;
            }
        }

        [HarmonyPatch(typeof(VehicleSmokeManager), "RunSmokeSequence")]
        static class CountSmoke
        {
            static void Prefix(VehicleSmokeManager __instance, VehicleSmokeManager.SmokePattern __0)
            {
                EquipmentSync e;
                if (GameBridge.ReplicaActive || !Registry.TryGetValue(__instance, out e))
                    return;
                int i = Array.IndexOf(__instance.SmokeGroups, __0);
                if (i >= 0)
                {
                    e.salvos[i]++;
                    GameBridge.Log("SMOKE host vehicle=" + e.unit.FriendlyName + " local=" + e.Local + " group=" + i + " count=" + e.salvos[i]);
                }
            }
        }

        [HarmonyPatch(typeof(VehicleSmokeManager), "HandleSmokeScreen")]
        static class ReplicaScreen
        {
            static bool Prefix()
            {
                return !GameBridge.ReplicaActive;
            }
        }
    }
}

using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using GHPC;
using GHPC.Weapons;
using GHPC.Weapons.Artillery;
using HarmonyLib;

namespace GhpcCoop
{
    // Guests request support; only the host runs native fire missions.
    // Battery IDs use faction and array position, so both peers need matching layouts.
    public sealed class SupportSync : IDisposable
    {
        public static SupportSync Current;
        public readonly bool Host;
        public bool Ready;
        bool handlingRequest;
        readonly Queue<Message> outgoing = new Queue<Message>();
        readonly List<GameObject> objects = new List<GameObject>();
        readonly HashSet<string> pending = new HashSet<string>();
        static readonly FieldInfo Missions = AccessTools.Field(typeof(ArtilleryBattery), "_missionsAvailable"),
            Firing = AccessTools.Field(typeof(ArtilleryBattery), "<IsFiring>k__BackingField"),
            Cooldown = AccessTools.Field(typeof(ArtilleryBattery), "<RemainingCooldown>k__BackingField"),
            Delay = AccessTools.Field(typeof(ArtilleryBattery), "<RemainingDelay>k__BackingField"),
            Impact = AccessTools.Field(typeof(ArtilleryBattery), "<TimeUntilImpactSeconds>k__BackingField"),
            GrenadeEffect = AccessTools.Field(typeof(GHPC.Weaponry.Grenade), "_effectPrefab");
        public SupportSync(bool host)
        {
            Host = host;
            Current = this;
        }

        static IEnumerable<KeyValuePair<string, ArtilleryBattery>> Batteries()
        {
            var manager = FireMissionManager.Instance;
            if (manager == null)
                yield break;
            var batteriesByFaction = new[]
            {
                manager.BlueArtilleryBatteries,
                manager.RedArtilleryBatteries
            };
            for (int side = 0; side < 2; side++)
            {
                var factionBatteries = batteriesByFaction[side];
                if (factionBatteries == null)
                    continue;
                for (int i = 0; i < factionBatteries.Length; i++)
                    if (factionBatteries[i] != null)
                        yield return new KeyValuePair<string, ArtilleryBattery>((side == 0 ? "B:" : "R:") + i, factionBatteries[i]);
            }
        }

        static ArtilleryBattery Find(string id)
        {
            return Batteries().Where(x => x.Key == id).Select(x => x.Value).FirstOrDefault();
        }

        public bool Request(ArtilleryBattery battery, Vector3 point, IndirectFireMunitionType type)
        {
            if (!Ready)
                return false;
            var key = Batteries().FirstOrDefault(x => object.ReferenceEquals(x.Value, battery)).Key;
            if (key == null || pending.Contains(key))
                return false;
            pending.Add(key);
            outgoing.Enqueue(new Message { Kind = Kind.SupportRequest, SupportId = key, Munition = (int)type, SupportX = point.x, SupportY = point.y, SupportZ = point.z });
            GameBridge.Log("SUPPORT request " + key + " type=" + type);
            return false;
        }

        static bool OwnsBattery(ArtilleryBattery battery, Unit guest)
        {
            var manager = FireMissionManager.Instance;
            if (manager != null && battery != null && guest != null)
            {
                var friendlyBatteries = guest.Allegiance == Faction.Red ? manager.RedArtilleryBatteries : guest.Allegiance == Faction.Blue ? manager.BlueArtilleryBatteries : null;
                return friendlyBatteries != null && friendlyBatteries.Contains(battery);
            }

            return false;
        }

        public Message HandleRequest(Message request, Unit guest)
        {
            var battery = Find(request.SupportId);
            bool ownsBattery = OwnsBattery(battery, guest);

            bool accepted = ownsBattery && battery.HasMunitionType((IndirectFireMunitionType)request.Munition) && battery.IsReadyToFire;
            if (accepted)
            {
                // The native callback must not enqueue a second result for this request.
                handlingRequest = true;
                try
                {
                    accepted = FireMissionManager.Instance.SendFireMissionOnCall(new Vector3(request.SupportX, request.SupportY, request.SupportZ), battery, (IndirectFireMunitionType)request.Munition).IsSuccess;
                }
                finally
                {
                    handlingRequest = false;
                }
            }

            if (accepted)
            {
                var point = new Vector3(request.SupportX, request.SupportY, request.SupportZ);
                Mark(battery, point);
                var map = GHPC.UI.MapController.Instance;
                var button = Resources.FindObjectsOfTypeAll<GHPC.UI.Map.MapIconControlType>().FirstOrDefault(x => x.SupportInfos != null &&
                    x.SupportInfos.Contains(battery));
                if (map != null && button != null)
                    AccessTools.Method(typeof(GHPC.UI.MapController), "OnMissionCalled").Invoke(map, new object[] { new MapMissionResult(true, battery, point), button.MapControlType });
            }

            GameBridge.Log("SUPPORT host request " + request.SupportId + " accepted=" + accepted);
            return new Message
            {
                Kind = Kind.SupportResult,
                Supports = Capture(),
                SupportId = request.SupportId,
                Munition = request.Munition,
                SupportX = request.SupportX,
                SupportY = request.SupportY,
                SupportZ = request.SupportZ,
                SupportAccepted = accepted,
                Text = accepted ? "Fire support accepted" : "Fire support unavailable"
            };
        }

        public void Accepted(ArtilleryBattery battery, Vector3 point, IndirectFireMunitionType type)
        {
            if (!Host || !Ready || handlingRequest)
                return;
            var key = Batteries().FirstOrDefault(x => object.ReferenceEquals(x.Value, battery)).Key;
            if (key != null)
                outgoing.Enqueue(new Message { Kind = Kind.SupportResult,
                    SupportId = key,
                    Munition = (int)type,
                    SupportX = point.x,
                    SupportY = point.y,
                    SupportZ = point.z,
                    SupportAccepted = true });
        }

        public SupportState[] Capture()
        {
            return Batteries().Select(x => new SupportState { Id = x.Key,
                Missions = x.Value.RemainingMissions,
                Firing = x.Value.IsFiring,
                Cooldown = x.Value.RemainingCooldown,
                Delay = x.Value.RemainingDelay,
                Impact = x.Value.TimeUntilImpactSeconds }).ToArray();
        }

        public void Apply(SupportState[] states)
        {
            foreach (var snapshot in states)
            {
                var battery = Find(snapshot.Id);
                if (battery == null)
                    throw new InvalidOperationException("Fire support battery layout differs: " + snapshot.Id);
                Missions.SetValue(battery, snapshot.Missions);
                Firing.SetValue(battery, snapshot.Firing);
                Cooldown.SetValue(battery, snapshot.Cooldown);
                Delay.SetValue(battery, snapshot.Delay);
                Impact.SetValue(battery, snapshot.Impact);
            }
        }

        public void Result(Message result)
        {
            pending.Remove(result.SupportId);
            Apply(result.Supports);
            if (result.SupportAccepted)
            {
                var battery = Find(result.SupportId);
                if (battery != null)
                    Mark(battery, new Vector3(result.SupportX, result.SupportY, result.SupportZ));
            }

            GameBridge.Log("SUPPORT result " + result.SupportId + " accepted=" + result.SupportAccepted);
        }

        readonly List<GHPC.UI.Map.MapIcon> icons = new List<GHPC.UI.Map.MapIcon>();
        void Mark(ArtilleryBattery battery, Vector3 point)
        {
            var map = GHPC.UI.MapController.Instance;
            if (map == null || !map.IsInitialized)
                return;
            var icon = map.AddManuallyUpdatedIcon("", GHPC.UI.Map.MapIconType.Artillery, new Color(), map.GetMapLocalPosition(point), Quaternion.identity);
            icon.IsSelectable = false;
            icon.Screenspace = false;
            icons.Add(icon);
            var cooldown = GHPC.Event.CooldownManager.Instance;
            if (cooldown != null)
            {
                cooldown.TrackCooldown(icon, Mathf.Max(1, battery.TotalTimeToComplete), 0);
                foreach (var button in Resources.FindObjectsOfTypeAll<GHPC.UI.Map.MapIconControlType>())
                    if (button.SupportInfos != null && button.SupportInfos.Contains(battery))
                        cooldown.TrackCooldown(battery, button);
            }
        }

        static IEnumerable<KeyValuePair<string, GameObject>> Prefabs()
        {
            foreach (var pair in Batteries())
                foreach (var munition in pair.Value.MunitionsChoices)
                {
                    if (munition == null || munition.DefaultProjectile == null)
                        continue;
                    string key = pair.Key + ":" + (int)munition.Type;
                    yield return new KeyValuePair<string, GameObject>("P:" + key, munition.DefaultProjectile);
                    var grenade = munition.DefaultProjectile.GetComponentInChildren<GHPC.Weaponry.Grenade>(true);
                    if (grenade != null)
                    {
                        var effectPrefab = GrenadeEffect.GetValue(grenade) as GameObject;
                        if (effectPrefab != null)
                            yield return new KeyValuePair<string, GameObject>("E:" + key, effectPrefab);
                    }
                }
        }

        public static GameObject SpawnAndCapture(GameObject prefab)
        {
            var projectile = UnityEngine.Object.Instantiate(prefab);
            var sync = Current;
            if (sync == null || !sync.Host || !sync.Ready)
                return projectile;
            var entry = Prefabs().FirstOrDefault(x => x.Value == prefab && x.Key.StartsWith("P:", StringComparison.Ordinal));
            if (entry.Key == null)
                return projectile;
            var grenade = projectile.GetComponentInChildren<GHPC.Weaponry.Grenade>(true);
            if (grenade != null)
            {
                string key = "E:" + entry.Key.Substring(2);
                grenade.Exploded += explodedGrenade =>
                {
                    if (Current == sync)
                        sync.QueueVisual(key, explodedGrenade.transform.position);
                };
            }
            else
                sync.pendingSpawns.Add(new KeyValuePair<string, GameObject>(entry.Key, projectile));
            return projectile;
        }

        readonly List<KeyValuePair<string, GameObject>> pendingSpawns = new List<KeyValuePair<string, GameObject>>();
        void QueueVisual(string key, Vector3 position)
        {
            if (outgoing.Count >= 256)
                return;
            outgoing.Enqueue(new Message { Kind = Kind.SupportVisual, SupportId = key, SupportX = position.x, SupportY = position.y, SupportZ = position.z });
            GameBridge.Log("SUPPORT visual " + key + " at=" + position);
        }

        public Message[] Drain()
        {
            foreach (var spawn in pendingSpawns)
                if (spawn.Value != null)
                    QueueVisual(spawn.Key, spawn.Value.transform.position);
            pendingSpawns.Clear();
            var result = outgoing.ToArray();
            outgoing.Clear();
            foreach (var message in result)
                if (message.Kind == Kind.SupportResult)
                    message.Supports = Capture();
            return result;
        }

        public void Visual(Message message)
        {
            var prefab = Prefabs().Where(x => x.Key == message.SupportId).Select(x => x.Value).FirstOrDefault();
            if (prefab == null)
            {
                GameBridge.Log("SUPPORT unknown visual " + message.SupportId);
                return;
            }

            var visualEffect = UnityEngine.Object.Instantiate(prefab, new Vector3(message.SupportX, message.SupportY, message.SupportZ), Quaternion.identity);
            objects.Add(visualEffect);
            UnityEngine.Object.Destroy(visualEffect, 240);
            GameBridge.Log("SUPPORT visual replay " + message.SupportId);
        }

        public void Dispose()
        {
            if (Current == this)
                Current = null;
            foreach (var icon in icons)
                icon.Dispose();
            icons.Clear();
            foreach (var visualEffect in objects)
                if (visualEffect != null)
                    UnityEngine.Object.Destroy(visualEffect);
            objects.Clear();
            outgoing.Clear();
            pendingSpawns.Clear();
            pending.Clear();
        }
    }

    [HarmonyPatch(typeof(ArtilleryBattery), "SendFireMissionOnCall")]
    static class SupportRequestPatch
    {
        static bool Prefix(ArtilleryBattery __instance, Vector3 __0, IndirectFireMunitionType __1, ref bool __result)
        {
            var supportSync = SupportSync.Current;
            if (!DamageSync.IsGuest)
                return true;
            __result = supportSync != null && supportSync.Request(__instance, __0, __1);
            return false;
        }

        static void Postfix(ArtilleryBattery __instance, Vector3 __0, IndirectFireMunitionType __1, bool __result)
        {
            var supportSync = SupportSync.Current;
            if (__result && supportSync != null && supportSync.Host)
                supportSync.Accepted(__instance, __0, __1);
        }
    }

    [HarmonyPatch(typeof(ArtilleryBattery), "DoUpdate")]
    static class SupportUpdateGuard
    {
        static bool Prefix()
        {
            return !DamageSync.IsGuest;
        }
    }

    [HarmonyPatch(typeof(ArtilleryBattery), "DoSingleShot")]
    static class SupportSpawnPatch
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                var method = instruction.operand as MethodInfo;
                if (instruction.opcode == OpCodes.Call &&
                    method != null &&
                    method.DeclaringType == typeof(UnityEngine.Object) &&
                    method.Name == "Instantiate" &&
                    method.IsGenericMethod &&
                    method.GetGenericArguments()[0] == typeof(GameObject) &&
                    method.GetParameters().Length == 1)
                {
                    instruction.operand = AccessTools.Method(typeof(SupportSync), "SpawnAndCapture");
                }

                yield return instruction;
            }
        }
    }
}

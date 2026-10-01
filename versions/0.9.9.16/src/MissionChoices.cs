using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using GHPC.Mission;
using HarmonyLib;

namespace GhpcCoop
{
    public static class MissionPolicy
    {
        public static GHPC.Faction DefaultFaction(GHPC.Mission.Data.MissionMetaData meta)
        {
            if (meta == null || meta.IsCategory)
                throw new InvalidOperationException("Not a playable mission");
            if (meta.IsFlexMission)
            {
                if (meta.FlexMissionData == null || meta.FlexMissionData.MissionData == null)
                    throw new InvalidOperationException("Missing mission formation data");
                var faction = meta.FlexMissionData.MissionData.PlayerFaction;
                if (faction != GHPC.Faction.Blue && faction != GHPC.Faction.Red && faction != GHPC.Faction.Neutral)
                    throw new InvalidOperationException("Invalid mission player faction: " + faction);
                return faction;
            }

            var sides = meta.FactionInfo == null ? new GHPC.Faction[0] : meta.FactionInfo.Select(x => x.Allegiance).Where(x => x == GHPC.Faction.Blue ||
                x == GHPC.Faction.Red ||
                x == GHPC.Faction.Neutral).Distinct().ToArray();
            if (sides.Length == 0)
                throw new InvalidOperationException("Mission has no playable faction");
            return sides[0];
        }

        public static void ValidateFaction(GHPC.Mission.Data.MissionMetaData meta, GHPC.Faction side)
        {
            var defaultSide = DefaultFaction(meta);
            if (side == defaultSide)
                return;
            if (meta.IsFlexMission && (side == GHPC.Faction.Blue || side == GHPC.Faction.Red))
                return;
            if (meta.FactionInfo != null && meta.FactionInfo.Any(x => x.Allegiance == side))
                return;
            throw new InvalidOperationException("Host faction " + side + " is not playable in " + meta.MissionSceneReference.Name + " (default " + defaultSide + ")");
        }

        static string Clean(string s)
        {
            return (s ?? "").Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');
        }

        public static void Audit(IEnumerable<GHPC.Mission.Data.MissionTheaterScriptable> theaters, string path)
        {
            var rows = new List<string>
            {
                "Theater\tMission\tScene\tDefaultFaction\tMenuFactions\tFriendlyUnits\tEnemyUnits\tStatus"
            };
            int count = 0, errors = 0;
            foreach (var t in theaters.OrderBy(x => x.Key, StringComparer.Ordinal))
                foreach (var m in t.Missions)
                {
                    if (m == null || m.IsCategory)
                        continue;
                    count++;
                    string scene = "", side = "", friendly = "", enemy = "", status = "OK";
                    try
                    {
                        scene = m.MissionSceneReference.Name;
                        side = DefaultFaction(m).ToString();
                        if (UnityEngine.SceneManagement.SceneUtility.GetBuildIndexByScenePath(t.TerrainSceneReference.Path) < 0 ||
                            UnityEngine.SceneManagement.SceneUtility.GetBuildIndexByScenePath(m.MissionSceneReference.Path) < 0)
                            throw new InvalidOperationException("Scene not installed");
                        if (m.IsFlexMission)
                        {
                            var d = m.FlexMissionData.MissionData;
                            friendly = d.FriendlySpawnInfo == null ? "" : String.Join(";", d.FriendlySpawnInfo.SpawnOrders.Select(x => x.Key + " x" + x.Count).ToArray());
                            enemy = d.EnemySpawnInfo == null ? "" : String.Join(";", d.EnemySpawnInfo.SpawnOrders.Select(x => x.Key + " x" + x.Count).ToArray());
                        }
                    }
                    catch (Exception e)
                    {
                        errors++;
                        status = e.Message;
                    }

                    string menu = m.FactionInfo == null ? "" : String.Join(",", m.FactionInfo.Select(x => x.Allegiance.ToString()).Distinct().ToArray());
                    rows.Add(String.Join("\t", new[] { t.Key, m.MissionName, scene, side, menu, friendly, enemy, status }.Select(Clean).ToArray()));
                }

            File.WriteAllLines(path, rows.ToArray(), System.Text.Encoding.UTF8);
            GameBridge.Log("MISSION AUDIT total=" + count + " errors=" + errors + " path=" + path);
        }
    }

    public static class MissionChoices
    {
        static readonly Dictionary<string, int> selected = new Dictionary<string, int>();
        static Dictionary<string, int> incoming;
        public static string Error = "";
        public static string Path(Transform t)
        {
            var names = new List<string>();
            while (t != null)
            {
                names.Add(t.name);
                t = t.parent;
            }

            names.Reverse();
            return String.Join("/", names.ToArray());
        }

        static string Key(RandomActivation.RandomizedChoiceSet set)
        {
            return Wire.Hash(set.Name + "|" + String.Join("|",
                set.Options.Select(o => o.RootItem == null ? "null" : o.RootItem.gameObject.scene.name + "/" + GameBridge.PathOf(o.RootItem)).ToArray()));
        }

        public static void Choose(RandomActivation.RandomizedChoiceSet set, ref int index)
        {
            var key = Key(set);
            if (incoming != null)
            {
                int value;
                if (!incoming.TryGetValue(key, out value) || value < 0 || value >= set.Options.Length)
                {
                    Error = "Host random formation selection missing or incompatible";
                    return;
                }

                index = value;
            }

            selected[key] = index;
        }

        public static string Export()
        {
            var rows = new List<string>();
            foreach (var component in Resources.FindObjectsOfTypeAll<RandomActivation>().Where(x => x.gameObject.scene.IsValid() &&
                x.gameObject.scene.isLoaded))
            {
                foreach (var set in component.RandomizedItems)
                {
                    var key = Key(set);
                    int index;
                    if (selected.TryGetValue(key, out index))
                        rows.Add(key + ":" + index);
                }
            }

            return String.Join("\n", rows.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray());
        }

        public static void Import(string value)
        {
            incoming = new Dictionary<string, int>();
            Error = "";
            foreach (var row in value.Split('\n'))
            {
                if (row.Length == 0)
                    continue;
                var p = row.Split(':');
                int n;
                if (p.Length != 2 || p[0].Length != 64 || !int.TryParse(p[1], out n) || n < 0 || n > 1024 || incoming.ContainsKey(p[0]))
                    throw new IOException("Invalid mission random choices");
                incoming.Add(p[0], n);
            }
        }

        public static void Reset()
        {
            incoming = null;
            Error = "";
        }
    }

    [HarmonyPatch(typeof(RandomActivation.RandomizedChoiceSet), "SetConfigurationActive")]
    static class MissionChoicePatch
    {
        static void Prefix(RandomActivation.RandomizedChoiceSet __instance, ref int __0)
        {
            MissionChoices.Choose(__instance, ref __0);
        }
    }
}

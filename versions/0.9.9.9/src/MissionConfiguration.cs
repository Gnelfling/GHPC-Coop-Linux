using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GHPC.Mission;
using HarmonyLib;
using UnityEngine;

namespace GhpcCoop
{
    // Explicit data-only payload: no type names, scripts or serialized Unity objects.
    public static class MissionConfiguration
    {
        static string appliedMission;
        static DynamicMissionMetadataOverrides previous;
        static readonly System.Reflection.FieldInfo Replacements =
            AccessTools.Field(typeof(DynamicMissionMetadataOverrides), "_unitReplacements");

        public static string Export(string mission)
        {
            var config = DynamicMissionLauncher.GetFlexOverrides(mission);
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                writer.Write(1);
                writer.Write(config == null ? "" : config.FriendlyInfantryArmyOverride ?? "");
                writer.Write(config == null ? "" : config.EnemyInfantryArmyOverride ?? "");
                var units = config == null ? null : Replacements.GetValue(config) as System.Collections.IList;
                writer.Write(units == null ? 0 : units.Count);
                if (units != null) foreach (var unit in units)
                {
                    var type = unit.GetType(); writer.Write((bool)type.GetField("Friendly").GetValue(unit)); writer.Write((int)(UnitClass)type.GetField("UClass").GetValue(unit));
                    writer.Write((int)type.GetField("VariantIndex").GetValue(unit)); writer.Write((string)type.GetField("NewUnitKey").GetValue(unit));
                }
                WriteAmmo(writer, config == null ? null : config.FriendlyAmmoOverrides);
                WriteAmmo(writer, config == null ? null : config.EnemyAmmoOverrides);
                writer.Flush();
                if (stream.Length > 2400) throw new IOException("Custom mission configuration exceeds the supported network limit.");
                return Convert.ToBase64String(stream.ToArray());
            }
        }
        static void WriteAmmo(BinaryWriter writer, List<DynamicMissionAmmoAdjustment> ammo)
        {
            writer.Write(ammo == null ? 0 : ammo.Count);
            if (ammo == null) return;
            foreach (var entry in ammo)
            {
                if (entry.AmmoSet == null) throw new IOException("Missing host ammunition configuration.");
                writer.Write(entry.AmmoSet.name); writer.Write(entry.SelectedIndex);
            }
        }
        static int Count(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > 128) throw new IOException("Invalid mission configuration count.");
            return count;
        }
        static string Text(BinaryReader reader)
        {
            string text = reader.ReadString();
            if (text.Length > 256 || text.Any(Char.IsControl)) throw new IOException("Invalid mission configuration name.");
            return text;
        }
        public static void Apply(string mission, string encoded)
        {
            if (encoded.Length > 3200) throw new IOException("Custom mission configuration is too large.");
            var config = new DynamicMissionMetadataOverrides { MissionName = mission };
            using (var stream = new MemoryStream(Convert.FromBase64String(encoded)))
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                if (reader.ReadInt32() != 1) throw new IOException("Unsupported mission configuration version.");
                config.FriendlyInfantryArmyOverride = Text(reader);
                config.EnemyInfantryArmyOverride = Text(reader);
                int count = Count(reader);
                for (int i = 0; i < count; i++)
                {
                    bool friendly = reader.ReadBoolean(); int unitClass = reader.ReadInt32();
                    int variant = reader.ReadInt32(); string key = Text(reader);
                    if (!Enum.IsDefined(typeof(UnitClass), unitClass) || variant < 0 || variant > 128 || key.Length == 0)
                        throw new IOException("Invalid unit replacement.");
                    config.AddUnitReplacement(friendly, (UnitClass)unitClass, variant, key, false);
                }
                ReadAmmo(reader, config, true); ReadAmmo(reader, config, false);
                if (stream.Position != stream.Length) throw new IOException("Trailing mission configuration data.");
            }
            // Validate first; only then modify the guest's in-memory configuration.
            Restore();
            previous = DynamicMissionLauncher.GetFlexOverrides(mission);
            appliedMission = mission;
            DynamicMissionLauncher.SaveFlexOverrides(config);
        }
        static void ReadAmmo(BinaryReader reader, DynamicMissionMetadataOverrides config, bool friendly)
        {
            int count = Count(reader);
            var sets = Resources.FindObjectsOfTypeAll<AmmoLogisticsScriptable>();
            for (int i = 0; i < count; i++)
            {
                string name = Text(reader); int index = reader.ReadInt32();
                var matches = sets.Where(set => set.name == name).ToArray();
                if (matches.Length != 1 || index < 0 || matches[0].AmmoLogistics == null ||
                    matches[0].AmmoLogistics.AmmoOptions == null || index >= matches[0].AmmoLogistics.AmmoOptions.Length)
                    throw new IOException("Host ammunition configuration is unavailable: " + name);
                config.AddAmmoReplacement(friendly, matches[0], index);
            }
        }
        public static void Restore()
        {
            if (appliedMission == null) return;
            if (previous == null) DynamicMissionLauncher.ClearFlexOverrides(appliedMission);
            else DynamicMissionLauncher.SaveFlexOverrides(previous);
            appliedMission = null; previous = null;
        }
    }
}


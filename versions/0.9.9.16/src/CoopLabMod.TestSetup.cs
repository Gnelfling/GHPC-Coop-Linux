using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Net;
using System.Security.Cryptography;
using MelonLoader;
using UnityEngine;
using GHPC;
using GHPC.Player;

namespace GhpcCoop
{
    public sealed partial class CoopLabMod
    {
        void ConfigureCustomMissionTest(GHPC.Mission.Data.MissionMetaData mission)
        {
            var replacementArgument = Environment.GetCommandLineArgs().FirstOrDefault(argument => argument.StartsWith("--coop-custom-unit=", StringComparison.Ordinal));
            if (replacementArgument == null)
                return;
            if (!mission.IsFlexMission)
                throw new InvalidOperationException("Customization test requires a flex mission.");
            var missionData = mission.FlexMissionData.MissionData;
            var replacementKey = replacementArgument.Substring("--coop-custom-unit=".Length);
            var overrides = new GHPC.Mission.DynamicMissionMetadataOverrides
            {
                MissionName = mission.MissionSceneReference.Name
            };
            // Include the original ammunition selections, as the native customizer
            // does even when the user changes only one vehicle.
            foreach (var ammunition in missionData.FriendlyAmmoData)
                overrides.AddAmmoReplacement(true, ammunition.AmmoSet, ammunition.SelectedIndex);
            foreach (var ammunition in missionData.EnemyAmmoData)
                overrides.AddAmmoReplacement(false, ammunition.AmmoSet, ammunition.SelectedIndex);
            var ammunitionTestSet = Resources.FindObjectsOfTypeAll<GHPC.Mission.AmmoLogisticsScriptable>().Where(set => set.AmmoLogistics != null &&
                set.AmmoLogistics.AmmoOptions != null &&
                set.AmmoLogistics.AmmoOptions.Length > 1).OrderBy(set => set.name, StringComparer.Ordinal).FirstOrDefault();
            if (ammunitionTestSet != null)
            {
                int selection = ammunitionTestSet.AmmoLogistics.AmmoOptions.Length - 1;
                overrides.AddAmmoReplacement(true, ammunitionTestSet, selection);
                GameBridge.Log("CUSTOM MISSION TEST ammo=" + ammunitionTestSet.name + " index=" + selection);
            }

            var formation = missionData.FriendlySpawnInfo.SpawnOrders.First(order => !order.BlockCustomization);
            overrides.AddUnitReplacement(true, formation.Class, formation.VariantIndex, replacementKey, false);
            GHPC.Mission.DynamicMissionLauncher.SaveFlexOverrides(overrides);
            GameBridge.Log("CUSTOM MISSION TEST mission=" + overrides.MissionName + " original=" + formation.Key + " replacement=" + replacementKey);
        }
    }
}

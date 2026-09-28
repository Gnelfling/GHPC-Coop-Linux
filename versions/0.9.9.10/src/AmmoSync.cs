using System;
using System.Collections.Generic;
using GHPC.Weapons;
using UnityEngine;
using HarmonyLib;

namespace GhpcCoop
{
    // The host captures native feed state; replicas replay that state and its notifications.
    // Reflection strings refer to the supported game's members and must remain exact.
    public static class AmmoSync
    {
        public static readonly Dictionary<AmmoFeed, AmmoState> States = new Dictionary<AmmoFeed, AmmoState>();
        static readonly Dictionary<AmmoFeed, AmmoType> LastKnownBreechAmmo = new Dictionary<AmmoFeed, AmmoType>();
        public static AmmoType LastAmmo(AmmoFeed feed)
        {
            AmmoType breechAmmo;
            return LastKnownBreechAmmo.TryGetValue(feed, out breechAmmo) ? breechAmmo : null;
        }

        static readonly Dictionary<string, AmmoType> AmmoTypesByName = new Dictionary<string, AmmoType>();
        static object ReadFeedField(AmmoFeed feed, string memberName)
        {
            return AccessTools.Field(typeof(AmmoFeed), memberName).GetValue(feed);
        }

        static void WriteFeedField(AmmoFeed feed, string memberName, object value)
        {
            AccessTools.Field(typeof(AmmoFeed), memberName).SetValue(feed, value);
        }

        static void InvokeFeedMethod(AmmoFeed feed, string memberName, params object[] args)
        {
            AccessTools.Method(typeof(AmmoFeed), memberName).Invoke(feed, args);
        }

        static void RaiseFeedEvent(AmmoFeed feed, string memberName, params object[] args)
        {
            var eventHandler = ReadFeedField(feed, memberName) as Delegate;
            if (eventHandler != null)
                eventHandler.DynamicInvoke(args);
        }

        public static AmmoState Capture(WeaponSystem weapon)
        {
            var feed = weapon.Feed as AmmoFeed;
            if (feed == null)
                return new AmmoState();
            return new AmmoState
            {
                Ballistic = weapon.CurrentAmmoType == null ? "" : weapon.CurrentAmmoType.Name,
                ClipType = feed.ReadyRack == null ? -1 : Array.IndexOf(feed.ReadyRack.ClipTypes, feed.Reloading ? (AmmoType.AmmoClip)ReadFeedField(feed, "_queuedClipTypeLockedIn") : feed.LoadedClipType),
                Breech = feed.AmmoTypeInBreech == null ? "" : feed.AmmoTypeInBreech.Name,
                Reloading = feed.Reloading,
                Cycling = feed.Cycling,
                Clip = feed.CurrentClipRemainingCount,
                Reserve = feed.ReadyRack != null && feed.LoadedClipType != null ? feed.ReserveCount : 0,
                Stage = (int)ReadFeedField(feed, "_clipFeedStage"),
                CycleStage = (int)ReadFeedField(feed, "_roundFeedStage"),
                Time = (float)ReadFeedField(feed, "_clipFeedTime"),
                CycleTime = (float)ReadFeedField(feed, "_roundFeedTime"),
                TriggerHeldTime = (float)AccessTools.Field(typeof(WeaponSystem), "_triggerHeldTime").GetValue(weapon)
            };
        }

        static void PlayFeedStage(AmmoFeed feed, int index, bool clip)
        {
            var stages = clip ? feed.ClipReloadStages : feed.RoundCycleStages;
            if (index < 0 || index >= stages.Length)
                return;
            var stage = stages[index];
            GameBridge.Log("AUDIO stage " + feed.name + " clip=" + clip + " index=" + index + " event=" + stage.StageAudioEvent + " source=" + (stage.StageAudio == null ? "none" : ("enabled=" + stage.StageAudio.enabled + " active=" + stage.StageAudio.gameObject.activeInHierarchy)));
            InvokeFeedMethod(feed, "DoReloadStage", stage, clip);
            if (clip)
                RaiseFeedEvent(feed, "ReloadStageStarted", index);
        }

        static void PlayMissedClipStages(AmmoFeed feed, int previous, int current)
        {
            // A network snapshot can skip a short stage. Its FMOD parameters may
            // start the MZ motor, so replay every crossed stage in native order.
            for (int index = Math.Max(0, previous + 1); index <= current && index < feed.ClipReloadStages.Length; index++)
                PlayFeedStage(feed, index, true);
        }

        // The catalog is shared for the session and reset by Clear.
        static void EnsureAmmoTypesLoaded()
        {
            if (AmmoTypesByName.Count != 0)
                return;

            foreach (var ammoCodex in Resources.FindObjectsOfTypeAll<GHPC.Weaponry.AmmoCodexScriptable>())
            {
                if (ammoCodex.AmmoType != null)
                    AmmoTypesByName[ammoCodex.AmmoType.Name] = ammoCodex.AmmoType;
            }
        }

        public static void Apply(WeaponSystem weapon, AmmoState snapshot)
        {
            // Fire is suppressed on replicas; without this state the native HUD
            // permanently displays the initial TOW trigger-hold countdown.
            AccessTools.Field(typeof(WeaponSystem), "_triggerHeldTime")
                .SetValue(weapon, snapshot.TriggerHeldTime);
            var feed = weapon.Feed as AmmoFeed;
            if (feed == null)
                return;
            AmmoState previousSnapshot;
            bool hasPreviousSnapshot = States.TryGetValue(feed, out previousSnapshot);
            States[feed] = snapshot;
            EnsureAmmoTypesLoaded();
            AmmoType breechAmmo = null;
            if (snapshot.Breech != "" && !AmmoTypesByName.TryGetValue(snapshot.Breech, out breechAmmo))
                throw new InvalidOperationException("Unknown replicated ammo: " + snapshot.Breech);
            if (breechAmmo != null)
                LastKnownBreechAmmo[feed] = breechAmmo;
            AmmoType ballistic = null;
            if (snapshot.Ballistic != "" && !AmmoTypesByName.TryGetValue(snapshot.Ballistic, out ballistic))
                throw new InvalidOperationException("Unknown ballistic ammo: " + snapshot.Ballistic);
            // A loaded round can be fired between snapshots. Replicate the weapon's last
            // ballistic type independently of whether the breech is currently empty.
            if (ballistic != null && weapon.CurrentAmmoType != ballistic)
            {
                AccessTools.Field(typeof(WeaponSystem), "<CurrentAmmoType>k__BackingField").SetValue(weapon, ballistic);
                var changed = AccessTools.Field(typeof(WeaponSystem), "AmmoTypeChanged").GetValue(weapon) as Action<AmmoType>;
                if (changed != null)
                    changed(ballistic);
                GameBridge.Log("BALLISTIC SYNC weapon=" + weapon.name + " ammo=" + ballistic.Name + " breechEmpty=" + (breechAmmo == null));
            }

            var clip = snapshot.ClipType >= 0 &&
                feed.ReadyRack != null &&
                snapshot.ClipType < feed.ReadyRack.ClipTypes.Length ? feed.ReadyRack.ClipTypes[snapshot.ClipType] : null;
            if (clip != null)
            {
                WriteFeedField(feed, "_queuedClipTypeLockedIn", clip);
                if (!snapshot.Reloading)
                    WriteFeedField(feed, "<LoadedClipType>k__BackingField", clip);
            }

            WriteFeedField(feed, "<AmmoTypeInBreech>k__BackingField", breechAmmo);
            WriteFeedField(feed, "<Reloading>k__BackingField", snapshot.Reloading);
            WriteFeedField(feed, "<Cycling>k__BackingField", snapshot.Cycling);
            WriteFeedField(feed, "_clipFeedTime", snapshot.Time);
            WriteFeedField(feed, "_roundFeedTime", snapshot.CycleTime);
            WriteFeedField(feed, "_clipFeedStage", snapshot.Stage);
            WriteFeedField(feed, "_roundFeedStage", snapshot.CycleStage);
            // Initial snapshots and stale weapon caches need the same native ammo notification as a reload.
            if (breechAmmo != null && (!hasPreviousSnapshot || previousSnapshot.Breech != snapshot.Breech || weapon.CurrentAmmoType != breechAmmo))
            {
                RaiseFeedEvent(feed, "LoadedRoundInBreech", breechAmmo);
            }

            if (hasPreviousSnapshot && previousSnapshot.Breech != snapshot.Breech)
            {
                if (breechAmmo == null && snapshot.Clip == 0)
                    RaiseFeedEvent(feed, "ClipDepleted");
                GameBridge.Log("AMMO REPLICA " + weapon.name + " loaded=" + (breechAmmo != null) + " reload=" + snapshot.Reloading + " reserve=" + snapshot.Reserve);
            }

            if (snapshot.Reloading && (!hasPreviousSnapshot || !previousSnapshot.Reloading))
            {
                InvokeFeedMethod(feed, "ResetLiveDurations");
                InvokeFeedMethod(feed, "StartAutoloaderAudio");
                GameBridge.Log("AUTOLOADER diagnostic weapon=" + weapon.name +
                    " event=" + ReadFeedField(feed, "_clipReloadFMODEvent") +
                    " carousel=" + (feed.Carousel != null));
                if (hasPreviousSnapshot && feed.AnnounceReloads && snapshot.Reserve == 1 && clip != null)
                    RaiseFeedEvent(feed, "LoadingFinalClip", clip);
                PlayMissedClipStages(feed, -1, snapshot.Stage);
                GameBridge.Log("AUDIO reload start " + weapon.name + " stage=" + snapshot.Stage);
            }
            else if (hasPreviousSnapshot && previousSnapshot.Reloading && (previousSnapshot.Stage != snapshot.Stage || !snapshot.Reloading))
            {
                RaiseFeedEvent(feed, "ReloadStageEnded", previousSnapshot.Stage);
                if (snapshot.Reloading)
                    PlayMissedClipStages(feed, previousSnapshot.Stage, snapshot.Stage);
                else
                {
                    InvokeFeedMethod(feed, "StopAutoloaderAudio");
                    if (clip != null && (snapshot.Clip > previousSnapshot.Clip || breechAmmo != null))
                    {
                        RaiseFeedEvent(feed, "LoadedNewClip", clip);
                        GameBridge.Log("AUDIO reload complete " + weapon.name);
                    }
                }
            }

            if (snapshot.Cycling && (!hasPreviousSnapshot || !previousSnapshot.Cycling || previousSnapshot.CycleStage != snapshot.CycleStage))
                PlayFeedStage(feed, snapshot.CycleStage, false);
        }

        public static int NextClipIndex(WeaponSystem weapon)
        {
            var feed = weapon == null ? null : weapon.Feed;
            return feed == null || feed.ReadyRack == null ? -1 : Array.IndexOf(feed.ReadyRack.ClipTypes, feed.QueuedClipType);
        }

        public static void ApplyNextClip(IEnumerable<WeaponSystemInfo> weapons, int role, string text)
        {
            if (String.IsNullOrEmpty(text))
                return;
            int marker = text.IndexOf(";clip:", StringComparison.Ordinal), index;
            if (marker < 0)
                return;
            if (!int.TryParse(text.Substring(marker + 6), out index) || index < -1)
                throw new InvalidOperationException("Invalid next ammo selection");
            if (index == -1)
                return;
            foreach (var info in weapons)
            {
                if (info == null || (int)info.Role != role || info.Weapon == null)
                    continue;
                var weapon = info.Weapon;
                var feed = weapon.Feed;
                if (feed == null || feed.ReadyRack == null || index >= feed.ReadyRack.ClipTypes.Length)
                    throw new InvalidOperationException("Next ammo selection outside weapon rack");
                var clip = feed.ReadyRack.ClipTypes[index];
                if (feed.QueuedClipType != clip)
                {
                    feed.SetNextClipType(clip);
                    GameBridge.Log("AMMO NEXT role=" + role + " clip=" + index);
                }

                return;
            }
        }

        public static void Clear()
        {
            foreach (var feed in States.Keys)
                if (feed != null)
                    InvokeFeedMethod(feed, "StopAutoloaderAudio");
            States.Clear();
            AmmoTypesByName.Clear();
            LastKnownBreechAmmo.Clear();
            RemoteReloadSettings.Preferences.Clear();
        }
    }

    static class RemoteReloadSettings
    {
        public static readonly Dictionary<GHPC.Crew.CrewManager, int> Preferences = new Dictionary<GHPC.Crew.CrewManager, int>();
    }

    // Remote players must not inherit the native non-player/AI auto-reload shortcut.
    // Remote feeds follow their owner's reload preference and native forced modes.
    [HarmonyPatch(typeof(AmmoFeed), "get_AutoReload")]
    static class RemoteManualReload
    {
        static bool Prefix(AmmoFeed __instance, GHPC.Crew.CrewManager ____crewManager, ref bool __result)
        {
            if (____crewManager == null)
                return true;
            if (GameBridge.IsRemoteCrew(____crewManager))
            {
                // Native AutoReload unconditionally returns true for AI-owned units, so
                // remote vehicles need the player rules rather than that AI shortcut.
                int preference;
                if (!RemoteReloadSettings.Preferences.TryGetValue(____crewManager, out preference))
                    preference = 0;
                __result = ReloadPolicy.Automatic((int)__instance.ReloadMode, preference, __instance.AutoReloadSwitchedOn);
                return false;
            }

            return true; // Local players retain the native game reload setting.
        }
    }

    [HarmonyPatch(typeof(AmmoFeed), "Update")]
    static class ReplicaFeedUpdate
    {
        static bool Prefix()
        {
            return !GameBridge.ReplicaActive;
        }
    }

    [HarmonyPatch(typeof(AmmoFeed), "FeedNewClip")]
    static class ReplicaFeedClip
    {
        static bool Prefix()
        {
            return !GameBridge.ReplicaActive;
        }
    }

    [HarmonyPatch(typeof(AmmoFeed), "FeedNewRound")]
    static class ReplicaFeedRound
    {
        static bool Prefix()
        {
            return !GameBridge.ReplicaActive;
        }
    }

    [HarmonyPatch(typeof(AmmoFeed), "get_CurrentClipRemainingCount")]
    static class ReplicaClipCount
    {
        static void Postfix(AmmoFeed __instance, ref int __result)
        {
            AmmoState snapshot;
            if (GameBridge.ReplicaActive && AmmoSync.States.TryGetValue(__instance, out snapshot))
                __result = snapshot.Clip;
        }
    }

    [HarmonyPatch(typeof(AmmoFeed), "get_ReserveCount")]
    static class ReplicaReserveCount
    {
        static void Postfix(AmmoFeed __instance, ref int __result)
        {
            AmmoState snapshot;
            if (GameBridge.ReplicaActive && AmmoSync.States.TryGetValue(__instance, out snapshot))
                __result = snapshot.Reserve;
        }
    }
}

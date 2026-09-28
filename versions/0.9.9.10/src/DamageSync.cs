using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using GHPC;
using GHPC.Equipment;
using GHPC.Weapons;
using HarmonyLib;

namespace GhpcCoop
{
    public sealed class DamageSync
    {
        public static bool Loading, ApplyingHealth, ApplyingFlags;
        public static bool IsGuest
        {
            get
            {
                return Loading || GameBridge.ReplicaActive;
            }
        }

        readonly GHPC.Equipment.DestructibleComponent[] parts;
        readonly GHPC.Effects.FlammablesManager[] scorch;
        public readonly string Layout;
        int lastFlags = -1;
        float[] lastScorch;
        public DamageSync(Unit u)
        {
            scorch = u.GetComponentsInChildren<GHPC.Effects.FlammablesManager>(true).OrderBy(x => RelativePath(x.transform, u.transform), StringComparer.Ordinal).ToArray();
            parts = u.GetComponentsInChildren<GHPC.Equipment.DestructibleComponent>(true).OrderBy(x => RelativePath(x.transform, u.transform), StringComparer.Ordinal).ThenBy(x => x.GetType().FullName, StringComparer.Ordinal).ToArray();
            if (parts.Length > 512)
                throw new InvalidOperationException("Vehicle damage layout too large");
            Layout = Wire.Hash(String.Join("\n", parts.Select(x => RelativePath(x.transform, u.transform) + ":" + x.GetType().FullName).ToArray()));
        }

        static string RelativePath(UnityEngine.Transform t, UnityEngine.Transform root)
        {
            var names = new List<string>();
            while (t != null && t != root)
            {
                names.Add(t.name + "#" + t.GetSiblingIndex());
                t = t.parent;
            }

            names.Reverse();
            return String.Join("/", names.ToArray());
        }

        public float[] CaptureScorch()
        {
            return scorch.Select(x => ScorchAuthority.Read(x)).ToArray();
        }

        public float[] Capture()
        {
            return parts.Select(x => x == null ? 0 : InfantryHealth.ToWire(x.HealthPercent)).ToArray();
        }

        public void Apply(Unit u, Pose p)
        {
            if (p.Health.Length != parts.Length)
                throw new InvalidOperationException("Vehicle damage layout changed");
            ApplyingHealth = true;
            try
            {
                for (int i = 0; i < parts.Length; i++)
                    if (parts[i] != null && Math.Abs(InfantryHealth.ToWire(parts[i].HealthPercent) - p.Health[i]) > .0001f)
                        parts[i].SetHealthPercent(InfantryHealth.ToNative(p.Health[i]));
            }
            finally
            {
                ApplyingHealth = false;
            }

            ApplyingFlags = true;
            try
            {
                if ((p.Flags & 1) != 0 && !u.Destroyed)
                    u.NotifyDestroyed();
                if ((p.Flags & 2) != 0 && !u.Abandoned)
                    u.NotifyAbandoned();
                if ((p.Flags & 4) != 0 && !u.CannotMove)
                    u.NotifyCannotMove();
                if ((p.Flags & 8) != 0 && !u.CannotShoot)
                    u.NotifyCannotShoot();
                if ((p.Flags & 16) != 0 && !u.UnitIncapacitated)
                    u.NotifyIncapacitated();
            }
            finally
            {
                ApplyingFlags = false;
            }

            if (p.Scorch.Length != scorch.Length)
                throw new InvalidOperationException("Vehicle scorch layout changed");
            ScorchAuthority.Applying = true;
            try
            {
                for (int i = 0; i < scorch.Length; i++)
                    if (scorch[i] != null && (lastScorch == null || Math.Abs(lastScorch[i] - p.Scorch[i]) > .0001f))
                        scorch[i].ForceScorchAll(p.Scorch[i]);
                lastScorch = (float[])p.Scorch.Clone();
            }
            finally
            {
                ScorchAuthority.Applying = false;
            }

            if (lastFlags != p.Flags)
            {
                lastFlags = p.Flags;
                GameBridge.Log("DAMAGE replica id=" + p.Id + " vehicle=" + u.FriendlyName + " flags=" + p.Flags + " damagedParts=" + p.Health.Count(x => x < .9999f));
            }
        }
    }

    static class ScorchAuthority
    {
        public static bool Applying;
        static readonly Dictionary<GHPC.Effects.FlammablesManager, float> actual = new Dictionary<GHPC.Effects.FlammablesManager, float>();
        public static float Read(GHPC.Effects.FlammablesManager m)
        {
            float v;
            return m == null ? 0 : actual.TryGetValue(m, out v) ? v : UnityEngine.Mathf.Clamp01(m.CurrentScorchRatio);
        }

        public static void Record(GHPC.Effects.FlammablesManager m, float ratio)
        {
            actual[m] = UnityEngine.Mathf.Clamp01(ratio);
        }

        public static void Clear()
        {
            actual.Clear();
        }
    }

    [HarmonyPatch(typeof(GHPC.Effects.FlammablesManager), "ForceScorchAll")]
    static class ScorchAuthorityPatch
    {
        static bool Prefix(GHPC.Effects.FlammablesManager __instance, float __0)
        {
            if (DamageSync.IsGuest && !ScorchAuthority.Applying)
                return false;
            ScorchAuthority.Record(__instance, __0);
            return true;
        }
    }

    [HarmonyPatch]
    static class ReplicaDamageGuard
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            return typeof(GHPC.Equipment.DestructibleComponent).Assembly.GetTypes().Where(t => typeof(GHPC.Equipment.DestructibleComponent).IsAssignableFrom(t)).SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)).Where(m => !m.IsAbstract &&
                (m.Name == "SetHealthPercent" ||
                m.Name.StartsWith("Apply", StringComparison.Ordinal) &&
                m.Name.EndsWith("Damage", StringComparison.Ordinal)));
        }

        static bool Prefix()
        {
            return !DamageSync.IsGuest || DamageSync.ApplyingHealth;
        }
    }

    [HarmonyPatch]
    static class ReplicaUnitDeathGuard
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            return new[]
            {
                "NotifyDestroyed",
                "NotifyAbandoned",
                "NotifyCannotMove",
                "NotifyCannotShoot",
                "NotifyIncapacitated"
            }.Select(n => (MethodBase)AccessTools.Method(typeof(Unit), n));
        }

        static bool Prefix()
        {
            return !DamageSync.IsGuest || DamageSync.ApplyingFlags;
        }
    }

    [HarmonyPatch(typeof(LiveRound), "DoUpdate")]
    static class ReplicaProjectileGuard
    {
        static bool Prefix()
        {
            return !DamageSync.IsGuest;
        }
    }

    [HarmonyPatch(typeof(LiveRound), "DoExplosiveBlast")]
    static class ReplicaBlastGuard
    {
        static bool Prefix()
        {
            return !DamageSync.IsGuest;
        }
    }

    [HarmonyPatch(typeof(GHPC.Effects.DetachableParent), "Detach")]
    static class ReplicaDetachGuard
    {
        public static bool Applying;
        static bool Prefix()
        {
            return !DamageSync.IsGuest || Applying;
        }
    }

    [HarmonyPatch(typeof(GHPC.Effects.DetachableParent), "DoDetachForces")]
    static class ReplicaDetachForcesGuard
    {
        static bool Prefix()
        {
            // The host sends the trajectory; local random forces would diverge.
            return !DamageSync.IsGuest;
        }
    }

    [HarmonyPatch(typeof(GHPC.Effects.FlammablesManagerer), "Update")]
    static class ReplicaFireSimulationGuard
    {
        static bool Prefix()
        {
            return !DamageSync.IsGuest;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace GhpcCoop
{
    // Periodic discovery passes only need objects that live in loaded scenes.
    // Resources.FindObjectsOfTypeAll also walks every loaded asset and prefab,
    // which measured 70-96 ms per infantry pass on slower hosts (user logs, 0.9.9.16)
    // and stalled snapshot sends once a second. FindObjectsByType searches scene
    // objects only and skips the instance-ID sort.
    //
    // HARDENING.md declined this swap because FindObjectsByType can exclude some
    // hidden runtime objects. To keep coverage provable, All<T> periodically runs
    // the legacy query too and compares the in-scene results. If the legacy query
    // ever finds an in-scene object the fast query missed, that type switches back
    // to the legacy query for the rest of the session and the mismatch is logged.
    // Callers must not depend on result order (the scans key by identity).
    static class SceneQuery
    {
        static readonly Dictionary<Type, int> calls = new Dictionary<Type, int>();
        // A fallback is kept for the whole game process: once missed, never trust that type again.
        static readonly HashSet<Type> legacy = new HashSet<Type>();
        static int lastScene = -1;

        public static T[] All<T>() where T : Component
        {
            var type = typeof(T);
            if (legacy.Contains(type))
                return Resources.FindObjectsOfTypeAll<T>();
            // Restart the verification schedule for each mission (new active scene).
            int scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
            if (scene != lastScene)
            {
                lastScene = scene;
                calls.Clear();
            }
            int call;
            calls.TryGetValue(type, out call);
            calls[type] = ++call;
            var fast = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            // Check early in a mission and then roughly once a minute at the fast cadence.
            if (call == 1 || call == 3 || call == 10 || call == 30 || call % 60 == 0)
                return Verify(type, fast);
            return fast;
        }

        static T[] Verify<T>(Type type, T[] fast) where T : Component
        {
            var found = new HashSet<int>();
            foreach (var item in fast)
                if (item != null)
                    found.Add(item.GetInstanceID());
            int inScene = 0, missed = 0;
            var complete = Resources.FindObjectsOfTypeAll<T>();
            foreach (var item in complete)
            {
                if (item == null || !item.gameObject.scene.IsValid() || !item.gameObject.scene.isLoaded)
                    continue;
                inScene++;
                if (!found.Contains(item.GetInstanceID()))
                    missed++;
            }

            if (missed > 0)
            {
                legacy.Add(type);
                GameBridge.Log("SCAN COVERAGE fallback type=" + type.Name + " legacyInScene=" + inScene + " missedByFast=" + missed);
            }
            else
                GameBridge.Log("SCAN COVERAGE ok type=" + type.Name + " inScene=" + inScene);
            // Repair coverage during this pass; waiting could remove valid replicas.
            return missed > 0 ? complete : fast;
        }

        // Same semantics as Object.FindObjectsOfType<T>() (active objects only) without the sort.
        public static T[] Active<T>() where T : UnityEngine.Object
        {
            return UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        }

        // Back off when a discovery pass is expensive on this machine. A 1 s cadence
        // keeps new spawns responsive on fast PCs; slow PCs trade up to 3 s of spawn
        // discovery latency for not freezing the host (and every guest) once a second.
        public const float FastInterval = 1f, SlowInterval = 3f;
        public const double SlowScanMs = 20;

        public static float NextInterval(double lastScanMs)
        {
            return lastScanMs > SlowScanMs ? SlowInterval : FastInterval;
        }
    }
}

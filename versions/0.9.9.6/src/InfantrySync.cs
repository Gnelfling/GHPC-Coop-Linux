using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GHPC.Infantry;
using GHPC.Equipment;
using UnityEngine;
namespace GhpcCoop
{
    // Separate from vehicle seats: infantry must never become a controllable tank slot.
    public sealed class InfantrySync
    {
        sealed class Entry
        {
            public InfantryUnit Unit;
            public GHPC.Equipment.DestructibleComponent[] Parts;
        }
        readonly Dictionary<string, Entry> units = new Dictionary<string, Entry>();
        readonly HashSet<string> ambiguous = new HashSet<string>();
        readonly Dictionary<string,string> lastHealth = new Dictionary<string,string>();
        readonly Dictionary<string,bool> lastDead = new Dictionary<string,bool>();
        float nextScan, nextResend;
        bool warned;
        void Refresh()
        {
            if (Time.realtimeSinceStartup < nextScan) return;
            nextScan = Time.realtimeSinceStartup + 1;
            units.Clear(); ambiguous.Clear();
            foreach (var unit in UnityEngine.Object.FindObjectsOfType<InfantryUnit>())
            {
                if (unit.Squad == null || !unit.SquadInitialized || unit.Human == null) continue;
                var parts = unit.GetComponentsInChildren<GHPC.Equipment.DestructibleComponent>(true)
                    .OrderBy(part => Relative(part.transform, unit.transform), StringComparer.Ordinal)
                    .ThenBy(part => part.GetType().FullName, StringComparer.Ordinal).ToArray();
                if (parts.Length > 128) continue;
                string layout = String.Join(";", parts.Select(part => Relative(part.transform, unit.transform) + ":" + part.GetType().FullName).ToArray());
                string id = Wire.Hash(unit.gameObject.scene.name + "|" + GameBridge.PathOf(unit.Squad.transform) + "|" +
                    unit.IndexInSquad.ToString("R", CultureInfo.InvariantCulture) + "|" + unit.Allegiance + "|" + unit.UniqueName + "|" + layout);
                if (units.ContainsKey(id)) { ambiguous.Add(id); continue; }
                units.Add(id, new Entry { Unit = unit, Parts = parts });
            }
            foreach (string id in ambiguous) units.Remove(id);
        }
        static string Relative(Transform transform, Transform root)
        {
            var names = new List<string>();
            while (transform != null && transform != root)
            {
                names.Add(transform.name + "#" + transform.GetSiblingIndex()); transform = transform.parent;
            }
            names.Reverse(); return String.Join("/", names.ToArray());
        }
        public void ResendAll(){lastHealth.Clear();lastDead.Clear();}
        public IEnumerable<Message> Capture()
        {
            Refresh();
            if(Time.realtimeSinceStartup>=nextResend){nextResend=Time.realtimeSinceStartup+10;ResendAll();}
            int budget = 4;
            foreach (var pair in units)
            {
                var entry = pair.Value;
                if (entry.Unit == null || entry.Unit.Human == null) continue;
                string health = String.Join(",", entry.Parts.Select(part =>
                    (part == null ? 0 : Mathf.Clamp01(part.HealthPercent)).ToString("R", CultureInfo.InvariantCulture)).ToArray());
                string oldHealth; bool oldDead;
                bool dead=entry.Unit.Human.IsDead;
                if(lastHealth.TryGetValue(pair.Key,out oldHealth)&&lastDead.TryGetValue(pair.Key,out oldDead)&&oldHealth==health&&oldDead==dead)continue;
                lastHealth[pair.Key]=health;lastDead[pair.Key]=dead;
                yield return new Message { Kind = Kind.Infantry, Unit = pair.Key,
                    Fire = dead, Text = health };
                if(--budget==0)yield break;
            }
        }
        public void Apply(Message message)
        {
            if (!GameBridge.ReplicaActive) return;
            Refresh();
            Entry entry;
            if (!units.TryGetValue(message.Unit, out entry) || entry.Unit == null || entry.Unit.Human == null)
            {
                if (!warned) { warned = true; GameBridge.Log("INFANTRY unmatched identity; state skipped, no positional guessing."); }
                return;
            }
            var health = InfantryHealth.Parse(message.Text, entry.Parts.Length);
            bool oldHealth = DamageSync.ApplyingHealth, oldFlags = DamageSync.ApplyingFlags;
            DamageSync.ApplyingHealth = DamageSync.ApplyingFlags = true;
            try
            {
                for (int i = 0; i < health.Length; i++)
                    if (entry.Parts[i] != null && Math.Abs(entry.Parts[i].HealthPercent - health[i]) > .0001f)
                        entry.Parts[i].SetHealthPercent(health[i]);
                if (message.Fire && !entry.Unit.Human.IsDead) entry.Unit.Human.Kill();
            }
            finally { DamageSync.ApplyingHealth = oldHealth; DamageSync.ApplyingFlags = oldFlags; }
        }
    }
}


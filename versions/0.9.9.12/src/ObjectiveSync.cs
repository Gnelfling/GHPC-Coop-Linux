using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GHPC.UI.Hud.Objectives;
using HarmonyLib;
using UnityEngine;

namespace GhpcCoop
{
    // Called on Unity's main thread. Only updates presentation, never mission events.
    public static class ObjectiveSync
    {
        static ObjectiveManager lastReplica, lastHost;
        static ObjectiveData[] lastData;
        static IDictionary<int, ObjectiveListItem> lastItems;
        static readonly Dictionary<int, string> LocalText = new Dictionary<int, string>();
        static readonly HashSet<int> ReceivedIds = new HashSet<int>();
        static readonly List<int> SortedIds = new List<int>();
        static readonly bool DiagnosticsEnabled = Array.IndexOf(Environment.GetCommandLineArgs(), "--coop-objective-diagnostics") >= 0;
        static readonly ObjectiveStatus[] Empty = new ObjectiveStatus[0];
        static bool compatibilityWarning, diagnosticWarning;
        static readonly FieldInfo Lists = AccessTools.Field(typeof(ObjectiveManager), "_listReferences");
        static readonly FieldInfo State = AccessTools.Field(typeof(ObjectiveListItem), "_state");
        static readonly FieldInfo Group = AccessTools.Field(typeof(ObjectiveManager), "_objectiveCanvasGroup");
        static readonly FieldInfo Prefab = AccessTools.Field(typeof(ObjectiveManager), "_objectiveListItemPrefab");
        static bool Compatible()
        {
            if (Lists != null && State != null && Group != null && Prefab != null)
                return true;
            if (!compatibilityWarning)
            {
                compatibilityWarning = true;
                GameBridge.Log("OBJECTIVES disabled: required game UI fields are unavailable.");
            }

            return false;
        }

        static string Plain(string text)
        {
            if (String.IsNullOrEmpty(text))
                return "";
            return text.IndexOf("<s>", StringComparison.Ordinal) < 0 &&
                text.IndexOf("</s>", StringComparison.Ordinal) < 0 ? text : text.Replace("<s>", "").Replace("</s>", "");
        }

        static ObjectiveManager Manager()
        {
            var current = ObjectiveManager.Instance;
            if (current != null && current.IsInitialized)
                return current;
            foreach (var candidate in UnityEngine.Object.FindObjectsOfType<ObjectiveManager>())
                if (candidate.IsInitialized)
                    return candidate;
            return null;
        }

        public static ObjectiveStatus[] Capture()
        {
            if (!Compatible())
                return Empty;
            var manager = Manager();
            if (manager == null)
                return Empty;
            var items = Lists.GetValue(manager) as IDictionary<int, ObjectiveListItem>;
            if (items == null || items.Count == 0)
                return Empty;
            if (lastHost != manager)
            {
                lastHost = manager;
                GameBridge.Log("OBJECTIVES host count=" + items.Count);
            }

            // Count alone cannot detect replacing one key with another.
            bool keysChanged = SortedIds.Count != items.Count;
            if (!keysChanged)
                foreach (int id in SortedIds)
                    if (!items.ContainsKey(id))
                    {
                        keysChanged = true;
                        break;
                    }

            if (keysChanged)
            {
                SortedIds.Clear();
                foreach (int id in items.Keys)
                    SortedIds.Add(id);
                SortedIds.Sort();
            }

            int count = 0;
            foreach (int id in SortedIds)
                if (items[id] != null)
                    count++;
            if (count == 0)
                return Empty;
            // Queued sends own their data; do not reuse mutable snapshot arrays.
            var result = new ObjectiveStatus[count];
            int index = 0;
            foreach (int id in SortedIds)
            {
                var item = items[id];
                if (item == null)
                    continue;
                result[index++] = new ObjectiveStatus
                {
                    Id = id,
                    State = (int)(ObjectiveState)State.GetValue(item),
                    Visible = item.gameObject.activeSelf,
                    Text = ""
                };
            }

            return result;
        }

        static bool RefreshLocalText(ObjectiveManager manager, IDictionary<int, ObjectiveListItem> items)
        {
            var data = manager.ActiveObjectiveData;
            bool changed = lastReplica != manager || !ReferenceEquals(lastData, data) || !ReferenceEquals(lastItems, items);
            if (!changed)
                return false;
            LocalText.Clear();
            foreach (var pair in items)
                if (pair.Value != null && pair.Value.Label != null)
                    LocalText[pair.Key] = Plain(pair.Value.Label.text);
            // GHPC InitializeObjectiveListItems uses array index minus skipped blank rows.
            if (data != null)
            {
                int id = 0;
                foreach (var row in data)
                {
                    if (row == null || String.IsNullOrWhiteSpace(row.ObjectiveText))
                        continue;
                    if (!LocalText.ContainsKey(id))
                        LocalText[id] = Plain(row.ObjectiveText);
                    id++;
                }
            }

            lastReplica = manager;
            lastData = data;
            lastItems = items;
            return true;
        }

        public static void Apply(ObjectiveStatus[] values)
        {
            if (!GameBridge.ReplicaActive || values == null || !Compatible())
                return;
            var manager = Manager();
            if (manager == null)
                return;
            var items = Lists.GetValue(manager) as IDictionary<int, ObjectiveListItem>;
            var group = Group.GetValue(manager) as ObjectiveCanvasGroup;
            if (items == null || group == null)
                return;
            bool changed = RefreshLocalText(manager, items);
            ReceivedIds.Clear();
            foreach (var value in values)
            {
                if (!ReceivedIds.Add(value.Id))
                    continue;
                ObjectiveListItem item;
                if (!items.TryGetValue(value.Id, out item) || item == null)
                {
                    var prefab = Prefab.GetValue(manager) as GameObject;
                    if (prefab == null)
                        continue;
                    item = UnityEngine.Object.Instantiate(prefab, group.transform).GetComponent<ObjectiveListItem>();
                    if (item == null)
                        continue;
                    items[value.Id] = item;
                    changed = true;
                }

                if (item.Label == null)
                    continue;
                string text;
                if (!LocalText.TryGetValue(value.Id, out text))
                    text = "Objective " + (value.Id + 1);
                var state = (ObjectiveState)value.State;
                if (Plain(item.Label.text) != text || (ObjectiveState)State.GetValue(item) != state)
                {
                    // The native method skips transitions between completed states.
                    // Reset its private display guard so the authoritative state wins.
                    State.SetValue(item, (ObjectiveState)(-1));
                    item.Label.text = text;
                    item.UpdateViewState(state);
                    // Preserve native formatting (including the failure strikethrough).
                    changed = true;
                }

                if (item.gameObject.activeSelf != value.Visible)
                {
                    item.gameObject.SetActive(value.Visible);
                    changed = true;
                }
            }

            foreach (var pair in items)
            {
                if (ReceivedIds.Contains(pair.Key) || pair.Value == null || !pair.Value.gameObject.activeSelf)
                    continue;
                pair.Value.gameObject.SetActive(false);
                changed = true;
            }

            if (!changed)
                return;
            group.TriggerAnimation();
            if (DiagnosticsEnabled)
                WriteDiagnostics(values, items);
        }

        static void WriteDiagnostics(ObjectiveStatus[] values, IDictionary<int, ObjectiveListItem> items)
        {
            GameBridge.Log("OBJECTIVES replica count=" + values.Length + " states=" + String.Join(",", values.Select(value => value.Id + ":" + value.State + ":" + value.Visible).ToArray()));
            try
            {
                Directory.CreateDirectory("UserData/GhpcCoop");
                var lines = new string[values.Length];
                for (int i = 0; i < values.Length; i++)
                {
                    var value = values[i];
                    ObjectiveListItem item;
                    string text = items.TryGetValue(value.Id, out item) && item != null && item.Label != null ? item.Label.text : "";
                    lines[i] = value.Id + "\t" + value.State + "\t" + value.Visible + "\t" + text;
                }

                File.WriteAllLines("UserData/GhpcCoop/objectives-diagnostic.txt", lines);
            }
            catch (IOException error)
            {
                WarnDiagnostic(error);
            }
            catch (UnauthorizedAccessException error)
            {
                WarnDiagnostic(error);
            }
        }

        static void WarnDiagnostic(Exception error)
        {
            if (diagnosticWarning)
                return;
            diagnosticWarning = true;
            GameBridge.Log("OBJECTIVES diagnostic write failed: " + error.Message);
        }
    }
}

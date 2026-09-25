using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Inventory
{
    /// <summary>
    /// What the player is carrying. Keep one in the scene: scripts use PlayerInventory.Instance,
    /// FSMs use the actions in PlayMaker's "Inventory" category.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerInventory : MonoBehaviour
    {
        [Serializable]
        public struct Entry
        {
            public ItemDefinition item;
            [Min(0)] public int count;
        }

        /// <summary>One inventory line as written to a save file.</summary>
        [Serializable]
        public class SavedEntry
        {
            public string id;
            public int count;
        }

        /// <summary>Lifetime totals for one item as written to a save file.</summary>
        [Serializable]
        public class SavedStats
        {
            public string id;
            public int collected;
            public int spent;
        }

        [Tooltip("List of every item, used to turn saved ids back into items.")]
        [SerializeField] private ItemDatabase database;
        [Tooltip("What the player starts with.")]
        [SerializeField] private List<Entry> startingItems = new();
        [Tooltip("Current contents while playing. For checking only: edits here are ignored.")]
        [SerializeField] private List<Entry> contents = new();

        private readonly Dictionary<ItemDefinition, int> counts = new();
        // Lifetime totals, e.g. every candy eaten and every candy spent. Loading a save doesn't count.
        private readonly Dictionary<ItemDefinition, int> collected = new();
        private readonly Dictionary<ItemDefinition, int> spent = new();

        public static PlayerInventory Instance { get; private set; }

        /// <summary>Raised after every change: the item, the change (+ added, - removed) and its new count.</summary>
        public event Action<ItemDefinition, int, int> Changed;

        public ItemDatabase Database => database;
        public IReadOnlyDictionary<ItemDefinition, int> Counts => counts;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[PlayerInventory] The scene already has an inventory ({Instance.name}); '{name}' is ignored.", this);
                return;
            }

            Instance = this;
            foreach (var entry in startingItems)
            {
                if (entry.item != null && entry.count > 0)
                    counts[entry.item] = ClampToStack(entry.item, GetCount(entry.item) + entry.count);
            }
            RefreshContents();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public int GetCount(ItemDefinition item)
        {
            return item != null && counts.TryGetValue(item, out var count) ? count : 0;
        }

        public bool Has(ItemDefinition item, int amount = 1)
        {
            return item != null && GetCount(item) >= amount;
        }

        public bool CanAdd(ItemDefinition item, int amount = 1)
        {
            if (item == null || amount <= 0) return false;
            return item.MaxStack <= 0 || GetCount(item) + amount <= item.MaxStack;
        }

        /// <summary>Adds up to the item's stack limit and returns how many were actually added.</summary>
        public int Add(ItemDefinition item, int amount = 1)
        {
            if (item == null || amount <= 0) return 0;

            int before = GetCount(item);
            int after = ClampToStack(item, before + amount);
            if (after == before) return 0;

            counts[item] = after;
            AddTo(collected, item, after - before);
            OnChanged(item, after - before, after);
            return after - before;
        }

        /// <summary>Removes the amount only if the player has enough of it.</summary>
        public bool TryRemove(ItemDefinition item, int amount = 1)
        {
            if (item == null || amount <= 0 || GetCount(item) < amount) return false;

            int after = GetCount(item) - amount;
            if (after == 0) counts.Remove(item);
            else counts[item] = after;

            AddTo(spent, item, amount);
            OnChanged(item, -amount, after);
            return true;
        }

        /// <summary>How many of the item were ever added, e.g. every candy eaten.</summary>
        public int GetCollected(ItemDefinition item)
        {
            return item != null && collected.TryGetValue(item, out var total) ? total : 0;
        }

        /// <summary>How many of the item were ever removed, e.g. every candy spent in the shop.</summary>
        public int GetSpent(ItemDefinition item)
        {
            return item != null && spent.TryGetValue(item, out var total) ? total : 0;
        }

        public List<SavedEntry> ToSaveData()
        {
            var saved = new List<SavedEntry>(counts.Count);
            foreach (var pair in counts)
                saved.Add(new SavedEntry { id = pair.Key.Id, count = pair.Value });
            return saved;
        }

        /// <summary>Replaces the contents with a saved list.</summary>
        public void LoadSaveData(IEnumerable<SavedEntry> saved)
        {
            var previous = new Dictionary<ItemDefinition, int>(counts);
            counts.Clear();

            if (saved != null)
            {
                foreach (var entry in saved)
                {
                    var item = FindSavedItem(entry.id);
                    if (item != null && entry.count > 0) counts[item] = ClampToStack(item, entry.count);
                }
            }

            RefreshContents();

            var touched = new HashSet<ItemDefinition>(previous.Keys);
            touched.UnionWith(counts.Keys);
            foreach (var item in touched)
            {
                previous.TryGetValue(item, out var before);
                int after = GetCount(item);
                if (after != before) Changed?.Invoke(item, after - before, after);
            }
        }

        public List<SavedStats> StatsToSaveData()
        {
            var items = new HashSet<ItemDefinition>(collected.Keys);
            items.UnionWith(spent.Keys);

            var saved = new List<SavedStats>(items.Count);
            foreach (var item in items)
                saved.Add(new SavedStats { id = item.Id, collected = GetCollected(item), spent = GetSpent(item) });
            return saved;
        }

        /// <summary>Replaces the lifetime totals with saved ones.</summary>
        public void LoadStatsSaveData(IEnumerable<SavedStats> saved)
        {
            collected.Clear();
            spent.Clear();
            if (saved == null) return;

            foreach (var entry in saved)
            {
                var item = FindSavedItem(entry.id);
                if (item == null) continue;
                if (entry.collected > 0) collected[item] = entry.collected;
                if (entry.spent > 0) spent[item] = entry.spent;
            }
        }

        private ItemDefinition FindSavedItem(string id)
        {
            var item = database != null ? database.Find(id) : null;
            if (item == null) Debug.LogWarning($"[PlayerInventory] Saved item '{id}' isn't in the item database; skipped.", this);
            return item;
        }

        private static void AddTo(Dictionary<ItemDefinition, int> totals, ItemDefinition item, int amount)
        {
            totals.TryGetValue(item, out var total);
            totals[item] = total + amount;
        }

        private static int ClampToStack(ItemDefinition item, int count)
        {
            return item.MaxStack > 0 ? Mathf.Min(count, item.MaxStack) : count;
        }

        private void OnChanged(ItemDefinition item, int change, int newCount)
        {
            RefreshContents();
            Changed?.Invoke(item, change, newCount);
        }

        private void RefreshContents()
        {
            contents.Clear();
            foreach (var pair in counts)
                contents.Add(new Entry { item = pair.Key, count = pair.Value });
        }
    }
}

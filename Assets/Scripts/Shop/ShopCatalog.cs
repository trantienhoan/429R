using System;
using System.Collections.Generic;
using Game.Inventory;
using UnityEngine;

namespace Game.Shopping
{
    [Serializable]
    public class ShopEntry
    {
        [Tooltip("What is sold. Its World Prefab is what appears when bought; without one it goes into the inventory.")]
        public ItemDefinition item;
        [Tooltip("Cost in the catalog's currency. 0 = free.")]
        [Min(0)] public int price = 5;
        [Tooltip("How many can be bought in total. 0 = unlimited.")]
        [Min(0)] public int stock;
    }

    /// <summary>
    /// An item that can come up on the special line (besides the normal list's items). A legendary one comes up only
    /// when a restock rolls a legendary offer (the catalog's Legendary Chance); others come up like the normal items.
    /// Its Chance says how often against the others of its kind. Each time it's offered it can be bought once.
    /// </summary>
    [Serializable]
    public class SpecialEntry : ShopEntry
    {
        [Tooltip("How often it comes up against the others of its kind (legendary against legendary, others against the " +
                 "other specials and the normal items, which count 1 each): 1 = as often as a normal item, 0.5 = half as often.")]
        [Min(0f)] public float chance = 1f;
        [Tooltip("Its chance while the pumpkin boss (pumpkin_boss_Z) is being fought.")]
        [Min(0f)] public float bossChance = 1f;
        [Tooltip("A legendary item: it only comes up on a legendary roll, and then the shop hand buzzes twice (once the " +
                 "special line is unlocked).")]
        public bool legendary;
    }

    /// <summary>What a shop sells and what it takes as payment. Create with Assets > Create > 429 Game > Shop > Catalog.</summary>
    [CreateAssetMenu(menuName = "429 Game/Shop/Catalog", fileName = "New Shop Catalog")]
    public class ShopCatalog : ScriptableObject
    {
        [Tooltip("What the player pays with, e.g. Candy.")]
        [SerializeField] private ItemDefinition currency;
        [SerializeField] private List<ShopEntry> entries = new();

        [Header("Special offers")]
        [Tooltip("The special line is an extra slot, picked again at every restock: first a roll for a legendary offer " +
                 "(Legendary Chance), otherwise one of the normal items (not one already on sale) or of the specials " +
                 "below that aren't legendary. Each offer can be bought once.")]
        [SerializeField] private List<SpecialEntry> specials = new();
        [Tooltip("Chance (0-1) a restock offers one of the legendary specials.")]
        [Range(0f, 1f)]
        [SerializeField] private float legendaryChance = 0.1f;
        [Tooltip("The same while the pumpkin boss (pumpkin_boss_Z) is being fought.")]
        [Range(0f, 1f)]
        [SerializeField] private float bossLegendaryChance = 0.5f;
        [Tooltip("The normal list's items come up on the special line too (each with chance 1). Off = only the specials.")]
        [SerializeField] private bool offerNormalItems = true;
        [Tooltip("What unlocking the special line costs, once and for good, e.g. Yellow Seed. Empty = never locked.")]
        [SerializeField] private ItemDefinition specialUnlockCurrency;
        [Min(0)]
        [SerializeField] private int specialUnlockPrice = 15;

        public ItemDefinition Currency => currency;
        public IReadOnlyList<ShopEntry> Entries => entries;
        public IReadOnlyList<SpecialEntry> Specials => specials;
        public float LegendaryChance => legendaryChance;
        public float BossLegendaryChance => bossLegendaryChance;
        public bool OfferNormalItems => offerNormalItems;
        public ItemDefinition SpecialUnlockCurrency => specialUnlockCurrency;
        public int SpecialUnlockPrice => specialUnlockPrice;

#if UNITY_EDITOR
        public void EditorSetCurrency(ItemDefinition item)
        {
            currency = item;
        }

        /// <summary>Adds the item for sale unless it's already listed. Returns true if it was added.</summary>
        public bool EditorAddEntry(ItemDefinition item, int price)
        {
            if (item == null || entries.Exists(entry => entry.item == item)) return false;
            entries.Add(new ShopEntry { item = item, price = price });
            return true;
        }

        /// <summary>Adds the item to the specials unless it's already one. Returns true if it was added.</summary>
        public bool EditorAddSpecial(ItemDefinition item, int price)
        {
            if (item == null || specials.Exists(entry => entry.item == item)) return false;
            specials.Add(new SpecialEntry { item = item, price = price });
            return true;
        }

        /// <summary>
        /// Makes the item a special at this price and these chances, adding it or changing it. Returns true if anything
        /// changed.
        /// </summary>
        public bool EditorSetSpecial(ItemDefinition item, int price, float chance, float bossChance, bool legendary)
        {
            if (item == null) return false;
            var entry = specials.Find(e => e.item == item);
            bool added = entry == null;
            if (added) specials.Add(entry = new SpecialEntry { item = item });
            bool changed = added || entry.price != price || !Mathf.Approximately(entry.chance, chance) ||
                           !Mathf.Approximately(entry.bossChance, bossChance) || entry.legendary != legendary;
            entry.price = price;
            entry.chance = chance;
            entry.bossChance = bossChance;
            entry.legendary = legendary;
            return changed;
        }

        /// <summary>Takes the item off the normal (not special) list. Returns true if it was there.</summary>
        public bool EditorRemoveEntry(ItemDefinition item)
        {
            return entries.RemoveAll(entry => entry.item == item) > 0;
        }

        public void EditorSetSpecialUnlock(ItemDefinition item, int price)
        {
            specialUnlockCurrency = item;
            specialUnlockPrice = price;
        }
#endif
    }
}

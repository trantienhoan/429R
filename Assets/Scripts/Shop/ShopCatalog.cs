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

    /// <summary>What a shop sells and what it takes as payment. Create with Assets > Create > 429 Game > Shop > Catalog.</summary>
    [CreateAssetMenu(menuName = "429 Game/Shop/Catalog", fileName = "New Shop Catalog")]
    public class ShopCatalog : ScriptableObject
    {
        [Tooltip("What the player pays with, e.g. Candy.")]
        [SerializeField] private ItemDefinition currency;
        [SerializeField] private List<ShopEntry> entries = new();

        public ItemDefinition Currency => currency;
        public IReadOnlyList<ShopEntry> Entries => entries;

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
#endif
    }
}

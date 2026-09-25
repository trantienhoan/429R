using System.Collections.Generic;
using UnityEngine;

namespace Game.Inventory
{
    /// <summary>
    /// List of every item in the game, so save files can turn item ids back into items.
    /// Kept up to date by Tools > 429 Game > Inventory > Refresh Item Database.
    /// </summary>
    [CreateAssetMenu(menuName = "429 Game/Inventory/Item Database", fileName = "ItemDatabase")]
    public class ItemDatabase : ScriptableObject
    {
        [SerializeField] private List<ItemDefinition> items = new();

        private Dictionary<string, ItemDefinition> byId;

        public IReadOnlyList<ItemDefinition> Items => items;

        public ItemDefinition Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            if (byId == null)
            {
                byId = new Dictionary<string, ItemDefinition>();
                foreach (var item in items)
                {
                    if (item != null) byId[item.Id] = item;
                }
            }

            return byId.TryGetValue(id, out var found) ? found : null;
        }

#if UNITY_EDITOR
        public void EditorSetItems(IEnumerable<ItemDefinition> newItems)
        {
            items = new List<ItemDefinition>(newItems);
            byId = null;
        }

        private void OnValidate()
        {
            byId = null;
        }
#endif
    }
}

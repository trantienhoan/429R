using UnityEngine;

namespace Game.Inventory
{
    /// <summary>
    /// One kind of item the player can carry, e.g. Candy.
    /// Create with Assets > Create > 429 Game > Inventory > Item.
    /// </summary>
    [CreateAssetMenu(menuName = "429 Game/Inventory/Item", fileName = "New Item")]
    public class ItemDefinition : ScriptableObject
    {
        [Tooltip("Id stored in save files. Never change it after release. Left empty, the asset name is used.")]
        [SerializeField] private string id;
        [Tooltip("Name shown to the player. Left empty, the asset name is used.")]
        [SerializeField] private string displayName;
        [SerializeField] private Sprite icon;
        [Tooltip("Optional prefab of this item in the world, e.g. what the shop hands over.")]
        [SerializeField] private GameObject worldPrefab;
        [Tooltip("Most the player can carry. 0 = no limit.")]
        [Min(0)]
        [SerializeField] private int maxStack;
        [Tooltip("Lower numbers are listed first, e.g. -1 keeps your money at the top. Equal numbers sort by name.")]
        [SerializeField] private int listOrder;

        public string Id => string.IsNullOrEmpty(id) ? name : id;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public Sprite Icon => icon;
        public GameObject WorldPrefab => worldPrefab;
        public int MaxStack => maxStack;
        public int ListOrder => listOrder;

#if UNITY_EDITOR
        /// <summary>Copies the asset name into the id so renaming the asset later can't break save files.</summary>
        public bool EditorFreezeId()
        {
            if (!string.IsNullOrEmpty(id)) return false;
            id = name;
            return true;
        }
#endif
    }
}

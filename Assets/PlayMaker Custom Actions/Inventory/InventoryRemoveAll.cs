using Game.Inventory;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Inventory")]
    [Tooltip("Takes away every one of an item the player carries, e.g. all their Wraps (Candy) when they die. It doesn't " +
             "count as spent in the shop.")]
    public class InventoryRemoveAll : FsmStateAction
    {
        [RequiredField]
        [ObjectType(typeof(ItemDefinition))]
        [Tooltip("The item to take away, e.g. Candy (the Wraps).")]
        public FsmObject item;

        [UIHint(UIHint.Variable)]
        [Tooltip("Optional: store how many were taken away.")]
        public FsmInt storeRemoved;

        public override void Reset()
        {
            item = null;
            storeRemoved = null;
        }

        public override void OnEnter()
        {
            var inventory = PlayerInventory.Instance;
            if (inventory == null) LogWarning("There is no PlayerInventory in the scene.");

            int removed = inventory != null ? inventory.RemoveAll(item.Value as ItemDefinition) : 0;
            if (storeRemoved != null && !storeRemoved.IsNone) storeRemoved.Value = removed;
            Finish();
        }
    }
}

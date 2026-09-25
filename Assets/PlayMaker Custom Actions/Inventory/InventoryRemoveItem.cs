using Game.Inventory;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Inventory")]
    [Tooltip("Removes items from the player's inventory. Nothing is removed unless the player has enough.")]
    public class InventoryRemoveItem : FsmStateAction
    {
        [RequiredField]
        [ObjectType(typeof(ItemDefinition))]
        [Tooltip("The item to remove.")]
        public FsmObject item;

        [Tooltip("How many to remove.")]
        public FsmInt amount;

        [Tooltip("Sent if the items were removed.")]
        public FsmEvent removedEvent;

        [Tooltip("Sent if the player doesn't have enough (nothing is removed).")]
        public FsmEvent notEnoughEvent;

        [UIHint(UIHint.Variable)]
        [Tooltip("Optional: store how many the player has afterwards.")]
        public FsmInt storeNewCount;

        public override void Reset()
        {
            item = null;
            amount = 1;
            removedEvent = null;
            notEnoughEvent = null;
            storeNewCount = null;
        }

        public override void OnEnter()
        {
            var inventory = PlayerInventory.Instance;
            var definition = item.Value as ItemDefinition;
            if (inventory == null) LogWarning("There is no PlayerInventory in the scene.");

            bool removed = inventory != null && inventory.TryRemove(definition, amount.Value);

            if (!storeNewCount.IsNone && inventory != null) storeNewCount.Value = inventory.GetCount(definition);
            Fsm.Event(removed ? removedEvent : notEnoughEvent);
            Finish();
        }
    }
}

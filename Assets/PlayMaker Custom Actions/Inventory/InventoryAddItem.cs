using Game.Inventory;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Inventory")]
    [Tooltip("Adds items to the player's inventory.")]
    public class InventoryAddItem : FsmStateAction
    {
        [RequiredField]
        [ObjectType(typeof(ItemDefinition))]
        [Tooltip("The item to add.")]
        public FsmObject item;

        [Tooltip("How many to add.")]
        public FsmInt amount;

        [UIHint(UIHint.Variable)]
        [Tooltip("Optional: store how many the player has afterwards.")]
        public FsmInt storeNewCount;

        [Tooltip("Sent if nothing could be added (no inventory in the scene, or the item's stack is full).")]
        public FsmEvent failedEvent;

        public override void Reset()
        {
            item = null;
            amount = 1;
            storeNewCount = null;
            failedEvent = null;
        }

        public override void OnEnter()
        {
            var inventory = PlayerInventory.Instance;
            var definition = item.Value as ItemDefinition;

            int added = 0;
            if (inventory == null) LogWarning("There is no PlayerInventory in the scene.");
            else added = inventory.Add(definition, amount.Value);

            if (!storeNewCount.IsNone && inventory != null) storeNewCount.Value = inventory.GetCount(definition);
            if (added == 0) Fsm.Event(failedEvent);
            Finish();
        }
    }
}

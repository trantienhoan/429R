using Game.Inventory;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Inventory")]
    [Tooltip("Waits until the inventory changes, then sends an event. Leave Item empty to react to any item.")]
    public class InventoryOnChanged : FsmStateAction
    {
        [ObjectType(typeof(ItemDefinition))]
        [Tooltip("Only react to this item (optional).")]
        public FsmObject item;

        [RequiredField]
        [Tooltip("The event to send when it changes.")]
        public FsmEvent sendEvent;

        [UIHint(UIHint.Variable)]
        [ObjectType(typeof(ItemDefinition))]
        [Tooltip("Optional: store the item that changed.")]
        public FsmObject storeItem;

        [UIHint(UIHint.Variable)]
        [Tooltip("Optional: store its new count.")]
        public FsmInt storeNewCount;

        [UIHint(UIHint.Variable)]
        [Tooltip("Optional: store how much it changed (positive = added, negative = removed).")]
        public FsmInt storeChange;

        private PlayerInventory subscribed;

        public override void Reset()
        {
            item = null;
            sendEvent = null;
            storeItem = null;
            storeNewCount = null;
            storeChange = null;
        }

        public override void OnEnter()
        {
            TrySubscribe();
        }

        public override void OnUpdate()
        {
            // The inventory may not exist yet when this state starts.
            if (subscribed == null) TrySubscribe();
        }

        public override void OnExit()
        {
            if (subscribed != null) subscribed.Changed -= OnInventoryChanged;
            subscribed = null;
        }

        private void TrySubscribe()
        {
            var inventory = PlayerInventory.Instance;
            if (inventory == null || inventory == subscribed) return;

            subscribed = inventory;
            subscribed.Changed += OnInventoryChanged;
        }

        private void OnInventoryChanged(ItemDefinition changedItem, int change, int newCount)
        {
            var filter = item != null && !item.IsNone ? item.Value as ItemDefinition : null;
            if (filter != null && filter != changedItem) return;

            if (!storeItem.IsNone) storeItem.Value = changedItem;
            if (!storeNewCount.IsNone) storeNewCount.Value = newCount;
            if (!storeChange.IsNone) storeChange.Value = change;
            Fsm.Event(sendEvent);
        }
    }
}

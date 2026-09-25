using Game.Inventory;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Inventory")]
    [Tooltip("Checks whether the player carries at least a given amount of an item.")]
    public class InventoryHasItem : FsmStateAction
    {
        [RequiredField]
        [ObjectType(typeof(ItemDefinition))]
        [Tooltip("The item to check.")]
        public FsmObject item;

        [Tooltip("The minimum amount.")]
        public FsmInt amount;

        [Tooltip("Sent if the player has enough.")]
        public FsmEvent trueEvent;

        [Tooltip("Sent if the player doesn't have enough.")]
        public FsmEvent falseEvent;

        [UIHint(UIHint.Variable)]
        [Tooltip("Optional: store the result.")]
        public FsmBool storeResult;

        [Tooltip("Repeat every frame.")]
        public bool everyFrame;

        public override void Reset()
        {
            item = null;
            amount = 1;
            trueEvent = null;
            falseEvent = null;
            storeResult = null;
            everyFrame = false;
        }

        public override void OnEnter()
        {
            DoCheck();
            if (!everyFrame) Finish();
        }

        public override void OnUpdate()
        {
            DoCheck();
        }

        private void DoCheck()
        {
            var inventory = PlayerInventory.Instance;
            bool has = inventory != null && inventory.Has(item.Value as ItemDefinition, amount.Value);

            if (!storeResult.IsNone) storeResult.Value = has;
            Fsm.Event(has ? trueEvent : falseEvent);
        }
    }
}

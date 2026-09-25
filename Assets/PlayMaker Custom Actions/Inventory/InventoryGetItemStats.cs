using Game.Inventory;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Inventory")]
    [Tooltip("Gets how many of an item the player ever collected and ever spent, e.g. every candy eaten and every candy " +
             "spent in the shop. Kept in the save file.")]
    public class InventoryGetItemStats : FsmStateAction
    {
        [RequiredField]
        [ObjectType(typeof(ItemDefinition))]
        [Tooltip("The item.")]
        public FsmObject item;

        [UIHint(UIHint.Variable)]
        [Tooltip("Where to store how many were ever collected.")]
        public FsmInt storeCollected;

        [UIHint(UIHint.Variable)]
        [Tooltip("Where to store how many were ever spent.")]
        public FsmInt storeSpent;

        [Tooltip("Repeat every frame.")]
        public bool everyFrame;

        public override void Reset()
        {
            item = null;
            storeCollected = null;
            storeSpent = null;
            everyFrame = false;
        }

        public override void OnEnter()
        {
            DoGetStats();
            if (!everyFrame) Finish();
        }

        public override void OnUpdate()
        {
            DoGetStats();
        }

        private void DoGetStats()
        {
            var inventory = PlayerInventory.Instance;
            var definition = item.Value as ItemDefinition;
            if (!storeCollected.IsNone) storeCollected.Value = inventory != null ? inventory.GetCollected(definition) : 0;
            if (!storeSpent.IsNone) storeSpent.Value = inventory != null ? inventory.GetSpent(definition) : 0;
        }
    }
}

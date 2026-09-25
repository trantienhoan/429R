using Game.Inventory;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Inventory")]
    [Tooltip("Gets how many of an item the player carries.")]
    public class InventoryGetItemCount : FsmStateAction
    {
        [RequiredField]
        [ObjectType(typeof(ItemDefinition))]
        [Tooltip("The item to count.")]
        public FsmObject item;

        [RequiredField]
        [UIHint(UIHint.Variable)]
        [Tooltip("Where to store the count.")]
        public FsmInt storeCount;

        [Tooltip("Repeat every frame.")]
        public bool everyFrame;

        public override void Reset()
        {
            item = null;
            storeCount = null;
            everyFrame = false;
        }

        public override void OnEnter()
        {
            DoGetCount();
            if (!everyFrame) Finish();
        }

        public override void OnUpdate()
        {
            DoGetCount();
        }

        private void DoGetCount()
        {
            var inventory = PlayerInventory.Instance;
            storeCount.Value = inventory != null ? inventory.GetCount(item.Value as ItemDefinition) : 0;
        }
    }
}

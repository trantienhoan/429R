using Game.Inventory;
using Game.Shopping;
using UnityEngine;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Shop")]
    [Tooltip("Waits until the player buys something, then sends an event. Leave Item empty to react to any purchase.")]
    public class ShopOnPurchase : FsmStateAction
    {
        [ObjectType(typeof(ItemDefinition))]
        [Tooltip("Only react to this item (optional).")]
        public FsmObject item;

        [RequiredField]
        [Tooltip("The event to send after a purchase.")]
        public FsmEvent sendEvent;

        [UIHint(UIHint.Variable)]
        [ObjectType(typeof(ItemDefinition))]
        [Tooltip("Optional: store the item that was bought.")]
        public FsmObject storeItem;

        [UIHint(UIHint.Variable)]
        [Tooltip("Optional: store the price paid.")]
        public FsmInt storePrice;

        [UIHint(UIHint.Variable)]
        [Tooltip("Optional: store the object that appeared (empty if the item went into the inventory).")]
        public FsmGameObject storeSpawnedObject;

        public override void Reset()
        {
            item = null;
            sendEvent = null;
            storeItem = null;
            storePrice = null;
            storeSpawnedObject = null;
        }

        public override void OnEnter()
        {
            Shop.AnyPurchased += OnPurchased;
        }

        public override void OnExit()
        {
            Shop.AnyPurchased -= OnPurchased;
        }

        private void OnPurchased(Shop shop, ItemDefinition bought, int price, GameObject spawned)
        {
            var filter = item != null && !item.IsNone ? item.Value as ItemDefinition : null;
            if (filter != null && filter != bought) return;

            if (!storeItem.IsNone) storeItem.Value = bought;
            if (!storePrice.IsNone) storePrice.Value = price;
            if (!storeSpawnedObject.IsNone) storeSpawnedObject.Value = spawned;
            Fsm.Event(sendEvent);
        }
    }
}

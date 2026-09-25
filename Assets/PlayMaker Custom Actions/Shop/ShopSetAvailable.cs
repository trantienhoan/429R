using Game.Shopping;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Shop")]
    [Tooltip("Turns shopping on or off, e.g. off during a boss fight. Turning it off also closes an open shop. " +
             "Leave Shop empty to switch every shop.")]
    public class ShopSetAvailable : FsmStateAction
    {
        [CheckForComponent(typeof(Shop))]
        [Tooltip("One shop, e.g. Candy Shop. Leave empty for all shops.")]
        public FsmGameObject shop;

        [RequiredField]
        [Tooltip("Whether the shop button opens the shop.")]
        public FsmBool available;

        public override void Reset()
        {
            shop = null;
            available = true;
        }

        public override void OnEnter()
        {
            var target = shop.IsNone || shop.Value == null ? null : shop.Value.GetComponentInParent<Shop>();
            if (target != null) target.SetAvailable(available.Value);
            else Shop.SetAllAvailable(available.Value);
            Finish();
        }
    }
}

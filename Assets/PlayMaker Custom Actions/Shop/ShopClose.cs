using Game.Shopping;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Shop")]
    [Tooltip("Closes a shop's panel.")]
    public class ShopClose : FsmStateAction
    {
        [CheckForComponent(typeof(Shop))]
        [Tooltip("The object with the Shop, e.g. Candy Shop. Leave empty to use the current shop.")]
        public FsmGameObject shop;

        public override void Reset()
        {
            shop = null;
        }

        public override void OnEnter()
        {
            var target = Shop.Resolve(shop.IsNone ? null : shop.Value);
            if (target != null) target.Close();
            Finish();
        }
    }
}

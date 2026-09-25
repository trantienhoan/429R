using Game.Shopping;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Shop")]
    [Tooltip("Opens the shop panel in front of the player, as if they pressed the shop button. It stays open until closed.")]
    public class ShopOpen : FsmStateAction
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
            if (target != null) target.Open();
            else LogWarning("No shop found. Run Tools > 429 Game > Shop > Set Up Candy Shop.");
            Finish();
        }
    }
}

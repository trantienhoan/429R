using UnityEngine;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Menus")]
    [Tooltip("Shows the Congratulation Warning (the end-of-game thank-you notice) in front of the player. The scene " +
             "needs one: Tools > 429 Game > Menu > Set Up Congratulation Warning.")]
    public class ShowCongratulationWarning : FsmStateAction
    {
        public override void OnEnter()
        {
            if (!Game.Menus.CongratulationWarning.ShowInScene())
                Debug.LogWarning("[Congratulation Warning] The scene has none. Run Tools > 429 Game > Menu > Set Up Congratulation Warning.");
            Finish();
        }
    }
}

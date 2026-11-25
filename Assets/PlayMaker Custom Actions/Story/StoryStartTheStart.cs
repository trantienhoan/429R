using Game.Story;
using UnityEngine;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Story")]
    [Tooltip("The start rooftop by the story (Story Start): put in GAMESTAGES \"Stage 1 - The Start\" right after the " +
             "start pumpkin's point is switched on. Remembers in the save that the player has met the Fairy. When she is " +
             "free (the final boss beaten) there is no pumpkin: its point is switched off again before it drops one, the " +
             "walk signs go and the dark ends; the free Fairy's board leads on (gates, or the seed that starts the game).")]
    public class StoryStartTheStart : FsmStateAction
    {
        [Tooltip("The start pumpkin's spawn point (Start_Pumpkin_Pos).")]
        public FsmGameObject startPumpkinPoint;

        [Tooltip("The walk tutorial signs (Walk Tutorial Signs), switched off as \"Send death\" does.")]
        public FsmGameObject walkSigns;

        public override void Reset()
        {
            startPumpkinPoint = null;
            walkSigns = null;
        }

        public override void OnEnter()
        {
            var mode = StoryStart.Current;
            StoryStart.Remember(StoryStart.MetFairy);
            if (mode == StoryStart.Mode.FairyFree)
            {
                if (startPumpkinPoint.Value != null) startPumpkinPoint.Value.SetActive(false);
                if (walkSigns.Value != null) walkSigns.Value.SetActive(false);
                StoryDarkWalk.End(2.5f);
            }
            Finish();
        }
    }
}

using Game.Story;
using UnityEngine;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Story")]
    [Tooltip("The start rooftop by the story (Story Start): put FIRST in GAMESTAGES \"Move to Fairy\". First meeting: no " +
             "guide Knight (Knight Point off). Later runs: the Knight is back; the Fairy made in this state is dressed a " +
             "frame later (first meeting: she will dodge the first pumpkin by teleporting): missing (only the empty jail) until the final boss is beaten, then free (no cage, no lock " +
             "tower, her board), and in that case Freed Event is sent (to \"Stage 1 - The Start\": no walk to a cage, no " +
             "Send death). Otherwise it finishes and the state goes on as before.")]
    public class StoryStartMeeting : FsmStateAction
    {
        [Tooltip("The guide Knight's spawn point (Knight_pos under Fairy_Pos): off for the first meeting.")]
        public FsmGameObject knightPoint;

        [Tooltip("The seed the free Fairy gives (Seed_Bracer, the first chest's seed).")]
        public FsmGameObject seed;

        [Tooltip("Sent when she is free (the boss beaten): leads to \"Stage 1 - The Start\".")]
        public FsmEvent freedEvent;

        private StoryStart.Mode mode;
        private int frames;

        public override void Reset()
        {
            knightPoint = null;
            seed = null;
            freedEvent = null;
        }

        public override void OnEnter()
        {
            mode = StoryStart.Current;
            frames = 0;
            if (knightPoint.Value != null) knightPoint.Value.SetActive(mode != StoryStart.Mode.FirstMeeting);
        }

        // The Fairy is made by this state's later actions: dress her once she is there.
        public override void OnUpdate()
        {
            frames++;
            var fairy = StoryStart.FindFairy();
            if (fairy == null)
            {
                if (frames > 90) Finish();
                return;
            }
            StoryStart.Dress(fairy, mode, seed.Value);
            if (mode == StoryStart.Mode.FairyFree && freedEvent != null) Fsm.Event(freedEvent);
            else Finish();
        }
    }
}

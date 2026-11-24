using Game;
using Game.Story;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Story")]
    [Tooltip("The story's seed pieces go into the dirt: one green glowing piece for each seed taken this run (each true " +
             "GAMESTAGES bool in Pieces) pops out in front of the player, flies to above this spot, they join into one and " +
             "it dives in with a puff, over Seconds. Finishes at once: the state's own Wait still times what comes next " +
             "(on the rooftop's Pumpkin_Tree_Pos, the tree).")]
    public class SeedsIntoSoil : FsmStateAction
    {
        [RequiredField]
        [Tooltip("The spot the seed dives into (the dirt).")]
        public FsmOwnerDefault gameObject;

        [RequiredField]
        [Tooltip("What a piece looks like: a seed prefab. It only shows (its FSM never starts).")]
        public FsmGameObject piece;

        [Tooltip("The smoke puff where the joined seed goes in.")]
        public FsmGameObject puff;

        [Tooltip("GAMESTAGES bools: one piece for each that is true (the seeds taken this run).")]
        public FsmString[] pieces;

        [Tooltip("At least this many pieces, even if no seed was taken.")]
        public FsmInt atLeast;

        [Tooltip("Seconds for the whole flight, join and dive.")]
        public FsmFloat seconds;

        public override void Reset()
        {
            gameObject = null;
            piece = null;
            puff = null;
            pieces = new FsmString[] { "Bracer_Gear", "Shield_Gear", "Head_Gear", "Chest_Gear", "Boots_Gear" };
            atLeast = 1;
            seconds = 2.75f;
        }

        public override void OnEnter()
        {
            var spot = Fsm.GetOwnerDefaultTarget(gameObject);
            if (spot != null)
            {
                int count = 0;
                if (pieces != null)
                    foreach (var flag in pieces)
                        if (flag != null && GameStages.GetBool(flag.Value)) count++;
                if (count < atLeast.Value) count = atLeast.Value;
                StorySeedsFlight.Play(spot.transform.position, piece.Value, puff.Value, count, seconds.Value);
            }
            Finish();
        }
    }
}

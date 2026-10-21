using Game.Bonus;
using UnityEngine;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Bonus")]
    [Tooltip("Plays the nesting pumpkin bonus round if the player earned it in the stage before (see Watch Bonus Round), " +
             "then finishes. Not earned: finishes at once. The bonus area comes in the middle (on the floor that's " +
             "there) and goes again when the round is over.")]
    public class PlayBonusRound : FsmStateAction
    {
        [RequiredField]
        [Tooltip("The bonus area prefab (Bonus_Area).")]
        public FsmGameObject area;

        [Tooltip("Optional: taken away before the bonus area comes. Empty = nothing (the room's floor stays as the stage's " +
                 "floor: the bonus area's own floor is invisible).")]
        public FsmGameObject removeFirst;

        [Tooltip("Plays it even if the player didn't earn it (for testing).")]
        public FsmBool always;

        [Tooltip("Sent when the round is over or wasn't earned. Empty: the state just finishes (FINISHED).")]
        public FsmEvent finishEvent;

        private GameObject spawned;
        private BonusRound round;

        public override void Reset()
        {
            area = null;
            removeFirst = null;
            always = false;
            finishEvent = null;
        }

        public override void OnEnter()
        {
            bool earned = BonusRound.Earned || always.Value;
            BonusRound.ForgetStage();
            if (!earned || area.Value == null)
            {
                Done();
                return;
            }
            if (removeFirst.Value != null) Object.Destroy(removeFirst.Value);
            var stuff = FsmVariables.GlobalVariables.FindFsmGameObject("CurrentlySpawnedStuffs");
            var parent = stuff != null && stuff.Value != null ? stuff.Value.transform : null;
            spawned = Object.Instantiate(area.Value, Vector3.zero, Quaternion.identity, parent);
            round = spawned.GetComponentInChildren<BonusRound>();
        }

        public override void OnUpdate()
        {
            if (spawned == null || round == null || round.Finished) Done();
        }

        public override void OnExit()
        {
            // Over, or left early (e.g. game over): the area goes.
            if (spawned != null) Object.Destroy(spawned);
            spawned = null;
            round = null;
        }

        private void Done()
        {
            Finish();
            if (finishEvent != null) Fsm.Event(finishEvent);
        }
    }
}

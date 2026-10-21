using Game.Bonus;
using UnityEngine;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Bonus")]
    [Tooltip("Plays the nesting pumpkin bonus round if the player earned it in the stage before (see Watch Bonus Round), " +
             "then finishes. Not earned: finishes at once, and the bonus stage doesn't come. Earned: switches the bonus " +
             "stage on (e.g. --Bedroom--MiniGame--: the field and the pumpkin chest) and starts the round there; punching " +
             "the chest open lets the giant out and the countdown starts. Finishes when the round is over and the " +
             "candies are eaten (the next state takes the field away: Destroy Object $BOSS_AREA).")]
    public class PlayBonusRound : FsmStateAction
    {
        [RequiredField]
        [Tooltip("The bonus round prefab (Bonus_Round).")]
        public FsmGameObject round;

        [RequiredField]
        [Tooltip("The bonus stage, switched on when the round was earned (e.g. --Bedroom--MiniGame--): its chest makes " +
                 "the giant. The round goes where it is (its height is the floor's). Empty: no bonus, the state just goes on.")]
        public FsmGameObject stage;

        [Tooltip("Plays it even if the player didn't earn it (for testing).")]
        public FsmBool always;

        [Tooltip("Sent when the round is over or wasn't earned. Empty: the state just finishes (FINISHED).")]
        public FsmEvent finishEvent;

        private BonusRound running;

        public override void Reset()
        {
            round = null;
            stage = null;
            always = false;
            finishEvent = null;
        }

        public override void OnEnter()
        {
            bool earned = BonusRound.Earned || always.Value;
            BonusRound.ForgetStage();
            // No stage, no chest to let the giant out: no round (it would wait for good).
            if (!earned || round.Value == null || stage.Value == null)
            {
                Done();
                return;
            }
            stage.Value.SetActive(true);
            var at = stage.Value.transform.position;
            var stuff = FsmVariables.GlobalVariables.FindFsmGameObject("CurrentlySpawnedStuffs");
            var parent = stuff != null && stuff.Value != null ? stuff.Value.transform : null;
            running = Object.Instantiate(round.Value, at, Quaternion.identity, parent).GetComponentInChildren<BonusRound>();
        }

        public override void OnUpdate()
        {
            if (running == null || running.Finished) Done();
        }

        public override void OnExit()
        {
            // Over, or left early (e.g. game over): the round goes (the field goes with the next state's clean-up).
            if (running != null) Object.Destroy(running.gameObject);
            running = null;
        }

        private void Done()
        {
            Finish();
            if (finishEvent != null) Fsm.Event(finishEvent);
        }
    }
}

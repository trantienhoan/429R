using Game.Bonus;
using UnityEngine;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Bonus")]
    [Tooltip("Plays a bonus round if the player earned it in the stage before (see Watch Bonus Round), then finishes. " +
             "Not earned: finishes at once, and the bonus stage doesn't come. Earned: switches the bonus stage on (e.g. " +
             "--Bedroom--MiniGame--: the field and the pumpkin chest) and starts the round there (the nesting pumpkins: " +
             "punching the chest open lets the giant out; Paint Bees: standing on the ring in the middle). Finishes when " +
             "the round is over and the candies are eaten (the next state takes the field away: Destroy Object $BOSS_AREA).")]
    public class PlayBonusRound : FsmStateAction
    {
        [RequiredField]
        [Tooltip("The bonus round prefab (Bonus_Round: the nesting pumpkins; Paint_Bees_Round).")]
        public FsmGameObject round;

        [RequiredField]
        [Tooltip("The bonus stage, switched on when the round was earned (e.g. --Bedroom--MiniGame--): its chest makes " +
                 "the giant. The round goes where it is (its height is the floor's). Empty: no bonus, the state just goes on.")]
        public FsmGameObject stage;

        [Tooltip("Plays it even if the player didn't earn it (for testing).")]
        public FsmBool always;

        [Tooltip("Sent when the round is over or wasn't earned. Empty: the state just finishes (FINISHED).")]
        public FsmEvent finishEvent;

        // The round's component (a Bonus Round or a Paint Bees Round: see IBonusGame).
        private Component running;

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
            running = Object.Instantiate(round.Value, at, Quaternion.identity, parent).GetComponentInChildren<IBonusGame>() as Component;
            // A special stage (Types 1 and 2): level-0 stats until the round goes (user, 2026-11-10). Added here, not on
            // the prefabs, so their setup menus can't drop it; OnExit's Destroy gives the stats back.
            if (running != null) running.gameObject.AddComponent<Game.Leveling.SpecialStageStats>();
        }

        public override void OnUpdate()
        {
            // Not while the player is down (coins in the air, getting up, dead): ending gives the stats back (Special Stage
            // Stats), which would heal a player at 0 health and pop the Level Up Notice over them, and after a game over
            // it would move GAMESTAGES on to the next checkpoint. Dead: the Game Over takes the round away instead.
            if (running == null || (((IBonusGame)running).Finished && !PlayerDown())) Done();
        }

        // Down like Daruma Stage's Player Down: the lucky coins in the air, getting up, or 0 health (dead).
        private static bool PlayerDown() =>
            Game.Saving.Checkpoints.IsDying ||
            (Game.Combat.PlayerRevive.Instance != null && Game.Combat.PlayerRevive.Instance.IsReviving) ||
            (Game.Saving.PlayerBoosts.TryGetHealth(out float health) && health <= 0f);

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

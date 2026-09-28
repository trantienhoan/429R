using Game.Enemies;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Enemies")]
    [Tooltip("Waits until fewer enemies than the limit are alive, then finishes. Put it in its own state just before " +
             "the state that spawns an enemy, so a wave waits for the player to kill some instead of piling up. " +
             "Change the limit on the Enemy Limit object (Tools > 429 Game > Enemies > Set Up Enemy Limit).")]
    public class WaitForEnemySlot : FsmStateAction
    {
        [Tooltip("Most enemies alive at once for this spawner. 0 = the Enemy Limit's number (12 on Quest, 20 on PC unless you change it).")]
        public FsmInt maxEnemies;

        [Tooltip("Event to send when there's room. Left empty, the state just finishes (FINISHED).")]
        public FsmEvent finishEvent;

        public override void Reset()
        {
            maxEnemies = 0;
            finishEvent = null;
        }

        public override void OnEnter()
        {
            TryContinue();
        }

        public override void OnUpdate()
        {
            TryContinue();
        }

        private void TryContinue()
        {
            // Nothing dies while the game is paused, so no spawn gets through then either.
            if (UnityEngine.Time.timeScale == 0f || !EnemyLimit.TryTakeSlot(maxEnemies.Value)) return;

            Finish();
            if (finishEvent != null) Fsm.Event(finishEvent);
        }
    }
}

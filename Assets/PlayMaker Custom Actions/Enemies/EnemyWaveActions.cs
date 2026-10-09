using Game.Enemies;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Enemies")]
    [Tooltip("Starts a new wave for this FSM: the enemies it spawned before stop counting. Put it in the first state.")]
    public class StartWave : FsmStateAction
    {
        public override void OnEnter()
        {
            EnemyWave.Start(Fsm);
            Finish();
        }
    }

    [ActionCategory("Enemies")]
    [Tooltip("Counts an enemy this FSM just spawned as part of its wave. Use the object Create Object stored.")]
    public class AddEnemyToWave : FsmStateAction
    {
        [RequiredField]
        [UIHint(UIHint.Variable)]
        [Tooltip("The enemy just spawned (Create Object's Store Object).")]
        public FsmGameObject enemy;

        public override void Reset()
        {
            enemy = null;
        }

        public override void OnEnter()
        {
            EnemyWave.Add(Fsm, enemy.Value);
            Finish();
        }
    }

    [ActionCategory("Enemies")]
    [Tooltip("Waits until fewer than Max Alive enemies of this FSM's wave are alive (and the Enemy Limit has room), " +
             "then finishes. Put it in its own state just before the state that spawns an enemy.")]
    public class WaitForWaveRoom : FsmStateAction
    {
        [RequiredField]
        [Tooltip("Most enemies of this wave alive at the same time.")]
        public FsmInt maxAlive;

        [Tooltip("Also wait for the Enemy Limit (all enemies in the game: 12 on Quest, 20 on PC).")]
        public FsmBool useEnemyLimit;

        [Tooltip("Event to send when there's room. Left empty, the state just finishes (FINISHED).")]
        public FsmEvent finishEvent;

        public override void Reset()
        {
            maxAlive = 4;
            useEnemyLimit = true;
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
            // Nothing dies while the game is paused, so nothing spawns then either.
            if (UnityEngine.Time.timeScale == 0f) return;
            if (EnemyWave.Alive(Fsm) >= maxAlive.Value) return;
            if (useEnemyLimit.Value && !EnemyLimit.TryTakeSlot()) return;

            Finish();
            if (finishEvent != null) Fsm.Event(finishEvent);
        }
    }

    [ActionCategory("Enemies")]
    [Tooltip("Waits until at most Left Alive enemies of this FSM's wave are still alive, then finishes. Use it before " +
             "calling the boss, so the boss comes when the wave is (nearly) beaten.")]
    public class WaitForWaveCleared : FsmStateAction
    {
        [Tooltip("Finish when this many or fewer are still alive. 0 = all of them dead.")]
        public FsmInt leftAlive;

        [Tooltip("Finish anyway after this many seconds, in case one is stuck somewhere it can't be reached. 0 = never.")]
        public FsmFloat giveUpAfter;

        [UIHint(UIHint.Variable)]
        [Tooltip("Optional: store how many are still alive, every frame.")]
        public FsmInt storeAlive;

        [Tooltip("Event to send when cleared. Left empty, the state just finishes (FINISHED).")]
        public FsmEvent finishEvent;

        private float waited;

        public override void Reset()
        {
            leftAlive = 0;
            giveUpAfter = 90f;
            storeAlive = new FsmInt { UseVariable = true };
            finishEvent = null;
        }

        public override void OnEnter()
        {
            waited = 0f;
            Check();
        }

        public override void OnUpdate()
        {
            waited += UnityEngine.Time.deltaTime;
            Check();
        }

        private void Check()
        {
            int alive = EnemyWave.Alive(Fsm);
            if (!storeAlive.IsNone) storeAlive.Value = alive;
            if (alive > leftAlive.Value && (giveUpAfter.Value <= 0f || waited < giveUpAfter.Value)) return;

            Finish();
            if (finishEvent != null) Fsm.Event(finishEvent);
        }
    }
}

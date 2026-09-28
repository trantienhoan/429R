using Game.Enemies;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Enemies")]
    [Tooltip("Gets how many enemies are alive (objects tagged Enemy) and the most allowed at once.")]
    public class GetEnemyCount : FsmStateAction
    {
        [UIHint(UIHint.Variable)]
        [Tooltip("Where to store how many enemies are alive.")]
        public FsmInt storeCount;

        [UIHint(UIHint.Variable)]
        [Tooltip("Where to store the most enemies allowed at once on this platform.")]
        public FsmInt storeLimit;

        [Tooltip("Repeat every frame.")]
        public bool everyFrame;

        public override void Reset()
        {
            storeCount = null;
            storeLimit = null;
            everyFrame = false;
        }

        public override void OnEnter()
        {
            DoGetCount();
            if (!everyFrame) Finish();
        }

        public override void OnUpdate()
        {
            DoGetCount();
        }

        private void DoGetCount()
        {
            if (!storeCount.IsNone) storeCount.Value = EnemyLimit.AliveCount;
            if (!storeLimit.IsNone) storeLimit.Value = EnemyLimit.Max;
        }
    }
}

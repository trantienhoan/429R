namespace Enemies
{
    public class KamikazeState : IState
    {
        private ShadowMonster monster;

        public KamikazeState(ShadowMonster monster)
        {
            this.monster = monster;
        }

        public void OnEnter()
        {
            if (monster.agent != null && monster.agent.isActiveAndEnabled && monster.agent.isOnNavMesh)
            {
                monster.agent.isStopped = true;
            }
            monster.StartCharge();
        }

        public void Tick()
        {
            if (!monster.isGrounded || monster.IsBeingHeld || monster.healthComponent.IsDead())
            {
                monster.stateMachine.ChangeState(new ChaseState(monster));
                return;
            }

            float distance = monster.GetDistanceToTarget();
            if (distance > monster.kamikazeRange || monster.healthComponent.GetHealthPercentage() >= 0.19f)
            {
                monster.stateMachine.ChangeState(new ChaseState(monster));
            }
            else if (monster.IsChargeComplete())
            {
                monster.Explode();
            }
        }

        public void OnExit()
        {
            if (monster.agent != null && monster.agent.isActiveAndEnabled && monster.agent.isOnNavMesh)
            {
                monster.agent.isStopped = false;
            }
            monster.animator.SetBool("isCharging", false);
        }
    }
}
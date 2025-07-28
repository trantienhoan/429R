namespace Enemies
{
    public class ChargeState : IState
    {
        private ShadowMonster monster;
        private bool isKamikaze;

        public ChargeState(ShadowMonster monster, bool isKamikaze = false)
        {
            this.monster = monster;
            this.isKamikaze = isKamikaze;
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
                monster.stateMachine.ChangeState(new HurtState(monster)); // Transition to HurtState on conditions
                return;
            }

            float distance = monster.GetDistanceToTarget();
            if (isKamikaze)
            {
                if (distance > monster.kamikazeRange || monster.healthComponent.GetHealthPercentage() >= 0.19f)
                {
                    monster.stateMachine.ChangeState(new ChaseState(monster));
                }
                else if (monster.IsChargeComplete())
                {
                    monster.Explode();
                }
            }
            else
            {
                if (distance > monster.attackRange || monster.healthComponent.GetHealthPercentage() < 0.19f)
                {
                    monster.stateMachine.ChangeState(new ChaseState(monster));
                }
                else if (monster.IsChargeComplete())
                {
                    monster.PerformAttack();
                    monster.stateMachine.ChangeState(new IdleState(monster));
                }
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
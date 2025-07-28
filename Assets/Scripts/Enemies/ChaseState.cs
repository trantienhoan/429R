using UnityEngine;

namespace Enemies
{
    public class ChaseState : IState
    {
        private ShadowMonster monster;

        public ChaseState(ShadowMonster monster)
        {
            this.monster = monster;
        }

        public void OnEnter()
        {
            if (monster.agent != null && monster.agent.isActiveAndEnabled && monster.agent.isOnNavMesh)
            {
                monster.animator.SetBool("isRunning", true);
            }
        }

        public void Tick()
        {
            if (!monster.isGrounded || monster.IsBeingHeld || monster.healthComponent.IsDead()) return;

            Transform target = monster.GetClosestTarget();
            float distance = monster.GetDistanceToTarget();

            if (target == null || distance > monster.chaseRange)
            {
                monster.stateMachine.ChangeState(new IdleState(monster));
            }
            else if (distance <= monster.attackRange && monster.healthComponent.GetHealthPercentage() >= 0.19f)
            {
                monster.stateMachine.ChangeState(new ChargeState(monster, isKamikaze: false));
            }
            else if (distance <= monster.kamikazeRange && monster.healthComponent.GetHealthPercentage() < 0.19f)
            {
                monster.stateMachine.ChangeState(new ChargeState(monster, isKamikaze: true));
            }
            else
            {
                if (monster.agent != null && monster.agent.isActiveAndEnabled && monster.agent.isOnNavMesh)
                {
                    monster.agent.SetDestination(target.position);
                }
            }
        }

        public void OnExit()
        {
            monster.animator.SetBool("isRunning", false);
        }
    }
}
using UnityEngine;

namespace Enemies
{
    public class ChaseState : IState
    {
        private ShadowMonster monster;

        public ChaseState(ShadowMonster monster) { this.monster = monster; }

        public void OnEnter()
        {
            if (monster.animator != null)
            {
                monster.animator.SetBool("isRunning", true);
                monster.animator.SetBool("isCharging", false);
                monster.animator.SetBool("isGrounded", monster.isGrounded);
                monster.animator.ResetTrigger("Attack");
                monster.animator.Update(0f);
            }
            monster.EnsureAgentOnNavMesh();
            if (monster.agent != null && monster.agent.isActiveAndEnabled)
            {
                monster.agent.isStopped = false;
                monster.agent.stoppingDistance = monster.attackRange - 0.5f;
            }
            monster.currentTarget = monster.GetClosestTarget();
        }

        public void Tick()
        {
            if (monster.currentTarget == null)
            {
                monster.stateMachine.ChangeState(new IdleState(monster));
                return;
            }
            monster.agent.SetDestination(monster.currentTarget.position);
            float distance = monster.GetDistanceToTarget();
            if (distance <= monster.attackRange && Time.time >= monster.lastAttackTime + monster.attackCooldown)
            {
                monster.stateMachine.ChangeState(new ChargeState(monster));
            }
            else if (distance > monster.chaseRange)
            {
                monster.stateMachine.ChangeState(new IdleState(monster));
            }
        }

        public void OnExit()
        {
            if (monster.animator != null) monster.animator.SetBool("isRunning", false);
            if (monster.agent != null) monster.agent.isStopped = true;
        }
    }
}
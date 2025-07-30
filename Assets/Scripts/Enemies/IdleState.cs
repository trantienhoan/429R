using UnityEngine;

namespace Enemies
{
    public class IdleState : IState
    {
        private ShadowMonster monster;
        private float idleTimer;

        public IdleState(ShadowMonster monster) { this.monster = monster; }

        public void OnEnter()
        {
            idleTimer = 0f;
            monster.ResetChargeTimer();
            if (monster.animator != null)
            {
                monster.animator.SetBool("isRunning", false);
                monster.animator.SetBool("isCharging", false);
                monster.animator.SetBool("isGrounded", monster.isGrounded);
                monster.animator.ResetTrigger("Attack");
                monster.animator.ResetTrigger("KamikazeAttack");
                monster.animator.Update(0f);
            }
            if (monster.agent != null && monster.agent.isActiveAndEnabled && monster.agent.isOnNavMesh)
            {
                monster.agent.isStopped = true;
            }
        }

        public void Tick()
        {
            idleTimer += Time.deltaTime;
            float distance = monster.GetDistanceToTarget();
            if (monster.currentTarget != null && distance <= monster.chaseRange && monster.isGrounded && !monster.IsBeingHeld && Time.time >= monster.lastAttackTime + monster.attackCooldown)
            {
                monster.stateMachine.ChangeState(new ChaseState(monster));
                return;
            }
            if (idleTimer >= monster.idleTimeBeforeDive && monster.isGrounded)
            {
                monster.stateMachine.ChangeState(new DiveState(monster));
            }
        }

        public void OnExit() { }
    }
}
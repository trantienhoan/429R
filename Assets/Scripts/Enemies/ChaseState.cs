using UnityEngine;

namespace Enemies
{
    public class ChaseState : IState
    {
        private static readonly int IsRunning = Animator.StringToHash("isRunning");
        private static readonly int IsCharging = Animator.StringToHash("isCharging");
        private static readonly int IsGrounded = Animator.StringToHash("isGrounded");
        private readonly ShadowMonster monster;

        public ChaseState(ShadowMonster monster) { this.monster = monster; }

        public void OnEnter()
        {
            if (monster.animator != null)
            {
                monster.animator.SetBool(IsRunning, true);
                monster.animator.SetBool(IsCharging, false);
                monster.animator.SetBool(IsGrounded, monster.isGrounded);
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
                //Debug.Log("[ChaseState] No target, returning to Idle");
                monster.stateMachine.ChangeState(new IdleState(monster));
                return;
            }
            monster.agent.SetDestination(monster.currentTarget.position);
            monster.IsGrounded();  // Re-check
            if (!monster.isGrounded) monster.EnsureAgentOnNavMesh();
            float distance = monster.GetDistanceToTarget();
            //Debug.Log($"[ChaseState Tick] Distance: {distance}, CooldownReady: {Time.time >= monster.lastAttackTime + monster.attackCooldown}");
            if (distance <= monster.attackRange && Time.time >= monster.lastAttackTime + monster.attackCooldown)
            {
                //Debug.Log("[ChaseState] In attack range, switching to ChargeState");
                monster.stateMachine.ChangeState(new ChargeState(monster));
            }
            else if (distance > monster.chaseRange)
            {
                //Debug.Log("[ChaseState] Out of chase range, returning to Idle");
                monster.stateMachine.ChangeState(new IdleState(monster));
            }
        }

        public void OnExit()
        {
            if (monster.animator != null) monster.animator.SetBool(IsRunning, false);
            if (monster.agent != null && monster.agent.isActiveAndEnabled) // Add check
            {
                monster.agent.isStopped = true;
            }
        }
    }
}
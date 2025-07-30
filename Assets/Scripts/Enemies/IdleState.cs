using UnityEngine;

namespace Enemies
{
    public class IdleState : IState
    {
        private ShadowMonster monster;
        private float idleTimer;

        public IdleState(ShadowMonster monster)
        {
            this.monster = monster;
        }

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
                Debug.Log($"[IdleState {monster.gameObject.name}] OnEnter: Set isRunning=false, isCharging=false, isGrounded={monster.isGrounded}");
            }
            if (monster.agent != null && monster.agent.isActiveAndEnabled && monster.agent.isOnNavMesh)
            {
                monster.agent.isStopped = true;
                Debug.Log($"[IdleState {monster.gameObject.name}] OnEnter: NavMeshAgent stopped");
            }
            Debug.Log($"[IdleState {monster.gameObject.name}] Entered IdleState");
        }

        public void Tick()
        {
            idleTimer += Time.deltaTime;

            Transform target = monster.GetClosestTarget();
            float distance = monster.GetDistanceToTarget();
            if (target != null && distance <= monster.chaseRange && !monster.IsBeingHeld && monster.isGrounded && Time.time >= monster.lastAttackTime + monster.attackCooldown)
            {
                if (monster.agent != null && monster.agent.isActiveAndEnabled && monster.agent.isOnNavMesh)
                {
                    Debug.Log($"[IdleState {monster.gameObject.name}] Transitioning to ChaseState (distance: {distance}, chaseRange: {monster.chaseRange})");
                    monster.stateMachine.ChangeState(new ChaseState(monster));
                }
                else
                {
                    Debug.LogWarning($"[IdleState {monster.gameObject.name}] Cannot transition to ChaseState, NavMeshAgent not ready (Active: {(monster.agent != null ? monster.agent.isActiveAndEnabled : false)}, OnNavMesh: {(monster.agent != null ? monster.agent.isOnNavMesh : false)})");
                }
                return;
            }

            if (idleTimer >= monster.idleTimeBeforeDive && monster.isGrounded)
            {
                if (monster.animator != null)
                {
                    monster.animator.SetTrigger("Dive");
                }
                Debug.Log($"[IdleState {monster.gameObject.name}] Transitioning to DiveState");
                monster.stateMachine.ChangeState(new DiveState(monster));
            }
        }

        public void OnExit()
        {
            if (monster.agent != null && monster.agent.isActiveAndEnabled && monster.agent.isOnNavMesh)
            {
                monster.agent.isStopped = true;
                Debug.Log($"[IdleState {monster.gameObject.name}] OnExit: NavMeshAgent stopped");
            }
            if (monster.animator != null)
            {
                monster.animator.SetBool("isRunning", false);
                monster.animator.SetBool("isCharging", false);
            }
            Debug.Log($"[IdleState {monster.gameObject.name}] Exited IdleState");
        }
    }
}
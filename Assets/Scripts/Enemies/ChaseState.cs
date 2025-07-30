using UnityEngine;

namespace Enemies
{
    public class ChaseState : IState
    {
        private ShadowMonster monster;
        private Transform target;

        public ChaseState(ShadowMonster monster)
        {
            this.monster = monster;
        }

        public void OnEnter()
        {
            target = monster.GetClosestTarget();
            if (monster.animator != null)
            {
                monster.animator.SetBool("isRunning", true);
                monster.animator.SetBool("isCharging", false);
                monster.animator.SetBool("isGrounded", monster.isGrounded);
                monster.animator.ResetTrigger("Attack");
                monster.animator.Update(0f);
                Debug.Log($"ChaseState OnEnter: Set isRunning = true, isCharging = false, isGrounded = {monster.isGrounded}, Animator isRunning = {monster.animator.GetBool("isRunning")}, isCharging = {monster.animator.GetBool("isCharging")}, Current State = {monster.animator.GetCurrentAnimatorStateInfo(0).fullPathHash} ({monster.GetStateName(monster.animator.GetCurrentAnimatorStateInfo(0).fullPathHash)})");
            }
            if (monster.agent != null && monster.agent.isActiveAndEnabled && monster.agent.isOnNavMesh)
            {
                monster.agent.isStopped = false;
                monster.agent.speed = 3.5f;
                monster.agent.stoppingDistance = 0.5f;
                if (target != null)
                {
                    monster.agent.SetDestination(target.position);
                    Debug.Log($"ChaseState: Set NavMesh destination to {target.name} at {target.position}, Agent Active = {monster.agent.isActiveAndEnabled}, OnNavMesh = {monster.agent.isOnNavMesh}");
                }
                else
                {
                    Debug.LogWarning("ChaseState: No target found, cannot set NavMesh destination");
                }
            }
            else
            {
                Debug.LogWarning($"ChaseState: NavMeshAgent is not active or not on NavMesh, Agent Active = {(monster.agent != null ? monster.agent.isActiveAndEnabled : false)}, OnNavMesh = {(monster.agent != null ? monster.agent.isOnNavMesh : false)}");
                monster.stateMachine.ChangeState(new IdleState(monster));
            }
            Debug.Log($"Entered ChaseState: Target = {(target != null ? target.name : "None")}");
        }

        public void Tick()
        {
            target = monster.GetClosestTarget();
            float distance = monster.GetDistanceToTarget();
            if (target == null || distance > monster.chaseRange)
            {
                Debug.Log($"[ChaseState {monster.gameObject.name}] Transitioning to IdleState (target: {target}, distance: {distance}, chaseRange: {monster.chaseRange})");
                monster.stateMachine.ChangeState(new IdleState(monster));
                return;
            }
            if (distance <= monster.attackRange && Time.time >= monster.lastAttackTime + monster.attackCooldown)
            {
                Debug.Log($"[ChaseState {monster.gameObject.name}] Transitioning to ChargeState (distance: {distance}, attackRange: {monster.attackRange})");
                monster.stateMachine.ChangeState(new ChargeState(monster)); // Line 73
                return;
            }
            if (monster.agent != null && monster.agent.isActiveAndEnabled && monster.agent.isOnNavMesh)
            {
                monster.agent.SetDestination(target.position);
                Debug.Log($"[ChaseState {monster.gameObject.name}] Chasing target at {target.position}, distance: {distance}");
            }
        }

        public void OnExit()
        {
            if (monster.animator != null)
            {
                monster.animator.SetBool("isRunning", false);
                monster.animator.SetBool("isCharging", false);
            }
            if (monster.agent != null && monster.agent.isActiveAndEnabled && monster.agent.isOnNavMesh)
            {
                monster.agent.isStopped = true;
                Debug.Log($"[ChaseState {monster.gameObject.name}] OnExit: NavMeshAgent stopped");
            }
            Debug.Log($"[ChaseState {monster.gameObject.name}] Exiting ChaseState");
        }
    }
}
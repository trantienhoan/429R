using UnityEngine;

namespace Enemies
{
    public class IdleState : IState
    {
        private ShadowMonster monster;
        private float idleTimer;
        private Vector3 wanderTarget;
        private bool isWandering;
        private float wanderTimer;

        public IdleState(ShadowMonster monster)
        {
            this.monster = monster;
        }

        public void OnEnter()
        {
            idleTimer = 0f;
            wanderTimer = 0f;
            isWandering = false;
            if (monster.animator != null)
            {
                monster.animator.SetBool("isRunning", false);
                monster.animator.SetBool("isCharging", false);
                monster.animator.SetBool("isGrounded", monster.isGrounded);
                monster.animator.ResetTrigger("Attack");
                monster.animator.Update(0f);
                Debug.Log($"IdleState OnEnter: Set isRunning = false, isCharging = false, isGrounded = {monster.isGrounded}, Animator isRunning = {monster.animator.GetBool("isRunning")}, isCharging = {monster.animator.GetBool("isCharging")}, Current State = {monster.animator.GetCurrentAnimatorStateInfo(0).fullPathHash} ({monster.GetStateName(monster.animator.GetCurrentAnimatorStateInfo(0).fullPathHash)})");
            }
            if (monster.agent != null && monster.agent.isActiveAndEnabled && monster.agent.isOnNavMesh)
            {
                monster.agent.isStopped = true;
                Debug.Log("IdleState OnEnter: NavMeshAgent stopped");
            }
            Debug.Log("Entered IdleState");
        }

        public void Tick()
        {
            idleTimer += Time.deltaTime;
            wanderTimer += Time.deltaTime;

            Transform target = monster.GetClosestTarget();
            float distance = monster.GetDistanceToTarget();
            Debug.Log($"IdleState: Target = {(target != null ? target.name : "None")}, Distance = {distance}, ChaseRange = {monster.chaseRange}, IdleTimer = {idleTimer}, WanderTimer = {wanderTimer}, IsWandering = {isWandering}, NavMeshAgent Active = {(monster.agent != null ? monster.agent.isActiveAndEnabled : false)}, OnNavMesh = {(monster.agent != null ? monster.agent.isOnNavMesh : false)}");

            if (target != null && distance <= monster.chaseRange && !monster.IsBeingHeld && monster.isGrounded)
            {
                if (monster.agent != null && monster.agent.isActiveAndEnabled && monster.agent.isOnNavMesh)
                {
                    monster.stateMachine.ChangeState(new ChaseState(monster));
                    Debug.Log("IdleState: Transitioning to ChaseState");
                }
                else
                {
                    Debug.LogWarning($"IdleState: Cannot transition to ChaseState, NavMeshAgent not ready, Agent Active = {(monster.agent != null ? monster.agent.isActiveAndEnabled : false)}, OnNavMesh = {(monster.agent != null ? monster.agent.isOnNavMesh : false)}");
                }
                return;
            }

            if (!isWandering && wanderTimer >= monster.wanderDelay && monster.isGrounded && monster.agent != null && monster.agent.isActiveAndEnabled && monster.agent.isOnNavMesh)
            {
                wanderTarget = monster.GetRandomWanderPoint();
                monster.agent.SetDestination(wanderTarget);
                monster.agent.isStopped = false;
                monster.agent.speed = 3.5f;
                isWandering = true;
                if (monster.animator != null)
                {
                    monster.animator.SetBool("isRunning", true);
                }
                Debug.Log($"IdleState: Started wandering to {wanderTarget}, Agent Active = {monster.agent.isActiveAndEnabled}, OnNavMesh = {monster.agent.isOnNavMesh}");
            }

            if (isWandering && monster.agent != null && monster.agent.isActiveAndEnabled && monster.agent.isOnNavMesh)
            {
                if (monster.agent.remainingDistance <= monster.agent.stoppingDistance)
                {
                    isWandering = false;
                    wanderTimer = 0f;
                    monster.agent.isStopped = true;
                    if (monster.animator != null)
                    {
                        monster.animator.SetBool("isRunning", false);
                    }
                    Debug.Log("IdleState: Reached wander point, stopping");
                }
            }

            if (idleTimer >= monster.idleTimeBeforeDive && monster.isGrounded)
            {
                if (monster.animator != null)
                {
                    monster.animator.SetTrigger("Dive");
                }
                monster.stateMachine.ChangeState(new DiveState(monster));
                Debug.Log("IdleState: Transitioning to DiveState");
            }
        }

        public void OnExit()
        {
            if (monster.agent != null && monster.agent.isActiveAndEnabled && monster.agent.isOnNavMesh)
            {
                monster.agent.isStopped = true;
                Debug.Log("IdleState OnExit: NavMeshAgent stopped");
            }
            if (monster.animator != null)
            {
                monster.animator.SetBool("isRunning", false);
                monster.animator.SetBool("isCharging", false);
            }
            Debug.Log("Exiting IdleState");
        }
    }
}
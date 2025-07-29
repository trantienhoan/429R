using UnityEngine;

namespace Enemies
{
    public class AttackState : IState
    {
        private ShadowMonster monster;
        private float attackDuration = 0.5f; // Adjust to match Attack animation length
        private float attackTimer;

        public AttackState(ShadowMonster monster)
        {
            this.monster = monster;
        }

        public void OnEnter()
        {
            attackTimer = 0f;
            if (monster.animator != null)
            {
                monster.animator.SetBool("isCharging", false);
                monster.animator.SetBool("isRunning", false);
                monster.animator.SetBool("isGrounded", monster.isGrounded);
                monster.animator.ResetTrigger("Attack"); // Reset to ensure clean trigger
                monster.animator.SetTrigger("Attack");
                monster.animator.Update(0f); // Force Animator update
                Debug.Log($"AttackState OnEnter: Triggered Attack, set isCharging = false, isRunning = false, isGrounded = {monster.isGrounded}, Animator isCharging = {monster.animator.GetBool("isCharging")}, Current State = {monster.animator.GetCurrentAnimatorStateInfo(0).fullPathHash} ({monster.GetStateName(monster.animator.GetCurrentAnimatorStateInfo(0).fullPathHash)})");
            }
            if (monster.agent != null && monster.agent.isActiveAndEnabled && monster.agent.isOnNavMesh)
            {
                monster.agent.isStopped = true;
                Debug.Log("AttackState OnEnter: NavMeshAgent stopped");
            }
            Debug.Log("Entered AttackState");
        }

        public void Tick()
        {
            attackTimer += Time.deltaTime;
            if (monster.animator != null)
            {
                AnimatorStateInfo stateInfo = monster.animator.GetCurrentAnimatorStateInfo(0);
                bool isInAttack = stateInfo.IsName("Attack") || stateInfo.fullPathHash == Animator.StringToHash("Base Layer.Attack");
                Debug.Log($"AttackState Tick: AttackTimer = {attackTimer}, AttackDuration = {attackDuration}, IsInAttack = {isInAttack}, Current State = {stateInfo.fullPathHash} ({monster.GetStateName(stateInfo.fullPathHash)})");
                if (attackTimer >= attackDuration && isInAttack)
                {
                    monster.PerformAttack();
                    monster.stateMachine.ChangeState(new IdleState(monster));
                    Debug.Log("AttackState: Attack complete, triggered PerformAttack, transitioning to IdleState");
                }
                else if (attackTimer >= attackDuration && !isInAttack)
                {
                    Debug.LogWarning("AttackState: Attack animation not playing, forcing PerformAttack and transition to IdleState");
                    monster.PerformAttack();
                    monster.stateMachine.ChangeState(new IdleState(monster));
                }
            }
            else
            {
                Debug.LogWarning("AttackState: Animator is null, forcing PerformAttack and transition to IdleState");
                monster.PerformAttack();
                monster.stateMachine.ChangeState(new IdleState(monster));
            }
        }

        public void OnExit()
        {
            if (monster.animator != null)
            {
                monster.animator.ResetTrigger("Attack");
                monster.animator.SetBool("isRunning", false);
                monster.animator.SetBool("isCharging", false);
                Debug.Log($"AttackState OnExit: Reset Attack trigger, set isRunning = false, isCharging = false");
            }
            if (monster.agent != null && monster.agent.isActiveAndEnabled && monster.agent.isOnNavMesh)
            {
                monster.agent.isStopped = false;
                Debug.Log("AttackState OnExit: NavMeshAgent resumed");
            }
            Debug.Log("Exiting AttackState");
        }
    }
}
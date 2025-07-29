using UnityEngine;

namespace Enemies
{
    public class ChargeState : IState
    {
        private ShadowMonster monster;
        private Transform target;

        public ChargeState(ShadowMonster monster)
        {
            this.monster = monster;
        }

        public void OnEnter()
        {
            target = monster.GetClosestTarget();
            monster.StartCharge();
            if (monster.animator != null)
            {
                monster.animator.SetBool("isCharging", true);
                monster.animator.SetBool("isRunning", false);
                monster.animator.SetBool("isGrounded", monster.isGrounded);
                monster.animator.ResetTrigger("Attack");
                monster.animator.Update(0f); // Force Animator update
                Debug.Log($"ChargeState OnEnter: Set isCharging = true, isRunning = false, isGrounded = {monster.isGrounded}, Animator isCharging = {monster.animator.GetBool("isCharging")}, Current State = {monster.animator.GetCurrentAnimatorStateInfo(0).fullPathHash} ({monster.GetStateName(monster.animator.GetCurrentAnimatorStateInfo(0).fullPathHash)})");
            }
            if (monster.agent != null && monster.agent.isActiveAndEnabled && monster.agent.isOnNavMesh)
            {
                monster.agent.isStopped = true;
                Debug.Log("ChargeState OnEnter: NavMeshAgent stopped");
            }
            Debug.Log($"Entered ChargeState: Target = {(target != null ? target.name : "None")}, ChargeDelay = {monster.chargeDelay}");
        }

        public void Tick()
        {
            if (!monster.isGrounded || monster.IsBeingHeld)
            {
                monster.stateMachine.ChangeState(new IdleState(monster));
                Debug.Log("ChargeState: Exiting to IdleState (not grounded or held)");
                return;
            }

            target = monster.GetClosestTarget();
            float distance = monster.GetDistanceToTarget();
            if (monster.animator != null)
            {
                Debug.Log($"ChargeState Tick: Distance = {distance}, ChargeTimer = {monster.chargeTimer}, ChargeDelay = {monster.chargeDelay}, Target = {(target != null ? target.name : "None")}, Animator isCharging = {monster.animator.GetBool("isCharging")}, Current State = {monster.animator.GetCurrentAnimatorStateInfo(0).fullPathHash} ({monster.GetStateName(monster.animator.GetCurrentAnimatorStateInfo(0).fullPathHash)})");
            }

            if (target == null || distance > monster.attackRange)
            {
                monster.stateMachine.ChangeState(new ChaseState(monster));
                Debug.Log("ChargeState: Exiting to ChaseState (no target or out of attack range)");
                return;
            }

            if (monster.IsChargeComplete())
            {
                monster.animator.SetBool("isCharging", false); // Ensure isCharging is false before Attack
                monster.stateMachine.ChangeState(new AttackState(monster));
                Debug.Log("ChargeState: Charge complete, transitioning to AttackState");
            }
        }

        public void OnExit()
        {
            if (monster.animator != null)
            {
                monster.animator.SetBool("isCharging", false);
                monster.animator.SetBool("isRunning", false);
                Debug.Log($"ChargeState OnExit: Set isCharging = false, isRunning = false, Animator isCharging = {monster.animator.GetBool("isCharging")}");
            }
            if (monster.agent != null && monster.agent.isActiveAndEnabled && monster.agent.isOnNavMesh)
            {
                monster.agent.isStopped = false;
                Debug.Log("ChargeState OnExit: NavMeshAgent resumed");
            }
            Debug.Log("Exiting ChargeState");
        }
    }
}
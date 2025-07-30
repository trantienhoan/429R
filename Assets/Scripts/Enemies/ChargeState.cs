using UnityEngine;

namespace Enemies
{
    public class ChargeState : IState
    {
        private ShadowMonster monster;
        private float stuckTimer;

        public ChargeState(ShadowMonster monster)
        {
            this.monster = monster;
        }

        public void OnEnter()
        {
            stuckTimer = 0f;
            if (monster.animator != null)
            {
                monster.animator.SetBool("isCharging", true);
                monster.animator.SetBool("isRunning", false);
                monster.animator.SetBool("isGrounded", monster.isGrounded);
                monster.animator.ResetTrigger("Attack"); // Reset to avoid conflicts
                monster.animator.Update(0f);
                Debug.Log($"[ChargeState {monster.gameObject.name}] OnEnter: Set isCharging=true, isRunning=false, isGrounded={monster.isGrounded}");
            }
            if (monster.agent != null && monster.agent.isActiveAndEnabled && monster.agent.isOnNavMesh)
            {
                monster.agent.isStopped = true;
                Debug.Log($"[ChargeState {monster.gameObject.name}] OnEnter: NavMeshAgent stopped");
            }
            monster.StartCharge();
            Debug.Log($"[ChargeState {monster.gameObject.name}] Entered ChargeState");
        }

        public void Tick()
        {
            if (!monster.isGrounded || monster.IsBeingHeld)
            {
                Debug.Log($"[ChargeState {monster.gameObject.name}] Transitioning to IdleState (isGrounded={monster.isGrounded}, IsBeingHeld={monster.IsBeingHeld})");
                monster.animator.SetBool("isCharging", false);
                monster.stateMachine.ChangeState(new IdleState(monster));
                return;
            }

            Transform target = monster.GetClosestTarget();
            float distance = monster.GetDistanceToTarget();
            if (target == null || distance > monster.chaseRange)
            {
                Debug.Log($"[ChargeState {monster.gameObject.name}] Transitioning to ChaseState (target: {target?.name}, distance: {distance}, chaseRange: {monster.chaseRange})");
                monster.animator.SetBool("isCharging", false);
                monster.stateMachine.ChangeState(new ChaseState(monster));
                return;
            }

            if (monster.IsChargeComplete())
            {
                if (distance <= monster.attackRange)
                {
                    Debug.Log($"[ChargeState {monster.gameObject.name}] Charge complete, transitioning to AttackState (distance: {distance}, attackRange: {monster.attackRange})");
                    monster.animator.SetBool("isCharging", false);
                    monster.animator.SetTrigger("Attack"); // Set Attack trigger
                    monster.stateMachine.ChangeState(new AttackState(monster));
                }
                else
                {
                    Debug.Log($"[ChargeState {monster.gameObject.name}] Charge complete, transitioning to IdleState (distance: {distance}, attackRange: {monster.attackRange})");
                    monster.animator.SetBool("isCharging", false);
                    monster.stateMachine.ChangeState(new IdleState(monster));
                }
                return;
            }

            stuckTimer += Time.deltaTime;
            if (stuckTimer >= 9f && !monster.isInKamikazeMode)
            {
                Debug.Log($"[ChargeState {monster.gameObject.name}] Stuck for 9s, transitioning to KamikazeState");
                monster.animator.SetBool("isCharging", false);
                monster.stateMachine.ChangeState(new KamikazeState(monster));
            }

            Debug.Log($"[ChargeState {monster.gameObject.name}] Charging, timer: {monster.chargeTimer}/{monster.chargeDelay}, distance: {distance}, stuckTimer: {stuckTimer}");
        }

        public void OnExit()
        {
            if (monster.animator != null)
            {
                monster.animator.SetBool("isCharging", false);
                monster.animator.SetBool("isRunning", false);
                monster.animator.ResetTrigger("Attack"); // Reset on exit
                Debug.Log($"[ChargeState {monster.gameObject.name}] OnExit: Set isCharging=false, isRunning=false, Attack trigger reset");
            }
            if (monster.agent != null && monster.agent.isActiveAndEnabled && monster.agent.isOnNavMesh)
            {
                monster.agent.isStopped = false;
                Debug.Log($"[ChargeState {monster.gameObject.name}] OnExit: NavMeshAgent resumed");
            }
        }
    }
}
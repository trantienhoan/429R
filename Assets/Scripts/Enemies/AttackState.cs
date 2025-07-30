using UnityEngine;
using Core;

namespace Enemies
{
    public class AttackState : IState
    {
        private ShadowMonster monster;
        private float attackDuration = 0.5f;
        private float attackTimer;
        private float kamikazeHealthThreshold = 0.19f;

        public AttackState(ShadowMonster monster)
        {
            this.monster = monster;
        }

        public void OnEnter()
        {
            attackTimer = 0f;
            if (monster.animator != null)
            {
                monster.animator.SetTrigger("Attack");
                monster.animator.SetBool("isAttacking", true);
                monster.animator.SetBool("isCharging", false);
                monster.animator.SetBool("isRunning", false);
                monster.animator.Update(0f);
                Debug.Log($"[AttackState {monster.gameObject.name}] OnEnter: Triggered Attack animation, isAttacking=true");
            }
            monster.PerformAttack();
            Debug.Log($"[AttackState {monster.gameObject.name}] PerformAttack called");
        }

        public void Tick()
        {
            attackTimer += Time.deltaTime;

            HealthComponent healthComponent = monster.GetComponent<HealthComponent>();
            if (healthComponent != null && healthComponent.GetHealthPercentage() <= kamikazeHealthThreshold)
            {
                Debug.Log($"[AttackState {monster.gameObject.name}] Transitioning to KamikazeState due to low health ({healthComponent.GetHealthPercentage()})");
                monster.stateMachine.ChangeState(new KamikazeState(monster));
                return;
            }

            Transform target = monster.GetClosestTarget();
            float distance = monster.GetDistanceToTarget();
            if (target == null || distance > monster.attackRange || !monster.isGrounded || monster.IsBeingHeld)
            {
                Debug.Log($"[AttackState {monster.gameObject.name}] Transitioning to ChaseState (target: {target?.name}, distance: {distance}, attackRange: {monster.attackRange}, isGrounded: {monster.isGrounded}, IsBeingHeld: {monster.IsBeingHeld})");
                monster.stateMachine.ChangeState(new ChaseState(monster));
                return;
            }

            if (monster.animator != null)
            {
                AnimatorStateInfo stateInfo = monster.animator.GetCurrentAnimatorStateInfo(0);
                bool isInAttack = stateInfo.IsName("Attack") || stateInfo.fullPathHash == Animator.StringToHash("Base Layer.Attack");
                Debug.Log($"[AttackState {monster.gameObject.name}] Tick: attackTimer={attackTimer}/{attackDuration}, isInAttack={isInAttack}, normalizedTime={stateInfo.normalizedTime}, distance={distance}");
            }
        }

        public void OnExit()
        {
            if (monster.animator != null)
            {
                monster.animator.ResetTrigger("Attack");
                monster.animator.SetBool("isAttacking", false);
                Debug.Log($"[AttackState {monster.gameObject.name}] OnExit: Reset Attack trigger, isAttacking=false");
            }
        }
    }
}
using UnityEngine;

namespace Enemies
{
    public class AttackState : IState
    {
        private readonly ShadowMonster monster;
        private float attackTimer;
        private readonly float attackDuration = 0.5f;  // Adjust to your Attack anim length

        public AttackState(ShadowMonster monster) { this.monster = monster; }

        public void OnEnter()
        {
            attackTimer = 0f;
            if (monster.animator != null)
            {
                monster.animator.Update(0f);  // Ensure entry
                Debug.Log("Entered AttackState: Current animator state: " + monster.animator.GetCurrentAnimatorStateInfo(0).shortNameHash);
            }
            monster.PerformAttack();  // Damage/SFX here only
        }

        public void Tick()
        {
            attackTimer += Time.deltaTime;
            if (attackTimer >= attackDuration)
            {
                monster.stateMachine.ChangeState(new IdleState(monster));
            }
        }

        public void OnExit()
        {
            if (monster.animator != null)
            {
                monster.animator.ResetTrigger("Attack");
                //monster.animator.ResetTrigger("Attack2");
                //monster.animator.ResetTrigger("Attack3");
            }
        }
    }
}
using UnityEngine;

namespace Enemies
{
    public class AttackState : IState
    {
        private readonly ShadowMonster monster;
        private float attackTimer;
        private readonly float attackDuration = 0.5f;  // Adjust to your anim length

        public AttackState(ShadowMonster monster) { this.monster = monster; }

        public void OnEnter()
        {
            attackTimer = 0f;

            // Activate hitbox right here, before anim
            if (monster.attackHitbox != null)
            {
                monster.attackHitbox.gameObject.SetActive(true);
                monster.attackHitbox.Activate(0.3f); // Only duration
            }

            if (monster.animator != null)
            {
                monster.SafeSet("Attack"); // Trigger anim
                monster.animator.Update(0f); // Force immediate
            }

            monster.PerformAttack(); // Keep for damage
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
                monster.SafeReset("Attack");
            }
            if (monster.attackHitbox != null)
            {
                monster.attackHitbox.gameObject.SetActive(false);
            }
        }
    }
}
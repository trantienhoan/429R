using UnityEngine;

namespace Enemies
{
    public class KamikazeState : IState
    {
        private ShadowMonster monster;

        public KamikazeState(ShadowMonster monster) { this.monster = monster; }

        public void OnEnter()
        {
            if (monster.animator != null)
            {
                monster.animator.SetBool("isRunning", true);
                monster.animator.SetBool("isCharging", false);
                monster.animator.Update(0f);
            }
            if (monster.agent != null && monster.agent.isActiveAndEnabled)
            {
                monster.agent.isStopped = false;
                monster.agent.stoppingDistance = 0f;
            }
            monster.currentTarget = monster.GetClosestTarget();
        }

        public void Tick()
        {
            if (monster.currentTarget == null)
            {
                monster.stateMachine.ChangeState(new IdleState(monster));
                return;
            }
            monster.agent.SetDestination(monster.currentTarget.position);
            float distance = monster.GetDistanceToTarget();
            if (distance <= monster.kamikazeRange)
            {
                if (monster.animator != null) monster.animator.SetTrigger("KamikazeAttack");
                monster.PerformKamikazeAttack();  // Explodes and kills
            }
        }

        public void OnExit()
        {
            if (monster.animator != null) monster.animator.SetBool("isRunning", false);
            if (monster.agent != null)
            {
                monster.agent.isStopped = true;
                monster.agent.speed /= 1.5f;  // Reset speed
            }
        }
    }
}
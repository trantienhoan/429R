using UnityEngine;

namespace Enemies
{
    public class DiveState : IState
    {
        private ShadowMonster monster;

        public DiveState(ShadowMonster monster)
        {
            this.monster = monster;
        }

        public void OnEnter()
        {
            monster.animator.SetTrigger("Dive");
            if (monster.agent != null && monster.agent.isActiveAndEnabled && monster.agent.isOnNavMesh)
            {
                monster.agent.SetDestination(monster.transform.position + monster.transform.forward * 5f);
            }
        }

        public void Tick()
        {
            if (!monster.isGrounded || monster.IsBeingHeld || monster.healthComponent.IsDead())
            {
                monster.stateMachine.ChangeState(new HurtState(monster)); // Transition to HurtState for consistency
                return;
            }

            Transform target = monster.GetClosestTarget();
            if (target != null && monster.GetDistanceToTarget() <= monster.chaseRange)
            {
                monster.stateMachine.ChangeState(new ChaseState(monster));
            }
            else if (!monster.agent.hasPath || monster.agent.remainingDistance < 0.5f)
            {
                monster.stateMachine.ChangeState(new IdleState(monster));
            }
        }

        public void OnExit() { }
    }
}
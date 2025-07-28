using UnityEngine;

namespace Enemies
{
    public class WanderState : IState
    {
        private readonly ShadowMonster monster;
        private Vector3 destination;

        public WanderState(ShadowMonster monster)
        {
            this.monster = monster;
        }

        public void OnEnter()
        {
            monster.animator.Play("Walk"); // Make sure "Walk" exists
            destination = monster.GetRandomWanderPoint();
            monster.agent.SetDestination(destination);
        }

        public void Tick()
        {
            if (!monster.agent.pathPending && monster.agent.remainingDistance < 0.5f)
            {
                monster.SetState(new IdleState(monster));
                return;
            }

            var target = monster.GetClosestTarget();
            if (target != null && Vector3.Distance(monster.transform.position, target.position) < monster.chaseRange)
            {
                monster.SetState(new ChaseState(monster, target));
            }
        }

        public void OnExit() {}
    }
}
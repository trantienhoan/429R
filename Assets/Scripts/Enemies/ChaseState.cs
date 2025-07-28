using UnityEngine;

namespace Enemies
{
    public class ChaseState : IState
    {
        private readonly ShadowMonster monster;
        private readonly Transform target;

        public ChaseState(ShadowMonster monster, Transform target)
        {
            this.monster = monster;
            this.target = target;
        }

        public void OnEnter()
        {
            monster.animator.Play("Walk");
            monster.agent.isStopped = false;
        }

        public void Tick()
        {
            if (target == null)
            {
                monster.SetState(new IdleState(monster));
                return;
            }

            monster.agent.SetDestination(target.position);

            float distance = Vector3.Distance(monster.transform.position, target.position);

            if (distance < monster.attackRange)
            {
                monster.SetState(new ChargeState(monster, target));
            }
        }

        public void OnExit() {}
    }
}
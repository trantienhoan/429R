using UnityEngine;

namespace Enemies
{
    public class IdleState : IState
    {
        private ShadowMonster monster;
        private float idleTimer;

        public IdleState(ShadowMonster monster)
        {
            this.monster = monster;
        }

        public void OnEnter()
        {
            monster.animator.SetBool("isRunning", false);
            idleTimer = 0f;
        }

        public void Tick()
        {
            if (!monster.isGrounded || monster.IsBeingHeld || monster.healthComponent.IsDead()) return;

            idleTimer += Time.deltaTime;
            Transform target = monster.GetClosestTarget();
            float distance = monster.GetDistanceToTarget();

            if (target != null && distance <= monster.chaseRange)
            {
                monster.stateMachine.ChangeState(new ChaseState(monster));
            }
            else if (idleTimer >= monster.idleTimeBeforeDive)
            {
                monster.stateMachine.ChangeState(new DiveState(monster));
            }
        }

        public void OnExit() { }
    }
}
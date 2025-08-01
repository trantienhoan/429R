using UnityEngine;

namespace Enemies
{
    public class HurtState : IState
    {
        private ShadowMonster monster;
        private float hurtDuration = 0.5f;

        public HurtState(ShadowMonster monster)
        {
            this.monster = monster;
        }

        public void OnEnter()
        {
            if (monster.agent != null && monster.agent.isActiveAndEnabled && monster.agent.isOnNavMesh)
            {
                monster.agent.isStopped = true;
            }
            monster.rb.linearVelocity = Vector3.zero;
            monster.rb.angularVelocity = Vector3.zero;
            monster.animator.SetBool("isCharging", false);
            monster.animator.SetBool("isRunning", false);
            monster.animator.SetBool("isGrounded", monster.isGrounded);
            monster.animator.SetTrigger("Hurt");
        }

        public void Tick()
        {
            hurtDuration -= Time.deltaTime;
            if (hurtDuration <= 0f && !monster.isBeingHeld && !monster.healthComponent.IsDead())
            {
                Transform target = monster.GetClosestTarget();
                float distance = monster.GetDistanceToTarget();
                if (target != null && distance <= monster.chaseRange && monster.isGrounded)
                {
                    monster.stateMachine.ChangeState(new ChaseState(monster));
                }
                else
                {
                    monster.stateMachine.ChangeState(new IdleState(monster));
                }
            }
        }

        public void OnExit()
        {
            if (monster.agent != null && monster.agent.isActiveAndEnabled && monster.agent.isOnNavMesh)
            {
                monster.agent.isStopped = false;
            }
            monster.animator.SetBool("isGrounded", monster.isGrounded);
        }
    }
}
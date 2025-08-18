using UnityEngine;

namespace Enemies
{
    public class ChaseState : IState
    {
        private static readonly int IsRunning  = Animator.StringToHash("isRunning");
        private static readonly int IsCharging = Animator.StringToHash("isCharging");
        private static readonly int IsGrounded = Animator.StringToHash("isGrounded");
        private readonly ShadowMonster monster;

        public ChaseState(ShadowMonster monster) { this.monster = monster; }

        public void OnEnter()
        {
            if (!monster.TryMakeAgentReady()) {
                Debug.Log("[Chase] Agent not ready, back to Idle");
                monster.stateMachine.ChangeState(new IdleState(monster));
                return;
            }
            monster.agent.updatePosition = true; // Enable sync
            monster.agent.updateRotation = true;
            monster.agent.isStopped = false;
            monster.currentTarget = monster.GetClosestTarget();
        }

        public void Tick()
        {
            if (monster.currentTarget == null)
            {
                monster.currentTarget = monster.GetClosestTarget();
                if (monster.currentTarget == null)
                {
                    monster.stateMachine.ChangeState(new IdleState(monster));
                    return;
                }
            }
            if (monster.currentTarget != null) {
                if (Vector3.Distance(monster.agent.destination, monster.currentTarget.position) > 0.5f) { // Only if changed significantly
                    monster.agent.SetDestination(monster.currentTarget.position);
                }
                // Existing dist check for Charge
            }
            Debug.Log($"[Chase] Target pos: {monster.currentTarget.position}, Agent dest: {monster.agent.destination}, hasPath: {monster.agent.hasPath}, stopped: {monster.agent.isStopped}, vel: {monster.agent.velocity.magnitude}");

            // Keep destination updated every frame
            monster.agent.SetDestination(monster.currentTarget.position);

            // Quick visibility while testing:
            //Debug.Log($"[Chase] hasPath={monster.agent.hasPath} status={monster.agent.pathStatus} " +
                      //$"stopped={monster.agent.isStopped} vel={monster.agent.desiredVelocity}");

            var dist = monster.GetDistanceToTarget();
            if (dist <= monster.attackRange && Time.time >= monster.nextAttackAllowed)
                monster.stateMachine.ChangeState(new ChargeState(monster));
        }

        public void OnExit()
        {
            if (monster.animator != null) monster.animator.SetBool(IsRunning, false);
            if (monster.AgentReady()) monster.agent.isStopped = true;
        }
    }
}

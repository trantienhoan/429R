using UnityEngine;

namespace Enemies
{
    public class IdleState : IState
    {
        private ShadowMonster monster;
        private float idleTimer;

        public IdleState(ShadowMonster monster) { this.monster = monster; }

        public void OnEnter()
        {
            idleTimer = 0f;
            monster.ResetChargeTimer();
            if (monster.animator != null)
            {
                monster.animator.SetBool("isRunning", false);
                monster.animator.SetBool("isCharging", false);
                monster.animator.SetBool("isGrounded", monster.isGrounded);
                monster.animator.ResetTrigger("Attack");
                monster.animator.ResetTrigger("KamikazeAttack");
                monster.animator.Update(0f);
            }
            if (monster.agent != null && monster.agent.isActiveAndEnabled && monster.agent.isOnNavMesh)
            {
                monster.agent.isStopped = true;
            }
        }

        public void Tick()
        {
            idleTimer += Time.deltaTime;
            float distance = monster.GetDistanceToTarget();
            bool hasTarget = monster.currentTarget != null;
            bool inRange = distance <= monster.chaseRange;
            bool grounded = monster.isGrounded;
            bool notHeld = !monster.IsBeingHeld;
            bool cooldownReady = Time.time >= monster.lastAttackTime + monster.attackCooldown;

            Debug.Log($"[IdleState Tick] HasTarget: {hasTarget}, Distance: {distance} (inRange: {inRange}), Grounded: {grounded}, NotHeld: {notHeld}, CooldownReady: {cooldownReady}, IdleTimer: {idleTimer}");

            if (hasTarget && inRange && grounded && notHeld && cooldownReady)
            {
                Debug.Log("[IdleState] All conditions met, switching to ChaseState");
                monster.stateMachine.ChangeState(new ChaseState(monster));
                return;
            }
            if (idleTimer >= monster.idleTimeBeforeDive && monster.isGrounded)
            {
                Debug.Log("[IdleState] Idle timeout, switching to DiveState");
                monster.stateMachine.ChangeState(new DiveState(monster));
            }
        }

        public void OnExit() { }
    }
}
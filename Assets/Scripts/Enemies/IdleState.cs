using UnityEngine;

namespace Enemies
{
    public class IdleState : IState
    {
        private static readonly int IsRunning = Animator.StringToHash("isRunning");
        private static readonly int IsCharging = Animator.StringToHash("isCharging");
        private static readonly int IsGrounded = Animator.StringToHash("isGrounded");
        private readonly ShadowMonster monster;
        private float idleTimer;

        public IdleState(ShadowMonster monster) { this.monster = monster; }

        public void OnEnter()
        {
            idleTimer = 0f;
            monster.ResetChargeTimer();
            if (monster.animator != null)
            {
                monster.animator.SetBool(IsRunning, false);
                monster.animator.SetBool(IsCharging, false);
                monster.animator.SetBool(IsGrounded, monster.isGrounded);
                monster.animator.ResetTrigger("Attack");
                monster.animator.ResetTrigger("KamikazeAttack");
                monster.animator.Update(0f);
            }
            if (monster.agent != null && monster.agent.isActiveAndEnabled && monster.agent.isOnNavMesh)
            {
                monster.agent.isStopped = true;
            }
            monster.stuckTimer = 0f;
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

            //Debug.Log($"[IdleState Tick] HasTarget: {hasTarget}, Distance: {distance} (inRange: {inRange}), Grounded: {grounded}, NotHeld: {notHeld}, CooldownReady: {cooldownReady}, IdleTimer: {idleTimer}");

            if (hasTarget && inRange && grounded && notHeld && cooldownReady)
            {
                //Debug.Log("[IdleState] Switching to ChaseState");
                monster.stateMachine.ChangeState(new ChaseState(monster));
                return;
            }
            if (idleTimer >= monster.idleTimeBeforeDive && monster.isGrounded)
            {
                //Debug.Log("[IdleState] Idle timeout, switching to DiveState");
                monster.stateMachine.ChangeState(new DiveState(monster));
            }
        }

        public void OnExit() { }
    }
}
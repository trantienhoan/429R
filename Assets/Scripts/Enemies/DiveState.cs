using System;
using UnityEngine;

namespace Enemies
{
    public class DiveState : IState
    {
        private static readonly int IsCharging = Animator.StringToHash("isCharging");
        private static readonly int IsRunning = Animator.StringToHash("isRunning");
        private static readonly int Dive = Animator.StringToHash("Dive");
        private static readonly int IsGrounded = Animator.StringToHash("isGrounded");
        private readonly ShadowMonster monster;
        private float diveTimer;

        public DiveState(ShadowMonster monster) { this.monster = monster; }

        public void OnEnter()
        {
            diveTimer = 0f;
            monster.stuckTimer = 0f;

            // Ensure agent disabled and physics ready
            if (monster.agent != null)
            {
                monster.agent.enabled = false;
            }
            if (monster.rb != null)
            {
                monster.rb.isKinematic = false;
                Vector3 diveDirection = monster.transform.forward;  // Or towards target if available
                monster.rb.AddForce(diveDirection * monster.diveSpeed * monster.diveForceMultiplier, ForceMode.VelocityChange);
                Debug.Log("[DiveState] Diving forward with force: " + (monster.diveSpeed * monster.diveForceMultiplier) + " in direction: " + diveDirection);
            }

            // Animator - Force trigger and update
            if (monster.animator != null)
            {
                monster.animator.SetBool(IsRunning, false);
                monster.animator.SetBool(IsCharging, false);
                monster.animator.SetTrigger(Dive);
                monster.animator.Update(0f);  // Immediate update to ensure trigger fires
            }

            // Start dive attack coroutine
            monster.StartCoroutine(monster.PerformDiveAttackCoroutine());
        }

        public void Tick()
        {
            diveTimer += Time.deltaTime;

            // Exit if timeout or low velocity (settled/hit)
            if (diveTimer >= monster.diveTimeout || (monster.rb != null && monster.rb.linearVelocity.magnitude < 0.5f))
            {
                monster.stateMachine.ChangeState(new IdleState(monster));  // Or Chase if target near
            }
        }

        public void OnExit()
        {
            // Stop movement
            if (monster.rb != null)
            {
                monster.rb.linearVelocity = Vector3.zero;
                monster.rb.angularVelocity = Vector3.zero;
            }

            // Re-enable and sync agent
            monster.EnsureAgentOnNavMesh();
            if (monster.agent != null)
            {
                monster.agent.enabled = true;
                monster.agent.isStopped = true;
            }

            // Reset animator
            if (monster.animator != null)
            {
                monster.animator.ResetTrigger("Dive");
                monster.animator.SetBool(IsGrounded, monster.IsGrounded());
                monster.animator.Update(0f);
            }

            // Ensure hitbox is disabled (in case coroutine didn't finish)
            if (monster.attackHitbox != null)
            {
                monster.attackHitbox.gameObject.SetActive(false);
            }
        }
    }
}
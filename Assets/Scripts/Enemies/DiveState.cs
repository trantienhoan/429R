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
            monster.stuckTimer = 0f;  // Reset stuck

            // Disable agent for physics dive
            if (monster.agent != null && monster.agent.isActiveAndEnabled)
            {
                monster.agent.enabled = false;
            }

            // Apply forward dive force (use Force for acceleration)
            if (monster.rb != null)
            {
                monster.rb.isKinematic = false;
                Vector3 diveDirection = monster.transform.forward;
                monster.rb.AddForce(diveDirection * monster.diveSpeed * monster.diveForceMultiplier, ForceMode.VelocityChange);
                Debug.Log("[DiveState] Diving forward with force: " + (monster.diveSpeed * monster.diveForceMultiplier));
            }

            // Animator
            if (monster.animator != null)
            {
                monster.animator.SetBool(IsRunning, false);
                monster.animator.SetBool(IsCharging, false);
                monster.animator.SetTrigger(Dive);
                monster.animator.Update(0f);
            }
        }

        [Obsolete("Obsolete")]
        public void Tick()
        {
            diveTimer += Time.deltaTime;

            // Exit if timeout or stopped (hit something, velocity low)
            if (diveTimer >= monster.diveTimeout || (monster.rb != null && monster.rb.velocity.magnitude < 0.5f))
            {
                monster.stateMachine.ChangeState(new IdleState(monster));  // Or new ChaseState if target still valid
            }
        }

        [Obsolete("Obsolete")]
        public void OnExit()
        {
            // Stop movement
            if (monster.rb != null)
            {
                monster.rb.velocity = Vector3.zero;
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
        }
    }
}
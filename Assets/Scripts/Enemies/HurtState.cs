using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Enemies
{
    [Serializable]
    public class HurtState : IState
    {
        private static readonly int IsCharging = Animator.StringToHash("isCharging");
        private static readonly int IsRunning = Animator.StringToHash("isRunning");
        private static readonly int IsGrounded = Animator.StringToHash("isGrounded");
        private static readonly int Hurt = Animator.StringToHash("Hurt");
        private readonly ShadowMonster monster;
        [SerializeField] private float hurtDuration = 0.5f; // Tune in Inspector
        [SerializeField] private float knockbackForce = 5f; // Tune for push strength

        private float remainingDuration;

        public HurtState(ShadowMonster monster)
        {
            this.monster = monster;
        }

        public void OnEnter()
        {
            remainingDuration = hurtDuration;

            // Halt navigation and apply knockback
            if (monster.agent != null && monster.agent.isActiveAndEnabled && monster.agent.isOnNavMesh)
            {
                monster.agent.enabled = false; // Temporarily disable for physics freedom
            }
            if (monster.rb != null)
            {
                // Apply knockback: Backward or random direction
                Vector3 knockDir = -monster.transform.forward + Random.insideUnitSphere * 0.3f; // Add slight randomness
                knockDir.y = 0f; // Keep horizontal
                monster.rb.AddForce(knockDir.normalized * knockbackForce, ForceMode.VelocityChange);
            }

            // Animator updates
            if (monster.animator != null)
            {
                monster.animator.SetBool(IsCharging, false);
                monster.animator.SetBool(IsRunning, false);
                monster.animator.SetBool(IsGrounded, monster.isGrounded);
                monster.SafeSet("Hurt"); // Use safe method to avoid warnings if trigger missing
            }

            // Optional: Play hit SFX or particles here for polish
            // e.g., if (monster.hitSfx) monster.audioSource.PlayOneShot(monster.hitSfx);
        }

        public void Tick()
        {
            remainingDuration -= Time.deltaTime;
            if (remainingDuration <= 0f && !monster.isBeingHeld && !monster.healthComponent.IsDead())
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
            // Re-enable agent and soft-reset movement
            if (monster.agent != null)
            {
                monster.TryResumeAgent(); // Handles enabling and stopping false
            }
            if (monster.rb != null)
            {
                monster.rb.linearVelocity *= 0.5f; // Dampen any residual velocity softly
            }
            if (monster.animator != null)
            {
                monster.animator.SetBool(IsGrounded, monster.isGrounded);
                monster.SafeReset("Hurt"); // Clean up trigger
            }
        }
    }
}
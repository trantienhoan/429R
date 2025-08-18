using System;
using UnityEngine;

namespace Enemies
{
    public class DiveState : IState
    {
        private static readonly int IsCharging = Animator.StringToHash("isCharging");
        private static readonly int IsRunning  = Animator.StringToHash("isRunning");
        private static readonly int DiveTrig   = Animator.StringToHash("Dive");
        private static readonly int IsGrounded = Animator.StringToHash("isGrounded");

        private readonly ShadowMonster monster;
        private float diveTimer;

        public DiveState(ShadowMonster monster) { this.monster = monster; }

        public void OnEnter()
        {
            diveTimer = 0f;
            monster.stuckTimer = 0f;

            // Disable agent during a physics-driven dive to avoid fighting the RB
            if (monster.agent != null && monster.agent.enabled)
                monster.agent.enabled = false;

            // Compute a robust, horizontal dive direction
            Vector3 dir = Vector3.zero;

            // 1) Use agent desired velocity if available (sometimes zero right after state changes)
            if (monster.agent != null && monster.agent.desiredVelocity.sqrMagnitude > 0.01f)
                dir = monster.agent.desiredVelocity;

            // 2) Otherwise, toward current target
            if (dir.sqrMagnitude < 0.01f && monster.currentTarget != null)
                dir = (monster.currentTarget.position - monster.transform.position);

            // 3) Last fallback: forward
            if (dir.sqrMagnitude < 0.01f)
                dir = monster.transform.forward;

            // Keep it purely horizontal
            dir.y = 0f;
            dir.Normalize();

            // Physics launch
            if (monster.rb != null)
            {
                monster.rb.isKinematic = false;
                // Let gravity act normally during dive (ShadowMonster.Update switches gravity off again for AI)
                monster.rb.useGravity = true;

                float diveForce = monster.diveSpeed * monster.diveForceMultiplier;
                monster.rb.AddForce(dir * diveForce, ForceMode.VelocityChange);
                Debug.Log($"[DiveState] Diving forward with force: {diveForce} in direction: {dir}");
            }

            // Animator
            if (monster.animator != null)
            {
                monster.animator.SetBool(IsRunning, false);
                monster.animator.SetBool(IsCharging, false);
                monster.SafeReset("Attack"); // safe in case Attack trigger exists
                monster.animator.ResetTrigger(DiveTrig); // ensure clean
                monster.animator.SetTrigger(DiveTrig);
                monster.animator.SetBool(IsGrounded, monster.IsGrounded());
                monster.animator.Update(0f);
            }

            // Small initial sweep damage window
            monster.StartCoroutine(monster.PerformDiveAttackCoroutine());
        }

        public void Tick()
        {
            diveTimer += Time.deltaTime;

            // Exit if timeout or velocity too low (hit/settled)
            if (diveTimer >= monster.diveTimeout ||
                (monster.rb != null && monster.rb.linearVelocity.magnitude < 0.5f))
            {
                monster.stateMachine.ChangeState(new IdleState(monster)); // or ChaseState if you prefer
            }
        }

        public void OnExit()
        {
            // Stop physics motion and return control to the agent
            if (monster.rb != null)
            {
                monster.rb.linearVelocity = Vector3.zero;
                monster.rb.angularVelocity = Vector3.zero;
                monster.rb.isKinematic = true;            // <-- important: give control back to NavMesh
                monster.rb.detectCollisions = true;
            }

            // Re-enable & snap agent to the mesh safely
            monster.EnsureAgentOnNavMesh();               // your helper – but prefer a warp-style helper below
            if (monster.agent != null)
            {
                monster.agent.enabled = true;
                // Do NOT leave it stopped forever; Chase will unstop it.
                monster.agent.isStopped = true;           // temporary, Chase will set false
                monster.agent.nextPosition = monster.transform.position;
                monster.agent.updatePosition = true;
                monster.agent.updateRotation = true;
            }

            if (monster.animator != null)
            {
                monster.animator.ResetTrigger("Dive");
                monster.animator.SetBool(IsGrounded, monster.IsGrounded());
                monster.animator.Update(0f);
            }

            if (monster.attackHitbox != null)
                monster.attackHitbox.gameObject.SetActive(false);
        }
    }
}

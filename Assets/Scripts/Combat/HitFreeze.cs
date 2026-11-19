using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Combat
{
    /// <summary>
    /// The hit freeze (user, 2026-11-19: "controlling and feeling good before anything else"): when the player's hit
    /// hurts an enemy, the enemy stops dead for a few hundredths of a second, its animations and its walking (never the
    /// player's view, which must not stop in VR), so the hit lands with weight. Enemy Hit Damage adds it when needed and
    /// calls Freeze; Hit Settings' Hit Freeze sets how long. A knock-back waits until the freeze is over (Knockback.Hold).
    /// It runs late, after the brains: a brain sets its animator's speed every frame (its leg speed), so each frame of a
    /// freeze it notes the speed the brain wanted and then holds 0; when the freeze ends that speed comes back.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    public class HitFreeze : MonoBehaviour
    {
        private readonly List<Animator> animators = new();
        private readonly List<float> wanted = new();
        private NavMeshAgent agent;
        private bool agentSearched;
        private bool frozen;
        private float startedAt;
        private float until;
        private float restUntil = -1f;

        /// <summary>When the freeze running now ends (Time.time), or -1 when it isn't frozen.</summary>
        public float FrozenUntil => frozen ? until : -1f;

        /// <summary>
        /// Freezes it for this many seconds. While a freeze runs, a longer one stretches it (counted from when it
        /// started, so fast hits can't chain it on and on); Always (a killing blow) counts from now. Within Rest seconds
        /// after a freeze it isn't frozen again, unless Always. True if it's frozen now.
        /// </summary>
        public bool Freeze(float seconds, float rest, bool always)
        {
            if (seconds <= 0f || !gameObject.activeInHierarchy) return false;
            float now = Time.time;
            if (!frozen)
            {
                if (!always && now < restUntil) return false;
                animators.Clear();
                wanted.Clear();
                GetComponentsInChildren(animators);
                foreach (var animator in animators) wanted.Add(animator.speed);
                if (!agentSearched)
                {
                    agentSearched = true;
                    TryGetComponent(out agent);
                }
                frozen = true;
                startedAt = now;
                until = now + seconds;
            }
            else
            {
                until = Mathf.Max(until, (always ? now : startedAt) + seconds);
            }
            restUntil = until + Mathf.Max(0f, rest);
            enabled = true;
            Hold();
            return true;
        }

        private void Update()
        {
            if (!frozen)
            {
                enabled = false;
                return;
            }
            if (Time.time >= until) Release();
            else Hold();
        }

        // Each frame: a speed a brain set since the last frame is the one to give back; then it's held at 0. Its walking
        // is held still too (the agent picks up speed again from nothing afterwards).
        private void Hold()
        {
            for (int i = 0; i < animators.Count; i++)
            {
                var animator = animators[i];
                if (animator == null) continue;
                if (animator.speed != 0f) wanted[i] = animator.speed;
                animator.speed = 0f;
            }
            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.velocity = Vector3.zero;
        }

        private void Release()
        {
            frozen = false;
            for (int i = 0; i < animators.Count; i++)
            {
                var animator = animators[i];
                // (Left alone if something else set a speed of its own meanwhile.)
                if (animator != null && animator.speed == 0f) animator.speed = wanted[i];
            }
            enabled = false;
        }

        // Switched off or put back in a pool mid-freeze: it never stays frozen.
        private void OnDisable()
        {
            if (frozen) Release();
        }
    }
}

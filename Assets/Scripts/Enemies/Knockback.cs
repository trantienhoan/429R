using UnityEngine;
using UnityEngine.AI;

namespace Game.Enemies
{
    /// <summary>
    /// Slides an enemy back after a hit: fast at first and slowing to a stop, with a small hop, then it stays put a
    /// moment before walking again. NavMesh agents can't be pushed by physics, so this moves the agent itself along
    /// the NavMesh, which keeps it out of walls and from sliding off edges. PlayMaker's "Knock Back" action adds it
    /// when needed.
    /// </summary>
    [DisallowMultipleComponent]
    public class Knockback : MonoBehaviour
    {
        private NavMeshAgent agent;
        private Vector3 direction;
        private float distance;
        private float duration;
        private float hopHeight;
        private float stunTime;
        private float elapsed;
        private float moved;
        private float restingBaseOffset;
        private bool pushing;

        /// <summary>True while sliding back or standing stunned afterwards.</summary>
        public bool IsKnockedBack => pushing;

        /// <summary>
        /// Knocks the enemy away from <paramref name="awayFrom"/>. A new push while one is running starts fresh from
        /// where the enemy is now.
        /// </summary>
        public void Push(Vector3 awayFrom, float distance, float duration, float hopHeight, float stunTime)
        {
            if (agent == null) agent = GetComponent<NavMeshAgent>();

            var away = transform.position - awayFrom;
            away.y = 0f;
            // Hit from straight above: knock it back the way it's facing from.
            direction = away.sqrMagnitude > 0.0001f ? away.normalized : -FlatForward();

            // The resting height is the one from before the first hit, not from the middle of a hop.
            if (!pushing && agent != null) restingBaseOffset = agent.baseOffset;

            this.distance = Mathf.Max(0f, distance);
            this.duration = Mathf.Max(0.01f, duration);
            this.hopHeight = Mathf.Max(0f, hopHeight);
            this.stunTime = Mathf.Max(0f, stunTime);
            elapsed = 0f;
            moved = 0f;
            pushing = true;
            enabled = true;
        }

        private void Update()
        {
            if (!pushing)
            {
                enabled = false;
                return;
            }

            // Game time, so a push holds while the game is paused.
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // Ease out: fast at first, slowing to a stop.
            float travelled = distance * (1f - (1f - t) * (1f - t));
            var step = direction * (travelled - moved);
            moved = travelled;

            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                // Hold the agent's own walking still, so it doesn't fight the push or walk off during the stun.
                agent.velocity = Vector3.zero;
                agent.Move(step);
                agent.baseOffset = restingBaseOffset + HopOffset(t);
            }
            else
            {
                transform.position += step;
            }

            if (elapsed >= duration + stunTime) Stop();
        }

        private void OnDisable()
        {
            if (pushing) Stop();
        }

        private void Stop()
        {
            if (pushing && agent != null) agent.baseOffset = restingBaseOffset;
            pushing = false;
            enabled = false;
        }

        // Base Offset is in the enemy's own (scaled) units, so the hop is divided by its scale to stay in metres.
        private float HopOffset(float t)
        {
            return hopHeight * Mathf.Sin(t * Mathf.PI) / Mathf.Max(0.01f, transform.lossyScale.y);
        }

        private Vector3 FlatForward()
        {
            var forward = transform.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        }
    }
}

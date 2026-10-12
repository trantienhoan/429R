using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game.Combat
{
    /// <summary>
    /// A heavy block (the metal Jenga blocks) that hurts the player when it falls on them, like a Jenga tower coming
    /// down. Touching, pushing or leaning on it does nothing: it only counts while the block is falling at Light Hit
    /// Speed or faster when it reaches the player's body (a standing cylinder from their feet to their head). Faster
    /// falls hit harder. Hits from all blocks share one cooldown, so a whole tower landing at once doesn't stack up.
    /// A block the player is holding never counts. Goes on the block, next to its Rigidbody.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class FallingBlockDamage : MonoBehaviour
    {
        [Tooltip("Falling at least this fast (m/s, downwards) when it reaches the player gives a light hit (about a " +
                 "30 cm drop).")]
        [Min(0.1f)]
        [SerializeField] private float lightHitSpeed = 2.5f;
        [Tooltip("Sent to the player's PlayerHealth FSM for a light hit (Damage = 6-9).")]
        [SerializeField] private string lightHitEvent = "Damage";
        [Tooltip("Falling at least this fast gives a heavy hit (about a 1.3 m drop).")]
        [Min(0.1f)]
        [SerializeField] private float heavyHitSpeed = 5f;
        [Tooltip("Sent for a heavy hit (DamageHeavy = 40-50).")]
        [SerializeField] private string heavyHitEvent = "DamageHeavy";
        [SerializeField] private string playerHealthFsm = "PlayerHealth";
        [Tooltip("How far round the player's head-to-feet line the block counts as touching them, in metres.")]
        [Min(0.05f)]
        [SerializeField] private float bodyRadius = 0.3f;
        [Tooltip("After any block hurts the player, no block can hurt them again for this many seconds.")]
        [Min(0f)]
        [SerializeField] private float sharedCooldown = 1f;

        private static float nextHitAt;

        private readonly List<PlayMakerFSM> fsmBuffer = new();
        private Rigidbody body;
        private Collider[] shapes;
        private XRBaseInteractable grab;
        private XROrigin player;
        private float lastFall;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => nextHitAt = 0f;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            shapes = GetComponentsInChildren<Collider>();
            TryGetComponent(out grab);
        }

        private void FixedUpdate()
        {
            if (body.isKinematic || body.IsSleeping()) { lastFall = 0f; return; }
            // The block may already have bounced off the player this step, so last step's speed counts too.
            float falling = -body.linearVelocity.y;
            float fall = Mathf.Max(falling, lastFall);
            lastFall = falling;
            if (fall < lightHitSpeed || Time.time < nextHitAt) return;
            if (grab != null && grab.enabled && grab.isSelected) return;
            if (!FindPlayer() || !TouchesPlayer()) return;
            nextHitAt = Time.time + sharedCooldown;
            DamageVignette.HitFrom(transform.position);
            HurtPlayer(fall >= heavyHitSpeed ? heavyHitEvent : lightHitEvent);
        }

        // Any of the block's solid shapes within Body Radius of the player's head-to-feet line.
        private bool TouchesPlayer()
        {
            var head = player.Camera.transform.position;
            float feet = player.transform.position.y + bodyRadius;
            float r2 = bodyRadius * bodyRadius;
            for (int i = 0; i <= 4; i++)
            {
                var point = new Vector3(head.x, Mathf.Lerp(feet, head.y, i / 4f), head.z);
                foreach (var shape in shapes)
                {
                    if (shape == null || !shape.enabled || shape.isTrigger) continue;
                    if ((shape.ClosestPoint(point) - point).sqrMagnitude <= r2) return true;
                }
            }
            return false;
        }

        private void HurtPlayer(string eventName)
        {
            player.gameObject.GetComponents(fsmBuffer);
            foreach (var fsm in fsmBuffer)
            {
                if (fsm.FsmName == playerHealthFsm) fsm.SendEvent(eventName);
            }
        }

        private bool FindPlayer()
        {
            if (player != null && player.Camera != null) return true;
            player = FindAnyObjectByType<XROrigin>();
            return player != null && player.Camera != null;
        }
    }
}

using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game.Enemies
{
    /// <summary>
    /// Makes a big enemy plough through the room: while it moves (charging at the player, or being knocked back), loose
    /// objects in its way are shoved aside and ahead of it, like a bulldozer. The push zone grows with the enemy, so a
    /// boss that grows bigger clears a wider path. Only loose physics objects move: walls, furniture that isn't loose,
    /// other enemies, things in the player's hands and the player stay put. Goes on the enemy's root, next to its NavMesh
    /// Agent (TheSpider, TheSpider 1, TheCockroach, TheCockroach 1 and pumpkin_boss_Z have it).
    /// On the pumpkin boss (2026-11-11) the zone follows its model (its root stays size 1 while the model grows) and, while
    /// its giant pumpkin is out, reaches the pumpkin's edge plus Radius; it counts the boss's own moves (its charge) as well
    /// as its agent's, pushes nothing while it's in the air (a hop, its bounce), and leaves what the boss crushes to it.
    /// </summary>
    [DisallowMultipleComponent]
    public class Bulldozer : MonoBehaviour
    {
        [Header("Push zone (at the enemy's normal size; it grows with the enemy)")]
        [Tooltip("How far around its body things get pushed, in metres.")]
        [Min(0.05f)]
        [SerializeField] private float radius = 0.4f;
        [Tooltip("How high the middle of the push zone is above its feet, in metres.")]
        [SerializeField] private float height = 0.25f;
        [Tooltip("How far ahead of its middle the push zone reaches, in metres, so it clears the way before it gets there.")]
        [SerializeField] private float reachAhead = 0.15f;

        [Header("Push")]
        [Tooltip("How fast things are shoved away, in metres per second; the same for light and heavy things, except the " +
                 "very heaviest (Mass 1,000,000 or more, e.g. the Stone_Cube_Dice), which stay put.")]
        [Min(0f)]
        [SerializeField] private float pushSpeed = 4f;
        [Tooltip("A fast enemy pushes harder (1.5 times its speed), but never faster than this, in metres per second, so " +
                 "small things aren't shot through walls.")]
        [Min(0f)]
        [SerializeField] private float maxPushSpeed = 8f;
        [Tooltip("How much of the push goes upwards, so things tumble instead of sliding (0 = none).")]
        [Range(0f, 1f)]
        [SerializeField] private float lift = 0.3f;
        [Tooltip("How much things are carried along ahead of it, compared with being thrown to the sides (0 = only sideways).")]
        [Range(0f, 2f)]
        [SerializeField] private float forwardPush = 0.6f;
        [Tooltip("It only pushes while it moves at least this fast, in metres per second.")]
        [Min(0f)]
        [SerializeField] private float minMoveSpeed = 0.2f;

        private static readonly Collider[] hits = new Collider[64];
        private static readonly HashSet<Rigidbody> pushed = new();

        private NavMeshAgent agent;
        private Rigidbody ownBody;
        private CandyVacuum vacuum;
        private PumpkinBossBrain boss;
        private Vector3 lastPosition;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            ownBody = GetComponent<Rigidbody>();
            vacuum = GetComponent<CandyVacuum>();
            boss = GetComponent<PumpkinBossBrain>();
            lastPosition = transform.position;
        }

        private void FixedUpdate()
        {
            var position = transform.position;
            // The agent knows how fast it's walking; knock-backs and other moves show up as a change of position.
            var velocity = agent != null && agent.enabled ? agent.velocity : (position - lastPosition) / Time.fixedDeltaTime;
            // The pumpkin boss also moves itself (its charge): how far it really went counts too. In the air it pushes nothing.
            if (boss != null)
            {
                var moved = (position - lastPosition) / Time.fixedDeltaTime;
                if (moved.sqrMagnitude > velocity.sqrMagnitude) velocity = moved;
                if (boss.Airborne) velocity = Vector3.zero;
            }
            lastPosition = position;
            velocity.y = 0f;
            if (velocity.sqrMagnitude < minMoveSpeed * minMoveSpeed) return;

            var moving = velocity.normalized;
            // The pumpkin boss: its model's size, and its giant pumpkin's reach while that's out.
            float size = boss != null ? boss.ModelSize : transform.lossyScale.x;
            float pumpkin = boss != null ? boss.BodyRadius : 0f;
            var center = position + Vector3.up * (pumpkin > 0f ? pumpkin * 0.6f : height * size) + moving * (reachAhead * size);
            float reach = pumpkin > 0f ? pumpkin + radius : radius * size;

            pushed.Clear();
            int count = Physics.OverlapSphereNonAlloc(center, reach, hits, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var body = hits[i].attachedRigidbody;
                if (body == null || body == ownBody || body.isKinematic || !pushed.Add(body)) continue;
                if (IsHeldOrPlayer(body)) continue;
                // The very heaviest things (the stone blocks that close a path) stay put.
                if (Game.Combat.PushWeight.IsImmovable(body)) continue;
                // Candy it's about to eat isn't pushed away.
                if (vacuum != null && vacuum.IsCandy(body)) continue;
                // What the pumpkin boss crushes is left for that (pushed away first, it would miss it).
                if (boss != null && boss.Crushes(hits[i])) continue;

                Push(body, center, moving, Mathf.Clamp(velocity.magnitude * 1.5f, pushSpeed, Mathf.Max(pushSpeed, maxPushSpeed)));
            }
        }

        // Shoves the body out of the way: off to the side it's already on, a bit ahead, a bit up. Things already flying
        // away fast enough are left alone, so they're cleared, not launched.
        private void Push(Rigidbody body, Vector3 center, Vector3 moving, float speed)
        {
            var away = body.worldCenterOfMass - center;
            away.y = 0f;
            // Things behind it aren't in its way.
            if (Vector3.Dot(away, moving) < -0.25f * away.magnitude) return;

            var sideways = away - moving * Vector3.Dot(away, moving);
            var direction = (sideways.sqrMagnitude > 0.0001f ? sideways.normalized : Vector3.zero) + moving * forwardPush;
            if (direction.sqrMagnitude < 0.0001f) direction = moving;
            direction = (direction.normalized + Vector3.up * lift).normalized;

            float already = Vector3.Dot(body.linearVelocity, direction);
            if (already >= speed) return;
            body.AddForce(direction * (speed - already), ForceMode.VelocityChange);
        }

        private static bool IsHeldOrPlayer(Rigidbody body)
        {
            if (body.TryGetComponent<XRGrabInteractable>(out var grab) && grab.isSelected) return true;
            return body.GetComponentInParent<XROrigin>() != null;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            float size = transform.lossyScale.x;
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * (height * size) + transform.forward * (reachAhead * size), radius * size);
        }
#endif
    }
}

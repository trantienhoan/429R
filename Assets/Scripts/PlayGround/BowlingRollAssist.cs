using Game.Combat;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game.PlayGround
{
    /// <summary>
    /// Makes the bowling ball easy to bowl. Let go of it low, near the floor, and it rolls: flat along the floor the way
    /// it was swung, at least fast enough to reach the pins, already turning like a rolling ball so it doesn't skid,
    /// and with some of the wrist's twist kept as hook, which curves it. Let go of it higher up and it's an ordinary
    /// throw. Runs once, the moment the ball leaves the hand. Goes on the ball (pumpkin_bowling), next to its XR Grab
    /// Interactable.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)] // after XRI's interaction manager, which throws the ball in its LateUpdate
    [RequireComponent(typeof(XRGrabInteractable))]
    public class BowlingRollAssist : MonoBehaviour, IThrowAssistOptOut
    {
        [Tooltip("Let go with the bottom of the ball at most this high above the floor, in metres, and it's bowled. " +
                 "Higher up it's an ordinary throw.")]
        [Min(0f)]
        [SerializeField] private float maxReleaseHeight = 0.6f;
        [Tooltip("Let go slower than this, in metres per second, and it's just put down.")]
        [Min(0f)]
        [SerializeField] private float dropSpeed = 1f;
        [Tooltip("Slowest bowl, in metres per second: softer bowls are sped up to this so the ball reaches the pins.")]
        [Min(0f)]
        [SerializeField] private float minRollSpeed = 3f;
        [Tooltip("Fastest bowl, in metres per second.")]
        [Min(0f)]
        [SerializeField] private float maxRollSpeed = 10f;

        [Header("Aim")]
        [Tooltip("Optional: the head pin (or the middle of the pins). Bowls heading roughly that way are steered a " +
                 "little toward it.")]
        [SerializeField] private Transform aimAt;
        [Tooltip("How much it steers toward Aim At: 0 = not at all, 1 = straight at it.")]
        [Range(0f, 1f)]
        [SerializeField] private float straighten = 0.3f;
        [Tooltip("Only bowls heading within this many degrees of Aim At are steered.")]
        [Range(0f, 90f)]
        [SerializeField] private float aimAngle = 25f;

        [Header("Hook")]
        [Tooltip("How much of the wrist's twist as you let go is kept as hook, which curves the ball: 0 = it always " +
                 "rolls straight, 1 = all of it.")]
        [Range(0f, 1f)]
        [SerializeField] private float hook = 0.5f;
        [Tooltip("Most hook spin, in turns per second.")]
        [Min(0f)]
        [SerializeField] private float maxHookSpin = 1.5f;

        [Tooltip("Layers that count as floor.")]
        [SerializeField] private LayerMask floorLayers = Physics.DefaultRaycastLayers;

        private static readonly RaycastHit[] Hits = new RaycastHit[8];

        private XRGrabInteractable grab;
        private Rigidbody body;
        private float radius;
        private bool bowled;
        private Vector3 floorNormal = Vector3.up;

        // A bowl gets no Throw Assist: this rolls it instead.
        public bool OptOutOfThrowAssist => bowled;

        private void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            body = GetComponent<Rigidbody>();
            var scale = transform.lossyScale;
            if (TryGetComponent(out SphereCollider sphere))
                radius = sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            else if (GetComponentInChildren<Collider>() is { } other)
                radius = other.bounds.extents.y;
            radius = Mathf.Max(radius, 0.01f);
        }

        private void OnEnable()
        {
            grab.selectExited.AddListener(OnReleased);
        }

        private void OnDisable()
        {
            grab.selectExited.RemoveListener(OnReleased);
            bowled = false;
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            bowled = !grab.isSelected && IsNearFloor();
        }

        // XRI has thrown the ball by now; turn that throw into a roll.
        private void LateUpdate()
        {
            if (!bowled) return;
            bowled = false;
            if (grab.isSelected || body.isKinematic) return;

            var along = Vector3.ProjectOnPlane(body.linearVelocity, floorNormal);
            float speed = along.magnitude;
            if (speed < dropSpeed || Vector3.Dot(body.linearVelocity, floorNormal) > speed) return; // put down, or lobbed up
            var direction = along / speed;
            if (aimAt != null)
            {
                var toPins = Vector3.ProjectOnPlane(aimAt.position - body.position, floorNormal);
                if (toPins.sqrMagnitude > 0.01f && Vector3.Angle(direction, toPins) <= aimAngle)
                    direction = Vector3.Slerp(direction, toPins.normalized, straighten);
            }
            speed = Mathf.Clamp(speed, minRollSpeed, Mathf.Max(minRollSpeed, maxRollSpeed));

            float mostHook = maxHookSpin * 2f * Mathf.PI;
            float twist = Mathf.Clamp(Vector3.Dot(body.angularVelocity, direction) * hook, -mostHook, mostHook);
            var spin = Vector3.Cross(floorNormal, direction) * (speed / radius) + direction * twist;
            if (body.maxAngularVelocity < spin.magnitude) body.maxAngularVelocity = spin.magnitude;
            body.linearVelocity = direction * speed;
            body.angularVelocity = spin;
        }

        private bool IsNearFloor()
        {
            int count = Physics.RaycastNonAlloc(body.worldCenterOfMass, Vector3.down, Hits, radius + maxReleaseHeight,
                floorLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var hit = Hits[i];
                if (hit.rigidbody == body || hit.normal.y < 0.7f || hit.distance >= nearest) continue;
                if (hit.collider.GetComponentInParent<XROrigin>() != null) continue; // the player's own body
                nearest = hit.distance;
                floorNormal = hit.normal;
            }
            return nearest < float.MaxValue;
        }
    }
}

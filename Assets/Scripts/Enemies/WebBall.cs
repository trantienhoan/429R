using Game.Combat;
using Game.Locomotion;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using FsmVariables = HutongGames.PlayMaker.FsmVariables;

namespace Game.Enemies
{
    /// <summary>
    /// A spider's web shot (Spider Brain launches it). It flies with gravity; reaching the player's body it hurts them
    /// (Player Event, sent to their Damage FSM) and slows their walking for Slow Seconds. A weapon held in its way, or a
    /// fist, swats it. Anything else just splats it. It has no collider of its own and finds what it hits by sweeping
    /// along its flight, so it never knocks things over, hurts other enemies, or sets off other objects' trigger FSMs.
    /// Goes on the web ball prefab's root, next to its Rigidbody.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class WebBall : MonoBehaviour
    {
        [Tooltip("Sent to the player's Damage FSM when it hits them: Damage (1-3 health), DamageHeavy (6-12), ...")]
        [SerializeField] private string playerEvent = "Damage";
        [Tooltip("The player's walking speed while the web sticks, as a share of normal (0.4 = 40%).")]
        [Range(0f, 1f)]
        [SerializeField] private float slowTo = 0.4f;
        [Tooltip("How long the web slows the player, in seconds.")]
        [Min(0f)]
        [SerializeField] private float slowSeconds = 3f;
        [Tooltip("How big it is for hitting things (radius, metres at size 1).")]
        [Min(0.01f)]
        [SerializeField] private float radius = 0.1f;
        [Tooltip("It splats on its own after this many seconds.")]
        [Min(0.5f)]
        [SerializeField] private float lifetime = 4f;

        [Header("Effects")]
        [Tooltip("Appears where it splats, e.g. a puff.")]
        [SerializeField] private GameObject splatEffect;
        [Tooltip("Size of the splat effect. The Hyper Casual FX smoke is about 13 m wide, so 0.03 makes it about 40 cm.")]
        [Min(0f)]
        [SerializeField] private float splatScale = 0.03f;
        [Tooltip("Played when it sticks to the player.")]
        [SerializeField] private AudioClip hitSound;
        [Tooltip("Played when it splats anywhere else or is swatted.")]
        [SerializeField] private AudioClip splatSound;
        [Range(0f, 1f)]
        [SerializeField] private float volume = 1f;

        private const string PlayerVariable = "Player";
        private const string PlayerDamageFsm = "Damage";
        private static readonly RaycastHit[] hits = new RaycastHit[16];

        private Rigidbody body;
        private Transform shooter;
        private Vector3 lastPosition;
        private float dieAt;
        private bool done;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.interpolation = RigidbodyInterpolation.Interpolate;
            lastPosition = body.position;
            dieAt = Time.time + lifetime;
        }

        /// <summary>Sends it flying at this velocity; the shooter's own body is never hit.</summary>
        public void Launch(Vector3 velocity, Transform from)
        {
            shooter = from;
            body.linearVelocity = velocity;
            lastPosition = body.position;
        }

        /// <summary>
        /// The starting velocity at about <paramref name="speed"/> that lands on <paramref name="target"/>: the low arc,
        /// or the furthest-reaching one if it can't get there at that speed.
        /// </summary>
        public static Vector3 Aim(Vector3 from, Vector3 target, float speed, float gravity)
        {
            var to = target - from;
            var flat = new Vector3(to.x, 0f, to.z);
            float x = flat.magnitude, y = to.y;
            if (x < 0.05f || gravity <= 0.01f) return to.normalized * speed;
            float v2 = speed * speed;
            float root = v2 * v2 - gravity * (gravity * x * x + 2f * y * v2);
            float angle = root >= 0f ? Mathf.Atan((v2 - Mathf.Sqrt(root)) / (gravity * x)) : Mathf.PI / 4f;
            return flat / x * (speed * Mathf.Cos(angle)) + Vector3.up * (speed * Mathf.Sin(angle));
        }

        // Sweeps from where it was to where it is: the first thing in between decides.
        private void FixedUpdate()
        {
            if (done) return;
            var now = body.position;
            var step = now - lastPosition;
            float size = Mathf.Max(0.01f, transform.lossyScale.x);
            if (step.sqrMagnitude > 0.000001f)
            {
                int count = Physics.SphereCastNonAlloc(lastPosition, radius * size, step.normalized, hits, step.magnitude, Physics.AllLayers, QueryTriggerInteraction.Collide);
                RaycastHit? first = null;
                for (int i = 0; i < count; i++)
                {
                    if (!Counts(hits[i].collider)) continue;
                    if (first == null || hits[i].distance < first.Value.distance) first = hits[i];
                }
                if (first != null)
                {
                    var hit = first.Value;
                    HitSomething(hit.collider, hit.distance > 0f ? hit.point : now);
                    return;
                }
            }
            lastPosition = now;
            if (Time.time >= dieAt) Splat(now, splatSound);
        }

        // What it can hit: the player (any of their colliders), held weapons and fists, and solid scenery. Not its
        // shooter, other enemies, loose triggers, or other web balls.
        private bool Counts(Collider other)
        {
            if (other == null || (shooter != null && other.transform.IsChildOf(shooter))) return false;
            if (IsPlayer(other) || IsHeldOrFist(other)) return true;
            if (other.isTrigger || other.GetComponentInParent<NavMeshAgent>() != null || other.GetComponentInParent<WebBall>() != null) return false;
            return true;
        }

        private void HitSomething(Collider other, Vector3 point)
        {
            if (IsHeldOrFist(other))
            {
                Splat(point, splatSound);
                return;
            }
            if (IsPlayer(other))
            {
                SendToPlayer(playerEvent);
                PlayerSlow.Apply(slowTo, slowSeconds);
                Splat(point, hitSound != null ? hitSound : splatSound);
                return;
            }
            Splat(point, splatSound);
        }

        private void Splat(Vector3 point, AudioClip sound)
        {
            done = true;
            if (splatEffect != null)
            {
                var effect = Instantiate(splatEffect, point, Quaternion.identity);
                effect.transform.localScale = Vector3.one * splatScale;
                Destroy(effect, 3f);
            }
            if (sound != null) AudioSource.PlayClipAtPoint(sound, point, volume);
            Destroy(gameObject);
        }

        private static bool IsPlayer(Collider other)
        {
            return other.GetComponentInParent<XROrigin>() != null;
        }

        private static bool IsHeldOrFist(Collider other)
        {
            var held = other.attachedRigidbody;
            if (held == null) return false;
            if (Fist.IsFistBall(held)) return true;
            return held.TryGetComponent(out XRGrabInteractable grab) && grab.isSelected;
        }

        private static void SendToPlayer(string eventName)
        {
            if (string.IsNullOrEmpty(eventName)) return;
            var global = FsmVariables.GlobalVariables.FindFsmGameObject(PlayerVariable);
            var origin = FindAnyObjectByType<XROrigin>();
            var target = global != null && global.Value != null ? global.Value : origin != null ? origin.gameObject : null;
            if (target == null) return;
            foreach (var fsm in target.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName == PlayerDamageFsm) fsm.SendEvent(eventName);
            }
        }
    }
}

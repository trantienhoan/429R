using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Game.Combat
{
    /// <summary>
    /// Lets the player punch with an empty hand. Squeezing grip and trigger together (a fist) with nothing in that
    /// hand turns on a small invisible ball at the knuckles that follows the hand by physics, so it hits things like a
    /// held weapon does: enemies take damage by how fast the punch was (x Power; it takes several hard punches to kill a
    /// spider, and mini bosses barely feel it), loose things get pushed, and the controller rumbles.
    /// A fist doesn't pull things from a distance: with the trigger squeezed too, the hand only grabs what's within its
    /// reach, so making a fist while pointing at something doesn't yank it into the hand instead of punching it.
    /// Pointing (trigger let go) and squeezing the grip still pulls things from afar.
    /// Goes on each controller (Left Controller, Right Controller), next to the hand model.
    /// </summary>
    [DisallowMultipleComponent]
    public class Fist : MonoBehaviour, IXRSelectFilter
    {
        [Tooltip("Multiplies the punch speed for damage, like a weapon's Power. At 1.2 a firm punch hurts small bugs, a " +
                 "hard one hurts spiders and cockroaches, and mini bosses (Toughness 5-7) barely feel it. A fist moves " +
                 "only as fast as the hand, so it stays weaker than a swung weapon.")]
        [Min(0f)]
        [SerializeField] private float power = 1.2f;
        [Tooltip("How far the grip and trigger must be squeezed (0 to 1) to make a fist.")]
        [Range(0.1f, 1f)]
        [SerializeField] private float squeeze = 0.6f;
        [Tooltip("Untick to punch with the grip alone, without the trigger.")]
        [SerializeField] private bool needTrigger = true;
        [Tooltip("Size of the fist ball, in metres (its radius).")]
        [Min(0.01f)]
        [SerializeField] private float radius = 0.055f;
        [Tooltip("Moves the fist ball from the knuckles, in the controller's own space, in metres (Z = forward).")]
        [SerializeField] private Vector3 offset = Vector3.zero;
        [Tooltip("Squeeze held this long, in seconds, before the fist counts, so grabbing something doesn't punch it away first.")]
        [Min(0f)]
        [SerializeField] private float formSeconds = 0.12f;
        [Tooltip("With the trigger squeezed at least this much, the hand doesn't pull things from a distance (it's making " +
                 "a fist); it still grabs what's within its reach. 0 = fists pull things from afar too.")]
        [Range(0f, 1f)]
        [SerializeField] private float noFarGrabTrigger = 0.25f;

        [Header("Input (left empty: the hand model's own)")]
        [SerializeField] private InputActionProperty grip;
        [SerializeField] private InputActionProperty trigger;

        [Header("Feel")]
        [Tooltip("Rumble on a hit: x for a soft punch, y for a hard one.")]
        [SerializeField] private Vector2 rumble = new(0.25f, 0.9f);

        // Where the fist is when the bones can't be found: a little in front of the controller.
        private static readonly Vector3 FallbackPoint = new(0f, -0.01f, 0.05f);
        // Punches this fast (metres per second) or faster rumble the most.
        private const float HardPunch = 8f;
        // Farther than this from the hand (stuck on a wall), the ball jumps back to it.
        private const float SnapDistance = 0.3f;
        private const float MaxSpeed = 25f;
        // Things this close to the hand, in metres, are within its reach (the same as Gentle Far Grab's far distance).
        private const float Reach = 0.4f;

        private static readonly List<Fist> fists = new();

        private InputAction gripAction;
        private InputAction triggerAction;
        private readonly List<IXRSelectInteractor> interactors = new();
        private readonly List<NearFarInteractor> nearFars = new();
        private XRBaseInputInteractor rumbler;
        private Transform knuckleA;
        private Transform knuckleB;
        private Transform palm;
        private Rigidbody body;
        private SphereCollider ball;
        private WeaponPower weapon;
        private float squeezedSince = -1f;
        private bool closed;

        /// <summary>Whether the hand is a fist right now (it punches).</summary>
        public bool IsFist => closed;

        /// <summary>Whether this is a fist's punching ball (it has a Weapon Power like a weapon, but it's a hand).</summary>
        public static bool IsFistBall(Component thing)
        {
            return thing != null && thing.TryGetComponent<Punch>(out _);
        }

        private void Awake()
        {
            var hand = GetComponentInChildren<AnimateHandOnInput>(true);
            gripAction = Pick(grip, hand != null ? hand.gripAnimationAction : default);
            triggerAction = Pick(trigger, hand != null ? hand.pinchAnimationAction : default);
            if (gripAction == null) Debug.LogWarning($"[Fist] '{name}' has no grip input, so it can't make a fist.", this);

            GetComponentsInChildren(true, interactors);
            GetComponentsInChildren(true, nearFars);
            rumbler = GetComponentInChildren<XRBaseInputInteractor>(true);
            FindKnuckles();
            MakeBall();
        }

        private void OnEnable()
        {
            fists.Add(this);
            foreach (var nearFar in nearFars) nearFar.selectFilters.Add(this);
            if (body != null) body.gameObject.SetActive(true);
        }

        private void OnDisable()
        {
            fists.Remove(this);
            foreach (var nearFar in nearFars)
            {
                if (nearFar != null) nearFar.selectFilters.Remove(this);
            }
            Open();
            if (body != null) body.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (body != null) Destroy(body.gameObject);
        }

        private void Update()
        {
            bool squeezing = Read(gripAction) >= squeeze && (!needTrigger || triggerAction == null || Read(triggerAction) >= squeeze);
            if (!squeezing || Holding())
            {
                squeezedSince = -1f;
                Open();
                return;
            }

            if (squeezedSince < 0f) squeezedSince = Time.time;
            if (!closed && Time.time - squeezedSince >= formSeconds) Close();
        }

        private void FixedUpdate()
        {
            var target = FistPoint();
            var gap = target - body.position;
            if (!closed || gap.sqrMagnitude > SnapDistance * SnapDistance)
            {
                body.position = target;
                body.linearVelocity = Vector3.zero;
                return;
            }
            body.linearVelocity = Vector3.ClampMagnitude(gap / Time.fixedDeltaTime, MaxSpeed);
        }

        // Called by the ball when it hits something: a rumble as strong as the punch.
        private void OnPunch(Collision collision)
        {
            if (rumbler == null) return;
            float hard = Mathf.InverseLerp(1f, HardPunch, collision.relativeVelocity.magnitude);
            if (hard <= 0f) return;
            rumbler.SendHapticImpulse(Mathf.Lerp(rumble.x, rumble.y, hard), Mathf.Lerp(0.04f, 0.12f, hard));
        }

        private void Close()
        {
            closed = true;
            // Power and size are read each time, so they can be changed while playing.
            weapon.Power = power;
            ball.radius = radius;
            // Starts right at the hand, so it doesn't sweep through whatever is between.
            body.position = FistPoint();
            body.linearVelocity = Vector3.zero;
            ball.enabled = true;
        }

        private void Open()
        {
            if (!closed) return;
            closed = false;
            if (ball != null) ball.enabled = false;
        }

        public bool canProcess => isActiveAndEnabled;

        // Asked by the hand's Near-Far Interactor before it grabs something: with the trigger squeezed (a fist), only
        // things within reach.
        public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {
            if (noFarGrabTrigger <= 0f || interactor is not NearFarInteractor || Read(triggerAction) < noFarGrabTrigger) return true;
            return GentleFarGrab.HandDistance(interactor.transform, interactable) <= Reach;
        }

        // Anything in this hand (a weapon, a lollipop, ...) means it isn't a fist.
        private bool Holding()
        {
            foreach (var interactor in interactors)
            {
                if (interactor != null && interactor.hasSelection) return true;
            }
            return false;
        }

        // The knuckles of the closed hand: between the palm and the first joints of the index and middle fingers.
        private Vector3 FistPoint()
        {
            Vector3 point;
            if (knuckleA != null && knuckleB != null)
            {
                var knuckles = (knuckleA.position + knuckleB.position) * 0.5f;
                point = palm != null ? Vector3.Lerp(palm.position, knuckles, 0.75f) : knuckles;
            }
            else
            {
                point = transform.TransformPoint(FallbackPoint);
            }
            return point + transform.TransformVector(offset);
        }

        private void FindKnuckles()
        {
            foreach (var bone in GetComponentsInChildren<Transform>(true))
            {
                string boneName = bone.name;
                if (knuckleA == null && boneName.StartsWith("f_index.01")) knuckleA = bone;
                else if (knuckleB == null && boneName.StartsWith("f_middle.01")) knuckleB = bone;
                else if (palm == null && boneName.StartsWith("palm.02")) palm = bone;
            }
        }

        // The ball lives outside the rig (a body that physics moves can't ride along inside a moving parent), and
        // doesn't bump into the player's own body or the other fist.
        private void MakeBall()
        {
            var go = new GameObject($"Fist ({name})");
            go.tag = "Weapon";
            body = go.AddComponent<Rigidbody>();
            body.mass = 3f;
            body.useGravity = false;
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.position = FistPoint();
            go.transform.position = body.position;

            ball = go.AddComponent<SphereCollider>();
            ball.radius = radius;
            ball.enabled = false;
            weapon = go.AddComponent<WeaponPower>();
            weapon.Power = power;
            go.AddComponent<Punch>().owner = this;

            var rig = GetComponentInParent<XROrigin>(true);
            if (rig == null) rig = FindAnyObjectByType<XROrigin>();
            if (rig != null)
            {
                foreach (var part in rig.GetComponentsInChildren<Collider>(true)) Physics.IgnoreCollision(ball, part);
            }
            foreach (var other in fists)
            {
                if (other != this && other.ball != null) Physics.IgnoreCollision(ball, other.ball);
            }
        }

        // Its own input if one is set (an empty field still holds an action, just with no bindings), else the hand's.
        private static InputAction Pick(InputActionProperty own, InputActionProperty fallback)
        {
            if (own.reference != null || (own.action != null && own.action.bindings.Count > 0)) return own.action;
            return fallback.action;
        }

        private static float Read(InputAction action)
        {
            return action != null ? action.ReadValue<float>() : 0f;
        }

        /// <summary>On the fist ball: passes its hits back to the Fist.</summary>
        private class Punch : MonoBehaviour
        {
            public Fist owner;

            private void OnCollisionEnter(Collision collision)
            {
                if (owner != null) owner.OnPunch(collision);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (knuckleA == null) FindKnuckles();
            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(FistPoint(), radius);
        }
#endif
    }
}

using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Game.Locomotion
{
    /// <summary>
    /// Keeps a held thing from going much faster than the hand holding it moves it. XR Interaction pulls a held thing
    /// towards the hand as fast as it takes to get there in one physics step, so one pressed against something it can't
    /// push (a wall, a heavy thing) builds up a huge speed without moving, and lands a big hit the moment it slips -
    /// enemies hurt, things broken, the weapon's own hit puffs - though the player never swung it. Each physics step,
    /// after XR Interaction has set its speed, a held thing is only ever slowed (never pushed): to the hand's own speed,
    /// plus how fast the hand's turn swings it round (its spin times how far the thing sits from the hand), plus Catch Up
    /// Speed; and its spin to the hand's plus Catch Up Turn. Held in two hands (Two Handed Grip), the faster hand counts,
    /// and so does the second hand steering it round the first (how fast the hands move against each other across the
    /// line between them, for how far apart they are), so a two-handed swing isn't held back. So a real swing hits as hard as ever. A thing just grabbed
    /// is left alone for Grace Seconds, so one grabbed from afar flies to the hand as before. The hand's speed is
    /// measured in the play area: turning and walking by the controls don't count (Carry Held Things carries held
    /// things along with the player). Goes on the XR Origin, next to Carry Held Things.
    /// </summary>
    // After the XR Interaction Manager (-105) sets held things' speeds in FixedUpdate, and after the hands' tracking (0)
    // in Update.
    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent]
    public class HeldSwingLimit : MonoBehaviour
    {
        [Tooltip("How much faster than the hand moves it a held thing may go, in metres per second (so it can catch up " +
                 "with the hand after being held back).")]
        [Min(0f)]
        [SerializeField] private float catchUpSpeed = 1f;
        [Tooltip("How much faster than the hand turns it a held thing may spin, in degrees per second.")]
        [Min(0f)]
        [SerializeField] private float catchUpTurn = 360f;
        [Tooltip("Seconds after a thing is grabbed before it's limited, so one grabbed from afar flies to the hand freely.")]
        [Min(0f)]
        [SerializeField] private float graceSeconds = 0.4f;

        // No hand moves faster than this, so a tracking jump can't let a held thing loose. Metres per second, radians
        // per second, and metres (the furthest a held thing's middle counts as being from the hand).
        private const float MaxHandSpeed = 20f;
        private const float MaxHandSpin = 40f;
        private const float MaxLever = 1.5f;
        // Two hands closer than this (metres) count as this far apart, so hands side by side can't make a wild turn.
        private const float MinHandGap = 0.1f;

        private class Hand
        {
            public XRBaseInteractor interactor;
            public Vector3 position;
            public Quaternion rotation;
            public bool known;
            // How fast it moved and turned in the play area over the last frame.
            public Vector3 velocity;
            public float speed;
            public float spin;
        }

        private readonly List<Hand> hands = new();
        private readonly Dictionary<IXRSelectInteractor, Hand> handOf = new();
        private readonly HashSet<Rigidbody> limited = new();
        // When each held thing was grabbed (things let go are forgotten).
        private readonly Dictionary<Rigidbody, float> grabbedAt = new();
        private readonly List<Rigidbody> letGo = new();
        private Transform space;

        private void OnEnable()
        {
            var origin = GetComponent<XROrigin>();
            if (origin == null) origin = FindAnyObjectByType<XROrigin>();
            space = origin != null && origin.CameraFloorOffsetObject != null ? origin.CameraFloorOffsetObject.transform : transform;
            hands.Clear();
            handOf.Clear();
            foreach (var interactor in (origin != null ? origin.transform : transform).GetComponentsInChildren<XRBaseInteractor>(true))
            {
                var hand = new Hand { interactor = interactor };
                hands.Add(hand);
                handOf[interactor] = hand;
            }
        }

        // The hands' own motion, measured once a frame (after their tracking moved them) in the play area, so turning
        // and walking by the controls don't count.
        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            var toSpace = Quaternion.Inverse(space.rotation);
            foreach (var hand in hands)
            {
                var attach = hand.interactor != null ? hand.interactor.attachTransform : null;
                if (attach == null) continue;
                var position = space.InverseTransformPoint(attach.position);
                var rotation = toSpace * attach.rotation;
                if (hand.known)
                {
                    hand.velocity = Vector3.ClampMagnitude((position - hand.position) / dt, MaxHandSpeed);
                    hand.speed = hand.velocity.magnitude;
                    hand.spin = Mathf.Min(Quaternion.Angle(hand.rotation, rotation) * Mathf.Deg2Rad / dt, MaxHandSpin);
                }
                hand.position = position;
                hand.rotation = rotation;
                hand.known = true;
            }
        }

        private void FixedUpdate()
        {
            limited.Clear();
            float extraSpin = catchUpTurn * Mathf.Deg2Rad;
            foreach (var hand in hands)
            {
                if (!IsHolding(hand)) continue;
                foreach (var held in hand.interactor.interactablesSelected)
                {
                    // Held in two hands: limited once, by both hands together.
                    if (held == null || !held.transform.TryGetComponent<Rigidbody>(out var body) || body.isKinematic || !limited.Add(body)) continue;
                    // Just grabbed (maybe from afar): it flies to the hand freely first.
                    if (!grabbedAt.TryGetValue(body, out float since)) grabbedAt[body] = since = Time.time;
                    if (Time.time - since < graceSeconds) continue;
                    if (!TryGetMost(held, body, out float most, out float mostSpin)) continue;

                    // As fast as the hands move it, plus a little to catch up.
                    most += catchUpSpeed;
                    var velocity = body.linearVelocity;
                    if (velocity.sqrMagnitude > most * most) body.linearVelocity = velocity * (most / velocity.magnitude);

                    mostSpin += extraSpin;
                    var spin = body.angularVelocity;
                    if (spin.sqrMagnitude > mostSpin * mostSpin) body.angularVelocity = spin * (mostSpin / spin.magnitude);
                }
            }

            // Forget things that were let go, so grabbing them again starts a new grace.
            letGo.Clear();
            foreach (var body in grabbedAt.Keys)
            {
                if (body == null || !limited.Contains(body)) letGo.Add(body);
            }
            foreach (var body in letGo) grabbedAt.Remove(body);
        }

        // The fastest the hands holding it move it (metres per second) and turn it (radians per second): each hand's
        // speed plus its turn swinging the thing round it (spin times how far the thing's middle is from it). Held in
        // two hands, the second hand also steers it round the first, so it can turn as fast as the hands move against
        // each other (across the line between them) for how far apart they are. False if no hand that's being followed
        // holds it.
        private bool TryGetMost(IXRSelectInteractable held, Rigidbody body, out float most, out float mostSpin)
        {
            most = 0f;
            mostSpin = 0f;
            Hand first = null;
            Hand second = null;
            var middle = body.worldCenterOfMass;
            foreach (var holder in held.interactorsSelecting)
            {
                if (holder == null || !handOf.TryGetValue(holder, out var hand) || !IsHolding(hand)) continue;
                most = Mathf.Max(most, hand.speed + hand.spin * Lever(hand, middle));
                mostSpin = Mathf.Max(mostSpin, hand.spin);
                if (first == null) first = hand;
                else if (second == null) second = hand;
            }
            if (first == null) return false;

            if (second != null)
            {
                // Only the hands moving against each other across the line between them turn it: sliding them closer
                // together or farther apart along it (squeezing a hilt) doesn't.
                var gap = second.position - first.position;
                float apart = Mathf.Max(gap.magnitude, MinHandGap);
                var across = Vector3.ProjectOnPlane(second.velocity - first.velocity, gap);
                float steer = Mathf.Min(across.magnitude / apart, MaxHandSpin);
                mostSpin = Mathf.Max(mostSpin, first.spin + steer);
                most = Mathf.Max(most, first.speed + (first.spin + steer) * Lever(first, middle));
            }
            return true;
        }

        private static bool IsHolding(Hand hand)
        {
            var interactor = hand.interactor;
            return interactor != null && interactor.isActiveAndEnabled && interactor.hasSelection && hand.known;
        }

        private static float Lever(Hand hand, Vector3 middle)
        {
            var attach = hand.interactor.attachTransform;
            var grip = attach != null ? attach.position : hand.interactor.transform.position;
            return Mathf.Min((middle - grip).magnitude, MaxLever);
        }
    }
}

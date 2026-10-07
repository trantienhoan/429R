using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game.Locomotion
{
    /// <summary>
    /// Keeps the player walking on the ground instead of riding up and down over things on it, which makes people
    /// dizzy in VR. The player's body walks straight through loose things lying low on the floor (candies, weapons,
    /// pumpkins, small enemies, broken bits: anything with a Rigidbody no wider than Max Width whose top is below Walk
    /// Through Height), and
    /// anything in the player's hands, instead of stepping up onto them; walls, the ground and big things (the stone
    /// path blocks) still stop them. The step it can climb is lowered too. Only the body's solid collider is affected:
    /// triggers (enemy attack zones, pick-ups) still notice the player. Goes on the XR Origin, next to its Character
    /// Controller.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public class StayOnGround : MonoBehaviour
    {
        [Tooltip("Highest step the player can walk up, in metres (the Character Controller's Step Offset). Lower = " +
                 "harder to get lifted up; too low and small ledges in the floor stop them.")]
        [Min(0f)]
        [SerializeField] private float stepOffset = 0.3f;
        [Tooltip("Loose things (with a Rigidbody) whose top is at most this high above the player's feet are walked " +
                 "through instead of stepped on, in metres. Taller ones still block, like walls.")]
        [Min(0f)]
        [SerializeField] private float walkThroughHeight = 0.8f;
        [Tooltip("Only things at most this wide, in metres, are walked through: never the floor, platforms or other " +
                 "big things, even ones with a Rigidbody.")]
        [Min(0.05f)]
        [SerializeField] private float maxWidth = 1.5f;
        [Tooltip("How far around the body it looks for such things each physics step, in metres.")]
        [Min(0.05f)]
        [SerializeField] private float lookAround = 0.4f;

        private const int MaxNearby = 64;
        // Looked at up to at least this high above the feet, in metres, for things in the player's hands.
        private const float BodyHeight = 1.9f;

        private readonly Collider[] nearby = new Collider[MaxNearby];
        private CharacterController body;
        private XROrigin rig;

        private void Awake()
        {
            body = GetComponent<CharacterController>();
            rig = GetComponentInParent<XROrigin>();
        }

        private void Start()
        {
            ApplyStep();
        }

        private void OnValidate()
        {
            if (body != null) ApplyStep();
        }

        private void FixedUpdate()
        {
            if (!body.enabled) return;

            // The XR Origin stands on the floor (the player's real floor), so its height is the feet. Looked at: a
            // column around the body from the feet to above the head, a little wider than the body, so things are let
            // through before the body reaches them.
            float feet = transform.position.y;
            float radius = body.radius * Mathf.Abs(transform.lossyScale.x) + lookAround;
            float headTop = Mathf.Max(body.bounds.max.y, feet + BodyHeight);
            var bottom = new Vector3(transform.position.x, feet + radius - 0.05f, transform.position.z);
            var top = new Vector3(bottom.x, Mathf.Max(bottom.y, headTop - radius), bottom.z);

            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, nearby, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var thing = nearby[i];
                var loose = thing.attachedRigidbody;
                // Static things (walls, the ground) always block.
                if (loose == null || thing == body) continue;
                if (rig != null && thing.transform.IsChildOf(rig.transform)) continue;

                // Small and low, like things lying about; never the floor or a platform (some have a Rigidbody too).
                var shape = thing.bounds;
                bool small = Mathf.Max(shape.size.x, shape.size.z) <= maxWidth;
                bool low = shape.max.y <= feet + walkThroughHeight;
                // Asked again every step: Unity forgets it when either collider is switched off and on.
                if ((small && low) || Held(loose)) Physics.IgnoreCollision(body, thing, true);
            }
        }

        private void ApplyStep()
        {
            // A Character Controller can't step higher than it is tall.
            body.stepOffset = Mathf.Min(stepOffset, body.height + body.radius * 2f);
        }

        // Something in the player's hands: they can't step on their own sword.
        private static bool Held(Rigidbody loose)
        {
            var grab = loose.GetComponent<XRGrabInteractable>();
            return grab != null && grab.isSelected;
        }
    }
}

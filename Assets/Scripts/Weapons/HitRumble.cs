using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Game.Weapons
{
    /// <summary>
    /// Buzzes the controller holding this weapon when it hits something: harder hits buzz stronger and longer.
    /// Held in two hands, both buzz. Thrown weapons (nobody holding them) stay quiet. Goes on the weapon's root,
    /// next to its XR Grab Interactable; Tools > 429 Game > Weapons > Add Hit Rumble To Weapons adds it.
    /// </summary>
    [DisallowMultipleComponent]
    public class HitRumble : MonoBehaviour
    {
        [Tooltip("Hits slower than this, in metres per second, don't buzz, so resting or brushing against things stays quiet.")]
        [Min(0f)]
        [SerializeField] private float minHitSpeed = 1f;
        [Tooltip("Hits this fast or faster buzz at full strength.")]
        [Min(0.1f)]
        [SerializeField] private float fullHitSpeed = 12f;
        [Tooltip("Strength of the softest buzz (0-1).")]
        [Range(0f, 1f)]
        [SerializeField] private float minStrength = 0.2f;
        [Tooltip("Strength of the hardest buzz (0-1).")]
        [Range(0f, 1f)]
        [SerializeField] private float maxStrength = 1f;
        [Tooltip("How long a soft buzz lasts, in seconds. The hardest hits last twice as long.")]
        [Min(0.01f)]
        [SerializeField] private float duration = 0.06f;
        [Tooltip("Shortest time between two buzzes, so scraping along a wall doesn't buzz nonstop.")]
        [Min(0f)]
        [SerializeField] private float cooldown = 0.08f;
        [Tooltip("Hits on objects with this tag don't buzz, e.g. your own body and hands. Empty = everything buzzes.")]
        [SerializeField] private string ignoreTag = "Player";

        private XRGrabInteractable grab;
        private float nextRumble;

        private void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
        }

        // Unity sends collisions of every collider under this Rigidbody here, so child colliders count too.
        private void OnCollisionEnter(Collision collision)
        {
            if (grab == null || !grab.isSelected || Time.time < nextRumble) return;
            if (!string.IsNullOrEmpty(ignoreTag) && (collision.collider.CompareTag(ignoreTag) || collision.gameObject.CompareTag(ignoreTag))) return;

            float speed = collision.relativeVelocity.magnitude;
            if (speed < minHitSpeed) return;

            float hardness = Mathf.InverseLerp(minHitSpeed, fullHitSpeed, speed);
            float strength = Mathf.Lerp(minStrength, maxStrength, hardness);
            float length = duration * (1f + hardness);

            foreach (var interactor in grab.interactorsSelecting)
            {
                // Hands and controllers are input interactors; sockets and the like have nothing to buzz.
                if (interactor is XRBaseInputInteractor hand) hand.SendHapticImpulse(strength, length);
            }
            nextRumble = Time.time + cooldown;
        }
    }
}

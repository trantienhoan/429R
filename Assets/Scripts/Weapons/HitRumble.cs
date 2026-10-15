using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game.Weapons
{
    /// <summary>
    /// Buzzes the controller holding this weapon (or anything held) when it hurts something: the more damage, the stronger
    /// the buzz (Hit Buzz). Enemies and furniture with Enemy Hit Damage report their hits by themselves; this tells Hit
    /// Buzz to watch the PlayMaker breakables it hits (boxes, books, pumpkins...). Thrown things (nobody holding them),
    /// the player's own body and fists, and the moment it's picked up (a far grab flies it in through things) don't count.
    /// Goes on the weapon's root, next to its XR Grab Interactable; Tools > 429 Game > Weapons > Add Hit Rumble To Weapons
    /// adds it, and Hit Rumble On Grab (on the XR Origin) adds it to anything else the moment it's picked up.
    /// </summary>
    [DisallowMultipleComponent]
    public class HitRumble : MonoBehaviour
    {
        [Tooltip("Hits on objects with this tag never count, e.g. your own body and hands. Empty = everything counts.")]
        [SerializeField] private string ignoreTag = "Player";
        [Tooltip("Seconds after it's picked up before its hits count (a far grab flies it in through things).")]
        [Min(0f)]
        [SerializeField] private float quietAfterGrab = 0.3f;

        private XRGrabInteractable grab;
        private Rigidbody body;
        private float countFrom;

        private void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            body = GetComponent<Rigidbody>();
        }

        private void OnEnable()
        {
            if (grab != null) grab.selectEntered.AddListener(OnGrabbed);
        }

        private void OnDisable()
        {
            if (grab != null) grab.selectEntered.RemoveListener(OnGrabbed);
        }

        private void OnGrabbed(SelectEnterEventArgs args) => Grabbed();

        /// <summary>It was just picked up: its hits don't count for Quiet After Grab.</summary>
        public void Grabbed() => countFrom = Mathf.Max(countFrom, Time.time + quietAfterGrab);

        // Unity sends collisions of every collider under this Rigidbody here, so child colliders count too.
        private void OnCollisionEnter(Collision collision)
        {
            if (grab == null || body == null || !grab.isSelected || Time.time < countFrom) return;
            if (!string.IsNullOrEmpty(ignoreTag) && (collision.collider.CompareTag(ignoreTag) || collision.gameObject.CompareTag(ignoreTag))) return;
            // The player's own body, hands and fists.
            if (collision.collider.GetComponentInParent<XROrigin>() != null) return;
            if (collision.rigidbody != null && Game.Combat.Fist.IsFistBall(collision.rigidbody)) return;
            Game.Combat.HitBuzz.Watch(collision, body);
        }
    }
}

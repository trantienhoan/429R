using System.Collections.Generic;
using System.Text.RegularExpressions;
using Game.PlayGround;
using Game.Weapons;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game.Inventory
{
    /// <summary>
    /// A jumping pumpkin breaks the candy it sits on: once it has landed and settled (slower than Settled Speed), any candy
    /// (Candy_1 ... Candy_8) inside it or squashed under it breaks the usual way (its health runs out: pieces and smoke).
    /// That includes its own Candy Trail, which passes through it while it bounces (so it isn't smashed mid-jump) and can
    /// end up under it when it lands. Candy it only bumps on the way, candy in a hand and flying candy (kinematic) are left
    /// alone. Goes on the pumpkin (the object with the Rigidbody), e.g. Pumpkin_3, 5 and 7.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class CrushCandy : MonoBehaviour
    {
        [Tooltip("Slower than this (metres per second) counts as sitting.")]
        [Min(0f)]
        [SerializeField] private float settledSpeed = 0.4f;
        [Tooltip("Squashed candy under it this far below its middle (share of its height from the bottom) breaks; candy " +
                 "just leaning on its side doesn't.")]
        [Range(0f, 1f)]
        [SerializeField] private float underShare = 0.35f;

        private const float Every = 0.15f;
        // The candies, not their broken pieces (Candy_Drop).
        private static readonly Regex CandyName = new(@"^Candy_\d+$");
        private static readonly Collider[] hits = new Collider[32];

        private Rigidbody body;
        private readonly List<Collider> solids = new();
        private float nextLook;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            foreach (var part in GetComponentsInChildren<Collider>(true))
            {
                if (!part.isTrigger) solids.Add(part);
            }
        }

        private bool Settled => body.linearVelocity.sqrMagnitude < settledSpeed * settledSpeed;

        // Candy inside it (its own trail passes through it, so there's no collision to hear about).
        private void FixedUpdate()
        {
            if (Time.time < nextLook || !Settled) return;
            nextLook = Time.time + Every;
            foreach (var part in solids)
            {
                if (part == null || !part.enabled || !part.gameObject.activeInHierarchy) continue;
                var box = part.bounds;
                int count = Physics.OverlapBoxNonAlloc(box.center, box.extents, hits, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    var other = hits[i];
                    if (!IsCandy(other.attachedRigidbody)) continue;
                    if (Physics.ComputePenetration(part, part.transform.position, part.transform.rotation,
                                                   other, other.transform.position, other.transform.rotation, out _, out _))
                        Break(other.attachedRigidbody.gameObject);
                }
            }
        }

        // Other candy squashed under it once it sits still.
        private void OnCollisionStay(Collision collision)
        {
            if (!Settled || !IsCandy(collision.rigidbody)) return;
            var bounds = BodyBounds();
            if (collision.rigidbody.worldCenterOfMass.y <= bounds.min.y + bounds.size.y * underShare)
                Break(collision.rigidbody.gameObject);
        }

        private Bounds BodyBounds()
        {
            var bounds = new Bounds(body.worldCenterOfMass, Vector3.zero);
            foreach (var part in solids)
            {
                if (part != null && part.enabled) bounds.Encapsulate(part.bounds);
            }
            return bounds;
        }

        private static bool IsCandy(Rigidbody candy)
        {
            if (candy == null || candy.isKinematic) return false;
            if (!CandyName.IsMatch(CookingIngredient.BaseName(candy.name))) return false;
            return !candy.TryGetComponent<XRGrabInteractable>(out var grab) || !grab.isSelected;
        }

        private static void Break(GameObject candy)
        {
            if (!Blast.HasHealth(candy, out var health, out _) || health == null || health.Value <= 0f) return;
            // Crushed by a pumpkin, not hit: no damage number.
            Game.Combat.DamageNumbers.Quiet(candy);
            health.Value = 0f;
        }
    }
}

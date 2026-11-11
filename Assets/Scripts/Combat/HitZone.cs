using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// A part of an enemy that loses its health more slowly: hits on the collider next to this take only Damage Share of
    /// their damage off (user, 2026-11-11, the pumpkin boss's giant pumpkin butt: "punching or hitting its hips below part
    /// only do 1/5 damage"). Enemy Hit Damage reads it for every hit on that collider: a punch or flying jab, a weapon in
    /// the hand, a thrown thing, a shot; a knock-out counts on the collider nearest where it landed and a blast on the one
    /// nearest its middle. Whole numbers: the fractions add up from hit to hit. Its Part Hit event still tells the full hit
    /// (the boss's pumpkin cracks by it). No Hit Zone: the full damage, as always.
    /// </summary>
    [DisallowMultipleComponent]
    public class HitZone : MonoBehaviour
    {
        [Tooltip("The share of a hit's damage this part takes off the enemy's health: 1 = all of it, 0.2 = a fifth.")]
        [Range(0f, 1f)]
        [SerializeField] private float damageShare = 1f;

        /// <summary>The share of a hit's damage this part takes off the health.</summary>
        public float DamageShare
        {
            get => damageShare;
            set => damageShare = Mathf.Clamp01(value);
        }

        /// <summary>The Damage Share of the Hit Zone on this collider; 1 without one (or no collider).</summary>
        public static float ShareOf(Collider part)
        {
            return part != null && part.TryGetComponent(out HitZone zone) ? zone.damageShare : 1f;
        }
    }
}

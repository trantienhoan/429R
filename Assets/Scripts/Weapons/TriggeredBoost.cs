using Game.Combat;
using UnityEngine;

namespace Game.Weapons
{
    /// <summary>
    /// For a weapon whose trigger grows a part of it through its Animator (Hammer_Boong and Hammer_Tool: holding the
    /// trigger plays Fire, which grows the hammer head to Animated Size). It makes that part grow bigger (to Triggered
    /// Size instead, following the animation's own timing) and makes the weapon hit harder the bigger it is: at full
    /// size its hits do Damage Boost times the damage (Weapon Power's Damage) and Power Boost times the power. It follows
    /// the part's real size, so it's right however the animation gets there and back (dropped while big, too).
    /// Goes on the weapon's root, next to its Weapon Power. Tools > 429 Game > Combat > Stronger Triggered Hammers adds it.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WeaponPower))]
    public class TriggeredBoost : MonoBehaviour
    {
        [Tooltip("The part the trigger grows (the hammer head bone, Bone.001).")]
        [SerializeField] private Transform grows;
        [Tooltip("How big the animation (Fire) makes it at full size.")]
        [Min(1.01f)]
        [SerializeField] private float animatedSize = 2.11f;
        [Tooltip("How big it gets instead at full size.")]
        [Min(1f)]
        [SerializeField] private float triggeredSize = 3f;
        [Tooltip("At full size its hits do this many times the damage (1.5: a hammer's best hit 8 → 12).")]
        [Min(0f)]
        [SerializeField] private float damageBoost = 1.5f;
        [Tooltip("At full size its hits count this many times as hard (more of them are big hits that knock enemies back). " +
                 "1 = no change.")]
        [Min(0f)]
        [SerializeField] private float powerBoost = 1f;

        private WeaponPower weapon;
        private float baseDamage;
        private float basePower;
        private Vector3 lastAnimated;
        private Vector3 lastWritten;
        private bool wrote;

        /// <summary>How grown it is: 0 = normal, 1 = full size.</summary>
        public float Grown { get; private set; }

        private void Awake()
        {
            weapon = GetComponent<WeaponPower>();
            baseDamage = weapon.Damage;
            basePower = weapon.Power;
        }

        private void OnDisable()
        {
            if (weapon == null) return;
            weapon.Damage = baseDamage;
            weapon.Power = basePower;
            Grown = 0f;
        }

        // After the Animator has posed it this frame.
        private void LateUpdate()
        {
            if (grows == null) return;

            // The animation sets the size again every frame; if it didn't this frame, it's still what it set last time.
            var animated = wrote && grows.localScale == lastWritten ? lastAnimated : grows.localScale;
            lastAnimated = animated;

            Grown = Mathf.InverseLerp(1f, animatedSize, animated.x);
            float extra = Mathf.Lerp(1f, triggeredSize / animatedSize, Grown);
            lastWritten = animated * extra;
            grows.localScale = lastWritten;
            wrote = true;

            weapon.Damage = baseDamage * Mathf.Lerp(1f, damageBoost, Grown);
            weapon.Power = basePower * Mathf.Lerp(1f, powerBoost, Grown);
        }
    }
}

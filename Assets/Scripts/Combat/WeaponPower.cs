using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// How hard this weapon (or thrown thing) hits compared with a bare fist. Power multiplies its hit speed before the
    /// damage is worked out, so a hammer (2) turns a normal swing into a big hit. Damage multiplies the damage that hit
    /// does: a fist's best hit does 5, a hammer's (1.6) 8, a thrown book's (0.4) 2. Anything without it counts as 1 and 1.
    /// Goes on the weapon's root (the object with the Rigidbody). The player's Weapon Efficiency (a level-up stat)
    /// multiplies the Damage of everything but fists, which have Fist Size and Power instead.
    /// Tools > 429 Game > Combat > Balance Damage sets both for every weapon and throwable.
    /// </summary>
    [DisallowMultipleComponent]
    public class WeaponPower : MonoBehaviour
    {
        [Tooltip("Multiplies this weapon's hit speed. 1 = a bare swing, 2 = twice as hard, below 1 = softer (e.g. a pillow).")]
        [Min(0f)]
        [SerializeField] private float power = 1f;
        [Tooltip("Multiplies the damage of its hits. 1 = a fist (the best hit does 5), 1.6 = a hammer (8), 0.4 = a book (2). " +
                 "A hit that counts always does at least 1.")]
        [Min(0f)]
        [SerializeField] private float damage = 1f;

        public float Power
        {
            get => power;
            set => power = Mathf.Max(0f, value);
        }

        public float Damage
        {
            get => damage;
            set => damage = Mathf.Max(0f, value);
        }

        /// <summary>
        /// Extra damage multiplier set while the game runs (not saved), on top of Damage: e.g. 1.5 while a weapon is held in
        /// two hands (TwoHandedGrip), or 2 for punches while the Elon Boong buff is on (PlayerBuffs). 1 = none.
        /// </summary>
        [System.NonSerialized] public float boost = 1f;

        /// <summary>The Power of whatever made this collision, or 1 if it has none.</summary>
        public static float Of(Collision collision)
        {
            var weapon = Find(collision);
            return weapon != null ? weapon.power : 1f;
        }

        /// <summary>The Damage of whatever made this collision (with Weapon Efficiency, but not for fists), or 1 if it has none.</summary>
        public static float DamageOf(Collision collision)
        {
            return DamageOf(Find(collision));
        }

        /// <summary>The Damage of the weapon on this body, counted as for its hits (e.g. a fist ball's, with its boost), or 1 if it has none.</summary>
        public static float DamageOf(Rigidbody body)
        {
            return DamageOf(body != null && body.TryGetComponent(out WeaponPower weapon) ? weapon : null);
        }

        private static float DamageOf(WeaponPower weapon)
        {
            if (weapon == null) return 1f;
            float scale = Fist.IsFistBall(weapon) ? weapon.damage : weapon.damage * Game.Leveling.PlayerLevel.WeaponDamageScale;
            return scale * weapon.boost;
        }

        /// <summary>Whether whatever made this collision is a weapon, a fist or a throwable (has a Weapon Power).</summary>
        public static bool Has(Collision collision) => Find(collision) != null;

        private static WeaponPower Find(Collision collision)
        {
            WeaponPower weapon = null;
            if (collision.rigidbody != null) collision.rigidbody.TryGetComponent(out weapon);
            if (weapon == null && collision.collider != null) weapon = collision.collider.GetComponentInParent<WeaponPower>();
            return weapon;
        }
    }
}

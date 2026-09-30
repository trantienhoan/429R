using UnityEngine;

namespace Game.Combat
{
    /// <summary>What one hit did: how strong it was, the damage it did, and whether it was a big hit.</summary>
    public readonly struct HitResult
    {
        public HitResult(float power, float damage, bool isBig)
        {
            Power = power;
            Damage = damage;
            IsBig = isBig;
        }

        /// <summary>Hit speed (metres per second) x the weapon's Power / the enemy's Toughness.</summary>
        public float Power { get; }
        public float Damage { get; }
        public bool IsBig { get; }
    }

    /// <summary>
    /// How hard a hit has to be to hurt, and how much it hurts. A hit's power is its speed in metres per second, times
    /// the weapon's Power, divided by the enemy's Toughness. Too weak does nothing, a normal hit does 1-2 damage, and a
    /// big hit 3-5 and knocks the enemy back. Shared by every enemy; Tools > 429 Game > Combat > Set Up Hit Damage makes it.
    /// </summary>
    [CreateAssetMenu(menuName = "429 Game/Combat/Hit Settings", fileName = "Hit Settings")]
    public class HitSettings : ScriptableObject
    {
        [Tooltip("Hits weaker than this do no damage; the enemy only flinches.")]
        [Min(0f)]
        [SerializeField] private float minHitPower = 3f;
        [Tooltip("Hits at least this strong are big hits: more damage, and the enemy is knocked back.")]
        [Min(0f)]
        [SerializeField] private float bigHitPower = 8f;
        [Tooltip("Big hits this strong or stronger do the most damage.")]
        [Min(0f)]
        [SerializeField] private float maxHitPower = 14f;
        [Tooltip("Damage of a normal hit: x for the weakest, y for the hardest.")]
        [SerializeField] private Vector2 normalHitDamage = new(1f, 2f);
        [Tooltip("Damage of a big hit: x for the weakest, y for the hardest.")]
        [SerializeField] private Vector2 bigHitDamage = new(3f, 5f);

        private static HitSettings defaults;

        /// <summary>The numbers used when no settings asset is assigned.</summary>
        public static HitSettings Defaults
        {
            get
            {
                if (defaults == null)
                {
                    defaults = CreateInstance<HitSettings>();
                    defaults.hideFlags = HideFlags.HideAndDontSave;
                }
                return defaults;
            }
        }

        /// <summary>Works out what a hit of this power does. Damage is a whole number.</summary>
        public HitResult Evaluate(float power)
        {
            if (power < minHitPower) return new HitResult(power, 0f, false);

            if (power < bigHitPower)
            {
                float normal = Mathf.InverseLerp(minHitPower, bigHitPower, power);
                return new HitResult(power, Whole(Mathf.Lerp(normalHitDamage.x, normalHitDamage.y, normal)), false);
            }

            float big = Mathf.InverseLerp(bigHitPower, maxHitPower, power);
            return new HitResult(power, Whole(Mathf.Lerp(bigHitDamage.x, bigHitDamage.y, big)), true);
        }

        // Rounds halves up (Mathf.Round rounds them to even, which would make 4.5 into 4).
        private static float Whole(float damage)
        {
            return Mathf.Floor(damage + 0.5f);
        }
    }
}

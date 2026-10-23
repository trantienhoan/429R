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

        [Header("Hit effect (every enemy)")]
        [Tooltip("The small pop shown where a hit hurts an enemy (Enemy_Hit_Pop). Empty: none. Tools > 429 Game > " +
                 "Enemies > Build Enemy Hit FX makes it.")]
        [SerializeField] private GameObject hitEffect;
        [Tooltip("The bigger pop for a big hit or a knock-out (Enemy_Hit_Big). Empty: the normal pop.")]
        [SerializeField] private GameObject bigHitEffect;
        [Tooltip("Hits doing less damage than this show no pop. 1 = every hit that hurts.")]
        [Min(0f)]
        [SerializeField] private float effectMinDamage = 1f;
        [Tooltip("Only the player's hits pop (a fist, a thing they hold or just threw, a shot). Off: also a chair " +
                 "tipping over or a book falling onto an enemy.")]
        [SerializeField] private bool effectOnlyPlayerHits = true;
        [Tooltip("How big the pop is: x for a 1-damage hit, y for an 8-damage hit (in between, in proportion).")]
        [SerializeField] private Vector2 effectSize = new(0.8f, 1.3f);

        [Header("Headshot (thrown things)")]
        [Tooltip("A thrown thing (not a fist, not a weapon in the hand) that hurts an enemy on the head knocks it out for " +
                 "this many seconds: it's shoved back and lies stunned (a boss with its Stun Guard up shrugs that part " +
                 "off). The hit's own damage is unchanged. 0 = no headshots.")]
        [Min(0f)]
        [SerializeField] private float headshotStunSeconds = 1.2f;
        [Tooltip("How far a headshot knocks it back and how high it hops (shares of a big hit's knock-back).")]
        [SerializeField] private Vector2 headshotPush = new(0.7f, 0.8f);
        [Tooltip("EXP the player gets for a headshot, on top of the enemy's own. 0 = none.")]
        [Min(0f)]
        [SerializeField] private float headshotExp = 1f;
        [SerializeField] private string headshotWords = "HEADSHOT!";
        [SerializeField] private Color headshotColor = new(1f, 0.35f, 0.2f);
        [Tooltip("Played on the enemy's head at a headshot, e.g. a comic bonk. One at random.")]
        [SerializeField] private AudioClip[] headshotSounds = System.Array.Empty<AudioClip>();
        [Range(0f, 1f)]
        [SerializeField] private float headshotVolume = 1f;

        // A pop lasts under half a second, so its copy is free again soon (a swarm fight needs only a few copies).
        private const float EffectSeconds = 0.6f;
        // Moved this far towards the eyes, so the enemy's own body doesn't hide half of it.
        private const float EffectTowardsEyes = 0.05f;

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

        /// <summary>
        /// What a hit of this power does with a weapon of this Damage (see Weapon Power): the damage times it, a whole
        /// number, at least 1 for a hit that counts. It's only a big hit (knock-back) if it still does a big hit's damage,
        /// so a thrown book doesn't send an enemy flying.
        /// </summary>
        public HitResult Evaluate(float power, float damageScale)
        {
            var hit = Evaluate(power);
            if (hit.Damage <= 0f || Mathf.Approximately(damageScale, 1f)) return hit;

            float damage = Mathf.Max(1f, Whole(hit.Damage * damageScale));
            return new HitResult(power, damage, hit.IsBig && damage >= bigHitDamage.x);
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

        /// <summary>Whether this hit shows a pop: it hurt enough, and (with Effect Only Player Hits) the player made it.</summary>
        public bool ShowsEffect(HitResult hit, bool byPlayer)
        {
            return hit.Damage > 0f && hit.Damage >= effectMinDamage && (byPlayer || !effectOnlyPlayerHits);
        }

        /// <summary>
        /// Shows the hit pop at a point: the big one for a big hit, sized by the damage, times Size (the enemy's own
        /// Hit Effect Size).
        /// </summary>
        public void PlayEffect(Vector3 at, HitResult hit, float size = 1f)
        {
            var effect = hit.IsBig && bigHitEffect != null ? bigHitEffect : hitEffect;
            if (effect == null) return;

            float scale = Mathf.Lerp(effectSize.x, effectSize.y, Mathf.InverseLerp(1f, 8f, hit.Damage)) * size;
            var eyes = Camera.main;
            if (eyes != null) at = Vector3.MoveTowards(at, eyes.transform.position, EffectTowardsEyes);
            EffectPool.Play(effect, at, scale, EffectSeconds);
        }

        /// <summary>Whether thrown things can land headshots (Headshot Stun Seconds above 0).</summary>
        public bool Headshots => headshotStunSeconds > 0f;

        public float HeadshotStunSeconds => headshotStunSeconds;
        public Vector2 HeadshotPush => headshotPush;
        public float HeadshotExp => headshotExp;

        /// <summary>The headshot's words and sound, at the enemy's head.</summary>
        public void PlayHeadshot(Vector3 at)
        {
            if (!string.IsNullOrEmpty(headshotWords)) Game.VFX.FloatingWords.Show(headshotWords, headshotColor, at + Vector3.up * 0.25f);
            if (headshotSounds.Length == 0) return;
            var clip = headshotSounds[Random.Range(0, headshotSounds.Length)];
            if (clip != null) AudioSource.PlayClipAtPoint(clip, at, headshotVolume);
        }

        /// <summary>Gets the pops ready, so the first hit doesn't stutter (once; more calls do nothing).</summary>
        public void WarmEffects()
        {
            EffectPool.Warm(hitEffect, 4);
            EffectPool.Warm(bigHitEffect, 2);
        }

        // Rounds halves up (Mathf.Round rounds them to even, which would make 4.5 into 4).
        private static float Whole(float damage)
        {
            return Mathf.Floor(damage + 0.5f);
        }
    }
}

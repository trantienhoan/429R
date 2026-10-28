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
    /// the weapon's Power, divided by the enemy's Toughness. Two steps, whole numbers (user, 2026-10-27): too weak does
    /// nothing, a normal hit does the weapon's Damage (a punch 1, a hammer 2), and a hard hit twice that (a hard throw
    /// three times) and knocks the
    /// enemy back. Shared by every enemy, piece of furniture and prop; Tools > 429 Game > Combat > Set Up Hit Damage makes it.
    /// </summary>
    [CreateAssetMenu(menuName = "429 Game/Combat/Hit Settings", fileName = "Hit Settings")]
    public class HitSettings : ScriptableObject
    {
        [Tooltip("Hits weaker than this do no damage; the enemy only flinches.")]
        [Min(0f)]
        [SerializeField] private float minHitPower = 3f;
        [Tooltip("Hits at least this strong are hard hits: Hard Hit Times the damage, and the enemy is knocked back.")]
        [Min(0f)]
        [SerializeField] private float bigHitPower = 8f;
        [Tooltip("A hard hit does this many times a normal hit's damage (2: a punch 1, a hard punch 2).")]
        [Min(1)]
        [SerializeField] private int hardHitTimes = 2;
        [Tooltip("A hard hit with a thrown thing (on what it hits, and on the thrown thing itself) does this many times a " +
                 "normal hit's damage instead: a good hard throw counts the most (user, 2026-10-27: 3, a thrown book 6).")]
        [Min(1)]
        [SerializeField] private int hardThrowTimes = 3;
        // Set by Tools > 429 Game > Combat > Whole Numbers > 1. Halve Health And Blasts, so it only ever halves once.
        [SerializeField, HideInInspector] private bool healthHalved;

        [Header("Punches (the last resort: little damage, a big shove)")]
        [Tooltip("The combo (user, 2026-10-27): a punch that hurts knocks the enemy back out of the fist's reach but into " +
                 "the flying jab's, whatever its Fist Size: this share of how far a good jab flies now (0.4-0.75 m at Fist " +
                 "Size 1, up to about 1.9 m at 10), x for a normal punch, y for a hard one. Heavier enemies (the bosses) go " +
                 "less far.")]
        [SerializeField] private Vector2 punchPush = new(0.55f, 0.75f);
        [Tooltip("How high a punched enemy hops (shares of a hard hit's hop): x normal punch, y hard.")]
        [SerializeField] private Vector2 punchHop = new(0.35f, 0.5f);
        [Tooltip("How long a punched enemy stays stunned after it lands, in seconds (time for the follow-up jab): x normal " +
                 "punch, y hard.")]
        [SerializeField] private Vector2 punchStun = new(0.7f, 1f);
        [Tooltip("A punch too weak to hurt still shoves the enemy this share of a good jab's reach (and stuns it Weak " +
                 "Punch Stun seconds). 0 = it only flinches.")]
        [Range(0f, 1f)]
        [SerializeField] private float weakPunchPush = 0.3f;
        [Min(0f)]
        [SerializeField] private float weakPunchStun = 0.25f;

        [Header("Flying jabs (the skill move: unlocked and grown by Fist Size)")]
        [Tooltip("A hard, fast flying jab does this much more than a normal one (not twice): 2 at the 1st Fist Size point, " +
                 "3 hard (user, 2026-10-27).")]
        [Min(0)]
        [SerializeField] private int flyingHardBonus = 1;
        [Tooltip("A flying jab that hurts knocks the enemy back much farther than a punch: shares of a hard hit's " +
                 "knock-back, x normal, y hard.")]
        [SerializeField] private Vector2 flyingPush = new(1.6f, 2.2f);
        [Tooltip("How high it hops (shares of a hard hit's hop): x normal, y hard.")]
        [SerializeField] private Vector2 flyingHop = new(0.8f, 1.2f);
        [Tooltip("How long it stays stunned after it lands, in seconds: x normal, y hard.")]
        [SerializeField] private Vector2 flyingStun = new(1f, 1.4f);

        [Header("Blasts (more push than damage)")]
        [Tooltip("A blast's damage at its edge, as a share of its damage in the middle (1 = the same everywhere). Like a " +
                 "hit's, it's divided by the Toughness and made whole: a tough enemy near the edge is only pushed.")]
        [Range(0f, 1f)]
        [SerializeField] private float blastEdgeDamage = 0.5f;
        [Tooltip("How far a blast throws an enemy in its middle, in shares of the enemy's own Knock Back Distance (less " +
                 "towards the edge, none at it). Enemies with Knock Back off (bosses that stand their ground) stay put.")]
        [Min(0f)]
        [SerializeField] private float blastKnockBack = 1.5f;
        [Tooltip("How high it hops in the middle, in shares of its own Knock Back Hop.")]
        [Min(0f)]
        [SerializeField] private float blastHop = 1.2f;
        [Tooltip("How long it lies stunned after landing, in seconds, in the middle.")]
        [Min(0f)]
        [SerializeField] private float blastStun = 0.8f;
        [Tooltip("Loose things (props, furniture, weapons) are pushed this many times as hard as each blast's own Push.")]
        [Min(0f)]
        [SerializeField] private float blastPushScale = 1.5f;

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
        [Tooltip("How big the pop is: x for a 1-damage hit, y for a 4-damage hit (in between, in proportion).")]
        [SerializeField] private Vector2 effectSize = new(0.8f, 1.3f);

        [Header("Stunned (mini bosses and the pumpkin boss)")]
        [Tooltip("Stars circling over the head while a mini boss is knocked back and stunned, or the pumpkin boss is " +
                 "dizzy (Stun_Stars; Tools > 429 Game > Enemies > Build Stun Stars FX makes it). Empty: none.")]
        [SerializeField] private GameObject stunStars;
        [Tooltip("How big the ring of stars is, compared with the head it's over (1 = the usual).")]
        [Min(0.1f)]
        [SerializeField] private float stunStarsSize = 1f;

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
        [Tooltip("Played on the enemy's head at a headshot, e.g. a comic bonk. One at random.")]
        [SerializeField] private AudioClip[] headshotSounds = System.Array.Empty<AudioClip>();
        [Range(0f, 1f)]
        [SerializeField] private float headshotVolume = 1f;

        // A pop lasts under half a second, so its copy is free again soon (a swarm fight needs only a few copies).
        private const float EffectSeconds = 0.6f;
        // Moved this far towards the eyes, so the enemy's own body doesn't hide half of it.
        private const float EffectTowardsEyes = 0.05f;

        private static HitSettings defaults;
        private static HitSettings shared;

        /// <summary>The numbers used when no settings asset is assigned.</summary>
        public static HitSettings Defaults
        {
            get
            {
                if (defaults == null)
                {
                    makingDefaults = true;
                    defaults = CreateInstance<HitSettings>();
                    makingDefaults = false;
                    defaults.hideFlags = HideFlags.HideAndDontSave;
                }
                return defaults;
            }
        }

        /// <summary>
        /// The game's Hit Settings asset once anything using it has loaded (every enemy and breakable points at it), else
        /// the defaults: for code with no asset of its own, e.g. the blasts.
        /// </summary>
        public static HitSettings Shared => shared != null ? shared : Defaults;

        private static bool makingDefaults;

        private void OnEnable()
        {
            if (!makingDefaults) shared = this;
        }

        /// <summary>
        /// What a hit of this power does with a weapon of this Damage (see Weapon Power, a whole number, at least 1): a
        /// normal hit does the Damage, a hard hit Hard Hit Times it (and knocks the enemy back), or Hard Throw Times it when
        /// a thrown thing is in it (Thrown).
        /// </summary>
        public HitResult Evaluate(float power, float damage, bool thrown = false, bool flyingFist = false)
        {
            if (power < minHitPower) return new HitResult(power, 0f, false);

            damage = Mathf.Max(1f, Whole(damage));
            bool hard = power >= bigHitPower;
            if (!hard) return new HitResult(power, damage, false);
            // A hard flying jab: one more (technique, user 2026-10-27), not twice.
            if (flyingFist) return new HitResult(power, damage + flyingHardBonus, true);
            return new HitResult(power, damage * (thrown ? hardThrowTimes : hardHitTimes), true);
        }

        /// <summary>What a hit of this power does with a Damage of 1 (a punch): 0, 1, or a hard hit's 2.</summary>
        public HitResult Evaluate(float power) => Evaluate(power, 1f);

        /// <summary>
        /// How a punch knocks an enemy back: x = the distance (for the fist's own punch a share of the flying jab's reach,
        /// for a flying jab a share of a hard hit's knock-back), y = share of a hard hit's hop, z = stun seconds.
        /// Hurt: a punch that did damage (Hard: a hard one); otherwise a punch too weak to hurt (zero x: no shove).
        /// </summary>
        public Vector3 PunchKnock(bool hurt, bool hard, bool flyingFist = false)
        {
            if (!hurt) return new Vector3(weakPunchPush, weakPunchPush, weakPunchStun);
            if (flyingFist)
                return hard ? new Vector3(flyingPush.y, flyingHop.y, flyingStun.y) : new Vector3(flyingPush.x, flyingHop.x, flyingStun.x);
            return hard ? new Vector3(punchPush.y, punchHop.y, punchStun.y) : new Vector3(punchPush.x, punchHop.x, punchStun.x);
        }

        /// <summary>A hit that counts, normal or hard, with a weapon of this Damage (Thrown: a thrown thing is in it).</summary>
        public float HitDamage(float damage, bool hard, bool thrown = false) =>
            Mathf.Max(1f, Whole(damage)) * (hard ? (thrown ? hardThrowTimes : hardHitTimes) : 1);

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

            float scale = Mathf.Lerp(effectSize.x, effectSize.y, Mathf.InverseLerp(1f, 4f, hit.Damage)) * size;
            var eyes = Camera.main;
            if (eyes != null) at = Vector3.MoveTowards(at, eyes.transform.position, EffectTowardsEyes);
            EffectPool.Play(effect, at, scale, EffectSeconds);
        }

        /// <summary>
        /// What a blast of this Damage (in its middle) does to something Edge of the way out (0 = in the middle, 1 = at
        /// the edge) with this Toughness: whole, 0 = it's only pushed.
        /// </summary>
        public float BlastDamage(float damage, float edge, float toughness)
        {
            if (damage <= 0f) return 0f;
            return Whole(damage * Mathf.Lerp(1f, blastEdgeDamage, Mathf.Clamp01(edge)) / Mathf.Max(0.1f, toughness));
        }

        /// <summary>
        /// How a blast throws an enemy Edge of the way out: x = shares of its Knock Back Distance, y = shares of its hop,
        /// z = stun seconds (all less towards the edge, nothing at it).
        /// </summary>
        public Vector3 BlastKnock(float edge) => new Vector3(blastKnockBack, blastHop, blastStun) * (1f - Mathf.Clamp01(edge));

        /// <summary>Loose things are pushed this many times as hard as a blast's own Push.</summary>
        public float BlastPushScale => blastPushScale;

        /// <summary>Whether thrown things can land headshots (Headshot Stun Seconds above 0).</summary>
        public bool Headshots => headshotStunSeconds > 0f;

        /// <summary>The stars shown over a stunned boss's head (null: none), and how big (1 = the usual).</summary>
        public GameObject StunStars => stunStars;
        public float StunStarsSize => stunStarsSize;

        public float HeadshotStunSeconds => headshotStunSeconds;
        public Vector2 HeadshotPush => headshotPush;
        public float HeadshotExp => headshotExp;

        /// <summary>The headshot's sound, at the enemy's head.</summary>
        public void PlayHeadshot(Vector3 at)
        {
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

        /// <summary>Rounds halves up (Mathf.Round rounds them to even, which would make 4.5 into 4).</summary>
        public static float Whole(float damage)
        {
            return Mathf.Floor(damage + 0.5f);
        }
    }
}

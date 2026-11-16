using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game.Weapons
{
    /// <summary>
    /// The explosions the Throw Power and Weapon Efficiency stats unlock (user, 2026-11-12), all their numbers and looks
    /// in one place (Assets/Resources/Blast Skills.asset; Tools > 429 Game > Combat > Set Up Gloves makes it). The
    /// unlocks themselves are Player Level's (own points + the gloves', never candies'):
    /// - Explosive Objects (Throw Power 1, 4, 7 = levels 1, 2, 3; Throwing gloves: level 1): everything the player throws
    ///   hard enough (the hard-throw rule of the books: the hand's speed, Hard Throw Hand Speed x the thing's Throw
    ///   Velocity Scale x Player Level's Hard Throw Share) explodes where it hits, bigger and harder each level. The thing
    ///   itself isn't hurt by its own blast. Books, bottles and flip-flops (Grenade) keep their own blast, grown by the
    ///   level (Grenade Radius / Damage Bonus).
    /// - Bomber Man (Throw Power 10): every throw (a toss too, not a thing put down) explodes where it hits, and again
    ///   Second Blast Seconds later where it is then. One throw, two blasts: never more.
    /// - Impact (Weapon Efficiency 1, 4, 7 = small, decent, big; Soldier gloves: small): a thing or an enemy hit with a
    ///   weapon held in the hand and knocked into another thing or enemy within Knock Seconds explodes where they meet,
    ///   hurting both. Once per knock.
    /// - Weapon Master (Weapon Efficiency 10): every hit of a held weapon on something that can be hurt (furniture too)
    ///   explodes where it hit, bigger than the punches' biggest blast (Explosive Fist 3: 0.6 m, 3 damage).
    /// Damage: the user doubled it (2026-11-12), reaches unchanged. Impacts and Weapon Master never hurt the player or what
    /// they hold; thrown blasts keep the books' rule (user, 2026-11-12: a small bite within Thrown Player Reach, 0.5 m, Bomber
    /// Man's too, never farther). Never explode (Explodes): candy, Seeds, Coins, Chests, the cooking things, and (user,
    /// 2026-11-12) the picture puzzle's pieces, Jenga blocks, the bowling balls, the magic hats, boongs, candles and the
    /// Legendary Sword and Shield. Without this asset only the books, bottles and flip-flops explode (as before).
    /// </summary>
    [CreateAssetMenu(fileName = "Blast Skills", menuName = "429 Game/Blast Skills")]
    public class BlastSkills : ScriptableObject
    {
        [Header("Explosive Objects (Throw Power 1 / 4 / 7; Throwing gloves: level 1)")]
        [Tooltip("How far a thrown thing's blast reaches at levels 1, 2 and 3, in metres. Eaten Throw Power candies add Player " +
                 "Level's Blast Radius Per Point each (up to its Max Blast Radius).")]
        [SerializeField] private Vector3 thrownRadius = new(0.6f, 0.9f, 1.2f);
        [Tooltip("Health everything in it loses, at levels 1, 2 and 3.")]
        [SerializeField] private Vector3 thrownDamage = new(2f, 4f, 6f);
        [Tooltip("How fast it pushes loose things away (metres per second), at levels 1, 2 and 3.")]
        [SerializeField] private Vector3 thrownPush = new(3f, 3.5f, 4f);
        [Tooltip("Books, bottles and flip-flops (Grenade) keep their own blast; at levels 1, 2 and 3 (and Bomber Man as 3) " +
                 "it reaches this much farther, in metres...")]
        [SerializeField] private Vector3 grenadeRadiusBonus = new(0f, 0.25f, 0.5f);
        [Tooltip("...and does this much more damage (on top of its own; no cap).")]
        [SerializeField] private Vector3 grenadeDamageBonus = new(0f, 2f, 4f);
        [Tooltip("A throw counts as hard (and explodes) with the hand moving at least this fast as it lets go, in metres per " +
                 "second, like the books' (x the thing's Throw Velocity Scale, x Player Level's Hard Throw Share).")]
        [Min(0f)]
        [SerializeField] private float hardThrowHandSpeed = 3.5f;
        [Tooltip("A thrown blast hurts the player (Thrown Player Event) only within this many metres of it, like a book's own " +
                 "blast (never the extra reach; user, 2026-11-12: keep this small bite, Bomber Man's blasts too). 0 = never.")]
        [Min(0f)]
        [SerializeField] private float thrownPlayerReach = 0.5f;
        [Tooltip("Sent to the player's Damage FSM when a thrown blast reaches them: Damage = a bug bite (as the books). Empty: " +
                 "never.")]
        [SerializeField] private string thrownPlayerEvent = "Damage";
        [SerializeField] private GameObject[] thrownEffects = Array.Empty<GameObject>();
        [Tooltip("Size of those effects for a level 1 blast (bigger ones grow with their reach).")]
        [Min(0.05f)]
        [SerializeField] private float thrownEffectSize = 0.6f;
        [SerializeField] private AudioClip thrownSound;
        [Range(0f, 1f)]
        [SerializeField] private float thrownVolume = 0.8f;

        [Header("Bomber Man (Throw Power 10)")]
        [Tooltip("Each of its two blasts: reach (metres), damage, push.")]
        [SerializeField] private Vector3 bomber = new(1.4f, 6f, 4.5f);
        [Tooltip("The second blast this many seconds after the first, where the thing is then.")]
        [Min(0.05f)]
        [SerializeField] private float secondBlastSeconds = 0.5f;
        [Tooltip("Any throw counts with the hand moving at least this fast as it lets go (m/s, x Throw Velocity Scale): a " +
                 "toss does, a thing put down doesn't.")]
        [Min(0f)]
        [SerializeField] private float tossHandSpeed = 1f;

        [Header("Impact (Weapon Efficiency 1 / 4 / 7: small, decent, big; Soldier gloves: small)")]
        [Tooltip("How far the impact's blast reaches at small, decent and big, in metres.")]
        [SerializeField] private Vector3 impactRadius = new(0.5f, 0.8f, 1.1f);
        [SerializeField] private Vector3 impactDamage = new(2f, 4f, 6f);
        [SerializeField] private Vector3 impactPush = new(2f, 3f, 4f);
        [Tooltip("A thing knocked with a held weapon makes an impact if it meets another thing or enemy within this many " +
                 "seconds.")]
        [Min(0.1f)]
        [SerializeField] private float knockSeconds = 1.5f;
        [Tooltip("The weapon's hit must be at least this fast (m/s) to knock.")]
        [Min(0f)]
        [SerializeField] private float knockHitSpeed = 2f;
        [SerializeField] private GameObject[] impactEffects = Array.Empty<GameObject>();
        [Min(0.05f)]
        [SerializeField] private float impactEffectSize = 0.5f;
        [SerializeField] private AudioClip impactSound;
        [Range(0f, 1f)]
        [SerializeField] private float impactVolume = 0.7f;

        [Header("Weapon Master (Weapon Efficiency 10)")]
        [Tooltip("Every hit of a held weapon on something that can be hurt explodes: reach (metres), damage, push. Bigger " +
                 "than the punches' biggest blast (0.6 m, 3 damage).")]
        [SerializeField] private Vector3 master = new(0.9f, 8f, 3f);
        [Tooltip("The hit must be at least this fast (m/s).")]
        [Min(0f)]
        [SerializeField] private float masterHitSpeed = 2f;
        [Tooltip("Shortest time between two of them from one weapon, in seconds (one swing hitting several parts: once).")]
        [Min(0f)]
        [SerializeField] private float masterEvery = 0.3f;
        [SerializeField] private GameObject[] masterEffects = Array.Empty<GameObject>();
        [Min(0.05f)]
        [SerializeField] private float masterEffectSize = 0.45f;
        [SerializeField] private AudioClip masterSound;
        [Range(0f, 1f)]
        [SerializeField] private float masterVolume = 0.5f;

        [Header("Limit")]
        [Tooltip("At most this many of these blasts a second, whatever is thrown or hit (the rest just hit), so a pile of " +
                 "throws can't flood the frame.")]
        [Min(1)]
        [SerializeField] private int maxBlastsPerSecond = 10;

        // What Tools > 429 Game > Combat > Set Up Gloves has already set here (1: the doubled damage, user 2026-11-12), so
        // running it again never undoes a change made by hand.
        [SerializeField, HideInInspector] private int menuVersion;

        /// <summary>The asset's name in Resources.</summary>
        public const string AssetName = "Blast Skills";

        private static BlastSkills asset;
        private static BlastSkills defaults;
        private static bool looked;
        private static int blastsThisSecond;
        private static int blastSecond = -1;
        private static Runner runner;

        /// <summary>The asset (Resources/Blast Skills), or null: then only books, bottles and flip-flops explode.</summary>
        public static BlastSkills Asset
        {
            get
            {
                if (!looked)
                {
                    looked = true;
                    asset = Resources.Load<BlastSkills>(AssetName);
                }
                return asset;
            }
        }

        /// <summary>The numbers in use: the asset's, or the defaults above when there's none.</summary>
        public static BlastSkills Values
        {
            get
            {
                if (Asset != null) return asset;
                if (defaults == null)
                {
                    defaults = CreateInstance<BlastSkills>();
                    defaults.hideFlags = HideFlags.HideAndDontSave;
                }
                return defaults;
            }
        }

        /// <summary>
        /// Raised for every blast made here (for tests and anything that wants to know): which ("Thrown", "Bomber",
        /// "Bomber2", "Impact", "Master"), where, its reach and damage, and whether it reached the player.
        /// </summary>
        public static event Action<string, Vector3, float, float, bool> Exploded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            asset = null;
            looked = false;
            blastsThisSecond = 0;
            blastSecond = -1;
            runner = null;
            Exploded = null;
        }

        private static float Pick(Vector3 byLevel, int level) => byLevel[Mathf.Clamp(level, 1, 3) - 1];

        public float HardThrowHandSpeed => hardThrowHandSpeed;
        public float TossHandSpeed => tossHandSpeed;
        public float KnockSeconds => knockSeconds;
        public float KnockHitSpeed => knockHitSpeed;
        public float MasterHitSpeed => masterHitSpeed;
        public float MasterEvery => masterEvery;
        public float SecondBlastSeconds => secondBlastSeconds;

        /// <summary>A book's, bottle's or flip-flop's extra reach at this Explosive Objects level (Bomber Man: as level 3).</summary>
        public float GrenadeRadiusBonus(int level) => level <= 0 ? 0f : Pick(grenadeRadiusBonus, level);

        /// <summary>A book's, bottle's or flip-flop's extra damage at this Explosive Objects level.</summary>
        public float GrenadeDamageBonus(int level) => level <= 0 ? 0f : Pick(grenadeDamageBonus, level);

        /// <summary>A thrown thing's blast reach at this level, before candy (Bomber Man's when Bomber).</summary>
        public float ThrownRadius(int level, bool bomberMan) => bomberMan ? bomber.x : Pick(thrownRadius, level);

        public float ThrownDamage(int level, bool bomberMan) => bomberMan ? bomber.y : Pick(thrownDamage, level);
        public float ImpactRadius(int level) => Pick(impactRadius, level);
        public float ImpactDamage(int level) => Pick(impactDamage, level);
        public float MasterRadius => master.x;
        public float MasterDamage => master.y;

        /// <summary>
        /// Whether a thrown or knocked thing can explode: never candy, Seeds, Coins, Chests, the cooking things (ingredients,
        /// firewood) or what can't be moved.
        /// </summary>
        public static bool Explodes(GameObject thing)
        {
            if (thing == null) return false;
            string name = thing.name;
            foreach (var word in KeepNames)
            {
                if (name.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0) return false;
            }
            if (thing.GetComponentInParent<Game.PlayGround.CookingIngredient>() != null ||
                thing.GetComponentInParent<Game.PlayGround.FireFuel>() != null) return false;
            return !thing.TryGetComponent(out Rigidbody body) || !Game.Combat.PushWeight.IsImmovable(body);
        }

        // Never explode (Explodes), by a word in the name: the keep rule the boss's crushing and Fall Edge use, candy, and
        // (user, 2026-11-12) the picture puzzle's pieces (..._Puzzle_N), Jenga_Block, Bowling_Ball, the magic hats (Hat_Black,
        // Hat_Blue, Hat_Green), the boongs (Hammer_Boong, Elon_Boong, Bus_Boong, Boong_Gun...), candles (Candle_Fat,
        // Pumpkin_Candle, Skull_Candle...) and Pie_Knight_Sword_Legendary / Pie_Knight_Shield_Legendary.
        private static readonly string[] KeepNames =
        {
            "Candy", "Lollipop", "Seed", "Coin", "Chest", "Puzzle", "Jenga", "Bowling_Ball", "Hat_Black", "Hat_Blue", "Hat_Green",
            "Boong", "Candle", "Legendary",
        };

        /// <summary>
        /// Whether this can be hurt by a hit (an enemy, furniture, a breakable: Enemy Hit Damage, a script that takes hits,
        /// a whack pumpkin, a Health FSM with a Damage FSM).
        /// </summary>
        public static bool CanBeHurt(Collider part)
        {
            if (part == null || Blast.IsPlayer(part)) return false;
            if (part.GetComponentInParent<Game.Combat.IHittable>() != null || part.GetComponentInParent<Game.Combat.EnemyHitDamage>() != null ||
                part.GetComponentInParent<Game.Combat.FairHits>() != null) return true;
            for (var t = part.transform; t != null; t = t.parent)
            {
                if (Blast.HasHealth(t.gameObject, out var health, out _) && health != null) return true;
            }
            return false;
        }

        // At most Max Blasts Per Second, all kinds together.
        private bool Budget()
        {
            int second = Mathf.FloorToInt(Time.time);
            if (second != blastSecond)
            {
                blastSecond = second;
                blastsThisSecond = 0;
            }
            if (blastsThisSecond >= maxBlastsPerSecond) return false;
            blastsThisSecond++;
            return true;
        }

        /// <summary>
        /// A thrown thing's blast where it hit (Explosive Objects; Bomber Man: both blasts, the second one later where the
        /// thing is then, unless it's back in a hand). Candy Reach: the eaten candies' extra reach (Player Level). False if
        /// the limit was reached.
        /// </summary>
        public bool ThrownBlast(Vector3 point, Rigidbody thing, int level, bool bomberMan, float candyReach, float maxReach)
        {
            if (!Budget()) return false;
            float reach = ThrownRadius(level, bomberMan);
            reach = Mathf.Min(reach + candyReach, Mathf.Max(reach, maxReach));
            float damage = ThrownDamage(level, bomberMan);
            float push = bomberMan ? bomber.z : Pick(thrownPush, level);
            ThrownBang(bomberMan ? "Bomber" : "Thrown", point, thing, reach, damage, push);
            if (bomberMan) Later(secondBlastSeconds, () =>
            {
                // Where it is now (it may have rolled on), or where it hit if it's gone; not in a hand.
                if (thing != null && thing.TryGetComponent(out XRGrabInteractable grab) && grab.isSelected) return;
                if (!Budget()) return;
                ThrownBang("Bomber2", thing != null ? thing.worldCenterOfMass : point, thing, reach, damage, push);
            });
            return true;
        }

        private void ThrownBang(string kind, Vector3 point, Rigidbody thing, float reach, float damage, float push)
        {
            float size = thrownEffectSize * Mathf.Clamp(reach / Mathf.Max(0.1f, thrownRadius.x), 1f, 2.2f);
            bool hurt = Blast.Explode(point, reach, damage, push, thing, thrownEffects, thrownSound, thrownVolume,
                thrownPlayerEvent, false, size, thrownPlayerReach);
            ViewShake.Near(point, 1f + reach, 4f + reach * 2f);
            Exploded?.Invoke(kind, point, reach, damage, hurt);
        }

        /// <summary>
        /// The second blast of a book, bottle or flip-flop thrown with Bomber Man (its own blast was the first): the same
        /// blast again where it went off, Second Blast Seconds later.
        /// </summary>
        public void GrenadeSecondBlast(Vector3 point, float reach, float damage, float push, GameObject[] effects, AudioClip sound,
            float volume, string playerEvent, float effectScale, float playerReach)
        {
            Later(secondBlastSeconds, () =>
            {
                if (!Budget()) return;
                bool hurt = Blast.Explode(point, reach, damage, push, null, effects, sound, volume, playerEvent, false, effectScale, playerReach);
                ViewShake.Near(point, 1.5f + reach * 1.5f, 5f + reach * 3f);
                Exploded?.Invoke("Bomber2", point, reach, damage, hurt);
            });
        }

        /// <summary>
        /// An impact (a thing knocked by a held weapon meeting another): a blast where they met, hurting both (never the
        /// player or what they hold). Weapon: the weapon that knocked it (left out).
        /// </summary>
        public void ImpactBlast(Vector3 point, int level, Rigidbody weapon)
        {
            if (level <= 0 || !Budget()) return;
            float reach = ImpactRadius(level);
            float size = impactEffectSize * Mathf.Clamp(reach / Mathf.Max(0.1f, impactRadius.x), 1f, 2.2f);
            Blast.Explode(point, reach, ImpactDamage(level), Pick(impactPush, level), weapon, impactEffects, impactSound,
                impactVolume, "", false, size, 0f);
            Exploded?.Invoke("Impact", point, reach, ImpactDamage(level), false);
        }

        /// <summary>Weapon Master: a held weapon's hit explodes where it hit (never the player or what they hold).</summary>
        public void MasterBlast(Vector3 point, Rigidbody weapon)
        {
            if (!Budget()) return;
            Blast.Explode(point, master.x, master.y, master.z, weapon, masterEffects, masterSound, masterVolume, "", false,
                masterEffectSize, 0f);
            Exploded?.Invoke("Master", point, master.x, master.y, false);
        }

        /// <summary>Gets the blasts' effects ready at load.</summary>
        public void Warm()
        {
            Blast.Warm(thrownEffects);
            Blast.Warm(impactEffects);
            Blast.Warm(masterEffects);
        }

        // Runs something a moment later (game time), on a runner that lives as long as the scene.
        private static void Later(float seconds, Action then)
        {
            if (runner == null)
            {
                var go = new GameObject("Blast Skills Runner") { hideFlags = HideFlags.HideInHierarchy };
                runner = go.AddComponent<Runner>();
            }
            runner.StartCoroutine(After(seconds, then));
        }

        private static IEnumerator After(float seconds, Action then)
        {
            yield return new WaitForSeconds(seconds);
            then();
        }

        private class Runner : MonoBehaviour
        {
        }
    }
}

using System;
using System.Collections.Generic;
using Game.Locomotion;
using Game.Saving;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

namespace Game.Leveling
{
    /// <summary>
    /// What the player can put level-up points into: the six normal stats, then the bonus stats (from Extra Revive on),
    /// which only take bonus points. New ones go at the end: the save keeps the points in this order.
    /// </summary>
    public enum Stat
    {
        MaxHealth,
        Toughness,
        WeaponEfficiency,
        FistSize,
        ThrowPower,
        MoveSpeed,
        ExtraRevive,
        // No longer offered (Extra Revive gives the health now); kept so the saved points keep their places.
        BonusMaxHealth,
        BonusFistSize,
        BonusPower,
    }

    /// <summary>
    /// The player's level. Killing enemies gives EXP (each enemy's Exp, on its Enemy Hit Damage or Ghost Balloon; FSMs
    /// can give some with the "Add Player Exp" action). Enough EXP is a level up (Exp To Next Level, one number per
    /// level; the last one is the highest level), and each level up gives Points Per Level points to put into the six
    /// stats in the Level Up menu:
    /// Max Health raises the health cap; Toughness cuts the damage enemies do and how far big hits throw the player;
    /// Weapon Efficiency makes weapons hit harder and wear out slower; Fist Size grows the fists; Throw Power makes
    /// throws leave the hand faster; Move Speed makes walking faster. What one point gives is set below.
    /// Reaching a Bonus Level (10, 20, 21) also gives a bonus point for the three bonus stats (the menu's Bonus Stats
    /// panel): Fist Size at Bonus Multiplier (10) times a normal point, Power (throws as Bonus Multiplier times a normal
    /// Throw Power point, and punches hit and push harder) and Extra Revive (one more revive every run, see Checkpoints:
    /// one more lucky coin to flip when the player goes down, see Revive Coins; and a higher health cap).
    /// The level, the EXP and the points are kept in the save (Save Manager). Keep one in the scene;
    /// Tools > 429 Game > Player > Set Up Level System makes it, with its menu and its notice.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerLevel : MonoBehaviour
    {
        public const int StatCount = 10;

        /// <summary>The stats every level's points go into, in the menu's order.</summary>
        public static readonly Stat[] NormalStats =
            { Stat.MaxHealth, Stat.Toughness, Stat.WeaponEfficiency, Stat.FistSize, Stat.ThrowPower, Stat.MoveSpeed };

        /// <summary>The bonus stats, in the order of the menu's Bonus Stats panel.</summary>
        public static readonly Stat[] BonusStats = { Stat.BonusFistSize, Stat.BonusPower, Stat.ExtraRevive };

        /// <summary>What the save keeps.</summary>
        [Serializable]
        public class SavedLevel
        {
            public int level;
            public int exp;
            // Points in each stat, in the order of Stat.
            public List<int> points = new();
        }

        [Header("Levels")]
        [Tooltip("EXP needed to go up from each level: the first number is from level 0 to 1, the next from 1 to 2, and so " +
                 "on. The player starts at level 0; there are as many levels as numbers here.")]
        [SerializeField] private int[] expToNextLevel =
            { 100, 200, 300, 400, 400, 400, 400, 400, 400, 400, 400, 400, 400, 400, 400, 400, 400, 400, 400, 400, 400 };
        [Tooltip("Points to spend on stats, given at every level up.")]
        [Min(0)]
        [SerializeField] private int pointsPerLevel = 2;
        [Tooltip("The most points one stat can have.")]
        [Min(1)]
        [SerializeField] private int maxPointsPerStat = 10;

        [Header("Bonus stats")]
        [Tooltip("Reaching each of these levels gives a bonus point, for the bonus stats only. The menu's Bonus Stats page " +
                 "shows from the first of them on.")]
        [SerializeField] private int[] bonusLevels = { 10, 20, 21 };
        [Tooltip("A bonus point in Fist Size, and Power's throws, give this many times what a normal point does.")]
        [Min(1f)]
        [SerializeField] private float bonusMultiplier = 10f;
        [Tooltip("Power: besides the throws, punches hit, break and push this share harder (0.25 = 25% more a bonus point).")]
        [Min(0f)]
        [SerializeField] private float punchPowerPerPoint = 0.25f;
        [Tooltip("Extra Revive: besides one more revive every run, the health cap goes up by this much a bonus point (and " +
                 "the player is healed that much).")]
        [Min(0f)]
        [SerializeField] private float reviveHealthPerPoint = 50f;

        [Header("What one point gives")]
        [Tooltip("Max Health: the health cap goes up by this much (and the player is healed that much).")]
        [Min(0f)]
        [SerializeField] private float healthPerPoint = 10f;
        [Tooltip("Toughness: enemies' damage is cut by this share (0.04 = 4% less a point).")]
        [Range(0f, 0.09f)]
        [SerializeField] private float damageCutPerPoint = 0.04f;
        [Tooltip("Toughness: big hits throw the player this share less far and high (0.06 = 6% less a point).")]
        [Range(0f, 0.09f)]
        [SerializeField] private float pushCutPerPoint = 0.06f;
        [Tooltip("Weapon Efficiency: weapons and their bullets hit this share harder (0.05 = 5% more a point). Fists don't " +
                 "count: they have Fist Size.")]
        [Min(0f)]
        [SerializeField] private float weaponDamagePerPoint = 0.05f;
        [Tooltip("Weapon Efficiency: a hard hit wears a weapon the player has held this share less (0.05 = 5% less a " +
                 "point), so it takes more hits to break.")]
        [Range(0f, 0.09f)]
        [SerializeField] private float weaponWearCutPerPoint = 0.05f;
        [Tooltip("Fist Size: the fist grows by this much of the hand's normal size (0.05: Fist Scale 1.5 becomes 1.55).")]
        [Min(0f)]
        [SerializeField] private float fistSizePerPoint = 0.05f;
        [Tooltip("Throw Power: things leave the hand this share faster (0.06 = 6% more a point). Gentle drops aren't sped up.")]
        [Min(0f)]
        [SerializeField] private float throwPerPoint = 0.06f;
        [Tooltip("Move Speed: walking is this share faster (0.04 = 4% more a point).")]
        [Min(0f)]
        [SerializeField] private float speedPerPoint = 0.04f;

        private readonly int[] points = new int[StatCount];
        private readonly Dictionary<ContinuousMoveProvider, float> normalSpeeds = new();
        private readonly List<XRBaseInteractor> hands = new();
        private int level;
        private int exp;

        public static PlayerLevel Instance { get; private set; }

        /// <summary>Raised when the EXP, the level or the points change.</summary>
        public static event Action Changed;

        /// <summary>Raised at a level up, with the new level (once for each level, if several are gained at once).</summary>
        public static event Action<int> LeveledUp;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            Changed = null;
            LeveledUp = null;
            lastRead = null;
        }

        public int Level => level;
        public int MaxLevel => expToNextLevel != null ? expToNextLevel.Length : 0;
        public bool IsMaxLevel => level >= MaxLevel;

        /// <summary>EXP gathered towards the next level.</summary>
        public int Exp => exp;

        /// <summary>EXP the next level takes (0 at the highest level).</summary>
        public int ExpNeeded => IsMaxLevel ? 0 : Mathf.Max(1, expToNextLevel[level]);

        public int MaxPointsPerStat => maxPointsPerStat;

        /// <summary>Points not yet put into a stat (bonus points not counted).</summary>
        public int UnspentPoints
        {
            get
            {
                int spent = 0;
                for (int i = 0; i < StatCount; i++)
                {
                    if (!IsBonus((Stat)i)) spent += points[i];
                }
                return Mathf.Max(0, level * pointsPerLevel - spent);
            }
        }

        /// <summary>Bonus points the levels reached so far gave (one at each Bonus Level).</summary>
        public int BonusPoints
        {
            get
            {
                int count = 0;
                if (bonusLevels != null)
                {
                    foreach (int at in bonusLevels)
                    {
                        if (at > 0 && level >= at) count++;
                    }
                }
                return count;
            }
        }

        /// <summary>Bonus points not yet put into a bonus stat.</summary>
        public int UnspentBonusPoints
        {
            get
            {
                int spent = 0;
                foreach (var stat in BonusStats) spent += points[(int)stat];
                return Mathf.Max(0, BonusPoints - spent);
            }
        }

        /// <summary>The levels that give a bonus point.</summary>
        public IReadOnlyList<int> BonusLevels => bonusLevels ?? Array.Empty<int>();

        /// <summary>Whether the stat takes bonus points instead of the points every level gives.</summary>
        public static bool IsBonus(Stat stat) => stat >= Stat.ExtraRevive;

        public int Points(Stat stat) => points[(int)stat];

        /// <summary>The most points the stat can have (a bonus stat: one for each Bonus Level).</summary>
        public int MaxPoints(Stat stat) => IsBonus(stat) ? Mathf.Max(1, bonusLevels != null ? bonusLevels.Length : 0) : maxPointsPerStat;

        /// <summary>Whether the Level Up menu shows the stat (the bonus stats once the first bonus point is in).</summary>
        public bool IsShown(Stat stat) => !IsBonus(stat) || BonusPoints > 0;

        public bool CanRaise(Stat stat) =>
            Points(stat) < MaxPoints(stat) && (IsBonus(stat) ? UnspentBonusPoints > 0 : UnspentPoints > 0);

        // What the stats do, read by the things they change. Without a Player Level in the scene nothing changes.

        /// <summary>The share of an enemy's damage the player takes (Toughness): 1 = all of it.</summary>
        public static float DamageShare => Instance != null ? Instance.Cut(Stat.Toughness, Instance.damageCutPerPoint) : 1f;

        /// <summary>The share of a big hit's throw the player takes (Toughness): 1 = all of it.</summary>
        public static float PushShare => Instance != null ? Instance.Cut(Stat.Toughness, Instance.pushCutPerPoint) : 1f;

        /// <summary>How much harder weapons hit (Weapon Efficiency): 1 = as always.</summary>
        public static float WeaponDamageScale => Instance != null ? Instance.Gain(Stat.WeaponEfficiency, Instance.weaponDamagePerPoint) : 1f;

        /// <summary>The share of a hit's wear a weapon takes (Weapon Efficiency): 1 = all of it.</summary>
        public static float WeaponWearShare => Instance != null ? Instance.Cut(Stat.WeaponEfficiency, Instance.weaponWearCutPerPoint) : 1f;

        /// <summary>How much bigger the fist is, in hand sizes (Fist Size and its bonus): 0 = as always.</summary>
        public static float FistSizeBonus => Instance != null ? Instance.WithBonus(Stat.FistSize, Stat.BonusFistSize) * Instance.fistSizePerPoint : 0f;

        /// <summary>How much faster throws leave the hand (Throw Power, and the Power bonus): 1 = as always.</summary>
        public static float ThrowScale => Instance != null ? 1f + Instance.WithBonus(Stat.ThrowPower, Stat.BonusPower) * Instance.throwPerPoint : 1f;

        /// <summary>How much faster the player walks (Move Speed): 1 = as always.</summary>
        public static float SpeedScale => Instance != null ? Instance.Gain(Stat.MoveSpeed, Instance.speedPerPoint) : 1f;

        /// <summary>How much harder punches hit, break and push (the Power bonus): 1 = as always.</summary>
        public static float PunchPowerScale => Instance != null ? Instance.Gain(Stat.BonusPower, Instance.punchPowerPerPoint) : 1f;

        private float Cut(Stat stat, float perPoint) => Mathf.Clamp(1f - Points(stat) * perPoint, 0.1f, 1f);

        private float Gain(Stat stat, float perPoint) => 1f + Points(stat) * perPoint;

        // A stat's points with its bonus stat's on top, each bonus point worth Bonus Multiplier normal ones.
        private float WithBonus(Stat stat, Stat bonus) => Points(stat) + bonusMultiplier * Points(bonus);

        /// <summary>The stat's name on the Level Up menu.</summary>
        public static string Name(Stat stat)
        {
            switch (stat)
            {
                case Stat.MaxHealth: return "Max Health";
                case Stat.Toughness: return "Toughness";
                case Stat.WeaponEfficiency: return "Weapon Efficiency";
                case Stat.FistSize: return "Fist Size";
                case Stat.ThrowPower: return "Throw Power";
                case Stat.MoveSpeed: return "Move Speed";
                case Stat.BonusFistSize: return "Fist Size " + Times;
                case Stat.BonusPower: return "Power";
                default: return "Extra Revive";
            }
        }

        // "x10": how many normal points a bonus point is worth.
        private static string Times => "x" + (Instance != null ? Instance.bonusMultiplier : 10f).ToString("0.#");

        /// <summary>
        /// What the stat's points give right now, for the Level Up menu, e.g. "+30 health" (a bonus stat with no point
        /// yet: what one gives).
        /// </summary>
        public string Describe(Stat stat)
        {
            int p = Points(stat);
            // A bonus stat shows what a point would give until it has one.
            int shown = IsBonus(stat) && p == 0 ? 1 : p;
            string each = IsBonus(stat) && p == 0 ? " a point" : "";
            switch (stat)
            {
                case Stat.MaxHealth: return $"+{p * healthPerPoint:0} health";
                case Stat.Toughness: return $"-{Percent(1f - DamageShare)} damage, -{Percent(1f - PushShare)} knockback";
                case Stat.WeaponEfficiency: return $"+{Percent(WeaponDamageScale - 1f)} damage, -{Percent(1f - WeaponWearShare)} wear";
                case Stat.FistSize: return $"+{Percent(p * fistSizePerPoint)} size";
                case Stat.ThrowPower: return $"+{Percent(p * throwPerPoint)} throw speed";
                case Stat.MoveSpeed: return $"+{Percent(SpeedScale - 1f)} walking speed";
                case Stat.BonusFistSize: return $"+{Percent(shown * bonusMultiplier * fistSizePerPoint)} fist size{each}";
                case Stat.BonusPower: return $"+{Percent(shown * bonusMultiplier * throwPerPoint)} throws, +{Percent(shown * punchPowerPerPoint)} punches{each}";
                default:
                    return (shown == 1 ? "+1 lucky coin every run" : $"+{shown} lucky coins every run") +
                           $", +{shown * reviveHealthPerPoint:0} max health{each}";
            }
        }

        private static string Percent(float share) => Mathf.RoundToInt(share * 100f) + "%";

        /// <summary>Gives the player EXP, e.g. for a kill. Nothing happens without a Player Level in the scene.</summary>
        public static void AddExp(int amount)
        {
            if (Instance != null) Instance.GainExp(amount);
        }

        private void GainExp(int amount)
        {
            if (amount <= 0 || IsMaxLevel) return;

            exp += amount;
            int before = level;
            while (!IsMaxLevel && exp >= ExpNeeded)
            {
                exp -= ExpNeeded;
                level++;
            }
            // Nothing more to gather at the top.
            if (IsMaxLevel) exp = 0;

            Changed?.Invoke();
            for (int reached = before + 1; reached <= level; reached++) LeveledUp?.Invoke(reached);
        }

        /// <summary>Puts one unspent point into the stat. False when there's no point to spend or the stat is full.</summary>
        public bool Raise(Stat stat)
        {
            if (!CanRaise(stat)) return false;
            points[(int)stat]++;
            Apply(true, true);
            Changed?.Invoke();
            return true;
        }

        public SavedLevel ToSaveData()
        {
            return new SavedLevel { level = level, exp = exp, points = new List<int>(points) };
        }

        /// <summary>The saved level, from the Save Manager when the save is read (null with no save: level 0).</summary>
        public void LoadSaveData(SavedLevel saved)
        {
            read = true;
            level = saved != null ? Mathf.Clamp(saved.level, 0, MaxLevel) : 0;
            exp = saved != null && !IsMaxLevel ? Mathf.Clamp(saved.exp, 0, ExpNeeded - 1) : 0;
            int left = level * pointsPerLevel;
            int bonusLeft = BonusPoints;
            for (int i = 0; i < StatCount; i++)
            {
                int wanted = saved != null && saved.points != null && i < saved.points.Count ? saved.points[i] : 0;
                // Never more than the level has given (e.g. after Points Per Level was lowered).
                if (IsBonus((Stat)i))
                {
                    // A bonus stat no longer offered gives its points back.
                    if (Array.IndexOf(BonusStats, (Stat)i) < 0) wanted = 0;
                    points[i] = Mathf.Clamp(wanted, 0, Mathf.Min(MaxPoints((Stat)i), bonusLeft));
                    bonusLeft -= points[i];
                    continue;
                }
                points[i] = Mathf.Clamp(wanted, 0, Mathf.Min(maxPointsPerStat, left));
                left -= points[i];
            }
            // The saved revives already count the bonus revives the player has.
            Apply(true, false);
            Changed?.Invoke();
        }

        /// <summary>
        /// What to save: the scene's Player Level, or, in a scene that has none, the level as it was read, so saving
        /// there doesn't lose it.
        /// </summary>
        public static SavedLevel Save() => Instance != null ? Instance.ToSaveData() : lastRead ?? new SavedLevel();

        /// <summary>Reads the saved level into the scene's Player Level (null: back to level 0, e.g. Reset Progress).</summary>
        public static void Load(SavedLevel saved)
        {
            lastRead = saved ?? new SavedLevel();
            if (Instance != null) Instance.LoadSaveData(saved);
        }

        // The level as the Save Manager last read it, for a Player Level that turns up later (or never).
        private static SavedLevel lastRead;
        private bool read;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[Player Level] The scene already has a Player Level ({Instance.name}); '{name}' is ignored.", this);
                enabled = false;
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            // The save was read before this was around (e.g. in an earlier scene).
            if (Instance == this && !read && lastRead != null) LoadSaveData(lastRead);
        }

        private void OnEnable()
        {
            if (Instance != this) return;
            // Weapons the player picks up wear out slower with Weapon Efficiency.
            var origin = FindAnyObjectByType<XROrigin>(FindObjectsInactive.Include);
            if (origin != null) origin.GetComponentsInChildren(true, hands);
            foreach (var hand in hands) hand.selectEntered.AddListener(OnPickedUp);
        }

        private void OnDisable()
        {
            foreach (var hand in hands)
            {
                if (hand != null) hand.selectEntered.RemoveListener(OnPickedUp);
            }
            hands.Clear();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnPickedUp(SelectEnterEventArgs args)
        {
            if (args.interactableObject is Component thing) WeaponWear.Watch(thing.gameObject);
        }

        // The stats that are set on something (the others are read when needed): the health cap, the walking speed and
        // the revives. Heal: a raised cap heals too. Give Revives: a new bonus revive is one more right now too.
        private void Apply(bool heal, bool giveRevives)
        {
            PlayerBoosts.SetLevelHealthBonus(Points(Stat.MaxHealth) * healthPerPoint + Points(Stat.ExtraRevive) * reviveHealthPerPoint, heal);
            ApplySpeed();
            Checkpoints.SetLevelRevives(Points(Stat.ExtraRevive), giveRevives);
        }

        private void ApplySpeed()
        {
            if (normalSpeeds.Count == 0)
            {
                var origin = FindAnyObjectByType<XROrigin>(FindObjectsInactive.Include);
                if (origin == null) return;
                // A slow in progress has changed the speeds: it knows the normal ones.
                origin.TryGetComponent(out PlayerSlow slowed);
                foreach (var provider in origin.GetComponentsInChildren<ContinuousMoveProvider>(true))
                    normalSpeeds[provider] = slowed != null ? slowed.NormalSpeed(provider) : provider.moveSpeed;
            }

            PlayerSlow slow = null;
            foreach (var pair in normalSpeeds)
            {
                if (pair.Key == null) continue;
                if (slow == null) pair.Key.TryGetComponent(out slow);
                if (slow == null) slow = pair.Key.GetComponentInParent<PlayerSlow>();
                float speed = pair.Value * SpeedScale;
                // While the player is slowed (a web), the slow gives the new speed back when it ends.
                if (slow != null && slow.IsSlowed) slow.SetNormalSpeed(pair.Key, speed);
                else pair.Key.moveSpeed = speed;
            }
        }
    }
}

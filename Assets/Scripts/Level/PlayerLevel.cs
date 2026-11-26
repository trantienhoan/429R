using System;
using System.Collections.Generic;
using Game.Combat;
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
        // Explosive Fist (named from when it was Fist Size x10; the name keeps the saved points in place).
        BonusFistSize,
        // Strong Grip (named from when it was Power; it was called Weapon Master until 2026-11-12, when Weapon Efficiency's
        // 10th point took that name; the name keeps the saved points in place).
        BonusPower,
    }

    /// <summary>
    /// The player's level. Killing enemies gives EXP (each enemy's Exp, on its Enemy Hit Damage or Ghost Balloon; FSMs
    /// can give some with the "Add Player Exp" action; breakables with Exp When Broken, e.g. a tree pumpkin's 0.25, give
    /// parts of a point that add up). Enough EXP is a level up (Exp To Next Level, one number per
    /// level; the last one is the highest level), and each level up gives Points Per Level points to put into the six
    /// stats in the Level Up menu:
    /// Max Health raises the health cap and makes eating candy heal more; Toughness cuts the damage enemies do and how
    /// far enemies push the player; Weapon Efficiency makes weapons hit harder and wear out slower; Fist Size makes
    /// punches hit harder and flying punches bigger and fly farther, the hand (or the glove) itself only a little bigger;
    /// Throw Power makes throws leave the hand faster and far grabs pull faster (Gentle Far Grab); Move Speed makes walking
    /// faster.
    /// What the points unlock (user, 2026-11-12; the player's own points in the stat and the gloves', never eaten candies':
    /// a level-10 player with no Throw Power point has no exploding throws at all), told once each with a short notice:
    /// - Fist Size: the flying Jab at 1, Swing at 3, Uppercut at 5, Swollen Hand at 6, Triple Punch at 10;
    /// - Throw Power: Explosive Objects at 1, 4 and 7 (levels 1, 2, 3: every thing thrown hard enough explodes where it
    ///   hits, bigger each level; books, bottles and flip-flops their own way, Grenade), Bomber Man at 10 (every throw
    ///   explodes twice, at the hit and half a second later);
    /// - Weapon Efficiency: Impact at 1, 4 and 7 (small, decent, big: a thing knocked with a held weapon into another
    ///   explodes where they meet), Weapon Master at 10 (every held weapon's hit explodes).
    /// The blasts' numbers are Blast Skills' (Assets/Resources/Blast Skills.asset). What one point gives is set below; the last point that fills a stat up counts as Last Point
    /// Worth points (1 since 2026-11-10, so like any other; it was 7).
    /// Reaching a Bonus Level (10, 20, 21) also gives a bonus point for the three bonus stats (the menu's Bonus Stats
    /// panel): Explosive Fist (every punch that hits something explodes, harder with each point, and flying punches come
    /// quicker one after another; no bigger fists), Strong Grip (was Weapon Master: weapons held in the hand hit harder,
    /// and a held thing that blocks a bite wears 20/30/40% less) and Extra Revive (one more revive every run, see Checkpoints:
    /// one more lucky coin to flip when the player goes down, see Revive Coins; and a higher health cap).
    /// A smoked boong adds Toughness points for a while (Player Buffs); those are never saved. Eaten candies add points
    /// for the run (Eat Candy Buff; user 2026-11-10: one candy kind per stat, see Candy Kinds), each its full share up to
    /// Candy Full Rate Points a stat (5), then only Candy Slow Points (0.1), at most Candy Max Points (10), every share
    /// bigger the more of their own points the player has in that stat (Candy Boost Per Point). Fist Size candy makes
    /// the fist bigger and the jab harder but never unlocks a punch. Never saved, gone when a new run starts.
    /// A pair of gloves (user, 2026-11-12: Gloves, the Knight's pair at the start of each run) adds its kind's points to
    /// one stat for the run (Boxing: Fist Size, Throwing: Throw Power, Soldier: Weapon Efficiency; 3 each), counted like
    /// eaten candies' points (candies and gloves together at most Candy Max Points a stat, nothing in a special stage),
    /// except that, unlike candies, they also count towards the unlocks above, and each kind unlocks its own on top
    /// (Glove Kinds' Unlocks: Boxing all three flying punches, Throwing Explosive Objects 1, Soldier Impact 1), for as
    /// long as they're worn. What a pair unlocks is told once per save slot, a few seconds after it's picked up. A punch
    /// a point unlocks while the player has no gloves is told about once a pair is on (no gloves, no punching).
    /// Inside a special stage (user, 2026-11-10: the nesting pumpkins, Paint Bees and Daruma; see Special Stage Stats)
    /// the player has level-0 stats: their own points and eaten candies count for nothing and the health cap drops to
    /// the level-0 one (Boss Seed raises kept), but the health itself stays as it is, the points stay theirs (menu, save)
    /// and boong buffs and lucky coins stay on; nothing the points unlock works there. Everything comes back when the
    /// stage ends.
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
            // Part of an EXP point gathered so far (e.g. 0.75 after three tree pumpkins at 0.25 each). Older saves: 0.
            public float expPart;
            // Extra stat points given as rewards, kept for good (e.g. the cooking game's first clear). Older saves: 0.
            public int giftPoints;
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
        [Tooltip("Weapon Master: weapons held in the hand do this much more damage, by its points (1st, 2nd, 3rd point: " +
                 "+1, +1, +2). Fists and thrown things don't count.")]
        [SerializeField] private int[] weaponMasterBonus = { 1, 1, 2 };
        [Tooltip("Weapon Master: a held thing that blocks an enemy's bite loses this share less health, by its points " +
                 "(1st, 2nd, 3rd point: 20%, 30%, 40% less).")]
        [SerializeField] private float[] weaponMasterBlockCut = { 0.2f, 0.3f, 0.4f };
        [Tooltip("Extra Revive: besides one more revive every run, the health cap goes up by this much a bonus point (and " +
                 "the player is healed that much).")]
        [Min(0f)]
        [SerializeField] private float reviveHealthPerPoint = 50f;

        [Header("What one point gives")]
        [Tooltip("Max Health: the health cap goes up by this much (and the player is healed that much).")]
        [Min(0f)]
        [SerializeField] private float healthPerPoint = 10f;
        [Tooltip("Max Health: eating candy (candies, lollipops, seeds) heals this share more (0.05 = 5% more a point; " +
                 "+50% at 10 points).")]
        [Min(0f)]
        [SerializeField] private float candyHealPerPoint = 0.05f;
        [Tooltip("Toughness: enemies' damage is cut by this share (0.04 = 4% less a point).")]
        [Range(0f, 0.09f)]
        [SerializeField] private float damageCutPerPoint = 0.04f;
        [Tooltip("Toughness: enemies push the player (bites, the boss's punches, touching a monster) this share less far " +
                 "and high (user, 2026-11-10: 0.04 = 4% less a point; it was 8%). Never less than a tenth.")]
        [Range(0f, 0.09f)]
        [SerializeField] private float pushCutPerPoint = 0.04f;
        [Tooltip("Weapon Efficiency: weapons and their bullets do +1 damage a point (user, 2026-11-10; it was +1 every 5 " +
                 "points), and a hard hit wears a weapon the player has held this share less (0.05 = 5% less a point), so " +
                 "it takes more hits to break.")]
        [Range(0f, 0.09f)]
        [SerializeField] private float weaponWearCutPerPoint = 0.05f;
        [Tooltip("Fist Size: flying punches get this share bigger (user, 2026-11-10: 0.05 = 5% more a point, 1.5x at 10 " +
                 "points; it was 10%). A flying jab flies as far as it is big, so it flies a little farther too. The " +
                 "Fist's Max Fly Size caps it.")]
        [Min(0f)]
        [SerializeField] private float punchSizePerPoint = 0.05f;
        [Tooltip("Fist Size: the hand itself grows by this much of its normal size, only a little so it never blocks the " +
                 "view (0.02: Fist Scale 1.25 becomes 1.27; the Fist's Max Hand Growth caps it).")]
        [Min(0f)]
        [SerializeField] private float handSizePerPoint = 0.02f;
        [Tooltip("Throw Power: things leave the hand this share faster (0.03 = 3% more a point: +30% at 10). Gentle drops " +
                 "aren't sped up.")]
        [Min(0f)]
        [SerializeField] private float throwPerPoint = 0.03f;
        [Tooltip("Throw Power never makes throws more than this many times faster (1.6 = " +
                 "+60%). Throws already leave the hand 4.5-6 times faster than it moves; much faster and bottles and " +
                 "pumpkins can fly through a small bug.")]
        [Min(1f)]
        [SerializeField] private float maxThrowScale = 1.6f;
        [Tooltip("Throw Power: far grabs pull things to the hand this share faster (0.035 = 3.5% more a point: +35% at " +
                 "10, 9 m/s becomes 12). Gentle Far Grab's Max Pull Speed caps it.")]
        [Min(0f)]
        [SerializeField] private float pullPerPoint = 0.035f;

        [Header("One stat, one move (user, 2026-11-19: every level-up is felt in the move it's for)")]
        [Tooltip("Fist Size: the closed punch (the fist itself, gloves on) does +1 damage every this many points (3: +1 " +
                 "at 3, 6 and 9), eaten candies' whole points included. Flying jabs keep their own +1 a point. 0 = never.")]
        [Min(0)]
        [SerializeField] private int punchDamageEvery = 3;
        [Tooltip("Throw Power: a thrown thing does +1 damage every this many points (2: +5 at 10), eaten candies' whole " +
                 "points included, on top of its own Thrown Bonus (a hard throw triples it all). 0 = never.")]
        [Min(0)]
        [SerializeField] private int throwDamageEvery = 2;

        [Header("Exploding throws (Throw Power)")]
        [Tooltip("What makes thrown blasts bigger is the Explosive Objects level (Throw Power 1 / 4 / 7, Blast Skills); on " +
                 "top, every eaten Throw Power candy point makes them reach this many metres farther (user, 2026-11-12: " +
                 "candies never unlock, they only grow the basic numbers), up to Max Blast Radius.")]
        [Min(0f)]
        [SerializeField] private float blastRadiusPerPoint = 0.08f;
        [Tooltip("Throw Power: every point after the 1st makes a throw need this share less speed to count as a hard throw " +
                 "(set off a book, flip-flop or bottle, burst a tree pumpkin, hit hard): 0.03 = 3% a point, never below Min " +
                 "Hard Throw Share. Throws also leave the hand faster with each point, so it adds up (user, 2026-10-27).")]
        [Range(0f, 0.1f)]
        [SerializeField] private float hardThrowCutPerPoint = 0.03f;
        [Range(0.1f, 1f)]
        [SerializeField] private float minHardThrowShare = 0.55f;
        [Tooltip("The farthest a thrown blast reaches with eaten candies, in metres (a blast that reaches farther by its " +
                 "level keeps that reach).")]
        [Min(0.1f)]
        [SerializeField] private float maxBlastRadius = 2f;
        [Tooltip("Move Speed: walking is this share faster (0.04 = 4% more a point).")]
        [Min(0f)]
        [SerializeField] private float speedPerPoint = 0.04f;
        [Tooltip("Move Speed: a smoked boong's high lasts this share longer a point (0.1 = 10% a point, so at 10 points " +
                 "60 s becomes 120 s). Only the player's own points count, not a boong's or candies' (user, 2026-10-29).")]
        [Min(0f)]
        [SerializeField] private float highTimePerPoint = 0.1f;

        [Header("Last point")]
        [Tooltip("The last point of these stats (the one that fills them up) counts as Last Point Worth points: a big " +
                 "reward for maxing one.")]
        [SerializeField] private Stat[] bigLastPoint =
            { Stat.MaxHealth, Stat.Toughness, Stat.WeaponEfficiency, Stat.ThrowPower, Stat.MoveSpeed };
        [Tooltip("How many points the last point of a Big Last Point stat counts as (user, 2026-11-10: 1, so the 10th " +
                 "point counts like the others and eaten candies fill the rest; it was 7, a full stat counting as 16). " +
                 "Damage, knockback and wear never drop below a tenth.")]
        [Min(1)]
        [SerializeField] private int lastPointWorth = 1;

        [Header("Candy")]
        [Tooltip("The most points eaten candies add to one stat this run (each candy's Eat Candy Buff adds its share, " +
                 "e.g. 0.5). They come on top of the player's own points, aren't saved, and go when a new run starts.")]
        [Min(0f)]
        [SerializeField] private float candyMaxPoints = 10f;
        [Tooltip("Candy points a stat takes at the full rate (each candy its own share). Past this, each candy adds only " +
                 "Candy Slow Points to it, up to Candy Max Points.")]
        [Min(0f)]
        [SerializeField] private float candyFullRatePoints = 5f;
        [Tooltip("What each candy adds to a stat past Candy Full Rate Points (0.1 = a tenth of a point).")]
        [Min(0f)]
        [SerializeField] private float candySlowPoints = 0.1f;
        [Tooltip("How much stronger eaten candies are for each of the player's own points in their stat (user, " +
                 "2026-11-10: the more points a stat has, the sooner its candies fill it). A candy adds its points x (1 + " +
                 "this x the stat's own points): 0.2 gives x1 at 0 points (0.5 a candy), x2 at 5 (1.0), x3 at 10 (1.5). " +
                 "The slow share past Candy Full Rate Points grows the same way.")]
        [Min(0f)]
        [SerializeField] private float candyBoostPerPoint = 0.2f;
        [Tooltip("Eating candy above max health (user, 2026-11-10): up to max health a candy heals in full, from there to " +
                 "this many times max health it heals its action's Above Share (50%), and above that only Candy Slow Heal " +
                 "Share (1.5 = from 150% of max health on). A candy that crosses a line is split.")]
        [Min(1f)]
        [SerializeField] private float candySlowHealFrom = 1.5f;
        [Tooltip("The share of a candy's heal that counts above Candy Slow Heal From x max health (0.1 = 10%). Whole " +
                 "numbers are rounded up or down at random, so it really averages this (a 4 heals 1 four times in ten).")]
        [Range(0f, 1f)]
        [SerializeField] private float candySlowHealShare = 0.1f;
        [Tooltip("Way over the cap (user, 2026-11-22: \"health = maximum health x2, eat candy only heal 5% normal\"): " +
                 "from this many times max health on, a candy heals only Candy Tiny Heal Share.")]
        [Min(1f)]
        [SerializeField] private float candyTinyHealFrom = 2f;
        [Tooltip("The share of a candy's heal that counts above Candy Tiny Heal From x max health (0.05 = 5%; rounded at " +
                 "random like the slow share, so it really averages this).")]
        [Range(0f, 1f)]
        [SerializeField] private float candyTinyHealShare = 0.05f;

        [Header("Flying punches unlocked by Fist Size (own + gloves' points, never candies')")]
        [Tooltip("Fist Size points that unlock the Jab (a fast straight punch flies on). Below it no punch flies. " +
                 "0 = unlocked from the start. Boxing gloves unlock Jab, Swing and Uppercut at once (Glove Kinds).")]
        [Min(0)]
        [SerializeField] private int jabAtFistSize = 1;
        [Tooltip("Fist Size points that unlock the Swing (a fast sideways hook of the fist).")]
        [Min(0)]
        [SerializeField] private int swingAtFistSize = 3;
        [Tooltip("Fist Size points that unlock the Uppercut (a fast drive of the fist upwards; user, 2026-11-12: at 5). " +
                 "(Swing and Uppercut aren't locked for later any more: the old Lock Swing And Uppercut is gone.)")]
        [Min(0)]
        [SerializeField] private int uppercutUnlockPoints = 5;
        [Tooltip("Fist Size points for the Triple Punch: a jab throws 3 fists side by side, a swing or an uppercut 3 in a " +
                 "row. Flying swings and uppercuts also reach their farthest here (they go farther step by step from the " +
                 "Jab's points); a flying jab flies as far as it is big (the Fist's Jab Size Reach).")]
        [Min(1)]
        [SerializeField] private int triplePunchAtFistSize = 10;
        [Tooltip("Fist Size points for Swollen Hand: a red-hot hand's fist (Hot Hands) and its flying punches swell bigger " +
                 "while it's hot (Swollen Size on Hot Hands).")]
        [Min(1)]
        [SerializeField] private int swollenHandAtFistSize = 6;

        [Header("Powers unlocked by Throw Power and Weapon Efficiency (own + gloves' points, never candies')")]
        [Tooltip("Throw Power points for Explosive Objects levels 1, 2 and 3 (user, 2026-11-12: 1, 4, 7): every thing thrown " +
                 "hard enough explodes where it hits, bigger each level (Blast Skills). Throwing gloves give level 1.")]
        [SerializeField] private int[] explosiveAtThrowPower = { 1, 4, 7 };
        [Tooltip("Throw Power points for Bomber Man (10): every throw explodes, at the hit and again a moment later.")]
        [Min(1)]
        [SerializeField] private int bomberManAtThrowPower = 10;
        [Tooltip("Weapon Efficiency points for Impact small, decent and big (1, 4, 7): a thing knocked with a held weapon into " +
                 "another explodes where they meet (Blast Skills). Soldier gloves give the small one.")]
        [SerializeField] private int[] impactAtWeaponEfficiency = { 1, 4, 7 };
        [Tooltip("Weapon Efficiency points for Weapon Master (10): every hit of a held weapon explodes (Blast Skills).")]
        [Min(1)]
        [SerializeField] private int weaponMasterAtWeaponEfficiency = 10;

        [Header("Worn weapons")]
        [Tooltip("A weapon the player has held cracks (Crack Sound) when its health first drops to this share of what it " +
                 "had at the first grab (0.3 = 30%), and creaks (Creak Sounds) at every hard hit after that, higher and " +
                 "higher until it breaks. So the player hears it's about to go. 0 = no warning.")]
        [Range(0f, 1f)]
        [SerializeField] private float crackShare = 0.3f;
        [SerializeField] private AudioClip crackSound;
        [Range(0f, 1f)]
        [SerializeField] private float crackVolume = 0.7f;
        [Tooltip("One of these, picked at random, at each hard hit once it has cracked.")]
        [SerializeField] private AudioClip[] creakSounds = Array.Empty<AudioClip>();
        [Range(0f, 1f)]
        [SerializeField] private float creakVolume = 0.4f;
        [Tooltip("Things whose name has one of these words never crack or creak: they aren't real weapons, just things " +
                 "that break (pumpkins, boxes). They still wear out as before.")]
        [SerializeField] private string[] noCrackNames = { "Pumpkin", "Box" };

        private readonly int[] points = new int[StatCount];
        // Points eaten candies add this run (Eat Candy Buff), by stat; never saved, so a new scene starts without them.
        private readonly float[] candyPoints = new float[StatCount];
        private readonly Dictionary<ContinuousMoveProvider, float> normalSpeeds = new();
        private readonly List<XRBaseInteractor> hands = new();
        private int level;
        private int exp;
        // Part of an EXP point (0 to just under 1), from small rewards like a tree pumpkin's 0.25.
        private float expPart;

        public static PlayerLevel Instance { get; private set; }

        /// <summary>Raised when the EXP, the level or the points change.</summary>
        public static event Action Changed;

        /// <summary>Raised at a level up, with the new level (once for each level, if several are gained at once).</summary>
        public static event Action<int> LeveledUp;

        /// <summary>
        /// A point (or a pair of gloves) just unlocked something, by its name (not when the save is read): a punch ("Jab",
        /// "Swing", "Uppercut", "Triple", or "Swollen": Swollen Hand) or a power ("Explosive1" to "Explosive3",
        /// "BomberMan", "Impact1" to "Impact3", "WeaponMaster"). The Level Up Notice tells the player.
        /// </summary>
        public static event Action<string> PunchUnlocked;

        // The names of everything the points unlock, in the order they're told (punches first).
        public const string Jab = "Jab", Swing = "Swing", Uppercut = "Uppercut", Swollen = "Swollen", Triple = "Triple";
        public const string BomberManName = "BomberMan", WeaponMasterName = "WeaponMaster";
        public static readonly string[] AllUnlocks =
        {
            Jab, Swing, Uppercut, Swollen, Triple, "Explosive1", "Explosive2", "Explosive3", BomberManName,
            "Impact1", "Impact2", "Impact3", WeaponMasterName,
        };

        /// <summary>Whether this unlock is a punch (it waits for gloves: no gloves, no punching).</summary>
        public static bool IsPunch(string name) => name == Jab || name == Swing || name == Uppercut || name == Swollen || name == Triple;

        /// <summary>An eaten candy added points to this stat (Eat Candy Buff): the health HUD shows which, e.g. "+SPD".</summary>
        public static event Action<Stat> CandyEaten;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            Changed = null;
            LeveledUp = null;
            PunchUnlocked = null;
            CandyEaten = null;
            unlockedNow.Clear();
            unlockedBefore.Clear();
            lastRead = null;
            stageHolders.Clear();
            suspended = false;
            describing = false;
            nextRunNews.Clear();
            gloveNews.Clear();
        }

        // The special stages on right now (Special Stage Stats); while any is, the stats are at level 0.
        private static readonly HashSet<UnityEngine.Object> stageHolders = new();
        // What the stage's start put away (see Suspend), given back when the last stage ends.
        private static bool suspended;
        // Everything unlocked right now (Apply Unlocks), and what was unlocked when the special stage started.
        private static readonly HashSet<string> unlockedNow = new();
        private static readonly HashSet<string> unlockedBefore = new();
        private static float levelHealthAtStart;
        // Max Health candy points at the stage's start: a candy eaten inside raises the cap on the way out without healing.
        private static float candyHealthAtStart;
        // The Level Up menu is asking what the points give (Describe): the real stats, also in a special stage.
        private static bool describing;
        // A game over inside a special stage (user, 2026-11-11): the punches and powers ("Jab", ... see Punch Unlocked) a
        // point spent inside unlocked, told about once the next run is under way (Update).
        private static readonly List<string> nextRunNews = new();
        // Glove news waiting to be told (Tell Once): remembered for the save slot once it's really shown.
        private static readonly HashSet<string> gloveNews = new();
        // How long the player is up in the new run before that news shows (the restart's fade, getting placed).
        private const float NextRunNewsDelay = 3f;
        private float upSince = -1f;

        /// <summary>
        /// Whether the player is in a special stage (user, 2026-11-10): the stats are at level 0 until it ends (see
        /// Enter Special Stage).
        /// </summary>
        public static bool InSpecialStage => stageHolders.Count > 0;

        // The stats count as level 0 right now: in a special stage, unless the menu is describing the points.
        private static bool StatsDown => stageHolders.Count > 0 && !describing;

        /// <summary>
        /// A special stage starts (Special Stage Stats, added by Play Bonus Round and Daruma Stage): the first one puts
        /// the stats to level 0 (user, 2026-11-10): no own points or candy points count, the cap drops to the level-0 one
        /// (Boss Seed raises kept), walking goes back to normal and the flying punches lock, all without a notice. The
        /// health stays as it is (user: keep current health). Boong buffs and the lucky coins stay.
        /// </summary>
        public static void EnterSpecialStage(UnityEngine.Object by)
        {
            if (by == null || !stageHolders.Add(by) || stageHolders.Count > 1) return;
            if (Instance != null) Instance.Suspend();
        }

        /// <summary>
        /// A special stage is over (or gone): when no other one is on, the stats come back. Points spent and candies eaten
        /// inside count from now on, and a punch a point inside unlocked is announced now.
        /// </summary>
        public static void ExitSpecialStage(UnityEngine.Object by)
        {
            if (by == null || !stageHolders.Remove(by) || stageHolders.Count > 0) return;
            Resume(false);
        }

        private void Suspend()
        {
            suspended = true;
            unlockedBefore.Clear();
            unlockedBefore.UnionWith(unlockedNow);
            levelHealthAtStart = PlayerBoosts.LevelHealthBonus;
            candyHealthAtStart = candyPoints[(int)Stat.MaxHealth];
            // Lowering the cap never takes health away (Player Boosts), and the revives aren't lowered (Checkpoints
            // would take the coins for good): Apply keeps them at the real points.
            Apply(false, false);
        }

        // Quiet: a game over inside the stage (End Special Stages), so the player is dead. The stats come back without a
        // heal (the restart fills the heart to the real cap anyway) and without a notice over the death (it would stop
        // the game): a punch or a power a point spent inside unlocked works from the new run on, and is told about once
        // that run is under way (Next Run News).
        private static void Resume(bool quiet)
        {
            var player = Instance;
            if (!suspended)
            {
                // Nothing was put away (no Player Level when it started): just the real stats again.
                if (player != null) player.Apply(false, false);
                return;
            }
            suspended = false;
            unlockedNow.Clear();
            unlockedNow.UnionWith(unlockedBefore);
            SetUnlocks();
            if (player == null) return;
            // What was unlocked before the stage, announced first: only what a point spent inside unlocked is new (Apply's
            // own quiet Apply Unlocks then changes nothing; quiet, it sets them without a word and keeps the news for the
            // next run).
            player.ApplyUnlocks(!quiet, quiet ? nextRunNews : null);
            player.Apply(false, false);
            if (quiet) return;
            // The cap comes back without healing, but a Max Health or Extra Revive point spent inside heals as it would
            // have then. A Max Health candy eaten inside doesn't: it healed when it was eaten (like outside a stage).
            float raised = PlayerBoosts.LevelHealthBonus - levelHealthAtStart -
                           (player.candyPoints[(int)Stat.MaxHealth] - candyHealthAtStart) * player.healthPerPoint;
            // (A point raises it by 10 or more; under 1 is only the cap's rounding to a whole number.)
            if (raised >= 1f && PlayerBoosts.TryGetHealth(out float health)) PlayerBoosts.SetHealth(health + raised);
        }

        // A new run (e.g. a game over inside a stage): no stage holds the stats any more, so the full cap is back before
        // the heart is filled. Quietly: the player is dead (a game over) or the scene reloads at once (Re-Dream), so no
        // heal and no notice. Raised twice at a game over: the second time there's nothing left to do.
        private void EndSpecialStages()
        {
            if (stageHolders.Count == 0) return;
            stageHolders.Clear();
            Resume(true);
        }

        public int Level => level;
        public int MaxLevel => expToNextLevel != null ? expToNextLevel.Length : 0;
        public bool IsMaxLevel => level >= MaxLevel;

        /// <summary>EXP gathered towards the next level.</summary>
        public int Exp => exp;

        /// <summary>Part of an EXP point gathered on top of Exp (0 to just under 1), e.g. 0.5 after two tree pumpkins.</summary>
        public float ExpPart => expPart;

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
                return Mathf.Max(0, level * pointsPerLevel + giftPoints - spent);
            }
        }

        /// <summary>Extra stat points given as rewards (spent like a level's; saved).</summary>
        public int GiftPoints => giftPoints;

        /// <summary>
        /// Gives stat points for good, as a reward (user, 2026-11-06: the cooking game's first clear): they're spent in the
        /// Level Up menu like a level's points and kept in the save.
        /// </summary>
        public static void GiveStatPoints(int count)
        {
            if (Instance == null || count <= 0) return;
            Instance.giftPoints += count;
            Changed?.Invoke();
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

        /// <summary>The share of an enemy's push the player takes (Toughness): 1 = all of it.</summary>
        public static float PushShare => Instance != null ? Instance.Cut(Stat.Toughness, Instance.pushCutPerPoint) : 1f;

        /// <summary>How much more eating candy heals (Max Health): 1 = as always.</summary>
        public static float CandyHealScale => Instance != null ? Instance.Gain(Stat.MaxHealth, Instance.candyHealPerPoint) : 1f;

        /// <summary>Above this many times max health, candy heals only Candy Slow Heal Share (1.5 without a Player Level).</summary>
        public static float CandySlowHealFrom => Instance != null ? Instance.candySlowHealFrom : 1.5f;

        /// <summary>The share of a candy's heal above Candy Slow Heal From x max health (0.1 without a Player Level).</summary>
        public static float CandySlowHealShare => Instance != null ? Instance.candySlowHealShare : 0.1f;

        /// <summary>From this many times max health on, candy heals only Candy Tiny Heal Share (2 without a Player Level).</summary>
        public static float CandyTinyHealFrom => Instance != null ? Mathf.Max(Instance.candyTinyHealFrom, Instance.candySlowHealFrom) : 2f;

        /// <summary>The share of a candy's heal above Candy Tiny Heal From x max health (0.05 without a Player Level).</summary>
        public static float CandyTinyHealShare => Instance != null ? Instance.candyTinyHealShare : 0.05f;

        /// <summary>
        /// The extra damage weapons do (Weapon Efficiency, user 2026-11-10): +1 a point, eaten candies' whole points
        /// included. 0 = as always.
        /// </summary>
        public static int WeaponDamageBonus => Instance != null ? Whole(Instance.Worth(Stat.WeaponEfficiency)) : 0;

        /// <summary>The share of a hit's wear a weapon takes (Weapon Efficiency): 1 = all of it.</summary>
        public static float WeaponWearShare => Instance != null ? Instance.Cut(Stat.WeaponEfficiency, Instance.weaponWearCutPerPoint) : 1f;

        /// <summary>
        /// How much bigger the hand is as a fist, in hand sizes (Fist Size and its bonus, a little, and the Green Boong's
        /// buff): 0 = as always.
        /// </summary>
        public static float FistSizeBonus => Instance != null ? (Instance.FistSizePoints + PlayerBuffs.FistSizePoints) * Instance.handSizePerPoint : 0f;

        /// <summary>How much bigger flying punches are (Fist Size and its bonus, and the Green Boong's buff): 0 = as always, 1 = twice as big.</summary>
        public static float PunchSizeBonus => Instance != null ? (Instance.FistSizePoints + PlayerBuffs.FistSizePoints) * Instance.punchSizePerPoint : 0f;

        /// <summary>
        /// How far flying swings and uppercuts (and a closed punch's combo knock-back) have come along to their farthest:
        /// 0 at the Fist Size that unlocks the Jab, 1 at the Triple Punch's. A flying jab flies its own size instead.
        /// </summary>
        public static float PunchReachGrowth => Instance != null
            ? Mathf.InverseLerp(Instance.jabAtFistSize, Instance.triplePunchAtFistSize, Instance.FistSizePoints)
            : 0f;

        /// <summary>
        /// The extra damage flying jabs do (user, 2026-10-27): +1 every Fist Size point, eaten candies' whole points
        /// included (Candy_8, user 2026-11-10). The closed fist doesn't get it.
        /// </summary>
        public static int FlyingJabBonus => Instance != null ? Whole(Instance.Worth(Stat.FistSize)) : 0;

        /// <summary>
        /// The extra damage of the closed punch (user, 2026-11-19, "one stat, one move"): +1 every Punch Damage Every
        /// Fist Size points, eaten candies' whole points included. A bare fist still only taps for 1.
        /// </summary>
        public static int PunchDamageBonus => Instance != null && Instance.punchDamageEvery > 0
            ? Whole(Instance.Worth(Stat.FistSize)) / Instance.punchDamageEvery
            : 0;

        /// <summary>
        /// The extra damage of a thrown thing (user, 2026-11-19, "one stat, one move"): +1 every Throw Damage Every Throw
        /// Power points, eaten candies' whole points included, on top of its Thrown Bonus.
        /// </summary>
        public static int ThrowDamageBonus => Instance != null && Instance.throwDamageEvery > 0
            ? Whole(Instance.Worth(Stat.ThrowPower)) / Instance.throwDamageEvery
            : 0;

        // Whole points of a stat's worth (eaten candies give parts of a point).
        private static int Whole(float worth) => Mathf.FloorToInt(worth + 0.001f);

        /// <summary>How much faster throws leave the hand (Throw Power, at most Max Throw Scale): 1 = as always.</summary>
        public static float ThrowScale => Instance != null
            ? Mathf.Min(1f + Instance.Worth(Stat.ThrowPower) * Instance.throwPerPoint, Instance.maxThrowScale)
            : 1f;

        /// <summary>How much faster far grabs pull things to the hand (Throw Power): 1 = as always.</summary>
        public static float PullScale => Instance != null ? Instance.Gain(Stat.ThrowPower, Instance.pullPerPoint) : 1f;

        /// <summary>
        /// The Explosive Objects level (user, 2026-11-12): 0 none, 1 to 3 at Throw Power 1 / 4 / 7 (own + gloves' points;
        /// Throwing gloves: at least 1). 0 in a special stage, and without a Player Level.
        /// </summary>
        public static int ExplosiveLevel => unlockedNow.Contains("Explosive3") ? 3 : unlockedNow.Contains("Explosive2") ? 2 :
            unlockedNow.Contains("Explosive1") ? 1 : 0;

        /// <summary>Bomber Man (Throw Power 10): every throw explodes twice.</summary>
        public static bool BomberMan => unlockedNow.Contains(BomberManName);

        /// <summary>The Impact level: 0 none, 1 to 3 (small, decent, big) at Weapon Efficiency 1 / 4 / 7 (Soldier gloves: 1).</summary>
        public static int ImpactLevel => unlockedNow.Contains("Impact3") ? 3 : unlockedNow.Contains("Impact2") ? 2 :
            unlockedNow.Contains("Impact1") ? 1 : 0;

        /// <summary>Weapon Master (Weapon Efficiency 10): every held weapon's hit explodes.</summary>
        public static bool WeaponMaster => unlockedNow.Contains(WeaponMasterName);

        /// <summary>Whether this is unlocked right now (a name from All Unlocks).</summary>
        public static bool IsUnlocked(string name) => unlockedNow.Contains(name);

        /// <summary>
        /// Whether thrown books, bottles and flip-flops explode (Grenade): from Explosive Objects level 1 (or Bomber Man).
        /// Without a Player Level in the scene they always do.
        /// </summary>
        public static bool ExplodingThrows => Instance == null || ExplosiveLevel >= 1 || BomberMan;

        /// <summary>
        /// A book's, bottle's or flip-flop's blast reach, from its own: farther with the Explosive Objects level (Blast
        /// Skills' Grenade Radius Bonus; Bomber Man as level 3) and a little with eaten Throw Power candies.
        /// </summary>
        public static float ThrowBlastRadius(float own)
        {
            if (Instance == null) return own;
            int level = BomberMan ? 3 : ExplosiveLevel;
            float reach = own + Game.Weapons.BlastSkills.Values.GrenadeRadiusBonus(level);
            return Mathf.Min(reach + CandyBlastReach, Mathf.Max(reach, Instance.maxBlastRadius));
        }

        /// <summary>
        /// A book's, bottle's or flip-flop's blast damage, from its own: more with the Explosive Objects level (Blast
        /// Skills' Grenade Damage Bonus, user 2026-11-12: +0 / +2 / +4; no cap any more: the old Max Blast Damage is gone).
        /// </summary>
        public static float ThrowBlastDamage(float own)
        {
            if (Instance == null) return own;
            int level = BomberMan ? 3 : ExplosiveLevel;
            return own + Game.Weapons.BlastSkills.Values.GrenadeDamageBonus(level);
        }

        /// <summary>How much farther thrown blasts reach from eaten Throw Power candies (the basic growth), in metres.</summary>
        public static float CandyBlastReach => Instance != null && !StatsDown
            ? Instance.candyPoints[(int)Stat.ThrowPower] * Instance.blastRadiusPerPoint
            : 0f;

        /// <summary>The farthest the candies make a thrown blast reach (a level's own reach can be more).</summary>
        public static float MaxBlastReach => Instance != null ? Instance.maxBlastRadius : 2f;

        /// <summary>
        /// The share of a hard throw's speed a throw needs with Throw Power (1 = as always, less with every point after the
        /// 1st, never below Min Hard Throw Share): books, flip-flops and bottles go off and tree pumpkins burst more easily.
        /// </summary>
        public static float HardThrowShare => Instance != null
            ? Mathf.Max(Instance.minHardThrowShare, 1f - Instance.BlastSteps * Instance.hardThrowCutPerPoint)
            : 1f;

        // Throw Power points after the one that unlocks exploding throws (the last point counting as Last Point Worth).
        private float BlastSteps => Mathf.Max(0f, Worth(Stat.ThrowPower) - 1f);

        /// <summary>How much faster the player walks (Move Speed): 1 = as always.</summary>
        public static float SpeedScale => Instance != null ? Instance.Gain(Stat.MoveSpeed, Instance.speedPerPoint) : 1f;

        /// <summary>
        /// How many times longer a smoked boong's high lasts (Move Speed: High Time Per Point for each of the player's own
        /// points): 1 = as always.
        /// </summary>
        public static float HighTimeScale => Instance != null ? 1f + Instance.OwnWorth(Stat.MoveSpeed) * Instance.highTimePerPoint : 1f;

        /// <summary>The extra damage of weapons held in the hand (the Strong Grip bonus stat; it was Weapon Master): 0 = as always.</summary>
        public static int WeaponMasterBonus
        {
            get
            {
                if (Instance == null) return 0;
                int p = Instance.EffectPoints(Stat.BonusPower);
                var bonus = Instance.weaponMasterBonus;
                return p <= 0 || bonus == null || bonus.Length == 0 ? 0 : bonus[Mathf.Min(p, bonus.Length) - 1];
            }
        }

        /// <summary>The share of a block's wear a held thing takes (the Strong Grip bonus stat): 1 = all of it.</summary>
        public static float WeaponMasterBlockShare => Instance != null ? 1f - Instance.Tier(Instance.weaponMasterBlockCut) : 1f;

        // Weapon Master's value for its points now (its 1st, 2nd or 3rd entry; 0 with no point).
        private float Tier(float[] byPoint)
        {
            int p = EffectPoints(Stat.BonusPower);
            return p <= 0 || byPoint == null || byPoint.Length == 0 ? 0f : byPoint[Mathf.Min(p, byPoint.Length) - 1];
        }

        /// <summary>
        /// Explosive Fist points (a bonus stat): every punch that hits something explodes, harder with each, and the hand
        /// can throw flying punches quicker one after another (Fist: Explosive Fist Faster). 0 = none.
        /// </summary>
        public static int ExplosiveFist => Instance != null ? Instance.EffectPoints(Stat.BonusFistSize) : 0;

        /// <summary>Worn weapons (Weapon Wear): the share of its health at which a weapon cracks (0 = never), and its sounds.</summary>
        public static float CrackShare => Instance != null ? Instance.crackShare : 0f;
        public static AudioClip CrackSound => Instance != null ? Instance.crackSound : null;
        public static float CrackVolume => Instance != null ? Instance.crackVolume : 0f;
        public static float CreakVolume => Instance != null ? Instance.creakVolume : 0f;

        /// <summary>Whether a thing with this name never cracks or creaks (No Crack Names: pumpkins, boxes).</summary>
        public static bool NoCracksFor(string thingName)
        {
            var words = Instance != null ? Instance.noCrackNames : null;
            if (words == null || string.IsNullOrEmpty(thingName)) return false;
            foreach (var word in words)
            {
                if (!string.IsNullOrEmpty(word) && thingName.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        /// <summary>A creak for a worn weapon's hard hit, picked at random (none if there are none).</summary>
        public static AudioClip RandomCreak()
        {
            var clips = Instance != null ? Instance.creakSounds : null;
            return clips != null && clips.Length > 0 ? clips[UnityEngine.Random.Range(0, clips.Length)] : null;
        }

        private float Cut(Stat stat, float perPoint) => Mathf.Clamp(1f - Worth(stat) * perPoint, 0.1f, 1f);

        private float Gain(Stat stat, float perPoint) => 1f + Worth(stat) * perPoint;

        // A stat's points as they count: a full Big Last Point stat's last point counts as Last Point Worth. A boong's
        // points for now (Player Buffs: the Bone Boong's Toughness) and the run's points (Run Points: eaten candies', one
        // candy kind for each of the 6 stats, user 2026-11-10, and the gloves', user 2026-11-12) come on top, never as
        // that last point. Candies' and gloves' Fist Size points make the fist (the glove) and its flying punches bigger
        // and the jab harder; only the gloves' also count for the unlocks (Unlock Points). In a special stage only a
        // boong's count.
        private float Worth(Stat stat) =>
            OwnWorth(stat) + PlayerBuffs.ExtraPoints(stat) + (!StatsDown ? RunPoints(stat) : 0f);

        // The run's points in a stat: eaten candies' and the gloves' (user, 2026-11-12: like candies), together at most
        // Candy Max Points (10). Not saved here: candies are gone after a reload, the gloves come back with the run
        // (Checkpoints keeps them).
        private float RunPoints(Stat stat) =>
            TakesCandy(stat) ? Mathf.Min(candyPoints[(int)stat] + Gloves.PointsFor(stat) + Game.Combat.KnightArmor.PointsFor(stat), candyMaxPoints) : 0f;

        // The points that unlock things in a stat (user, 2026-11-12): the player's own (and a boong's), and the gloves'
        // while they're worn (none in a special stage); never eaten candies' (they'd come and go with every death).
        private float UnlockPoints(Stat stat) => OwnWorth(stat) + PlayerBuffs.ExtraPoints(stat) +
                                                 (!StatsDown ? Gloves.PointsFor(stat) : 0f);

        // A stat's own points as they work right now: none in a special stage (level-0 stats there; the points stay).
        private int EffectPoints(Stat stat) => StatsDown ? 0 : Points(stat);

        // The player's own points as they count (the big last point included), without a boong's or candies'.
        private int OwnWorth(Stat stat)
        {
            int p = EffectPoints(stat);
            if (p > 0 && p >= MaxPoints(stat) && bigLastPoint != null && Array.IndexOf(bigLastPoint, stat) >= 0)
                p += lastPointWorth - 1;
            return p;
        }

        /// <summary>
        /// An eaten candy's points for this run (the PlayMaker action Eat Candy Buff; user 2026-11-10: one candy kind for
        /// each of the 6 stats). They count like the player's own points, except that Fist Size candy never unlocks a
        /// punch. Each candy's points grow with the player's own points in that stat (Candy Boost Per Point): full points
        /// up to Candy Full Rate Points a stat, then only Candy Slow Points a candy, up to Candy Max Points; never saved,
        /// and gone when a new run starts. Returns the stat's candy points now (0 for a bonus stat, or without a Player
        /// Level).
        /// </summary>
        public static float AddCandyPoints(Stat stat, float points)
        {
            if (Instance == null) return 0f;
            if (!TakesCandy(stat))
            {
                Debug.LogWarning($"[Player Level] Candy can't add {Name(stat)} points.", Instance);
                return 0f;
            }
            int i = (int)stat;
            float had = Instance.candyPoints[i];
            // The candy that reaches Candy Full Rate Points tops it up to there; every candy after it adds the slow share.
            // Both are bigger the more of their own points the player has put into the stat (their real points, also
            // in a special stage: the candy counts once it's over).
            if (points > 0f)
            {
                float boost = 1f + Instance.candyBoostPerPoint * Instance.Points(stat);
                points = had < Instance.candyFullRatePoints
                    ? Mathf.Min(points * boost, Instance.candyFullRatePoints - had)
                    : Mathf.Min(points, Instance.candySlowPoints) * boost;
            }
            Instance.candyPoints[i] = Mathf.Clamp(had + points, 0f, Instance.candyMaxPoints);
            if (stat == Stat.MoveSpeed) Instance.ApplySpeed();
            // A higher health cap, without healing (the candy has just healed by itself).
            if (stat == Stat.MaxHealth) Instance.ApplyHealth(false);
            // Only a candy that really added something is shown (the HUD's '+TOPH' and the like): at Candy Max Points it
            // adds nothing (play test 2026-11-10).
            if (Instance.candyPoints[i] > had) CandyEaten?.Invoke(stat);
            return Instance.candyPoints[i];
        }

        /// <summary>The points eaten candies have added to the stat this run (0 without a Player Level).</summary>
        public static float CandyPoints(Stat stat) => Instance != null ? Instance.candyPoints[(int)stat] : 0f;

        /// <summary>Drops every candy point (a new run starts: Checkpoints.RunStarted).</summary>
        public static void ClearCandyPoints()
        {
            if (Instance != null) Instance.DropCandy();
        }

        /// <summary>
        /// The gloves worn changed (Gloves: picked up, read from the save, or off for a new run): their points count from
        /// now on, and what their points and their kind unlock is unlocked (or locked again when they come off). Picked Up: a pair was just picked up, so a raised health cap heals too and what the pair
        /// unlocks is told once per save slot (Glove News), a few seconds later; read from the save or taken off, quietly
        /// (the cap comes down without taking health away).
        /// </summary>
        public static void GlovesChanged(bool pickedUp)
        {
            var player = Instance;
            if (player == null) return;
            player.ApplyHealth(pickedUp);
            player.ApplySpeed();
            var unlocked = pickedUp ? new List<string>() : null;
            player.ApplyUnlocks(false, unlocked);
            if (pickedUp)
            {
                foreach (var name in unlocked) TellOnce(name);
            }
            Changed?.Invoke();
        }

        // Glove news (what a pair just picked up unlocks): told a few seconds later (Next Run News, Update) and only the
        // first time in this save slot (Show Once), so a Boxing pair doesn't stop the game with JAB every run.
        private static void TellOnce(string news)
        {
            if (Game.Menus.ShowOnce.WasShown(GloveNewsKey + news)) return;
            gloveNews.Add(news);
            if (!nextRunNews.Contains(news)) nextRunNews.Add(news);
        }

        // The Show Once keys of the glove news ("GloveNews.Jab", ... "GloveNews.WeaponMaster"; Show Once lists them all).
        public const string GloveNewsKey = "GloveNews.";

        // The 6 stats the level-up points go into; never a bonus stat.
        private static bool TakesCandy(Stat stat) => !IsBonus(stat);

        private void DropCandy()
        {
            Array.Clear(candyPoints, 0, StatCount);
            // The cap a Max Health candy raised comes down (never taking health away), and the walking speed.
            ApplyHealth(false);
            ApplySpeed();
        }

        /// <summary>
        /// A boong buff started or ended (Player Buffs: Bus Boong's Move Speed points, and being high on any boong makes
        /// the player slower): the walking speed is set again. Toughness needs nothing: it's read when needed. The points
        /// spent and the save don't change.
        /// </summary>
        public static void BuffsChanged()
        {
            if (Instance != null) Instance.ApplySpeed();
        }

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
                case Stat.BonusFistSize: return "Explosive Fist";
                // Was "Weapon Master" (user, 2026-11-12: the name went to Weapon Efficiency's 10th point).
                case Stat.BonusPower: return "Strong Grip";
                default: return "Extra Revive";
            }
        }

        /// <summary>
        /// What the stat's points give right now, for the Level Up menu, e.g. "+30 health" (a bonus stat with no point
        /// yet: what one gives). In a special stage too: what they give once it's over.
        /// </summary>
        public string Describe(Stat stat)
        {
            describing = true;
            try { return DescribeNow(stat); }
            finally { describing = false; }
        }

        private string DescribeNow(Stat stat)
        {
            int p = Points(stat);
            // A bonus stat shows what a point would give until it has one.
            int shown = IsBonus(stat) && p == 0 ? 1 : p;
            string each = IsBonus(stat) && p == 0 ? " a point" : "";
            switch (stat)
            {
                case Stat.MaxHealth: return $"+{Worth(stat) * healthPerPoint:0} health, +{Percent(CandyHealScale - 1f)} candy heal";
                case Stat.Toughness: return $"-{Percent(1f - DamageShare)} damage, -{Percent(1f - PushShare)} enemy push";
                case Stat.WeaponEfficiency:
                    return $"+{WeaponDamageBonus} damage, -{Percent(1f - WeaponWearShare)} wear" + NextNote(Stat.WeaponEfficiency);
                case Stat.FistSize:
                    return Join(Join($"+{Percent(FistSizePoints * punchSizePerPoint)} size", p > 0 ? $"flying jab +{FlyingJabBonus} damage" : ""),
                               PunchDamageBonus > 0 ? $"punch +{PunchDamageBonus}" : "") + NextNote(Stat.FistSize);
                case Stat.ThrowPower: return ThrowNote();
                case Stat.MoveSpeed:
                    return Join($"+{Percent(SpeedScale - 1f)} walking speed", p > 0 ? $"+{Percent(HighTimeScale - 1f)} boong time" : "");
                case Stat.BonusFistSize: return (shown == 1 ? "Punches explode" : $"Punches explode, {shown}x blast") + ", quicker jabs";
                case Stat.BonusPower: return WeaponMasterNote(shown);
                default:
                    return (shown == 1 ? "+1 lucky coin every run" : $"+{shown} lucky coins every run") +
                           $", +{shown * reviveHealthPerPoint:0} max health{each}";
            }
        }

        private static string Percent(float share) => Mathf.RoundToInt(share * 100f) + "%";

        private static string Join(string a, string b) => string.IsNullOrEmpty(a) ? b : string.IsNullOrEmpty(b) ? a : a + ", " + b;

        // The Strong Grip row (was Weapon Master): what this many points give, e.g. "+1 held weapon damage, -20% block wear".
        private string WeaponMasterNote(int points)
        {
            float Pick(float[] byPoint) => byPoint == null || byPoint.Length == 0 ? 0f : byPoint[Mathf.Clamp(points, 1, byPoint.Length) - 1];
            int bonus = weaponMasterBonus == null || weaponMasterBonus.Length == 0
                ? 0
                : weaponMasterBonus[Mathf.Clamp(points, 1, weaponMasterBonus.Length) - 1];
            return $"+{bonus} held weapon damage, -{Percent(Pick(weaponMasterBlockCut))} block wear";
        }

        // What a stat's points unlock next, briefly (the user wants few words): ", Uppercut at 5", ", Bomber Man at 10"; ""
        // once all is unlocked.
        private string NextNote(Stat stat)
        {
            float have = UnlockPoints(stat);
            int next = int.MaxValue;
            string what = "";
            foreach (var (name, at) in Unlocks(stat))
            {
                if (at > have && at < next)
                {
                    next = at;
                    what = name;
                }
            }
            return next == int.MaxValue ? "" : $", {what} at {next}";
        }

        // A stat's unlocks with their points, for the menu.
        private IEnumerable<(string name, int at)> Unlocks(Stat stat)
        {
            if (stat == Stat.FistSize)
            {
                yield return ("Jab", jabAtFistSize);
                yield return ("Swing", swingAtFistSize);
                yield return ("Uppercut", uppercutUnlockPoints);
                yield return ("Swollen Hand", swollenHandAtFistSize);
                yield return ("Triple Punch", triplePunchAtFistSize);
            }
            else if (stat == Stat.ThrowPower)
            {
                for (int i = 0; explosiveAtThrowPower != null && i < explosiveAtThrowPower.Length; i++)
                    yield return (i == 0 ? "Explosive Throws" : $"Explosive Throws {i + 1}", explosiveAtThrowPower[i]);
                yield return ("Bomber Man", bomberManAtThrowPower);
            }
            else if (stat == Stat.WeaponEfficiency)
            {
                for (int i = 0; impactAtWeaponEfficiency != null && i < impactAtWeaponEfficiency.Length; i++)
                    yield return (i == 0 ? "Impact" : $"Impact {i + 1}", impactAtWeaponEfficiency[i]);
                yield return ("Weapon Master", weaponMasterAtWeaponEfficiency);
            }
        }

        // The Throw Power row: the throw and pull speed, and what it unlocks next.
        private string ThrowNote()
        {
            float throwShare = Mathf.Min(Worth(Stat.ThrowPower) * throwPerPoint, maxThrowScale - 1f);
            return Join($"+{Percent(throwShare)} throw speed, +{Percent(PullScale - 1f)} pull",
                       ThrowDamageBonus > 0 ? $"+{ThrowDamageBonus} damage" : "") + NextNote(Stat.ThrowPower);
        }

        // Fist Size points as they count for size and reach, eaten candies' included (the bonus stat is Explosive Fist
        // now: it gives no size). Unlocking punches counts only the player's own (Unlock Points).
        private float FistSizePoints => Worth(Stat.FistSize);

        // Everything the points unlock (user, 2026-11-12), from the player's own points in each stat and the gloves' (never
        // candies'), and what the gloves' kind unlocks by itself (Boxing: the three flying punches; Throwing: Explosive
        // Objects 1; Soldier: Impact 1); none of it in a special stage. Things lock again if the points go (Reset
        // Progress, the gloves coming off). Announce: a point just raised it (not the save being read), so the player is
        // told about each new thing (a punch only once gloves are on). Keep: the new ones are added to it instead, to be
        // told about later.
        private void ApplyUnlocks(bool announce, List<string> keep = null)
        {
            var glove = !StatsDown && Gloves.Worn != null ? Gloves.Worn.unlocks : GloveUnlocks.None;
            var now = new HashSet<string>();
            float fist = UnlockPoints(Stat.FistSize);
            bool moves = (glove & GloveUnlocks.FlyingPunches) != 0;
            if (fist >= jabAtFistSize || moves) now.Add(Jab);
            if (fist >= swingAtFistSize || moves) now.Add(Swing);
            if (fist >= uppercutUnlockPoints || moves) now.Add(Uppercut);
            if (fist >= swollenHandAtFistSize) now.Add(Swollen);
            if (fist >= triplePunchAtFistSize) now.Add(Triple);
            float throwing = UnlockPoints(Stat.ThrowPower);
            int explosive = Steps(throwing, explosiveAtThrowPower);
            if ((glove & GloveUnlocks.ExplosiveObjects) != 0) explosive = Mathf.Max(explosive, 1);
            for (int i = 1; i <= Mathf.Min(explosive, 3); i++) now.Add("Explosive" + i);
            if (throwing >= bomberManAtThrowPower) now.Add(BomberManName);
            float weapons = UnlockPoints(Stat.WeaponEfficiency);
            int impact = Steps(weapons, impactAtWeaponEfficiency);
            if ((glove & GloveUnlocks.Impact) != 0) impact = Mathf.Max(impact, 1);
            for (int i = 1; i <= Mathf.Min(impact, 3); i++) now.Add("Impact" + i);
            if (weapons >= weaponMasterAtWeaponEfficiency) now.Add(WeaponMasterName);

            var unlocked = new List<string>();
            foreach (var name in AllUnlocks)
            {
                if (now.Contains(name) && !unlockedNow.Contains(name)) unlocked.Add(name);
            }
            unlockedNow.Clear();
            unlockedNow.UnionWith(now);
            SetUnlocks();
            keep?.AddRange(unlocked);
            if (!announce) return;
            // No gloves, no punching (user, 2026-11-12): a punch's news waits until a pair is on (Update).
            bool punches = Gloves.CanPunch;
            foreach (var name in unlocked)
            {
                if (IsPunch(name) && !punches)
                {
                    if (!nextRunNews.Contains(name)) nextRunNews.Add(name);
                }
                else
                {
                    PunchUnlocked?.Invoke(name);
                }
            }
        }

        // How many of these steps the points have reached (e.g. 1, 4, 7: 5 points = 2).
        private static int Steps(float have, int[] at)
        {
            int count = 0;
            if (at == null) return 0;
            foreach (int step in at)
            {
                if (have >= step) count++;
            }
            return count;
        }

        // The punches' switches (Fist, Hot Hands) as the unlocks are now.
        private static void SetUnlocks()
        {
            Fist.JabUnlocked = unlockedNow.Contains(Jab);
            Fist.SwingUnlocked = unlockedNow.Contains(Swing);
            Fist.UppercutUnlocked = unlockedNow.Contains(Uppercut);
            Fist.TriplePunch = unlockedNow.Contains(Triple);
            HotHands.SwollenHand = unlockedNow.Contains(Swollen);
        }

        // The news a game over inside a special stage kept back (Resume), and punches a point unlocked while the player
        // had no gloves (Apply Unlocks): told once the next run is under way and the player has been up for Next Run
        // News Delay (not dying, getting up or at 0 health; not in a stage or the opening), the punches only once a pair
        // of gloves is on. Only what's still unlocked then (Reset Progress may have locked it again).
        private void Update()
        {
            if (nextRunNews.Count == 0 || Instance != this) return;
            bool punches = Gloves.CanPunch;
            if (InSpecialStage || PlayerDown() || Game.Menus.OpeningCutscene.IsPlaying || !Checkpoints.HasResumed ||
                (!punches && nextRunNews.TrueForAll(IsPunch)))
            {
                upSince = -1f;
                return;
            }
            if (upSince < 0f) upSince = Time.time;
            if (Time.time - upSince < NextRunNewsDelay) return;
            upSince = -1f;
            // Each once (the same punch can be kept back twice: no gloves, then a game over in a stage).
            var news = new List<string>();
            foreach (var item in nextRunNews)
            {
                if (!news.Contains(item)) news.Add(item);
            }
            nextRunNews.Clear();
            foreach (var item in news)
            {
                if (IsPunch(item) && !punches)
                {
                    // Still no gloves: kept for when a pair is on.
                    nextRunNews.Add(item);
                }
                else if (StillUnlocked(item))
                {
                    PunchUnlocked?.Invoke(item);
                    Told(item);
                }
            }
        }

        // Glove news just shown: not again in this save slot (Show Once).
        private static void Told(string news)
        {
            if (gloveNews.Remove(news)) Game.Menus.ShowOnce.Remember(GloveNewsKey + news);
        }

        private static bool StillUnlocked(string name) => unlockedNow.Contains(name);

        // The player is down: the lucky coins in the air, getting up from a revive, or 0 health (dead).
        private static bool PlayerDown() =>
            Checkpoints.IsDying ||
            (PlayerRevive.Instance != null && PlayerRevive.Instance.IsReviving) ||
            (PlayerBoosts.TryGetHealth(out float health) && health <= 0f);

        /// <summary>Gives the player EXP, e.g. for a kill. Nothing happens without a Player Level in the scene.</summary>
        public static void AddExp(int amount)
        {
            if (Instance != null) Instance.GainExp(amount);
        }

        /// <summary>
        /// Gives the player EXP that can be part of a point, e.g. 0.25 for a tree pumpkin: the parts add up (and are
        /// saved), and every whole point counts like AddExp(1).
        /// </summary>
        public static void AddExp(float amount)
        {
            if (Instance != null) Instance.GainExpPart(amount);
        }

        /// <summary>
        /// The EXP this thing would give when it breaks or dies isn't given (nor its gold orbs): it went without the
        /// player earning it, e.g. the giant's hands, a Fall Edge, Live Limit making room. Only what the player does
        /// gives EXP.
        /// </summary>
        public static void NoRewardFor(GameObject thing)
        {
            if (thing == null) return;
            foreach (var reward in thing.GetComponentsInChildren<ExpWhenBroken>(true)) reward.noReward = true;
            foreach (var enemy in thing.GetComponentsInChildren<EnemyHitDamage>(true)) enemy.noReward = true;
            foreach (var fat in thing.GetComponentsInChildren<Game.Enemies.FatPumpkin>(true)) fat.noReward = true;
            // Not the player's hit either: no damage number when its health is emptied.
            DamageNumbers.Quiet(thing);
        }

        private void GainExpPart(float amount)
        {
            if (amount <= 0f || IsMaxLevel) return;

            expPart += amount;
            // A hair of slack so e.g. ten 0.1s still make a whole point.
            int whole = Mathf.FloorToInt(expPart + 0.0001f);
            expPart = Mathf.Max(0f, expPart - whole);
            if (expPart < 0.0001f) expPart = 0f;
            if (whole > 0) GainExp(whole);
            // Only a part: the save still has to keep it.
            else Changed?.Invoke();
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
            if (IsMaxLevel)
            {
                exp = 0;
                expPart = 0f;
            }

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
            return new SavedLevel { level = level, exp = exp, points = new List<int>(points), expPart = expPart, giftPoints = giftPoints };
        }

        /// <summary>The saved level, from the Save Manager when the save is read (null with no save: level 0).</summary>
        public void LoadSaveData(SavedLevel saved)
        {
            read = true;
            level = saved != null ? Mathf.Clamp(saved.level, 0, MaxLevel) : 0;
            exp = saved != null && !IsMaxLevel ? Mathf.Clamp(saved.exp, 0, ExpNeeded - 1) : 0;
            expPart = saved != null && !IsMaxLevel ? Mathf.Clamp(saved.expPart, 0f, 0.999f) : 0f;
            giftPoints = saved != null ? Mathf.Max(0, saved.giftPoints) : 0;
            int left = level * pointsPerLevel + giftPoints;
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
        private int giftPoints;

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
            // A new run drops the candy points, and ends any special stage's level-0 stats.
            Checkpoints.RunStarted += DropCandy;
            Checkpoints.RunStarted += EndSpecialStages;
            // Weapons the player picks up wear out slower with Weapon Efficiency.
            var origin = FindAnyObjectByType<XROrigin>(FindObjectsInactive.Include);
            if (origin != null) origin.GetComponentsInChildren(true, hands);
            foreach (var hand in hands) hand.selectEntered.AddListener(OnPickedUp);
        }

        private void OnDisable()
        {
            Checkpoints.RunStarted -= DropCandy;
            Checkpoints.RunStarted -= EndSpecialStages;
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
            if (args.interactableObject is not Component thing) return;
            WeaponWear.Watch(thing.gameObject);
            // Its throws and hits can explode (Explosive Objects, Bomber Man, Impact, Weapon Master; user, 2026-11-12).
            Game.Weapons.WeaponSkills.Watch(thing.gameObject);
        }

        // The stats that are set on something (the others are read when needed): the health cap, the walking speed and
        // the revives. Heal: a raised cap heals too. Give Revives: a new bonus revive is one more right now too.
        private void Apply(bool heal, bool giveRevives)
        {
            ApplyHealth(heal);
            ApplySpeed();
            // Always the real points, also in a special stage: lowering them would take the lucky coins for good.
            Checkpoints.SetLevelRevives(Points(Stat.ExtraRevive), giveRevives);
            // Raised by a point (giveRevives), a new punch or power is announced; read from the save, it's just unlocked.
            ApplyUnlocks(giveRevives);
        }

        // The health cap from Max Health (eaten candies' points included) and Extra Revive. Heal: a raised cap heals too.
        private void ApplyHealth(bool heal)
        {
            // A whole number: candy points come in halves and tenths, which made caps like 127.5 (play test 2026-11-10).
            float bonus = Worth(Stat.MaxHealth) * healthPerPoint + EffectPoints(Stat.ExtraRevive) * reviveHealthPerPoint;
            PlayerBoosts.SetLevelHealthBonus(Mathf.Floor(bonus + 0.5f), heal);
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
                // The Move Speed stat, and 30% slower while high on a boong (Player Buffs).
                float speed = pair.Value * SpeedScale * PlayerBuffs.SpeedScale;
                // While the player is slowed (a web), the slow gives the new speed back when it ends.
                if (slow != null && slow.IsSlowed) slow.SetNormalSpeed(pair.Key, speed);
                else pair.Key.moveSpeed = speed;
            }
        }
    }
}

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
        // Weapon Master (named from when it was Power; the name keeps the saved points in place).
        BonusPower,
    }

    /// <summary>
    /// The player's level. Killing enemies gives EXP (each enemy's Exp, on its Enemy Hit Damage or Ghost Balloon; FSMs
    /// can give some with the "Add Player Exp" action; breakables with Exp When Broken, e.g. a tree pumpkin's 0.25, give
    /// parts of a point that add up). Enough EXP is a level up (Exp To Next Level, one number per
    /// level; the last one is the highest level), and each level up gives Points Per Level points to put into the six
    /// stats in the Level Up menu:
    /// Max Health raises the health cap and makes eating candy heal more; Toughness cuts the damage enemies do and how
    /// far enemies push the player; Weapon Efficiency makes weapons hit harder and wear out slower; Fist Size unlocks the
    /// flying Jab at 1 point (Swing and Uppercut are locked for later: Lock Swing And Uppercut), and every point makes
    /// punches hit harder and flying punches bigger and fly farther (Triple Punch at 10), the hand itself only a little
    /// bigger; Throw Power makes throws leave the hand faster and far grabs pull faster (Gentle Far Grab), and from 1
    /// point thrown books, bottles and flip-flops explode (Grenade), more points making the blast reach farther and
    /// hurt more (at 0 they don't explode); Move Speed
    /// makes walking faster. What one point gives is set below; the last point that fills a stat up counts as Last Point
    /// Worth points (1 since 2026-11-10, so like any other; it was 7).
    /// Reaching a Bonus Level (10, 20, 21) also gives a bonus point for the three bonus stats (the menu's Bonus Stats
    /// panel): Explosive Fist (every punch that hits something explodes, harder with each point, and flying punches come
    /// quicker one after another; no bigger fists), Weapon Master (weapons held in the hand hit 30/40/50% harder, and a
    /// held thing that blocks a bite wears 20/30/40% less) and Extra Revive (one more revive every run, see Checkpoints:
    /// one more lucky coin to flip when the player goes down, see Revive Coins; and a higher health cap).
    /// A smoked boong adds Toughness points for a while (Player Buffs); those are never saved. Eaten candies add points
    /// for the run (Eat Candy Buff; user 2026-11-10: one candy kind per stat, see Candy Kinds), each its full share up to
    /// Candy Full Rate Points a stat (5), then only Candy Slow Points (0.1), at most Candy Max Points (10), every share
    /// bigger the more of their own points the player has in that stat (Candy Boost Per Point). Fist Size candy makes
    /// the fist bigger and the jab harder but never unlocks a punch. Never saved, gone when a new run starts.
    /// Inside a special stage (user, 2026-11-10: the nesting pumpkins, Paint Bees and Daruma; see Special Stage Stats)
    /// the player has level-0 stats: their own points and eaten candies count for nothing and the health cap drops to
    /// the level-0 one (Boss Seed raises kept), but the health itself stays as it is, the points stay theirs (menu, save)
    /// and boong buffs and lucky coins stay on. Everything comes back when the stage ends.
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

        [Header("Exploding throws (Throw Power)")]
        [Tooltip("At 0 Throw Power, thrown books, bottles and flip-flops (Grenade) don't explode: they just hit and break. " +
                 "The 1st point makes them explode as they always did; each point after it makes the blast reach this many " +
                 "metres farther (+0.72 m at 10 points).")]
        [Min(0f)]
        [SerializeField] private float blastRadiusPerPoint = 0.08f;
        [Tooltip("Throw Power: +1 blast damage every this many points after the 1st (5: +1 at 6 points, +2 at 11, which " +
                 "takes eaten candies' points on top of the 10).")]
        [Min(1)]
        [SerializeField] private int blastDamageStepPoints = 5;
        [Tooltip("Throw Power: every point after the 1st makes a throw need this share less speed to count as a hard throw " +
                 "(set off a book, flip-flop or bottle, burst a tree pumpkin, hit hard): 0.03 = 3% a point, never below Min " +
                 "Hard Throw Share. Throws also leave the hand faster with each point, so it adds up (user, 2026-10-27).")]
        [Range(0f, 0.1f)]
        [SerializeField] private float hardThrowCutPerPoint = 0.03f;
        [Range(0.1f, 1f)]
        [SerializeField] private float minHardThrowShare = 0.55f;
        [Tooltip("The farthest a thrown blast reaches with Throw Power, in metres (a blast that reaches farther by itself " +
                 "keeps its own reach).")]
        [Min(0.1f)]
        [SerializeField] private float maxBlastRadius = 2f;
        [Tooltip("The most damage a thrown blast does with Throw Power (a blast that does more by itself keeps its own).")]
        [Min(0f)]
        [SerializeField] private float maxBlastDamage = 5f;
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

        [Header("Flying punches unlocked by Fist Size")]
        [Tooltip("Fist Size points that unlock the Jab (a fast straight punch flies on). Below it no punch flies. " +
                 "0 = unlocked from the start.")]
        [Min(0)]
        [SerializeField] private int jabAtFistSize = 1;
        [Tooltip("Swing and Uppercut stay locked whatever the Fist Size (kept for later). Untick to unlock them at their " +
                 "Fist Size below again.")]
        [SerializeField] private bool lockSwingAndUppercut = true;
        [Tooltip("Fist Size points that unlock the Swing (a fast sideways hook of the fist).")]
        [Min(0)]
        [SerializeField] private int swingAtFistSize = 3;
        [Tooltip("Fist Size points that unlock the Uppercut (a fast drive of the fist upwards).")]
        [Min(0)]
        [SerializeField] private int uppercutAtFistSize = 3;
        [Tooltip("Fist Size points for the Triple Punch: a jab throws 3 fists side by side, a swing or an uppercut 3 in a " +
                 "row. Flying swings and uppercuts also reach their farthest here (they go farther step by step from the " +
                 "Jab's points); a flying jab flies as far as it is big (the Fist's Jab Size Reach).")]
        [Min(1)]
        [SerializeField] private int triplePunchAtFistSize = 10;
        [Tooltip("Fist Size points for Swollen Hand: a red-hot hand's fist (Hot Hands) and its flying punches swell bigger " +
                 "while it's hot (Swollen Size on Hot Hands).")]
        [Min(1)]
        [SerializeField] private int swollenHandAtFistSize = 6;

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
        /// A Fist Size point just unlocked a punch: "Jab", "Swing", "Uppercut" or "Triple", or "Swollen" (Swollen Hand; not
        /// when the save is read).
        /// </summary>
        public static event Action<string> PunchUnlocked;

        /// <summary>The first Throw Power point was just spent: thrown things explode from now on (not when the save is read).</summary>
        public static event Action ExplodingThrowsUnlocked;

        /// <summary>An eaten candy added points to this stat (Eat Candy Buff): the health HUD shows which, e.g. "+SPD".</summary>
        public static event Action<Stat> CandyEaten;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            Changed = null;
            LeveledUp = null;
            PunchUnlocked = null;
            ExplodingThrowsUnlocked = null;
            CandyEaten = null;
            lastRead = null;
            stageHolders.Clear();
            suspended = false;
            describing = false;
        }

        // The special stages on right now (Special Stage Stats); while any is, the stats are at level 0.
        private static readonly HashSet<UnityEngine.Object> stageHolders = new();
        // What the stage's start put away (see Suspend), given back when the last stage ends.
        private static bool suspended;
        private static bool hadJab, hadSwing, hadUppercut, hadTriple, hadSwollen, hadExplodingThrows;
        private static float levelHealthAtStart;
        // Max Health candy points at the stage's start: a candy eaten inside raises the cap on the way out without healing.
        private static float candyHealthAtStart;
        // The Level Up menu is asking what the points give (Describe): the real stats, also in a special stage.
        private static bool describing;

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
            hadJab = Fist.JabUnlocked;
            hadSwing = Fist.SwingUnlocked;
            hadUppercut = Fist.UppercutUnlocked;
            hadTriple = Fist.TriplePunch;
            hadSwollen = HotHands.SwollenHand;
            hadExplodingThrows = Points(Stat.ThrowPower) >= 1;
            levelHealthAtStart = PlayerBoosts.LevelHealthBonus;
            candyHealthAtStart = candyPoints[(int)Stat.MaxHealth];
            // Lowering the cap never takes health away (Player Boosts), and the revives aren't lowered (Checkpoints
            // would take the coins for good): Apply keeps them at the real points.
            Apply(false, false);
        }

        // Quiet: a game over inside the stage (End Special Stages), so the player is dead. The stats come back without a
        // heal (the restart fills the heart to the real cap anyway) and without a notice over the death (it would stop
        // the game): a punch or exploding throws a point spent inside unlocked just works from the new run on.
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
            Fist.JabUnlocked = hadJab;
            Fist.SwingUnlocked = hadSwing;
            Fist.UppercutUnlocked = hadUppercut;
            Fist.TriplePunch = hadTriple;
            HotHands.SwollenHand = hadSwollen;
            if (player == null) return;
            // The punches as they were before the stage, announced first: only one a point spent inside unlocked is new
            // (Apply's own quiet ApplyPunches then changes nothing; quiet, it sets them without a word).
            if (!quiet) player.ApplyPunches(true);
            player.Apply(false, false);
            if (quiet) return;
            // The cap comes back without healing, but a Max Health or Extra Revive point spent inside heals as it would
            // have then. A Max Health candy eaten inside doesn't: it healed when it was eaten (like outside a stage).
            float raised = PlayerBoosts.LevelHealthBonus - levelHealthAtStart -
                           (player.candyPoints[(int)Stat.MaxHealth] - candyHealthAtStart) * player.healthPerPoint;
            // (A point raises it by 10 or more; under 1 is only the cap's rounding to a whole number.)
            if (raised >= 1f && PlayerBoosts.TryGetHealth(out float health)) PlayerBoosts.SetHealth(health + raised);
            // The first Throw Power point spent inside: thrown things explode from now on (Raise kept the notice back).
            if (!hadExplodingThrows && player.Points(Stat.ThrowPower) >= 1) ExplodingThrowsUnlocked?.Invoke();
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

        // Whole points of a stat's worth (eaten candies give parts of a point).
        private static int Whole(float worth) => Mathf.FloorToInt(worth + 0.001f);

        /// <summary>How much faster throws leave the hand (Throw Power, at most Max Throw Scale): 1 = as always.</summary>
        public static float ThrowScale => Instance != null
            ? Mathf.Min(1f + Instance.Worth(Stat.ThrowPower) * Instance.throwPerPoint, Instance.maxThrowScale)
            : 1f;

        /// <summary>How much faster far grabs pull things to the hand (Throw Power): 1 = as always.</summary>
        public static float PullScale => Instance != null ? Instance.Gain(Stat.ThrowPower, Instance.pullPerPoint) : 1f;

        /// <summary>
        /// Whether thrown books, bottles and flip-flops explode (Grenade): from the first Throw Power point. Without a Player
        /// Level in the scene they always do.
        /// </summary>
        public static bool ExplodingThrows => Instance == null || Instance.EffectPoints(Stat.ThrowPower) >= 1;

        /// <summary>A thrown blast's reach with Throw Power, from its own: farther with every point after the first.</summary>
        public static float ThrowBlastRadius(float own) =>
            Instance != null ? Mathf.Min(own + Instance.BlastSteps * Instance.blastRadiusPerPoint, Mathf.Max(own, Instance.maxBlastRadius)) : own;

        /// <summary>A thrown blast's damage with Throw Power, from its own: more with every point after the first.</summary>
        public static float ThrowBlastDamage(float own) =>
            Instance != null ? Mathf.Min(own + Instance.BlastDamageBonus, Mathf.Max(own, Instance.maxBlastDamage)) : own;

        // Whole blast damage steps from Throw Power: +1 every Blast Damage Step Points after the 1st point.
        private int BlastDamageBonus => Mathf.FloorToInt((BlastSteps + 0.001f) / Mathf.Max(1, blastDamageStepPoints));

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

        /// <summary>The extra damage of weapons held in the hand (the Weapon Master bonus): 0 = as always.</summary>
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

        /// <summary>The share of a block's wear a held thing takes (the Weapon Master bonus): 1 = all of it.</summary>
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
        // points for now (Player Buffs: the Bone Boong's Toughness) and eaten candies' points (one candy kind for each of
        // the 6 stats, user 2026-11-10) come on top, never as that last point, and aren't saved. Candies' Fist Size
        // points make the fist and its flying punches bigger and the jab harder, but never unlock a punch (Unlock
        // Points). In a special stage only a boong's count.
        private float Worth(Stat stat) =>
            OwnWorth(stat) + PlayerBuffs.ExtraPoints(stat) + (!StatsDown ? candyPoints[(int)stat] : 0f);

        // Fist Size points that unlock punches: the player's own (and a boong's), never eaten candies' (they'd come and
        // go with every death).
        private float UnlockPoints => OwnWorth(Stat.FistSize) + PlayerBuffs.ExtraPoints(Stat.FistSize);

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
                case Stat.BonusPower: return "Weapon Master";
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
                case Stat.WeaponEfficiency: return $"+{WeaponDamageBonus} damage, -{Percent(1f - WeaponWearShare)} wear";
                case Stat.FistSize:
                    return Join($"+{Percent(FistSizePoints * punchSizePerPoint)} size", p > 0 ? $"flying jab +{FlyingJabBonus} damage" : "") + PunchNote();
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

        // The Weapon Master row: what this many points give, e.g. "+1 held weapon damage, -20% block wear".
        private string WeaponMasterNote(int points)
        {
            float Pick(float[] byPoint) => byPoint == null || byPoint.Length == 0 ? 0f : byPoint[Mathf.Clamp(points, 1, byPoint.Length) - 1];
            int bonus = weaponMasterBonus == null || weaponMasterBonus.Length == 0
                ? 0
                : weaponMasterBonus[Mathf.Clamp(points, 1, weaponMasterBonus.Length) - 1];
            return $"+{bonus} held weapon damage, -{Percent(Pick(weaponMasterBlockCut))} block wear";
        }

        // The Fist Size row also says which punch comes next: ", Jab at 1", ", Triple Punch at 10" (and ", Swing +
        // Uppercut at 3" when they aren't locked), and ", Triple Punch" once everything is unlocked.
        private string PunchNote()
        {
            float size = UnlockPoints;
            bool bigMoves = !lockSwingAndUppercut;
            int next = int.MaxValue;
            foreach (int at in new[] { jabAtFistSize, bigMoves ? swingAtFistSize : -1, bigMoves ? uppercutAtFistSize : -1, swollenHandAtFistSize, triplePunchAtFistSize })
            {
                if (at > size && at < next) next = at;
            }
            if (next == int.MaxValue) return ", Triple Punch";
            var names = new List<string>();
            if (jabAtFistSize == next) names.Add("Jab");
            if (bigMoves && swingAtFistSize == next) names.Add("Swing");
            if (bigMoves && uppercutAtFistSize == next) names.Add("Uppercut");
            if (swollenHandAtFistSize == next) names.Add("Swollen Hand");
            if (triplePunchAtFistSize == next) names.Add("Triple Punch");
            return $", {string.Join(" + ", names)} at {next}";
        }

        // The Throw Power row: "Throws explode at 1 point" at 0, then the throw and pull speed and how much bigger the
        // blasts are.
        private string ThrowNote()
        {
            int p = Points(Stat.ThrowPower);
            if (p == 0) return "Throws explode at 1 point";
            float throwShare = Mathf.Min(Worth(Stat.ThrowPower) * throwPerPoint, maxThrowScale - 1f);
            string speed = $"+{Percent(throwShare)} throw speed, +{Percent(PullScale - 1f)} pull";
            if (BlastSteps == 0) return speed + ", throws explode";
            return BlastDamageBonus > 0
                ? $"{speed}, blasts +{BlastDamageBonus} damage, +{BlastSteps * blastRadiusPerPoint:0.0} m"
                : $"{speed}, blasts +{BlastSteps * blastRadiusPerPoint:0.0} m";
        }

        // Fist Size points as they count for size and reach, eaten candies' included (the bonus stat is Explosive Fist
        // now: it gives no size). Unlocking punches counts only the player's own (Unlock Points).
        private float FistSizePoints => Worth(Stat.FistSize);

        // The flying punches unlock at their Fist Size (and lock again if the points go, e.g. Reset Progress). Announce:
        // a point just raised it (not the save being read), so the player is told about each new punch.
        private void ApplyPunches(bool announce)
        {
            float size = UnlockPoints;
            bool jab = size >= jabAtFistSize;
            // Kept for later: locked, they never unlock (and are never announced).
            bool swing = !lockSwingAndUppercut && size >= swingAtFistSize;
            bool uppercut = !lockSwingAndUppercut && size >= uppercutAtFistSize;
            bool triple = size >= triplePunchAtFistSize;
            bool swollen = size >= swollenHandAtFistSize;
            var unlocked = new List<string>();
            if (jab && !Fist.JabUnlocked) unlocked.Add("Jab");
            if (swing && !Fist.SwingUnlocked) unlocked.Add("Swing");
            if (uppercut && !Fist.UppercutUnlocked) unlocked.Add("Uppercut");
            if (swollen && !HotHands.SwollenHand) unlocked.Add("Swollen");
            if (triple && !Fist.TriplePunch) unlocked.Add("Triple");
            Fist.JabUnlocked = jab;
            Fist.SwingUnlocked = swing;
            Fist.UppercutUnlocked = uppercut;
            Fist.TriplePunch = triple;
            HotHands.SwollenHand = swollen;
            if (!announce) return;
            foreach (var punch in unlocked) PunchUnlocked?.Invoke(punch);
        }

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
            // The first Throw Power point: the player is told thrown things explode now (in a special stage, once it ends).
            if (stat == Stat.ThrowPower && points[(int)stat] == 1 && !InSpecialStage) ExplodingThrowsUnlocked?.Invoke();
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
            if (args.interactableObject is Component thing) WeaponWear.Watch(thing.gameObject);
        }

        // The stats that are set on something (the others are read when needed): the health cap, the walking speed and
        // the revives. Heal: a raised cap heals too. Give Revives: a new bonus revive is one more right now too.
        private void Apply(bool heal, bool giveRevives)
        {
            ApplyHealth(heal);
            ApplySpeed();
            // Always the real points, also in a special stage: lowering them would take the lucky coins for good.
            Checkpoints.SetLevelRevives(Points(Stat.ExtraRevive), giveRevives);
            // Raised by a point (giveRevives), a new punch is announced; read from the save, it's just unlocked.
            ApplyPunches(giveRevives);
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

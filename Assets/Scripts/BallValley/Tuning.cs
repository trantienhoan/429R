using UnityEngine;

namespace Balls
{
    /// <summary>All gameplay numbers in one place. Lives on the ValleyGame component so it can be tweaked live in the Inspector.</summary>
    // 429R: it lives on the Ball Valley Stage (Prefabs/BallValley/Ball_Valley). The numbers are BALLS' (a 200 x 100 m valley,
    // 16 players), changed for a VR valley of 50 x 25 m with 5 to 8 bots where a "429R:" comment says so.
    [System.Serializable]
    public class Tuning
    {
        [Header("Valley")]
        public float halfX = 25f;     // 429R: a 50 x 25 m valley (BALLS: 100 x 50 half sizes, 200 x 100 m)
        public float halfZ = 12.5f;
        // 429R: no Player Count: the stage's Bots Per Round says how many balls play each round.

        [Header("Round: shrinking valley")]
        [Tooltip("Seconds into a match before the valley first shrinks (phase 1, full size, lasts this long).")]
        public float shrinkDelay = 30f;   // 429R: 30 (BALLS 60)
        [Tooltip("Seconds from the first shrink to the last; the shrinks are evenly apart (60 + 240 with 6 phases: a new phase every minute, the valley at its smallest about 5 minutes in).")]
        public float shrinkDuration = 120f;   // 429R: 120 (BALLS 240): smallest about 2.5 minutes in
        [Tooltip("The valley stops shrinking at this fraction of its full size (0.15 = 30 x 15 m instead of 200 x 100). The six holes keep their size, so the smaller this is, the closer they crowd together (their pits would overlap below about 0.07).")]
        [Range(0.07f, 1f)] public float minMapScale = 0.35f;   // 429R: 0.35 = 17.5 x 8.75 m (BALLS 0.15); the VR holes are relatively bigger: their pits would overlap below about 0.16
        [Tooltip("How many sizes the valley has: phase 1 is full size, the last one Min Map Scale, evenly apart. It shrinks smoothly from one phase to the next on the shrink schedule; a new match grows it back.")]
        [Range(2, 12)] public int phases = 5;   // 429R: 5 (BALLS 6)
        [Tooltip("Seconds the valley takes to shrink from one phase to the next: the lawn, the border and the six holes move in together. Trees and boulders the border or the holes reach crumble.")]
        public float phaseShrinkSeconds = 4f;
        [Tooltip("How far the lawn reaches out under the border rocks (m; the rocks are 3.6 m thick), so no dark chasm shows between them and the lawn or through their joints. Never in front of a hole.")]
        public float lawnUnderBorder = 3f;
        [Tooltip("Once the valley is at its smallest: the Moment of Truth. Players keep their revives, but anyone pocketed drops straight back in, until one ball is left: the winner.")]
        [UnityEngine.Serialization.FormerlySerializedAs("suddenDeath")]
        public bool momentOfTruth = true;
        [Tooltip("In the Moment of Truth nobody waits outside: a pocketed player (or bot) with a revive left drops straight back in after this many seconds.")]
        public float momentOfTruthReviveSeconds = 1f;
        [Tooltip("Off: the border stays up in the Moment of Truth. On: it crumbles away as the Moment of Truth starts (only the lawn and the six holes are left, so balls can roll off any edge); it's back for the next match.")]
        public bool momentOfTruthCrumble = false;
        [Tooltip("Seconds the valley takes to grow back to full size when a new round starts.")]
        public float regrowDuration = 3f;

        [Header("Round: revives")]
        [Tooltip("How many times each player can drop back in after being pocketed, per round. Out of revives = spectator.")]
        public int revives = 2;   // 429R: 2 (BALLS 3)
        [Tooltip("When a player rolls into a hole by themself (nobody pushed them), every OTHER player gets this many revives (spectators come back).")]
        public int selfPocketGift = 1;
        [Tooltip("Also give the player who fell in by themself the gift (then falling in by yourself costs no revive).")]
        public bool selfPocketGiftsFaller = false;
        [Tooltip("No more gifts once the valley is smaller than this: by then the closing holes swallow balls nobody pushed, and gifts would keep everyone alive forever.")]
        [Range(0f, 1f)] public float selfPocketGiftMinScale = 0.6f;   // 429R: 0.6 (BALLS 0.35): gifts only in the first two phases
        [Tooltip("Points for being the last ball standing.")]
        public float pointsPerRoundWin = 200f;
        [Tooltip("With nobody to vote (bots only, start screen): seconds on the Glory Board before the replay starts by itself.")]
        public float roundRestartDelay = 6f;
        // 429R: no replay vote and no music (BALLS' Replay Vote Delay and Music numbers).

        [Header("Bots")]
        [Tooltip("Each bot gets a temper between Min and Max. 0 = grazes and rarely fights; 1 = hunts nonstop, from far away, and fires risky shots.")]
        // 429R: the stage sets these each round from its Aggression Per Round (round 1: 0.45 - 0.6; BALLS 0.75 - 1).
        [Range(0f, 1f)] public float botAggressionMin = 0.45f;
        [Range(0f, 1f)] public float botAggressionMax = 0.6f;

        [Header("Trees and boulders")]
        [Tooltip("Hit points of a boulder (bigger boulders have more). A ball does about (speed - 3) x sqrt(weight) per crash, a special move twice that.")]
        public float rockHealth = 600f;
        [Tooltip("Hit points of a tree.")]
        public float treeHealth = 400f;
        [Tooltip("Berries a broken boulder / tree bursts into.")]
        public int rockFood = 8;
        public int treeFood = 5;

        [Header("Caves")]
        [Tooltip("The gap in the border at a corner hole (m, along each edge about 0.72 of it). Holes always keep their size.")]
        public float caveCornerWidth = 5f;   // 429R: 5 (BALLS 9): the corner hole models are sized to it (pit radius 2.56 m)
        [Tooltip("The gap in the border at a middle hole (m).")]
        public float caveMiddleWidth = 4.5f;   // 429R: 4.5 (BALLS 8): pit radius 2.36 m
        [Tooltip("Sizes the code-built pits (used only if the map model is missing) and the editor's hole guides.")]
        public float caveMaxWidth = 10f;   // 429R: 10 (BALLS 18)

        [Header("Ball size")]
        [Tooltip("Radius of ball B1 (size S1). You start as this one ball.")]
        public float headRadius = 0.8f;
        [Tooltip("Each new ball is this much bigger than the previous one: S2 = 1.1 S1, S3 = 1.2 S1 ...")]
        public float sizeStep = 0.1f;
        [Tooltip("No ball grows past this radius (reached at ball #29 with the defaults). Must stay smaller than half the cave width so big balls still fit in caves.")]
        public float headMaxRadius = 2f;   // 429R: 2 (BALLS 3): under half of both cave widths and the pits' radius
        [Tooltip("Mass of a size-S1 main ball; mass grows with size squared.")]
        public float baseMass = 1f;
        [Tooltip("Extra mass (= hitting power) per tail ball. No cap: more balls = more force.")]
        public float massPerBaby = 0.08f;

        [Header("Movement")]
        public float baseAccel = 14f;
        public float baseTopSpeed = 9f;
        public float topSpeedPerSqrtBaby = 0.06f;
        [Tooltip("Bigger balls roll faster: top speed (and push) at the biggest size is this much higher (0.7 = 70% faster).")]
        public float topSpeedPerSize = 0.7f;
        [Tooltip("Sideways grip when small (high = turns sharply).")]
        public float gripSmall = 3f;
        [Tooltip("Sideways grip at max size (low = drifty, hard to stop).")]
        public float gripBig = 0.6f;
        [Tooltip("Rolling slowdown when small (high = stops quickly).")]
        public float dampingSmall = 0.9f;
        [Tooltip("Rolling slowdown at max size (low = keeps rolling, hard to stop).")]
        public float dampingBig = 0.25f;
        [Tooltip("Extra gravity (x normal) so balls stay on the table like snooker balls.")]
        public float extraGravity = 1f;
        // 429R: no mouse or touch steering (BALLS' Mouse / Touch Full Speed Range).

        [Header("Special move")]
        public float chargeTime = 1.2f;
        public float dashSpeed = 24f;
        [Tooltip("How far your own special-move shot rolls, as a multiple of what Dash Speed alone gives (2 = twice as far). The launch speed is raised to match.")]
        public float launchDistance = 2f;
        public float dashPerSqrtBaby = 0.06f;
        public float dashCooldown = 4f;
        public float dashDuration = 0.8f;
        [Tooltip("How much smoke a charging or launched ball kicks up (1 = the full amount). The puffs are big and see-through, so they're costly to draw on slow devices; phones use at most half.")]
        [Range(0f, 1f)] public float smokeAmount = 0.5f;   // 429R: Quest, as BALLS' phones (BALLS 0.75)
        [Tooltip("Rolling slowdown while launched, so the shot keeps its power until it hits something.")]
        public float dashDamping = 0.25f;
        [Tooltip("Backspin on a full-power shot, as a share of natural roll (1 = as much backspin as a rolling ball has topspin); weaker shots get less. The shot slides and its backspin wears off as it travels: close and hard = it screws back after a full hit, middle distance = it stops dead (stun), far or soft = it follows through.")]
        public float shotScrew = 1f;
        [Tooltip("How far (m) a shot travels while its spin wears off by one natural roll: full backspin stuns at this distance and rolls naturally at twice it.")]
        public float spinRollDistance = 10f;
        [Tooltip("After hitting a ball: how much of the speed it lost the striker's spin turns into drawing back (backspin) or following through (roll), along the way it was going. Also what bends a cut's path forward or back.")]
        [Range(0f, 1f)] public float spinTransfer = 0.35f;
        [Tooltip("Cap on how fast a ball can be sent flying by a special-move hit.")]
        public float maxHitSpeed = 60f;
        [Tooltip("After a launch (or being knocked) you have NO control until your ball rolls slower than this.")]
        public float shotRecoverSpeed = 5f;
        [Tooltip("Safety cap: the shooter gets control back after this many seconds even if still rolling.")]
        public float shotMaxLock = 7f;
        [Tooltip("Minimum rolling slowdown while your own shot rolls out.")]
        public float shotDamping = 0.6f;
        [Tooltip("Extra braking (m/s^2) on your own ball after the launch burst, so you recover quickly.")]
        public float shotFriction = 5f;

        [Tooltip("429R gate 3: a knocked ball's braking (m/s^2) while it rolls out; below 0 = Shot Friction (BALLS). Lower = " +
                 "a punched ball rolls further (a real snooker shot) before its bot steers it again.")]
        public float knockFriction = -1f;
        [Tooltip("429R gate 3: a knocked ball is back in its bot's control below this speed; below 0 = Shot Recover Speed (BALLS).")]
        public float knockRecoverSpeed = -1f;

        [Header("Knock (ball hit by a special move)")]
        // (A struck ball takes the push like a real snooker ball - into an equal ball on a full hit, nearly all your
        // speed; your swallowed tail's weight makes it more - carries the shot's burst on, then brakes like a shot.)
        [Tooltip("A main ball hit at least this hard by a knocked/launched ball gets knocked too (chain reactions).")]
        public float knockChainSpeed = 8f;

        [Header("Ball collisions")]
        [Tooltip("Bounciness of ball-on-ball hits (snooker balls ~0.9).")]
        public float ballBounce = 0.92f;
        [Tooltip("Bounciness of balls off walls, rocks and trees. Those bounces have no friction, so a ball always comes off at the mirror angle: bank shots can be planned (the aim guide shows the first bounce).")]
        [Range(0f, 1f)] public float rimBounce = 0.8f;

        [Header("Tail")]
        public float tailSpring = 40f;
        public float tailDamping = 9f;
        public float tailMaxAccel = 160f;
        [Tooltip("Mass of a size-S1 tail ball (tail balls are lighter than the main ball so they fly like pinballs).")]
        public float babyMass = 0.35f;

        [Header("Food")]
        [Tooltip("Extra tail balls you spawn with (0 = start as a single ball B1).")]
        public int startBabies = 0;
        [Tooltip("Food needed for the next ball = baseCost + tailBalls * costPerBaby.")]
        public float baseCost = 3f;
        public float costPerBaby = 1f;
        public int pelletValue = 1;
        [Tooltip("Food value of a knocked-off size-S1 ball; bigger balls are worth proportionally more.")]
        public int looseBallValue = 3;
        public int maxPellets = 12;   // 429R: 12 x Food Boost = 30 berries (BALLS 70: 175)
        public float pelletSpawnInterval = 0.4f;
        [Tooltip("More food: berries on the field = Max Pellets x this, and they reappear this many times faster. Plenty of food lets everyone grow big enough to shove each other into the caves.")]
        public float foodBoost = 2.5f;
        [Tooltip("As the valley shrinks, the berry limit shrinks with the field's area (never below Min Berries), so the small arena isn't carpeted with berries. Off = the same limit at every size.")]
        public bool berriesFollowField = true;
        public int minBerries = 6;   // 429R: 6 (BALLS 8)
        public int rockCount = 6;   // 429R: 6 (BALLS 40)
        public int treeCount = 5;   // 429R: 5 (BALLS 30)

        [Header("Grass (slime food)")]
        [Tooltip("Most grass tufts on the field at once.")]
        public int maxGrass = 12;   // 429R: 12 (BALLS 80)
        [Tooltip("Seconds between new tufts popping up.")]
        public float grassSpawnInterval = 1f;
        [Tooltip("Height of a grass tuft (m).")]
        public float grassHeight = 1.2f;

        [Header("Slimes (living food: come out of rocks, graze, run from balls)")]
        [Tooltip("Share of the field's rocks that are slime rocks (cracked, with a hole slimes squeeze out of).")]
        [Range(0f, 1f)] public float slimeRockShare = 0.3f;
        [Tooltip("Most slimes in the valley at once.")]
        public int slimeCount = 4;   // 429R: 4 (BALLS 16)
        [Tooltip("Seconds between slimes squeezing out of a rock.")]
        public float slimeSpawnInterval = 3f;
        [Tooltip("Most slimes at once when the valley is at its smallest: it goes from Slime Count to this as the valley shrinks.")]
        public int slimeCountSmallest = 6;   // 429R: 6 (BALLS 24)
        [Tooltip("Seconds between new slimes at the smallest size (from Slime Spawn Interval at full size). With no slime rocks left, slimes climb up over the field's edge.")]
        public float slimeSpawnIntervalSmallest = 0.8f;
        [Tooltip("Slimes a tree or boulder lets out when the border or a hole swallows it (instead of berries). Smashed by a ball, it still bursts into berries.")]
        public int swallowedPropSlimes = 1;
        [Tooltip("Radius of a newborn slime (m): twice a berry, so you can spot them (they grow up to Slime Max Size times this).")]
        public float slimeRadius = 0.6f;
        public float slimeMass = 0.15f;
        [Tooltip("Sideways speed of each hop while grazing (fat slimes hop slower).")]
        public float slimeHopSpeed = 5f;
        [Tooltip("Seconds between hops while grazing or wandering (random between these).")]
        public Vector2 slimeHopInterval = new Vector2(0.3f, 0.55f);
        [Tooltip("Upward speed of each hop.")]
        public float slimeHopUp = 3f;
        [Tooltip("Slimes spot grass within this distance.")]
        public float slimeSightRadius = 50f;
        [Tooltip("Size gained per tuft eaten (1 = newborn).")]
        public float slimeGrowPerGrass = 0.12f;
        public float slimeMaxSize = 1.8f;
        [Tooltip("Slimes run from any main ball closer than this (plus the ball's radius).")]
        public float slimeFleeRadius = 13f;
        [Tooltip("Sideways speed of each hop while running away (a small ball tops out around 9, so you can catch them).")]
        public float slimeFleeHopSpeed = 7.5f;
        [Tooltip("Food a newborn slime is worth when a ball eats it (a berry is 1).")]
        public int slimeFood = 3;
        [Tooltip("Extra food for every tuft the slime ate.")]
        public int slimeFoodPerGrass = 2;

        [Header("Rainbow Slime (a long reign ends in a hole: catch it to be invincible)")]
        [Tooltip("A ball that's been King of the Valley this many seconds in a row and then falls into a cave lets a Rainbow Slime jump out of that cave (one at a time).")]
        public float rainbowKingSeconds = 55f;
        [Tooltip("Seconds the Rainbow Slime stays if nobody catches it; then it hops back into the nearest cave.")]
        public float rainbowSlimeLife = 25f;
        [Tooltip("Hop speed while running, times a scared slime's (Slime Flee Hop Speed). It never stops to eat.")]
        public float rainbowSpeed = 1.7f;
        [Tooltip("It notices balls this many times farther away than a slime does (Slime Flee Radius).")]
        public float rainbowFleeRange = 2f;
        [Tooltip("Food it's worth, on top of the power.")]
        public int rainbowFood = 6;
        [Tooltip("Catching it: seconds of invincibility. Crashes cost no balls, no hit knocks you away; only a cave can still get you.")]
        public float rainbowInvincibleSeconds = 7f;
        [Tooltip("While invincible your main ball weighs this many times as much: it pushes other balls about half as hard again and hardly budges when hit.")]
        public float rainbowWeight = 4f;

        [Header("Points (leaderboard)")]
        [Tooltip("Points for each food eaten (a berry is 1 food).")]
        public float pointsPerFood = 1f;
        [Tooltip("Points for pocketing another player...")]
        public float pointsPerKill = 50f;
        [Tooltip("...plus this many for each of their balls.")]
        public float pointsPerVictimBall = 10f;
        [Tooltip("Points per second while you are King of the Valley (the most balls).")]
        public float kingPointsPerSecond = 5f;

        [Header("Damage")]
        [Tooltip("Damage needed to knock one baby ball off.")]
        public float hpPerBall = 6f;
        [Tooltip("Only crashing into walls, boulders and trees costs balls (balls never hurt each other). Slower than this does nothing.")]
        public float hazardSafeSpeed = 6f;
        [Tooltip("Damage per m/s above the safe speed (hpPerBall damage = one ball lost).")]
        public float hazardDamage = 0.7f;
        [Tooltip("How much your weight adds to a crash: damage x (mass / baseMass) ^ this. 0 = speed only, 1 = full momentum.")]
        [Range(0f, 1f)] public float hazardMassWeight = 0.5f;
        public float spawnShield = 3f;
        [Tooltip("Seconds of invincibility (no crash damage) right after releasing the special move.")]
        public float shotShield = 0.7f;
        [Tooltip("Seconds after a touch during which a pocketing still counts as that player's kill.")]
        public float killCreditWindow = 6f;

        [Header("429R: VR")]
        [Tooltip("Most baby balls one ball can have (BALLS: no limit). Food past that only scores points.")]
        public int maxBabies = 8;
        [Tooltip("Balls drop into the valley from this high (m; BALLS: 26).")]
        public float spawnDropHeight = 10f;
        [Tooltip("Most loose (white) balls lying about at once; the oldest goes first (BALLS: 400, phones 150).")]
        public int maxLooseBalls = 40;

        /// <summary>429R: BALLS' map size, and its corner / middle cave widths, that its models and layout numbers were made for.</summary>
        public const float BallsHalfX = 100f, BallsHalfZ = 50f, BallsCornerWidth = 9f, BallsMiddleWidth = 8f;

        /// <summary>
        /// 429R: BALLS' layout distances (insets from the edge, prop spacing) are for its 200 x 100 m valley; times this they
        /// fit this one (0.25 for 50 x 25 m). Ball sizes and speeds are not scaled.
        /// </summary>
        public float LayoutScale => Mathf.Clamp(halfX / BallsHalfX, 0.05f, 1f);

        /// <summary>429R: the corner hole model, and the room BALLS keeps from its holes, sized to Cave Corner Width (BALLS: 9 m).</summary>
        public float HoleScale => Mathf.Clamp(caveCornerWidth / BallsCornerWidth, 0.1f, 2f);

        /// <summary>429R: the middle hole model sized to Cave Middle Width (BALLS: 8 m).</summary>
        public float MiddleHoleScale => Mathf.Clamp(caveMiddleWidth / BallsMiddleWidth, 0.1f, 2f);

        /// <summary>429R: the map model (mountains, ground ring) stretched to this valley: x and z from Half X / Half Z, y their average.</summary>
        public Vector3 ArtScale
        {
            get
            {
                float x = Mathf.Max(0.01f, halfX / BallsHalfX), z = Mathf.Max(0.01f, halfZ / BallsHalfZ);
                return new Vector3(x, (x + z) * 0.5f, z);
            }
        }
    }
}

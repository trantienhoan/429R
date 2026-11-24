using System.Collections;
using System.Collections.Generic;
using Game.Bonus;
using Game.Enemies;
using Game.Gates;
using Game.Locomotion;
using Game.PlayGround;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

namespace Game.Bowling
{
    /// <summary>
    /// Gate 2, "Bowl and Fight" (2026-11-08; on the shared gate flow, Gate Run, since 2026-11-23). The player stands on a
    /// small dirt island in the middle of an X of 4 long lanes. Gate Run does what every gate does (the card, the start
    /// chest and the 3 round chests, the rounds' time limits, the HUD line, the hand-off to the finale); this plays the
    /// bowling (Gate Run calls it through <see cref="IGateGame"/>):
    /// - the start chest's seed: vines grow bowling pumpkins and a Giant Flying Slime comes down from the sky;
    /// - a slime flies to a lane (it lights up) and poops a 6-pin rack of slime pins; the pins grow fat and hatch small
    ///   flying slimes (fight them). A strike counts for that rack's slime; Round Strikes strikes hurt it (they add up);
    ///   6 racks without being hurt and it rains divers on the player instead (that also counts as its hurt);
    /// - round 1: one slime (3 strikes), hurt it splits in two; round 2: the two (2 strikes each), each splits; round 3:
    ///   four small slimes, one strike pops each. Every chest seed grows more vines (more balls);
    /// - the finale: the giant pumpkin (Gate Run's Finale Boss, through Gate Boss Finale) drops on a lane's far end, bowls
    ///   itself down the lane at the player, stops dizzy at the dirt's edge, leaps to another lane; punch it or bowl at it.
    ///   Broken: its candies on the dirt, then Gate Exit (The_Seed, the rooftop).
    /// While it runs (until the giant pumpkin): thumbstick and arm-swing moving are off, a fall goes back to the centre, the
    /// fog is lifted. Made by GAMESTAGES (Path 2 ON) under Currently Spawned Stuffs at [LevelManager]. Built by
    /// Tools > 429 Game > Bowling > Set Up Bowling Arena; Gate 2: Use Gate Run sets its Gate Run up.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BowlingStage : MonoBehaviour, IGateGame
    {
        [Header("Arena")]
        [Tooltip("The 4 lanes (lane 0 is in front of the player at the start).")]
        [SerializeField] private BowlingLane[] lanes = System.Array.Empty<BowlingLane>();
        [Tooltip("The middle of the dirt (a fall puts the player back here). Its X/Z run along the lanes (the X frame).")]
        [SerializeField] private Transform centre;
        [Tooltip("Half the dirt's side (metres): chests, the seed and candies are kept this far from the centre, less a little.")]
        [SerializeField] private float padHalfSize = 2.5f;
        [Tooltip("Grows the bowling pumpkins on the vine tips.")]
        [SerializeField] private BowlingBallSupply balls;
        [Tooltip("The vines that pop out of the dirt with each chest seed.")]
        [SerializeField] private VineGrow vines;
        [Tooltip("Where the pin sets go (the arena's \"Pins\" child).")]
        [SerializeField] private Transform pinsRoot;

        [Header("Prefabs")]
        [Tooltip("The Giant Flying Slime (Prefabs/Bowling/Giant_Flying_Slime).")]
        [SerializeField] private GiantFlyingSlime slimePrefab;
        [Tooltip("One slime pin (Prefabs/Bowling/Slime_Pin).")]
        [SerializeField] private SlimePin pinPrefab;
        [Tooltip("What a full-grown pin hatches into (Prefabs/Bowling/Slime_Pin_Flyer).")]
        [SerializeField] private GameObject flyerPrefab;
        [Tooltip("The divers a slime rains after 6 racks unhurt (Prefabs/Enemies/Daruma_Bomber).")]
        [SerializeField] private GameObject diverPrefab;
        [Tooltip("The puff (Smoke_Flash_Small_Puff).")]
        [SerializeField] private GameObject puff;
        [Tooltip("Only without a Gate Run (a test): what the giant pumpkin leaves (Gate Run's Finale Seed otherwise).")]
        [SerializeField] private GameObject seedPrefab;

        [Header("Where Gate Run drops the chests (on the dirt)")]
        [Tooltip("This far in front of the head (metres), kept on the dirt, clear of balls and open sprouts.")]
        [SerializeField] private float chestAhead = 1.2f;
        [Tooltip("Kept this close to the centre (X frame, metres).")]
        [SerializeField] private float chestKeep = 1.9f;
        [Tooltip("Kept this far from balls and open sprouts (metres).")]
        [SerializeField] private float chestClear = 0.5f;

        [Header("Rounds (1, 2, 3)")]
        [Tooltip("Strikes on one slime's racks that hurt it (they add up): one slime, then two, then four small ones.")]
        [SerializeField] private int[] roundStrikes = { 3, 2, 1 };
        [Tooltip("The slimes' root scale.")]
        [SerializeField] private float[] sizes = { 7f, 5.5f, 4.5f };
        [Tooltip("Fly speed (metres a second).")]
        [SerializeField] private float[] speeds = { 6f, 6.5f, 7f };
        [Tooltip("Least seconds between one slime's poops (2026-11-23: was 12/14/18; round 1 then had a rack every 16 s).")]
        [SerializeField] private float[] poopEvery = { 8f, 12f, 16f };
        [Tooltip("Seconds a pin takes from landing to full fat (then it hatches).")]
        [SerializeField] private float[] growSeconds = { 24f, 22f, 20f };
        [Tooltip("At most this many of the stage's flyers alive.")]
        [SerializeField] private int[] flyersCap = { 5, 8, 8 };
        [Tooltip("Wing loop pitch.")]
        [SerializeField] private float[] pitches = { 0.55f, 0.65f, 0.75f };
        [Tooltip("Wing loop volume.")]
        [SerializeField] private float[] volumes = { 0.8f, 0.65f, 0.5f };
        [Tooltip("Colours, round after round: 1 (round 1), 2 (round 2), 4 (round 3). Slime i splits into 2i and 2i+1.")]
        [SerializeField] private Color[] colours =
        {
            new(0.45f, 0.85f, 0.25f),
            new(0.89f, 0.15f, 0.18f), new(0.2f, 0.45f, 1f),
            new(0.89f, 0.15f, 0.18f), new(1f, 0.85f, 0.12f), new(0.2f, 0.45f, 1f), new(0.45f, 0.85f, 0.25f),
        };

        [Header("Strike / miss sounds")]
        [Tooltip("Played at the player when a rack goes down in one ball (a strike). Empty: none.")]
        [SerializeField] private AudioClip strikeSound;
        [Range(0f, 1f)]
        [SerializeField] private float strikeVolume = 0.9f;
        [Tooltip("Played at the player when a ball knocks some of a rack but not all (a miss). Empty: none.")]
        [SerializeField] private AudioClip missSound;
        [Range(0f, 1f)]
        [SerializeField] private float missVolume = 0.6f;

        [Header("Rules")]
        [Tooltip("Strikes count up and a miss doesn't reset them (user, 2026-11-18: \"make it simple\"). Off: in a row.")]
        [SerializeField] private bool strikesAddUp = true;
        [Tooltip("Racks a slime poops without being hurt; the next time it rains divers instead (and that counts as its hurt).")]
        [Min(1)]
        [SerializeField] private int poopsBeforeDive = 6;
        [Tooltip("Divers it rains, and the seconds between them.")]
        [Min(0)]
        [SerializeField] private int divers = 6;
        [SerializeField] private float diverGap = 0.3f;
        [Tooltip("A bowl is steered to a lane whose direction is this close (degrees).")]
        [SerializeField] private float aimAngle = 25f;
        [Tooltip("A bowl whose line to the head pin crosses the lane's mouth closer than this to the floor's edge (metres; " +
                 "the ball is 0.16 m round, the rails stand on the edge; 0.4 on a 2 m lane = 0.6 m from its middle) would roll " +
                 "off the dirt beside the lane: it's steered into the lane instead, at a point on its middle line Mouth Lead " +
                 "metres past the mouth (or past the ball). The lane's width is its Floor's (hand set up, 2026-11-23).")]
        [Min(0f)] [SerializeField] private float mouthMargin = 0.4f;
        [Min(0.5f)] [SerializeField] private float mouthLead = 1.0f;
        [Tooltip("Seconds a pin flies from the slime to its slot, and between pins.")]
        [SerializeField] private float pinFlightSeconds = 0.7f;
        [SerializeField] private float pinStagger = 0.1f;
        [Tooltip("A pin shivers its last this many seconds before it hatches.")]
        [SerializeField] private float shiverLast = 4f;

        [Header("Slime flight")]
        [Tooltip("The circle over the head: radius and height (metres; each slime after the first a little wider and higher).")]
        [SerializeField] private float circleRadius = 3f;
        [SerializeField] private float circleHeight = 5f;
        [Tooltip("Seconds for one lap.")]
        [SerializeField] private float lapSeconds = 5f;
        [Tooltip("The first slime comes from this high, this far out along lane 0 (metres).")]
        [SerializeField] private float entranceHeight = 30f;
        [SerializeField] private float entranceAhead = 8f;
        [Tooltip("Seconds of its descent, and laps it flies over the head before its first poop.")]
        [SerializeField] private float entranceSeconds = 5f;
        [Min(0)]
        [SerializeField] private int entranceLaps = 0;
        [Tooltip("Seconds after the start chest's seed before the slime comes down (the balls grow meanwhile).")]
        [SerializeField] private float slimeDelay = 1f;
        [Tooltip("Seconds after the start chest's seed before the balls start growing.")]
        [SerializeField] private float ballsDelay = 0.6f;
        [Tooltip("A slime with nothing to do this long goes back to circling the head.")]
        [SerializeField] private float idleReturnSeconds = 4f;
        [Tooltip("Strain seconds: before a rack, before the divers.")]
        [SerializeField] private float poopStrain = 0.6f;
        [SerializeField] private float diveStrain = 0.8f;

        [Header("Hurt")]
        [Tooltip("Seconds from the bonk to the split (rounds 1 and 2) or the pop (round 3).")]
        [SerializeField] private float splitDelay = 0.4f;
        [Tooltip("The two halves grow from this share over these seconds, flung apart this fast (m/s).")]
        [SerializeField] private float splitGrowFrom = 0.6f;
        [SerializeField] private float splitGrowSeconds = 0.5f;
        [SerializeField] private float splitFling = 2f;

        [Header("Look")]
        [Tooltip("While the stage runs, the fog starts and ends this far (metres).")]
        [SerializeField] private Vector2 fog = new(14f, 38f);
        [Tooltip("While the stage runs the fog takes this colour (the house's is near black: a dark band at the horizon); " +
                 "given back with the fog.")]
        [SerializeField] private bool tintFog = true;
        [SerializeField] private Color fogColour = new(0.16f, 0.1f, 0.25f);

        [Header("The giant pumpkin (Gate Run's Finale Boss)")]
        [Tooltip("Seconds after the finale starts before it drops onto a lane's far end.")]
        [Min(0f)] [SerializeField] private float finaleDelay = 1.5f;
        [Tooltip("Its size here (metres): the lanes are 2 m wide.")]
        [Min(0.5f)] [SerializeField] private float finaleRadius = 1.3f;
        [Tooltip("Its health here (the prefab's own if 0).")]
        [Min(0f)] [SerializeField] private float finaleHealth = 150f;   // user, 2026-11-23: 150, as the other gates' (was 100)
        [Tooltip("Bowl at the giant pumpkin: a bowling ball and it meeting at least Ball Boss Speed (metres a second; it " +
                 "rolled into it, or it ran over a ball in its way) hurts it by Ball Boss Damage (twice while it's dizzy), on " +
                 "top of what the hit itself does (a ball let go less than 2.5 s ago counts as the player's throw: 1, 2 " +
                 "dizzy). Play test 2026-11-23: a ball alone did 2 to its 100 health.")]
        [Min(0f)] [SerializeField] private float ballBossSpeed = 3f;
        [Min(0f)] [SerializeField] private float ballBossDamage = 4f;

        [Tooltip("Only without a Gate Run (a test): the giant pumpkin (Gate Run's Finale Boss otherwise).")]
        [SerializeField] private GameObject finaleBoss;

        [Header("Hand set up")]
        [Tooltip("On: the arena is set up by hand in Unity (the 4 lanes are one Lane prefab, Prefabs/Bowling/Bowling_Lane; " +
                 "Tools > 429 Game > Bowling > Hand Set Up). The old builder menus (Set Up Bowling Arena, Build / Remove " +
                 "SlimeBowl Kit) then refuse to touch the arena, so hand work is never overwritten.")]
        [SerializeField] private bool handSetUp;

        [Header("Hooks")]
        [Tooltip("A lane lit (its index).")]
        [SerializeField] private UnityEvent<int> onLaneLit = new();
        [Tooltip("A round started (1..3, 4 = the giant pumpkin).")]
        [SerializeField] private UnityEvent<int> onPhase = new();
        [Tooltip("The stage won (the giant pumpkin broke).")]
        [SerializeField] private UnityEvent onWon = new();

        /// <summary>The stage being played (only one at a time).</summary>
        public static BowlingStage Running { get; private set; }

        /// <summary>Raised when it's over: won (the player is on the rooftop), or not (cleared away, e.g. Game Over).</summary>
        public static event System.Action<BowlingStage, bool> Ended;

        /// <summary>0 before round 1, 1..3 the rounds, 4 the giant pumpkin, 5 won.</summary>
        public int Phase { get; private set; }

        public bool Won => won;
        public IReadOnlyList<BowlingLane> Lanes => lanes;

        /// <summary>Set up by hand (the builder menus leave it alone).</summary>
        public bool HandSetUp => handSetUp;

        /// <summary>The giant slimes flying now (in their order: circle places, Debug Hit).</summary>
        public IReadOnlyList<GiantFlyingSlime> Slimes => slimeList;

        // One giant slime and what the stage knows of it.
        private sealed class Flyer
        {
            public GiantFlyingSlime slime;
            public int gen;              // the round it belongs to (1..3)
            public int slot;             // its place in that round (colour, what it splits into)
            public int streak, poops;
            public int lastLane = -1;
            public readonly int[] uses = new int[4];
            public float nextPoopAt, retryAt, idleSince;
            public bool hurt;            // hurt this round (a split or a pop coming)
            public bool acting;          // the stage has it busy (the entrance, a rack, the divers, a split)
            public bool circling;
            public int targetLane = -1;  // the lane it's flying to or pooping on
            public int ticket;           // goes up on a hurt: its jobs stop
            public Coroutine job;
        }

        private GateRun run;
        private XROrigin origin;
        private Transform head;
        private bool started, restored, won, endedSent;
        private readonly Dictionary<ContinuousMoveProvider, bool> moveWas = new();
        private readonly List<ContinuousMoveProvider> movers = new();
        private float nextMoveCheck;
        private bool lockMoves;
        private System.Func<Vector3, Vector3, (Vector3 point, float steer)?> laneAim;
        private System.Action<Rigidbody, Collision, Vector3> ballHit;
        private Vector3 returnSpot;
        private bool returnSet;
        private float fogStartWas, fogEndWas;
        private Color fogColourWas;
        private bool fogSet;
        private int strikes;

        private readonly List<Flyer> flyers = new();
        private readonly List<GiantFlyingSlime> slimeList = new();
        private readonly List<PinSet> sets = new();
        private int seedsEaten;
        private bool ballsBegun, entranceStarted, roundOver;
        private int roundDone;
        private Coroutine playJob;
        private readonly GiantFlyingSlime[] counterOwner = new GiantFlyingSlime[4];
        private readonly int[] counterStreak = { -1, -1, -1, -1 };

        // The player's health (PlayerHealth FSM), for "knocked down".
        private HutongGames.PlayMaker.FsmFloat playerHealth;

        private void Start()
        {
            if (Running != null && Running != this) { Destroy(gameObject); return; }
            Running = this;
            started = true;
            EnemyWave.Start(this);
            laneAim = FindLaneAim;
            BowlingRollAssist.LaneAim = laneAim;

            origin = FindAnyObjectByType<XROrigin>();
            head = origin != null && origin.Camera != null ? origin.Camera.transform : (Camera.main != null ? Camera.main.transform : null);
            MovementOff();
            lockMoves = true;
            nextMoveCheck = Time.unscaledTime + 0.5f;
            returnSpot = centre != null ? centre.position : transform.position;
            returnSet = true;
            FallOutOfWorld.ReturnSpot = returnSpot;
            FallOutOfWorld.ReturnYaw = lanes.Length > 0 && lanes[0] != null ? Quaternion.LookRotation(lanes[0].Direction).eulerAngles.y : transform.eulerAngles.y + 45f;
            Fog(true);

            // All lanes dark (floors and rails still show), no counters. The vines hide themselves.
            foreach (var lane in lanes)
            {
                if (lane == null) continue;
                lane.SetLit(false, false);
                lane.SetCounter(null);
            }
            Phase = 0;
            run = GetComponent<GateRun>();
            if (run != null) run.chestSpot = ChestSpot;
            else
            {
                Debug.LogWarning("[Bowling] No Gate Run on the arena (Tools > 429 Game > Bowling > Gate 2: Use Gate Run): " +
                                 "the rounds run on their own, without the card, the chests or the HUD line.", this);
                playJob = StartCoroutine(OnItsOwn());
            }
        }

        private void Update()
        {
            if (!started || restored) return;
            // The pause menu or a fall may switch a mover back on (not in the finale: they may walk then).
            if (lockMoves && Time.unscaledTime >= nextMoveCheck)
            {
                nextMoveCheck = Time.unscaledTime + 0.5f;
                MovementOff();
            }
            if (won) return;
            Prune();
            var floor = HeadFloor();
            foreach (var f in flyers) if (f.slime != null) f.slime.SetCircleCentre(floor);
            if (Phase >= 1 && Phase <= 3 && !PlayerDown())
            {
                // Think never adds or removes slimes (it only starts their jobs).
                for (int i = 0; i < flyers.Count; i++) Think(flyers[i]);
            }
            UpdateCounters();
        }

        private void OnDestroy()
        {
            // Cleared away (Game Over), or Gate Exit is done with it (the player is on the rooftop): give everything back.
            // Never moves the player (Game Over or Gate Exit has them).
            Restore();
            if (started && !endedSent)
            {
                endedSent = true;
                Ended?.Invoke(this, won);
            }
            if (Running == this) Running = null;
        }

        // ------------------------------------------------------------------ Gate Run (IGateGame)

        /// <summary>The start chest's seed is eaten (or its wait ran out): the first vines and balls grow and the Giant
        /// Flying Slime comes down.</summary>
        public void GateStart(GateRun gateRun)
        {
            if (gateRun != null) run = gateRun;
            if (restored || won) return;
            GrowMore();
            StartEntrance();
        }

        /// <summary>Round 1, 2 or 3: that round's slimes (1, 2, 4) start pooping; the HUD counts its strikes. From round 2 on
        /// the last chest's seed is in (or its wait ran out): more vines, more balls.</summary>
        public void RoundBegin(GateRun gateRun, int round)
        {
            if (gateRun != null) run = gateRun;
            if (restored || won) return;
            if (round >= 2) GrowMore();
            StartEntrance();
            Phase = Mathf.Clamp(round, 1, 3);
            roundOver = false;
            roundDone = 0;
            EnsureSlimes(Phase);
            int i = 0;
            foreach (var f in flyers)
            {
                if (f.gen != Phase) continue;
                f.hurt = false;
                f.streak = 0;
                f.poops = 0;
                f.lastLane = -1;
                System.Array.Clear(f.uses, 0, f.uses.Length);
                f.nextPoopAt = Time.time + 1f + 1.5f * i;
                f.retryAt = 0f;
                f.idleSince = Time.time;
                i++;
            }
            if (run != null)
            {
                run.Goal("STRIKES", Need(Phase) * SlimesIn(Phase));
                run.Progress(0);
            }
            onPhase?.Invoke(Phase);
            Debug.Log("[Bowling] Round " + Phase + ": " + i + " slimes, " + Need(Phase) + " strikes each.", this);
        }

        /// <summary>The round's time ran out: its slimes still flying are hurt anyway (split or popped), so the next round
        /// has its slimes; every rack melts.</summary>
        public void RoundTimeUp(GateRun gateRun, int round)
        {
            if (restored || won || round != Phase) return;
            Debug.Log("[Bowling] Round " + round + " ran out of time: its slimes are hurt anyway.", this);
            foreach (var f in flyers.ToArray())
                if (f.gen == Phase && !f.hurt) Hit(f, "time up");
            MeltAll();
        }

        /// <summary>Round 3's chest seed is in: the giant pumpkin.</summary>
        public void FinaleBegin(GateRun gateRun)
        {
            if (gateRun != null) run = gateRun;
            if (restored || won || Phase >= 4) return;
            PumpkinFinale();
        }

        // Without a Gate Run (an old arena, a test): the rounds on their time limits, then the finale.
        private IEnumerator OnItsOwn()
        {
            yield return Wait(1f);
            GateStart(null);
            for (int r = 1; r <= 3; r++)
            {
                yield return Wait(r == 1 ? 0.5f : 3f);
                RoundBegin(null, r);
                for (float until = Time.time + 150f; !roundOver && Time.time < until;) yield return null;
                if (!roundOver) RoundTimeUp(null, r);
            }
            FinaleBegin(null);
        }

        private static readonly int[] SlimesPerRound = { 1, 2, 4 };
        private static int SlimesIn(int round) => SlimesPerRound[Mathf.Clamp(round, 1, 3) - 1];
        private int Need(int round) => Mathf.Max(1, Pick(roundStrikes, round, 1));

        // ------------------------------------------------------------------ the game

        private void StartEntrance()
        {
            if (entranceStarted) return;
            entranceStarted = true;
            if (slimePrefab == null)
            {
                Debug.LogError("[Bowling] No Giant Flying Slime prefab: the stage can't go on.", this);
                return;
            }
            playJob = StartCoroutine(Entrance());
        }

        // The round-1 slime is made at once (high over lane 0, far above the fog), comes down after Slime Delay (louder
        // and louder), laps the head Entrance Laps times, then goes to its first lane.
        private IEnumerator Entrance()
        {
            var c = CentrePoint();
            var dir = lanes.Length > 0 && lanes[0] != null ? lanes[0].Direction : transform.forward;
            var from = c + dir * entranceAhead + Vector3.up * entranceHeight;
            var look = -dir;
            look.y = 0f;
            var slime = Instantiate(slimePrefab, from, Quaternion.LookRotation(look.sqrMagnitude > 0.0001f ? look : Vector3.forward), transform);
            slime.Setup(ColourOf(1, 0), Pick(sizes, 1, 7f), Pick(pitches, 1, 0.55f), Pick(volumes, 1, 0.8f));
            var f = AddFlyer(slime, 1, 0);
            f.acting = true;
            Debug.Log("[Bowling] The Giant Flying Slime comes.", this);
            if (slimeDelay > 0f) yield return Wait(slimeDelay);
            if (slime == null || f.ticket != 0) yield break;

            // Its circle over the head, phased so it comes in on its own side (the circle's phase runs with time).
            var feet = HeadFloor();
            var side = from - feet;
            float startDegrees = (side.x * side.x + side.z * side.z > 0.0001f ? Mathf.Atan2(side.x, side.z) * Mathf.Rad2Deg : 0f) -
                                 360f * Time.time / Mathf.Max(0.1f, lapSeconds);
            slime.SetCircleCentre(feet);
            slime.Circle(feet, circleRadius, circleHeight, lapSeconds, startDegrees);
            f.circling = true;
            bool arrived = false;
            slime.Arrive(from, entranceSeconds, () => arrived = true);
            float giveUp = Time.time + entranceSeconds + 4f;
            while (!arrived && Time.time < giveUp && slime != null && f.ticket == 0) yield return null;
            if (slime == null || f.ticket != 0) yield break;
            for (float until = Time.time + entranceLaps * lapSeconds; Time.time < until;)
            {
                if (slime == null || f.ticket != 0) yield break;
                yield return null;
            }
            f.acting = false;
            f.nextPoopAt = Time.time;
            f.idleSince = Time.time;
        }

        // The round's slimes are all there (1, 2, 4): any missing (gone without a split, a test) is made over the head.
        private void EnsureSlimes(int round)
        {
            if (slimePrefab == null) return;
            int n = SlimesIn(round);
            var have = new bool[n];
            foreach (var f in flyers)
                if (f.slime != null && f.gen == round && f.slot >= 0 && f.slot < n) have[f.slot] = true;
            for (int slot = 0; slot < n; slot++)
            {
                if (have[slot]) continue;
                var slime = Instantiate(slimePrefab, CirclePoint(slot, n), Quaternion.identity, transform);
                slime.Setup(ColourOf(round, slot), Pick(sizes, round, 5f), Pick(pitches, round, 0.65f), Pick(volumes, round, 0.65f));
                slime.GrowIn(splitGrowFrom, splitGrowSeconds, Vector3.zero);
                AddFlyer(slime, round, slot);
                Debug.Log("[Bowling] Round " + round + " was missing slime " + slot + ": made over the head.", this);
            }
            RefreshSlimes();
            Respace();
        }

        // One slime's turn (every frame while it's free): poop on a free lane, rain divers, or circle.
        private void Think(Flyer f)
        {
            if (f.slime == null || f.acting || f.hurt || f.gen != Phase) return;
            float now = Time.time;
            if (now < f.nextPoopAt || now < f.retryAt)
            {
                if (!f.circling && now - f.idleSince > idleReturnSeconds) Circle(f);
                return;
            }
            if (f.poops >= poopsBeforeDive)
            {
                f.job = StartCoroutine(DiveJob(f));
                return;
            }
            int k = PickLane(f);
            if (k < 0)
            {
                if (!f.circling) Circle(f);
                f.retryAt = now + 1f;
                return;
            }
            f.job = StartCoroutine(PoopJob(f, k));
        }

        // A free lane that isn't its last one: the one it used least this round, then the one nearest the player's facing.
        private int PickLane(Flyer f)
        {
            int best = -1, bestUses = int.MaxValue;
            float bestAngle = float.MaxValue;
            var forward = HeadForward();
            for (int k = 0; k < lanes.Length && k < 4; k++)
            {
                if (lanes[k] == null || k == f.lastLane || !LaneFree(k, f)) continue;
                int uses = f.uses[k];
                float angle = Vector3.Angle(lanes[k].Direction, forward);
                if (uses < bestUses || (uses == bestUses && angle < bestAngle))
                {
                    best = k;
                    bestUses = uses;
                    bestAngle = angle;
                }
            }
            return best;
        }

        // No live rack on it and no other slime heading there.
        private bool LaneFree(int k, Flyer who)
        {
            if (LiveSet(k) != null) return false;
            foreach (var f in flyers)
                if (f != who && f.slime != null && f.acting && f.targetLane == k) return false;
            return true;
        }

        // Light the lane, fly over its deck, strain, poop a rack of 6.
        private IEnumerator PoopJob(Flyer f, int k)
        {
            f.acting = true;
            f.targetLane = k;
            f.circling = false;
            int ticket = f.ticket;
            var s = f.slime;
            if (!lanes[k].Lit) LightLane(k);

            bool arrived = false;
            s.LookAt(CentrePoint());
            s.FlyTo(HoverPoint(k, s.Size), Pick(speeds, f.gen, 6f), () => arrived = true);
            float giveUp = Time.time + 20f;
            while (!arrived && Time.time < giveUp)
            {
                if (Lost(f, ticket)) yield break;
                yield return null;
            }
            if (Lost(f, ticket)) yield break;

            bool strained = false;
            s.Strain(poopStrain, () => strained = true);
            giveUp = Time.time + poopStrain + 2f;
            while (!strained && Time.time < giveUp)
            {
                if (Lost(f, ticket)) yield break;
                yield return null;
            }
            if (Lost(f, ticket)) yield break;

            SpawnSet(f, k);
            f.acting = false;
            f.targetLane = -1;
            f.idleSince = Time.time;
            f.job = null;
        }

        private bool Lost(Flyer f, int ticket) => f.slime == null || f.ticket != ticket || won || restored;

        // A rack of 6 from the slime's bottom to the lane's slots.
        private void SpawnSet(Flyer f, int k)
        {
            if (pinPrefab == null)
            {
                Debug.LogError("[Bowling] No Slime Pin prefab: no rack.", this);
                return;
            }
            var lane = lanes[k];
            var growth = new PinGrowth
            {
                seconds = Pick(growSeconds, Phase, 24f),
                shiverLast = shiverLast,
                flyerPrefab = flyerPrefab,
                waveOwner = this,
                maxFlyersAlive = Pick(flyersCap, Phase, 5),
                flyerParent = Spawned(),
            };
            var set = PinSet.Spawn(pinPrefab, pinsRoot != null ? pinsRoot : transform, f.slime.BottomPoint, lane.SlotPositions(),
                                   lane.transform.position, lane.Direction, f.slime.Colour, growth, balls, pinFlightSeconds, pinStagger,
                                   lane.HalfWidth);
            if (set == null)
            {
                Debug.LogWarning("[Bowling] No rack made on lane " + k + ".", this);
                return;
            }
            set.Owner = f.slime;
            set.Lane = k;
            set.OnResolved += OnSetResolved;
            set.OnCleared += OnSetCleared;
            sets.Add(set);
            f.poops++;
            f.lastLane = k;
            if (k < f.uses.Length) f.uses[k]++;
            f.nextPoopAt = Time.time + Pick(poopEvery, Phase, 12f);
        }

        // The 7th poop: to its circle place over the head, strain, the divers; that's its hurt.
        private IEnumerator DiveJob(Flyer f)
        {
            f.acting = true;
            f.circling = false;
            int ticket = f.ticket;
            var s = f.slime;
            int index = Mathf.Max(0, flyers.IndexOf(f));
            Debug.Log("[Bowling] " + s.name + " pooped " + f.poops + " racks unhurt: divers.", this);

            bool arrived = false;
            if (head != null) s.LookAt(head.position);
            s.FlyTo(CirclePoint(index, CircleCount()), Pick(speeds, f.gen, 6f), () => arrived = true);
            float giveUp = Time.time + 15f;
            while (!arrived && Time.time < giveUp)
            {
                if (Lost(f, ticket)) yield break;
                yield return null;
            }
            if (Lost(f, ticket)) yield break;

            bool strained = false;
            s.Strain(diveStrain, () => strained = true);
            giveUp = Time.time + diveStrain + 2f;
            while (!strained && Time.time < giveUp)
            {
                if (Lost(f, ticket)) yield break;
                yield return null;
            }
            for (int i = 0; i < divers; i++)
            {
                if (Lost(f, ticket)) yield break;
                SpawnDiver(f);
                yield return new WaitForSeconds(diverGap);
            }
            if (Lost(f, ticket)) yield break;
            f.acting = false;
            f.job = null;
            Hit(f, "its divers");
        }

        // One diver out of the slime's bottom, flung down and a little out. It always comes (it's the slime's hurt).
        private void SpawnDiver(Flyer f)
        {
            if (diverPrefab == null) return;
            var go = Instantiate(diverPrefab, f.slime.BottomPoint, Quaternion.identity, Spawned());
            var flat = Random.insideUnitCircle;
            if (flat.sqrMagnitude < 0.0001f) flat = Vector2.right;
            flat.Normalize();
            if (go.TryGetComponent(out SlimeBomber bomber)) bomber.Launch(f.slime.Colour, Vector3.down * 3f + new Vector3(flat.x, 0f, flat.y));
            EnemyWave.Add(this, go);
            EnemyLimit.TryTakeSlot();
        }

        private void OnSetResolved(PinSet set, bool strike)
        {
            if (set == null || won || restored) return;
            // Strike / miss sounds (user, 2026-11-23): at the player, so they're heard from 18 m away; a rack left alone
            // (no ball reached it) stays quiet.
            if (strike) PlayAtPlayer(strikeSound, strikeVolume);
            else if (set.Knocked) PlayAtPlayer(missSound, missVolume);
            var owner = Find(set.Owner as GiantFlyingSlime);
            if (strike)
            {
                strikes++;
                if (owner == null || owner.hurt || owner.gen != Phase) return;
                owner.streak++;
                roundDone++;
                ReportProgress();
                Debug.Log("[Bowling] Strike on lane " + set.Lane + ": " + owner.slime.name + " " + owner.streak + "/" + Need(owner.gen) + ".", this);
                if (owner.streak >= Need(owner.gen)) Hit(owner, Need(owner.gen) + " strikes");
            }
            else if (owner != null && !strikesAddUp && !owner.hurt && owner.gen == Phase)
            {
                roundDone -= owner.streak;
                owner.streak = 0;
                ReportProgress();
            }
        }

        private void OnSetCleared(PinSet set)
        {
            if (set == null) return;
            set.OnResolved -= OnSetResolved;
            set.OnCleared -= OnSetCleared;
            sets.Remove(set);
        }

        private static void PlayAtPlayer(AudioClip clip, float volume)
        {
            if (clip == null || volume <= 0f) return;
            var cam = Camera.main;
            var at = cam != null ? cam.transform.position + cam.transform.forward * 0.4f : Vector3.zero;
            AudioSource.PlayClipAtPoint(clip, at, volume);
        }

        private void ReportProgress()
        {
            if (run != null) run.Progress(Mathf.Max(0, roundDone));
        }

        // A slime is hurt: bonk, its racks melt; rounds 1 and 2 it splits in two (they wait for the next round), round 3 it
        // pops. Hurt any other way than by strikes (divers, time up), its missing strikes count on the HUD. When every slime
        // of the round is hurt, the round is won (Gate Run drops its chest).
        private void Hit(Flyer f, string why)
        {
            if (f == null || f.slime == null || f.hurt || won || restored || f.gen != Phase) return;
            int need = Need(f.gen);
            if (f.streak < need)
            {
                roundDone += need - f.streak;
                ReportProgress();
            }
            f.hurt = true;
            f.ticket++;
            if (f.job != null) StopCoroutine(f.job);
            f.job = null;
            f.targetLane = -1;
            Debug.Log("[Bowling] " + f.slime.name + " hurt (" + why + ") in round " + Phase + ".", this);
            f.slime.Bonk(head != null ? head.position : CentrePoint());
            MeltSetsOf(f.slime);
            f.streak = 0;
            f.poops = 0;
            f.acting = true;
            StartCoroutine(f.gen < 3 ? Split(f) : Pop(f));
            CheckRoundOver();
        }

        private IEnumerator Split(Flyer f)
        {
            yield return new WaitForSeconds(splitDelay);
            var s = f.slime;
            flyers.Remove(f);
            if (s == null || won || restored)
            {
                RefreshSlimes();
                yield break;
            }
            var at = s.transform.position;
            var turn = s.transform.rotation;
            var side = s.transform.right;
            side.y = 0f;
            side = side.sqrMagnitude > 0.0001f ? side.normalized : Vector3.right;
            float off = 0.6f * s.Size * 0.26f;
            int next = f.gen + 1;
            s.Burst();

            var made = new List<Flyer>();
            for (int c = 0; c < 2; c++)
            {
                float sign = c == 0 ? -1f : 1f;
                int slot = f.slot * 2 + c;
                var child = Instantiate(slimePrefab, at + side * (sign * off), turn, transform);
                child.Setup(ColourOf(next, slot), Pick(sizes, next, 5.5f), Pick(pitches, next, 0.65f), Pick(volumes, next, 0.65f));
                child.GrowIn(splitGrowFrom, splitGrowSeconds, side * (sign * splitFling));
                var record = AddFlyer(child, next, slot);
                record.acting = true;
                made.Add(record);
            }
            Debug.Log("[Bowling] " + s.name + " split in two (round " + next + " slimes).", this);
            yield return new WaitForSeconds(splitGrowSeconds + 0.05f);
            foreach (var record in made)
            {
                if (record.slime == null) continue;
                record.acting = false;
            }
            Respace();
        }

        // Round 3: a small slime pops (its burst, gone).
        private IEnumerator Pop(Flyer f)
        {
            yield return new WaitForSeconds(splitDelay);
            flyers.Remove(f);
            if (f.slime != null) f.slime.Burst();
            RefreshSlimes();
            Respace();
            CheckRoundOver();
        }

        // Every slime of the round hurt (or gone): the round is won, its racks melt, Gate Run drops its chest.
        private void CheckRoundOver()
        {
            if (roundOver || won || restored || Phase < 1 || Phase > 3) return;
            foreach (var f in flyers)
                if (f.slime != null && f.gen == Phase && !f.hurt) return;
            roundOver = true;
            MeltAll();
            Debug.Log("[Bowling] Round " + Phase + " won.", this);
            if (run != null) run.RoundWon();
        }

        // ------------------------------------------------------------------ the giant pumpkin

        private PumpkinBallBoss finalePumpkin;
        private GateBossFinale finale;
        private int finaleLane = -1;
        private int finaleCandies;

        /// <summary>The giant pumpkin, once it's in (Play tests).</summary>
        public PumpkinBallBoss FinalePumpkin => finalePumpkin;

        // The slimes fly off, every rack melts, the last vines grow, the thumbstick comes back (dodge its charges), and the
        // giant pumpkin drops onto a lane's far end (Gate Run's Finale: Gate Boss Finale, then Gate Exit). The arena rides
        // with the finale's object: Gate Exit takes that out of Currently Spawned Stuffs (GAMESTAGES' clean-up must not take
        // the floor from under the player) and removes it once the player is on the rooftop.
        private void PumpkinFinale()
        {
            Phase = 4;
            onPhase?.Invoke(4);
            if (playJob != null) StopCoroutine(playJob);
            playJob = null;
            MeltAll();
            foreach (var f in flyers)
            {
                f.ticket++;
                if (f.job != null) StopCoroutine(f.job);
                if (f.slime != null) f.slime.Leave();
            }
            flyers.Clear();
            RefreshSlimes();
            EnemyWave.KillAll(this);
            GrowMore();
            lockMoves = false;
            MovementBack();
            finaleLane = PickFinaleLane(-1);
            var landAt = LaneEnd(finaleLane);
            finale = run != null
                ? run.Finale(landAt, finaleDelay, SetUpFinaleBoss, _ => SeedSpot())
                : GateBossFinale.Begin(finaleBoss, seedPrefab, landAt, finaleDelay, SetUpFinaleBoss, _ => SeedSpot(), "[Bowling]");
            if (finale != null) transform.SetParent(finale.transform, true);
            Debug.Log("[Bowling] The giant pumpkin comes to lane " + finaleLane + ".", this);
        }

        // Gate Boss Finale's set-up: the lanes are its arena (it charges down them at the dirt and leaps between them).
        private void SetUpFinaleBoss(PumpkinBallBoss boss)
        {
            finalePumpkin = boss;
            boss.Resize(finaleRadius);
            if (finaleHealth > 0f) boss.health = finaleHealth;
            // From a lane its candies would fly past the fall edges: it breaks without them and the stage drops them on the
            // dirt (the same number).
            finaleCandies = boss.breakCandies;
            boss.breakCandies = 0;
            boss.arenaCentre = LaneEnd(finaleLane);
            boss.canBeAt = OnALane;
            boss.chargeAlong = TowardsTheDirt;
            boss.hopTo = () =>
            {
                finaleLane = PickFinaleLane(finaleLane);
                var end = LaneEnd(finaleLane);
                boss.arenaCentre = end;
                return end;
            };
            boss.Broken += OnFinaleBroken;
            ballHit ??= OnBallHit;
            BowlingRollAssist.BallHit = ballHit;
            Debug.Log("[Bowling] The giant pumpkin drops onto lane " + finaleLane + ".", this);
        }

        // A bowling ball and the giant pumpkin ran into each other at least Ball Boss Speed (the ball rolled into it, or it
        // ran over a ball lying in its way): Ball Boss Damage (Special Hit: twice while it's dizzy, and nothing while it's in
        // the air), once per ball in a short while.
        private void OnBallHit(Rigidbody ball, Collision collision, Vector3 before)
        {
            if (finalePumpkin == null || finalePumpkin.IsBroken || collision == null || collision.collider == null || ball == null) return;
            if (collision.collider.GetComponentInParent<PumpkinBallBoss>() != finalePumpkin) return;
            var flat = before;
            flat.y = 0f;
            if (Mathf.Max(flat.magnitude, collision.relativeVelocity.magnitude) < ballBossSpeed) return;
            if (lastBallHit.TryGetValue(ball, out float at) && Time.time - at < 0.5f) return;
            lastBallHit[ball] = Time.time;
            finalePumpkin.SpecialHit(ballBossDamage);
        }

        private readonly Dictionary<Rigidbody, float> lastBallHit = new();

        private void OnFinaleBroken(PumpkinBallBoss boss)
        {
            var candies = boss != null ? boss.candies : null;
            if (finaleCandies > 0 && candies != null && candies.Length > 0)
                CandyFountain.Play(CandySpot(), finaleCandies, candies, 1.2f, new Vector2(3.5f, 5f), new Vector2(0.4f, 1.4f));
            Win();
        }

        // On a lane's middle line, all of it off the dirt, short of the lane's end.
        private bool OnALane(Vector3 p) => LaneOf(p, out _);

        private bool LaneOf(Vector3 p, out Vector3 dir)
        {
            dir = Vector3.zero;
            var flat = p - CentrePoint();
            flat.y = 0f;
            float r = finalePumpkin != null ? finalePumpkin.radius : finaleRadius;
            foreach (var lane in lanes)
            {
                if (lane == null) continue;
                var d = lane.Direction;
                float along = Vector3.Dot(flat, d);
                float across = Mathf.Abs(Vector3.Dot(flat, Vector3.Cross(Vector3.up, d)));
                // The lane's own floor (hand set up): from its start (+ the pumpkin's size) to just short of its end.
                if (across > 0.25f || along < lane.StartAlong + r + 0.1f || along > lane.EndAlong - 0.3f) continue;
                dir = d;
                return true;
            }
            return false;
        }

        // A charge goes down its lane, at the dirt.
        private Vector3 TowardsTheDirt(Vector3 p) => LaneOf(p, out var dir) ? -dir : Vector3.zero;

        // The far end of lane k, on its floor.
        private Vector3 LaneEnd(int k)
        {
            var c = CentrePoint();
            bool ok = k >= 0 && k < lanes.Length && lanes[k] != null;
            var dir = ok ? lanes[k].Direction : Vector3.forward;
            return c + dir * ((ok ? lanes[k].EndAlong : BowlingLayout.LaneEnd) - 1.5f);
        }

        private int PickFinaleLane(int not)
        {
            var ok = new List<int>();
            for (int k = 0; k < lanes.Length; k++) if (lanes[k] != null && k != not) ok.Add(k);
            return ok.Count > 0 ? ok[Random.Range(0, ok.Count)] : 0;
        }

        // ------------------------------------------------------------------ slimes, racks, lanes

        private Flyer AddFlyer(GiantFlyingSlime slime, int gen, int slot)
        {
            var f = new Flyer { slime = slime, gen = gen, slot = slot, idleSince = Time.time };
            slime.name = "Giant Flying Slime " + gen + "." + slot;
            if (slime.WingsLeft < 2)
                Debug.LogError("[Bowling] " + slime.name + " has " + slime.WingsLeft + " wings (wingLeft/wingRight unset or inactive " +
                               "on Giant_Flying_Slime.prefab): run Set Up Giant Slime And Showdown.", slime);
            flyers.Add(f);
            RefreshSlimes();
            return f;
        }

        private Flyer Find(GiantFlyingSlime slime)
        {
            if (slime == null) return null;
            foreach (var f in flyers) if (f.slime == slime) return f;
            return null;
        }

        private void RefreshSlimes()
        {
            slimeList.Clear();
            foreach (var f in flyers) if (f.slime != null) slimeList.Add(f.slime);
        }

        // Slimes gone by themselves and racks done are forgotten.
        private void Prune()
        {
            bool gone = flyers.RemoveAll(f => f.slime == null) > 0;
            sets.RemoveAll(s => s == null || s.Done);
            if (!gone) return;
            RefreshSlimes();
            CheckRoundOver();
        }

        // Circles the head in its place (each a little wider, higher and further round than the one before).
        private void Circle(Flyer f)
        {
            if (f.slime == null) return;
            int n = CircleCount();
            int i = Mathf.Max(0, flyers.IndexOf(f));
            f.slime.Circle(HeadFloor(), circleRadius + 0.4f * i, circleHeight + 0.5f * i, lapSeconds, 360f / Mathf.Max(1, n) * i);
            f.circling = true;
            f.idleSince = Time.time;
        }

        // Every circling slime takes its new place (after a split or a pop).
        private void Respace()
        {
            foreach (var f in flyers)
                if (f.slime != null && !f.acting && (f.circling || f.hurt || f.gen != Phase)) Circle(f);
        }

        private int CircleCount() => Mathf.Max(1, flyers.Count);

        private Vector3 CirclePoint(int i, int n)
        {
            var dir = Quaternion.Euler(0f, 360f / Mathf.Max(1, n) * i, 0f) * Vector3.forward;
            return HeadFloor() + dir * (circleRadius + 0.4f * i) + Vector3.up * (circleHeight + 0.5f * i);
        }

        // Over lane k's deck, at the height for a slime this size.
        private Vector3 HoverPoint(int k, float size)
        {
            var lane = lanes[k];
            var p = lane.Hover != null ? lane.Hover.position : lane.transform.position + lane.Direction * BowlingLayout.DeckAt;
            p.y = CentrePoint().y + BowlingLayout.HoverHeight(size);
            return p;
        }

        private void LightLane(int k)
        {
            lanes[k].SetLit(true, true);
            onLaneLit?.Invoke(k);
            Debug.Log("[Bowling] Lane " + k + " lit.", this);
        }

        /// <summary>A lane's live rack (not done), or null.</summary>
        private PinSet LiveSet(int k)
        {
            foreach (var s in sets) if (s != null && !s.Done && s.Lane == k) return s;
            return null;
        }

        private void MeltSetsOf(GiantFlyingSlime slime)
        {
            foreach (var s in sets.ToArray())
                if (s != null && !s.Done && s.Owner == (object)slime) s.Melt();
        }

        private void MeltAll()
        {
            foreach (var s in sets.ToArray()) if (s != null && !s.Done) s.Melt();
        }

        // Each lane's counter: its rack owner's strikes out of what hurts it, in its colour; hidden with no rack.
        private void UpdateCounters()
        {
            for (int k = 0; k < lanes.Length && k < counterOwner.Length; k++)
            {
                if (lanes[k] == null) continue;
                var set = LiveSet(k);
                var owner = set != null ? Find(set.Owner as GiantFlyingSlime) : null;
                var slime = owner != null ? owner.slime : null;
                int streak = owner != null ? owner.streak : -1;
                if (slime == counterOwner[k] && streak == counterStreak[k]) continue;
                counterOwner[k] = slime;
                counterStreak[k] = streak;
                if (owner == null) lanes[k].SetCounter(null);
                else lanes[k].SetCounter(owner.streak + "/" + Need(owner.gen), slime.Colour);
            }
        }

        private Color ColourOf(int gen, int slot)
        {
            int start = gen <= 1 ? 0 : gen == 2 ? 1 : 3;
            int i = start + slot;
            return colours != null && i >= 0 && i < colours.Length ? colours[i] : Color.white;
        }

        // A chest seed is in (or its wait ran out): more vines and more balls (the first one starts them).
        private void GrowMore()
        {
            if (won || restored) return;
            seedsEaten++;
            if (vines != null) vines.GrowNext();
            if (balls == null) return;
            int open = 0;
            for (int i = 0; i < seedsEaten && i < BowlingLayout.SproutGroups.Length; i++) open += BowlingLayout.SproutGroups[i];
            balls.OpenSpots = open;
            balls.MaxOnPad = 1 + seedsEaten;
            if (!ballsBegun)
            {
                ballsBegun = true;
                StartCoroutine(BeginBalls());
            }
        }

        private IEnumerator BeginBalls()
        {
            yield return new WaitForSeconds(ballsDelay);
            if (balls != null && !won && !restored && !balls.Running) balls.Begin();
        }

        // Waits these seconds, longer while the player is down or the game stopped.
        private IEnumerator Wait(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until || PlayerDown() || Paused()) yield return null;
        }

        private Vector3 CentrePoint() => centre != null ? centre.position : transform.position;

        private static float Pick(float[] values, int n, float fallback) =>
            values == null || values.Length == 0 ? fallback : values[Mathf.Clamp(n - 1, 0, values.Length - 1)];

        private static int Pick(int[] values, int n, int fallback) =>
            values == null || values.Length == 0 ? fallback : values[Mathf.Clamp(n - 1, 0, values.Length - 1)];

        // The roll assist asks where a bowl going this way should go: the nearest lane within Aim Angle. Its line to the head
        // pin passing through the lane's mouth: the head pin, steered by the ball's own Straighten. Bowled from beside the
        // mouth (that line would roll off the dirt next to the lane): steered all the way to a point on the lane's middle
        // line just past the mouth, so it rolls into the lane (the rails keep it there).
        private (Vector3 point, float steer)? FindLaneAim(Vector3 from, Vector3 flatDir)
        {
            if (won) return null;
            flatDir.y = 0f;
            if (flatDir.sqrMagnitude < 0.0001f) return null;
            BowlingLane best = null;
            float bestAngle = aimAngle;
            foreach (var lane in lanes)
            {
                if (lane == null || lane.Aim == null) continue;
                float angle = Vector3.Angle(flatDir, lane.Direction);
                if (angle > bestAngle) continue;
                bestAngle = angle;
                best = lane;
            }
            if (best == null) return null;
            var d = best.Direction;
            var rel = from - CentrePoint();
            rel.y = 0f;
            float along = Vector3.Dot(rel, d);
            float side = Vector3.Dot(rel, Vector3.Cross(Vector3.up, d));
            // The lane's own sizes (hand set up): the foul line at the dirt's edge (or the floor's start past it), the head
            // pin at its Aim, safe within Mouth Margin of its floor's edge.
            float foul = Mathf.Max(best.StartAlong, padHalfSize);
            float headPin = Mathf.Max(foul + 1f, best.HeadPinAlong);
            float mouthSafe = Mathf.Max(0.1f, best.HalfWidth - mouthMargin);
            float mouthSide = along >= foul
                ? side
                : side * (headPin - foul) / Mathf.Max(0.5f, headPin - along);
            if (Mathf.Abs(mouthSide) <= mouthSafe) return (best.Aim.position, -1f);
            float into = Mathf.Max(along, foul) + mouthLead;
            var point = CentrePoint() + d * into;
            point.y = best.Aim.position.y;
            return (point, 1f);
        }

        // ------------------------------------------------------------------ where Gate Run drops the chests

        // A chest in front of the head, on the dirt, clear of balls and open sprouts.
        private Vector3 ChestSpot()
        {
            var frame = centre != null ? centre : transform;
            var feet = HeadFloor();
            var forward = HeadForward();
            float[] turns = { 0f, 45f, -45f, 90f, -90f, 135f, -135f, 180f };
            foreach (float turn in turns)
            {
                var at = OnDirt(frame, feet + Quaternion.Euler(0f, turn, 0f) * forward * chestAhead);
                if (ChestClear(frame, at)) return at;
            }
            return OnDirt(frame, feet + forward * chestAhead);
        }

        private Vector3 OnDirt(Transform frame, Vector3 p)
        {
            var local = frame.InverseTransformPoint(p);
            local.x = Mathf.Clamp(local.x, -chestKeep, chestKeep);
            local.z = Mathf.Clamp(local.z, -chestKeep, chestKeep);
            local.y = BowlingLayout.DirtTop;
            return frame.TransformPoint(local);
        }

        private bool ChestClear(Transform frame, Vector3 at)
        {
            float clear = chestClear * chestClear;
            if (balls != null)
            {
                int open = Mathf.Clamp(balls.OpenSpots, 0, BowlingLayout.SproutYaws.Length);
                for (int i = 0; i < open; i++)
                    if (FlatSqr(frame.TransformPoint(BowlingLayout.SproutPoint(i)), at) < clear) return false;
                var live = balls.Balls;
                if (live != null)
                    foreach (var ball in live)
                        if (ball != null && FlatSqr(ball.position, at) < clear) return false;
            }
            return true;
        }

        private static float FlatSqr(Vector3 a, Vector3 b)
        {
            float x = a.x - b.x, z = a.z - b.z;
            return x * x + z * z;
        }

        // ------------------------------------------------------------------ tests

        /// <summary>A chest seed lying about is eaten, as the player would (its item, heal and gear bool).</summary>
        [ContextMenu("Eat The Chest Seed")]
        public void DebugEatSeed()
        {
            var spawned = Global("CurrentlySpawnedStuffs");
            if (spawned == null) return;
            foreach (var fsm in spawned.GetComponentsInChildren<PlayMakerFSM>())
            {
                if (fsm == null || fsm.ActiveStateName != "Rotate and Wait") continue;
                bool seed = false;
                for (var t = fsm.transform; t != null && t != spawned.transform; t = t.parent) seed |= t.name.StartsWith("Seed_");
                if (!seed) continue;
                Debug.Log("[Bowling] Test: " + fsm.gameObject.name + " eaten.", this);
                fsm.SendEvent("next");
                return;
            }
            Debug.Log("[Bowling] No chest seed waiting.", this);
        }

        /// <summary>The live rack on that lane resolves as a strike.</summary>
        public void DebugStrike(int lane)
        {
            if (!started || restored || won) return;
            var set = LiveSet(lane);
            if (set == null)
            {
                Debug.Log("[Bowling] No live rack on lane " + lane + ".", this);
                return;
            }
            set.ForceStrike();
        }

        [ContextMenu("Strike The First Rack")]
        private void StrikeFirst()
        {
            for (int k = 0; k < lanes.Length; k++)
            {
                if (LiveSet(k) == null) continue;
                DebugStrike(k);
                return;
            }
            Debug.Log("[Bowling] No live rack.", this);
        }

        /// <summary>Hurts Slimes[slime] (as its strikes would).</summary>
        public void DebugHit(int slime)
        {
            if (!started || restored || won) return;
            if (slime < 0 || slime >= slimeList.Count)
            {
                Debug.Log("[Bowling] No slime " + slime + " (" + slimeList.Count + " flying).", this);
                return;
            }
            var f = Find(slimeList[slime]);
            if (f == null || f.gen != Phase || f.hurt)
            {
                Debug.Log("[Bowling] Slime " + slime + " can't be hurt now (round " + Phase + ").", this);
                return;
            }
            Hit(f, "test");
        }

        [ContextMenu("Hit Slime 0")]
        private void HitFirst() => DebugHit(0);

        /// <summary>Hurts every slime of the round (the round is won).</summary>
        [ContextMenu("Win This Round")]
        public void DebugWinRound()
        {
            if (!started || restored || won) return;
            if (Phase < 1 || Phase > 3)
            {
                Debug.Log("[Bowling] Not in a round (phase " + Phase + ").", this);
                return;
            }
            foreach (var f in flyers.ToArray())
                if (f.gen == Phase && !f.hurt) Hit(f, "test");
        }

        /// <summary>Breaks the giant pumpkin (tests; Gate Run's "Test: Skip To The Finale" brings it).</summary>
        [ContextMenu("Win Now")]
        public void DebugWin()
        {
            if (!started || restored || won) return;
            if (finalePumpkin != null && !finalePumpkin.IsBroken)
            {
                finalePumpkin.SpecialHit(100000f);
                return;
            }
            Debug.Log("[Bowling] Win Now works once the giant pumpkin is in (Gate Run: Test: Skip To The Finale).", this);
        }

        /// <summary>The slime's current strikes (0 if it isn't one of ours).</summary>
        public int StreakOf(GiantFlyingSlime s)
        {
            var f = Find(s);
            return f != null ? f.streak : 0;
        }

        /// <summary>Racks the slime pooped since it came or was last hurt.</summary>
        public int PoopsOf(GiantFlyingSlime s)
        {
            var f = Find(s);
            return f != null ? f.poops : 0;
        }

        /// <summary>Live racks on a lane (0 or 1).</summary>
        public int SetsOn(int lane)
        {
            int n = 0;
            foreach (var s in sets) if (s != null && !s.Done && s.Lane == lane) n++;
            return n;
        }

        // ------------------------------------------------------------------ the win

        // The giant pumpkin broke: everything left goes. Gate Exit (every gate's one exit, started by Gate Boss Finale) does
        // the rest: the chest seeds still lying about and any gear bool still false, The_Seed at Seed Spot, the black, the
        // rooftop, the player by the Pumpkin Tree.
        private void Win()
        {
            if (won) return;
            won = true;
            Phase = 5;
            Debug.Log("[Bowling] Won.", this);
            StopAllCoroutines();
            if (balls != null) balls.StopAndClear();
            MeltAll();
            foreach (var f in flyers) if (f.slime != null) f.slime.Leave();
            flyers.Clear();
            RefreshSlimes();
            EnemyWave.KillAll(this);
            if (BowlingRollAssist.LaneAim == laneAim) BowlingRollAssist.LaneAim = null;
            if (ballHit != null && BowlingRollAssist.BallHit == ballHit) BowlingRollAssist.BallHit = null;
            onWon?.Invoke();
            StartCoroutine(OffToTheRooftop());
        }

        // The new rooftop is up (the player waits in the black on the dirt, held by Gate Exit): the arena's Fall Edges go off
        // (they overlap the roof's arms: they'd kill what's there and hole its NavMesh) and everything is given back
        // (moving, the fog, the fall's return spot). Gate Exit removes the arena once the player is on the rooftop.
        private IEnumerator OffToTheRooftop()
        {
            var oldRoof = Global("Rooftop_Base");
            while (true)
            {
                var roof = Global("Rooftop_Base");
                if (roof != null && roof != oldRoof) break;
                yield return null;
            }
            foreach (var edge in GetComponentsInChildren<FallEdge>(true))
                if (edge != null) edge.gameObject.SetActive(false);
            Restore();
        }

        // The seed's floor spot: 0.8 m in front of the head, kept on the dirt (Gate Exit shows it 1 m over it).
        private Vector3 SeedSpot() => OnPad(HeadFloor() + HeadForward() * 0.8f, 0.3f);

        // Where the giant pumpkin's candies pop out: over the dirt 1.2 m in front of the head and 1 m to the right, clear of
        // The_Seed (0.8 m in front: candies made inside its collider were shoved off the dirt, play test 2026-11-23).
        private Vector3 CandySpot()
        {
            var forward = HeadForward();
            var right = Vector3.Cross(Vector3.up, forward);
            return OnPad(HeadFloor() + forward * 1.2f + right * 1f, 0.7f) + Vector3.up * 1.3f;
        }

        // A point kept on the dirt (Inset metres in from its edge), on its top.
        private Vector3 OnPad(Vector3 p, float inset)
        {
            var frame = centre != null ? centre : transform;
            var local = frame.InverseTransformPoint(p);
            float keep = Mathf.Max(0.2f, padHalfSize - inset);
            local.x = Mathf.Clamp(local.x, -keep, keep);
            local.z = Mathf.Clamp(local.z, -keep, keep);
            local.y = BowlingLayout.DirtTop;
            return frame.TransformPoint(local);
        }

        // ------------------------------------------------------------------ the player and the world

        // Thumbstick and arm-swing moving off (each one's own state remembered once, given back by Restore). The movers are
        // looked up once (again only if one is gone), not every half second.
        private void MovementOff()
        {
            bool gone = movers.Count == 0;
            foreach (var mover in movers) gone |= mover == null;
            if (gone)
            {
                movers.Clear();
                movers.AddRange(FindObjectsByType<ContinuousMoveProvider>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            }
            foreach (var mover in movers)
            {
                if (mover == null) continue;
                if (!moveWas.ContainsKey(mover)) moveWas[mover] = mover.enabled;
                if (mover.enabled) mover.enabled = false;
            }
        }

        private void MovementBack()
        {
            foreach (var pair in moveWas) if (pair.Key != null) pair.Key.enabled = pair.Value;
            moveWas.Clear();
        }

        private void Fog(bool on)
        {
            if (on)
            {
                fogStartWas = RenderSettings.fogStartDistance;
                fogEndWas = RenderSettings.fogEndDistance;
                fogColourWas = RenderSettings.fogColor;
                RenderSettings.fogStartDistance = fog.x;
                RenderSettings.fogEndDistance = fog.y;
                if (tintFog) RenderSettings.fogColor = fogColour;
                fogSet = true;
                return;
            }
            if (!fogSet) return;
            RenderSettings.fogStartDistance = fogStartWas;
            RenderSettings.fogEndDistance = fogEndWas;
            RenderSettings.fogColor = fogColourWas;
            fogSet = false;
        }

        // Everything given back (once): moving, the fall's return spot, the fog, the roll assist's hooks, the flyers.
        private void Restore()
        {
            if (!started || restored) return;
            restored = true;
            lockMoves = false;
            foreach (var set in sets)
            {
                if (set == null) continue;
                set.OnResolved -= OnSetResolved;
                set.OnCleared -= OnSetCleared;
            }
            if (BowlingRollAssist.LaneAim == laneAim) BowlingRollAssist.LaneAim = null;
            if (ballHit != null && BowlingRollAssist.BallHit == ballHit) BowlingRollAssist.BallHit = null;
            MovementBack();
            if (returnSet && FallOutOfWorld.ReturnSpot.HasValue && (FallOutOfWorld.ReturnSpot.Value - returnSpot).sqrMagnitude < 0.01f)
                FallOutOfWorld.ReturnSpot = null;
            returnSet = false;
            Fog(false);
            EnemyWave.KillAll(this);
        }

        // The foot of the head (on the XR Origin's floor).
        private Vector3 HeadFloor()
        {
            if (head == null) return transform.position;
            float y = origin != null ? origin.transform.position.y : 0f;
            return new Vector3(head.position.x, y, head.position.z);
        }

        private Vector3 HeadForward()
        {
            var f = head != null ? Vector3.ProjectOnPlane(head.forward, Vector3.up) : Vector3.zero;
            if (f.sqrMagnitude < 0.0001f && lanes.Length > 0 && lanes[0] != null) f = lanes[0].Direction;
            return f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
        }

        // The player is down: health out, the lucky coins under way, or getting back up from a revive.
        private bool PlayerDown()
        {
            if (playerHealth == null && origin != null)
            {
                foreach (var fsm in origin.GetComponents<PlayMakerFSM>())
                {
                    if (fsm.FsmName == "PlayerHealth") playerHealth = fsm.FsmVariables.FindFsmFloat("PlayerHealth");
                }
            }
            return Game.Saving.Checkpoints.IsDying ||
                   (Game.Combat.PlayerRevive.Instance != null && Game.Combat.PlayerRevive.Instance.IsReviving) ||
                   (playerHealth != null && playerHealth.Value <= 0f);
        }

        // The game is stopped: the pause menu, a level-up or other notice, the lucky coins (Pause Menu holds), lost focus.
        private static bool Paused() => Time.timeScale <= 0f || Game.Menus.PauseMenu.IsPaused || Game.Menus.PauseMenu.IsHeld;

        // A PlayMaker global GameObject's object (null if it's empty or gone).
        private static GameObject Global(string name)
        {
            var global = HutongGames.PlayMaker.FsmVariables.GlobalVariables.FindFsmGameObject(name);
            return global != null && global.Value != null ? global.Value : null;
        }

        // Where spawned things go: Currently Spawned Stuffs (null: the scene root).
        private static Transform Spawned()
        {
            var spawned = Global("CurrentlySpawnedStuffs");
            return spawned != null ? spawned.transform : null;
        }
    }
}

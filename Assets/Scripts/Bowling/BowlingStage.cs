using System.Collections;
using System.Collections.Generic;
using Game.Enemies;
using Game.Inventory;
using Game.Locomotion;
using Game.PlayGround;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

namespace Game.Bowling
{
    /// <summary>
    /// Gate 2, "Strike the Slimes" (2026-11-08): the player stands on a small dirt island in the middle of an X of 4 long
    /// lanes. Punch the start chest and eat its seed: vines grow bowling pumpkins and a Giant Flying Slime comes down and
    /// circles overhead. It flies to a lane (which lights up), poops a 6-pin rack of slime pins that grow fat and then hatch
    /// small flying slimes. A strike raises that slime's streak; 3 in a row hurts it (phase 1 and 2: it splits in two;
    /// phase 3: it loses a wing). 6 racks without being hurt and it rains 6 divers on the player instead (also its hurt).
    /// Each phase end drops a chest (more balls). When all 4 have lost a wing, each drops onto a lane as a big ground slime;
    /// beat them and The_Seed shows, then GAMESTAGES brings up the rooftop and the player is put near the Lid, in the black.
    /// While it runs (until the showdown): thumbstick and arm-swing moving are off, a fall goes back to the centre, the fog
    /// is lifted. Made by GAMESTAGES (Path 2 ON) under Currently Spawned Stuffs at [LevelManager].
    /// Tools > 429 Game > Bowling > Set Up Bowling Arena builds it (Prefabs/Bowling/Bowling_Arena).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BowlingStage : MonoBehaviour
    {
        [Header("Arena")]
        [Tooltip("The 4 lanes (lane 0 is in front of the player at the start).")]
        [SerializeField] private BowlingLane[] lanes = System.Array.Empty<BowlingLane>();
        [Tooltip("The middle of the dirt (a fall puts the player back here). Its X/Z run along the lanes (the X frame).")]
        [SerializeField] private Transform centre;
        [Tooltip("Half the dirt's side (metres): the seed is kept this far from the centre, less a little.")]
        [SerializeField] private float padHalfSize = 2.5f;
        [Tooltip("Grows the bowling pumpkins on the vine tips.")]
        [SerializeField] private BowlingBallSupply balls;
        [Tooltip("The vines that pop out of the dirt with each seed.")]
        [SerializeField] private VineGrow vines;
        [Tooltip("The rules sign over lane 0 (later the win sign).")]
        [SerializeField] private TMP_Text rulesSign;
        [Tooltip("Where the pin sets go (the arena's \"Pins\" child).")]
        [SerializeField] private Transform pinsRoot;

        [Header("Prefabs")]
        [Tooltip("The Giant Flying Slime (Prefabs/Bowling/Giant_Flying_Slime).")]
        [SerializeField] private GiantFlyingSlime slimePrefab;
        [Tooltip("One slime pin (Prefabs/Bowling/Slime_Pin).")]
        [SerializeField] private SlimePin pinPrefab;
        [Tooltip("What a full-grown pin hatches into (Prefabs/Bowling/Slime_Pin_Flyer).")]
        [SerializeField] private GameObject flyerPrefab;
        [Tooltip("The 7th poop's divers (Prefabs/Enemies/Daruma_Bomber).")]
        [SerializeField] private GameObject diverPrefab;
        [Tooltip("The showdown ground slimes: Red, Blue, Green, Yellow.")]
        [SerializeField] private GameObject[] showdownPrefabs = new GameObject[4];
        [Tooltip("For each phase-3 slime (Red, Yellow, Blue, Green), which showdown prefab it becomes.")]
        [SerializeField] private int[] showdownOf = { 0, 3, 1, 2 };
        [Tooltip("Shown in front of the player on a win (Items/Chest/The_Seed).")]
        [SerializeField] private GameObject seedPrefab;
        [Tooltip("The puff (Smoke_Flash_Small_Puff).")]
        [SerializeField] private GameObject puff;

        [Header("Chests")]
        [Tooltip("Dropped at the start (Treasure_Chest_and_Seed_Bracer): its seed brings the slime.")]
        [SerializeField] private GameObject startChest;
        [Tooltip("Dropped at the end of phase 1, 2 and 3 (PG chests: Chest, Head, Shield).")]
        [SerializeField] private GameObject[] phaseChests = new GameObject[3];
        [Tooltip("GAMESTAGES bools the start chest and the 3 phase chests' seeds set.")]
        [SerializeField] private string[] gearBools = { "Bracer_Gear", "Chest_Gear", "Head_Gear", "Shield_Gear" };
        [Tooltip("Seconds after arriving before the start chest drops.")]
        [SerializeField] private float startChestDelay = 1f;
        [Tooltip("A chest drops this far in front of the head (metres), kept on the dirt.")]
        [SerializeField] private float chestAhead = 1.2f;
        [Tooltip("A chest is kept this close to the centre (X frame, metres).")]
        [SerializeField] private float chestKeep = 1.9f;
        [Tooltip("A chest keeps this far from balls and open sprouts (metres).")]
        [SerializeField] private float chestClear = 0.5f;
        [SerializeField] private float chestPuffScale = 0.12f;

        [Header("Phases (1, 2, 3)")]
        [Tooltip("The slimes' root scale.")]
        [SerializeField] private float[] sizes = { 7f, 5.5f, 4.5f };
        [Tooltip("Fly speed (metres a second).")]
        [SerializeField] private float[] speeds = { 6f, 6.5f, 7f };
        [Tooltip("Least seconds between one slime's poops.")]
        [SerializeField] private float[] poopEvery = { 12f, 14f, 18f };
        [Tooltip("Seconds a pin takes from landing to full fat (then it hatches).")]
        [SerializeField] private float[] growSeconds = { 24f, 22f, 20f };
        [Tooltip("At most this many of the stage's flyers alive.")]
        [SerializeField] private int[] flyersCap = { 5, 8, 8 };
        [Tooltip("Wing loop pitch.")]
        [SerializeField] private float[] pitches = { 0.55f, 0.65f, 0.75f };
        [Tooltip("Wing loop volume.")]
        [SerializeField] private float[] volumes = { 0.8f, 0.65f, 0.5f };
        [Tooltip("Colours, phase after phase: 1 (phase 1), 2 (phase 2), 4 (phase 3). Slime i splits into 2i and 2i+1.")]
        [SerializeField] private Color[] colours =
        {
            new(0.45f, 0.85f, 0.25f),
            new(0.89f, 0.15f, 0.18f), new(0.2f, 0.45f, 1f),
            new(0.89f, 0.15f, 0.18f), new(1f, 0.85f, 0.12f), new(0.2f, 0.45f, 1f), new(0.45f, 0.85f, 0.25f),
        };

        [Header("Rules")]
        [Tooltip("Strikes in a row on one slime's racks that hurt it.")]
        [Min(1)]
        [SerializeField] private int strikesToHit = 3;
        [Tooltip("Racks a slime poops without being hurt; the next time it rains divers instead.")]
        [Min(1)]
        [SerializeField] private int poopsBeforeDive = 6;
        [Tooltip("Divers it rains, and the seconds between them.")]
        [Min(0)]
        [SerializeField] private int divers = 6;
        [SerializeField] private float diverGap = 0.3f;
        [Tooltip("A bowl is steered to a lane whose direction is this close (degrees).")]
        [SerializeField] private float aimAngle = 25f;
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
        [SerializeField] private float entranceSeconds = 6f;
        [Min(0)]
        [SerializeField] private int entranceLaps = 2;
        [Tooltip("Seconds after the start seed before the slime comes (the balls grow meanwhile).")]
        [SerializeField] private float slimeDelay = 4f;
        [Tooltip("Seconds after the start seed before the balls start growing.")]
        [SerializeField] private float ballsDelay = 0.6f;
        [Tooltip("A slime with nothing to do this long goes back to circling the head.")]
        [SerializeField] private float idleReturnSeconds = 4f;
        [Tooltip("Strain seconds: before a rack, before the divers, before the showdown drop.")]
        [SerializeField] private float poopStrain = 0.6f;
        [SerializeField] private float diveStrain = 0.8f;
        [SerializeField] private float dropStrain = 1f;
        [Tooltip("Seconds between phases (the slimes circle).")]
        [SerializeField] private float breatherSeconds = 5f;
        [Tooltip("Seconds after the phase-3 chest before the showdown.")]
        [SerializeField] private float finaleWait = 1.5f;

        [Header("Split")]
        [Tooltip("Seconds from the bonk to the split.")]
        [SerializeField] private float splitDelay = 0.4f;
        [Tooltip("The two halves grow from this share over these seconds, flung apart this fast (m/s).")]
        [SerializeField] private float splitGrowFrom = 0.6f;
        [SerializeField] private float splitGrowSeconds = 0.5f;
        [SerializeField] private float splitFling = 2f;

        [Header("Showdown")]
        [Tooltip("Seconds a slime falls onto its lane, and between drops.")]
        [SerializeField] private float dropSeconds = 0.9f;
        [SerializeField] private float dropGap = 0.6f;
        [Tooltip("Seconds after the last ground slime has landed before they all set off together.")]
        [SerializeField] private float holdOffBase = 0.5f;
        [Tooltip("Unused: the ground slimes all set off together once the last has landed (their speeds space them).")]
        [SerializeField] private float holdOffStep = 1f;

        [Header("Signs, look and sounds")]
        [TextArea]
        [SerializeField] private string rulesText = "STRIKE THE SLIMES\n<size=80%>3 strikes in a row hurts one</size>";
        [TextArea]
        [SerializeField] private string winText = "YOU WIN!\n<size=80%>Eat the seed</size>";
        [Tooltip("The rules sign hides after the first strike or this many seconds.")]
        [SerializeField] private float rulesSeconds = 25f;
        [Tooltip("While the stage runs, the fog starts and ends this far (metres).")]
        [SerializeField] private Vector2 fog = new(14f, 38f);

        [Header("After a win: the seed and the rooftop")]
        [Tooltip("Not eaten after this many seconds: it's eaten for them.")]
        [Min(1f)]
        [SerializeField] private float seedSeconds = 20f;
        [Tooltip("Seconds in the black waiting for the rooftop before the view comes back (with a warning; it keeps waiting).")]
        [Min(1f)]
        [SerializeField] private float roofWaitSeconds = 20f;
        [Tooltip("The player is put this far from the Lid (metres), facing it.")]
        [Min(0.5f)]
        [SerializeField] private float lidStandOff = 4f;

        [Header("Hooks")]
        [Tooltip("A lane lit (its index).")]
        [SerializeField] private UnityEvent<int> onLaneLit = new();
        [Tooltip("A phase started (1..3, 4 = showdown).")]
        [SerializeField] private UnityEvent<int> onPhase = new();
        [Tooltip("The stage won (before the seed shows).")]
        [SerializeField] private UnityEvent onWon = new();

        /// <summary>The stage being played (only one at a time).</summary>
        public static BowlingStage Running { get; private set; }

        /// <summary>Raised when it's over: won (the player is on the rooftop), or not (cleared away, e.g. Game Over).</summary>
        public static event System.Action<BowlingStage, bool> Ended;

        /// <summary>0 waiting for the start seed, 1..3 the phases, 4 the showdown, 5 won.</summary>
        public int Phase { get; private set; }

        public bool Won => won;
        public IReadOnlyList<BowlingLane> Lanes => lanes;

        /// <summary>The giant slimes flying now (in their order: circle places, Debug Hit).</summary>
        public IReadOnlyList<GiantFlyingSlime> Slimes => slimeList;

        // One giant slime and what the stage knows of it.
        private sealed class Flyer
        {
            public GiantFlyingSlime slime;
            public int gen;              // the phase it belongs to (1..3)
            public int slot;             // its place in that phase (colour, what it splits into, its showdown slime)
            public int streak, poops;
            public int lastLane = -1;
            public readonly int[] uses = new int[4];
            public float nextPoopAt, retryAt, idleSince;
            public bool hurt;            // hit this phase (split coming, or a wing gone)
            public bool acting;          // the stage has it busy (entrance, a rack, the divers, the showdown)
            public bool circling;
            public int targetLane = -1;  // the lane it's flying to or pooping on
            public int ticket;           // goes up on a hit: its jobs stop
            public Coroutine job;
        }

        // A chest dropped and the gear bool its seed sets.
        private sealed class Chest
        {
            public GameObject chest;
            public string gear;
            public int index;            // 0 start, 1..3 the phases
            public bool eaten;
        }

        private XROrigin origin;
        private Transform head;
        private bool started, restored, won, endedSent;
        private readonly Dictionary<ContinuousMoveProvider, bool> moveWas = new();
        private float nextMoveCheck;
        private bool lockMoves;
        private System.Func<Vector3, Vector3, Vector3?> aimFinder;
        private Vector3 returnSpot;
        private bool returnSet;
        private float fogStartWas, fogEndWas;
        private bool fogSet;
        private bool rulesShowing;
        private float rulesUntil;
        private int strikes;

        private readonly List<Flyer> flyers = new();
        private readonly List<GiantFlyingSlime> slimeList = new();
        private readonly List<PinSet> sets = new();
        private readonly List<Chest> pendingChests = new();
        private int seedsEaten;
        private bool ballsBegun;
        private bool inventoryHooked;
        private float nextSeedCheck;
        private bool phaseEnding;
        private Coroutine playJob, phaseJob;
        private readonly GiantFlyingSlime[] counterOwner = new GiantFlyingSlime[4];
        private readonly int[] counterStreak = { -1, -1, -1, -1 };

        // The showdown.
        private bool finaleStarted;
        private int showdownCount, showdownLanded, dropTurn;
        // The ground slimes landed so far, held until the last one is down.
        private readonly List<SpiderBrain> showdownBrains = new();
        private float nextDropAt, lastLandedAt;

        // The player's health (PlayerHealth FSM), for "knocked down".
        private HutongGames.PlayMaker.FsmFloat playerHealth;
        // GAMESTAGES' bool the seed sets, and its seed FSM's state and event.
        private const string SeedBool = "The_Seed";
        private const string SeedWaitState = "Rotate and Wait";
        private const string SeedEatenEvent = "next";
        // The item every chest seed gives (the fallback without GAMESTAGES).
        private const string SeedItem = "Yellow_Seed";

        private void Start()
        {
            if (Running != null && Running != this) { Destroy(gameObject); return; }
            Running = this;
            started = true;
            EnemyWave.Start(this);
            aimFinder = FindAim;
            BowlingRollAssist.AimFinder = aimFinder;

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

            // All lanes dark (floors and rails still show), no counters, no sign yet. The vines hide themselves.
            foreach (var lane in lanes)
            {
                if (lane == null) continue;
                lane.SetLit(false, false);
                lane.SetCounter(null);
            }
            if (rulesSign != null) rulesSign.gameObject.SetActive(false);
            Phase = 0;
            playJob = StartCoroutine(Play());
        }

        private void Update()
        {
            if (!started || restored) return;
            // The pause menu or a fall may switch a mover back on (not in the showdown: they may walk then).
            if (lockMoves && Time.unscaledTime >= nextMoveCheck)
            {
                nextMoveCheck = Time.unscaledTime + 0.5f;
                MovementOff();
            }
            if (rulesShowing && !won && (strikes > 0 || Time.time >= rulesUntil))
            {
                rulesShowing = false;
                if (rulesSign != null) rulesSign.gameObject.SetActive(false);
            }
            if (won) return;

            // The inventory may come up after this; hook it as soon as it's there (seeds without GAMESTAGES).
            if (!inventoryHooked && PlayerInventory.Instance != null)
            {
                PlayerInventory.Instance.Changed += OnInventory;
                inventoryHooked = true;
            }
            if (Time.time >= nextSeedCheck)
            {
                nextSeedCheck = Time.time + 0.2f;
                WatchSeeds();
            }

            Prune();
            var floor = HeadFloor();
            foreach (var f in flyers) if (f.slime != null) f.slime.SetCircleCentre(floor);
            if (Phase >= 1 && Phase <= 3 && !phaseEnding && !PlayerDown())
            {
                // Think never adds or removes slimes (it only starts their jobs).
                for (int i = 0; i < flyers.Count; i++) Think(flyers[i]);
            }
            UpdateCounters();

            if (Phase == 4 && finaleStarted && showdownLanded >= showdownCount && Time.time >= lastLandedAt + 1f && EnemyWave.Alive(this) == 0)
                Win();
        }

        private void OnDestroy()
        {
            // Cleared away (Game Over) or done: give everything back. Never moves the player (Game Over has them).
            Restore();
            if (started && !endedSent)
            {
                endedSent = true;
                Ended?.Invoke(this, false);
            }
            if (Running == this) Running = null;
        }

        // ------------------------------------------------------------------ the game

        // The start chest; its seed (vines, balls); the slime's entrance. Then the slimes run themselves (Think), the
        // phases end on hits (Phase End), and the showdown ends in Win.
        private IEnumerator Play()
        {
            yield return Wait(startChestDelay);
            if (won) yield break;
            var start = DropChest(startChest, Gear(0), 0);
            while (start != null && !start.eaten) yield return null;
            if (start == null) SeedEaten(new Chest { gear = Gear(0), index = 0 });

            yield return Wait(slimeDelay);
            if (won || slimePrefab == null)
            {
                if (slimePrefab == null) Debug.LogError("[Bowling] No Giant Flying Slime prefab: the stage can't go on.", this);
                yield break;
            }
            ShowRules();
            yield return Entrance();
        }

        // The phase-1 slime comes down from high over lane 0 (louder and louder), then laps the head before its first poop.
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
            Phase = 1;
            onPhase?.Invoke(1);
            Debug.Log("[Bowling] Phase 1: the Giant Flying Slime comes.", this);

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

        // A free lane that isn't its last one: the one it used least this phase, then the one nearest the player's facing.
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
                                   lane.transform.position, lane.Direction, f.slime.Colour, growth, balls, pinFlightSeconds, pinStagger);
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

        // The 7th poop: to its circle place over the head, strain, 6 divers; that's its hurt.
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
            var owner = Find(set.Owner as GiantFlyingSlime);
            if (strike)
            {
                strikes++;
                if (owner == null || owner.hurt || owner.gen != Phase) return;
                owner.streak++;
                Debug.Log("[Bowling] Strike on lane " + set.Lane + ": " + owner.slime.name + " streak " + owner.streak + "/" + strikesToHit + ".", this);
                if (owner.streak >= strikesToHit) Hit(owner, strikesToHit + " strikes");
            }
            else if (owner != null)
            {
                owner.streak = 0;
            }
        }

        private void OnSetCleared(PinSet set)
        {
            if (set == null) return;
            set.OnResolved -= OnSetResolved;
            set.OnCleared -= OnSetCleared;
            sets.Remove(set);
        }

        // A slime is hurt: bonk, its racks melt; phase 1/2 it splits in two (they wait for the next phase), phase 3 it
        // loses a wing. When every slime of the phase is hurt, the phase ends.
        private void Hit(Flyer f, string why)
        {
            if (f == null || f.slime == null || f.hurt || won || restored || f.gen != Phase) return;
            f.hurt = true;
            f.ticket++;
            if (f.job != null) StopCoroutine(f.job);
            f.job = null;
            f.acting = false;
            f.targetLane = -1;
            Debug.Log("[Bowling] " + f.slime.name + " hurt (" + why + ") in phase " + Phase + ".", this);
            f.slime.Bonk(head != null ? head.position : CentrePoint());
            MeltSetsOf(f.slime);
            f.streak = 0;
            f.poops = 0;
            if (f.gen < 3)
            {
                f.acting = true;
                StartCoroutine(Split(f));
            }
            else
            {
                f.slime.LoseWing();
                Circle(f);
            }
            CheckPhaseEnd();
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
            Debug.Log("[Bowling] " + s.name + " split in two (phase " + next + " slimes).", this);
            yield return new WaitForSeconds(splitGrowSeconds + 0.05f);
            foreach (var record in made)
            {
                if (record.slime == null) continue;
                record.acting = false;
            }
            Respace();
        }

        private void CheckPhaseEnd()
        {
            if (phaseEnding || won || restored || Phase < 1 || Phase > 3) return;
            foreach (var f in flyers)
                if (f.slime != null && f.gen == Phase && !f.hurt) return;
            phaseJob = StartCoroutine(PhaseEnd(Phase));
        }

        // Every rack melts, the phase's chest drops, a breather, then the next phase (they leave one by one). After phase 3:
        // the showdown.
        private IEnumerator PhaseEnd(int p)
        {
            phaseEnding = true;
            Debug.Log("[Bowling] Phase " + p + " over.", this);
            MeltAll();
            yield return Wait(0.5f);
            if (p - 1 < phaseChests.Length) DropChest(phaseChests[p - 1], Gear(p), p);
            if (p >= 3)
            {
                yield return Wait(finaleWait);
                yield return Finale();
                yield break;
            }
            yield return Wait(Mathf.Max(0f, breatherSeconds - 0.5f));
            Phase = p + 1;
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
            phaseEnding = false;
            onPhase?.Invoke(Phase);
            Debug.Log("[Bowling] Phase " + Phase + ": " + i + " slimes.", this);
        }

        // ------------------------------------------------------------------ the showdown

        // Each one-winged slime flies to its own lane, strains, loses the other wing and drops; each landing becomes a
        // ground slime coming down the lane. Moving comes back when the last has landed.
        private IEnumerator Finale()
        {
            Phase = 4;
            phaseEnding = false;
            onPhase?.Invoke(4);
            var group = new List<Flyer>();
            foreach (var f in flyers) if (f.slime != null && f.gen == 3) group.Add(f);
            group.Sort((a, b) => a.slot.CompareTo(b.slot));
            showdownCount = group.Count;
            showdownLanded = 0;
            dropTurn = 0;
            nextDropAt = 0f;
            finaleStarted = true;
            Debug.Log("[Bowling] Showdown: " + showdownCount + " slimes.", this);

            var c = CentrePoint();
            var taken = new bool[lanes.Length];
            for (int i = 0; i < group.Count; i++)
            {
                var f = group[i];
                var flat = f.slime.transform.position - c;
                flat.y = 0f;
                int best = -1;
                float bestAngle = float.MaxValue;
                for (int k = 0; k < lanes.Length; k++)
                {
                    if (taken[k] || lanes[k] == null) continue;
                    float angle = flat.sqrMagnitude > 0.0001f ? Vector3.Angle(flat, lanes[k].Direction) : k;
                    if (angle < bestAngle) { bestAngle = angle; best = k; }
                }
                if (best < 0) best = i % Mathf.Max(1, lanes.Length);
                else taken[best] = true;
                f.job = StartCoroutine(ShowdownRun(f, best, i));
            }
            if (showdownCount == 0) lastLandedAt = Time.time;
            yield break;
        }

        private IEnumerator ShowdownRun(Flyer f, int k, int order)
        {
            f.ticket++;
            f.acting = true;
            f.circling = false;
            f.targetLane = k;
            var s = f.slime;
            var lane = lanes[k];
            if (lane != null && !lane.Lit) LightLane(k);

            if (s != null && lane != null)
            {
                bool arrived = false;
                s.LookAt(CentrePoint());
                s.FlyTo(HoverPoint(k, s.Size), Pick(speeds, 3, 7f), () => arrived = true);
                for (float giveUp = Time.time + 20f; !arrived && s != null && Time.time < giveUp;) yield return null;
            }
            if (s != null)
            {
                bool strained = false;
                s.Strain(dropStrain, () => strained = true);
                for (float giveUp = Time.time + dropStrain + 2f; !strained && s != null && Time.time < giveUp;) yield return null;
            }
            if (s != null && s.WingsLeft > 0)
            {
                bool lost = false;
                s.LoseWing(() => lost = true);
                for (float giveUp = Time.time + 3f; !lost && s != null && Time.time < giveUp;) yield return null;
            }

            // In slime order, Drop Gap apart.
            while (dropTurn < order || Time.time < nextDropAt)
            {
                if (won || restored) yield break;
                yield return null;
            }
            dropTurn = order + 1;
            nextDropAt = Time.time + dropGap;

            var ground = lane != null ? lane.SlotPosition(0) : CentrePoint();
            bool landed = false;
            if (s != null)
            {
                s.Drop(ground, dropSeconds, at =>
                {
                    if (landed) return;
                    landed = true;
                    Landed(f, k, order, at);
                });
                for (float giveUp = Time.time + dropSeconds + 3f; !landed && Time.time < giveUp;) yield return null;
            }
            if (!landed && !won && !restored)
            {
                // The puppet went missing: its ground slime comes anyway.
                landed = true;
                Landed(f, k, order, ground);
            }
        }

        private void Landed(Flyer f, int k, int order, Vector3 at)
        {
            if (won || restored) return;
            flyers.Remove(f);
            RefreshSlimes();
            int kind = f.slot >= 0 && f.slot < showdownOf.Length ? showdownOf[f.slot] : f.slot;
            var ground = lanes[k] != null ? lanes[k].SlotPosition(0) : at;
            SpawnShowdown(kind, ground, order);
            showdownLanded++;
            lastLandedAt = Time.time;
            if (showdownLanded >= showdownCount)
            {
                foreach (var brain in showdownBrains)
                    if (brain != null) brain.HoldOff(holdOffBase);
                showdownBrains.Clear();
                lockMoves = false;
                MovementBack();
                Debug.Log("[Bowling] All " + showdownCount + " ground slimes down: moving is back.", this);
            }
        }

        // A ground slime on the NavMesh near the head pin, facing the centre; it always comes (the EnemyLimit is ignored).
        private void SpawnShowdown(int kind, Vector3 at, int order)
        {
            var prefab = showdownPrefabs != null && kind >= 0 && kind < showdownPrefabs.Length ? showdownPrefabs[kind] : null;
            if (prefab == null)
            {
                Debug.LogWarning("[Bowling] No showdown prefab " + kind + ": that slime is skipped.", this);
                return;
            }
            var filter = new NavMeshQueryFilter
            {
                agentTypeID = prefab.TryGetComponent(out NavMeshAgent agent) ? agent.agentTypeID : 0,
                areaMask = NavMesh.AllAreas,
            };
            var pos = at;
            if (NavMesh.SamplePosition(at, out var hit, 2.5f, filter)) pos = hit.position;
            else Debug.LogWarning("[Bowling] No NavMesh within 2.5 m of " + at + " for " + prefab.name + ".", this);
            var look = CentrePoint() - pos;
            look.y = 0f;
            var go = Instantiate(prefab, pos, look.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(look) : Quaternion.identity, Spawned());
            EnemyLimit.TryTakeSlot();
            EnemyWave.Add(this, go);
            if (go.TryGetComponent(out SpiderBrain brain))
            {
                brain.ComeAtOnce();
                // It waits until the last has landed; then they all set off together, so each one's own pace
                // (fastest first: Green, Blue, Red, Yellow) brings them to the player one by one.
                brain.HoldOff(60f);
                showdownBrains.Add(brain);
            }
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
            bool gone = flyers.RemoveAll(f => f.slime == null && !(Phase == 4 && f.acting)) > 0;
            sets.RemoveAll(s => s == null || s.Done);
            if (!gone) return;
            RefreshSlimes();
            CheckPhaseEnd();
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

        // Every circling slime takes its new place (after a split).
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

        // Each lane's counter: its rack owner's streak, in its colour; hidden with no rack.
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
                else lanes[k].SetCounter(owner.streak + "/" + strikesToHit, slime.Colour);
            }
        }

        private Color ColourOf(int gen, int slot)
        {
            int start = gen <= 1 ? 0 : gen == 2 ? 1 : 3;
            int i = start + slot;
            return colours != null && i >= 0 && i < colours.Length ? colours[i] : Color.white;
        }

        private void ShowRules()
        {
            if (rulesSign == null) return;
            rulesSign.text = rulesText;
            rulesSign.gameObject.SetActive(true);
            rulesShowing = true;
            rulesUntil = Time.time + rulesSeconds;
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

        // The roll assist asks where a bowl going this way should go: the nearest lane within Aim Angle, its aim point.
        private Vector3? FindAim(Vector3 from, Vector3 flatDir)
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
            return best.Aim.position;
        }

        // ------------------------------------------------------------------ chests and seeds

        private string Gear(int i) => gearBools != null && i >= 0 && i < gearBools.Length ? gearBools[i] : null;

        // A chest 1.2 m in front of the head, on the dirt, clear of balls and open sprouts, under Currently Spawned Stuffs.
        private Chest DropChest(GameObject prefab, string gear, int index)
        {
            if (prefab == null)
            {
                Debug.LogWarning("[Bowling] No chest " + index + ": its seed's gain is given at once.", this);
                if (!string.IsNullOrEmpty(gear) && GameStages.Fsm() != null) GameStages.SetBool(gear, true, this);
                var none = new Chest { gear = gear, index = index };
                SeedEaten(none);
                return none;
            }
            var at = ChestSpot();
            var made = Instantiate(prefab, at, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), Spawned());
            if (made.TryGetComponent<Rigidbody>(out var body) && !body.isKinematic) body.linearVelocity = Vector3.zero;
            if (puff != null) EffectPool.Play(puff, at, chestPuffScale);
            var chest = new Chest { chest = made, gear = gear, index = index };
            Debug.Log("[Bowling] Chest " + prefab.name + " dropped (" + gear + ").", this);
            if (!string.IsNullOrEmpty(gear) && GameStages.Fsm() != null && GameStages.GetBool(gear))
            {
                Debug.Log("[Bowling] " + gear + " was already true when its chest dropped: going on.", this);
                SeedEaten(chest);
                return chest;
            }
            pendingChests.Add(chest);
            return chest;
        }

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

        // The chests' gear bools going true = their seed eaten (also the seed's own timeout path, which gives no item).
        private void WatchSeeds()
        {
            if (pendingChests.Count == 0 || GameStages.Fsm() == null) return;
            foreach (var chest in pendingChests.ToArray())
                if (!string.IsNullOrEmpty(chest.gear) && GameStages.GetBool(chest.gear)) SeedEaten(chest);
        }

        // No GAMESTAGES (a test scene): a Yellow_Seed gained = the oldest chest's seed eaten.
        private void OnInventory(ItemDefinition item, int change, int newCount)
        {
            if (won || restored || GameStages.Fsm() != null || change <= 0 || item == null || item.Id != SeedItem) return;
            if (pendingChests.Count > 0) SeedEaten(pendingChests[0]);
        }

        // A seed eaten: more vines and more balls (the first one starts them).
        private void SeedEaten(Chest chest)
        {
            if (chest == null || chest.eaten) return;
            chest.eaten = true;
            pendingChests.Remove(chest);
            if (won || restored) return;
            seedsEaten++;
            Debug.Log("[Bowling] Seed eaten (" + chest.gear + "): " + seedsEaten + " so far.", this);
            if (vines != null) vines.GrowNext();
            if (balls != null)
            {
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
        }

        private IEnumerator BeginBalls()
        {
            yield return new WaitForSeconds(ballsDelay);
            if (balls != null && !won && !restored && !balls.Running) balls.Begin();
        }

        // Our chest seeds under Currently Spawned Stuffs (Seed_Bracer for Bracer_Gear, ...).
        private static void DestroySeeds(string gear)
        {
            if (string.IsNullOrEmpty(gear)) return;
            var spawned = Global("CurrentlySpawnedStuffs");
            if (spawned == null) return;
            string prefix = "Seed_" + gear.Replace("_Gear", "");
            foreach (Transform child in spawned.transform)
                if (child != null && child.name.StartsWith(prefix)) Destroy(child.gameObject);
        }

        // ------------------------------------------------------------------ tests

        /// <summary>The oldest pending chest's seed counts as eaten: the chest or seed goes (no item), its gear bool is set.</summary>
        [ContextMenu("Eat The Seed")]
        public void DebugEatSeed()
        {
            if (!started || restored || won) return;
            if (pendingChests.Count == 0)
            {
                Debug.Log("[Bowling] No chest seed waiting.", this);
                return;
            }
            var chest = pendingChests[0];
            if (chest.chest != null) Destroy(chest.chest);
            DestroySeeds(chest.gear);
            if (!string.IsNullOrEmpty(chest.gear) && GameStages.Fsm() != null) GameStages.SetBool(chest.gear, true, this);
            SeedEaten(chest);
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

        /// <summary>Hurts Slimes[slime] (as 3 strikes would).</summary>
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
                Debug.Log("[Bowling] Slime " + slime + " can't be hurt now (phase " + Phase + ").", this);
                return;
            }
            Hit(f, "test");
        }

        [ContextMenu("Hit Slime 0")]
        private void HitFirst() => DebugHit(0);

        /// <summary>Hurts every slime of the phase (the phase ends).</summary>
        [ContextMenu("Next Phase")]
        public void DebugNextPhase()
        {
            if (!started || restored || won) return;
            if (Phase < 1 || Phase > 3)
            {
                Debug.Log("[Bowling] Not in a phase (phase " + Phase + ").", this);
                return;
            }
            foreach (var f in flyers.ToArray())
                if (f.gen == Phase && !f.hurt) Hit(f, "test");
        }

        /// <summary>Jumps to the showdown: the phase-3 slimes (made if needed) lose a wing, the Shield chest drops, they drop.</summary>
        [ContextMenu("Showdown Now")]
        public void DebugShowdown()
        {
            if (!started || restored || won || Phase >= 4 || slimePrefab == null) return;
            if (playJob != null) StopCoroutine(playJob);
            if (phaseJob != null) StopCoroutine(phaseJob);
            playJob = phaseJob = null;
            MeltAll();
            if (balls != null && !balls.Running)
            {
                ballsBegun = true;
                balls.Begin();
            }
            var have = new Flyer[4];
            foreach (var f in flyers.ToArray())
            {
                f.ticket++;
                if (f.job != null) StopCoroutine(f.job);
                f.job = null;
                f.acting = false;
                f.targetLane = -1;
                if (f.slime != null && f.gen == 3 && f.slot >= 0 && f.slot < 4 && have[f.slot] == null) have[f.slot] = f;
                else
                {
                    if (f.slime != null) Destroy(f.slime.gameObject);
                    flyers.Remove(f);
                }
            }
            for (int slot = 0; slot < 4; slot++)
            {
                if (have[slot] != null) continue;
                var at = CirclePoint(slot, 4);
                var slime = Instantiate(slimePrefab, at, Quaternion.identity, transform);
                slime.Setup(ColourOf(3, slot), Pick(sizes, 3, 4.5f), Pick(pitches, 3, 0.75f), Pick(volumes, 3, 0.5f));
                have[slot] = AddFlyer(slime, 3, slot);
            }
            Phase = 3;
            foreach (var f in flyers)
            {
                f.hurt = true;
                if (f.slime != null && f.slime.WingsLeft >= 2) f.slime.LoseWing();
            }
            RefreshSlimes();
            Respace();
            Debug.Log("[Bowling] Test: straight to the showdown.", this);
            phaseJob = StartCoroutine(PhaseEnd(3));
        }

        /// <summary>Wins at once (tests).</summary>
        [ContextMenu("Win Now")]
        public void DebugWin()
        {
            if (!started || restored || won) return;
            Win();
        }

        /// <summary>The slime's current streak (0 if it isn't one of ours).</summary>
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

        private void Win()
        {
            if (won) return;
            won = true;
            Phase = 5;
            Debug.Log("[Bowling] Won.", this);
            StopAllCoroutines();
            StartCoroutine(ToTheRooftop());
        }

        // The slimes leave, everything left goes, any chest gear still missing is given, the seed shows. Once it's eaten
        // (or after Seed Seconds) GAMESTAGES clears what it spawned and brings up the rooftop: this arena stands alone
        // meanwhile (so it isn't cleared from under the player), fades to black, waits for the rooftop, puts the player
        // near the Lid facing it, fades in.
        private IEnumerator ToTheRooftop()
        {
            // Before anything can set The_Seed: GAMESTAGES' Clear Spawned Stuffs must not take the arena.
            transform.SetParent(null, true);
            if (balls != null) balls.StopAndClear();
            MeltAll();
            foreach (var f in flyers) if (f.slime != null) f.slime.Leave();
            flyers.Clear();
            RefreshSlimes();
            EnemyWave.KillAll(this);
            if (BowlingRollAssist.AimFinder == aimFinder) BowlingRollAssist.AimFinder = null;
            onWon?.Invoke();
            GearGuard();

            var oldRoof = Global("Rooftop_Base");

            var seed = SpawnSeed();
            WinSign();
            if (GameStages.Fsm() == null)
            {
                // A test scene: nothing goes on from here; the player may move again.
                Debug.LogWarning("[Bowling] No GAMESTAGES: the arena stays (no rooftop).", this);
                Restore();
                endedSent = true;
                Ended?.Invoke(this, true);
                yield break;
            }

            float autoAt = Time.time + seedSeconds;
            bool given = false;
            while (!GameStages.GetBool(SeedBool))
            {
                if (GameOver()) yield break;
                if (seed == null && !given)
                {
                    // No seed (none set, or gone): the bool is set for them.
                    if (seedPrefab == null) Debug.LogWarning("[Bowling] No seed prefab: GAMESTAGES The_Seed set directly.", this);
                    GameStages.SetBool(SeedBool, true, this);
                    given = true;
                }
                else if (seed != null && !given && Time.time >= autoAt)
                {
                    // Not eaten in time: eaten for them (its Activate Stuffs: the item, the heal, The_Seed).
                    foreach (var seedFsm in seed.GetComponentsInChildren<PlayMakerFSM>())
                    {
                        if (seedFsm.ActiveStateName != SeedWaitState) continue;
                        seedFsm.SendEvent(SeedEatenEvent);
                        given = true;
                        break;
                    }
                }
                yield return null;
            }

            if (rulesSign != null) rulesSign.gameObject.SetActive(false);
            // The seed's sound and shrink, then the black (not over a notice: it would hide it).
            yield return new WaitForSeconds(1f);
            while (Paused()) yield return null;
            var fade = HeadsetFade.Find();
            if (fade != null) fade.FadeOut(0.8f);
            yield return new WaitForSeconds(0.9f);

            // In the black until the new rooftop is there.
            GameObject roof;
            float waited = 0f;
            bool warned = false;
            while (true)
            {
                if (GameOver()) yield break;
                bool down = PlayerDown();
                roof = Global("Rooftop_Base");
                if (!down && roof != null && roof != oldRoof) break;
                if (!down && Paused())
                {
                    // A notice or the menu: shown, not hidden in the black; black again after.
                    if (fade != null) fade.FadeIn(0.3f);
                    while (Paused()) yield return null;
                    if (fade != null && !warned) fade.FadeOut(0.5f);
                }
                else if (!down && !warned && (waited += Time.deltaTime) > roofWaitSeconds)
                {
                    Debug.LogWarning("[Bowling] The rooftop hasn't come " + roofWaitSeconds + " s after the seed (GAMESTAGES is in '" +
                                     GameStages.ActiveState() + "'): waiting on the arena.", this);
                    if (fade != null) fade.FadeIn(0.8f);
                    warned = true;
                }
                yield return null;
            }
            // The rooftop is here: the arena's Fall Edges go off now. They overlap the roof's arms (killing what's there,
            // holing its NavMesh) and the spot by the Lid (a fall right after the move: black again).
            foreach (var edge in GetComponentsInChildren<FallEdge>(true))
                if (edge != null) edge.gameObject.SetActive(false);
            // A moment for the rooftop's animator to place its Lid.
            yield return new WaitForSeconds(0.3f);
            // Not while down, flying from a push, or off the pad in the black: Fall Out Of World would pull them back
            // here after the move. A fall's put-back (or a notice) shows the view: black again.
            var launch = origin != null ? origin.GetComponent<Game.Combat.PlayerLaunch>() : null;
            for (float until = Time.time + 6f; Time.time < until;)
            {
                if (GameOver()) yield break;
                if (!PlayerDown() && Paused())
                {
                    if (fade != null) fade.FadeIn(0.3f);
                    while (Paused()) yield return null;
                    continue;
                }
                if (PlayerDown() || (launch != null && launch.Flying) || !OverStage())
                {
                    yield return null;
                    continue;
                }
                if (fade == null || fade.Alpha >= 0.99f) break;
                fade.FadeOut(0.5f);
                yield return new WaitForSeconds(0.6f);
            }
            roof = Global("Rooftop_Base");
            var (spot, yaw) = RooftopSpot(roof);

            Restore();
            MovePlayer(spot, yaw);
            endedSent = true;
            Ended?.Invoke(this, true);
            if (Running == this) Running = null;
            if (fade != null) fade.FadeIn(0.8f);
            Destroy(gameObject, 0.2f);
        }

        // Before The_Seed: our chest seeds still waiting are eaten for the player (item, heal, gear bool), then any gear
        // bool still false is set (GAMESTAGES' Path 2 ON waits for all 4 and The_Seed; it would stall in the black).
        private void GearGuard()
        {
            var spawned = Global("CurrentlySpawnedStuffs");
            if (spawned != null)
            {
                foreach (var fsm in spawned.GetComponentsInChildren<PlayMakerFSM>())
                {
                    if (fsm == null || fsm.ActiveStateName != SeedWaitState || !IsChestSeed(fsm.transform, spawned.transform)) continue;
                    Debug.Log("[Bowling] " + fsm.gameObject.name + " not eaten: eaten for the player.", this);
                    fsm.SendEvent(SeedEatenEvent);
                }
            }
            pendingChests.Clear();
            if (GameStages.Fsm() == null || gearBools == null) return;
            foreach (var gear in gearBools)
            {
                if (string.IsNullOrEmpty(gear) || GameStages.GetBool(gear)) continue;
                GameStages.SetBool(gear, true, this);
                Debug.LogWarning("[Bowling] " + gear + " was still false at the win (chest skipped?): set so GAMESTAGES goes on.", this);
            }
        }

        // The FSM's object, or one of its parents below Currently Spawned Stuffs, is a "Seed_" (our chest seeds).
        private static bool IsChestSeed(Transform t, Transform spawned)
        {
            for (; t != null && t != spawned; t = t.parent)
                if (t.name.StartsWith("Seed_")) return true;
            return false;
        }

        // Died meanwhile: Game Over has the player; the arena (no longer under Currently Spawned Stuffs) just goes.
        private bool GameOver()
        {
            if (GameStages.ActiveState() != "Game Over") return false;
            Destroy(gameObject);
            return true;
        }

        // The seed 0.8 m in front of the head, 1 m up, kept on the pad. A child of the arena, so it goes with it.
        private GameObject SpawnSeed()
        {
            if (seedPrefab == null) return null;
            var frame = centre != null ? centre : transform;
            var at = HeadFloor() + HeadForward() * 0.8f;
            var local = frame.InverseTransformPoint(at);
            float keep = Mathf.Max(0.2f, padHalfSize - 0.3f);
            local.x = Mathf.Clamp(local.x, -keep, keep);
            local.z = Mathf.Clamp(local.z, -keep, keep);
            at = frame.TransformPoint(local);
            at.y = (origin != null ? origin.transform.position.y : transform.position.y) + 1f;
            return Instantiate(seedPrefab, at, Quaternion.identity, transform);
        }

        // The rules sign becomes the win sign, 2.5 m in front of the head, facing it.
        private void WinSign()
        {
            if (rulesSign == null) return;
            var forward = HeadForward();
            var at = HeadFloor() + forward * 2.5f;
            at.y = (origin != null ? origin.transform.position.y : transform.position.y) + 1.8f;
            rulesSign.transform.SetPositionAndRotation(at, Quaternion.LookRotation(forward));
            rulesSign.text = winText;
            rulesSign.gameObject.SetActive(true);
            rulesShowing = false;
        }

        // Lid Stand Off metres from the rooftop's Lid (or its middle), towards the south gate, on its floor, facing it.
        private (Vector3 spot, float yaw) RooftopSpot(GameObject roof)
        {
            var roofAt = roof != null ? roof.transform.position : Vector3.zero;
            var lid = Global("Lid");
            var middle = lid != null && roof != null && lid.transform.IsChildOf(roof.transform) ? lid.transform.position : roofAt;
            var back = roof != null ? -roof.transform.forward : Vector3.back;
            back.y = 0f;
            if (back.sqrMagnitude < 0.0001f) back = Vector3.back;
            var spot = middle + back.normalized * lidStandOff;
            spot.y = roofAt.y + 0.03f;
            if (Physics.Raycast(spot + Vector3.up * 3f, Vector3.down, out var hit, 6f, ~0, QueryTriggerInteraction.Ignore) &&
                Mathf.Abs(hit.point.y - roofAt.y) < 1.5f)
                spot.y = hit.point.y;
            var look = middle - spot;
            look.y = 0f;
            float yaw = look.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(look).eulerAngles.y : 0f;
            return (spot, yaw);
        }

        // ------------------------------------------------------------------ the player and the world

        // Thumbstick and arm-swing moving off (each one's own state remembered once, given back by Restore).
        private void MovementOff()
        {
            foreach (var mover in FindObjectsByType<ContinuousMoveProvider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
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
                RenderSettings.fogStartDistance = fog.x;
                RenderSettings.fogEndDistance = fog.y;
                fogSet = true;
                return;
            }
            if (!fogSet) return;
            RenderSettings.fogStartDistance = fogStartWas;
            RenderSettings.fogEndDistance = fogEndWas;
            fogSet = false;
        }

        // Everything given back (once): moving, the fall's return spot, the fog, the roll assist's aim, the flyers.
        private void Restore()
        {
            if (!started || restored) return;
            restored = true;
            lockMoves = false;
            if (inventoryHooked && PlayerInventory.Instance != null) PlayerInventory.Instance.Changed -= OnInventory;
            inventoryHooked = false;
            foreach (var set in sets)
            {
                if (set == null) continue;
                set.OnResolved -= OnSetResolved;
                set.OnCleared -= OnSetCleared;
            }
            if (BowlingRollAssist.AimFinder == aimFinder) BowlingRollAssist.AimFinder = null;
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

        private float HeadYaw()
        {
            if (head == null) return 0f;
            var f = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            return f.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(f).eulerAngles.y : 0f;
        }

        // The head over Spot, the feet on its floor, facing Yaw.
        private void MovePlayer(Vector3 spot, float yaw)
        {
            if (origin == null) return;
            var body = origin.GetComponent<CharacterController>();
            if (body != null) body.enabled = false;
            var launch = origin.GetComponent<Game.Combat.PlayerLaunch>();
            if (launch != null) launch.Stop();
            origin.RotateAroundCameraUsingOriginUp(Mathf.DeltaAngle(HeadYaw(), yaw));
            var feet = HeadFloor();
            var to = origin.transform.position + new Vector3(spot.x - feet.x, 0f, spot.z - feet.z);
            to.y = spot.y;
            origin.transform.position = to;
            Physics.SyncTransforms();
            if (body != null) body.enabled = true;
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

        private static readonly RaycastHit[] floorHits = new RaycastHit[8];

        // The head is over this arena's floor (the pad or a lane), not off its edge or mid fall.
        private bool OverStage()
        {
            if (head == null) return true;
            int count = Physics.RaycastNonAlloc(head.position, Vector3.down, floorHits, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (floorHits[i].collider != null && floorHits[i].collider.transform.IsChildOf(transform)) return true;
            }
            return false;
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

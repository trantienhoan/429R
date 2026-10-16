using System.Collections.Generic;
using Game.Combat;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.AI;
using FsmFloat = HutongGames.PlayMaker.FsmFloat;
using FsmVariables = HutongGames.PlayMaker.FsmVariables;

namespace Game.Enemies
{
    /// <summary>
    /// The Pumpkin boss (pumpkin_boss_Z). It laughs non-stop from the moment it appears, and breaks the room it comes
    /// up in (the bedroom or the living room). It starts as just its head sitting on the floor; every hit the player
    /// lands raises it one pumpkin out of the ground, and after the third one its arms and body come out, it slams
    /// both arms down and then fights; hurt a lot very fast, it stands there dizzy for a few seconds. Left alone it
    /// wakes itself up once, after Wake Up Seconds. Below True Form Below of its health it climbs out on two legs,
    /// grows and runs faster. How it fights (both forms):
    /// - the player too close for its punches (Too Close): it spins its cyclone to push them away (then it's dizzy);
    /// - in reach: one of three punches, left, right or both arms;
    /// - out of reach after a few punches (Punches Before Throw), or far away (further than it runs in Far Seconds,
    ///   after a clap): a combo of 1-5 pumpkins full of bugs, left hand then right. A hit slows the player down and it
    ///   chases them; dodging them all makes it vomit bugs to back it up, or clap for the player (Vomit After Dodge);
    /// - true form: after a few punches miss in a row (Misses Before Body Slam), a jump with a body slam.
    /// Hits never stop it. Its hits throw the player (Player Launch) and only hurt when they really land. Every health
    /// it loses drops a pumpkin to throw back at it.
    /// When it dies it shrinks back into one big pumpkin head that bursts like the other pumpkins, and sets
    /// GAMESTAGES' BOSS flag.
    /// Hits on it come from Enemy Hit Damage, which lowers the "health" of its (switched off) Health FSM.
    /// Tools > 429 Game > Enemies > Set Up Pumpkin Boss builds it.
    /// </summary>
    [DisallowMultipleComponent]
    public class PumpkinBossBrain : MonoBehaviour
    {
        private enum Phase { Head, Rising, Upper, Transforming, True, Dying }

        private enum Act { None, Taunt, PunchL, PunchR, Slam, Throw, JumpSlam, Vomit, Cyclone, Dizzy, Clap }

        [Header("Parts (set by the setup menu)")]
        [SerializeField] private Animator animator;
        [Tooltip("The child that holds the model: moved up and down for how far the boss is out of the ground, and scaled.")]
        [SerializeField] private Transform model;
        [SerializeField] private Transform head;
        [SerializeField] private Transform jaw;
        [SerializeField] private Transform handL;
        [SerializeField] private Transform handR;

        [Header("Health")]
        [SerializeField] private string healthFsm = "Health";
        [SerializeField] private string healthVariable = "health";
        [Tooltip("It goes true form below this share of its health.")]
        [Range(0.05f, 0.95f)]
        [SerializeField] private float trueFormBelow = 0.4f;

        [Header("Waking up")]
        [Tooltip("Hits it takes as a head before its arms and body come out.")]
        [Min(1)]
        [SerializeField] private int revealHits = 3;
        [Tooltip("With no hit for this long as a head, it wakes itself up (once).")]
        [Min(1f)]
        [SerializeField] private float wakeUpSeconds = 33f;
        [Tooltip("How far the model sits under the floor (model units, before its scale): head only, then one more " +
                 "pumpkin out per hit; the last one is the upper body, with the vines under its bottom pumpkin kept " +
                 "under the floor.")]
        [SerializeField] private float[] buried = { 1.72f, 1.41f, 1.22f, 1.06f };

        [Header("Size and speed")]
        [SerializeField] private float headScale = 1.7f;
        [SerializeField] private float upperScale = 1.7f;
        [SerializeField] private float trueScale = 2.1f;
        [Tooltip("Running speed in true form, in m/s. The upper body moves at Upper Speed of it.")]
        [Min(0.1f)]
        [SerializeField] private float trueSpeed = 5f;
        [Range(0.1f, 1f)]
        [SerializeField] private float upperSpeed = 0.6f;

        [Header("Getting dizzy (upper body)")]
        [Tooltip("It's stunned once it loses this share of its health in quick succession (0.0294 of 429 = about 13 " +
                 "health). What it has lost fades away by Dizzy Fade Per Second, so slow hits never add up.")]
        [Range(0.02f, 1f)]
        [SerializeField] private float dizzyAfterDamage = 0.0294f;
        [Range(0f, 0.5f)]
        [SerializeField] private float dizzyFadePerSecond = 0.0029f;
        [Tooltip("How long it stands there dizzy (being_hit), in seconds.")]
        [Min(0.5f)]
        [SerializeField] private float dizzySeconds = 3f;
        [Tooltip("After coming round, it can't be made dizzy again for this many seconds.")]
        [Min(0f)]
        [SerializeField] private float dizzyCooldown = 20f;

        [Header("Hitting the player")]
        [Tooltip("Sent to the player's PlayerHealth FSM when an upper-body hit lands (35-45) / a true-form hit (55-70).")]
        [SerializeField] private string upperHitEvent = "PumpkinBossHit";
        [SerializeField] private string trueHitEvent = "PumpkinBossHitHeavy";
        [SerializeField] private string playerHealthFsm = "PlayerHealth";
        [Tooltip("Throws: x = how high, y = how far, in metres.")]
        [SerializeField] private Vector2 slamLaunch = new(3.2f, 2.5f);
        [SerializeField] private Vector2 punchLaunch = new(1.0f, 3.0f);
        [SerializeField] private Vector2 jumpSlamLaunch = new(3.6f, 3.0f);
        [Tooltip("The cyclone pushes a player who came too close away: it throws them highest and furthest of all.")]
        [SerializeField] private Vector2 cycloneLaunch = new(4.5f, 7f);
        [Tooltip("How long the whole cyclone lasts, in seconds: the length of its clip (wind-up, spin, hard stop).")]
        [SerializeField] private float cycloneSeconds = 3f;
        [Tooltip("After a cyclone it can't spin another for this many seconds (nor in the first half of that after it " +
                 "turns into its true form); too close meanwhile, it punches.")]
        [Min(0f)]
        [SerializeField] private float cycloneCooldown = 8f;
        [Tooltip("After its cyclone it's spun itself dizzy (dizzy_true) for this many seconds: the player's chance " +
                 "to hit back. 0 = never dizzy.")]
        [Min(0f)]
        [SerializeField] private float cycloneDizzySeconds = 3f;

        [Header("Fighting")]
        [Tooltip("Closer than this (times its size) its punches can't land: it spins its cyclone to push the player away.")]
        [Min(0f)] [SerializeField] private float tooClose = 1.1f;
        [Tooltip("Further away than it can run in this many seconds, it stands clapping; still that far when the clap " +
                 "is nearly over, it throws pumpkins.")]
        [Min(0.5f)] [SerializeField] private float farSeconds = 2f;
        [Tooltip("Pumpkins it throws one after another, left hand then right: a random number from x to y.")]
        [SerializeField] private Vector2Int throwCombo = new(1, 5);
        [Tooltip("Bugs in each pumpkin of a throw combo.")]
        [Min(0)] [SerializeField] private int bugsPerThrownPumpkin = 1;
        [Tooltip("Seconds from one pumpkin leaving its hand to the next throw starting.")]
        [Min(0.05f)] [SerializeField] private float throwGap = 0.3f;
        [Tooltip("The Animator state for a left-hand throw. Without such a state its left punch throws instead.")]
        [SerializeField] private string leftThrowState = "throw_L";
        [Tooltip("After this many punches, a player who got out of its reach gets pumpkins thrown at them.")]
        [Min(1)] [SerializeField] private int punchesBeforeThrow = 2;
        [Tooltip("True form: after this many punches in a row miss, it jumps and body-slams, then punches again.")]
        [Min(1)] [SerializeField] private int missesBeforeBodySlam = 3;
        [Tooltip("How much a punching arm tilts to point at the player's chest (0 = the clip as it is, 1 = straight at " +
                 "them). The punch clips were made for the upper body, so in the true form, standing on its legs, they " +
                 "fly over the player's head without it. Aimed while it winds up; once the fist flies the player can " +
                 "still step aside.")]
        [Range(0f, 1f)] [SerializeField] private float punchAim = 1f;
        [Tooltip("After a throw combo it chases the player at least this long before it claps and throws again.")]
        [Min(0f)] [SerializeField] private float chaseSeconds = 4f;
        [Tooltip("The player dodged every pumpkin: the chance it vomits bugs to back it up; otherwise it claps for them.")]
        [Range(0f, 1f)] [SerializeField] private float vomitAfterDodge = 0.5f;

        [Header("Bugs")]
        [Tooltip("Small bugs it vomits and that come out of its thrown pumpkin; one at random each time.")]
        [SerializeField] private GameObject[] bugs = System.Array.Empty<GameObject>();
        [Tooltip("Bugs per vomit (when the player dodged all its pumpkins).")]
        [Min(0)] [SerializeField] private int vomitBugs = 6;
        [Tooltip("As it turns into its true form it calls its bugs: a vomit of this many, right away (if the game isn't " +
                 "already full of enemies). 0 = none.")]
        [Min(0)] [SerializeField] private int trueFormVomitBugs = 6;
        [Tooltip("After each retch its bugs come out of the black smoke this many seconds later, once the smoke is down.")]
        [Min(0f)] [SerializeField] private float vomitBugDelay = 0.5f;
        [Tooltip("The pumpkin full of bugs it throws (Bug Pumpkin).")]
        [SerializeField] private GameObject bugPumpkin;

        [Header("Pumpkins to throw back")]
        [Tooltip("Dropped for every health it loses.")]
        [SerializeField] private GameObject pumpkinDrop;
        [Min(1)] [SerializeField] private int maxDropsAround = 10;

        [Header("Smoke and dust")]
        [Tooltip("Its blasts where it slams the ground or a blow lands, and its black vomit smoke (Pumpkin Boss Fx; " +
                 "Tools > 429 Game > Enemies > Build Pumpkin Boss Smoke FX sets it up).")]
        [SerializeField] private PumpkinBossFx fx;
        [Tooltip("The little puff when it's hit as a head, gets dizzy, or bursts at the end.")]
        [SerializeField] private GameObject smoke;

        [Header("Dying")]
        [Tooltip("What its big head bursts into (the pieces of a broken pumpkin).")]
        [SerializeField] private GameObject burstInto;
        [SerializeField] private string gameStagesBool = "BOSS";
        [SerializeField] private string gameStagesEvent = "Win";

        [Header("Breaking the room")]
        [Tooltip("GAMESTAGES' global variables holding the rooms. When it appears it breaks whichever is standing: the " +
                 "walls fall away and the room is removed (there's ground under them).")]
        [SerializeField] private string[] roomVariables = { "Bedroom_Base", "Livingroom_Base" };
        [Tooltip("Seconds from the walls starting to fall until the room is removed (the fall-away clips last 3.3 s).")]
        [Min(0f)]
        [SerializeField] private float roomGoneAfter = 3.5f;

        [Header("Sounds")]
        [Tooltip("It laughs non-stop until it dies: one of these after another, never the same twice in a row.")]
        [SerializeField] private AudioClip[] laughs = System.Array.Empty<AudioClip>();
        [Tooltip("Seconds of quiet between two laughs.")]
        [Min(0f)]
        [SerializeField] private float laughGap = 0.1f;
        [Range(0f, 1f)]
        [SerializeField] private float laughVolume = 1f;
        [SerializeField] private AudioClip attackSound;
        [SerializeField] private AudioClip hurtSound;
        [SerializeField] private AudioClip roarSound;

        // Animator states and their timing (seconds): when the hit lands, when the move is over.
        private const string Laugh = "laugh", GetHit = "gethit", Come = "come", Move = "move", Slam = "attack",
            Clap = "clap", BeingHit = "being_hit", PunchRState = "punch_R", PunchLState = "punch_L",
            TransformState = "transform", Run = "run", IdleTrue = "idle_true", ThrowState = "throw", JumpSlamState = "jump_slam",
            VomitState = "vomit", CycloneState = "cyclone", DizzyTrue = "dizzy_true", Death = "death";

        // When the vomit clip retches (seconds in: frames 38, 55, 76 and 84 at 60 fps); the bugs come up then.
        private static readonly float[] VomitRetches = { 0.62f, 0.90f, 1.25f, 1.38f };

        // The cyclone clip winds up until 0.5 s, spins seven times until 2.5 s, then stops dead.
        private const float CycloneSpinFrom = 0.5f, CycloneSpinTo = 2.5f;

        private readonly List<GameObject> drops = new();
        private readonly Dictionary<Transform, Vector3> fistTracked = new();
        private readonly List<PlayMakerFSM> fsmBuffer = new();
        private NavMeshAgent agent;
        private AudioSource speaker;
        private AudioSource laughSpeaker;
        private int lastLaugh = -1;
        private float nextLaugh;
        private bool actImpact;
        private FsmFloat health;
        private float maxHealth;
        private float lastHealth;
        private Phase phase = Phase.Head;
        private Act act = Act.None;
        private float actStarted;
        private float actUntil;
        private bool actHit;
        private float nextAct;
        private float nextFlinch;
        private float daze;
        private float nextDizzy;
        private int hitsTaken;
        private float lastHitAt;
        private bool wokeItself;
        private float phaseUntil;
        private float targetBuried;
        private float targetScale;
        private Vector3 jumpFrom, jumpTo;
        private float nextCyclone;
        private int bugsLeft;
        private int retchesDone;
        // A throw combo: throws still to come, the hand of the next one, the one under way, the pumpkins in the air.
        private int throwsLeft;
        private bool throwLeftNext;
        private Transform throwHand;
        private float throwRelease;
        private int pumpkinsOut;
        private bool comboHit;
        private bool waitingForPumpkins;
        private float pumpkinsWaitUntil;
        // Punches since its last throws, punches missed in a row, and how long it keeps chasing whatever happens.
        private int spreePunches;
        private int spreeMisses;
        private float chaseUntil;
        // The clap is the far-away one (still far when it's nearly over: it throws).
        private bool clapToThrow;
        // When its last move ended: chasing a player it can't reach for long, it throws.
        private float idleSince;
        // Sinking into (or climbing out of) the ground fast, for the true form's cyclone.
        private bool quickBury;
        // Moments of the move under way that already had their blast (Once); cleared when a move starts.
        private int marks;
        private const int MarkTakeOff = 1, MarkFistR = 2, MarkFistL = 4, MarkHardStop = 8, MarkEmerge = 16;
        private float risingStarted, transformStarted, nextScrape;
        // Vomited bugs on their way out of the smoke: when each comes out, and where (the middle of the black cloud).
        private readonly List<float> bugsDue = new();
        private Vector3 vomitSpot;
        private const float VomitAhead = 2.5f;
        // The upper arms (tilted to aim a punch) and the tilt of the punch under way, in degrees down.
        private Transform upperArmL, upperArmR;
        private float aimPitch;
        // The fist flies out at this many seconds into a punch clip; it's out until about Punch Hit Until.
        private const float PunchRelease = 0.76f, PunchHitUntil = 1.4f;
        private string playing = "";
        private XROrigin player;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            speaker = GetComponent<AudioSource>();
            if (fx == null) fx = GetComponent<PumpkinBossFx>();
            // Its laughing has its own speaker, so yelps and roars play over it instead of cutting it off.
            if (speaker != null)
            {
                laughSpeaker = gameObject.AddComponent<AudioSource>();
                laughSpeaker.playOnAwake = false;
                laughSpeaker.outputAudioMixerGroup = speaker.outputAudioMixerGroup;
                laughSpeaker.spatialBlend = speaker.spatialBlend;
                laughSpeaker.rolloffMode = speaker.rolloffMode;
                laughSpeaker.minDistance = speaker.minDistance;
                laughSpeaker.maxDistance = speaker.maxDistance;
                laughSpeaker.priority = speaker.priority;
                laughSpeaker.dopplerLevel = speaker.dopplerLevel;
                laughSpeaker.spread = speaker.spread;
            }
            foreach (var fsm in GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName != healthFsm) continue;
                if (!fsm.Fsm.Initialized) fsm.Fsm.Init(fsm);
                health = fsm.FsmVariables.FindFsmFloat(healthVariable);
            }
            foreach (var bone in GetComponentsInChildren<Transform>(true))
            {
                if (bone.name == "UpperArm.L") upperArmL = bone;
                else if (bone.name == "UpperArm.R") upperArmR = bone;
            }
        }

        // A punch's arm tilts to point at the player's chest (after the Animator has posed it this frame).
        private void LateUpdate()
        {
            if (punchAim <= 0f || player == null || (act != Act.PunchL && act != Act.PunchR)) return;
            var arm = act == Act.PunchL ? upperArmL : upperArmR;
            if (arm == null) return;
            float t = Time.time - actStarted;
            if (t < PunchRelease) aimPitch = PitchTo(arm);
            // In while it winds up, out as the arm comes back.
            float w = punchAim * Mathf.Clamp01(Mathf.Min((t - 0.35f) / 0.3f, (1.6f - t) / 0.3f));
            if (w > 0f) arm.rotation = Quaternion.AngleAxis(aimPitch * w, transform.right) * arm.rotation;
        }

        // Degrees down from level for a punch from this shoulder to reach the player's chest.
        private float PitchTo(Transform shoulder)
        {
            float chest = player.Camera.transform.position.y - 0.35f;
            float across = Flat(PlayerFloor() - shoulder.position);
            return Mathf.Atan2(shoulder.position.y - chest, Mathf.Max(0.5f, across)) * Mathf.Rad2Deg;
        }

        private void Start()
        {
            maxHealth = lastHealth = health != null ? Mathf.Max(1f, health.Value) : 429f;
            targetBuried = buried.Length > 0 ? buried[0] : 0f;
            targetScale = headScale;
            if (model != null) { model.localScale = Vector3.one * headScale * 0.1f; SetBuried(targetBuried + 1f, model.localScale.x); }
            lastHitAt = Time.time;
            Stop();
            PlayState(Laugh);
            LaughNow();
            if (smoke != null) EffectPool.Warm(smoke);
            // It bursts up out of the floor (still tiny: the blast is the size of its head).
            GroundBlast(transform.position, PumpkinBossFx.Blast.Big, headScale);
            BreakRooms();
            EnemyHitDamage.AnyHit += OnAnyHit;
        }

        private void OnDestroy() => EnemyHitDamage.AnyHit -= OnAnyHit;

        private void OnAnyHit(EnemyHitDamage enemy, HitResult result)
        {
            if (enemy == null || enemy.gameObject != gameObject || phase == Phase.Dying) return;
            lastHitAt = Time.time;
            if (phase == Phase.Head)
            {
                hitsTaken++;
                PlayState(GetHit, true);
                PlaySound(hurtSound);
                Puff();
                int step = Mathf.Min(hitsTaken, buried.Length - 1);
                targetBuried = buried[step];
                if (hitsTaken >= revealHits) StartRising();
                return;
            }
            // Hits never stop it: between bigger moves it only yelps now and then. It reels only when dizzy (the upper
            // body from too much damage, see TrackHealth; the true form after its own cyclone).
            if (act != Act.None && act != Act.Taunt && act != Act.Dizzy) return;
            if (Time.time < nextFlinch || phase == Phase.Transforming || phase == Phase.Rising) return;
            nextFlinch = Time.time + 1.8f;
            PlaySound(hurtSound);
        }

        private void Update()
        {
            KeepLaughing();
            if (!FindPlayer()) return;
            TrackHealth();
            UpdateModel();
            switch (phase)
            {
                case Phase.Head: UpdateHead(); break;
                case Phase.Rising: UpdateRising(); break;
                case Phase.Upper: UpdateFighting(false); break;
                case Phase.Transforming: UpdateTransforming(); break;
                case Phase.True: UpdateFighting(true); break;
                case Phase.Dying: UpdateDying(); break;
            }
        }

        // ---------- health ----------

        private void TrackHealth()
        {
            if (health == null || phase == Phase.Dying) return;
            float now = health.Value;
            daze = Mathf.Max(0f, daze - dizzyFadePerSecond * Time.deltaTime);
            if (now < lastHealth)
            {
                int lost = Mathf.Max(1, Mathf.RoundToInt(lastHealth - now));
                for (int i = 0; i < lost; i++) DropPumpkin();
                if (act != Act.Dizzy && Time.time >= nextDizzy) daze += (lastHealth - now) / maxHealth;
            }
            lastHealth = now;
            if (now <= 0f) { StartDying(); return; }
            if (phase != Phase.Upper) return;
            if (now <= maxHealth * trueFormBelow && act == Act.None) StartTransform();
            // Too much damage too fast makes it dizzy, but only between moves: a punch or slam under way always finishes.
            else if (daze >= dizzyAfterDamage && act == Act.None) StartDizzy(BeingHit, dizzySeconds);
        }

        // Whatever it was doing, it stands there swaying, arms hanging, for a few seconds: being_hit for the upper body
        // (too much damage too fast), dizzy_true for the true form (spun itself dizzy with its cyclone).
        private void StartDizzy(string state, float seconds)
        {
            daze = 0f;
            Begin(Act.Dizzy, state, seconds);
            PlaySound(hurtSound);
            Puff();
        }

        private void DropPumpkin()
        {
            drops.RemoveAll(d => d == null);
            if (pumpkinDrop == null || drops.Count >= maxDropsAround) return;
            var around = Random.insideUnitCircle.normalized * Random.Range(1f, 2.2f);
            var at = transform.position + new Vector3(around.x, 1.6f, around.y);
            var drop = Instantiate(pumpkinDrop, at, Random.rotation, transform.parent);
            if (drop.TryGetComponent(out Rigidbody body))
                body.linearVelocity = new Vector3(around.x, 2.5f, around.y) * 1.2f;
            drops.Add(drop);
        }

        // ---------- head phase ----------

        private void UpdateHead()
        {
            Face(player.Camera.transform.position, 90f);
            if (!wokeItself && Time.time - lastHitAt >= wakeUpSeconds)
            {
                // Bored of waiting: it punches itself awake, once.
                wokeItself = true;
                PlayState(GetHit, true);
                LaughNow();
                Puff();
                StartRising();
                return;
            }
            if (playing == GetHit && Time.time >= actUntil) PlayState(Laugh);
        }

        private void StartRising()
        {
            phase = Phase.Rising;
            targetBuried = buried[buried.Length - 1];
            targetScale = upperScale;
            PlayState(Come, true);
            LaughNow();
            phaseUntil = Time.time + 2.3f;
            risingStarted = Time.time;
            marks = 0;
            // Its body heaves up out of the floor.
            GroundBlast(transform.position, PumpkinBossFx.Blast.Medium);
        }

        // Coming out ("come"): its arms rise out of the ground and its fists slap the floor either side (right at
        // 1.92 s, left at 2.05 s), then it fights.
        private void UpdateRising()
        {
            float t = Time.time - risingStarted;
            if (t >= 1.92f && Once(MarkFistR)) { GroundBlast(FistPoint(handR), PumpkinBossFx.Blast.Medium); SendShake("ShakeSmall"); }
            if (t >= 2.05f && Once(MarkFistL)) GroundBlast(FistPoint(handL), PumpkinBossFx.Blast.Medium);
            if (Time.time >= phaseUntil) EnterUpper();
        }

        // Out of the ground it comes out swinging: a two-arm slam on the spot first, then the chase.
        private void EnterUpper()
        {
            phase = Phase.Upper;
            // Hits it took as a head or while rising don't count towards its first dizzy spell.
            daze = 0f;
            agent.speed = trueSpeed * upperSpeed;
            Begin(Act.Slam, Slam, 2.75f);
            PlaySound(attackSound);
        }

        // ---------- fighting ----------

        private void UpdateFighting(bool trueForm)
        {
            if (act != Act.None) { UpdateAct(trueForm); return; }
            var target = PlayerFloor();
            float distance = Flat(target - transform.position);
            // Its pumpkins are still in the air: it watches where they land.
            if (waitingForPumpkins)
            {
                Stop();
                Face(target, 240f);
                PlayState(trueForm ? IdleTrue : Move);
                if (comboHit || pumpkinsOut <= 0 || Time.time >= pumpkinsWaitUntil) AfterThrows();
                return;
            }
            // The upper body's rubber-arm punch reaches far, so it stops well away and punches.
            float reach = Reach(trueForm);
            if (Time.time >= nextAct && ChooseAttack(trueForm, distance, reach)) return;
            if (distance > reach * 0.85f)
            {
                agent.isStopped = false;
                agent.stoppingDistance = reach * 0.6f;
                agent.SetDestination(target);
                PlayState(trueForm ? Run : Move);
            }
            else
            {
                Stop();
                Face(target, 240f);
                PlayState(trueForm ? IdleTrue : Move);
            }
        }

        // What it does next; false: it just chases the player (or stands facing them, in reach).
        private bool ChooseAttack(bool trueForm, float distance, float reach)
        {
            // Too close for its punches to land: its cyclone pushes the player away.
            if (distance <= tooClose * Size && Time.time >= nextCyclone)
            {
                StartCyclone();
                return true;
            }
            // True form: its punches keep missing, so it jumps and body-slams, then punches again.
            if (trueForm && spreeMisses >= missesBeforeBodySlam)
            {
                spreeMisses = 0;
                StartJumpSlam();
                return true;
            }
            if (distance <= reach)
            {
                StartPunch(distance);
                return true;
            }
            // The player got out of reach after a few punches: pumpkins, at once.
            if (spreePunches >= punchesBeforeThrow)
            {
                StartThrows();
                return true;
            }
            // Far away (further than it runs in Far Seconds): it claps, and throws if they're still that far.
            if (Time.time >= chaseUntil && distance > FormSpeed * farSeconds)
            {
                StartClap(true);
                return true;
            }
            // Chasing a player it can't catch for that long: pumpkins.
            if (Time.time >= chaseUntil && Time.time - idleSince > chaseSeconds)
            {
                StartThrows();
                return true;
            }
            return false;
        }

        private float Size => model != null ? model.localScale.x : 1f;

        // Its rubber-arm punches reach far in both forms (the fist flies out to about 3.2 times its size).
        private float Reach(bool trueForm) => 2.6f * Size;

        // Where its two-arm slam comes down, in front of it (times its size): only there does it slam.
        private const float SlamFrom = 1.7f, SlamTo = 2.5f;

        private float FormSpeed => phase == Phase.True ? trueSpeed : trueSpeed * upperSpeed;

        // One of its three punches: left, right, or both arms (the slam, only when the player stands where it lands).
        private void StartPunch(float distance)
        {
            bool canSlam = distance >= SlamFrom * Size && distance <= SlamTo * Size;
            float r = Random.value * (canSlam ? 3f : 2f);
            if (r < 1f) Begin(Act.PunchL, PunchLState, 1.6f);
            else if (r < 2f) Begin(Act.PunchR, PunchRState, 1.6f);
            else Begin(Act.Slam, Slam, 2.75f);
            spreePunches++;
            PlaySound(attackSound);
        }

        // ---------- throwing pumpkins ----------

        // A combo of Throw Combo pumpkins, left hand then right.
        private void StartThrows()
        {
            throwsLeft = Random.Range(Mathf.Max(1, throwCombo.x), Mathf.Max(throwCombo.x, throwCombo.y) + 1);
            throwLeftNext = true;
            pumpkinsOut = 0;
            comboHit = false;
            spreePunches = 0;
            spreeMisses = 0;
            NextThrow();
        }

        private void NextThrow()
        {
            throwsLeft--;
            bool left = throwLeftNext;
            throwLeftNext = !left;
            throwHand = left ? handL : handR;
            if (left && !HasState(leftThrowState))
            {
                // No left-hand throw clip: its left punch swings and lets the pumpkin go.
                Begin(Act.Throw, PunchLState, 1.6f);
                throwRelease = 0.7f;
            }
            else
            {
                Begin(Act.Throw, left ? leftThrowState : ThrowState, 1.5f);
                throwRelease = 0.65f;
            }
            PlaySound(attackSound);
        }

        // All thrown: a hit slowed the player, so it chases them; dodged them all, it vomits bugs to back it up or
        // claps for the player, then chases.
        private void AfterThrows()
        {
            waitingForPumpkins = false;
            if (!comboHit)
            {
                if (Random.value < vomitAfterDodge && CanVomit()) StartVomit(vomitBugs);
                else StartClap(false);
            }
            chaseUntil = Mathf.Max(Time.time, actUntil) + chaseSeconds;
        }

        private void StartClap(bool thenThrow)
        {
            Begin(Act.Clap, Clap, 1.5f);
            clapToThrow = thenThrow;
        }

        private bool HasState(string state)
        {
            return animator != null && !string.IsNullOrEmpty(state) && animator.HasState(0, Animator.StringToHash(state));
        }

        private void Begin(Act what, string state, float seconds)
        {
            fistTracked.Clear();
            marks = 0;
            act = what;
            actStarted = Time.time;
            actUntil = Time.time + seconds;
            actHit = false;
            actImpact = false;
            Stop();
            PlayState(state, true);
        }

        private void UpdateAct(bool trueForm)
        {
            float t = Time.time - actStarted;
            var target = PlayerFloor();
            switch (act)
            {
                case Act.PunchL:
                case Act.PunchR:
                    // It charges (aiming) and lets fly at about 0.8 s; the stretched fist flies out to its full reach
                    // (it passes a player 3.5 m away at about 1.1 s).
                    if (t < 0.7f) Face(target, 300f);
                    if (t >= PunchRelease && t <= PunchHitUntil) TryHit(act == Act.PunchR ? handR : handL, 0.32f, punchLaunch, false);
                    break;
                case Act.Slam:
                    if (t < 1.3f) Face(target, 200f);
                    if (t >= 1.5f && t <= 1.8f) { TryHit(handL, 0.42f, slamLaunch, false); TryHit(handR, 0.42f, slamLaunch, false); }
                    // The fists smash into the floor: a blast under each and a shake. Standing on its legs (true form)
                    // the clip's fists come down short of the floor: the blast goes off at the fist, dust below it.
                    if (!actImpact && t >= 1.58f)
                    {
                        actImpact = true;
                        foreach (var fist in new[] { handL, handR })
                        {
                            if (fist == null) continue;
                            var at = FistPoint(fist);
                            if (at.y - transform.position.y < 1f) { GroundBlast(at, PumpkinBossFx.Blast.Big); continue; }
                            if (fx != null) fx.Hit(at, at + Vector3.up, Size);
                            GroundBlast(at, PumpkinBossFx.Blast.Small);
                        }
                        SendShake("ShakeMed");
                    }
                    break;
                case Act.Throw:
                    Face(target, 260f);
                    // A pumpkin already hit: the combo's over, this one isn't thrown.
                    if (comboHit && !actHit) { throwsLeft = 0; actUntil = Time.time; break; }
                    if (!actHit && t >= throwRelease) { actHit = true; ThrowBugPumpkin(throwHand); }
                    // The next throw follows at once; a pumpkin that hit ends the combo (it chases the slowed player).
                    if (actHit && t >= throwRelease + throwGap)
                    {
                        if (comboHit) throwsLeft = 0;
                        if (throwsLeft > 0) { NextThrow(); return; }
                    }
                    break;
                case Act.Clap:
                    Face(target, 240f);
                    // Nearly over: still far away, the player gets pumpkins; otherwise it chases them.
                    if (clapToThrow && !actHit && t >= 1.2f)
                    {
                        actHit = true;
                        if (Flat(target - transform.position) > FormSpeed * farSeconds) { StartThrows(); return; }
                        chaseUntil = Time.time + chaseSeconds;
                    }
                    break;
                case Act.JumpSlam:
                    UpdateJumpSlam(t);
                    break;
                case Act.Vomit:
                    // It turns to where it's going to throw up (away from a player standing close).
                    if (t < 0.6f) Face(vomitSpot, 200f);
                    if (fx != null) fx.VomitMouth(MouthPoint(), MouthDirection(), transform.position.y);
                    // The bugs come up with its retches, shared out between them, and crawl out of the black smoke
                    // a moment later.
                    if (retchesDone < VomitRetches.Length && t >= VomitRetches[retchesDone])
                    {
                        int retchesLeft = VomitRetches.Length - retchesDone++;
                        int n = Mathf.Min(bugsLeft, Mathf.CeilToInt(bugsLeft / (float)retchesLeft));
                        bugsLeft -= n;
                        for (int i = 0; i < n; i++) bugsDue.Add(Time.time + vomitBugDelay + i * 0.08f);
                        Retch();
                    }
                    for (int i = bugsDue.Count - 1; i >= 0; i--)
                    {
                        if (Time.time < bugsDue[i]) continue;
                        bugsDue.RemoveAt(i);
                        SpitBug();
                    }
                    break;
                case Act.Cyclone:
                    // Winds up on the spot, spins after the player, then stops dead.
                    bool spinning = t >= CycloneSpinFrom && t <= CycloneSpinTo;
                    if (spinning) { agent.isStopped = false; agent.SetDestination(target); }
                    else { Stop(); if (t < CycloneSpinFrom) Face(target, 200f); }
                    // One hit per cyclone, from the flying rubber fists or the spinning body: it throws the player
                    // clear instead of grinding them down.
                    if (spinning && !actHit)
                    {
                        TryHit(handL, 0.3f, cycloneLaunch, trueForm);
                        TryHit(handR, 0.3f, cycloneLaunch, trueForm);
                        float span = 1.15f * Size;
                        if (!actHit && PlayerNear(transform.position + Vector3.up * 1.6f, span))
                        {
                            actHit = true;
                            if (fx != null) fx.Hit(PlayerChest(), transform.position + Vector3.up * 1.6f, Size);
                            HitPlayer(cycloneLaunch, trueForm);
                        }
                    }
                    // Spinning half sunk in the floor, it churns up dust all round it.
                    if (spinning && Time.time >= nextScrape && fx != null)
                    {
                        nextScrape = Time.time + 0.3f;
                        var round = Random.insideUnitCircle.normalized * (0.9f * Size);
                        fx.Scrape(transform.position + new Vector3(round.x, 0f, round.y), Size);
                    }
                    // The hard stop: both fists drive into the floor.
                    if (t >= 2.62f && Once(MarkHardStop))
                    {
                        GroundBlast(FistPoint(handL), PumpkinBossFx.Blast.Big);
                        GroundBlast(FistPoint(handR), PumpkinBossFx.Blast.Big);
                        SendShake("ShakeMed");
                    }
                    break;
            }
            if (Time.time < actUntil) return;
            if (act == Act.Vomit) EndVomit(true);
            if (act == Act.Cyclone)
            {
                agent.speed = FormSpeed;
                // It spun itself dizzy, still sunk into its upper-body form (it reels like one).
                if (cycloneDizzySeconds > 0f) { StartDizzy(BeingHit, cycloneDizzySeconds); return; }
            }
            // Sunk for its cyclone: back up on its legs, bursting out of the floor.
            if (trueForm && (act == Act.Cyclone || act == Act.Dizzy) && targetBuried > 0f)
            {
                targetBuried = 0f;
                GroundBlast(transform.position, PumpkinBossFx.Blast.Medium);
            }
            // A punch that didn't land counts towards the body slam; the body slam starts the count again.
            if (act == Act.PunchL || act == Act.PunchR || act == Act.Slam) spreeMisses = actHit ? 0 : spreeMisses + 1;
            if (act == Act.JumpSlam) spreeMisses = 0;
            // The last pumpkin is away: it watches them land (UpdateFighting), then decides.
            if (act == Act.Throw) { waitingForPumpkins = true; pumpkinsWaitUntil = Time.time + 2.5f; }
            if (act == Act.Dizzy) nextDizzy = Time.time + dizzyCooldown;
            act = Act.None;
            idleSince = Time.time;
            nextAct = Time.time + (trueForm ? Random.Range(0.6f, 1.2f) : Random.Range(0.8f, 1.6f));
        }

        private void StartJumpSlam()
        {
            Begin(Act.JumpSlam, JumpSlamState, 2.2f);
            PlaySound(roarSound != null ? roarSound : attackSound);
            jumpFrom = transform.position;
            var to = PlayerFloor();
            var leap = to - jumpFrom;
            leap.y = 0f;
            if (leap.magnitude > 8f) leap = leap.normalized * 8f;
            // Lands just short of where the player stood, so the shockwave reaches them.
            jumpTo = jumpFrom + leap * 0.85f;
            if (NavMesh.SamplePosition(jumpTo, out var hit, 3f, NavMesh.AllAreas)) jumpTo = hit.position;
        }

        private void UpdateJumpSlam(float t)
        {
            if (t < 0.55f) Face(jumpTo, 240f);
            // It kicks off: dust where it stood.
            if (t >= 0.6f && Once(MarkTakeOff)) GroundBlast(jumpFrom, PumpkinBossFx.Blast.Medium);
            if (t >= 0.6f && t <= 1.32f)
            {
                float k = Mathf.InverseLerp(0.6f, 1.32f, t);
                var p = Vector3.Lerp(jumpFrom, jumpTo, k);
                agent.Warp(p);
            }
            if (!actHit && t >= 1.32f)
            {
                actHit = true;
                // It lands with both fists slammed into the floor: its biggest blast (its dust rolls out past the fists;
                // more blasts on top would be a lot of smoke for the headset, right where the player stands).
                GroundBlast(transform.position, PumpkinBossFx.Blast.Huge);
                float radius = 1.7f * (model != null ? model.localScale.x : 1f);
                var flat = PlayerFloor() - transform.position;
                flat.y = 0f;
                if (flat.magnitude <= radius && !PlayerFlying()) HitPlayer(jumpSlamLaunch, true);
                // After the hit: the player's own hit shakes (sent by HitPlayer) would cut this big one short.
                SendShake("ShakeBig");
            }
        }

        private void StartCyclone()
        {
            Begin(Act.Cyclone, CycloneState, cycloneSeconds);
            agent.speed = FormSpeed * 0.7f;
            // Standing on its legs its arms would spin over the player's head: it sinks back into its upper-body form
            // for the spin (and the dizzy spell after it), then climbs out again.
            if (phase == Phase.True)
            {
                targetBuried = buried.Length > 0 ? buried[buried.Length - 1] : 0f;
                quickBury = true;
                // It drops into the floor: the ground bursts round it.
                GroundBlast(transform.position, PumpkinBossFx.Blast.Medium);
            }
            nextCyclone = Time.time + cycloneSeconds + cycloneCooldown;
            nextScrape = Time.time + CycloneSpinFrom;
            PlaySound(roarSound != null ? roarSound : attackSound);
        }

        private void StartVomit(int count)
        {
            Begin(Act.Vomit, VomitState, 2.0f);
            bugsLeft = count;
            retchesDone = 0;
            bugsDue.Clear();
            vomitSpot = PickVomitSpot();
            if (fx != null) fx.BeginVomit(Size, vomitSpot);
        }

        // Where its bugs come out, under the black cloud: Vomit Ahead in front of it, towards the player, but turned
        // aside from a player standing close, so the cloud doesn't swallow them and the bugs don't land at their feet.
        private Vector3 PickVomitSpot()
        {
            var toPlayer = PlayerFloor() - transform.position;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;
            var ahead = distance > 0.01f ? toPlayer / distance : transform.forward;
            if (distance < VomitAhead + 2.5f)
                ahead = Quaternion.Euler(0f, Random.value < 0.5f ? -70f : 70f, 0f) * ahead;
            var spot = transform.position + ahead * VomitAhead;
            return NavMesh.SamplePosition(spot, out var hit, 2f, NavMesh.AllAreas) ? hit.position : spot;
        }

        // The vomit is over: bugs still on their way come out now (or, cut short, never).
        private void EndVomit(bool letBugsOut)
        {
            if (letBugsOut) foreach (float _ in bugsDue) SpitBug();
            bugsDue.Clear();
            if (fx != null) fx.EndVomit();
        }

        private bool CanVomit() => bugs.Length > 0 && EnemyLimit.AliveCount < EnemyLimit.Max;

        // A gush of black smoke out of its mouth on every retch, bugs or not.
        private void Retch()
        {
            if (fx != null) fx.Retch(MouthPoint());
        }

        // Its lips: the jaw bone runs from its hinge out to the chin (about 0.3 of its size long).
        private Vector3 MouthPoint()
        {
            if (jaw != null) return jaw.position + jaw.up * 0.3f * Size;
            return head != null ? head.position + transform.forward * 0.5f * Size : transform.position + Vector3.up * Size;
        }

        // Out of its mouth (down while it's bent over to retch) and a little ahead, never upwards.
        private Vector3 MouthDirection()
        {
            var outOfMouth = jaw != null ? jaw.up : -transform.up;
            var dir = outOfMouth + transform.forward * 0.35f;
            if (dir.y > -0.3f) dir.y = -0.3f;
            return dir.normalized;
        }

        private void SpitBug()
        {
            if (bugs.Length == 0 || !EnemyLimit.TryTakeSlot()) return;
            var prefab = bugs[Random.Range(0, bugs.Length)];
            if (prefab == null) return;
            var forward = transform.forward;
            // Out of the middle of the black cloud.
            var around = Random.insideUnitCircle * 1.3f;
            var spot = vomitSpot + new Vector3(around.x, 0f, around.y);
            if (!NavMesh.SamplePosition(spot, out var hit, 2.5f, NavMesh.AllAreas)) return;
            var bug = Instantiate(prefab, hit.position, Quaternion.LookRotation(forward), transform.parent);
            // Vomited up to fight: it goes for the player at once instead of looking around first.
            if (bug.TryGetComponent(out SpiderBrain brain)) brain.ComeAtOnce();
        }

        // Thrown from the hand at where the player stands now: they dodge it by moving.
        private void ThrowBugPumpkin(Transform hand)
        {
            if (bugPumpkin == null) return;
            var from = hand != null ? hand.position : transform.position + Vector3.up * 3f;
            var to = PlayerFloor() + Vector3.up * 0.3f;
            const float flight = 1.0f;
            var velocity = (to - from) / flight - 0.5f * Physics.gravity * flight;
            var thrown = Instantiate(bugPumpkin, from, Random.rotation, transform.parent);
            if (!thrown.TryGetComponent(out BugPumpkin pumpkin)) return;
            pumpkinsOut++;
            pumpkin.Done += OnPumpkinDone;
            pumpkin.Launch(velocity, gameObject, bugs, bugsPerThrownPumpkin);
        }

        private void OnPumpkinDone(bool hitPlayer)
        {
            pumpkinsOut = Mathf.Max(0, pumpkinsOut - 1);
            if (hitPlayer) comboHit = true;
        }

        // ---------- hitting the player ----------

        private void TryHit(Transform hand, float radius, Vector2 launch, bool trueForm)
        {
            if (actHit || hand == null) return;
            // The fist's own hit sphere when it has one (it grows and flies out with the fist); otherwise round the hand.
            Vector3 point;
            float r;
            if (hand.TryGetComponent(out SphereCollider fist))
            {
                var b = fist.bounds;
                point = b.center;
                r = Mathf.Max(b.extents.x, Mathf.Max(b.extents.y, b.extents.z));
            }
            else
            {
                point = hand.position;
                r = radius * (model != null ? model.localScale.x : 1f);
            }
            // The rubber fist covers metres in a frame or two: everything along its path since last frame counts.
            bool reached = PlayerNear(point, r);
            if (!reached && fistTracked.TryGetValue(hand, out var last))
            {
                for (int i = 1; i < 8 && !reached; i++) reached = PlayerNear(Vector3.Lerp(last, point, i / 8f), r);
            }
            fistTracked[hand] = point;
            if (!reached) return;
            actHit = true;
            // The blow lands: a blast where the fist meets them (on the boss's side of it).
            if (fx != null) fx.Hit(point, new Vector3(transform.position.x, point.y, transform.position.z), Size);
            HitPlayer(launch, trueForm || phase == Phase.True);
        }

        private void HitPlayer(Vector2 launch, bool heavy)
        {
            var target = player.gameObject;
            target.GetComponents(fsmBuffer);
            foreach (var fsm in fsmBuffer)
            {
                if (fsm.FsmName == playerHealthFsm) fsm.SendEvent(heavy ? trueHitEvent : upperHitEvent);
            }
            PlayerLaunch.Launch(transform.position, launch.x, launch.y);
        }

        // A sphere touching the player's body: a standing cylinder from their feet to their head.
        private bool PlayerNear(Vector3 point, float radius)
        {
            var headPos = player.Camera.transform.position;
            float feet = player.transform.position.y;
            var closest = new Vector3(headPos.x, Mathf.Clamp(point.y, feet, headPos.y), headPos.z);
            return (closest - point).magnitude <= radius + 0.25f;
        }

        private bool PlayerFlying()
        {
            return player.TryGetComponent(out PlayerLaunch launch) && launch.Flying;
        }

        // ---------- transforming and dying ----------

        private void StartTransform()
        {
            phase = Phase.Transforming;
            Stop();
            act = Act.None;
            // A combo or punches under way don't carry over into its true form.
            waitingForPumpkins = false;
            spreePunches = 0;
            spreeMisses = 0;
            targetBuried = 0f;
            targetScale = trueScale;
            PlayState(TransformState, true);
            PlaySound(roarSound != null ? roarSound : laughs.Length > 0 ? laughs[0] : null);
            SendShake("ShakeMed");
            phaseUntil = Time.time + 2.0f;
            transformStarted = Time.time;
            marks = 0;
            // The ground cracks open round it...
            GroundBlast(transform.position, PumpkinBossFx.Blast.Big);
        }

        // ...and at 1.2 s its legs are out of the floor: it bursts up out of the ground.
        private void UpdateTransforming()
        {
            if (Time.time - transformStarted >= 1.2f && Once(MarkEmerge))
            {
                GroundBlast(transform.position, PumpkinBossFx.Blast.Huge);
                SendShake("ShakeMed");
            }
            if (Time.time >= phaseUntil) EnterTrue();
        }

        private void EnterTrue()
        {
            phase = Phase.True;
            agent.speed = trueSpeed;
            nextAct = Time.time + 0.4f;
            nextCyclone = Time.time + cycloneCooldown * 0.5f;
            // Its true form opens by calling its bugs: a big vomit, right away (unless the game is already full).
            if (trueFormVomitBugs > 0 && CanVomit()) StartVomit(trueFormVomitBugs);
            else PlayState(Run);
        }

        private void StartDying()
        {
            if (act == Act.Vomit) EndVomit(false);
            phase = Phase.Dying;
            act = Act.None;
            Stop();
            // A boss killed before it came out of the ground still stands up to die.
            targetBuried = Mathf.Min(targetBuried, buried.Length > 0 ? buried[buried.Length - 1] : 0f);
            PlayState(Death, true);
            PlaySound(hurtSound);
            phaseUntil = Time.time + 2.5f;
        }

        private void UpdateDying()
        {
            if (Time.time < phaseUntil || model == null || !model.gameObject.activeSelf) return;
            var at = head != null ? head.position : transform.position;
            // Killed while half in the ground, its head is partly under the floor: it bursts just above it.
            at.y = Mathf.Max(at.y, transform.position.y + 0.8f);
            if (burstInto != null)
            {
                var bits = Instantiate(burstInto, at, transform.rotation, transform.parent);
                bits.transform.localScale *= Mathf.Max(1f, model.localScale.x * 1.2f);
            }
            if (smoke != null) EffectPool.Play(smoke, at);
            GroundBlast(transform.position, PumpkinBossFx.Blast.Huge);
            PlaySound(laughs);
            SendShake("ShakeBig");
            model.gameObject.SetActive(false);
            var stages = FsmVariables.GlobalVariables.FindFsmGameObject("GAMESTAGES");
            if (stages != null && stages.Value != null)
            {
                foreach (var fsm in stages.Value.GetComponents<PlayMakerFSM>())
                {
                    if (fsm.FsmName != "GAMESTAGES") continue;
                    var flag = fsm.FsmVariables.FindFsmBool(gameStagesBool);
                    if (flag != null) flag.Value = true;
                    if (!string.IsNullOrEmpty(gameStagesEvent)) fsm.SendEvent(gameStagesEvent);
                }
            }
            Destroy(gameObject, 1.6f);
        }

        // ---------- laughing and breaking the room ----------

        // Non-stop: the next laugh starts Laugh Gap after the last one ends, until it dies.
        private void KeepLaughing()
        {
            if (phase == Phase.Dying || laughSpeaker == null) return;
            if (!laughSpeaker.isPlaying && Time.time >= nextLaugh) LaughNow();
        }

        // A new laugh right now (its big moments: appearing, waking, coming out of the ground).
        private void LaughNow()
        {
            if (laughSpeaker == null || laughs.Length == 0) return;
            int i = Random.Range(0, laughs.Length);
            if (laughs.Length > 1 && i == lastLaugh) i = (i + 1 + Random.Range(0, laughs.Length - 1)) % laughs.Length;
            lastLaugh = i;
            var clip = laughs[i];
            if (clip == null) return;
            laughSpeaker.clip = clip;
            laughSpeaker.volume = laughVolume;
            laughSpeaker.Play();
            nextLaugh = Time.time + clip.length + laughGap;
        }

        // The walls of whichever room is standing fall away (its own fall-away clip), then the room is removed.
        private void BreakRooms()
        {
            bool broke = false;
            foreach (var name in roomVariables)
            {
                var room = FsmVariables.GlobalVariables.FindFsmGameObject(name)?.Value;
                if (room == null || !room.activeInHierarchy) continue;
                var walls = room.GetComponentInChildren<Animator>();
                var fallAway = walls != null ? FallAwayState(walls) : null;
                if (fallAway != null) walls.CrossFadeInFixedTime(fallAway, 0.1f, 0, 0f);
                Destroy(room, fallAway != null ? roomGoneAfter : 0.5f);
                broke = true;
            }
            if (broke) SendShake("ShakeBig");
        }

        // Which clip takes the room's walls down from where they are now; none when they're already down.
        private static string FallAwayState(Animator walls)
        {
            var now = walls.GetCurrentAnimatorStateInfo(0);
            foreach (var (from, to) in FallAway)
            {
                if (!now.IsName(from)) continue;
                return to != null && walls.HasState(0, Animator.StringToHash(to)) ? to : null;
            }
            return null;
        }

        // Room animator states (Bedroom_Base_Z, Livingroom_Base_Z) and the clip that takes those walls down.
        private static readonly (string from, string to)[] FallAway =
        {
            ("Bedroom_Mini_COME", "Bedroom_Mini_GO"), ("Bedroom_Mini_GO2", "Bedroom_Mini_GO"),
            ("Bedroom_COME", "Bedroom_GO"), ("Bedroom_GO2", "Bedroom_GO"), ("Bedroom_Base_Z_Setup", "Bedroom_GO"),
            ("COME", "LivingRoom_GO"), ("LivingRoom_GO2", "LivingRoom_GO"), ("Livingroom_Setup", "LivingRoom_GO"),
            ("COME2", "GO4"),
        };

        // ---------- helpers ----------

        private void UpdateModel()
        {
            if (model == null) return;
            float s = Mathf.MoveTowards(model.localScale.x, targetScale, Time.deltaTime * 2.5f);
            float current = -model.localPosition.y / Mathf.Max(0.01f, s);
            float b = Mathf.MoveTowards(current, targetBuried, Time.deltaTime * (phase == Phase.Head ? 2.5f : quickBury ? 3f : 0.9f));
            if (quickBury && Mathf.Approximately(b, targetBuried) && targetBuried <= 0f) quickBury = false;
            model.localScale = Vector3.one * s;
            SetBuried(b, s);
        }

        private void SetBuried(float amount, float scale)
        {
            var p = model.localPosition;
            p.y = -amount * scale;
            model.localPosition = p;
        }

        private void PlayState(string state, bool restart = false)
        {
            if (animator == null || (!restart && playing == state)) return;
            playing = state;
            animator.CrossFadeInFixedTime(state, 0.15f, 0, 0f);
            if (phase == Phase.Head && state == GetHit) actUntil = Time.time + 0.65f;
        }

        private void Stop()
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh) { agent.isStopped = true; agent.velocity = Vector3.zero; }
        }

        private void Face(Vector3 point, float degreesPerSecond)
        {
            var flat = point - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.0001f) return;
            var want = Quaternion.LookRotation(flat);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, degreesPerSecond * Time.deltaTime);
        }

        private Vector3 PlayerFloor()
        {
            var headPos = player.Camera.transform.position;
            return new Vector3(headPos.x, player.transform.position.y, headPos.z);
        }

        private static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

        private bool FindPlayer()
        {
            if (player != null && player.Camera != null) return true;
            player = FindAnyObjectByType<XROrigin>();
            return player != null && player.Camera != null;
        }

        // The little puff at its head (never under the floor).
        private void Puff()
        {
            if (smoke == null) return;
            var at = head != null ? head.position : transform.position;
            at.y = Mathf.Max(at.y, transform.position.y + 0.3f);
            EffectPool.Play(smoke, at);
        }

        // A blast on the floor right under this point, as hard as the move and as big as the boss (or this size).
        private void GroundBlast(Vector3 at, PumpkinBossFx.Blast strength, float size = 0f)
        {
            if (fx == null) return;
            at.y = transform.position.y;
            fx.Ground(at, strength, size > 0f ? size : Size);
        }

        // The middle of a fist: its hit sphere (it grows and flies out with the fist), else the hand.
        private Vector3 FistPoint(Transform hand)
        {
            if (hand == null) return transform.position;
            return hand.TryGetComponent(out SphereCollider fist) ? fist.bounds.center : hand.position;
        }

        private Vector3 PlayerChest() => player.Camera.transform.position + Vector3.down * 0.35f;

        // True the first time it's asked for this moment of the move (or rising, or transform).
        private bool Once(int mark)
        {
            if ((marks & mark) != 0) return false;
            marks |= mark;
            return true;
        }

        private void PlaySound(AudioClip clip)
        {
            if (clip == null || speaker == null) return;
            speaker.PlayOneShot(clip);
        }

        private void PlaySound(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0) return;
            PlaySound(clips[Random.Range(0, clips.Length)]);
        }

        private static void SendShake(string eventName)
        {
            var cam = FsmVariables.GlobalVariables.FindFsmGameObject("MainCam");
            if (cam == null || cam.Value == null) return;
            foreach (var fsm in cam.Value.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName == "Info") fsm.SendEvent(eventName);
            }
        }
    }
}

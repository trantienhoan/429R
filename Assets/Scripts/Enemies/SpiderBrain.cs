using System;
using System.Collections.Generic;
using Game.Combat;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using FsmFloat = HutongGames.PlayMaker.FsmFloat;
using FsmVariables = HutongGames.PlayMaker.FsmVariables;
using Random = UnityEngine.Random;

namespace Game.Enemies
{
    /// <summary>
    /// A smarter spider, in place of the old "Spider" FSM. It wanders until it sees the player (within Sight Range, not
    /// through walls) or hears them (within Hear Range), and tells the spiders around it. Then it skitters in, in short
    /// bursts, and circles at a distance; only Max Attackers spiders attack at a time, the rest wait their turn. An
    /// attack starts with a wind-up (it rears up: the player's chance to step back or block), then it picks one of its
    /// Attacks that fits the distance, lunges and bites. The bite only hurts if its mouth actually touches the player's
    /// body collider; a weapon held in the way, or a fist, blocks it. Afterwards it backs off and circles again.
    /// A hit makes it flinch (not again for a moment, so it can't be stun-locked); low on health it runs away once.
    /// Size picks Small, Normal or Boss (or one at random by Weight): each has its own size range, health, speed and
    /// bite. It can first play an appearing animation (e.g. climbing out of the ground) before it does anything.
    /// While it waits for its turn it may shoot a web ball (Web Shot) that hurts and slows the player. Once its dodge
    /// animations are added it dodges swings (back) and things thrown or shot at it (to the side), now and then.
    /// Health, death and hits stay with its Health FSM and Enemy Hit Damage. It sends events to its own FSMs (Noticed,
    /// Attack, Bite, Blocked, Missed, Flinch, Flee, Died) for sounds and effects made in PlayMaker, and when it dies it
    /// can set bools in other FSMs, like the old bosses' Set Fsm Variable (e.g. BigSpiderDown in an EnemyManager).
    /// Goes on the spider's root, next to its NavMesh Agent; Tools > 429 Game > Enemies > Set Up Smarter Spider adds it.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public class SpiderBrain : MonoBehaviour
    {
        /// <summary>Which sizes of spider (for attacks only some sizes use).</summary>
        [Flags]
        public enum Sizes
        {
            Small = 1,
            Normal = 2,
            Boss = 4,
        }

        /// <summary>One way of attacking. The spider picks one each time its wind-up ends.</summary>
        [Serializable]
        public class AttackMove
        {
            [Tooltip("Just a name, for you.")]
            public string name = "Bite";
            [Tooltip("Which sizes of spider use this attack, e.g. a heavy slam only for the boss.")]
            public Sizes usedBy = Sizes.Small | Sizes.Normal | Sizes.Boss;
            [Tooltip("The Animator state it plays.")]
            public string animation = "attack";
            [Tooltip("How often it's picked, compared to the other attacks that fit the distance.")]
            [Min(0f)]
            public float weight = 1f;
            [Tooltip("It's only used when the player is between these distances (metres) as the wind-up ends.")]
            [Min(0f)]
            public float minDistance;
            [Min(0f)]
            public float maxDistance = 1.8f;
            [Tooltip("The furthest it hops at the player during the attack, in metres. It hops just far enough to reach " +
                     "where the player was as the attack started, so stepping back then makes it miss.")]
            [Min(0f)]
            public float lunge = 1f;
            [Tooltip("When the bite can hurt, in seconds after the attack starts: from...")]
            [Min(0f)]
            public float hitFrom = 0.25f;
            [Tooltip("...until. Match these to the bite in the animation.")]
            [Min(0f)]
            public float hitUntil = 0.5f;
            [Tooltip("How long the whole attack takes, in seconds.")]
            [Min(0.1f)]
            public float duration = 0.9f;
            [Tooltip("How far in front of the spider its mouth is, in metres (at size 1).")]
            [Min(0f)]
            public float reach = 0.3f;
            [Tooltip("How big the bite is (radius, metres at size 1).")]
            [Min(0.01f)]
            public float biteRadius = 0.25f;
            [Tooltip("Sent to the player's Damage FSM when the bite lands: Damage, DamageHeavy, ...")]
            public string playerEvent = "Damage";
            [Tooltip("Played as the attack starts.")]
            public AudioClip sound;
            [Tooltip("Optional: also sent to the spider's own FSMs as this attack starts.")]
            public string fsmEvent = "";
            [Tooltip("A leap at the player's face: it springs up to their face (Lunge is how far it jumps), clings there " +
                     "biting from Hit From to Hit Until, then drops off. It can be swatted out of the air.")]
            public bool leapToFace;
            [Tooltip("Afterwards it turns and runs away (Run Away Distance) instead of backing off facing the player.")]
            public bool runAwayAfter;
        }

        public enum SizeClass
        {
            Random,
            Small,
            Normal,
            Boss,
        }

        /// <summary>
        /// A bool to set when it dies, in the FSM called Fsm on the object in the PlayMaker global Global Object, like a
        /// Set Fsm Variable action (the old bosses set e.g. EnemyManager / EnemyManager / BigSpiderDown this way).
        /// </summary>
        [Serializable]
        public class DeathFlag
        {
            [Tooltip("The PlayMaker global GameObject holding it: EnemyManager (-EnemyManager 1), EnemyManager0, " +
                     "EnemyManager2, EnemyManager3, EnemyManager4, or GAMESTAGES.")]
            public string globalObject = "EnemyManager";
            [Tooltip("The FSM on that object: EnemyManager (or GAMESTAGES on GAMESTAGES).")]
            public string fsm = "EnemyManager";
            [Tooltip("The bool variable, e.g. BigSpiderDown, WhiteSpiderDown, BigRoachDown, BigBeeDown.")]
            public string boolName = "";
            [Tooltip("What it's set to.")]
            public bool value = true;
        }

        /// <summary>What one size of spider is like.</summary>
        [Serializable]
        public class SizeSettings
        {
            [Tooltip("With Size on Random: how often this size is picked, compared to the others.")]
            [Min(0f)]
            public float weight = 1f;
            [Tooltip("Its size (scale), random between.")]
            public Vector2 scale = new(0.4f, 0.75f);
            [Tooltip("Its health, a whole number random between (both included).")]
            public Vector2Int health = new(8, 10);
            [Tooltip("Multiplies its walking and running speed.")]
            [Min(0.1f)]
            public float speed = 1f;
            [Tooltip("Multiplies how long its wind-up lasts: a big spider warns longer before its heavy bite.")]
            [Min(0.1f)]
            public float windUp = 1f;
            [Tooltip("Sent to the player's Damage FSM when it bites, instead of the attack's own event (Damage takes 1-3 " +
                     "health, DamageHeavy 6-12). Empty: the attack's.")]
            public string playerEvent = "";
            [Tooltip("Multiplies how far a big hit knocks it back.")]
            [Min(0f)]
            public float knockBack = 1f;
            [Tooltip("Pitch of its sounds: higher for little ones, lower for big ones.")]
            [Range(0.3f, 3f)]
            public float pitch = 1f;
            [Tooltip("Chance (0 to 1) that it dodges a swing or something thrown at it, when it can.")]
            [Range(0f, 1f)]
            public float dodgeChance = 0.35f;
            [Tooltip("Whether this size shoots web balls.")]
            public bool webs = true;
        }

        private enum State
        {
            Appear,
            Idle,
            Wander,
            Notice,
            Chase,
            Circle,
            Prepare,
            Attack,
            Retreat,
            Flinch,
            Flee,
            Dodge,
            WebShot,
            Dead,
        }

        [Header("Size")]
        [Tooltip("Which size this spider is. Random: one of the three, picked by their Weight each time one appears.")]
        [SerializeField] private SizeClass size = SizeClass.Random;
        [SerializeField] private SizeSettings small = new() { weight = 45f, scale = new Vector2(0.3f, 0.5f), health = new Vector2Int(3, 5), speed = 1f, pitch = 1.3f, dodgeChance = 0.5f, webs = false };
        [SerializeField] private SizeSettings normal = new() { weight = 45f, scale = new Vector2(0.4f, 0.75f), health = new Vector2Int(8, 10), speed = 1.15f, pitch = 1f, dodgeChance = 0.35f };
        [SerializeField] private SizeSettings boss = new()
        {
            weight = 10f, scale = new Vector2(1.25f, 1.5f), health = new Vector2Int(20, 25), speed = 0.9f, windUp = 1.3f,
            playerEvent = "DamageHeavy", knockBack = 0.35f, pitch = 0.7f, dodgeChance = 0.15f,
        };

        [Header("Appearing")]
        [Tooltip("Animator state it plays when it appears, e.g. climbing out of the ground. Empty, or not in the " +
                 "Animator yet: it skips appearing.")]
        [SerializeField] private string appearAnimation = "";
        [Tooltip("How long appearing takes, in seconds. 0 = as long as that animation.")]
        [Min(0f)]
        [SerializeField] private float appearSeconds;
        [Tooltip("For an animation that climbs on the spot: the model starts this far underground (metres at size 1) " +
                 "and rises while it plays. 0 = off.")]
        [Min(0f)]
        [SerializeField] private float appearRise;

        [Header("Senses")]
        [Tooltip("It notices the player this far away, in metres, if nothing blocks its view.")]
        [Min(0f)]
        [SerializeField] private float sightRange = 8f;
        [Tooltip("It notices the player this close, in metres, even without seeing them.")]
        [Min(0f)]
        [SerializeField] private float hearRange = 2.5f;
        [Tooltip("When it notices the player, spiders this close to it, in metres, notice too.")]
        [Min(0f)]
        [SerializeField] private float alertFriendsRange = 6f;
        [Tooltip("It loses interest when the player is further than this, in metres...")]
        [Min(1f)]
        [SerializeField] private float loseInterestRange = 14f;
        [Tooltip("...and out of sight for this many seconds.")]
        [Min(0f)]
        [SerializeField] private float loseInterestSeconds = 6f;
        [Tooltip("Tick to have it come for the player as soon as it appears (e.g. dropped right next to them).")]
        [SerializeField] private bool noticeOnSpawn;

        [Header("Moving")]
        [Tooltip("Speed while wandering, in metres per second.")]
        [Min(0.1f)]
        [SerializeField] private float walkSpeed = 0.6f;
        [Tooltip("Speed while running at or around the player, in metres per second.")]
        [Min(0.1f)]
        [SerializeField] private float runSpeed = 2.8f;
        [Tooltip("Runs in short bursts with tiny stops, like a spider, instead of gliding.")]
        [SerializeField] private bool skitter = true;
        [Tooltip("How long each burst lasts, in seconds (random between).")]
        [SerializeField] private Vector2 burstSeconds = new(0.5f, 1.2f);
        [Tooltip("How long each stop lasts, in seconds (random between).")]
        [SerializeField] private Vector2 pauseSeconds = new(0.12f, 0.3f);
        [Tooltip("How far from the player it circles while it waits for its turn, in metres.")]
        [Min(0.5f)]
        [SerializeField] private float circleDistance = 2.3f;
        [Tooltip("How fast it circles, in degrees per second.")]
        [Min(0f)]
        [SerializeField] private float circleSpeed = 40f;
        [Tooltip("How far from where it appeared it wanders, in metres.")]
        [Min(0f)]
        [SerializeField] private float wanderRadius = 3f;

        [Header("Attacking")]
        [Tooltip("It starts its wind-up this close to the player, in metres, or closer for a small spider whose hop " +
                 "wouldn't reach from there.")]
        [Min(0.3f)]
        [SerializeField] private float attackRange = 1.3f;
        [Tooltip("How long the wind-up lasts, in seconds: the player's warning.")]
        [Min(0f)]
        [SerializeField] private float prepareSeconds = 0.6f;
        [Tooltip("The player's body for bites: a standing cylinder this wide (radius, metres) under their head, from " +
                 "the floor up. A bite that reaches it hurts, even where the player's own colliders are thinner.")]
        [Min(0f)]
        [SerializeField] private float playerBodyRadius = 0.2f;
        [Tooltip("Face leaps: seconds to spring up to the player's face (x), and to drop back down after the bite (y).")]
        [SerializeField] private Vector2 leapRiseFall = new(0.2f, 0.3f);
        [Tooltip("How far it runs away after an attack that runs away, in metres.")]
        [Min(0f)]
        [SerializeField] private float runAwayDistance = 3f;
        [Tooltip("What it does when the wind-up ends: one of these that fits the distance, picked by Weight.")]
        [SerializeField] private List<AttackMove> attacks = new() { new AttackMove() };
        [Tooltip("At most this many spiders attack at the same time; the others circle and wait.")]
        [Min(1)]
        [SerializeField] private int maxAttackers = 2;
        [Tooltip("Seconds between its attacks (random between).")]
        [SerializeField] private Vector2 attackCooldown = new(1.2f, 2.4f);
        [Tooltip("How far it backs off after an attack, in metres.")]
        [Min(0f)]
        [SerializeField] private float retreatDistance = 1.2f;
        [Tooltip("How fast it backs off, in metres per second, when it has a Walk Back Animation (without one it runs back).")]
        [Min(0.1f)]
        [SerializeField] private float walkBackSpeed = 0.7f;
        [Tooltip("A weapon held in the way of the bite, or a fist, blocks it.")]
        [SerializeField] private bool heldThingsBlock = true;

        [Header("Dodging")]
        [Tooltip("Animator states for hopping back, left and right. It only dodges once at least one is set and in the " +
                 "Animator; a missing one is swapped for another direction that has one.")]
        [SerializeField] private string dodgeBackAnimation = "";
        [SerializeField] private string dodgeLeftAnimation = "";
        [SerializeField] private string dodgeRightAnimation = "";
        [Tooltip("How far a dodge hops, in metres.")]
        [Min(0f)]
        [SerializeField] private float dodgeDistance = 1f;
        [Tooltip("How long a dodge takes, in seconds.")]
        [Min(0.05f)]
        [SerializeField] private float dodgeSeconds = 0.35f;
        [Tooltip("Seconds before it can dodge again.")]
        [Min(0f)]
        [SerializeField] private float dodgeCooldown = 2.5f;
        [Tooltip("A hand or held weapon moving at least this fast (metres per second) towards it counts as a swing.")]
        [Min(0.1f)]
        [SerializeField] private float swingSpeed = 2.5f;
        [Tooltip("Something flying at least this fast (metres per second) towards it counts as thrown or shot.")]
        [Min(0.1f)]
        [SerializeField] private float throwSpeed = 4f;

        [Header("Web shot")]
        [Tooltip("What it shoots (Spider_Web_Ball). Empty: no web shots.")]
        [SerializeField] private GameObject webBall;
        [Tooltip("Animator state for making and shooting the web. Empty: it rears up (Alert Animation) instead.")]
        [SerializeField] private string webAnimation = "";
        [Tooltip("When the ball leaves its mouth, in seconds after the shot starts.")]
        [Min(0f)]
        [SerializeField] private float webReleaseTime = 0.5f;
        [Tooltip("Bone in the model the ball is made on before it's shot: it follows the bone, and the bone's scale is " +
                 "how big the ball is so far (Spider_Shoot_Web_Ball grows it). Empty or not in the model: the ball " +
                 "appears at its mouth when it shoots.")]
        [SerializeField] private string webHoldBone = "Web_Ball";
        [Tooltip("How long the whole shot takes, in seconds.")]
        [Min(0.1f)]
        [SerializeField] private float webDuration = 1f;
        [Tooltip("It shoots when the player is between these distances, in metres, and in sight.")]
        [SerializeField] private Vector2 webRange = new(2f, 7f);
        [Tooltip("Seconds between its web shots (random between).")]
        [SerializeField] private Vector2 webCooldown = new(5f, 9f);
        [Tooltip("How fast the ball flies, in metres per second.")]
        [Min(1f)]
        [SerializeField] private float webSpeed = 9f;
        [Tooltip("At most this many spiders shoot at the same time.")]
        [Min(1)]
        [SerializeField] private int maxWebShooters = 1;
        [SerializeField] private AudioClip webSound;

        [Header("Getting hit")]
        [Tooltip("How long a hit stops it, in seconds.")]
        [Min(0f)]
        [SerializeField] private float flinchSeconds = 0.4f;
        [Tooltip("After a flinch, more hits don't stop it for this long, in seconds (they still hurt).")]
        [Min(0f)]
        [SerializeField] private float flinchCooldown = 1f;
        [Tooltip("With this share of its health left or less (0.3 = 30%), it runs away once. 0 = never.")]
        [Range(0f, 1f)]
        [SerializeField] private float fleeBelowHealth = 0.3f;
        [Tooltip("How long it runs away, in seconds.")]
        [Min(0f)]
        [SerializeField] private float fleeSeconds = 2.5f;

        [Header("Looks")]
        [Tooltip("Holds the model variants; one hidden variant is switched on at random.")]
        [SerializeField] private Transform models;
        [SerializeField] private Animator animator;
        [Tooltip("Seconds to blend from one animation to the next.")]
        [Min(0f)]
        [SerializeField] private float crossFade = 0.12f;
        [SerializeField] private string idleAnimation = "idle";
        [SerializeField] private string walkAnimation = "Spider_Walk";
        [SerializeField] private string runAnimation = "run";
        [Tooltip("Backing off after an attack, still facing the player. Empty or not in the Animator: it runs back instead.")]
        [SerializeField] private string walkBackAnimation = "walk_back";
        [Tooltip("Its wind-up (rearing up) and the hiss when it notices the player.")]
        [SerializeField] private string alertAnimation = "alert";
        [SerializeField] private string hitAnimation = "hit";
        [Tooltip("The speed (m/s) at which the run animation looks right; it plays faster or slower to match.")]
        [Min(0.1f)]
        [SerializeField] private float runAnimationSpeed = 2.2f;
        [Tooltip("The speed (m/s) at which the walk-back animation looks right; it plays faster or slower to match.")]
        [Min(0.1f)]
        [SerializeField] private float walkBackAnimationSpeed = 0.61f;
        [Tooltip("How much slower and faster than normal its walk and run animations may play to keep pace with its " +
                 "feet (0.6 = 60%, 2.5 = 250%). Past these its feet slide.")]
        [SerializeField] private Vector2 legSpeedRange = new(0.6f, 2.5f);
        [Tooltip("It appears at this share of its full size (0.6 = 60%)...")]
        [Range(0.05f, 1f)]
        [SerializeField] private float growFrom = 0.6f;
        [Tooltip("...and grows to full size over this many seconds. 0 = full size at once.")]
        [Min(0f)]
        [SerializeField] private float growSeconds = 7f;
        [Tooltip("Switched on when it's hurt, e.g. a puff of smoke (it switches itself off).")]
        [SerializeField] private GameObject hitEffect;

        [Header("When it dies")]
        [Tooltip("Bools it sets when it dies, e.g. on Spider_Boss only: EnemyManager / EnemyManager / BigSpiderDown, so " +
                 "that EnemyManager knows its boss is down. Empty: none.")]
        [SerializeField] private List<DeathFlag> setOnDeath = new();

        [Header("Sounds")]
        [SerializeField] private AudioClip[] noticeSounds = Array.Empty<AudioClip>();
        [SerializeField] private AudioClip[] hitSounds = Array.Empty<AudioClip>();
        [SerializeField] private AudioClip blockedSound;
        [Range(0f, 1f)]
        [SerializeField] private float volume = 1f;

        private const string PlayerVariable = "Player";
        private const string PlayerDamageFsm = "Damage";
        private const float SensesEvery = 0.2f;
        private const float RepathEvery = 0.25f;
        private const float NoticeSeconds = 0.5f;
        private const float TurnSpeed = 540f;
        private const float LungeLead = 0.15f;

        private static readonly List<SpiderBrain> spiders = new();
        private static readonly Collider[] touching = new Collider[32];
        private static readonly RaycastHit[] sightHits = new RaycastHit[16];
        private static readonly List<PlayMakerFSM> fsmBuffer = new();
        private static int attackersNow;
        private static int webShootersNow;
        private static readonly Collider[] nearby = new Collider[48];
        private static XROrigin player;

        private NavMeshAgent agent;
        private AudioSource speaker;
        private Knockback knockback;
        private readonly List<PlayMakerFSM> fsms = new();
        private PlayMakerFSM healthFsm;
        private FsmFloat health;
        private float startHealth;

        private State state;
        private float stateUntil;
        private bool noticed;
        private bool hasTurn;
        private bool fled;
        private float lastSeen;
        private float nextSenses;
        private float nextRepath;
        private float nextAttack;
        private float nextFlinch;
        private float burstUntil;
        private bool pausing;
        private float orbitAngle;
        private float orbitDirection = 1f;
        private float nextOrbitFlip;
        private Vector3 home;
        private Vector3 fullScale;
        private float bornAt;
        private string playing;
        private SizeSettings kind;
        private SizeClass kindName;
        private float appearStart;
        private Vector3 modelsHome;
        private bool appearTimed;
        private readonly HashSet<string> missingAnimations = new();

        private AttackMove move;
        private float attackStart;
        private bool attackResolved;
        private float lungeLeft;
        private float lungeTotal;
        private float hitShownUntil;

        private float nextDodge;
        private float nextThreatScan;
        private float lastThreatScan = -1f;
        private Vector3 dodgeVelocity;
        private readonly List<Transform> hands = new();
        private readonly Dictionary<Transform, Vector3> lastThreatPoints = new();

        private float nextWeb;
        private bool isShooting;
        private bool webLaunched;
        private float webStart;
        private Transform webHold;
        private GameObject heldWeb;
        private Vector3 heldWebScale;
        // Face leap: how high (metres) its model is lifted right now, and how fast it's falling when it lets go.
        private float leapLift;
        private float leapDropFrom;
        private float leapFallSpeed;
        private bool leapShown;
        private bool runningAway;
        private readonly List<Collider> bodyColliders = new();
        private readonly List<Vector3> bodyColliderCentres = new();

        /// <summary>What it's doing right now (for debugging and FSMs that want to know).</summary>
        public string Doing => state.ToString();

        /// <summary>Which size it turned out to be: Small, Normal or Boss.</summary>
        public string SizeName => kindName.ToString();

        private float WalkSpeed => walkSpeed * kind.speed;
        private float RunSpeed => runSpeed * kind.speed;

        /// <summary>Raised with the same names as the events it sends its FSMs (Noticed, Attack, Bite, Blocked, ...).</summary>
        public event Action<SpiderBrain, string> Signalled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            spiders.Clear();
            attackersNow = 0;
            webShootersNow = 0;
            player = null;
        }

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            TryGetComponent(out speaker);
            ChooseSize();
            PickModel();
            if (models != null) modelsHome = models.localPosition;
        }

        private void Start()
        {
            GetComponents(fsms);
            foreach (var fsm in fsms)
            {
                if (!fsm.FsmName.EndsWith("Health")) continue;
                healthFsm = fsm;
                health = fsm.FsmVariables.FindFsmFloat("health");
                break;
            }
            if (health != null) health.Value = Random.Range(kind.health.x, Mathf.Max(kind.health.x, kind.health.y) + 1);
            startHealth = health != null ? Mathf.Max(health.Value, 0.01f) : 1f;
            if (speaker != null) speaker.pitch = kind.pitch;
            if (TryGetComponent(out EnemyHitDamage hits)) hits.KnockBackDistance *= kind.knockBack;

            home = transform.position;
            bornAt = Time.time;
            orbitAngle = Random.Range(0f, 360f);
            orbitDirection = Random.value < 0.5f ? -1f : 1f;
            agent.avoidancePriority = Random.Range(35, 65);
            nextAttack = Time.time + Random.Range(0f, attackCooldown.y);
            nextWeb = Time.time + Random.Range(2f, Mathf.Max(2f, webCooldown.y));
            EnemyHitDamage.AnyHit += OnAnyHit;

            if (StartAppearing()) return;
            if (noticeOnSpawn) Notice(true);
            else Enter(State.Idle, Random.Range(0.5f, 2f));
        }

        private void OnEnable()
        {
            spiders.Add(this);
        }

        private void OnDisable()
        {
            spiders.Remove(this);
            GiveUpTurn();
            StopShooting();
        }

        private void OnDestroy()
        {
            EnemyHitDamage.AnyHit -= OnAnyHit;
        }

        private void Update()
        {
            if (state == State.Dead) return;
            if (IsDying())
            {
                Die();
                return;
            }

            Grow();
            if (player == null) player = FindAnyObjectByType<XROrigin>();
            if (player == null || player.Camera == null || !agent.isOnNavMesh) return;

            // Knockback moves the agent itself; wait until it's done.
            if (knockback != null || TryGetComponent(out knockback))
            {
                if (knockback.IsKnockedBack) return;
            }

            if (Time.time >= nextSenses) Sense();
            if (Time.time >= nextThreatScan) ScanThreats();

            switch (state)
            {
                case State.Appear: DoAppear(); break;
                case State.Idle: DoIdle(); break;
                case State.Wander: DoWander(); break;
                case State.Notice: DoNotice(); break;
                case State.Chase: DoChase(); break;
                case State.Circle: DoCircle(); break;
                case State.Prepare: DoPrepare(); break;
                case State.Attack: DoAttack(); break;
                case State.Retreat: DoRetreat(); break;
                case State.Flinch: DoFlinch(); break;
                case State.Flee: DoFlee(); break;
                case State.Dodge: DoDodge(); break;
                case State.WebShot: DoWebShot(); break;
            }
            FallIfLifted();
        }

        // ---- Senses ----

        private void Sense()
        {
            nextSenses = Time.time + SensesEvery;
            float distance = PlayerDistance();
            bool hears = distance <= hearRange;
            bool sees = !hears && distance <= sightRange && CanSeePlayer();

            if (!noticed)
            {
                if (hears || sees) Notice(true);
                return;
            }

            if (hears || sees || distance <= sightRange) lastSeen = Time.time;
            else if (distance > loseInterestRange && Time.time - lastSeen > loseInterestSeconds) CalmDown();
        }

        // Nothing solid between its eyes and the player's head. Loose things (props, other enemies, the player's own
        // body) don't block the view.
        private bool CanSeePlayer()
        {
            var eyes = transform.position + Vector3.up * (0.3f * Size());
            var head = player.Camera.transform.position;
            var toHead = head - eyes;
            int count = Physics.RaycastNonAlloc(eyes, toHead.normalized, sightHits, toHead.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = sightHits[i];
                if (hit.collider.transform.IsChildOf(transform) || IsPlayer(hit.collider)) continue;
                var body = hit.collider.attachedRigidbody;
                if (body != null && (!body.isKinematic || body.GetComponent<NavMeshAgent>() != null)) continue;
                return false;
            }
            return true;
        }

        /// <summary>Makes it notice the player now (e.g. from an FSM); it tells the spiders around it.</summary>
        public void Notice()
        {
            Notice(true);
        }

        private void Notice(bool tellFriends)
        {
            if (state == State.Dead || noticed) return;
            noticed = true;
            lastSeen = Time.time;
            // Still climbing out: it goes for the player once it's out.
            if (state == State.Appear)
            {
                if (tellFriends) TellFriends();
                return;
            }
            Enter(State.Notice, NoticeSeconds);
            Stop();
            Play(alertAnimation);
            PlayOne(noticeSounds);
            SendToOwnFsms("Noticed");
            if (tellFriends) TellFriends();
        }

        private void TellFriends()
        {
            if (alertFriendsRange <= 0f) return;
            foreach (var other in spiders)
            {
                if (other != this && !other.noticed &&
                    (other.transform.position - transform.position).sqrMagnitude <= alertFriendsRange * alertFriendsRange)
                    other.Notice(false);
            }
        }

        private void CalmDown()
        {
            noticed = false;
            GiveUpTurn();
            Enter(State.Idle, Random.Range(1f, 3f));
        }

        // ---- States ----

        private bool StartAppearing()
        {
            if (string.IsNullOrEmpty(appearAnimation) || !HasAnimation(appearAnimation)) return false;
            Enter(State.Appear, 5f);
            appearStart = Time.time;
            appearTimed = appearSeconds > 0f;
            if (appearTimed) stateUntil = Time.time + appearSeconds;
            if (models != null) modelsHome = models.localPosition;
            Stop();
            Play(appearAnimation, true);
            SendToOwnFsms("Appear");
            return true;
        }

        // Plays the appearing animation to its end (or for Appear Seconds), rising from underground if set.
        private void DoAppear()
        {
            Stop();
            bool done = Time.time >= stateUntil;
            if (!appearTimed && animator != null && Time.time - appearStart > 0.1f)
            {
                var current = animator.GetCurrentAnimatorStateInfo(0);
                if (current.IsName(appearAnimation) && current.normalizedTime >= 1f) done = true;
                else if (!current.IsName(appearAnimation) && !animator.IsInTransition(0)) done = true;
            }

            if (models != null && appearRise > 0f)
            {
                float length = appearTimed ? appearSeconds : Mathf.Max(0.1f, animator != null ? animator.GetCurrentAnimatorStateInfo(0).length : 1f);
                float t = Mathf.Clamp01((Time.time - appearStart) / length);
                // In the models' parent space (the spider's root), so the rise is in metres at size 1.
                models.localPosition = modelsHome - Vector3.up * (appearRise * (1f - t));
            }
            if (!done) return;

            if (models != null && appearRise > 0f) models.localPosition = modelsHome;
            playing = null;
            // Out of the Appear state first, so noticing goes ahead.
            Enter(State.Idle, Random.Range(0.3f, 1.5f));
            if (noticed)
            {
                noticed = false;
                Notice(false);
            }
            else if (noticeOnSpawn) Notice(true);
        }

        private void DoIdle()
        {
            Stop();
            Play(idleAnimation);
            if (Time.time < stateUntil) return;
            var point = home + Random.insideUnitSphere * wanderRadius;
            if (NavMesh.SamplePosition(point, out var hit, wanderRadius, NavMesh.AllAreas)) GoTo(hit.position, WalkSpeed, true);
            Enter(State.Wander, 8f);
        }

        private void DoWander()
        {
            Play(walkAnimation);
            SetLegs();
            if (Time.time >= stateUntil || (!agent.pathPending && agent.remainingDistance <= 0.2f))
                Enter(State.Idle, Random.Range(1f, 3f));
        }

        private void DoNotice()
        {
            Face(PlayerFloor());
            if (Time.time >= stateUntil) Enter(State.Chase, 0f);
        }

        // Runs at the player; close enough, it attacks if it's its turn, or circles.
        private void DoChase()
        {
            float distance = PlayerDistance();
            if (hasTurn || TryTakeTurn())
            {
                if (distance <= WindUpDistance())
                {
                    StartPrepare();
                    return;
                }
                Run(PlayerFloor());
                return;
            }

            if (TryStartWebShot(distance)) return;
            if (distance <= circleDistance + 0.6f)
            {
                orbitAngle = Mathf.Atan2(transform.position.z - PlayerFloor().z, transform.position.x - PlayerFloor().x) * Mathf.Rad2Deg;
                Enter(State.Circle, 0f);
                return;
            }
            Run(PlayerFloor());
        }

        // Circles the player at a distance, now and then turning round, until it may attack.
        private void DoCircle()
        {
            if (TryTakeTurn())
            {
                Enter(State.Chase, 0f);
                return;
            }
            if (PlayerDistance() > circleDistance + 2.5f)
            {
                Enter(State.Chase, 0f);
                return;
            }
            if (TryStartWebShot(PlayerDistance())) return;

            if (Time.time >= nextOrbitFlip)
            {
                nextOrbitFlip = Time.time + Random.Range(2f, 4.5f);
                if (Random.value < 0.4f) orbitDirection = -orbitDirection;
            }
            orbitAngle += orbitDirection * circleSpeed * Time.deltaTime;
            float radians = orbitAngle * Mathf.Deg2Rad;
            var center = PlayerFloor();
            var point = center + new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians)) * circleDistance;
            Run(point, 0.7f);
        }

        private void StartPrepare()
        {
            Enter(State.Prepare, prepareSeconds * kind.windUp);
            Stop();
            Play(alertAnimation);
            SendToOwnFsms("Prepare");
        }

        // The wind-up: stands still and rears up at the player. Then it picks its attack.
        private void DoPrepare()
        {
            Face(PlayerFloor());
            if (Time.time < stateUntil) return;

            float distance = PlayerDistance();
            move = PickAttack(distance);
            if (move == null)
            {
                // The player got away; run after them (it keeps its turn).
                Enter(State.Chase, 0f);
                return;
            }

            Enter(State.Attack, move.duration);
            attackStart = Time.time;
            attackResolved = false;
            // Just far enough to put its mouth where the player is now (a face leap stops just in front of the face).
            lungeTotal = lungeLeft = Mathf.Clamp(distance - move.reach * Size() - (move.leapToFace ? 0.1f : 0.05f), 0f, move.lunge);
            Play(move.animation, true);
            if (move.sound != null) PlayOne(move.sound);
            SendToOwnFsms("Attack");
            if (!string.IsNullOrEmpty(move.fsmEvent)) SendToOwnFsms(move.fsmEvent);
        }

        // The attack: turns to the player, lunges, and the bite counts only while it's in the bite window and its mouth
        // touches the player's body (a weapon or fist in the way blocks it).
        private void DoAttack()
        {
            float t = Time.time - attackStart;
            var target = PlayerFloor();
            if (t < move.hitFrom) Face(target);

            // A face leap covers the distance while it springs up; a bite lunges until its bite window ends.
            float lungeStart = move.leapToFace ? Mathf.Max(0f, move.hitFrom - leapRiseFall.x) : Mathf.Max(0f, move.hitFrom - LungeLead);
            float lungeEnd = move.leapToFace ? move.hitFrom : move.hitUntil;
            if (t >= lungeStart && t <= lungeEnd && lungeLeft > 0f)
            {
                float window = Mathf.Max(0.05f, lungeEnd - lungeStart);
                float step = Mathf.Min(lungeLeft, lungeTotal / window * Time.deltaTime);
                agent.Move(transform.forward * step);
                lungeLeft -= step;
            }
            if (move.leapToFace) Leap(t);

            if (!attackResolved && t >= move.hitFrom && t <= move.hitUntil) TryBite();
            if (!attackResolved && t > move.hitUntil)
            {
                attackResolved = true;
                SendToOwnFsms("Missed");
            }

            // A leaper waits until it's back on the floor.
            if (Time.time < stateUntil || leapLift > 0.001f) return;
            if (move.runAwayAfter) StartRunAway();
            else StartRetreat(retreatDistance, 0.6f);
        }

        // Up to the player's face (it follows their head height while it clings), then a quickening drop.
        private void Leap(float t)
        {
            float takeoff = Mathf.Max(0f, move.hitFrom - leapRiseFall.x);
            float face = Mathf.Max(0f, player.Camera.transform.position.y - 0.1f - transform.position.y - 0.15f * Size());
            if (t < takeoff)
            {
                leapLift = 0f;
            }
            else if (t < move.hitFrom)
            {
                float s = (t - takeoff) / Mathf.Max(0.01f, leapRiseFall.x);
                leapLift = face * (1f - (1f - s) * (1f - s));
            }
            else if (t < move.hitUntil)
            {
                leapLift = face;
                leapDropFrom = face;
            }
            else
            {
                float s = Mathf.Clamp01((t - move.hitUntil) / Mathf.Max(0.01f, leapRiseFall.y));
                leapLift = leapDropFrom * (1f - s * s);
            }
            leapFallSpeed = 0f;
        }

        // Anything that ends an attack early (a hit, a block, a dodge) lets a leaper fall from where it is.
        private void FallIfLifted()
        {
            if (leapLift <= 0f || (state == State.Attack && move != null && move.leapToFace)) return;
            leapFallSpeed += 9.81f * Time.deltaTime;
            leapLift = Mathf.Max(0f, leapLift - leapFallSpeed * Time.deltaTime);
        }

        // Its model (and its body's collider, so it can be swatted out of the air) at the leap's height.
        private void ShowLeap()
        {
            if (models == null || (!leapShown && leapLift <= 0f)) return;
            float local = leapLift / Mathf.Max(0.01f, transform.lossyScale.y);
            models.localPosition = modelsHome + Vector3.up * local;
            if (bodyColliders.Count == 0 && bodyColliderCentres.Count == 0)
            {
                foreach (var c in GetComponents<Collider>())
                {
                    if (c.isTrigger) continue;
                    bodyColliders.Add(c);
                    bodyColliderCentres.Add(Centre(c));
                }
            }
            for (int i = 0; i < bodyColliders.Count; i++) SetCentre(bodyColliders[i], bodyColliderCentres[i] + Vector3.up * local);
            leapShown = leapLift > 0f;
        }

        private static Vector3 Centre(Collider c) => c switch
        {
            SphereCollider s => s.center,
            CapsuleCollider k => k.center,
            BoxCollider b => b.center,
            _ => Vector3.zero,
        };

        private static void SetCentre(Collider c, Vector3 centre)
        {
            switch (c)
            {
                case SphereCollider s: s.center = centre; break;
                case CapsuleCollider k: k.center = centre; break;
                case BoxCollider b: b.center = centre; break;
            }
        }

        // Killed in the air: it still falls (the brain is switched off by then).
        private System.Collections.IEnumerator FallWhenDead()
        {
            while (leapLift > 0f)
            {
                leapFallSpeed += 9.81f * Time.deltaTime;
                leapLift = Mathf.Max(0f, leapLift - leapFallSpeed * Time.deltaTime);
                ShowLeap();
                yield return null;
            }
        }

        // Turns tail and runs off (after a face leap), then its turn is over.
        private void StartRunAway()
        {
            Enter(State.Retreat, runAwayDistance / Mathf.Max(0.5f, RunSpeed) + 0.4f);
            runningAway = true;
            var away = transform.position - PlayerFloor();
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = -transform.forward;
            var point = transform.position + away.normalized * runAwayDistance;
            if (NavMesh.SamplePosition(point, out var hit, runAwayDistance + 0.5f, NavMesh.AllAreas)) GoTo(hit.position, RunSpeed, true);
        }

        private void TryBite()
        {
            float size = Size();
            var mouth = transform.position + transform.forward * (move.reach * size) + Vector3.up * (0.15f * size + leapLift);
            int count = Physics.OverlapSphereNonAlloc(mouth, move.biteRadius * size, touching, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            bool bitPlayer = false;
            for (int i = 0; i < count; i++)
            {
                var other = touching[i];
                if (other.transform.IsChildOf(transform)) continue;
                if (heldThingsBlock && IsHeldOrFist(other))
                {
                    attackResolved = true;
                    if (blockedSound != null) PlayOne(blockedSound);
                    SendToOwnFsms("Blocked");
                    Play(hitAnimation, true);
                    hitShownUntil = Time.time + flinchSeconds;
                    StartRetreat(retreatDistance + 0.5f, 0.8f);
                    return;
                }
                if (!other.isTrigger && IsPlayer(other)) bitPlayer = true;
            }
            // The player's colliders sit on the rig, not always under their head (room-scale), and a small spider's
            // bite is small: the body around where they stand counts too.
            if (!bitPlayer) bitPlayer = ReachesBody(mouth, move.biteRadius * size);
            if (!bitPlayer) return;

            attackResolved = true;
            SendToPlayer(string.IsNullOrEmpty(kind.playerEvent) ? move.playerEvent : kind.playerEvent);
            SendToOwnFsms("Bite");
        }

        private bool ReachesBody(Vector3 mouth, float radius)
        {
            var flat = mouth - PlayerFloor();
            float headHeight = player.Camera.transform.position.y;
            flat.y = 0f;
            return flat.magnitude <= radius + playerBodyRadius && mouth.y - radius <= headHeight;
        }

        private void StartRetreat(float distance, float seconds)
        {
            // Walking back is slower than running back, so it gets the time to cover the distance.
            bool walkBack = HasAnimationQuiet(walkBackAnimation);
            float speed = walkBack ? walkBackSpeed * kind.speed : RunSpeed * 0.8f;
            if (walkBack) seconds = Mathf.Max(seconds, distance / speed);
            Enter(State.Retreat, seconds);
            runningAway = false;
            var away = transform.position - PlayerFloor();
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = -transform.forward;
            var point = transform.position + away.normalized * distance;
            if (NavMesh.SamplePosition(point, out var hit, distance + 0.5f, NavMesh.AllAreas)) GoTo(hit.position, speed, false);
        }

        // Backs off still facing the player, then its turn is over.
        private void DoRetreat()
        {
            if (runningAway)
            {
                Play(runAnimation);
            }
            else
            {
                Face(PlayerFloor());
                // A blocked bite shows its recoil (the hit animation) before it walks back.
                if (Time.time >= hitShownUntil) Play(HasAnimationQuiet(walkBackAnimation) ? walkBackAnimation : runAnimation);
            }
            SetLegs();
            if (Time.time < stateUntil) return;
            runningAway = false;
            GiveUpTurn();
            nextAttack = Time.time + Random.Range(attackCooldown.x, attackCooldown.y);
            Enter(State.Circle, 0f);
        }

        private void DoFlinch()
        {
            Stop();
            if (Time.time < stateUntil) return;

            if (!fled && fleeBelowHealth > 0f && health != null && health.Value / startHealth <= fleeBelowHealth)
            {
                fled = true;
                var away = transform.position - PlayerFloor();
                away.y = 0f;
                var point = transform.position + (away.sqrMagnitude > 0.01f ? away.normalized : -transform.forward) * 5f;
                if (NavMesh.SamplePosition(point, out var hit, 3f, NavMesh.AllAreas)) GoTo(hit.position, RunSpeed * 1.1f, true);
                Enter(State.Flee, fleeSeconds);
                SendToOwnFsms("Flee");
                return;
            }
            Enter(noticed ? State.Chase : State.Idle, 1f);
        }

        private void DoFlee()
        {
            Play(runAnimation);
            SetLegs();
            if (Time.time >= stateUntil || (!agent.pathPending && agent.remainingDistance <= 0.3f)) Enter(State.Chase, 0f);
        }

        // ---- Web shot ----

        private bool TryStartWebShot(float distance)
        {
            if (webBall == null || !kind.webs || Time.time < nextWeb || webShootersNow >= maxWebShooters) return false;
            if (distance < webRange.x || distance > webRange.y || !CanSeePlayer()) return false;

            isShooting = true;
            webShootersNow++;
            webLaunched = false;
            webStart = Time.time;
            Enter(State.WebShot, webDuration);
            Stop();
            Play(!string.IsNullOrEmpty(webAnimation) && HasAnimation(webAnimation) ? webAnimation : alertAnimation, true);
            if (playing == webAnimation) StartMakingWeb();
            SendToOwnFsms("WebShot");
            return true;
        }

        // With a web animation and its bone, the ball is there from the start, growing on the bone until it's shot.
        private void StartMakingWeb()
        {
            if (webHold == null && !string.IsNullOrEmpty(webHoldBone) && animator != null) webHold = FindBone(animator.transform, webHoldBone);
            if (webHold == null) return;

            heldWebScale = webBall.transform.localScale * Mathf.Clamp(Size(), 0.6f, 1.6f);
            heldWeb = Instantiate(webBall, webHold.position, webHold.rotation);
            heldWeb.transform.localScale = Vector3.zero;
            if (heldWeb.TryGetComponent(out WebBall web)) web.Hold();
        }

        private static Transform FindBone(Transform under, string boneName)
        {
            if (under.name == boneName) return under;
            foreach (Transform child in under)
            {
                var found = FindBone(child, boneName);
                if (found != null) return found;
            }
            return null;
        }

        // After the Animator has posed the bone.
        private void LateUpdate()
        {
            ShowLeap();
            if (heldWeb == null || webHold == null) return;
            heldWeb.transform.SetPositionAndRotation(webHold.position, webHold.rotation);
            float parentScale = webHold.parent != null ? Mathf.Max(0.0001f, webHold.parent.lossyScale.x) : 1f;
            heldWeb.transform.localScale = heldWebScale * Mathf.Max(0f, webHold.lossyScale.x / parentScale);
        }

        // Faces the player and spits the ball at Web Release Time, aimed a little ahead if they're moving.
        private void DoWebShot()
        {
            var target = PlayerFloor();
            Face(target);
            if (!webLaunched && Time.time - webStart >= webReleaseTime)
            {
                webLaunched = true;
                LaunchWeb();
            }
            if (Time.time < stateUntil) return;
            StopShooting();
            Enter(State.Circle, 0f);
        }

        private void LaunchWeb()
        {
            float size = Size();
            var mouth = transform.position + transform.forward * (0.35f * size) + Vector3.up * (0.25f * size);
            var head = player.Camera.transform.position;
            var chest = head + Vector3.down * 0.45f;
            var playerBody = player.GetComponent<CharacterController>();
            var lead = playerBody != null ? Vector3.ProjectOnPlane(playerBody.velocity, Vector3.up) : Vector3.zero;
            chest += lead * ((chest - mouth).magnitude / webSpeed);

            GameObject ball;
            if (heldWeb != null)
            {
                // Thrown from where its legs hold it, at full size.
                ball = heldWeb;
                heldWeb = null;
                mouth = ball.transform.position;
                ball.transform.localScale = heldWebScale;
            }
            else
            {
                ball = Instantiate(webBall, mouth, Quaternion.identity);
                ball.transform.localScale = webBall.transform.localScale * Mathf.Clamp(size, 0.6f, 1.6f);
            }
            var velocity = WebBall.Aim(mouth, chest, webSpeed, -Physics.gravity.y);
            if (ball.TryGetComponent(out WebBall web)) web.Launch(velocity, transform);
            else if (ball.TryGetComponent(out Rigidbody body)) body.linearVelocity = velocity;
            if (webSound != null) PlayOne(webSound);
        }

        private void StopShooting()
        {
            // Interrupted (hit, killed, ...) before it was thrown: the half-made ball bursts.
            if (heldWeb != null)
            {
                if (heldWeb.TryGetComponent(out WebBall web)) web.Burst();
                else Destroy(heldWeb);
                heldWeb = null;
            }
            if (!isShooting) return;
            isShooting = false;
            webShootersNow = Mathf.Max(0, webShootersNow - 1);
            nextWeb = Time.time + Random.Range(webCooldown.x, Mathf.Max(webCooldown.x, webCooldown.y));
        }

        // ---- Dodging ----

        private bool HasDodge => HasAnimationQuiet(dodgeBackAnimation) || HasAnimationQuiet(dodgeLeftAnimation) || HasAnimationQuiet(dodgeRightAnimation);

        // About 30 times a second while it could dodge: is a swing or something thrown about to reach it? Hands and
        // held weapons are tracked by how fast they move; flying things by their velocity.
        private void ScanThreats()
        {
            nextThreatScan = Time.time + 0.033f;
            bool canDodge = noticed && Time.time >= nextDodge && kind.dodgeChance > 0f && HasDodge &&
                            (state == State.Chase || state == State.Circle || state == State.Prepare || state == State.Retreat) &&
                            PlayerDistance() < 8f;
            float elapsed = lastThreatScan < 0f ? 0f : Time.time - lastThreatScan;
            lastThreatScan = Time.time;
            bool measure = canDodge && elapsed > 0.001f && elapsed < 0.12f;

            float size = Size();
            var center = transform.position + Vector3.up * (0.2f * size);
            if (hands.Count == 0) FindHands();

            Vector3 threatVelocity = Vector3.zero, threatPoint = Vector3.zero;
            bool melee = false, found = false;
            foreach (var hand in hands)
            {
                if (hand == null) continue;
                var point = HandPoint(hand);
                if (measure && lastThreatPoints.TryGetValue(hand, out var before))
                {
                    var velocity = (point - before) / elapsed;
                    if (velocity.magnitude >= swingSpeed && WillPass(point - center, velocity, 0.6f + 0.3f * size, 0.25f))
                    {
                        found = melee = true;
                        threatVelocity = velocity;
                        threatPoint = point;
                    }
                }
                lastThreatPoints[hand] = point;
            }

            if (!found && canDodge)
            {
                int count = Physics.OverlapSphereNonAlloc(center, 4f, nearby, Physics.AllLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count && !found; i++)
                {
                    var flyer = nearby[i].attachedRigidbody;
                    if (flyer == null || flyer.isKinematic || flyer.transform.IsChildOf(transform) || flyer.GetComponent<NavMeshAgent>() != null) continue;
                    var velocity = flyer.linearVelocity;
                    if (velocity.magnitude >= throwSpeed && WillPass(flyer.worldCenterOfMass - center, velocity, 0.45f + 0.25f * size, 0.45f))
                    {
                        found = true;
                        threatVelocity = velocity;
                        threatPoint = flyer.worldCenterOfMass;
                    }
                }
            }

            if (!found || !measure) return;
            // One roll per threat: if it doesn't dodge this one, it doesn't keep trying for the same swing.
            nextDodge = Time.time + 0.6f;
            if (Random.value > kind.dodgeChance) return;
            StartDodge(threatPoint - center, threatVelocity, melee);
        }

        // Whether something at 'offset' (from its middle) moving at 'velocity' comes within 'reach' in the next 'within' seconds.
        private static bool WillPass(Vector3 offset, Vector3 velocity, float reach, float within)
        {
            float speed2 = velocity.sqrMagnitude;
            if (speed2 < 0.0001f || Vector3.Dot(offset, velocity) >= 0f) return false;
            float t = Mathf.Clamp(-Vector3.Dot(offset, velocity) / speed2, 0f, within);
            return (offset + velocity * t).sqrMagnitude <= reach * reach;
        }

        // A sideways swing: hop back, out of reach. An overhead smash, a stab, or something thrown: hop to the side,
        // away from its path. A side with no room (a wall, the NavMesh edge) is swapped for the other side, then back.
        private void StartDodge(Vector3 offset, Vector3 velocity, bool melee)
        {
            var flat = Vector3.ProjectOnPlane(velocity, Vector3.up);
            var back = transform.position - PlayerFloor();
            back.y = 0f;
            back = back.sqrMagnitude > 0.0001f ? back.normalized : -transform.forward;

            bool side = !melee || -velocity.y > flat.magnitude || Vector3.Angle(flat, -Vector3.ProjectOnPlane(offset, Vector3.up)) < 35f;
            var across = flat.sqrMagnitude > 0.0001f ? Vector3.Cross(Vector3.up, flat.normalized) : transform.right;
            // Away from the side its path passes on.
            var passes = Vector3.ProjectOnPlane(offset + velocity * Mathf.Clamp(-Vector3.Dot(offset, velocity) / Mathf.Max(velocity.sqrMagnitude, 0.0001f), 0f, 0.45f), Vector3.up);
            float sign = Vector3.Dot(passes, across) > 0f ? -1f : 1f;
            if (Mathf.Abs(Vector3.Dot(passes, across)) < 0.05f) sign = Random.value < 0.5f ? -1f : 1f;

            var choices = side
                ? new[] { across * sign, -across * sign, back }
                : new[] { back, across * sign, -across * sign };
            foreach (var direction in choices)
            {
                var animation = DodgeAnimationFor(direction);
                if (animation == null || !HasRoom(direction)) continue;

                dodgeVelocity = direction * (dodgeDistance / dodgeSeconds);
                nextDodge = Time.time + dodgeCooldown;
                Enter(State.Dodge, dodgeSeconds);
                Stop();
                Play(animation, true);
                SendToOwnFsms("Dodge");
                return;
            }
        }

        private void DoDodge()
        {
            agent.Move(dodgeVelocity * Time.deltaTime);
            if (Time.time < stateUntil) return;
            Enter(hasTurn ? State.Chase : State.Circle, 0f);
        }

        // The animation for hopping that way, judged from where it faces: back, left or right. Null if none fits.
        private string DodgeAnimationFor(Vector3 direction)
        {
            var local = transform.InverseTransformDirection(direction);
            if (Mathf.Abs(local.x) >= Mathf.Abs(local.z))
                return local.x > 0f ? Usable(dodgeRightAnimation) : Usable(dodgeLeftAnimation);
            return local.z < 0f ? Usable(dodgeBackAnimation) : null;
        }

        private string Usable(string animation)
        {
            return HasAnimationQuiet(animation) ? animation : null;
        }

        private bool HasRoom(Vector3 direction)
        {
            var from = transform.position;
            return !NavMesh.Raycast(from, from + direction * dodgeDistance, out var hit, NavMesh.AllAreas) || hit.distance >= dodgeDistance * 0.7f;
        }

        private void FindHands()
        {
            foreach (var hand in player.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor>(true)) hands.Add(hand.transform);
        }

        // The hand, or what it holds (a weapon reaches further than the hand).
        private static Vector3 HandPoint(Transform hand)
        {
            if (hand.TryGetComponent(out UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor interactor) && interactor.hasSelection &&
                interactor.firstInteractableSelected is Component held && held != null)
                return held.TryGetComponent(out Rigidbody body) ? body.worldCenterOfMass : held.transform.position;
            return hand.position;
        }

        // ---- Hits and death ----

        private void OnAnyHit(EnemyHitDamage enemy, HitResult hit)
        {
            if (enemy == null || enemy.gameObject != gameObject || state == State.Dead) return;

            if (hitEffect != null) hitEffect.SetActive(true);
            PlayOne(hitSounds);
            if (!noticed) Notice(true);
            if (state == State.Appear || Time.time < nextFlinch) return;

            nextFlinch = Time.time + flinchCooldown;
            GiveUpTurn();
            StopShooting();
            Enter(State.Flinch, flinchSeconds);
            Stop();
            Play(hitAnimation, true);
            SendToOwnFsms("Flinch");
        }

        // The Health FSM leaves its first state when the spider dies or is popped by the fairy's shield.
        private bool IsDying()
        {
            if (health != null && health.Value <= 0f) return true;
            if (healthFsm == null || !healthFsm.enabled || healthFsm.Fsm == null) return false;
            var current = healthFsm.ActiveStateName;
            return !string.IsNullOrEmpty(current) && current != healthFsm.Fsm.StartState;
        }

        // The Health FSM plays the death; the brain only lets go.
        private void Die()
        {
            state = State.Dead;
            GiveUpTurn();
            StopShooting();
            Stop();
            if (animator != null) animator.speed = 1f;
            if (leapLift > 0f) StartCoroutine(FallWhenDead());
            foreach (var flag in setOnDeath)
            {
                if (flag != null && !string.IsNullOrEmpty(flag.boolName)) GameStages.SetFsmBool(flag.globalObject, flag.fsm, flag.boolName, flag.value, this);
            }
            SendToOwnFsms("Died");
            enabled = false;
        }

        // ---- Taking turns ----

        private bool TryTakeTurn()
        {
            if (hasTurn) return true;
            if (Time.time < nextAttack || attackersNow >= maxAttackers) return false;
            hasTurn = true;
            attackersNow++;
            return true;
        }

        private void GiveUpTurn()
        {
            if (!hasTurn) return;
            hasTurn = false;
            attackersNow = Mathf.Max(0, attackersNow - 1);
        }

        private AttackMove PickAttack(float distance)
        {
            float total = 0f;
            foreach (var attack in attacks)
            {
                if (Fits(attack, distance)) total += attack.weight;
            }
            if (total <= 0f) return null;

            float roll = Random.value * total;
            foreach (var attack in attacks)
            {
                if (!Fits(attack, distance)) continue;
                roll -= attack.weight;
                if (roll <= 0f) return attack;
            }
            return null;
        }

        private bool Fits(AttackMove attack, float distance)
        {
            // An attack whose animation isn't in the Animator yet is left out (a face leap without its animation would
            // just float up in whatever pose it's in).
            return attack != null && attack.weight > 0f && distance >= attack.minDistance && distance <= attack.maxDistance &&
                   distance <= Reachable(attack) && UsedByMe(attack) && HasAnimationQuiet(attack.animation);
        }

        // The furthest the player can be for this attack to land: its hop, then its reach and bite at this size, then
        // the player's body. A short hop isn't picked from too far (it closes in and tries again).
        private float Reachable(AttackMove attack)
        {
            return attack.lunge + (attack.reach + attack.biteRadius) * Size() + playerBodyRadius;
        }

        private bool UsedByMe(AttackMove attack)
        {
            var mine = kindName switch
            {
                SizeClass.Small => Sizes.Small,
                SizeClass.Boss => Sizes.Boss,
                _ => Sizes.Normal,
            };
            // Nothing ticked counts as everyone, so an attack is never left unused by mistake.
            return attack.usedBy == 0 || (attack.usedBy & mine) != 0;
        }

        // Close enough that its longest hop still puts its mouth on the player: a small spider, with its short reach,
        // comes in closer before winding up.
        private float WindUpDistance()
        {
            float hop = 0f, reach = 0f;
            foreach (var attack in attacks)
            {
                if (attack == null || attack.weight <= 0f || !UsedByMe(attack)) continue;
                hop = Mathf.Max(hop, attack.lunge);
                reach = Mathf.Max(reach, attack.reach);
            }
            return Mathf.Min(attackRange, hop * 0.85f + reach * Size());
        }

        // ---- Moving and looks ----

        private void Run(Vector3 point, float speedShare = 1f)
        {
            // Skittering: bursts of running with tiny stops in between.
            if (skitter && Time.time >= burstUntil)
            {
                pausing = !pausing;
                burstUntil = Time.time + (pausing ? Random.Range(pauseSeconds.x, pauseSeconds.y) : Random.Range(burstSeconds.x, burstSeconds.y));
            }
            if (skitter && pausing)
            {
                Stop();
                Play(idleAnimation);
                return;
            }

            if (Time.time >= nextRepath || agent.isStopped)
            {
                nextRepath = Time.time + RepathEvery;
                GoTo(point, RunSpeed * speedShare, true);
            }
            Play(runAnimation);
            SetLegs();
        }

        private void GoTo(Vector3 point, float speed, bool turnToPath)
        {
            agent.speed = speed;
            agent.updateRotation = turnToPath;
            agent.isStopped = false;
            agent.SetDestination(point);
        }

        private void Stop()
        {
            if (!agent.isOnNavMesh) return;
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            agent.updateRotation = false;
        }

        private void Face(Vector3 point)
        {
            var to = point - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), TurnSpeed * Time.deltaTime);
        }

        private void Enter(State next, float seconds)
        {
            state = next;
            stateUntil = Time.time + seconds;
        }

        private void Play(string animation, bool restart = false)
        {
            if (animator == null || string.IsNullOrEmpty(animation)) return;
            if (!restart && playing == animation) return;
            if (!HasAnimation(animation)) return;
            playing = animation;
            animator.speed = 1f;
            animator.CrossFadeInFixedTime(animation, crossFade, 0, 0f);
        }

        // A state the Animator doesn't have (e.g. an attack whose animation isn't added yet) is skipped, with one warning.
        private bool HasAnimation(string animation)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return false;
            if (animator.HasState(0, Animator.StringToHash(animation))) return true;
            if (missingAnimations.Add(animation))
                Debug.LogWarning($"[Spider Brain] '{name}': the Animator has no state called \"{animation}\"; skipped.", this);
            return false;
        }

        private bool HasAnimationQuiet(string animation)
        {
            return !string.IsNullOrEmpty(animation) && animator != null && animator.runtimeAnimatorController != null &&
                   animator.HasState(0, Animator.StringToHash(animation));
        }

        // The legs keep pace with how fast it actually moves.
        private void SetLegs()
        {
            if (animator == null) return;
            float speed = agent.velocity.magnitude;
            if (!string.IsNullOrEmpty(walkBackAnimation) && playing == walkBackAnimation)
                animator.speed = Mathf.Clamp(speed / (walkBackAnimationSpeed * Size()), legSpeedRange.x, legSpeedRange.y);
            else
                animator.speed = playing == runAnimation || playing == walkAnimation
                    ? Mathf.Clamp(speed / (runAnimationSpeed * Size()), legSpeedRange.x, legSpeedRange.y)
                    : 1f;
        }

        private void Grow()
        {
            if (growSeconds <= 0f) return;
            float t = (Time.time - bornAt) / growSeconds;
            if (t > 1.05f) return;
            t = Mathf.Clamp01(t);
            transform.localScale = fullScale * Mathf.Lerp(growFrom, 1f, t * t);
        }

        private void ChooseSize()
        {
            kindName = size;
            if (kindName == SizeClass.Random)
            {
                float total = small.weight + normal.weight + boss.weight;
                float roll = Random.value * total;
                kindName = total <= 0f || roll < normal.weight ? SizeClass.Normal
                    : roll < normal.weight + small.weight ? SizeClass.Small
                    : SizeClass.Boss;
            }
            kind = kindName switch
            {
                SizeClass.Small => small,
                SizeClass.Boss => boss,
                _ => normal,
            };
            fullScale = Vector3.one * Random.Range(kind.scale.x, Mathf.Max(kind.scale.x, kind.scale.y));
            transform.localScale = fullScale * (growSeconds > 0f ? growFrom : 1f);
        }

        private void PickModel()
        {
            if (models == null) return;
            var hidden = new List<GameObject>();
            foreach (Transform child in models)
            {
                if (!child.gameObject.activeSelf && child.GetComponentInChildren<Renderer>(true) != null) hidden.Add(child.gameObject);
            }
            if (hidden.Count > 0) hidden[Random.Range(0, hidden.Count)].SetActive(true);
            transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }

        // ---- The player ----

        private float Size()
        {
            return Mathf.Max(0.01f, transform.lossyScale.y);
        }

        // Where the player stands: under their head, at the spider's height.
        private Vector3 PlayerFloor()
        {
            var head = player.Camera.transform.position;
            return new Vector3(head.x, transform.position.y, head.z);
        }

        private float PlayerDistance()
        {
            var to = PlayerFloor() - transform.position;
            return to.magnitude;
        }

        private static bool IsPlayer(Collider collider)
        {
            return player != null && collider.transform.IsChildOf(player.transform);
        }

        // A weapon (or anything) held in a hand, or a fist's punching ball.
        private static bool IsHeldOrFist(Collider collider)
        {
            var body = collider.attachedRigidbody;
            if (body == null) return false;
            if (Fist.IsFistBall(body)) return true;
            return body.TryGetComponent(out XRGrabInteractable grab) && grab.isSelected;
        }

        private static void SendToPlayer(string eventName)
        {
            if (string.IsNullOrEmpty(eventName)) return;
            var global = FsmVariables.GlobalVariables.FindFsmGameObject(PlayerVariable);
            var target = global != null && global.Value != null ? global.Value : player != null ? player.gameObject : null;
            if (target == null) return;
            target.GetComponents(fsmBuffer);
            foreach (var fsm in fsmBuffer)
            {
                if (fsm.FsmName == PlayerDamageFsm) fsm.SendEvent(eventName);
            }
        }

        private void SendToOwnFsms(string eventName)
        {
            Signalled?.Invoke(this, eventName);
            foreach (var fsm in fsms)
            {
                if (fsm != null && fsm.enabled) fsm.SendEvent(eventName);
            }
        }

        private void PlayOne(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0) return;
            PlayOne(clips[Random.Range(0, clips.Length)]);
        }

        private void PlayOne(AudioClip clip)
        {
            if (clip == null) return;
            if (speaker != null) speaker.PlayOneShot(clip, volume);
            else AudioSource.PlayClipAtPoint(clip, transform.position, volume);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, sightRange);
            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, attackRange);
            if (attacks == null || attacks.Count == 0 || attacks[0] == null) return;
            float size = Mathf.Max(0.01f, transform.lossyScale.y);
            Gizmos.color = new Color(1f, 0f, 0f, 0.8f);
            Gizmos.DrawWireSphere(transform.position + transform.forward * (attacks[0].reach * size) + Vector3.up * (0.15f * size), attacks[0].biteRadius * size);
        }
#endif
    }
}

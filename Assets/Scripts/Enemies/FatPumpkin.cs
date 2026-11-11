using System.Collections.Generic;
using Game.Combat;
using Game.Inventory;
using Game.VFX;
using UnityEngine;
using UnityEngine.AI;
using FsmVariables = HutongGames.PlayMaker.FsmVariables;

namespace Game.Enemies
{
    /// <summary>
    /// A greedy tree pumpkin (Turns Into Fat Pumpkin makes it): it wakes up where a Pumpkin_On_Tree sat among the candies,
    /// then waddles after every candy lying on the floor, nearest first, sucks each one in and gets fatter and slower. With
    /// no candy left that it can get to (Angry After), it gets angry: it goes red and fumes a moment, then waddles after
    /// the player and bites (it squashes down, then hops at them with a chomp: a bug's bite and a little shove). A candy
    /// it can get to calms it down, and it goes back to eating. The player's hits break it (Health 20 and Toughness 2, user
    /// 2026-11-10: tankier, was 5 and 1; weaker hits add up; their blasts too), and it gives back only a few of what it ate
    /// (Drop Share).
    /// Two of them that meet fight (user, 2026-11-04: "make fat pumpkin eat each other, the 1st to bite win and get bonus
    /// stat"): within Duel Range they forget the candy and go for each other, each winds up a bite (a little sooner or
    /// later, by chance) and lunges; the first bite that lands wins: it sucks the other into its mouth and gets Winner
    /// Speed times as fast, Winner Health times the health, Winner Size times as big and Winner Candies times the candies
    /// when it bursts (again for each one it eats);
    /// what the eaten one had eaten comes out of the winner when it bursts.
    /// Once it's Danger Height tall (user, 2026-11-05: "when it gets to a certain size, like bigger than the Player, it
    /// will come and start jump on Player to push Player to fall edge") it's a giant for good: it roars, stays red, forgets
    /// the candy and comes for the player; in Stomp Range it squashes down and leaps onto where they stand, and landing on
    /// them it hurts (Stomp Event) and throws them towards the nearest Fall Edge (away from it if there's none near). A
    /// giant has Giant Health and shrugs hits off (user, 2026-11-10): they hurt, but don't knock it back, stop it or cancel
    /// its leap.
    /// Goes on the prefab's root, next to its NavMesh Agent. Its look is under Model > Jiggle: an Animator with a controller
    /// found under it replaces the waddle, and gets the Walking bool, the Walk Speed float and the Eat, Hit and Laugh triggers
    /// if its controller has them (Tools > 429 Game > Enemies > Set Up Fat Pumpkin Animation makes all that).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public class FatPumpkin : MonoBehaviour, IHittable
    {
        [Header("Look")]
        [Tooltip("The pivot that waddles (on the floor, under the model).")]
        [SerializeField] private Transform model;
        [Tooltip("Wobbles at every gulp and hit.")]
        [SerializeField] private Jiggle jiggle;
        [Tooltip("Its body's radius, in metres at size 1: how far its mouth is from its middle.")]
        [Min(0.01f)]
        [SerializeField] private float radius = 0.27f;
        [Tooltip("Where the candy flies to. Empty: just inside the front of its body.")]
        [SerializeField] private Transform mouth;
        [Tooltip("How far it rocks from side to side as it walks, in degrees, and how high it hops, in metres. Not with an animation.")]
        [SerializeField] private Vector2 waddle = new(9f, 0.04f);
        [Tooltip("Steps per second at full walking speed.")]
        [Min(0f)]
        [SerializeField] private float waddlePerSecond = 2.2f;

        [Header("Waking up")]
        [Tooltip("Played where it bursts out of the tree pumpkin (Effect Pool), and their size.")]
        [SerializeField] private GameObject[] breakOutEffects = System.Array.Empty<GameObject>();
        [Min(0f)]
        [SerializeField] private float breakOutEffectSize = 0.6f;
        [SerializeField] private AudioClip breakOutSound;
        [Tooltip("Seconds it looks at the player, laughing, before it goes for the candy.")]
        [Min(0f)]
        [SerializeField] private float wakeSeconds = 0.8f;
        [Tooltip("Its laugh as it wakes up, and now and then while it eats (one at random).")]
        [SerializeField] private AudioClip[] laughs = System.Array.Empty<AudioClip>();
        [Tooltip("Seconds between laughs (min, max).")]
        [SerializeField] private Vector2 laughEvery = new(4f, 8f);
        [Tooltip("Its voice's pitch: lower is fatter.")]
        [Range(0.5f, 2f)]
        [SerializeField] private float voicePitch = 0.85f;

        [Header("Eating")]
        [Tooltip("How fast it waddles when it's thin, in metres per second.")]
        [Min(0.1f)]
        [SerializeField] private float walkSpeed = 1.5f;
        [Tooltip("The share of that speed left once it's as fat as it gets.")]
        [Range(0.1f, 1f)]
        [SerializeField] private float fullSpeedShare = 0.6f;
        [Tooltip("How far off it notices a candy, in metres.")]
        [Min(1f)]
        [SerializeField] private float sightRange = 30f;
        [Tooltip("A candy this close to its side is sucked in, in metres.")]
        [Min(0f)]
        [SerializeField] private float eatReach = 0.35f;
        [Tooltip("Seconds a candy takes to fly into its mouth, and how long it stands still for each gulp.")]
        [Min(0.05f)]
        [SerializeField] private float eatSeconds = 0.25f;
        [Tooltip("It gets this much bigger for every candy (0.02 = 2%; user, 2026-11-05: a little too big at the start)...")]
        [Min(0f)]
        [SerializeField] private float growPerCandy = 0.02f;
        [Tooltip("...up to this many times its first size.")]
        [Min(1f)]
        [SerializeField] private float mostGrowth = 1.6f;
        [Tooltip("Every candy it swallows adds health (user, 2026-11-11: \"twice the amount that'd heal the Player, so the more " +
                 "eating, the more tankier\"): a roll of Candy Heal Roll (the player's candy: 1 to 7) times Candy Heal Times. " +
                 "No limit. A giant keeps it if it's above Giant Health.")]
        [SerializeField] private Vector2Int candyHealRoll = new(1, 7);
        [Min(0f)]
        [SerializeField] private float candyHealTimes = 2f;
        [Tooltip("Played at every gulp (at most one every Gulp Gap).")]
        [SerializeField] private AudioClip eatSound;
        [Range(0f, 1f)]
        [SerializeField] private float eatVolume = 0.8f;

        [Header("Angry (no candy left)")]
        [Tooltip("With no candy it can get to for this long, in seconds, it gets angry and goes for the player.")]
        [Min(0f)]
        [SerializeField] private float angryAfter = 1.5f;
        [Tooltip("Seconds it stands fuming, going red, before it comes for them.")]
        [Min(0f)]
        [SerializeField] private float fumeSeconds = 0.9f;
        [Tooltip("How red it goes.")]
        [SerializeField] private Color angryColour = new(1f, 0.3f, 0.2f, 1f);
        [Tooltip("Played as it gets angry, and now and then while it's angry. Empty: its laugh, low and slow.")]
        [SerializeField] private AudioClip angrySound;
        [Tooltip("Played over its head as it gets angry (Effect Pool), e.g. a puff of steam, and their size. Empty: none.")]
        [SerializeField] private GameObject[] angryEffects = System.Array.Empty<GameObject>();
        [Min(0f)]
        [SerializeField] private float angryEffectSize = 0.5f;
        [Tooltip("How fast it waddles after the player, as a share of its walking speed (still slower the fatter it is).")]
        [Min(0.1f)]
        [SerializeField] private float chaseSpeedShare = 1.3f;
        [Tooltip("It bites from this far, in metres from its side to where the player stands...")]
        [Min(0.1f)]
        [SerializeField] private float biteReach = 0.6f;
        [Tooltip("...squashing down first for this long, in seconds (the warning)...")]
        [Min(0f)]
        [SerializeField] private float biteWindUp = 0.45f;
        [Tooltip("...then hopping at them this far (x, metres) over this long (y, seconds).")]
        [SerializeField] private Vector2 biteLunge = new(0.7f, 0.25f);
        [Tooltip("Seconds from one bite to the next.")]
        [Min(0.2f)]
        [SerializeField] private float biteEvery = 1.6f;
        [Tooltip("Sent to the player's Damage FSM when a bite lands: Damage (1-3 health, a bug's bite), DamageHeavy (6-12).")]
        [SerializeField] private string biteEvent = "Damage";
        [Tooltip("A bite that lands shoves the player back this high (x) and this far (y), in metres (their Toughness cuts it).")]
        [SerializeField] private Vector2 biteShove = new(0.15f, 0.6f);
        [Tooltip("Played as it bites. Empty: its gulp.")]
        [SerializeField] private AudioClip biteSound;

        [Header("Eating each other")]
        [Tooltip("Two that come this close (metres between their sides) fight: the first to bite eats the other.")]
        [Min(0f)]
        [SerializeField] private float duelRange = 2.5f;
        [Tooltip("Each waits up to this many seconds more before it winds up its bite (by chance): who bites first.")]
        [Min(0f)]
        [SerializeField] private float duelHeadStart = 0.5f;
        [Tooltip("A fight that goes on this long (they can't reach each other) is given up.")]
        [Min(1f)]
        [SerializeField] private float duelGiveUp = 8f;
        [Tooltip("Seconds the eaten one takes to go into the winner's mouth.")]
        [Min(0.05f)]
        [SerializeField] private float eatEachOtherSeconds = 0.5f;
        [Tooltip("The winner is this many times as fast...")]
        [Min(1f)]
        [SerializeField] private float winnerSpeed = 1.75f;
        [Tooltip("...has this many times the health...")]
        [Min(1f)]
        [SerializeField] private float winnerHealth = 2f;
        [Tooltip("...and is this many times as big, for each one it eats...")]
        [Min(1f)]
        [SerializeField] private float winnerSize = 1.5f;
        [Tooltip("...and gives this many times the candies when it bursts (user, 2026-11-04: \"3x reward candy also\").")]
        [Min(1f)]
        [SerializeField] private float winnerCandies = 3f;

        [Header("Giant (user, 2026-11-05)")]
        [Tooltip("Once it's this tall, in metres (its body: twice Radius times its size), it's a giant: it comes for the " +
                 "player and stomps. It starts about 0.72 m tall; candies make it up to Most Growth times that, each fight it " +
                 "wins Winner Size times.")]
        [Min(0.1f)]
        [SerializeField] private float dangerHeight = 1.4f;
        [Tooltip("Its roar as it turns giant.")]
        [SerializeField] private AudioClip giantSound;
        [Tooltip("A giant walks at this share of Walk Speed (not slowed by how fat it is).")]
        [Min(0.1f)]
        [SerializeField] private float giantSpeedShare = 1f;
        [Tooltip("Turning giant, its health goes up to this (user, 2026-11-10: 300, whatever fights it won before). 0 = " +
                 "unchanged.")]
        [Min(0f)]
        [SerializeField] private float giantHealth = 300f;
        [Tooltip("It leaps at a player this close, in metres (flat), onto where they stand...")]
        [Min(0.5f)]
        [SerializeField] private float stompRange = 4.5f;
        [Tooltip("...after squashing down for this long, in seconds...")]
        [Min(0.05f)]
        [SerializeField] private float stompWindUp = 0.6f;
        [Tooltip("...in a leap this long, in seconds, this high, in metres...")]
        [Min(0.1f)]
        [SerializeField] private float stompSeconds = 0.7f;
        [Min(0f)]
        [SerializeField] private float stompHop = 0.9f;
        [Tooltip("...and again after this many seconds (x to y) once it's landed.")]
        [SerializeField] private Vector2 stompEvery = new(1.6f, 2.6f);
        [Tooltip("It lands on the player if they're within its body plus this, in metres.")]
        [Min(0f)]
        [SerializeField] private float stompReach = 0.4f;
        [Tooltip("Sent to the player's Damage FSM when it lands on them: Damage (6-9), DamageHeavy (40-50).")]
        [SerializeField] private string stompEvent = "DamageHeavy";
        [Tooltip("How it throws a player it landed on: x = how high, y = how far, in metres, towards the nearest Fall Edge " +
                 "within Edge Search metres (away from it with none).")]
        [SerializeField] private Vector2 stompPush = new(1f, 3.5f);
        [Min(0f)]
        [SerializeField] private float edgeSearch = 12f;
        [Tooltip("Its landing thud, and the dust it kicks up (none: Break Out Effects), this big times its size.")]
        [SerializeField] private AudioClip stompSound;
        [SerializeField] private GameObject[] stompEffects = System.Array.Empty<GameObject>();
        [Min(0f)]
        [SerializeField] private float stompEffectSize = 0.8f;

        [Header("Breaking")]
        [Tooltip("Its health, in the game's whole-number damage (a closed-fist punch does 1, hard 2; a flying jab 1 + the " +
                 "Fist Size points; weapons and throws their own). 20 since 2026-11-10 (user: tankier, was 5).")]
        [Min(1f)]
        [SerializeField] private float health = 20f;
        [Tooltip("How hits turn into damage (the enemies' shared Hit Settings asset). Empty: the default numbers.")]
        [SerializeField] private HitSettings hitSettings;
        [Tooltip("Divides the hit power, like an enemy's Toughness (2 since 2026-11-10, was 1: a punch has to be twice as " +
                 "fast to hurt).")]
        [Min(0.1f)]
        [SerializeField] private float toughness = 2f;
        [Tooltip("After a hit that hurts, more hits are ignored this long, in seconds, so one swing counts once.")]
        [Min(0f)]
        [SerializeField] private float hitCooldown = 0.2f;
        [Tooltip("A hit that doesn't break it knocks it back this far, in metres, and it stands stunned this long, in seconds " +
                 "(not a giant: it shrugs hits off).")]
        [SerializeField] private Vector2 knock = new(0.8f, 0.5f);
        [Tooltip("Played at a hit that doesn't break it (one at random).")]
        [SerializeField] private AudioClip[] hitSounds = System.Array.Empty<AudioClip>();
        [Tooltip("Played where it bursts (Effect Pool), and their size.")]
        [SerializeField] private GameObject[] burstEffects = System.Array.Empty<GameObject>();
        [Min(0f)]
        [SerializeField] private float burstEffectSize = 0.7f;
        [SerializeField] private AudioClip burstSound;
        [Tooltip("The pieces it bursts into (a Broken_Stuffs prefab), and their size for each of its own. Empty: none.")]
        [SerializeField] private GameObject pieces;
        [Min(0.01f)]
        [SerializeField] private float piecesSize = 0.55f;
        [Tooltip("EXP the player gets for breaking it.")]
        [Min(0)]
        [SerializeField] private int exp = 2;

        [Header("Candy back")]
        [Tooltip("One of these, picked at random, per candy it gives back.")]
        [SerializeField] private GameObject[] candies = System.Array.Empty<GameObject>();
        [Tooltip("It gives back this share of what it ate, rounded down...")]
        [Range(0f, 1f)]
        [SerializeField] private float dropShare = 0.25f;
        [Tooltip("...but at least this many...")]
        [Min(0)]
        [SerializeField] private int fewestCandies = 1;
        [Tooltip("...and at most this many.")]
        [Min(0)]
        [SerializeField] private int mostCandies = 4;
        [Tooltip("How hard they pop up, in metres per second. Candies break on a hard landing, so keep it low.")]
        [Min(0f)]
        [SerializeField] private float candyLaunchSpeed = 2.2f;

        [Header("Animation (only if its controller has them)")]
        [SerializeField] private string walkingBool = "Walking";
        [SerializeField] private string eatTrigger = "Eat";
        [SerializeField] private string hitTrigger = "Hit";
        [SerializeField] private string laughTrigger = "Laugh";
        [Tooltip("Its walking speed as a share of its thin speed: how fast it waddles.")]
        [SerializeField] private string walkSpeedFloat = "WalkSpeed";

        private enum Phase { Waking, Hunting, Angry, Giant, Gone }
        // A giant: coming for the player, squashing down to leap, in the air.
        private enum StompStep { Coming, WindingUp, Flying }
        private enum Duel { None, Closing, WindingUp, Lunging }

        // Every one that's out, to find the others.
        private static readonly List<FatPumpkin> everyone = new();

        // While angry: fuming, waddling after the player, squashing down to bite, hopping at them.
        private enum Rage { Fuming, Chasing, WindingUp, Lunging }

        private class Swallow
        {
            public GameObject candy;
            public Vector3 from;
            public Vector3 size;
            public float start;
        }

        // How often it looks round for candy, in seconds.
        private const float LookEvery = 0.25f;
        // A candy it can't get to (no path, or it got stuck) is left alone this long, in seconds.
        private const float LeaveAloneSeconds = 6f;
        private const float StuckSeconds = 1.5f;
        // How far from the NavMesh a candy can lie and still be gone for, in metres.
        private const float NavMeshReach = 1f;
        // A candy this far above or below it is out of its reach, in metres.
        private const float EatHeight = 1.2f;
        // At most this many candies in the air to its mouth at once.
        private const int MostSwallowing = 3;
        private const float GulpGap = 0.2f;
        private const float GulpWobble = 1f;
        private const float HitWobble = 1.5f;
        private const float SoftWobble = 0.4f;
        // Its pop out of the tree pumpkin: seconds, and how small it starts.
        private const float PopSeconds = 0.3f;
        private const float PopFrom = 0.5f;
        private const float GrowPerSecond = 0.5f;
        private const float TurnDegrees = 240f;
        private const float MinWalkAnimation = 0.3f;
        private const float RepathMoved = 0.4f;
        private const float KnockSeconds = 0.35f;
        private const float KnockHop = 0.2f;
        // The ring its candies pop out of: how far out from its side, how high, and how fast sideways.
        private const float CandyRingGap = 0.1f;
        private const float CandyHeight = 0.3f;
        private const float CandyOutward = 1f;
        private const string SpawnedStuffVariable = "CurrentlySpawnedStuffs";
        // Angry: how fast it goes red (and back), how low its angry voice is, how far it looks for the player's floor.
        private const float FlushPerSecond = 2.5f;
        private const float AngryPitch = 0.6f;
        private const float PlayerFloorReach = 1.5f;
        // The bite's squash (height, width at the deepest), the hop's height, and how close the hop must get.
        private const float SquashHeight = 0.78f;
        private const float SquashWidth = 1.15f;
        private const float LungeHop = 0.15f;
        private const float LungeStopsShort = 0.15f;
        private const float BiteContact = 0.35f;
        private const string PlayerVariable = "Player";
        private const string PlayerDamageFsm = "Damage";
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        // The candies lying about, as seen by the last one who looked.
        private static readonly List<GameObject> loose = new();

        /// <summary>How many are out right now.</summary>
        public static int Alive { get; private set; }

        /// <summary>Set while the game runs: it goes without the player earning it, so no EXP. See PlayerLevel.NoRewardFor.</summary>
        [System.NonSerialized] public bool noReward;

        /// <summary>How many candies it has eaten.</summary>
        public int Eaten { get; private set; }

        private NavMeshAgent agent;
        private NavMeshPath path;
        private AudioSource speaker;
        private Animator animator;
        private bool hasWalking, hasWalkSpeed, hasEat, hasHit, hasLaugh;
        private Knockback knockback;
        private Transform head;
        private Phase phase;
        private Vector3 fullScale;
        private Vector3 modelHome;
        private float bornAt;
        private float shownGrowth = 1f;
        private float waddleClock;
        private float nextLook;
        private float nextLaugh;
        private float nextGulpSound;
        private float nextHurt;
        private float nextSoftWobble;
        private float pausedUntil;
        private float stuckFor;
        private GameObject target;
        private Vector3 targetWasAt;
        private readonly List<Swallow> swallows = new();
        private readonly Dictionary<GameObject, float> leftAlone = new();
        // Angry: since when there's been no candy for it, what it's doing, until when, and how red it is (0-1).
        private float noCandySince = -1f;
        private Rage rage;
        private float rageUntil;
        private float nextBite;
        private bool bitten;
        private float flush;
        private Vector3 modelScale = Vector3.one;
        private Renderer[] looks = System.Array.Empty<Renderer>();
        private MaterialPropertyBlock block;
        private float nextChasePath;
        private AudioSource growler;
        // Fighting another one: who, how far along, until when; its size from the ones it ate, the candies they had
        // eaten, and whether it's being eaten itself.
        private FatPumpkin rival;
        private Duel duel;
        private float duelUntil;
        private float duelStarted;
        private float sizeBoost = 1f;
        private float candyBoost = 1f;
        private float shownSizeBoost = 1f;
        private int eatenWithThem;
        private bool beingEaten;
        // A giant: what it's doing, and when it may leap again.
        private StompStep stomp;
        private float nextStomp;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Alive = 0;
            everyone.Clear();
        }

        private void OnEnable()
        {
            Alive++;
            everyone.Add(this);
        }

        private void OnDisable()
        {
            Alive--;
            everyone.Remove(this);
        }

        private void Start()
        {
            agent = GetComponent<NavMeshAgent>();
            agent.updateRotation = false;
            if (agent.isOnNavMesh) agent.isStopped = true;
            path = new NavMeshPath();
            TryGetComponent(out speaker);
            if (speaker != null) speaker.pitch = voicePitch;
            FindAnimation();
            if (model != null)
            {
                modelHome = model.localPosition;
                modelScale = model.localScale;
            }
            var found = new List<Renderer>();
            foreach (var part in GetComponentsInChildren<Renderer>(true))
            {
                if (part is MeshRenderer || part is SkinnedMeshRenderer) found.Add(part);
            }
            looks = found.ToArray();
            fullScale = transform.localScale;
            bornAt = Time.time;
            head = Camera.main != null ? Camera.main.transform : null;
            nextLaugh = Time.time + Random.Range(laughEvery.x, laughEvery.y);

            foreach (var effect in breakOutEffects)
            {
                if (effect != null) EffectPool.Play(effect, Middle(), breakOutEffectSize);
            }
            if (breakOutSound != null) AudioSource.PlayClipAtPoint(breakOutSound, Middle());
            Laugh();
            phase = Phase.Waking;
        }

        // The model that replaces the first one brings its own animation.
        private void FindAnimation()
        {
            foreach (var found in GetComponentsInChildren<Animator>(true))
            {
                if (found.runtimeAnimatorController == null) continue;
                animator = found;
                foreach (var parameter in found.parameters)
                {
                    if (parameter.name == walkingBool) hasWalking = true;
                    else if (parameter.name == eatTrigger) hasEat = true;
                    else if (parameter.name == hitTrigger) hasHit = true;
                    else if (parameter.name == laughTrigger) hasLaugh = true;
                    else if (parameter.name == walkSpeedFloat) hasWalkSpeed = true;
                }
                return;
            }
        }

        private void Update()
        {
            if (phase == Phase.Gone || beingEaten) return;
            FlySwallows();
            Grow();
            Flush();
            if (phase == Phase.Waking)
            {
                TurnTo(HeadFlat());
                if (Time.time - bornAt >= wakeSeconds) phase = Phase.Hunting;
                return;
            }
            // Big enough: a giant for good, and it only cares about the player.
            if (phase != Phase.Giant && rival == null && Height >= dangerHeight) BecomeGiant();
            if (phase == Phase.Giant)
            {
                Stomp();
                return;
            }
            if (rival == null) LookForRival();
            if (rival != null)
            {
                Fight();
                return;
            }
            if (phase == Phase.Angry) Rampage();
            else Hunt();
        }

        // ---------- Going after the candy ----------

        private void Hunt()
        {
            if (Time.time >= nextLook)
            {
                nextLook = Time.time + LookEvery;
                LookForCandy();
                // Nothing left that it can get to (and nothing on its way to its mouth): a moment of that, and it's angry.
                if (target != null || swallows.Count > 0) noCandySince = -1f;
                else if (noCandySince < 0f) noCandySince = Time.time;
                else if (Time.time - noCandySince >= angryAfter)
                {
                    GetAngry();
                    return;
                }
            }
            bool standing = Time.time < pausedUntil || target == null;
            bool knocked = knockback != null && knockback.IsKnockedBack;
            if (agent.isOnNavMesh)
            {
                agent.isStopped = standing || knocked;
                // The fatter, the slower.
                float fatness = Mathf.InverseLerp(1f, Mathf.Max(1.01f, mostGrowth), Growth);
                agent.speed = walkSpeed * Mathf.Lerp(1f, fullSpeedShare, fatness);
            }

            var velocity = agent.velocity;
            velocity.y = 0f;
            float speed = velocity.magnitude;
            WatchForStuck(standing || knocked, speed);
            if (speed > 0.1f) TurnTo(velocity);
            else if (target != null) TurnTo(target.transform.position - transform.position);
            else TurnTo(HeadFlat());
            Waddle(speed);
            if (hasWalking) animator.SetBool(walkingBool, speed > 0.1f);
            if (hasWalkSpeed) animator.SetFloat(walkSpeedFloat, Mathf.Clamp(speed / walkSpeed, MinWalkAnimation, 1f));

            if (Time.time >= nextLaugh)
            {
                nextLaugh = Time.time + Random.Range(laughEvery.x, laughEvery.y);
                Laugh();
            }
        }

        // Sucks in what's in reach, and goes for the nearest of the others.
        private void LookForCandy()
        {
            CandyHelmet.FindLoose(loose);
            var here = transform.position;
            float mouthReach = radius * Size + eatReach;
            GameObject nearest = null;
            float best = sightRange * sightRange;
            foreach (var candy in loose)
            {
                var to = candy.transform.position - here;
                float height = Mathf.Abs(to.y);
                to.y = 0f;
                float away = to.sqrMagnitude;
                if (away <= mouthReach * mouthReach && height <= EatHeight)
                {
                    if (swallows.Count < MostSwallowing) Take(candy);
                    continue;
                }
                if (away >= best || LeftAlone(candy)) continue;
                best = away;
                nearest = candy;
            }
            if (target == null || !loose.Contains(target) || nearest != target) GoFor(nearest);
            else if ((target.transform.position - targetWasAt).sqrMagnitude > RepathMoved * RepathMoved) GoFor(target);
        }

        private void GoFor(GameObject candy)
        {
            target = candy;
            stuckFor = 0f;
            if (!agent.isOnNavMesh) return;
            if (candy == null)
            {
                agent.ResetPath();
                return;
            }
            targetWasAt = candy.transform.position;
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (NavMesh.SamplePosition(targetWasAt, out var spot, NavMeshReach, filter) && agent.CalculatePath(spot.position, path) &&
                path.status == NavMeshPathStatus.PathComplete)
            {
                agent.SetPath(path);
                return;
            }
            LeaveAlone(candy);
            target = null;
            agent.ResetPath();
        }

        // Walking at nothing (furniture in the way): that candy is left alone a while.
        private void WatchForStuck(bool standing, float speed)
        {
            if (standing || target == null || speed > 0.1f)
            {
                stuckFor = 0f;
                return;
            }
            stuckFor += Time.deltaTime;
            if (stuckFor < StuckSeconds) return;
            LeaveAlone(target);
            GoFor(null);
        }

        private void LeaveAlone(GameObject candy)
        {
            if (leftAlone.Count > 32) leftAlone.Clear();
            leftAlone[candy] = Time.time + LeaveAloneSeconds;
        }

        private bool LeftAlone(GameObject candy) => leftAlone.TryGetValue(candy, out float until) && Time.time < until;

        // ---------- Eating each other ----------

        // Another one close by, not fighting yet: the two of them fight.
        private void LookForRival()
        {
            if (Time.time < nextLook) return;
            foreach (var other in everyone)
            {
                if (other == this || other == null || other.rival != null || other.beingEaten || other.phase == Phase.Gone ||
                    other.phase == Phase.Waking || other.phase == Phase.Giant) continue;
                var gap = other.transform.position - transform.position;
                gap.y = 0f;
                if (gap.magnitude - radius * Size - other.radius * other.Size > duelRange) continue;
                StartFight(other);
                other.StartFight(this);
                return;
            }
        }

        private void StartFight(FatPumpkin other)
        {
            rival = other;
            duel = Duel.Closing;
            duelStarted = Time.time;
            GoFor(null);
            if (model != null) model.localRotation = Quaternion.identity;
            Squash(0f);
        }

        private void StopFight()
        {
            rival = null;
            duel = Duel.None;
            Squash(0f);
            if (phase == Phase.Angry) rage = Rage.Chasing;
        }

        // Up to the rival, wind up (sooner or later, by chance), lunge: the first bite that lands wins.
        private void Fight()
        {
            if (rival == null || rival.beingEaten || rival.phase == Phase.Gone || Time.time - duelStarted > duelGiveUp)
            {
                StopFight();
                return;
            }
            var toRival = rival.transform.position - transform.position;
            toRival.y = 0f;
            float gap = toRival.magnitude - radius * Size - rival.radius * rival.Size;
            float speed = 0f;
            switch (duel)
            {
                case Duel.Closing:
                    if (gap <= biteReach)
                    {
                        Stand();
                        duel = Duel.WindingUp;
                        duelUntil = Time.time + Random.Range(0f, duelHeadStart) + biteWindUp;
                        break;
                    }
                    if (agent.isOnNavMesh && Time.time >= nextChasePath)
                    {
                        nextChasePath = Time.time + LookEvery;
                        agent.isStopped = false;
                        agent.speed = walkSpeed * chaseSpeedShare;
                        agent.SetDestination(rival.transform.position);
                    }
                    var velocity = agent.velocity;
                    velocity.y = 0f;
                    speed = velocity.magnitude;
                    TurnTo(speed > 0.1f ? velocity : toRival);
                    break;
                case Duel.WindingUp:
                    Stand();
                    TurnTo(toRival);
                    float k = biteWindUp > 0f ? 1f - Mathf.Clamp01((duelUntil - Time.time) / biteWindUp) : 1f;
                    Squash(k * k);
                    if (gap > biteReach * 1.5f) duel = Duel.Closing;
                    else if (Time.time >= duelUntil) Lunge(toRival, gap, Duel.Lunging);
                    break;
                case Duel.Lunging:
                    Squash(0f);
                    if (gap <= BiteContact)
                    {
                        Eat(rival);
                        return;
                    }
                    if (knockback != null && knockback.IsKnockedBack && Time.time < rageUntil) break;
                    // Missed: again.
                    duel = Duel.Closing;
                    break;
            }
            Waddle(speed);
            if (hasWalking) animator.SetBool(walkingBool, speed > 0.1f);
            if (hasWalkSpeed) animator.SetFloat(walkSpeedFloat, Mathf.Clamp(speed / walkSpeed, MinWalkAnimation, 1f));
        }

        // It sucks the other one into its mouth, and is faster, tougher and bigger for it.
        private void Eat(FatPumpkin other)
        {
            other.beingEaten = true;
            other.phase = Phase.Gone;
            other.Stand();
            eatenWithThem += other.Eaten + other.eatenWithThem;
            foreach (var swallow in other.swallows) if (swallow.candy != null) eatenWithThem++;
            walkSpeed *= winnerSpeed;
            health *= winnerHealth;
            sizeBoost *= winnerSize;
            candyBoost *= winnerCandies;
            StopFight();
            pausedUntil = Mathf.Max(pausedUntil, Time.time + eatEachOtherSeconds);
            if (hasEat) animator.SetTrigger(eatTrigger);
            if (jiggle != null) jiggle.Hit(HitWobble);
            if (eatSound != null && speaker != null) speaker.PlayOneShot(eatSound, eatVolume);
            StartCoroutine(Swallowing(other));
        }

        private System.Collections.IEnumerator Swallowing(FatPumpkin other)
        {
            foreach (var part in other.GetComponentsInChildren<Collider>()) part.enabled = false;
            if (other.agent != null) other.agent.enabled = false;
            var from = other.transform.position;
            var size = other.transform.localScale;
            for (float t = 0f; t < eatEachOtherSeconds && other != null; t += Time.deltaTime)
            {
                float k = t / eatEachOtherSeconds;
                k *= k;
                other.transform.position = Vector3.Lerp(from, Mouth() - Vector3.up * (other.radius * other.Size * 0.5f), k);
                other.transform.localScale = size * Mathf.Lerp(1f, 0.05f, k);
                yield return null;
            }
            if (other != null) Destroy(other.gameObject);
            if (jiggle != null) jiggle.Hit(HitWobble);
        }

        // ---------- Angry ----------

        // No candy left for it: it goes red and fumes a moment (shaking, growling), then comes for the player.
        private void GetAngry()
        {
            phase = Phase.Angry;
            rage = Rage.Fuming;
            rageUntil = Time.time + fumeSeconds;
            noCandySince = -1f;
            GoFor(null);
            Stand();
            if (jiggle != null) jiggle.Hit(HitWobble);
            if (hasHit) animator.SetTrigger(hitTrigger);
            if (hasWalking) animator.SetBool(walkingBool, false);
            Growl();
            var top = transform.position + Vector3.up * (radius * Size * 2f);
            foreach (var effect in angryEffects)
            {
                if (effect != null) EffectPool.Play(effect, top, angryEffectSize * Size);
            }
            nextLaugh = Time.time + Random.Range(laughEvery.x, laughEvery.y);
        }

        // A candy it can get to: it forgets the player and goes for it.
        private void CalmDown()
        {
            phase = Phase.Hunting;
            noCandySince = -1f;
            if (model == null) return;
            model.localScale = modelScale;
            model.localRotation = Quaternion.identity;
        }

        private void Rampage()
        {
            // Still greedy: a candy it can get to (one in reach is sucked in at once) calms it down.
            if (Time.time >= nextLook && rage != Rage.Lunging)
            {
                nextLook = Time.time + LookEvery;
                LookForCandy();
                if (target != null)
                {
                    CalmDown();
                    return;
                }
            }
            var feet = PlayerFeet();
            var toPlayer = feet - transform.position;
            toPlayer.y = 0f;
            float gap = toPlayer.magnitude - radius * Size;
            bool knocked = knockback != null && knockback.IsKnockedBack && rage != Rage.Lunging;
            float speed = 0f;
            switch (rage)
            {
                case Rage.Fuming:
                    Stand();
                    TurnTo(toPlayer);
                    // Shaking with rage.
                    if (model != null) model.localRotation = Quaternion.Euler(0f, 0f, 4f * Mathf.Sin(Time.time * 40f));
                    if (Time.time < rageUntil) break;
                    if (model != null) model.localRotation = Quaternion.identity;
                    rage = Rage.Chasing;
                    break;
                case Rage.Chasing:
                    if (knocked || Time.time < pausedUntil)
                    {
                        Stand();
                        break;
                    }
                    // In reach: it stands there, glaring, until it can bite again (it doesn't walk into them).
                    if (gap <= biteReach)
                    {
                        Stand();
                        TurnTo(toPlayer);
                        if (Time.time < nextBite) break;
                        rage = Rage.WindingUp;
                        rageUntil = Time.time + biteWindUp;
                        break;
                    }
                    Chase(feet);
                    var velocity = agent.velocity;
                    velocity.y = 0f;
                    speed = velocity.magnitude;
                    TurnTo(speed > 0.1f ? velocity : toPlayer);
                    break;
                case Rage.WindingUp:
                    Stand();
                    TurnTo(toPlayer);
                    float k = biteWindUp > 0f ? 1f - Mathf.Clamp01((rageUntil - Time.time) / biteWindUp) : 1f;
                    Squash(k * k);
                    if (Time.time >= rageUntil) Lunge(toPlayer, gap);
                    break;
                case Rage.Lunging:
                    Squash(0f);
                    if (!bitten && gap <= BiteContact) Bite();
                    if (knockback != null && knockback.IsKnockedBack && Time.time < rageUntil) break;
                    rage = Rage.Chasing;
                    nextBite = Time.time + biteEvery;
                    break;
            }
            if (rage != Rage.Fuming) Waddle(speed);
            if (hasWalking) animator.SetBool(walkingBool, speed > 0.1f);
            if (hasWalkSpeed) animator.SetFloat(walkSpeedFloat, Mathf.Clamp(speed / walkSpeed, MinWalkAnimation, 1f));
            if (Time.time >= nextLaugh)
            {
                nextLaugh = Time.time + Random.Range(laughEvery.x, laughEvery.y);
                Growl();
            }
        }

        // After the player, a little faster than it walks (still slower the fatter it is): a new path now and then.
        private void Chase(Vector3 feet)
        {
            if (!agent.isOnNavMesh) return;
            agent.isStopped = false;
            float fatness = Mathf.InverseLerp(1f, Mathf.Max(1.01f, mostGrowth), Growth);
            agent.speed = walkSpeed * chaseSpeedShare * Mathf.Lerp(1f, fullSpeedShare, fatness);
            if (agent.hasPath && Time.time < nextChasePath) return;
            nextChasePath = Time.time + LookEvery;
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (NavMesh.SamplePosition(feet, out var spot, PlayerFloorReach, filter) && agent.CalculatePath(spot.position, path) &&
                path.status != NavMeshPathStatus.PathInvalid)
                agent.SetPath(path);
        }

        // Hops at the player with a chomp (its Eat animation), to where they stand at the most.
        private void Lunge(Vector3 toPlayer, float gap, Duel atRival = Duel.None)
        {
            if (atRival != Duel.None) duel = atRival;
            else rage = Rage.Lunging;
            rageUntil = Time.time + biteLunge.y + 0.1f;
            bitten = false;
            if (knockback == null && !TryGetComponent(out knockback)) knockback = gameObject.AddComponent<Knockback>();
            var way = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : transform.forward;
            // It lands just short of them, not on top of them.
            knockback.Push(transform.position - way, Mathf.Clamp(gap - LungeStopsShort, 0.1f, biteLunge.x), biteLunge.y, LungeHop, 0f);
            if (hasEat) animator.SetTrigger(eatTrigger);
            if (jiggle != null) jiggle.Hit(GulpWobble);
            var clip = biteSound != null ? biteSound : eatSound;
            if (clip != null && speaker != null) speaker.PlayOneShot(clip, eatVolume);
        }

        // The bite lands: a bug's bite, and a little shove back.
        private void Bite()
        {
            bitten = true;
            SendToPlayer(biteEvent);
            if (biteShove.y > 0f) PlayerLaunch.Launch(transform.position, biteShove.x, biteShove.y, false);
        }

        private void Stand()
        {
            if (agent.isOnNavMesh) agent.isStopped = true;
        }

        // Squashes down to bite (0 = its own shape).
        private void Squash(float k)
        {
            if (model == null) return;
            float wide = Mathf.Lerp(1f, SquashWidth, k);
            model.localScale = Vector3.Scale(modelScale, new Vector3(wide, Mathf.Lerp(1f, SquashHeight, k), wide));
        }

        // Goes red while it's angry, and back as it calms down.
        private void Flush()
        {
            float want = phase == Phase.Angry || phase == Phase.Giant ? 1f : 0f;
            if (flush == want) return;
            flush = Mathf.MoveTowards(flush, want, FlushPerSecond * Time.deltaTime);
            block ??= new MaterialPropertyBlock();
            foreach (var part in looks)
            {
                if (part == null) continue;
                if (flush <= 0f)
                {
                    part.SetPropertyBlock(null);
                    continue;
                }
                var material = part.sharedMaterial;
                var own = material != null && material.HasProperty(BaseColor) ? material.GetColor(BaseColor) : Color.white;
                var glow = material != null && material.HasProperty(EmissionColor) ? material.GetColor(EmissionColor) : Color.black;
                part.GetPropertyBlock(block);
                block.SetColor(BaseColor, Color.Lerp(own, own * angryColour, flush));
                block.SetColor(EmissionColor, glow + angryColour * (0.35f * flush));
                part.SetPropertyBlock(block);
            }
        }

        // Its angry voice: Angry Sound, or its laugh, low and slow.
        private void Growl()
        {
            if (speaker == null) return;
            if (angrySound != null)
            {
                speaker.PlayOneShot(angrySound);
                return;
            }
            if (laughs.Length == 0) return;
            if (growler == null)
            {
                growler = gameObject.AddComponent<AudioSource>();
                growler.playOnAwake = false;
                growler.spatialBlend = speaker.spatialBlend;
                growler.minDistance = speaker.minDistance;
                growler.maxDistance = speaker.maxDistance;
                growler.rolloffMode = speaker.rolloffMode;
                growler.volume = speaker.volume;
                growler.pitch = voicePitch * AngryPitch;
            }
            growler.PlayOneShot(laughs[Random.Range(0, laughs.Length)]);
        }

        private Vector3 PlayerFeet() =>
            head != null ? new Vector3(head.position.x, transform.position.y, head.position.z) : transform.position + transform.forward;

        private static void SendToPlayer(string eventName)
        {
            if (string.IsNullOrEmpty(eventName)) return;
            var player = FsmVariables.GlobalVariables.FindFsmGameObject(PlayerVariable);
            if (player == null || player.Value == null) return;
            foreach (var fsm in player.Value.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName == PlayerDamageFsm) fsm.SendEvent(eventName);
            }
        }

        // ---------- Giant ----------

        // Its body's height, in metres.
        private float Height => 2f * radius * Size;

        // Tall enough: it roars, goes red for good and forgets the candy.
        private void BecomeGiant()
        {
            phase = Phase.Giant;
            // A fixed number, so the fights it won before (Winner Health) don't stack on it.
            if (giantHealth > 0f) health = Mathf.Max(health, giantHealth);
            stomp = StompStep.Coming;
            nextStomp = Time.time + 1.2f;
            noCandySince = -1f;
            GoFor(null);
            Stand();
            if (model != null)
            {
                model.localScale = modelScale;
                model.localRotation = Quaternion.identity;
            }
            if (jiggle != null) jiggle.Hit(HitWobble);
            if (giantSound != null && speaker != null) speaker.PlayOneShot(giantSound);
            else Growl();
            var top = transform.position + Vector3.up * Height;
            foreach (var effect in angryEffects)
            {
                if (effect != null) EffectPool.Play(effect, top, angryEffectSize * Size);
            }
        }

        // After the player; in Stomp Range it squashes down and leaps onto where they stand.
        private void Stomp()
        {
            var feet = PlayerFeet();
            var toPlayer = feet - transform.position;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;
            float speed = 0f;
            switch (stomp)
            {
                case StompStep.Coming:
                    if (knockback != null && knockback.IsKnockedBack)
                    {
                        Stand();
                        break;
                    }
                    if (distance <= stompRange && Time.time >= nextStomp)
                    {
                        Stand();
                        stomp = StompStep.WindingUp;
                        rageUntil = Time.time + stompWindUp;
                        break;
                    }
                    if (distance > radius * Size + stompReach)
                    {
                        Chase(feet);
                        if (agent.isOnNavMesh) agent.speed = walkSpeed * giantSpeedShare;
                        var velocity = agent.velocity;
                        velocity.y = 0f;
                        speed = velocity.magnitude;
                        TurnTo(speed > 0.1f ? velocity : toPlayer);
                    }
                    else
                    {
                        Stand();
                        TurnTo(toPlayer);
                    }
                    break;
                case StompStep.WindingUp:
                    Stand();
                    TurnTo(toPlayer);
                    float k = 1f - Mathf.Clamp01((rageUntil - Time.time) / stompWindUp);
                    Squash(k * k);
                    if (Time.time >= rageUntil) Leap(toPlayer, distance);
                    break;
                case StompStep.Flying:
                    Squash(0f);
                    if ((knockback == null || !knockback.IsKnockedBack) || Time.time >= rageUntil) Land();
                    break;
            }
            if (stomp == StompStep.Coming) Waddle(speed);
            if (hasWalking) animator.SetBool(walkingBool, speed > 0.1f);
            if (hasWalkSpeed) animator.SetFloat(walkSpeedFloat, Mathf.Clamp(speed / walkSpeed, MinWalkAnimation, 1f));
        }

        // Up and onto where the player stands.
        private void Leap(Vector3 toPlayer, float distance)
        {
            stomp = StompStep.Flying;
            rageUntil = Time.time + stompSeconds + 0.3f;
            if (knockback == null && !TryGetComponent(out knockback)) knockback = gameObject.AddComponent<Knockback>();
            var way = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : transform.forward;
            knockback.Push(transform.position - way, Mathf.Clamp(distance, 0.2f, stompRange), stompSeconds, stompHop, 0f);
            if (hasEat) animator.SetTrigger(eatTrigger);
            if (jiggle != null) jiggle.Hit(GulpWobble);
            Growl();
        }

        // Down with a thud and dust; on the player, it hurts and throws them towards the nearest edge.
        private void Land()
        {
            stomp = StompStep.Coming;
            nextStomp = Time.time + Random.Range(stompEvery.x, Mathf.Max(stompEvery.x, stompEvery.y));
            Squash(0f);
            var effects = stompEffects.Length > 0 ? stompEffects : breakOutEffects;
            foreach (var effect in effects)
            {
                if (effect != null) EffectPool.Play(effect, transform.position + Vector3.up * 0.1f, stompEffectSize * Size);
            }
            if (stompSound != null && speaker != null) speaker.PlayOneShot(stompSound);
            if (jiggle != null) jiggle.Hit(HitWobble);
            var feet = PlayerFeet();
            var gap = feet - transform.position;
            gap.y = 0f;
            if (gap.magnitude <= radius * Size + stompReach)
            {
                SendToPlayer(stompEvent);
                ThrowToEdge(feet);
            }
            else if (gap.magnitude <= stompRange) ShakeView("ShakeSmall");
        }

        // Towards the nearest Fall Edge (away from it with none near).
        private void ThrowToEdge(Vector3 feet)
        {
            Vector3 way;
            if (Game.Locomotion.FallEdge.Nearest(feet, edgeSearch, out var edge)) way = edge - feet;
            else way = feet - transform.position;
            way.y = 0f;
            way = way.sqrMagnitude > 0.0001f ? way.normalized : transform.forward;
            PlayerLaunch.Launch(feet - way, stompPush.x, stompPush.y, true, true, 0.6f);
        }

        // A camera shake event sent to the main camera's Info FSM (ShakeSmall, ShakeMed, ShakeBigger).
        private static void ShakeView(string eventName)
        {
            var cam = FsmVariables.GlobalVariables.FindFsmGameObject("MainCam");
            if (cam == null || cam.Value == null) return;
            foreach (var fsm in cam.Value.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName == "Info") fsm.SendEvent(eventName);
            }
        }

        // ---------- Eating ----------

        private void Take(GameObject candy)
        {
            if (!CandyHelmet.Seize(candy)) return;
            swallows.Add(new Swallow { candy = candy, from = candy.transform.position, size = candy.transform.localScale, start = Time.time });
            pausedUntil = Mathf.Max(pausedUntil, Time.time + eatSeconds);
            if (hasEat) animator.SetTrigger(eatTrigger);
        }

        // Into its mouth, shrinking; then it's gone, and it's fatter.
        private void FlySwallows()
        {
            for (int i = swallows.Count - 1; i >= 0; i--)
            {
                var swallow = swallows[i];
                if (swallow.candy == null)
                {
                    swallows.RemoveAt(i);
                    continue;
                }
                float t = Mathf.Clamp01((Time.time - swallow.start) / eatSeconds);
                float k = t * t;
                swallow.candy.transform.position = Vector3.Lerp(swallow.from, Mouth(), k);
                swallow.candy.transform.localScale = swallow.size * Mathf.Lerp(1f, 0.3f, k);
                if (t < 1f) continue;
                Destroy(swallow.candy);
                swallows.RemoveAt(i);
                Gulp();
            }
        }

        private void Gulp()
        {
            Eaten++;
            health += candyHealTimes * Random.Range(Mathf.Min(candyHealRoll.x, candyHealRoll.y), Mathf.Max(candyHealRoll.x, candyHealRoll.y) + 1);
            if (jiggle != null) jiggle.Hit(GulpWobble);
            if (eatSound != null && speaker != null && Time.time >= nextGulpSound)
            {
                nextGulpSound = Time.time + GulpGap;
                speaker.PlayOneShot(eatSound, eatVolume);
            }
        }

        private float Growth => Mathf.Min(Mathf.Max(1f, mostGrowth), 1f + growPerCandy * Eaten);

        // Pops up to its size as it wakes up, then swells a little with each candy.
        private void Grow()
        {
            shownGrowth = Mathf.MoveTowards(shownGrowth, Growth, GrowPerSecond * Time.deltaTime);
            shownSizeBoost = Mathf.MoveTowards(shownSizeBoost, sizeBoost, GrowPerSecond * 2f * Time.deltaTime);
            float popped = Mathf.Clamp01((Time.time - bornAt) / PopSeconds);
            transform.localScale = fullScale * (shownGrowth * shownSizeBoost * Mathf.Lerp(PopFrom, 1f, 1f - (1f - popped) * (1f - popped)));
        }

        // ---------- Being hit ----------

        // The player's hits, by the one rule: a fist, a thing they hold or just threw, a shot.
        private void OnCollisionEnter(Collision collision)
        {
            if (phase == Phase.Gone || beingEaten || Time.time < nextHurt) return;
            var hit = EnemyHitDamage.Judge(collision, toughness, hitSettings, out bool byPlayer);
            if (!byPlayer) return;
            if (hit.Damage <= 0f)
            {
                // Too soft to hurt: it only wobbles.
                if (jiggle != null && Time.time >= nextSoftWobble)
                {
                    nextSoftWobble = Time.time + hitCooldown;
                    jiggle.Hit(SoftWobble);
                }
                return;
            }
            var at = collision.contactCount > 0 ? collision.GetContact(0).point : collision.transform.position;
            var settings = hitSettings != null ? hitSettings : HitSettings.Shared;
            if (settings.ShowsEffect(hit, true)) settings.PlayEffect(at, hit);
            HitBuzz.Dealt(collision, hit.Damage);
            bool breaks = health - hit.Damage <= 0f;
            if (breaks) HitBuzz.Killed(collision, hit.Damage);
            TakeDamage(hit.Damage);
        }

        /// <summary>The player's blast (a grenade, a bottle), or a rolling pumpkin running it over.</summary>
        public void Hit(float strength, Vector3 point)
        {
            if (phase == Phase.Gone) return;
            TakeDamage(Mathf.Max(1f, HitSettings.Whole(strength / toughness)));
        }

        private void TakeDamage(float damage)
        {
            health -= damage;
            nextHurt = Time.time + hitCooldown;
            if (health <= 0f)
            {
                Break();
                return;
            }
            if (jiggle != null) jiggle.Hit(HitWobble);
            // A giant shrugs hits off (user, 2026-11-10): they hurt (the wobble and its yelp), but don't knock it back,
            // stop it or cancel its leap, or fast punches could keep it from ever stomping.
            if (phase == Phase.Giant)
            {
                if (hitSounds.Length > 0 && speaker != null) speaker.PlayOneShot(hitSounds[Random.Range(0, hitSounds.Length)]);
                return;
            }
            pausedUntil = Mathf.Max(pausedUntil, Time.time + KnockSeconds + knock.y);
            // A hit stops a bite coming (it has to wind up again once it's over the hit).
            if (phase == Phase.Angry && rage != Rage.Fuming)
            {
                rage = Rage.Chasing;
                Squash(0f);
                nextBite = Mathf.Max(nextBite, pausedUntil);
            }
            if (hitSounds.Length > 0 && speaker != null) speaker.PlayOneShot(hitSounds[Random.Range(0, hitSounds.Length)]);
            if (hasHit) animator.SetTrigger(hitTrigger);
            if (head == null) return;
            if (knockback == null && !TryGetComponent(out knockback)) knockback = gameObject.AddComponent<Knockback>();
            knockback.Push(head.position, knock.x, KnockSeconds, KnockHop, knock.y);
        }

        // It bursts and gives back a few of the candies it ate. What's still on its way to its mouth counts as eaten.
        private void Break()
        {
            phase = Phase.Gone;
            foreach (var swallow in swallows)
            {
                if (swallow.candy == null) continue;
                Destroy(swallow.candy);
                Eaten++;
            }
            swallows.Clear();

            var at = Middle();
            float size = Size;
            foreach (var effect in burstEffects)
            {
                if (effect != null) EffectPool.Play(effect, at, burstEffectSize * size);
            }
            if (burstSound != null) AudioSource.PlayClipAtPoint(burstSound, at);
            var stuff = SpawnedStuff();
            var parts = BurstInto(stuff);
            DropCandies(stuff, parts);
            if (exp > 0 && !noReward) Game.Leveling.PlayerLevel.AddExp(exp);
            Destroy(gameObject);
        }

        private Collider[] BurstInto(Transform stuff)
        {
            if (pieces == null) return System.Array.Empty<Collider>();
            var burst = Instantiate(pieces, transform.position, transform.rotation, stuff);
            burst.transform.localScale = Vector3.one * (piecesSize * Size);
            return burst.GetComponentsInChildren<Collider>(true);
        }

        // Out of a ring round it, low over the floor: candies break on a hard bump, so they go through its pieces.
        private void DropCandies(Transform stuff, Collider[] parts)
        {
            if (candies.Length == 0) return;
            int amount = Mathf.Clamp(Mathf.FloorToInt((Eaten + eatenWithThem) * dropShare), fewestCandies, Mathf.Max(fewestCandies, mostCandies));
            // A winner of a fight gives Winner Candies times as many.
            amount = Mathf.RoundToInt(amount * candyBoost);
            float ring = radius * Size + CandyRingGap;
            float turn = Random.Range(0f, 360f);
            for (int i = 0; i < amount; i++)
            {
                var prefab = candies[Random.Range(0, candies.Length)];
                if (prefab == null) continue;
                var outward = Quaternion.Euler(0f, turn + 360f * i / amount, 0f) * Vector3.forward;
                var candy = Instantiate(prefab, transform.position + outward * ring + Vector3.up * CandyHeight,
                    Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), stuff);
                foreach (var part in candy.GetComponentsInChildren<Collider>(true))
                {
                    foreach (var piece in parts)
                    {
                        if (piece != null) Physics.IgnoreCollision(part, piece);
                    }
                }
                if (candy.TryGetComponent<Rigidbody>(out var body))
                    body.linearVelocity = outward * CandyOutward + Vector3.up * candyLaunchSpeed;
            }
        }

        // ---------- Helpers ----------

        private void Laugh()
        {
            if (laughs.Length > 0 && speaker != null) speaker.PlayOneShot(laughs[Random.Range(0, laughs.Length)]);
            // Waking up, the laugh is part of its animation.
            if (hasLaugh && phase != Phase.Waking) animator.SetTrigger(laughTrigger);
        }

        // Rocks from side to side round its foot as it walks, a little hop at each step. An animation does that instead.
        private void Waddle(float speed)
        {
            if (model == null || animator != null) return;
            float amount = Mathf.Clamp01(speed / walkSpeed);
            waddleClock += waddlePerSecond * amount * Time.deltaTime;
            float swing = Mathf.Sin(waddleClock * Mathf.PI);
            model.localRotation = Quaternion.Euler(0f, 0f, swing * waddle.x * amount);
            model.localPosition = modelHome + Vector3.up * (Mathf.Abs(swing) * waddle.y * amount / Size);
        }

        private void TurnTo(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), TurnDegrees * Time.deltaTime);
        }

        private Vector3 HeadFlat() => head != null ? head.position - transform.position : transform.forward;

        private float Size => Mathf.Max(0.01f, transform.lossyScale.y);

        private Vector3 Middle() => transform.position + Vector3.up * (radius * Size);

        private Vector3 Mouth() => mouth != null ? mouth.position : Middle() + transform.forward * (radius * Size * 0.8f);

        private static Transform SpawnedStuff()
        {
            var stuff = FsmVariables.GlobalVariables.FindFsmGameObject(SpawnedStuffVariable);
            return stuff != null && stuff.Value != null ? stuff.Value.transform : null;
        }

        private void OnDestroy()
        {
            // Taken away (a stage's clean-up) with candies on their way to its mouth.
            foreach (var swallow in swallows)
            {
                if (swallow.candy != null) Destroy(swallow.candy);
            }
        }
    }
}

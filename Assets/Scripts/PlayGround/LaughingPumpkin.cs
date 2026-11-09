using System;
using System.Collections.Generic;
using Game.Combat;
using Game.Enemies;
using Game.Weapons;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using FsmFloat = HutongGames.PlayMaker.FsmFloat;
using FsmVariables = HutongGames.PlayMaker.FsmVariables;
using Random = UnityEngine.Random;

namespace Game.PlayGround
{
    /// <summary>
    /// The laughing pumpkin the cauldron spits out: a catch-me game (user, 2026-11-07).
    /// - Fresh out of the pot it just sits there laughing. Get close (Catch Range) and it loses 1 health and hops away.
    /// - From then on it runs once the player comes within Flee Range, but only a few metres (Run Metres, or Run Seconds
    ///   of running): to a hiding place close by (Hide Radius: inside a bush, in tall grass, behind a tree or the giant
    ///   skull, or just out of sight), always inside the PlayGround and never at the cauldron, dropping candy (its Candy
    ///   Trail) as it goes.
    /// - Out of breath it's stunned for Stun Seconds: stars over it and not a sound. Get close then and it breaks. After
    ///   that its breath is back at once. Keeping still (Rest Before Regen) also brings its breath back slowly.
    /// - The rest of the time it laughs non-stop, one laugh right after the other.
    /// - Out of the PlayGround for Leash Seconds, or fallen through its floor: it puffs back to the middle.
    /// - Not caught in Transform After seconds (game time), it turns into the real Pumpkin Boss (the runner pumpkins'
    ///   Wait and Transform): over the last Warn Last seconds it wobbles more and more and its laugh gets deeper, over
    ///   the last Rumble Last seconds it trembles and growls. Never while held; in the air it waits (Overdue Seconds at
    ///   most). Turned Into Boss fires, never Caught.
    /// - Cornering it within Catch Range for a while breaks it. Holding it (close range only, see Near Grab Only) for a
    ///   while makes it go off in the hand: an explosion that costs the player a lot of health.
    /// - Every break is a small explosion, just big enough to reach the hand holding it: only a player holding it is hurt.
    /// - Hitting it or throwing things at it never breaks it: it hops away, and the hit's damage (worked out as for an
    ///   enemy, see Hit Settings) comes off its breath instead (Metres Per Damage), so beating on it stuns it. Blasts too.
    /// With Can Run off it's the big tanky one: it stays put, laughs, takes damage from hits (Enemy Hit Damage lowers its
    /// Health FSM) and throws out a candy for every Candy Every Health it loses.
    /// Breaking is left to its Health FSM (health 0 -> crack, drop, destroy); Caught / On Caught fire when it breaks.
    /// Tools > 429 Game > PlayGround > Set Up Laughing Pumpkins sets it up.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class LaughingPumpkin : MonoBehaviour, IHittable
    {
        public enum Mood { Fresh, Watching, Fleeing, Stunned, Held, Broken }

        private const string SpawnedStuffVariable = "CurrentlySpawnedStuffs";
        private const float LeashCheckEvery = 0.25f;
        private static readonly Vector3[] Sides = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };

        [Header("Running (user, 2026-11-07: a few metres, then stunned)")]
        [Tooltip("Off = the big tanky pumpkin: it never runs, it takes damage from hits instead.")]
        [SerializeField] private bool canRun = true;
        [Tooltip("How far it can run, in metres along the floor, before it's out of breath and stunned.")]
        [Min(0.5f)]
        [SerializeField] private float runMetres = 4f;
        [Tooltip("...or this many seconds of running, whichever comes first (so a run that gets nowhere still ends).")]
        [Min(0.5f)]
        [SerializeField] private float runSeconds = 3f;
        [Tooltip("Breath back, in metres per second, while it keeps still.")]
        [Min(0f)]
        [SerializeField] private float regenPerSecond = 0.5f;
        [Tooltip("It has to stay still this long, in seconds, before it gets any breath back.")]
        [Min(0f)]
        [SerializeField] private float restBeforeRegen = 3f;
        [Tooltip("Sideways speed of each hop, in metres per second.")]
        [Min(0.1f)]
        [SerializeField] private float hopSpeed = 3f;
        [Tooltip("Upward speed of each hop, in metres per second (2.4 = about half a second in the air).")]
        [Min(0.1f)]
        [SerializeField] private float hopUp = 2.4f;

        [Header("Stunned (out of breath)")]
        [Tooltip("Out of breath: stunned this long, in seconds (stars over it, not a sound; get close and it breaks). " +
                 "Then its breath is back at once.")]
        [Min(0f)]
        [SerializeField] private float stunSeconds = 4f;
        [Tooltip("The stun stars (Prefabs/Enemies/Hit_FX/Stun_Stars), over it while it's stunned. Empty: none.")]
        [SerializeField] private GameObject stunStars;
        [Tooltip("How wide the stars' ring is, from its own width.")]
        [Range(0.1f, 1.5f)]
        [SerializeField] private float stunStarsSize = 1f;

        [Header("Player")]
        [Tooltip("This close (metres, along the floor) counts as catching it.")]
        [Min(0.1f)]
        [SerializeField] private float catchRange = 1.3f;
        [Tooltip("When the player comes this close it starts running (more than Catch Range).")]
        [Min(0.1f)]
        [SerializeField] private float fleeRange = 3f;
        [Tooltip("Hidden at its spot with the player at least this far, it stops running to save its breath.")]
        [Min(0.1f)]
        [SerializeField] private float calmRange = 5f;
        [Tooltip("Cornered within Catch Range this long (seconds) while it can still run: it breaks anyway.")]
        [Min(0f)]
        [SerializeField] private float cornerSeconds = 1.5f;
        [Tooltip("Held in a hand this long (seconds): it breaks.")]
        [Min(0f)]
        [SerializeField] private float holdSeconds = 1.5f;

        [Header("Hiding (close by, inside the PlayGround)")]
        [Tooltip("Only hiding places this close, in metres.")]
        [Min(1f)]
        [SerializeField] private float hideRadius = 5f;
        [Tooltip("Spots at least this far in from the PlayGround's edge (its Ghost Roam Area), in metres.")]
        [Min(0f)]
        [SerializeField] private float arenaMargin = 0.6f;
        [Tooltip("Never a spot this close to the cauldron, in metres.")]
        [Min(0f)]
        [SerializeField] private float keepFromCauldron = 1.8f;
        [Tooltip("Outside the PlayGround this long (seconds), or below its floor at all: back to the middle with a puff.")]
        [Min(0.5f)]
        [SerializeField] private float leashSeconds = 2f;
        [Tooltip("The puff where it goes and where it comes back (Prefabs/Effects/Smoke/Smoke_Flash_Small_Puff). Empty: none.")]
        [SerializeField] private GameObject leashPuff;
        [Tooltip("Size of the puff, times the effect's own size.")]
        [Min(0.01f)]
        [SerializeField] private float leashPuffScale = 0.5f;

        [Header("Hits")]
        [Tooltip("Something hitting it at least this fast (metres per second) makes it hop away.")]
        [Min(0f)]
        [SerializeField] private float hitSpeed = 1.5f;
        [Tooltip("How hard a hit knocks it away: sideways and upward speed, in metres per second.")]
        [SerializeField] private Vector2 knock = new(3.5f, 3f);
        [Tooltip("How hits turn into damage (the enemies' shared Hit Settings asset); the damage comes off its breath. " +
                 "Empty: the default numbers.")]
        [SerializeField] private HitSettings hitSettings;
        [Tooltip("Divides the hit power, like an enemy's toughness.")]
        [Min(0.1f)]
        [SerializeField] private float toughness = 1f;
        [Tooltip("Breath (metres) lost per point of damage a hit or a blast does: a punch is 1, a hard one 2.")]
        [Min(0f)]
        [SerializeField] private float metresPerDamage = 1.5f;

        [Header("Exploding")]
        [Tooltip("It explodes whenever it breaks (caught, cornered, stunned, its health gone, or held too long). Only a " +
                 "player holding it is hurt (Hand Blast Event to their Damage FSM); things close by are hurt and pushed.")]
        [SerializeField] private GameObject[] handBlastEffects = Array.Empty<GameObject>();
        [SerializeField] private AudioClip handBlastSound;
        [Tooltip("Size of the effects (the bottle grenade's are 1).")]
        [Min(0.05f)] [SerializeField] private float handBlastEffectScale = 0.5f;
        [Tooltip("Metres: just big enough to reach the hand holding it.")]
        [Min(0.1f)] [SerializeField] private float handBlastRadius = 0.6f;
        [Tooltip("Health it takes from things in reach (bugs, breakables), like a grenade's damage.")]
        [Min(0f)] [SerializeField] private float handBlastDamage = 1f;
        [Min(0f)] [SerializeField] private float handBlastPush = 3f;
        [Tooltip("Sent to the player's Damage FSM when it explodes in their hand: DamageHeavy (40-50 health) or Damage (a bite).")]
        [SerializeField] private string handBlastEvent = "DamageHeavy";

        [Header("Candy (the big one)")]
        [Tooltip("Throws out one candy for every this much health it loses. 0 = none.")]
        [Min(0f)]
        [SerializeField] private float candyEveryHealth = 5f;
        [Tooltip("Candies to throw out; one picked at random each time.")]
        [SerializeField] private GameObject[] candies = Array.Empty<GameObject>();
        [Tooltip("How fast a thrown-out candy flies up, in metres per second.")]
        [Min(0f)]
        [SerializeField] private float candyLaunchSpeed = 3.5f;

        [Header("Reward")]
        [Tooltip("EXP the player gets when it breaks: caught, cornered, stunned, its health gone, or going off in their hand.")]
        [Min(0)]
        [SerializeField] private int exp = 5;

        [Header("Sounds")]
        [Tooltip("Its laughs, one right after another (never while stunned).")]
        [SerializeField] private AudioClip[] laughs = Array.Empty<AudioClip>();
        [Tooltip("Played when it's caught the first time, hit, or grabbed (never while stunned).")]
        [SerializeField] private AudioClip[] hitSounds = Array.Empty<AudioClip>();
        [Tooltip("Seconds of quiet between one laugh ending and the next starting (min, max).")]
        [SerializeField] private Vector2 laughGap = new(0.1f, 0.4f);
        [Tooltip("Pitch of its laugh while it runs or is held.")]
        [Range(0.3f, 2f)]
        [SerializeField] private float runPitch = 1.15f;

        [Header("Turning into the Pumpkin Boss (the runner pumpkins' Wait and Transform)")]
        [Tooltip("The real Pumpkin Boss (Prefabs/Enemies/pumpkin_boss_Z). Empty: it never turns.")]
        [SerializeField] private GameObject bossPrefab;
        [Tooltip("Seconds (game time, from when it appears) until it turns into the boss if it isn't broken first. 0 = never.")]
        [Min(0f)]
        [SerializeField] private float transformAfter = 300f;
        [Tooltip("The warning starts this many seconds before it turns.")]
        [Min(0f)]
        [SerializeField] private float warnLast = 60f;
        [Tooltip("Seconds between wobbles as the warning starts, and at the end.")]
        [SerializeField] private Vector2 wobbleEvery = new(8f, 1.2f);
        [Tooltip("How hard it wobbles as the warning starts, and at the end (1 = a full hit's jiggle).")]
        [SerializeField] private Vector2 wobble = new(0.3f, 0.8f);
        [Tooltip("Its laugh gets deeper over the warning, down to this pitch.")]
        [Range(0.3f, 1f)]
        [SerializeField] private float lowestPitch = 0.7f;
        [Tooltip("Over the last this many seconds it trembles with a growl.")]
        [Min(0f)]
        [SerializeField] private float rumbleLast = 10f;
        [Tooltip("The growl (Audio/SFX/Low,_rumbling_hmmmm). Empty: none.")]
        [SerializeField] private AudioClip rumbleSound;
        [Range(0f, 1f)]
        [SerializeField] private float rumbleVolume = 1f;
        [Tooltip("Seconds between trembles (a full wobble and the growl).")]
        [Min(0.1f)]
        [SerializeField] private float trembleEvery = 0.6f;
        [Tooltip("Held, it never turns; in the air it waits at most this many seconds more.")]
        [Min(0f)]
        [SerializeField] private float overdueSeconds = 5f;

        [Header("PlayMaker")]
        [Tooltip("The FSM that breaks it when its float \"health\" reaches 0.")]
        [SerializeField] private string healthFsm = "Health";
        [SerializeField] private string healthVariable = "health";
        [Tooltip("The old FSM that took health when the player touched it; switched off (this script decides now).")]
        [SerializeField] private string oldDamageFsm = "Damage";

        [Header("Events")]
        public UnityEvent onCaught = new();

        /// <summary>Fired when any laughing pumpkin breaks (caught, held, or its health ran out).</summary>
        public static event Action<LaughingPumpkin> Caught;

        /// <summary>Fired once when a pumpkin that was never caught turns into the boss (the boss made, the pumpkin about to go). Never together with Caught.</summary>
        public static event Action<LaughingPumpkin, GameObject> TurnedIntoBoss;

        private static readonly List<LaughingPumpkin> all = new();
        public static IReadOnlyList<LaughingPumpkin> All => all;

        private Rigidbody body;
        private XRGrabInteractable grab;
        private AudioSource voice;
        private AudioSource laughVoice;
        private AudioSource rumbleVoice;
        private Game.VFX.Jiggle jiggle;
        private Collider[] ownColliders;
        private FsmFloat health;
        private GhostRoamArea arena;
        private Cauldron pot;
        private NavMeshPath path;

        private Mood mood = Mood.Fresh;
        private float staminaLeft;
        // It has come to rest on the ground once since it was made (spat out of the pot).
        private bool landedOnce;
        private float stillFor;
        private float cornered;
        private float held;
        private float nextLaugh;
        private int lastLaugh = -1;
        private float nextHop;
        private float nextPick;
        private float nextPath;
        private float lastHealth;
        private float lostForCandy;
        private float nextHurt;
        private const float HurtCooldown = 0.3f;
        private float stunnedAt;
        private bool starsOn;
        private Vector3 lastPos;
        private float outsideFor;
        private float nextLeash;
        private float aliveFor;
        private float nextWobble;
        private float nextTremble;
        private int bossAgentType;
        private bool turned;
        private bool warnedNoSpot;
        private readonly List<(Vector3 point, Collider[] passThrough, float bonus)> hideSpots = new();
        private bool hasTarget;
        private Vector3 target;
        private Vector3 faceDirection;
        private readonly List<Vector3> corners = new();

        public Mood Current => mood;
        /// <summary>Breath left, in metres of running.</summary>
        public float StaminaLeft => staminaLeft;
        /// <summary>Full breath, in metres of running (Run Metres).</summary>
        public float MaxStamina => runMetres;
        public bool CanRun => canRun;
        public bool IsBroken => mood == Mood.Broken;
        public bool IsStunned => mood == Mood.Stunned;
        /// <summary>It has turned into the Pumpkin Boss (it's about to be gone).</summary>
        public bool HasTurned => turned;
        /// <summary>Seconds (game time) until it turns into the boss; infinity if it never will.</summary>
        public float SecondsUntilBoss => transformAfter > 0f && bossPrefab != null ? Mathf.Max(0f, transformAfter - aliveFor) : float.PositiveInfinity;

        // Stunned, or held while stunned: not a laugh, not a hit sound (user: "when stunned, it can't laugh").
        private bool Silent => mood == Mood.Stunned || (mood == Mood.Held && staminaLeft <= 0f);

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            grab = GetComponent<XRGrabInteractable>();
            voice = GetComponent<AudioSource>();
            ownColliders = GetComponentsInChildren<Collider>(true);
            jiggle = GetComponentInChildren<Game.VFX.Jiggle>(true);
            path = new NavMeshPath();
            staminaLeft = runMetres;
            // Its laugh and its growl have their own speakers: a stun stops the laugh at once without cutting a hit
            // sound, and the Health FSM's break laugh still plays on the first one.
            if (voice != null)
            {
                laughVoice = ExtraVoice();
                rumbleVoice = ExtraVoice();
            }

            foreach (var fsm in GetComponents<PlayMakerFSM>())
            {
                if (!string.IsNullOrEmpty(oldDamageFsm) && fsm.FsmName == oldDamageFsm) fsm.enabled = false;
                if (fsm.FsmName == healthFsm) health = fsm.FsmVariables.FindFsmFloat(healthVariable);
            }

            if (grab != null)
            {
                grab.selectEntered.AddListener(OnGrabbed);
                grab.selectExited.AddListener(OnReleased);
            }
        }

        private void OnEnable() => all.Add(this);
        private void OnDisable() => all.Remove(this);

        private void Start()
        {
            // The small ones: 2 health = caught once (hops away), then caught again (breaks).
            if (canRun && health != null) health.Value = 2f;
            lastHealth = health != null ? health.Value : 0f;
            pot = FindAnyObjectByType<Cauldron>();
            arena = GhostRoamArea.Around(body.position);
            if (arena == null && pot != null) arena = GhostRoamArea.Around(pot.transform.position);
            if (!canRun) mood = Mood.Watching;
            nextLaugh = Time.time + Random.Range(0.2f, 0.8f);
            faceDirection = transform.forward;
            lastPos = body.position;
            bossAgentType = bossPrefab != null && bossPrefab.TryGetComponent<NavMeshAgent>(out var agent) ? agent.agentTypeID : 0;
            // The first wobble comes as the warning starts.
            nextWobble = Mathf.Max(0f, transformAfter - warnLast);
            // So the first blast doesn't stutter.
            Blast.Warm(handBlastEffects);
        }

        private void Update()
        {
            if (mood == Mood.Broken || turned) return;
            WatchHealth();
            if (mood == Mood.Broken) return;
            if (TickTransform()) return;
            Leash();

            var head = Camera.main != null ? Camera.main.transform : null;
            float distance = head != null ? Flat(head.position - body.position).magnitude : float.MaxValue;
            if (head != null && mood != Mood.Fleeing) faceDirection = Flat(head.position - body.position);

            bool moving = Flat(body.linearVelocity).sqrMagnitude > 0.09f || !Grounded();
            if (!landedOnce && !moving) landedOnce = true;
            stillFor = moving || mood == Mood.Fleeing ? 0f : stillFor + Time.deltaTime;
            if (canRun && mood == Mood.Watching && stillFor >= restBeforeRegen)
                staminaLeft = Mathf.Min(runMetres, staminaLeft + regenPerSecond * Time.deltaTime);

            switch (mood)
            {
                case Mood.Fresh:
                    if (distance <= catchRange) FirstCatch(head);
                    break;

                case Mood.Watching:
                    if (!canRun) break;
                    if (CorneredTooLong(distance)) { Break(); return; }
                    // Still flying out of the pot: it runs off only once it has landed (the spit costs no breath).
                    if (distance <= fleeRange && landedOnce)
                    {
                        if (staminaLeft >= runMetres * 0.25f) StartFleeing(head);
                        else Stun();
                    }
                    break;

                case Mood.Fleeing:
                {
                    // Breath is the ground it really covers, and at least Run Metres over Run Seconds.
                    float moved = Flat(body.position - lastPos).magnitude;
                    lastPos = body.position;
                    staminaLeft -= Mathf.Max(moved, Time.deltaTime * runMetres / Mathf.Max(0.1f, runSeconds));
                    if (staminaLeft <= 0f) { Stun(); break; }
                    if (CorneredTooLong(distance)) { Break(); return; }
                    bool arrived = hasTarget && Flat(target - body.position).magnitude < 0.9f;
                    bool hidden = head != null && Hidden(head.position, body.position);
                    // Very far, far and out of sight, or at its hiding spot out of sight with the player not right on it:
                    // stop and hide there, saving its breath. At its spot with the player a little way off, it stays too.
                    bool safe = distance > calmRange + 3f || (distance > calmRange && hidden) || (arrived && hidden && distance > fleeRange);
                    bool settled = arrived && distance > fleeRange + 1f;
                    if (safe || settled) { mood = Mood.Watching; hasTarget = false; break; }
                    if (!hasTarget || arrived || Time.time >= nextPick) PickHidingSpot(head);
                    break;
                }

                case Mood.Stunned:
                    if (distance <= catchRange) { Break(); return; }
                    if (Time.time - stunnedAt >= stunSeconds)
                    {
                        staminaLeft = runMetres;
                        stillFor = 0f;
                        mood = Mood.Watching;
                    }
                    break;

                case Mood.Held:
                    held += Time.deltaTime;
                    if (held >= holdSeconds) { ExplodeInHand(); return; }
                    break;
            }

            ShowStars(mood == Mood.Stunned);
            KeepLaughing();
        }

        private void FixedUpdate()
        {
            if (mood == Mood.Broken || mood == Mood.Held || turned || body.isKinematic) return;

            if (mood == Mood.Fleeing && Time.time >= nextHop && Grounded())
            {
                var step = NextStep();
                faceDirection = step;
                body.linearVelocity = step * hopSpeed + Vector3.up * hopUp;
                nextHop = Time.time + 0.2f;
            }

            // Stay upright, turned to face the player (or where it's running); stunned, it just sits there dazed.
            if (mood != Mood.Stunned && faceDirection.sqrMagnitude > 0.001f)
            {
                var want = Quaternion.LookRotation(faceDirection.normalized, Vector3.up);
                body.MoveRotation(Quaternion.RotateTowards(body.rotation, want, 540f * Time.fixedDeltaTime));
            }
        }

        // ------------------------------------------------------------------ moods

        private void FirstCatch(Transform head)
        {
            if (health != null) health.Value = Mathf.Max(1f, health.Value - 1f);
            lastHealth = health != null ? health.Value : 0f;
            Knock(head != null ? Flat(body.position - head.position) : -transform.forward);
            Play(hitSounds, 1f, 1f);
            mood = Mood.Watching;
        }

        private void StartFleeing(Transform head)
        {
            mood = Mood.Fleeing;
            cornered = 0f;
            lastPos = body.position;
            PickHidingSpot(head);
        }

        // Out of breath: stunned from now, its laugh cut at once.
        private void Stun()
        {
            staminaLeft = 0f;
            if (mood != Mood.Stunned) stunnedAt = Time.time;
            mood = Mood.Stunned;
            hasTarget = false;
            cornered = 0f;
            if (laughVoice != null) laughVoice.Stop();
        }

        private bool CorneredTooLong(float distance)
        {
            cornered = distance <= catchRange ? cornered + Time.deltaTime : 0f;
            return cornered >= cornerSeconds;
        }

        /// <summary>Breaks it: it explodes, and its Health FSM plays the crack and the drop.</summary>
        public void Break()
        {
            if (mood == Mood.Broken || turned) return;
            bool inHand = LetGo();
            // Caught, not hit: no damage number.
            DamageNumbers.Quiet(gameObject);
            if (health != null) health.Value = 0f;
            Broke(inHand);
        }

        /// <summary>
        /// Held too long: it goes off in the player's hand. The explosion hurts them a lot and anything close, and the
        /// pumpkin is gone (that still counts as caught).
        /// </summary>
        public void ExplodeInHand()
        {
            if (mood == Mood.Broken || turned) return;
            LetGo();
            Broke(true);

            // Gone in the blast: no crack, no leftovers.
            foreach (var shown in GetComponentsInChildren<Renderer>()) shown.enabled = false;
            foreach (var part in ownColliders) if (part != null) part.enabled = false;
            foreach (var fsm in GetComponents<PlayMakerFSM>()) fsm.enabled = false;
            if (TryGetComponent<Game.Inventory.CandyTrail>(out var trail)) trail.enabled = false;
            Destroy(gameObject, 0.1f);
        }

        // Out of the player's hand for good (no grabbing it again); returns whether a hand was holding it.
        private bool LetGo()
        {
            bool inHand = mood == Mood.Held || (grab != null && grab.isSelected);
            if (grab != null && grab.isSelected && grab.interactionManager != null)
                grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);
            if (grab != null) grab.enabled = false;
            return inHand;
        }

        // It breaks, exploding: only a player holding it ('inHand') is hurt; things close by are hurt and pushed.
        private void Broke(bool inHand)
        {
            if (mood == Mood.Broken || turned) return;
            var center = ownColliders.Length > 0 && ownColliders[0] != null && ownColliders[0].enabled ? ownColliders[0].bounds.center : body.position;
            mood = Mood.Broken;
            hasTarget = false;
            // Its own laugh stops, so the Health FSM's break laugh plays clean.
            if (laughVoice != null) laughVoice.Stop();
            if (rumbleVoice != null) rumbleVoice.Stop();
            Game.VFX.StunStars.Hide(transform);
            if (!body.isKinematic) body.linearVelocity = new Vector3(0f, Mathf.Min(0f, body.linearVelocity.y), 0f);
            Blast.Explode(center, handBlastRadius, handBlastDamage, handBlastPush, body, handBlastEffects, handBlastSound,
                1f, inHand ? handBlastEvent : null, held: inHand, effectScale: handBlastEffectScale, byPlayer: false);
            if (exp > 0) Game.Leveling.PlayerLevel.AddExp(exp);
            onCaught.Invoke();
            Caught?.Invoke(this);
        }

        private void WatchHealth()
        {
            if (health == null) return;
            float now = health.Value;
            if (now < lastHealth)
            {
                float lost = lastHealth - now;
                if (!canRun)
                {
                    Hop(Vector3.zero, 1.6f);
                    Play(hitSounds, 1f, 1f);
                    if (candyEveryHealth > 0f)
                    {
                        lostForCandy += lost;
                        while (lostForCandy >= candyEveryHealth)
                        {
                            lostForCandy -= candyEveryHealth;
                            LaunchCandy();
                        }
                    }
                }
            }
            lastHealth = now;
            if (now <= 0f) Broke(LetGo());
        }

        // ------------------------------------------------------------------ turning into the Pumpkin Boss

        // The runner pumpkins' FSM 'Wait and Transform' (Wait 300 + Warn Before Transform), in C#: game time, so it pauses
        // with the game (and while Daruma has it switched off). Over the last Warn Last seconds it wobbles more and more
        // often and harder and its laugh deepens; over the last Rumble Last seconds it trembles with a growl. Never while
        // held, nor in the air (unless Overdue Seconds late). Returns true when it has just turned (it's going).
        private bool TickTransform()
        {
            if (transformAfter <= 0f || bossPrefab == null) return false;
            aliveFor += Time.deltaTime;
            float left = transformAfter - aliveFor;
            if (warnLast > 0f && left <= warnLast)
            {
                float anger = Anger();
                if (aliveFor >= nextWobble)
                {
                    nextWobble = aliveFor + Mathf.Max(0.1f, Mathf.Lerp(wobbleEvery.x, wobbleEvery.y, anger));
                    Wobble(Mathf.Lerp(wobble.x, wobble.y, anger));
                }
                if (rumbleLast > 0f && left <= rumbleLast && aliveFor >= nextTremble)
                {
                    nextTremble = aliveFor + Mathf.Max(0.1f, trembleEvery);
                    Wobble(1f);
                    Rumble();
                }
            }
            if (left > 0f) return false;

            if (mood == Mood.Held || (grab != null && grab.isSelected)) return false;
            bool settled = body.isKinematic || Grounded() || body.linearVelocity.sqrMagnitude < 0.04f;
            if (!settled && -left < overdueSeconds) return false;
            return TurnIntoBoss();
        }

        // 0 until the warning starts, then up to 1 when it turns.
        private float Anger() => transformAfter > 0f && warnLast > 0f && bossPrefab != null
            ? 1f - Mathf.Clamp01((transformAfter - aliveFor) / warnLast)
            : 0f;

        private void Wobble(float strength)
        {
            if (jiggle != null && strength > 0f) jiggle.Hit(strength);
        }

        private void Rumble()
        {
            if (rumbleVoice == null || rumbleSound == null || rumbleVolume <= 0f || rumbleVoice.isPlaying) return;
            // Play, not PlayOneShot, so Is Playing tells when it's done.
            rumbleVoice.clip = rumbleSound;
            rumbleVoice.volume = rumbleVolume;
            rumbleVoice.pitch = 1f;
            rumbleVoice.Play();
        }

        private bool TurnIntoBoss()
        {
            if (!BossSpot(out var at))
            {
                // No floor for the boss near here yet: again in a second.
                if (!warnedNoSpot) Debug.LogWarning($"[Laughing Pumpkin] '{name}' found no NavMesh for the Pumpkin Boss near {body.position}; trying again.", this);
                warnedNoSpot = true;
                aliveFor = transformAfter - 1f;
                return false;
            }
            turned = true;
            LetGo();
            // Its Health FSM can't break it now (the runner FSM switched Health off as it turned).
            foreach (var fsm in GetComponents<PlayMakerFSM>()) fsm.enabled = false;
            if (TryGetComponent<Game.Inventory.CandyTrail>(out var trail)) trail.enabled = false;
            if (laughVoice != null) laughVoice.Stop();
            if (rumbleVoice != null) rumbleVoice.Stop();
            Game.VFX.StunStars.Hide(transform);
            if (leashPuff != null) EffectPool.Play(leashPuff, body.position + Vector3.up * 0.3f, leashPuffScale * 2f);

            var head = Camera.main != null ? Camera.main.transform.position : at + transform.forward;
            var look = Flat(head - at);
            var boss = Instantiate(bossPrefab, at, look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity, SpawnedStuff());
            Debug.Log($"[Laughing Pumpkin] '{name}' wasn't caught in {transformAfter:0} s and turned into the Pumpkin Boss.", this);
            TurnedIntoBoss?.Invoke(this, boss);
            foreach (var shown in GetComponentsInChildren<Renderer>()) shown.enabled = false;
            foreach (var part in ownColliders) if (part != null) part.enabled = false;
            Destroy(gameObject);
            return true;
        }

        // On the boss's NavMesh (its agent type), where the pumpkin is: inside the PlayGround, clear of the cauldron and
        // not right on the player.
        private bool BossSpot(out Vector3 at)
        {
            at = body.position;
            if (arena != null && !arena.ContainsFlat(at)) at = ArenaMiddleFloor();
            if (pot != null)
            {
                var potAt = pot.transform.position;
                var fromPot = Flat(at - potAt);
                float clear = keepFromCauldron + 0.5f;
                if (fromPot.magnitude < clear)
                {
                    var dir = fromPot.sqrMagnitude > 0.01f ? fromPot.normalized : Flat(ArenaMiddleFloor() - potAt).normalized;
                    if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
                    at = new Vector3(potAt.x, at.y, potAt.z) + dir * clear;
                }
            }
            if (Camera.main != null)
            {
                var head = Camera.main.transform.position;
                var away = Flat(at - head);
                if (away.magnitude < 2f)
                {
                    var dir = away.sqrMagnitude > 0.01f ? away.normalized : Flat(ArenaMiddleFloor() - head).normalized;
                    if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
                    at = new Vector3(head.x, at.y, head.z) + dir * 2.5f;
                }
            }
            if (arena != null && !arena.ContainsFlat(at)) at = ArenaMiddleFloor();

            var filter = new NavMeshQueryFilter { agentTypeID = bossAgentType, areaMask = NavMesh.AllAreas };
            if (!NavMesh.SamplePosition(at, out var hit, 3f, filter)) return false;
            at = hit.position;
            return true;
        }

        // ------------------------------------------------------------------ grabbing

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            if (mood == Mood.Broken || turned) return;
            if (mood == Mood.Fresh && health != null) health.Value = Mathf.Max(1f, health.Value - 1f);
            mood = Mood.Held;
            held = 0f;
            hasTarget = false;
            Play(hitSounds, 1f, runPitch);
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            if (mood != Mood.Held) return;
            // Still out of breath: stunned again (it wakes as soon as its stun time is up).
            mood = staminaLeft > 0f ? Mood.Watching : Mood.Stunned;
        }

        // ------------------------------------------------------------------ hits

        private void OnCollisionEnter(Collision collision)
        {
            if (mood == Mood.Broken || mood == Mood.Held || turned || !canRun) return;
            if (collision.relativeVelocity.magnitude < hitSpeed) return;
            var other = collision.collider;
            if (IsPlayerBody(other)) return;
            // The floor and big props (some have a Rigidbody) aren't hits: landing on them is just landing.
            if (other.bounds.size.magnitude > 3f) return;
            var otherBody = collision.rigidbody;
            bool thrownOrSwung = (otherBody != null && !otherBody.isKinematic) || other.CompareTag("Weapon");
            if (!thrownOrSwung) return;

            // The hit's damage, worked out as for an enemy (the one rule), comes off its breath: only the player's hits.
            if (Time.time >= nextHurt)
            {
                var hit = EnemyHitDamage.Judge(collision, toughness, hitSettings, out bool byPlayer);
                if (byPlayer && hit.Damage > 0f)
                {
                    LoseStamina(hit.Damage * metresPerDamage);
                    nextHurt = Time.time + HurtCooldown;
                    // The hand holding what hit it buzzes as hard as it hurt.
                    HitBuzz.Dealt(collision, hit.Damage);
                }
            }

            var away = Flat(body.position - collision.GetContact(0).point);
            if (away.sqrMagnitude < 0.0001f) away = Flat(collision.relativeVelocity);
            Knock(away);
            Play(hitSounds, 1f, 1f);
            if (mood == Mood.Fresh) mood = Mood.Watching;
        }

        /// <summary>
        /// The player's blast (grenade, bottle): its damage over the Toughness, whole (the one rule), times Metres Per
        /// Damage comes off its breath, and it's knocked away.
        /// </summary>
        public void Hit(float strength, Vector3 point)
        {
            if (mood == Mood.Broken || mood == Mood.Held || turned || !canRun) return;
            LoseStamina(HitSettings.Whole(strength / Mathf.Max(0.1f, toughness)) * metresPerDamage);
            Knock(Flat(body.position - point));
            Play(hitSounds, 1f, 1f);
            if (mood == Mood.Fresh) mood = Mood.Watching;
        }

        // Hurt: out of breath sooner. At 0 it's stunned (never broken by this).
        private void LoseStamina(float amount)
        {
            if (!canRun || amount <= 0f) return;
            staminaLeft = Mathf.Max(0f, staminaLeft - amount);
            stillFor = 0f;
            if (staminaLeft <= 0f && (mood == Mood.Fleeing || mood == Mood.Watching || mood == Mood.Fresh)) Stun();
        }

        private static bool IsPlayerBody(Collider other)
        {
            if (other.CompareTag("Weapon")) return false;
            return other.GetComponentInParent<XROrigin>() != null;
        }

        private void Knock(Vector3 away)
        {
            away = away.sqrMagnitude > 0.0001f ? away.normalized : Random.insideUnitCircle.normalized.X0Y();
            if (!body.isKinematic) body.linearVelocity = away * knock.x + Vector3.up * knock.y;
            nextHop = Time.time + 0.35f;
        }

        private void Hop(Vector3 sideways, float up)
        {
            if (body.isKinematic || !Grounded()) return;
            body.linearVelocity = sideways + Vector3.up * up;
        }

        // ------------------------------------------------------------------ the PlayGround

        // Out of the PlayGround for Leash Seconds, or below its floor at all: back to the middle with a puff. Never while
        // held (a hand can take it anywhere).
        private void Leash()
        {
            if (arena == null || Time.time < nextLeash) return;
            nextLeash = Time.time + LeashCheckEvery;
            if (mood == Mood.Held || (grab != null && grab.isSelected)) { outsideFor = 0f; return; }
            var middle = ArenaMiddleFloor();
            bool below = body.position.y < middle.y - 2f;
            outsideFor = arena.ContainsFlat(body.position) ? 0f : outsideFor + LeashCheckEvery;
            if (below || outsideFor >= leashSeconds) BackInside(middle);
        }

        private void BackInside(Vector3 middle)
        {
            outsideFor = 0f;
            var head = Camera.main != null ? Camera.main.transform.position : middle + Vector3.forward * 10f;
            var spot = middle;
            for (int i = 0; i < 12; i++)
            {
                var tryAt = middle + Random.insideUnitCircle.X0Y() * 2.5f;
                if (Flat(tryAt - head).magnitude < 2.5f) continue;
                if (pot != null && Flat(tryAt - pot.transform.position).magnitude < keepFromCauldron) continue;
                if (NavMesh.SamplePosition(tryAt, out var onMesh, 1.5f, NavMesh.AllAreas)) { spot = onMesh.position; break; }
            }
            // Sitting on the floor, not in it.
            float bottom = ownColliders.Length > 0 && ownColliders[0] != null && ownColliders[0].enabled ? ownColliders[0].bounds.min.y : body.position.y;
            spot += Vector3.up * (body.position.y - bottom + 0.05f);
            if (leashPuff != null)
            {
                EffectPool.Play(leashPuff, body.position + Vector3.up * 0.3f, leashPuffScale);
                EffectPool.Play(leashPuff, spot + Vector3.up * 0.3f, leashPuffScale);
            }
            body.position = spot;
            transform.position = spot;
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            hasTarget = false;
            lastPos = spot;
            if (mood == Mood.Fleeing) mood = Mood.Watching;
        }

        // Inside the PlayGround by Arena Margin (the four points round it are inside too), and never at the cauldron.
        private bool InArena(Vector3 point)
        {
            if (pot != null && Flat(point - pot.transform.position).magnitude < keepFromCauldron) return false;
            if (arena == null) return true;
            if (!arena.ContainsFlat(point)) return false;
            if (arenaMargin <= 0f) return true;
            foreach (var side in Sides)
            {
                if (!arena.ContainsFlat(point + side * arenaMargin)) return false;
            }
            return true;
        }

        // The floor in the PlayGround's middle (as PlayGroundRounds finds it for the spit).
        private Vector3 ArenaMiddleFloor()
        {
            var middle = arena != null ? arena.Middle : body.position;
            if (Physics.Raycast(middle + Vector3.up * 2f, Vector3.down, out var hit, 30f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                middle = hit.point;
            if (NavMesh.SamplePosition(middle, out var onMesh, 3f, NavMesh.AllAreas)) middle = onMesh.position;
            return middle;
        }

        // ------------------------------------------------------------------ running and hiding

        private Vector3 NextStep()
        {
            var here = body.position;
            if (!hasTarget) return Random.insideUnitCircle.normalized.X0Y();

            if (Time.time >= nextPath)
            {
                nextPath = Time.time + 0.4f;
                corners.Clear();
                if (NavMesh.SamplePosition(here, out var from, 1.5f, NavMesh.AllAreas) &&
                    NavMesh.CalculatePath(from.position, target, NavMesh.AllAreas, path) &&
                    path.status != NavMeshPathStatus.PathInvalid)
                {
                    corners.AddRange(path.corners);
                }
            }

            var toward = target;
            foreach (var corner in corners)
            {
                if (Flat(corner - here).magnitude > 0.5f) { toward = corner; break; }
            }
            var step = Flat(toward - here);
            if (step.sqrMagnitude < 0.0001f) step = Random.insideUnitCircle.X0Y();
            // A little wobble so it doesn't move like a robot.
            return Quaternion.Euler(0f, Random.Range(-12f, 12f), 0f) * step.normalized;
        }

        private void PickHidingSpot(Transform head)
        {
            nextPick = Time.time + 1.6f;
            var here = body.position;
            var player = head != null ? head.position : here - transform.forward * 3f;
            var awayFromPlayer = Flat(here - player);
            if (awayFromPlayer.sqrMagnitude < 0.01f) awayFromPlayer = Random.insideUnitCircle.X0Y();
            awayFromPlayer.Normalize();

            float bestScore = float.MinValue;
            Vector3 best = here + awayFromPlayer * 2f;
            Collider[] bestPassThrough = null;
            bool found = false;

            // Its favourite spots first: inside a bush or tall grass, behind a tree trunk or the giant skull. Only close
            // ones: near and well hidden beats far.
            var toPlayerNow = Flat(player - here).normalized;
            foreach (var (point, passThrough, bonus) in HideSpots(player))
            {
                float far = Flat(point - here).magnitude;
                if (far > hideRadius) continue;
                if (!InArena(point)) continue;
                float fromPlayer = Flat(point - player).magnitude;
                if (fromPlayer < fleeRange) continue;                                   // not right next to the player
                float score = Mathf.Min(fromPlayer, calmRange + 2f) + bonus - 0.5f * far;
                if (Vector3.Dot(Flat(point - here).normalized, toPlayerNow) > 0.3f && far > 1f) score -= 8f;   // don't run past the player
                score += Random.Range(0f, 1.5f);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = point;
                    bestPassThrough = passThrough;
                    found = true;
                }
            }

            for (int i = 0; i < 20; i++)
            {
                // Mostly away from the player, sometimes off to the side; no further than one run.
                float angle = Random.Range(-120f, 120f);
                var direction = Quaternion.Euler(0f, angle, 0f) * awayFromPlayer;
                var spot = here + direction * Random.Range(1.5f, Mathf.Max(2f, runMetres));
                if (!InArena(spot)) continue;
                if (NavMesh.SamplePosition(spot, out var onMesh, 1.5f, NavMesh.AllAreas)) spot = onMesh.position;
                else if (NavMesh.SamplePosition(here, out _, 1.5f, NavMesh.AllAreas)) continue;   // there is a NavMesh, just not there

                float score = Mathf.Min(Flat(spot - player).magnitude, calmRange + 2f);   // away from the player
                if (Hidden(player, spot)) score += 5f;                                    // out of sight
                var toSpot = Flat(spot - here).normalized;
                var toPlayer = Flat(player - here).normalized;
                if (Vector3.Dot(toSpot, toPlayer) > 0.3f) score -= 8f;                    // don't run past the player
                score += Random.Range(0f, 1.5f);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = spot;
                    bestPassThrough = null;
                    found = true;
                }
            }
            if (!found) best = InArena(best) ? best : ArenaMiddleFloor();
            target = best;
            hasTarget = true;
            nextPath = 0f;

            // Going into a bush: it can pass through the bush's leaves.
            if (bestPassThrough != null)
            {
                foreach (var leaves in bestPassThrough)
                {
                    foreach (var own in ownColliders)
                    {
                        if (leaves != null && own != null) Physics.IgnoreCollision(own, leaves);
                    }
                }
            }
        }

        // Spots it likes to hide in: inside bushes (ghost bushes, and the bushes round a ghost tree's trunk) big enough to
        // hold it, and behind a ghost tree's trunk or the giant skull, on the side away from the player.
        private List<(Vector3 point, Collider[] passThrough, float bonus)> HideSpots(Vector3 player)
        {
            hideSpots.Clear();
            var mine = ownColliders.Length > 0 && ownColliders[0] != null ? ownColliders[0].bounds : new Bounds(body.position, Vector3.one * 0.7f);
            float width = Mathf.Max(mine.size.x, mine.size.z);

            foreach (var bush in FindObjectsByType<GhostBush>(FindObjectsSortMode.None))
            {
                var leaves = Solid(bush.GetComponentsInChildren<Collider>());
                AddBush(leaves, width);
            }
            // Tall grass (hideouts): it walks right in; inside, it can't be seen.
            foreach (var hideout in Hideout.All)
            {
                if (hideout == null || !hideout.Covering || hideout.Width < width * 1.3f || hideout.Height < width * 0.9f) continue;
                if (!NavMesh.SamplePosition(hideout.Spot, out var floor, 0.8f, NavMesh.AllAreas)) continue;
                hideSpots.Add((floor.position, null, 8f));
            }
            foreach (var tree in FindObjectsByType<GhostTree>(FindObjectsSortMode.None))
            {
                // Never the cauldron, even if it lets things out like a tree.
                if (tree.GetComponentInParent<Cauldron>() != null || tree.GetComponentInChildren<Cauldron>() != null) continue;
                var trunk = Solid(tree.GetComponents<Collider>());
                if (trunk.Length > 0)
                {
                    var bounds = Encapsulate(trunk);
                    var away = Flat(bounds.center - player);
                    if (away.sqrMagnitude > 0.01f)
                    {
                        float radius = Mathf.Max(bounds.extents.x, bounds.extents.z);
                        var behind = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) + away.normalized * (radius + width * 0.6f + 0.2f);
                        if (NavMesh.SamplePosition(behind, out var onMesh, 1f, NavMesh.AllAreas)) behind = onMesh.position;
                        hideSpots.Add((behind, null, 6f));
                    }
                }
                // The bushes round its trunk.
                foreach (var part in tree.GetComponentsInChildren<Collider>())
                {
                    if (part.transform == tree.transform || part.isTrigger) continue;
                    AddBush(new[] { part }, width);
                }
            }
            return hideSpots;
        }

        private void AddBush(Collider[] leaves, float width)
        {
            if (leaves.Length == 0) return;
            var bounds = Encapsulate(leaves);
            // Too small to hide in (e.g. the mini skull).
            if (Mathf.Min(bounds.size.x, bounds.size.z) < width * 1.3f || bounds.size.y < width * 0.9f) return;
            // Only bushes standing on the floor (not leaves up in a tree's crown).
            var inside = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            if (!NavMesh.SamplePosition(inside, out var floor, 0.8f, NavMesh.AllAreas) || Mathf.Abs(floor.position.y - bounds.min.y) > 0.5f) return;
            hideSpots.Add((floor.position, leaves, 8f));
        }

        private static Collider[] Solid(Collider[] colliders) => Array.FindAll(colliders, c => c != null && c.enabled && !c.isTrigger);

        private static Bounds Encapsulate(Collider[] colliders)
        {
            var bounds = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++) bounds.Encapsulate(colliders[i].bounds);
            return bounds;
        }

        // Is 'spot' out of the player's line of sight (something solid in between)?
        private bool Hidden(Vector3 eye, Vector3 spot)
        {
            // In tall grass nothing solid stands in the way, but it can't be seen there.
            if (Hideout.Covers(spot)) return true;
            var aim = spot + Vector3.up * 0.4f;
            var line = aim - eye;
            float length = line.magnitude;
            if (length < 0.01f) return false;
            foreach (var hit in Physics.RaycastAll(eye, line / length, length - 0.3f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (Array.IndexOf(ownColliders, hit.collider) >= 0) continue;
                if (hit.collider.GetComponentInParent<XROrigin>() != null) continue;
                if (hit.rigidbody != null && !hit.rigidbody.isKinematic && hit.collider.bounds.size.magnitude < 1f) continue;
                return true;
            }
            return false;
        }

        // On something solid, not rising. Loose things under it (its own candy trail, a ball) don't count, or it would
        // hop off them in mid-air and climb.
        private bool Grounded()
        {
            if (body.linearVelocity.y > 0.6f) return false;
            var bounds = ownColliders.Length > 0 && ownColliders[0] != null ? ownColliders[0].bounds : new Bounds(body.position, Vector3.one * 0.5f);
            var from = bounds.center;
            float reach = bounds.extents.y + 0.08f;
            foreach (var hit in Physics.RaycastAll(from, Vector3.down, reach, ~0, QueryTriggerInteraction.Ignore))
            {
                if (Array.IndexOf(ownColliders, hit.collider) >= 0) continue;
                if (hit.rigidbody != null && !hit.rigidbody.isKinematic && hit.collider.bounds.size.magnitude < 1.5f) continue;
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ candy, stars and sound

        private void LaunchCandy()
        {
            if (candies.Length == 0) return;
            var prefab = candies[Random.Range(0, candies.Length)];
            if (prefab == null) return;
            var bounds = ownColliders.Length > 0 && ownColliders[0] != null ? ownColliders[0].bounds : new Bounds(body.position, Vector3.one);
            var at = new Vector3(bounds.center.x, bounds.max.y + 0.3f, bounds.center.z);
            var candy = Instantiate(prefab, at, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), SpawnedStuff());
            foreach (var candyCollider in candy.GetComponentsInChildren<Collider>())
            {
                foreach (var own in ownColliders)
                {
                    if (own != null) Physics.IgnoreCollision(candyCollider, own);
                }
            }
            if (candy.TryGetComponent<Rigidbody>(out var candyBody))
                candyBody.linearVelocity = Random.insideUnitCircle.normalized.X0Y() * Random.Range(0.8f, 1.6f) + Vector3.up * candyLaunchSpeed;
        }

        // The runner pumpkins' tired stars (RunAwayPumpkin.ShowTired) over its top while it's stunned, without the dizzy
        // sound; gone as soon as it wakes, is picked up or breaks.
        private void ShowStars(bool on)
        {
            if (stunStars == null) return;
            if (!on)
            {
                if (starsOn) Game.VFX.StunStars.Hide(transform);
                starsOn = false;
                return;
            }
            starsOn = true;
            var shape = ownColliders.Length > 0 && ownColliders[0] != null && ownColliders[0].enabled ? ownColliders[0].bounds : new Bounds(body.position, Vector3.one * 0.5f);
            float ring = Mathf.Max(shape.extents.x, shape.extents.z) * stunStarsSize;
            float lift = shape.max.y - transform.position.y + ring * 0.35f;
            Game.VFX.StunStars.Show(stunStars, transform, transform, lift, ring, 0.25f, false);
        }

        // Non-stop, one laugh right after the other on its own speaker (the Pumpkin Boss's one-mouth laugh), never the same
        // clip twice in a row; quicker while it runs or is held, deeper as it's about to turn. Never while stunned.
        private void KeepLaughing()
        {
            if (laughVoice == null || laughs.Length == 0) return;
            if (Silent)
            {
                if (laughVoice.isPlaying) laughVoice.Stop();
                return;
            }
            if (laughVoice.isPlaying || Time.time < nextLaugh) return;
            int i = Random.Range(0, laughs.Length);
            if (laughs.Length > 1 && i == lastLaugh) i = (i + 1 + Random.Range(0, laughs.Length - 1)) % laughs.Length;
            lastLaugh = i;
            var clip = laughs[i];
            if (clip == null) return;
            float pitch = (mood == Mood.Fleeing || mood == Mood.Held ? runPitch : 1f) * Mathf.Lerp(1f, lowestPitch, Anger()) * Random.Range(0.97f, 1.03f);
            laughVoice.clip = clip;
            laughVoice.pitch = pitch;
            laughVoice.Play();
            nextLaugh = Time.time + clip.length / Mathf.Max(0.1f, pitch) + Random.Range(laughGap.x, laughGap.y);
        }

        private void Play(AudioClip[] clips, float volume, float pitch)
        {
            if (voice == null || clips.Length == 0 || Silent) return;
            var clip = clips[Random.Range(0, clips.Length)];
            if (clip == null) return;
            voice.pitch = pitch;
            voice.PlayOneShot(clip, volume);
        }

        // One more speaker like its own (the Pumpkin Boss's laugh speaker).
        private AudioSource ExtraVoice()
        {
            var extra = gameObject.AddComponent<AudioSource>();
            extra.playOnAwake = false;
            extra.loop = false;
            extra.outputAudioMixerGroup = voice.outputAudioMixerGroup;
            extra.volume = voice.volume;
            extra.spatialBlend = voice.spatialBlend;
            extra.rolloffMode = voice.rolloffMode;
            if (voice.rolloffMode == AudioRolloffMode.Custom)
                extra.SetCustomCurve(AudioSourceCurveType.CustomRolloff, voice.GetCustomCurve(AudioSourceCurveType.CustomRolloff));
            extra.minDistance = voice.minDistance;
            extra.maxDistance = voice.maxDistance;
            extra.priority = voice.priority;
            extra.dopplerLevel = voice.dopplerLevel;
            extra.spread = voice.spread;
            return extra;
        }

        private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

        private static Transform SpawnedStuff()
        {
            var variable = FsmVariables.GlobalVariables.FindFsmGameObject(SpawnedStuffVariable);
            return variable != null && variable.Value != null ? variable.Value.transform : null;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            DrawCircle(transform.position, catchRange);
            Gizmos.color = Color.yellow;
            DrawCircle(transform.position, fleeRange);
            Gizmos.color = Color.cyan;
            DrawCircle(transform.position, hideRadius);
            if (hasTarget)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, target);
                Gizmos.DrawWireSphere(target, 0.3f);
            }
        }

        private static void DrawCircle(Vector3 center, float radius)
        {
            var last = center + Vector3.forward * radius;
            for (int i = 1; i <= 32; i++)
            {
                float angle = i / 32f * Mathf.PI * 2f;
                var next = center + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;
                Gizmos.DrawLine(last, next);
                last = next;
            }
        }
#endif
    }

    internal static class LaughingPumpkinVectors
    {
        public static Vector3 X0Y(this Vector2 v) => new(v.x, 0f, v.y);
    }
}

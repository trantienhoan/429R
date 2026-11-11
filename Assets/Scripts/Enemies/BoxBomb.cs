using System.Collections.Generic;
using Game.Combat;
using Game.Weapons;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using FsmFloat = HutongGames.PlayMaker.FsmFloat;
using FsmVariables = HutongGames.PlayMaker.FsmVariables;

namespace Game.Enemies
{
    /// <summary>
    /// A paper box that turns out to be a bomb (user, 2026-11-10). Each box rolls once when it appears: Chance (0.30) of
    /// them are armed, the rest drop this component and stay plain boxes. An armed box looks, weighs and breaks exactly
    /// like any other box until the player comes near: then it shakes (little physics hops, the shaking bottles' rattle,
    /// a soft orange pulse on its own texture), hops once more and flies straight at the eyes. In the face it goes off:
    /// DamageHeavy x0.5 (20-25), a small shove, and it breaks in front of the face with its normal drop. Dodged, it goes
    /// off in the air or on what it hits (a small bite only when the eyes are close). The player's answers: hit it while
    /// it's awake (it breaks at once, normal drop, no bang), grab it (defused for good), back off during the shake (it
    /// calms down) or duck. At most Max Awake (1) boxes are awake at once, with Rest Seconds (3) after each attack.
    /// Boxes anywhere can wake, also behind the player (they shake longer then). It never changes Is Kinematic or Use
    /// Gravity (XRI saves and restores those on a grab). Tools > 429 Game > Enemies > Set Up Box Bombs puts it on
    /// Paper_Box_1-5 (not on Paper_Box_0 or the shop's Candy_Box: those are paid for).
    /// </summary>
    [DisallowMultipleComponent]
    public class BoxBomb : MonoBehaviour
    {
        [Header("Becoming a bomb")]
        [Tooltip("The chance a box is a bomb, rolled once when it appears (user, 2026-11-10: 0.30). A box that misses " +
                 "removes this component and stays a plain box for good.")]
        [Range(0f, 1f)]
        [SerializeField] private float chance = 0.3f;

        [Header("Waking up")]
        [Tooltip("A box can't wake until this many seconds after it appeared (rooms drop all their boxes at once and " +
                 "they settle).")]
        [Min(0f)]
        [SerializeField] private float wakeDelay = 3f;
        [Tooltip("...and until it has lain still this many seconds (a knock starts this over).")]
        [Min(0f)]
        [SerializeField] private float stillSeconds = 1f;
        [Tooltip("Slower than this (m/s) counts as lying still.")]
        [Min(0.01f)]
        [SerializeField] private float stillSpeed = 0.2f;
        [Tooltip("It wakes when the player's head is this close, in metres, measured flat along the floor (so a box on " +
                 "the floor and one on a shelf wake at the same distance).")]
        [Min(0.1f)]
        [SerializeField] private float wakeRange = 2.2f;
        [Tooltip("...and the head is at most this many metres higher or lower than the box.")]
        [Min(0.1f)]
        [SerializeField] private float wakeHeight = 2f;
        [Tooltip("Boxes outside the view wake too (user, 2026-11-10). One farther than this many degrees from where the " +
                 "head looks counts as out of view and shakes longer (Out Of View Shake Seconds).")]
        [Range(0f, 180f)]
        [SerializeField] private float viewAngle = 60f;
        [Tooltip("At most this many boxes awake (shaking or flying) at once (user, 2026-11-10: 1). Shared by all the " +
                 "boxes: the first armed box in the room decides, so set the same on Paper_Box_1-5.")]
        [Min(1)]
        [SerializeField] private int maxAwake = 1;
        [Tooltip("After an attack ends (a bang, a swat, a defuse or a calm-down), no box may wake for this many seconds " +
                 "(user, 2026-11-10: 3). Shared: the box whose attack ended decides, so set the same on Paper_Box_1-5.")]
        [Min(0f)]
        [SerializeField] private float restSeconds = 3f;
        [Tooltip("How often, in seconds, the boxes look for the player (one check for all of them). Shared: the first " +
                 "armed box in the room decides, so set the same on Paper_Box_1-5.")]
        [Min(0.05f)]
        [SerializeField] private float checkEvery = 0.2f;

        [Header("Shaking (the warning)")]
        [Tooltip("How long it shakes before it jumps, when the player can see it.")]
        [Min(0.1f)]
        [SerializeField] private float shakeSeconds = 1f;
        [Tooltip("How long it shakes when it's out of view, so the player hears it and can turn (user, 2026-11-10: 1.4).")]
        [Min(0.1f)]
        [SerializeField] private float outOfViewShakeSeconds = 1.4f;
        [Tooltip("Little kicks a second while it shakes.")]
        [Min(1f)]
        [SerializeField] private float kicksPerSecond = 11f;
        [Tooltip("Each kick hops it up this fast, in m/s (0.45 is a hop of about 1 cm).")]
        [Min(0f)]
        [SerializeField] private float hopSpeed = 0.45f;
        [Tooltip("Each kick tilts it this fast, in radians a second (a few degrees).")]
        [Min(0f)]
        [SerializeField] private float tiltSpeed = 1.5f;
        [Tooltip("Loops on the box (3D) for as long as it shakes: the shaking bottles' rattle (poision_bottle_shake).")]
        [SerializeField] private AudioClip rattle;
        [Range(0f, 1f)]
        [SerializeField] private float rattleVolume = 0.8f;
        [Tooltip("Plays Beep Before seconds before it flies: the 'about to blow' beep (elon_boong_beep_beep).")]
        [SerializeField] private AudioClip beep;
        [Min(0f)]
        [SerializeField] private float beepBefore = 0.35f;
        [Range(0f, 1f)]
        [SerializeField] private float beepVolume = 0.8f;
        [Tooltip("Its own texture glows while it's awake, pulsing (no particles, no light). Untick for shake and rattle " +
                 "only.")]
        [SerializeField] private bool glow = true;
        [Tooltip("The glow's colour (its texture's light times this).")]
        [ColorUsage(false, true)]
        [SerializeField] private Color glowColour = new(1f, 0.45f, 0.2f, 1f);
        [Tooltip("Pulses a second while it shakes.")]
        [Min(0.1f)]
        [SerializeField] private float glowPulse = 6f;
        [Tooltip("Glows without pulsing for its last this-many seconds before it flies.")]
        [Min(0f)]
        [SerializeField] private float glowSolid = 0.25f;

        [Header("Jumping and flying")]
        [Tooltip("At the last hop it calms down instead (still a bomb, it can wake again later) if the player's head is " +
                 "farther than this many metres (flat along the floor), the way to the eyes is blocked, or the player " +
                 "is down.")]
        [Min(0.1f)]
        [SerializeField] private float calmDistance = 3.2f;
        [Tooltip("The last hop, in m/s up (1.5 is about 12 cm).")]
        [Min(0f)]
        [SerializeField] private float lastHop = 1.5f;
        [Tooltip("It flies this many seconds after the last hop. Its aim locks onto the eyes then: it doesn't follow " +
                 "the head.")]
        [Min(0f)]
        [SerializeField] private float hopSeconds = 0.15f;
        [Tooltip("How fast it flies, in m/s: from X to Y over Speed Up Seconds, dead straight. Under 7 m/s it never " +
                 "hurts itself or other boxes on what it hits.")]
        [SerializeField] private Vector2 flySpeed = new(3f, 5.5f);
        [Min(0f)]
        [SerializeField] private float speedUpSeconds = 0.25f;
        [Tooltip("It turns over this fast while it flies, in radians a second (0: it doesn't).")]
        [Min(0f)]
        [SerializeField] private float tumble = 2f;
        [Tooltip("Plays as it flies off (whoosh).")]
        [SerializeField] private AudioClip launchSound;
        [Range(0f, 1f)]
        [SerializeField] private float launchVolume = 0.8f;
        [SerializeField] private Vector2 launchPitch = new(1.15f, 1.35f);
        [Tooltip("For its first this-many seconds of flight it doesn't go off on what it touches (it leaves its shelf).")]
        [Min(0f)]
        [SerializeField] private float launchGrace = 0.12f;
        [Tooltip("Loose things up to this heavy (kg) are pushed aside as it flies, and don't hide the player from a " +
                 "sleeping box: books, bottles and boxes weigh 100, candy 10, broken pieces 1. Anything heavier (furniture " +
                 "420 and up, shelves, big pumpkins), the walls and anything fixed stop it, and it goes off there. Other " +
                 "boxes (Stop Names) and bugs stop it too.")]
        [Min(0f)]
        [SerializeField] private float pushMass = 100f;
        [Tooltip("Things whose names start with one of these stop it like furniture, although they weigh no more than a " +
                 "book: the other boxes. Their broken pieces (names with _Drop) are pushed aside.")]
        [SerializeField] private string[] stopNames = { "Paper_Box", "Candy_Box" };
        [Tooltip("It hits the face when the eyes come this close to its surface, in metres (or it touches the player's " +
                 "body no higher than Duck Height above the eyes).")]
        [Min(0f)]
        [SerializeField] private float faceReach = 0.2f;
        [Tooltip("The player's body keeps its standing height while they duck (it changes only when they move by stick), " +
                 "so a touch on the body more than this many metres above the eyes is a duck under it: it flies on and " +
                 "misses. Lower, a touch on the body counts as the face hit.")]
        [Min(0f)]
        [SerializeField] private float duckHeight = 0.2f;
        [Tooltip("Dodged: once it's this many metres past where the head was, it goes off in the air.")]
        [Min(0f)]
        [SerializeField] private float missPast = 0.6f;
        [Tooltip("It goes off in the air after this many seconds of flight at the most.")]
        [Min(0.1f)]
        [SerializeField] private float maxFlightSeconds = 1f;

        [Header("Hurting the player")]
        [Tooltip("Sent to the player's Damage FSM when it hits the face (DamageHeavy: 40-50 before Toughness).")]
        [SerializeField] private string faceEvent = "DamageHeavy";
        [Tooltip("The face hit does this share of it (user, 2026-11-10: 0.5, so 20-25).")]
        [Range(0f, 3f)]
        [SerializeField] private float faceShare = 0.5f;
        [Tooltip("A face hit shoves the player this high (X) and this far back (Y), in metres (the Fat Pumpkin's bite " +
                 "shove; no camera shake).")]
        [SerializeField] private Vector2 shove = new(0.15f, 0.6f);
        [Tooltip("A face hit goes off this far in front of the eyes (level, also when the player looks down), so the bang " +
                 "and what falls out of the box are in view, not inside the head. When a wall, shelf or other thing is " +
                 "there, it goes off this far back along the way it came instead, or else where it is.")]
        [Min(0f)]
        [SerializeField] private float bangInFront = 0.45f;
        [Tooltip("A bang that missed the face still bites with this (Damage: 6-9) when the eyes are within Near Reach.")]
        [SerializeField] private string nearEvent = "Damage";
        [Min(0f)]
        [SerializeField] private float nearReach = 0.6f;

        [Header("Bang")]
        [Tooltip("How far the bang reaches, in metres.")]
        [Min(0.1f)]
        [SerializeField] private float bangRadius = 1f;
        [Tooltip("What the bang does to bugs and boxes in reach (the one blast rule). 1 hurts a bug by 1 and a box by " +
                 "nothing, so a bang in a pile of boxes breaks none. Not the player's blast: a bug it kills gives no EXP.")]
        [Min(0f)]
        [SerializeField] private float bangDamage = 1f;
        [Tooltip("Loose things in reach are pushed away this fast, in m/s; heavy ones much less (Push Weight: the Jenga " +
                 "blocks barely move).")]
        [Min(0f)]
        [SerializeField] private float bangPush = 1.5f;
        [Tooltip("Its effects, played from the Effect Pool (Smoke_Flash_Small_Puff, Flash_round_yellow). Never " +
                 "Smoke_Blast.")]
        [SerializeField] private GameObject[] effects = System.Array.Empty<GameObject>();
        [Min(0.01f)]
        [SerializeField] private float effectSize = 0.6f;
        [SerializeField] private AudioClip bangSound;
        [Range(0f, 1f)]
        [SerializeField] private float bangVolume = 0.8f;

        [Header("Swatting it")]
        [Tooltip("A box awake (shaking or flying) that the player hits at least this hard (m/s: a fist, a held or " +
                 "thrown thing, a shot) breaks at once with its normal drop and no bang. Any health it loses while " +
                 "awake does the same.")]
        [Min(0f)]
        [SerializeField] private float swatSpeed = 1f;

        [Header("PlayMaker")]
        [SerializeField] private string healthFsm = "Health";
        [SerializeField] private string healthVariable = "health";
        [Tooltip("The fairy shield's tag. Its clean-up pops the box (the Health FSM's Trigger Event): an awake box stops " +
                 "at once, so it can't still hit the face while the pop plays.")]
        [SerializeField] private string shieldTag = "Fairy_Shield";

        private enum State { Sleeping, Shaking, Hop, Flying, Done }

        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        // At most this many boxes are tried (one ray each) per check, nearest first.
        private const int RaysPerCheck = 3;
        // Flying slower than this share of its speed after the grace: it's stuck on something, and goes off there.
        private const float StuckShare = 0.35f;
        // A box whose way to the eyes was blocked isn't tried again for this long, so the next checks' rays go to the
        // boxes behind it (else three blocked boxes nearest the player would keep a clear one farther off asleep).
        private const float BlockedRecheck = 1f;

        private static readonly List<BoxBomb> armed = new();
        private static readonly List<BoxBomb> candidates = new();
        private static readonly RaycastHit[] rayHits = new RaycastHit[32];
        private static readonly Collider[] overlap = new Collider[128];
        private static readonly HashSet<Rigidbody> pushed = new();
        private static int awakeCount;
        private static float nextCheck;
        private static float restUntil;
        private static PlayMakerFSM playerHealthFsm;
        private static FsmFloat playerHealth;
        private static float nextPlayerLookup;

        /// <summary>Test only (Box Bombs: Arm Every New Box): every box that appears from now on is armed.</summary>
        public static bool ForceArm { get; set; }

        /// <summary>How many armed boxes there are now (sleeping or awake).</summary>
        public static int ArmedCount => armed.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            armed.Clear();
            candidates.Clear();
            pushed.Clear();
            awakeCount = 0;
            nextCheck = 0f;
            restUntil = 0f;
            playerHealthFsm = null;
            playerHealth = null;
            nextPlayerLookup = 0f;
            ForceArm = false;
        }

        private Rigidbody body;
        private XRGrabInteractable grab;
        private EnemyHitDamage hits;
        private Renderer look;
        private readonly List<Collider> ownColliders = new();
        private FsmFloat health;
        private MaterialPropertyBlock block;
        private Color baseEmission;
        private bool glowing;
        private bool hadBlock;

        private bool rolled;
        private bool listening;
        private bool broken;
        private bool forced;
        private State state = State.Sleeping;
        private float spawnedAt;
        private float stillSince;
        private float blockedUntil;
        private float stateAt;
        private float shakeFor;
        private float nextKick;
        private bool beeped;
        private float healthAtWake;
        private Vector3 aim;
        private Vector3 dir;
        private AudioSource rattleSource;
        private bool modeChanged;
        private CollisionDetectionMode oldMode;
        private WeaponPower weapon;
        private bool powerMuted;
        private float oldPower;
        private float speedNow;
        // The player's body parts it flew past (a duck under it), let through until the attack ends.
        private readonly List<Collider> passedThrough = new();

        private bool IsAwake => state == State.Shaking || state == State.Hop || state == State.Flying;

        // Rolled once, after every FSM has set itself up (as Enemy Hit Damage does).
        private void Start()
        {
            rolled = true;
            TryGetComponent(out body);
            if ((!ForceArm && Random.value >= chance) || body == null)
            {
                // Removed, not switched off: Unity still sends collisions to switched-off scripts.
                Destroy(this);
                return;
            }
            TryGetComponent(out grab);
            TryGetComponent(out hits);
            TryGetComponent(out weapon);
            if (!TryGetComponent(out look)) look = GetComponentInChildren<Renderer>();
            foreach (var c in GetComponentsInChildren<Collider>())
            {
                if (!c.isTrigger && c.attachedRigidbody == body) ownColliders.Add(c);
            }
            health = FindHealth();
            spawnedAt = stillSince = Time.time;
            Blast.Warm(effects);
            Join();
        }

        private void OnEnable()
        {
            // Start does the first one (after the roll).
            if (rolled && state != State.Done) Join();
        }

        private void OnDisable()
        {
            armed.Remove(this);
            // Popped by a fairy shield, a room reset...: the awake slot is free again.
            if (IsAwake) EndAttack(State.Sleeping);
            if (listening && grab != null) grab.selectEntered.RemoveListener(OnGrabbed);
            listening = false;
        }

        private void Join()
        {
            if (!armed.Contains(this)) armed.Add(this);
            stillSince = Time.time;
            if (!listening && grab != null)
            {
                grab.selectEntered.AddListener(OnGrabbed);
                listening = true;
            }
        }

        private FsmFloat FindHealth()
        {
            foreach (var fsm in GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName == healthFsm) return fsm.FsmVariables.FindFsmFloat(healthVariable);
            }
            return null;
        }

        private void Update()
        {
            // The first one checks for all of them.
            if (armed.Count > 0 && armed[0] == this && Time.time >= nextCheck)
            {
                nextCheck = Time.time + checkEvery;
                CheckAll();
            }
            if (!IsAwake) return;
            // Any health lost while awake (a weak punch, the player's own grenade...): broken, no bang.
            if (health != null && health.Value < healthAtWake - 0.001f)
            {
                Note("lost health while awake: broken, no bang");
                Break(false);
                return;
            }
            Glow();
        }

        // ---------------------------------------------------------------- waking

        private static void CheckAll()
        {
            float now = Time.time;
            foreach (var one in armed) one.TrackStill(now);
            if (armed.Count == 0 || awakeCount >= armed[0].maxAwake || now < restUntil) return;
            if (Game.Menus.PauseMenu.IsPaused || PlayerDown()) return;
            var head = SlimeKit.Head;
            if (head == null) return;
            var eyes = head.position;

            candidates.Clear();
            foreach (var one in armed)
            {
                if (one.CanWake(now, eyes)) candidates.Add(one);
            }
            // Nearest first; a box with no clear line to the eyes (in a pile, behind a shelf board) stays asleep.
            for (int tries = 0; tries < RaysPerCheck && candidates.Count > 0; tries++)
            {
                int best = 0;
                float bestDistance = float.MaxValue;
                for (int i = 0; i < candidates.Count; i++)
                {
                    float d = candidates[i].FlatDistance(eyes);
                    if (d >= bestDistance) continue;
                    bestDistance = d;
                    best = i;
                }
                var box = candidates[best];
                candidates.RemoveAt(best);
                if (!box.ClearLineTo(eyes))
                {
                    box.blockedUntil = now + BlockedRecheck;
                    continue;
                }
                box.Wake(head, false);
                break;
            }
            candidates.Clear();
        }

        // A knock (or a hand) starts its still time over.
        private void TrackStill(float now)
        {
            if (state != State.Sleeping || body == null) return;
            bool moving = body.linearVelocity.sqrMagnitude > stillSpeed * stillSpeed;
            if (moving || (grab != null && (grab.isSelected || HeldThings.InUse(grab)))) stillSince = now;
        }

        private bool CanWake(float now, Vector3 eyes)
        {
            if (state != State.Sleeping || broken || body == null || body.isKinematic) return false;
            if (now - spawnedAt < wakeDelay || now - stillSince < stillSeconds || now < blockedUntil) return false;
            if (grab != null && (grab.isSelected || HeldThings.InUse(grab))) return false;
            if (health != null && health.Value <= 0f) return false;
            var centre = body.worldCenterOfMass;
            if (Mathf.Abs(eyes.y - centre.y) > wakeHeight) return false;
            return FlatDistance(eyes) <= wakeRange;
        }

        private float FlatDistance(Vector3 eyes)
        {
            var centre = body.worldCenterOfMass;
            float x = eyes.x - centre.x;
            float z = eyes.z - centre.z;
            return Mathf.Sqrt(x * x + z * z);
        }

        // Nothing that would stop it between it and the eyes: the player, loose books, bottles, candy and pieces, and what
        // the player holds don't count.
        private bool ClearLineTo(Vector3 eyes)
        {
            var from = body.worldCenterOfMass;
            var way = eyes - from;
            float distance = way.magnitude;
            if (distance < 0.01f) return true;
            int count = Physics.RaycastNonAlloc(from, way / distance, rayHits, distance, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var c = rayHits[i].collider;
                if (c == null) continue;
                var other = c.attachedRigidbody;
                if (other == body) continue;
                if (other != null && other.TryGetComponent<XRGrabInteractable>(out var held) && held.isSelected) continue;
                if (!Stops(other)) continue;
                if (Blast.IsPlayer(c)) continue;
                return false;
            }
            return true;
        }

        // What it can't push through: the scenery, anything fixed or heavier than a book, a bug, another box. Books and
        // bottles weigh as much as a box (100 kg), so weight alone can't tell them apart: the other boxes go by name.
        private bool Stops(Rigidbody other)
        {
            if (other == null) return true;
            if (other == body) return false;
            if (other.isKinematic || other.mass > pushMass) return true;
            if (other.TryGetComponent<EnemyHitDamage>(out var enemy) && !enemy.IsFurniture) return true;
            return IsOtherBox(other.name);
        }

        private bool IsOtherBox(string thing)
        {
            if (stopNames == null || thing.Contains("_Drop")) return false;
            foreach (var start in stopNames)
            {
                if (!string.IsNullOrEmpty(start) && thing.StartsWith(start, System.StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private void Wake(Transform head, bool test)
        {
            forced = test;
            awakeCount++;
            Enter(State.Shaking);
            var toBox = body.worldCenterOfMass - head.position;
            bool inView = Vector3.Angle(head.forward, toBox) <= viewAngle;
            shakeFor = inView ? shakeSeconds : outOfViewShakeSeconds;
            healthAtWake = health != null ? health.Value : 0f;
            nextKick = Time.time;
            beeped = false;
            StopRattle();
            rattleSource = LoopOn(rattle, rattleVolume);
            Note($"woke ({(inView ? "in view" : "out of view")}, shakes {shakeFor:0.0} s, {FlatDistance(head.position):0.00} m flat)");
        }

        private void Enter(State next)
        {
            state = next;
            stateAt = Time.time;
        }

        // ---------------------------------------------------------------- shaking, hopping, flying

        private void FixedUpdate()
        {
            switch (state)
            {
                case State.Shaking: Shake(); break;
                case State.Hop:
                    if (Time.time - stateAt >= hopSeconds) Launch();
                    break;
                case State.Flying: Fly(); break;
            }
        }

        private void Shake()
        {
            // Something else took it over (made it kinematic), or the player is down: it lies still again.
            if (body.isKinematic || PlayerDown())
            {
                CalmDown("the player is down or it's held still");
                return;
            }
            float t = Time.time - stateAt;
            // The beep comes a moment before the last hop decides, so it waits while that would calm it down (the beep
            // used to play for a box that then calmed down; play test 2026-11-10).
            if (!beeped && t >= shakeFor + hopSeconds - beepBefore && WouldGo())
            {
                beeped = true;
                Ride(beep, beepVolume, Vector2.one);
            }
            if (t >= shakeFor)
            {
                LastHop();
                return;
            }
            if (Time.time < nextKick) return;
            nextKick = Time.time + 1f / kicksPerSecond;
            // A little hop (only when it isn't still going up from the last one) and a new little tilt.
            if (body.linearVelocity.y < hopSpeed * 0.5f) body.AddForce(Vector3.up * hopSpeed, ForceMode.VelocityChange);
            var axis = Random.insideUnitCircle;
            if (axis.sqrMagnitude < 0.0001f) axis = Vector2.right;
            axis.Normalize();
            body.angularVelocity = new Vector3(axis.x, 0f, axis.y) * tiltSpeed;
        }

        // Whether the last hop would go ahead as things are now (the same checks as LastHop).
        private bool WouldGo()
        {
            var head = SlimeKit.Head;
            if (head == null || PlayerDown()) return false;
            return forced || (FlatDistance(head.position) <= calmDistance && ClearLineTo(head.position));
        }

        // The 'now' moment: calm down if the player got away, can't be seen or is down; else one bigger hop.
        private void LastHop()
        {
            var head = SlimeKit.Head;
            if (head == null || PlayerDown())
            {
                CalmDown("the player is down");
                return;
            }
            if (!forced)
            {
                if (FlatDistance(head.position) > calmDistance)
                {
                    CalmDown("the player backed off");
                    return;
                }
                if (!ClearLineTo(head.position))
                {
                    CalmDown("the way to the eyes is blocked");
                    return;
                }
            }
            StopRattle();
            body.angularVelocity = Vector3.zero;
            body.AddForce(Vector3.up * (lastHop - body.linearVelocity.y), ForceMode.VelocityChange);
            Enter(State.Hop);
        }

        private void Launch()
        {
            var head = SlimeKit.Head;
            if (head == null || body.isKinematic || PlayerDown())
            {
                CalmDown("the player is down or it's held still");
                return;
            }
            aim = head.position;
            var way = aim - body.worldCenterOfMass;
            dir = way.sqrMagnitude > 0.0001f ? way.normalized : -head.forward;
            if (!modeChanged)
            {
                oldMode = body.collisionDetectionMode;
                modeChanged = true;
            }
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            // While it flies it hits like nothing (Power 0): a bug it hits is hurt only by the bang (no EXP), not by its
            // own collision rule, which would pay EXP for a knock the player didn't make.
            if (weapon != null && !powerMuted)
            {
                oldPower = weapon.Power;
                weapon.Power = 0f;
                powerMuted = true;
            }
            speedNow = flySpeed.x;
            body.linearVelocity = dir * flySpeed.x;
            if (body.useGravity) body.AddForce(-Physics.gravity, ForceMode.Acceleration);
            body.angularVelocity = tumble > 0f ? Random.onUnitSphere * tumble : Vector3.zero;
            Ride(launchSound, launchVolume, launchPitch);
            Enter(State.Flying);
            Note("flies");
        }

        private void Fly()
        {
            if (body.isKinematic)
            {
                CalmDown("it's held still");
                return;
            }
            float t = Time.time - stateAt;
            var centre = body.worldCenterOfMass;
            var head = SlimeKit.Head;
            if (head != null && !PlayerDown() && NearFace(head.position))
            {
                HitPlayer(head);
                return;
            }
            float speed = Mathf.Lerp(flySpeed.x, flySpeed.y, speedUpSeconds > 0f ? Mathf.Clamp01(t / speedUpSeconds) : 1f);
            speedNow = speed;
            // Stuck on something it can't push (it stays in touch, so no new collision says so).
            if (t >= launchGrace && Vector3.Dot(body.linearVelocity, dir) < speed * StuckShare)
            {
                Note("stuck: goes off there");
                Bang(centre, false);
                return;
            }
            // Dodged: past where the head was, or flying too long.
            if (Vector3.Dot(centre - aim, dir) > missPast || t >= maxFlightSeconds)
            {
                Note("missed: goes off in the air");
                Bang(centre, false);
                return;
            }
            // Dead straight: its speed set every step, gravity cancelled by a force (Use Gravity is never touched).
            body.linearVelocity = dir * speed;
            if (body.useGravity) body.AddForce(-Physics.gravity, ForceMode.Acceleration);
        }

        // The eyes within Face Reach of its surface (its real shape: the boxes are 0.11-0.52 m).
        private bool NearFace(Vector3 eyes)
        {
            float reach = faceReach * faceReach;
            foreach (var c in ownColliders)
            {
                if (c == null || !c.enabled) continue;
                bool exact = c is BoxCollider || c is SphereCollider || c is CapsuleCollider || (c is MeshCollider mesh && mesh.convex);
                var point = exact ? c.ClosestPoint(eyes) : c.bounds.ClosestPoint(eyes);
                if ((point - eyes).sqrMagnitude <= reach) return true;
            }
            return false;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!IsAwake || broken) return;
            // The player's hit first (their fists sit under the XR Origin too): broken, normal drop, no bang.
            if (HeldThings.ByPlayer(collision) && collision.relativeVelocity.magnitude >= swatSpeed)
            {
                Note("swatted: broken, no bang");
                Break(false);
                return;
            }
            if (state != State.Flying) return;
            if (collision.collider != null && Blast.IsPlayer(collision.collider))
            {
                var head = SlimeKit.Head;
                if (head != null)
                {
                    var at = collision.contactCount > 0 ? collision.GetContact(0).point : body.worldCenterOfMass;
                    if (at.y <= head.position.y + duckHeight)
                    {
                        HitPlayer(head);
                        return;
                    }
                    // Above the eyes: the body's standing-height top, the player ducked under it. It flies on.
                    PassThrough(collision.collider);
                    return;
                }
            }
            if (Time.time - stateAt < launchGrace || !Stops(collision.rigidbody)) return;
            Note("hit " + collision.gameObject.name + ": goes off there");
            Bang(collision.contactCount > 0 ? collision.GetContact(0).point : body.worldCenterOfMass, false);
        }

        // Ducked under it: that part of the player's body lets it through until the attack ends, and the bump on it
        // doesn't count (its speed is put back, so it isn't taken as stuck).
        private void PassThrough(Collider part)
        {
            Note("the player ducked under it: flies on");
            if (!passedThrough.Contains(part))
            {
                foreach (var own in ownColliders)
                {
                    if (own != null) Physics.IgnoreCollision(own, part, true);
                }
                passedThrough.Add(part);
            }
            body.linearVelocity = dir * speedNow;
        }

        private void StopPassingThrough()
        {
            foreach (var part in passedThrough)
            {
                if (part == null) continue;
                foreach (var own in ownColliders)
                {
                    if (own != null) Physics.IgnoreCollision(own, part, false);
                }
            }
            passedThrough.Clear();
        }

        // A fairy shield's clean-up pops it (its Health FSM's Trigger Event does that, with no drop): it stops where it is
        // and is a plain box again, so it can't still hit the face while the pop plays.
        private void OnTriggerEnter(Collider other)
        {
            if (state == State.Done || broken || other == null || string.IsNullOrEmpty(shieldTag) ||
                !other.CompareTag(shieldTag)) return;
            bool moving = state == State.Hop || state == State.Flying;
            if (IsAwake) Note("popped by the fairy shield");
            EndAttack(State.Done);
            armed.Remove(this);
            if (moving && body != null && !body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            Destroy(this);
        }

        // ---------------------------------------------------------------- endings

        // In the face (or it touched the body): the hit, then it goes off in front of the face.
        private void HitPlayer(Transform head)
        {
            var eyes = head.position;
            // Where it came from (the side the red lines show, what a held shield must cover, where the shove comes from).
            var from = eyes - dir;
            if (!PlayerDown())
            {
                if (LegendaryGear.TryBlock(from)) Note("in the face, blocked by the shield");
                else
                {
                    Note("in the face");
                    PlayerDamageShare.Next(faceShare);
                    Blast.SendToPlayer(faceEvent);
                    DamageVignette.HitFrom(from);
                    PlayerLaunch.Launch(from, shove.x, shove.y, false);
                }
            }
            var point = FaceBangSpot(eyes, head);
            var centre = body.worldCenterOfMass;
            if ((point - centre).sqrMagnitude > 0.0001f)
            {
                // Moved there whole (its middle on the spot), the transform and the body alike.
                var moved = transform.position + (point - centre);
                transform.position = moved;
                body.position = moved;
            }
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            Bang(point, true);
        }

        // Where a face hit goes off: Bang In Front level in front of the eyes (in view, and not in the player's own body
        // when they look down) when the whole box fits there; else that far back along the way it came (it just flew
        // through there); else where it is. Never inside a wall or a shelf, where its drop would be lost and the things
        // it overlapped would be thrown out hard.
        private Vector3 FaceBangSpot(Vector3 eyes, Transform head)
        {
            var centre = body.worldCenterOfMass;
            if (!OwnBounds(out var bounds)) return centre;
            var offset = bounds.center - centre;
            var size = bounds.extents * 0.9f;
            var ahead = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (ahead.sqrMagnitude > 0.01f)
            {
                var spot = eyes + ahead.normalized * bangInFront;
                if (Fits(eyes, spot, offset, size)) return spot;
            }
            var back = eyes - dir * bangInFront;
            if (Fits(eyes, back, offset, size)) return back;
            return centre;
        }

        private bool OwnBounds(out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (var c in ownColliders)
            {
                if (c == null || !c.enabled) continue;
                if (!any) bounds = c.bounds;
                else bounds.Encapsulate(c.bounds);
                any = true;
            }
            return any;
        }

        // Nothing solid between the eyes and Spot, and room for the box there (the player, what they hold and the box
        // itself don't count).
        private bool Fits(Vector3 eyes, Vector3 spot, Vector3 offset, Vector3 size)
        {
            var way = spot - eyes;
            float distance = way.magnitude;
            if (distance > 0.01f)
            {
                int count = Physics.RaycastNonAlloc(eyes, way / distance, rayHits, distance, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    if (InTheWay(rayHits[i].collider)) return false;
                }
            }
            int found = Physics.OverlapBoxNonAlloc(spot + offset, size, overlap, Quaternion.identity,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < found; i++)
            {
                if (InTheWay(overlap[i])) return false;
            }
            return true;
        }

        private bool InTheWay(Collider c)
        {
            if (c == null) return false;
            var other = c.attachedRigidbody;
            if (other == body || Blast.IsPlayer(c)) return false;
            return other == null || !other.TryGetComponent<XRGrabInteractable>(out var held) || !held.isSelected;
        }

        private void Bang(Vector3 point, bool hitFace)
        {
            if (broken) return;
            // Bugs and boxes by the one rule, not the player's blast (no EXP); its own push below, by weight.
            Blast.Explode(point, bangRadius, bangDamage, 0f, body, effects, bangSound, bangVolume, null, false, effectSize,
                -1f, false);
            PushAround(point);
            if (!hitFace) NearBite(point);
            Break(true);
        }

        // Loose things pushed away, heavy ones much less (Push Weight); what the player holds stays in the hand.
        private void PushAround(Vector3 point)
        {
            if (bangPush <= 0f) return;
            pushed.Clear();
            int count = Physics.OverlapSphereNonAlloc(point, bangRadius, overlap, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var other = overlap[i].attachedRigidbody;
                if (other == null || other == body || other.isKinematic || !pushed.Add(other)) continue;
                if (other.TryGetComponent<XRGrabInteractable>(out var held) && held.isSelected) continue;
                if (Blast.IsPlayer(overlap[i])) continue;
                float share = PushWeight.Share(other);
                if (share <= 0f) continue;
                other.AddExplosionForce(bangPush * share, point, bangRadius, 0.5f, ForceMode.VelocityChange);
            }
            pushed.Clear();
        }

        // A bang that missed still stings when the eyes are close (pinned against a wall).
        private void NearBite(Vector3 point)
        {
            var head = SlimeKit.Head;
            if (head == null || string.IsNullOrEmpty(nearEvent) || PlayerDown()) return;
            if ((head.position - point).sqrMagnitude > nearReach * nearReach) return;
            if (LegendaryGear.TryBlock(point)) return;
            DamageVignette.HitFrom(point);
            Blast.SendToPlayer(nearEvent);
        }

        // Breaks the usual way, once: its own Health FSM drops its normal items (as Grenade's Break Apart does).
        private void Break(bool quiet)
        {
            if (broken) return;
            broken = true;
            EndAttack(State.Done);
            armed.Remove(this);
            if (TryGetComponent(out Game.Leveling.WeaponWear wear)) wear.enabled = false;
            // It broke itself, not a hit: no damage number.
            if (quiet) DamageNumbers.Quiet(gameObject);
            if (hits != null) hits.noReward = true;
            if (health != null)
            {
                if (health.Value > 0f) health.Value = 0f;
            }
            else Destroy(gameObject);
        }

        // Back to sleep, still a bomb (it can wake again once it has lain still).
        private void CalmDown(string why)
        {
            Note("calms down: " + why);
            EndAttack(State.Sleeping);
            stillSince = Time.time;
        }

        // Grabbed (sleeping, shaking or flying): a plain box for good.
        private void OnGrabbed(SelectEnterEventArgs args)
        {
            if (broken || state == State.Done) return;
            if (IsAwake) Note("caught: defused");
            EndAttack(State.Done);
            armed.Remove(this);
            Destroy(this);
        }

        // The attack is over (or never started): the slot is free, the rest starts, sounds and glow stop.
        private void EndAttack(State next)
        {
            bool wasAwake = IsAwake;
            state = next;
            stateAt = Time.time;
            StopRattle();
            ClearGlow();
            StopPassingThrough();
            if (modeChanged && body != null)
            {
                body.collisionDetectionMode = oldMode;
                modeChanged = false;
            }
            // Its hits count again (asleep or defused). Not when it broke: a bug's own collision this same step must
            // still find Power 0.
            if (powerMuted && !broken)
            {
                if (weapon != null) weapon.Power = oldPower;
                powerMuted = false;
            }
            if (!wasAwake) return;
            awakeCount = Mathf.Max(0, awakeCount - 1);
            restUntil = Mathf.Max(restUntil, Time.time + restSeconds);
        }

        // ---------------------------------------------------------------- glow and sounds

        private void Glow()
        {
            if (!glow || look == null) return;
            float amount = 1f;
            if (state == State.Shaking)
            {
                float t = Time.time - stateAt;
                float left = shakeFor + hopSeconds - t;
                if (left > glowSolid) amount = 0.5f - 0.5f * Mathf.Cos(t * glowPulse * 2f * Mathf.PI);
            }
            SetGlow(Color.Lerp(BaseEmission(), glowColour, amount));
        }

        private Color BaseEmission()
        {
            if (glowing) return baseEmission;
            var material = look.sharedMaterial;
            baseEmission = material != null && material.HasProperty(EmissionColor) ? material.GetColor(EmissionColor) : Color.black;
            return baseEmission;
        }

        // On its own material through a property block (no new material); what else was in the block is kept.
        private void SetGlow(Color colour)
        {
            block ??= new MaterialPropertyBlock();
            if (!glowing)
            {
                hadBlock = look.HasPropertyBlock();
                glowing = true;
            }
            look.GetPropertyBlock(block);
            block.SetColor(EmissionColor, colour);
            look.SetPropertyBlock(block);
        }

        private void ClearGlow()
        {
            if (!glowing) return;
            glowing = false;
            if (look == null) return;
            if (!hadBlock)
            {
                look.SetPropertyBlock(null);
                return;
            }
            look.GetPropertyBlock(block);
            block.SetColor(EmissionColor, baseEmission);
            look.SetPropertyBlock(block);
        }

        // A clip riding on the box (3D: the box's own Audio Source is 2D), so it moves with it and stops with it.
        private AudioSource Ride(AudioClip clip, float volume, Vector2 pitch)
        {
            return clip == null ? null : SlimeKit.PlayOn(new[] { clip }, transform, volume, pitch);
        }

        // The rattle: its own looping 3D source on the box (Slime Kit's Play On removes its sound when one play of the
        // clip ends, so a shake longer than the clip would go quiet). Settings as Play On's.
        private AudioSource LoopOn(AudioClip clip, float volume)
        {
            if (clip == null) return null;
            var go = new GameObject("Box Bomb Rattle");
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.pitch = Random.Range(0.95f, 1.05f);
            source.loop = true;
            source.spatialBlend = 1f;
            source.minDistance = 1.5f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.maxDistance = 25f;
            source.dopplerLevel = 0f;
            source.Play();
            return source;
        }

        private void StopRattle()
        {
            if (rattleSource != null) Destroy(rattleSource.gameObject);
            rattleSource = null;
        }

        // ---------------------------------------------------------------- the player

        // Down, dying or getting back up (as Player Launch checks it).
        private static bool PlayerDown()
        {
            if (Game.Saving.Checkpoints.IsDying) return true;
            if (PlayerRevive.Instance != null && PlayerRevive.Instance.IsReviving) return true;
            var hp = PlayerHealth();
            return hp != null && hp.Value <= 0f;
        }

        private static FsmFloat PlayerHealth()
        {
            if (playerHealthFsm != null) return playerHealth;
            if (Time.time < nextPlayerLookup) return null;
            nextPlayerLookup = Time.time + 1f;
            var player = FsmVariables.GlobalVariables.FindFsmGameObject("Player");
            if (player == null || player.Value == null) return null;
            foreach (var fsm in player.Value.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName != "PlayerHealth") continue;
                playerHealthFsm = fsm;
                playerHealth = fsm.FsmVariables.FindFsmFloat("PlayerHealth");
                return playerHealth;
            }
            return null;
        }

        // ---------------------------------------------------------------- tests

        /// <summary>
        /// Test only (Box Bombs: Wake Nearest): wakes the armed box nearest the head, whatever its distance, the view,
        /// the cap and the rest; it doesn't calm down for distance or a blocked way either. What happened, for the log.
        /// </summary>
        public static string WakeNearestForTest()
        {
            var head = SlimeKit.Head;
            if (head == null) return "no main camera";
            BoxBomb best = null;
            float bestDistance = float.MaxValue;
            foreach (var one in armed)
            {
                if (one.state != State.Sleeping || one.broken || one.body == null || one.body.isKinematic) continue;
                float d = (one.body.worldCenterOfMass - head.position).sqrMagnitude;
                if (d >= bestDistance) continue;
                bestDistance = d;
                best = one;
            }
            if (best == null) return $"no sleeping armed box ({armed.Count} armed)";
            best.Wake(head, true);
            return $"woke {best.name}, {Mathf.Sqrt(bestDistance):0.00} m from the head ({armed.Count} armed)";
        }

        // What a box does, in the Console, while testing (a test wake, or Arm Every New Box on).
        private void Note(string what)
        {
#if UNITY_EDITOR
            if (forced || ForceArm) Debug.Log($"[Box Bomb] {name}: {what}", this);
#endif
        }
    }
}

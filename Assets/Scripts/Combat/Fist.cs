using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Game.Combat
{
    /// <summary>
    /// Lets the player punch with an empty hand. Squeezing grip and trigger together (a fist) with nothing in that
    /// hand turns on a small invisible ball at the knuckles that follows the hand by physics, so it hits things like a
    /// held weapon does: enemies take damage by how fast the punch was (x Power; it takes several hard punches to kill a
    /// spider, and mini bosses barely feel it), loose things get pushed, and the controller rumbles. While it's a fist
    /// the hand swells to Fist Scale (half as big again) with a little buzz, so the player sees it's ready and punches
    /// land easier; the ball grows with it.
    /// Three punches thrown well do more, and fly: an echo of the fist, bigger than the fist (Fly Size), goes on the way
    /// the punch went (Jab Reach) and hits the first thing in its way. A jab is a fast, straight punch out where the
    /// player is looking, and flies straight ahead. A swing (a hook: the fist sweeps sideways) hits harder (Swing Power)
    /// and flies on sideways, level, the way the fist swept (a left hook sweeping to the right flies to the right). An
    /// uppercut (the fist drives upwards) hits harder (Uppercut Power), flies up, sends things flying up and away and
    /// knocks enemies back (Uppercut Push); on an enemy's head it's a knock-out (Head Power, Knock Out Seconds). A swing
    /// and an uppercut fly bigger than a jab (Big Move Size). They count the same when the fist itself lands. Which one
    /// a punch is goes by where the hand went, from the player's point of view, measured in the play area's own space, so
    /// walking and turning don't count as punching. Slow pushes and hammer fists are just punches.
    /// The Fist Size level-up stat unlocks them (Jab Unlocked, Swing Unlocked, Uppercut Unlocked; until then a punch
    /// doesn't fly; for now only the Jab unlocks, Player Level's Lock Swing And Uppercut) and makes punches hit harder
    /// and flying punches bigger and fly farther; the hand itself grows only a little. At full Fist
    /// Size (Triple Punch) a jab throws two more fists, one each side, and a swing or an uppercut two more in a row
    /// along the punch, each starting farther out and reaching farther.
    /// A fist doesn't pull things from a distance: with the trigger squeezed too, the hand only grabs what's within its
    /// reach, so making a fist while pointing at something doesn't yank it into the hand instead of punching it.
    /// Pointing (trigger let go) and squeezing the grip still pulls things from afar.
    /// A smoked boong's buff (Player Buffs) makes punches stronger for a while: with the Elon Boong the ball reaches 0.5 m
    /// past the knuckles, punches do double damage, and every punch that hits something explodes once more where it hit
    /// (Elon Boong, never hurting the player; the Explosive Fist bonus stat does that too, harder with each point); the
    /// Elon Boong (0.5 m) and the Green Boong (5 m) make every flying punch fly farther.
    /// Goes on each controller (Left Controller, Right Controller), next to the hand model.
    /// </summary>
    [DisallowMultipleComponent]
    public class Fist : MonoBehaviour, IXRSelectFilter
    {
        [Tooltip("Multiplies the punch speed for damage, like a weapon's Power. At 1.2 a firm punch hurts small bugs, a " +
                 "hard one hurts spiders and cockroaches, and mini bosses (Toughness 5-7) barely feel it. A fist moves " +
                 "only as fast as the hand, so it stays weaker than a swung weapon.")]
        [Min(0f)]
        [SerializeField] private float power = 2.6f;
        [Tooltip("How far the grip and trigger must be squeezed (0 to 1) to make a fist.")]
        [Range(0.1f, 1f)]
        [SerializeField] private float squeeze = 0.6f;
        [Tooltip("Untick to punch with the grip alone, without the trigger.")]
        [SerializeField] private bool needTrigger = true;
        [Tooltip("Size of the fist ball, in metres (its radius), at the hand's normal size. It grows with Fist Scale.")]
        [Min(0.01f)]
        [SerializeField] private float radius = 0.055f;
        [Tooltip("Moves the fist ball from the knuckles, in the controller's own space, in metres (Z = forward).")]
        [SerializeField] private Vector3 offset = Vector3.zero;
        [Tooltip("Squeeze held this long, in seconds, before the fist counts, so grabbing something doesn't punch it away first.")]
        [Min(0f)]
        [SerializeField] private float formSeconds = 0.12f;
        [Tooltip("With the trigger squeezed at least this much, the hand doesn't pull things from a distance (it's making " +
                 "a fist); it still grabs what's within its reach. 0 = fists pull things from afar too.")]
        [Range(0f, 1f)]
        [SerializeField] private float noFarGrabTrigger = 0.25f;

        [Tooltip("How much harder things made in PlayMaker feel a punch (pumpkins and the like: a Damage FSM that compares " +
                 "collisionForce in a Check force state). They were tuned for swung weapons (10-15), and a fist only " +
                 "moves 3-6 metres per second: punch speed x this is what they get. 1 = no help.")]
        [Min(1f)]
        [SerializeField] private float pumpkinForce = 2.6f;

        [Tooltip("How hard a punch sends loose things flying, like a throw: the thing leaves at the punch's speed x this. " +
                 "1 = as fast as the fist, 0 = only the bump physics gives by itself. Heavy things give way less: up to " +
                 "Mass 1,000 in full, 100,000 a third of it, 1,000,000 or more not at all (Push Weight).")]
        [Min(0f)]
        [SerializeField] private float punchPush = 1.5f;

        [Header("Big Fist")]
        [Tooltip("How big the hand grows while it's a fist (2 = twice its size), so it's easy to see and punches land " +
                 "easier. The fist ball grows with it. 1 = no change. The Fist Size level-up stat adds a little to it " +
                 "(Hand Size Per Point on Player Level); it mostly makes the flying punches bigger.")]
        [Min(1f)]
        [SerializeField] private float fistScale = 1.5f;
        [Tooltip("The most Fist Size adds to the hand (on top of Fist Scale), however many points, so the hand never " +
                 "blocks the view.")]
        [Min(0f)]
        [SerializeField] private float maxHandGrowth = 0.4f;
        [Tooltip("How long, in seconds, the hand takes to grow into a big fist, and to shrink back.")]
        [Min(0f)]
        [SerializeField] private float growSeconds = 0.08f;
        [Tooltip("The point the hand grows around, in the controller's own space, in metres. 0 = the controller's grip, " +
                 "so the big fist stays around the player's real hand.")]
        [SerializeField] private Vector3 growAround = Vector3.zero;
        [Tooltip("A short rumble when the fist forms, so the player feels it's ready to punch. 0 = none.")]
        [Range(0f, 1f)]
        [SerializeField] private float formRumble = 0.2f;

        [Header("Flying Jab")]
        [Tooltip("A fast, straight punch flies on past the fist and hits what's in front, farther than the arm reaches. " +
                 "Untick to turn it off.")]
        [SerializeField] private bool flyingJab = true;
        [Tooltip("How fast the fist must move at its fastest, in metres per second, for the punch to fly. An easy punch is " +
                 "about 2-3, a snappy jab 4-6.")]
        [Min(0.5f)]
        [SerializeField] private float jabSpeed = 2.6f;
        [Tooltip("A jab this fast or faster (and straight, and aimed) flies the farthest.")]
        [Min(0.5f)]
        [SerializeField] private float topJabSpeed = 5f;
        [Tooltip("How far past the fist a flying punch goes (a jab, a swing, an uppercut), in metres: x for a " +
                 "just-good-enough one, y for a perfect one.")]
        [SerializeField] private Vector2 jabReach = new(0.4f, 0.75f);
        [Tooltip("Longest the punch may take, in seconds, from starting to move to snapping to a stop. Slower pushes don't fly.")]
        [Min(0.05f)]
        [SerializeField] private float jabSeconds = 0.4f;
        [Tooltip("How straight the punch must be: 1 = a perfectly straight line. Swings, hooks and hammer fists curve, so " +
                 "they don't fly.")]
        [Range(0.5f, 1f)]
        [SerializeField] private float jabStraightness = 0.75f;
        [Tooltip("How far from where the player is looking the punch may go, in degrees.")]
        [Range(10f, 90f)]
        [SerializeField] private float jabAim = 60f;
        [Tooltip("A jab this good or better counts as a perfect jab (the bonus round's board counts them): 0 = just good " +
                 "enough, 1 = at Top Jab Speed, dead straight, right where the player looks. Counted whether or not jabs fly yet.")]
        [Range(0f, 1f)]
        [SerializeField] private float perfectJab = 0.8f;
        [Tooltip("How hard a flying punch hits (a jab, a swing, an uppercut), compared with the fist itself hitting at " +
                 "the punch's top speed; a jab that lands with the fist itself hits this hard too. 1 = the same.")]
        [Min(0f)]
        [SerializeField] private float jabPower = 1.8f;
        [Tooltip("Optional sound when a punch flies, e.g. a whoosh.")]
        [SerializeField] private AudioClip jabSound;

        [Header("Flying Punches (grow with the Fist Size level-up stat)")]
        [Tooltip("How big a flying jab is compared with the fist (at Fist Scale), before Fist Size: 1.3 = a third bigger. " +
                 "Each Fist Size point makes it bigger still (Punch Size Per Point on Player Level).")]
        [Min(0.5f)]
        [SerializeField] private float flySize = 1.3f;
        [Tooltip("The biggest a flying jab gets compared with the fist (at Fist Scale), however much Fist Size (with the " +
                 "Green Boong's on top), so it never fills the view. Swings and uppercuts are Big Move Size times this.")]
        [Min(0.5f)]
        [SerializeField] private float maxFlySize = 4f;
        [Tooltip("A flying swing and a flying uppercut are this many times as big as a flying jab.")]
        [Min(1f)]
        [SerializeField] private float bigMoveSize = 1.35f;
        [Tooltip("At full Fist Size, flying punches go this many times as far as when the Jab is unlocked (Jab Reach x " +
                 "this); in between, step by step.")]
        [Min(1f)]
        [SerializeField] private float maxReachScale = 2.5f;
        [Tooltip("Triple Punch (full Fist Size): the two extra jab fists fly out this many degrees to each side.")]
        [Range(0f, 45f)]
        [SerializeField] private float tripleJabSpread = 10f;
        [Tooltip("Triple Punch: a swing's or an uppercut's 2nd fist reaches this share farther than the 1st, the 3rd " +
                 "twice that (0.5 = 1.5x and 2x as far). Each also starts a fist's width farther out than the one before.")]
        [Min(0f)]
        [SerializeField] private float tripleReach = 0.5f;
        [Tooltip("The burst at the wrist of a flying punch, riding along with it (it leaves no trail behind and goes when " +
                 "the punch does), e.g. Flying_Punch_Shockwave (or Smoke_Blast). Its star flash (a part called Star, or for " +
                 "Smoke_Blast the system on its root) shows only with Explosive Fist points; it burns red while the hands " +
                 "are hot (Hot Hands). Empty: none.")]
        [FormerlySerializedAs("flyTrailPuff")]
        [SerializeField] private GameObject wristSmoke;
        [Tooltip("How big that burst is for a flying punch the size of the fist; bigger flying punches have a bigger one " +
                 "(Flying_Punch_Shockwave is made in metres: 1; Smoke_Blast: 0.004).")]
        [FormerlySerializedAs("flyTrailSize")]
        [Min(0f)]
        [SerializeField] private float wristSmokeSize = 1f;

        [Header("Swing")]
        [Tooltip("A fast sideways sweep of the fist (a hook) hits harder, and flies on sideways, level, the way the fist " +
                 "swept. Untick to turn it off.")]
        [SerializeField] private bool swing = true;
        [Tooltip("How fast the fist must sweep at its fastest, in metres per second, to count as a swing.")]
        [Min(0.5f)]
        [SerializeField] private float swingSpeed = 3.2f;
        [Tooltip("How much harder a swing hits than a plain punch of the same speed, when the fist lands and when it flies.")]
        [Min(1f)]
        [SerializeField] private float swingPower = 1.6f;

        [Header("Uppercut")]
        [Tooltip("A fast drive of the fist upwards sends loose things flying, up and away, knocks enemies back with a " +
                 "high hop, and flies on upwards. Untick to turn it off.")]
        [SerializeField] private bool uppercut = true;
        [Tooltip("How fast the fist must rise at its fastest, in metres per second, to count as an uppercut.")]
        [Min(0.5f)]
        [SerializeField] private float uppercutSpeed = 2.8f;
        [Tooltip("How much harder an uppercut hits than a plain punch of the same speed, when the fist lands and when it flies.")]
        [Min(1f)]
        [SerializeField] private float uppercutPower = 1.6f;
        [Tooltip("How much harder an uppercut sends things flying than a plain punch (Punch Push x this), and how much " +
                 "higher a knocked-back enemy hops.")]
        [Min(1f)]
        [SerializeField] private float uppercutPush = 2f;
        [Tooltip("An uppercut that lands on an enemy's head is a knock-out: an extra hit this many times as strong as the " +
                 "punch, thrown back farther and higher.")]
        [Min(1f)]
        [SerializeField] private float headPower = 2.5f;
        [Tooltip("How long a knocked-out enemy lies stunned, in seconds, before it gets up again (bosses that stand their " +
                 "ground only take the damage).")]
        [Min(0f)]
        [SerializeField] private float knockOutSeconds = 2.5f;

        [Header("Input (left empty: the hand model's own)")]
        [SerializeField] private InputActionProperty grip;
        [SerializeField] private InputActionProperty trigger;

        [Header("Feel")]
        [Tooltip("Shown where a good punch lands: a flying punch, or the fist itself landing a swing, an uppercut or " +
                 "anything at Jab Speed or faster (e.g. Smoke_Blast). It plays at the hit from copies kept ready (Effect Pool). " +
                 "Not on enemies, which show their own hit pop (Hit Settings). Empty: none.")]
        [SerializeField] private GameObject goodPunchEffect;
        [Tooltip("Size of that effect, compared with its prefab.")]
        [Min(0.01f)]
        [SerializeField] private float goodPunchEffectSize = 0.5f;
        [Tooltip("Played where a punch (the fist or a flying one) lands on anything, e.g. fist_bump_punch. Empty: none.")]
        [SerializeField] private AudioClip punchHitSound;
        [Tooltip("Its loudness: x for the slowest punch that counts, y from Jab Speed on.")]
        [SerializeField] private Vector2 punchHitVolume = new(0.45f, 0.9f);
        [Tooltip("Its pitch, at random between these, so punches don't all sound the same.")]
        [SerializeField] private Vector2 punchHitPitch = new(0.9f, 1.12f);

        [Header("Elon Boong (a smoked boong's buff, see Player Buffs) and Explosive Fist (a bonus stat)")]
        [Tooltip("While the Elon Boong buff is on, or with Explosive Fist points, every punch that hits something explodes " +
                 "once more where it hit (each Explosive Fist point is one Boong Blast Damage), with " +
                 "these effects (the bottles': Flash_round_yellow, Hit_Blast, Smoke_Blast). Empty: the Good Punch Effect. " +
                 "Tools > 429 Game > Player > Set Up Boong Buffs fills them in.")]
        [SerializeField] private GameObject[] boongBlastEffects = System.Array.Empty<GameObject>();
        [Tooltip("Size of those effects, compared with their prefabs (made for a blast about 2 m across).")]
        [Min(0.01f)]
        [SerializeField] private float boongBlastEffectSize = 0.3f;
        [SerializeField] private AudioClip boongBlastSound;
        [Range(0f, 1f)]
        [SerializeField] private float boongBlastVolume = 0.45f;
        [Tooltip("How far that explosion reaches, in metres.")]
        [Min(0.05f)]
        [SerializeField] private float boongBlastRadius = 0.6f;
        [Tooltip("Health everything in it loses, on top of the punch (which does double damage while the buff is on). It " +
                 "never hurts the player.")]
        [Min(0f)]
        [SerializeField] private float boongBlastDamage = 1f;
        [Tooltip("How fast it pushes loose things away, in metres per second.")]
        [Min(0f)]
        [SerializeField] private float boongBlastPush = 2f;
        [Tooltip("Shortest time between two of these explosions from one hand, in seconds (a Triple Punch's three fists " +
                 "landing together explode once).")]
        [Min(0f)]
        [SerializeField] private float boongBlastEvery = 0.2f;
        [Tooltip("Explosive Fist: each point cuts the time before the hand can throw another flying punch by this share " +
                 "(0.25 = 25% a point: 0.25 s, then 0.19, 0.13, 0.06 s at 3 points).")]
        [Range(0f, 0.33f)]
        [SerializeField] private float explosiveFistFaster = 0.25f;

        // Where the fist is when the bones can't be found: a little in front of the controller.
        private static readonly Vector3 FallbackPoint = new(0f, -0.01f, 0.05f);
        // Farther than this from the hand (stuck on a wall), the ball jumps back to it. So it does when the point it
        // follows jumps this far in one step (a snap turn, a teleport), instead of sweeping across.
        private const float SnapDistance = 0.3f;
        private const float MaxSpeed = 25f;
        // The Elon Boong reach: the ball moves out from the knuckles (and catches up after being held back) only this
        // much faster than the hand itself moves, in metres per second, so closing a fist or turning the hand next to
        // something isn't a punch (below what PlayMaker breakables and the tip board count as a hit).
        private const float ReachOutSpeed = 2f;
        // The play area turning or moving more than this in one physics step is a snap turn or a teleport, not walking.
        private const float RigJumpDegrees = 5f;
        private const float RigJumpDistance = 0.1f;
        // A pooled effect copy is busy this long after it starts, in seconds (Effect Pool's Lifetime).
        private const float EffectBusySeconds = 3f;
        // Most copies of each Elon Boong explosion effect made ready at load.
        private const int MaxBoongBlastCopies = 15;
        // Things this close to the hand, in metres, are within its reach (the same as Gentle Far Grab's far distance).
        private const float Reach = 0.4f;
        // A punch starts when the hand moves this fast (metres per second)...
        private const float StrokeSpeed = 1f;
        // ...and snaps when it slows to this share of its top speed (or comes back).
        private const float SnapShare = 0.45f;
        // The fist travels at least this far in a jab, a swing or an uppercut, in metres.
        private const float MoveTravel = 0.15f;
        // A swing may be this far off sideways, and an uppercut this far off straight up, in degrees.
        private const float SwingAngle = 50f;
        private const float UppercutAngle = 45f;
        // Longest a swing or an uppercut may take, in seconds (they travel farther than a jab).
        private const float LongMoveSeconds = 0.7f;
        // Shortest time between two flying punches from one hand (Explosive Fist makes it shorter, never under the min).
        private const float FlyCooldown = 0.25f;
        private const float MinFlyCooldown = 0.05f;
        // Fewest flying fists one hand may have out at once (a Triple Punch is 3); punches that fly far allow more (Max
        // Flyers). Past it, the one nearest the end of its flight is thrown again.
        private const int MinFlyers = 9;
        // How fast the flying fist flies, in metres per second; its damage is worked out from the punch's own speed.
        private const float FlySpeed = 8f;
        // How far behind a flying fist's middle its wrist smoke sits, in its ball's radii.
        private const float WristBehind = 1.5f;
        // Punches slower than this (metres per second) don't send things flying, and nothing leaves faster than Max.
        // A hand moving slower than this (metres per second) isn't punching: no rumble, push, puff or pumpkin help.
        private const float MinPunchSpeed = 1f;
        private const float MinPushSpeed = 1.5f;
        private const float MaxPushSpeed = 14f;

        private static readonly List<Fist> fists = new();

        private InputAction gripAction;
        private InputAction triggerAction;
        private readonly List<IXRSelectInteractor> interactors = new();
        private readonly List<NearFarInteractor> nearFars = new();
        private XRBaseInputInteractor rumbler;
        private Transform knuckleA;
        private Transform knuckleB;
        private Transform palm;
        private Rigidbody body;
        private SphereCollider ball;
        private WeaponPower weapon;
        private float squeezedSince = -1f;
        private bool closed;
        private Transform handModel;
        private Vector3 handScale;
        private Vector3 handPosition;
        // The hand's size now: 1 = normal, Fist Scale = a big fist.
        private float grown = 1f;
        private SkinnedMeshRenderer handSkin;
        // Flying fists, made when first needed and used again once they've stopped.
        private readonly List<FlyingFist> flyers = new();
        private readonly List<Collider> playerParts = new();
        // The punch being thrown, in the play area's space (the controller's parent).
        private bool hasLastHand;
        private Vector3 lastHand;
        private Vector3 handVelocity;
        private bool stroking;
        // The last punch is still carrying on the same way: the next one starts once the hand has settled or turned.
        private bool followingThrough;
        private Vector3 lastStrokeDirection;
        private Move strokeMove;
        private float strokeMoveUntil;
        private Vector3 strokeStart;
        private float strokeStartTime;
        private float strokePath;
        private float strokePeak;
        // How good the stroke is: as its move, and as a jab (whether or not jabs fly yet; -1: it isn't one).
        private float strokeQuality;
        private float strokeJab = -1f;
        // Which stroke it is (both hands count up together), so the flying fists of one punch count as one.
        private int strokeId;
        private static int strokeCount;
        private float lastBallHit = -1f;
        private float nextFly;
        private float nextPuff;
        private float nextBoongBlast;
        // The punch hit sound: where it plays from (made on first use), and when it may play again (a hit can be
        // reported several times in one step).
        private AudioSource punchSpeaker;
        private float nextPunchSound;
        private const float PunchSoundEvery = 0.08f;
        // The Elon Boong's explosion effects: Boong Blast Effects, or the Good Punch Effect when that's empty.
        private GameObject[] boongEffects;
        // The Elon Boong reach: how far out from the knuckles the ball is now (world space, moving out at Reach Out
        // Speed), and the point it followed last physics step.
        private Vector3 reachOut;
        private Vector3 lastTarget;
        // The play area (the controller's parent) at the last physics step, to tell a snap turn or teleport.
        private bool hasArea;
        private Matrix4x4 lastArea;
        private Quaternion lastAreaTurn;
        private Vector3 lastAreaPosition;

        private enum Move { None, Jab, Swing, Uppercut }

        /// <summary>
        /// The flying punches the Fist Size level-up stat has unlocked (Player Level sets them from the points spent).
        /// Locked, a punch doesn't fly: a fast straight one, a sideways sweep or an upward drive is just a punch. Locked
        /// again each time the game starts, until the save is read.
        /// </summary>
        public static bool JabUnlocked { get; set; }
        public static bool SwingUnlocked { get; set; }
        public static bool UppercutUnlocked { get; set; }
        /// <summary>Full Fist Size: every flying punch throws three fists.</summary>
        public static bool TriplePunch { get; set; }

        /// <summary>A punch that landed (the fist itself, or a flying one), for whoever counts punches.</summary>
        public struct PunchLanded
        {
            /// <summary>What it hit.</summary>
            public Collider Hit;
            /// <summary>
            /// The stroke it came from: one punch can throw three flying fists, or bounce the fist twice. 0 = no stroke
            /// (the fist just bumped into it fast enough).
            /// </summary>
            public int Stroke;
            /// <summary>A jab at least Perfect Jab good.</summary>
            public bool Perfect;
        }

        /// <summary>Raised for every punch that lands, e.g. for the bonus round's board.</summary>
        public static event System.Action<PunchLanded> Landed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetUnlocks()
        {
            JabUnlocked = false;
            SwingUnlocked = false;
            UppercutUnlocked = false;
            TriplePunch = false;
            Landed = null;
        }

        // Each move is thrown only when it's switched on here and unlocked.
        private bool CanJab => flyingJab && JabUnlocked;
        private bool CanSwing => swing && SwingUnlocked;
        private bool CanUppercut => uppercut && UppercutUnlocked;

        // The punch the hand is throwing right now (or has just thrown), for when the fist itself lands.
        private Move CurrentMove => stroking || Time.time < strokeMoveUntil ? strokeMove : Move.None;

        /// <summary>Whether the hand is a fist right now (it punches).</summary>
        public bool IsFist => closed;

        /// <summary>Where the fist's knuckles are (the middle of its punching ball).</summary>
        public Vector3 Knuckles => FistPoint();

        /// <summary>How big the fist's punching ball is right now (its radius, in metres).</summary>
        public float BallSize => radius * grown;

        /// <summary>How fast the hand itself is moving, in the play area's space (walking and turning left out).</summary>
        public Vector3 HandVelocity => handVelocity;

        /// <summary>A rumble in this hand, e.g. for a fist bump (not damage: silent while Hit Buzz's Only Damage is on).</summary>
        public void Buzz(float strength, float seconds)
        {
            HitBuzz.Other(rumbler, strength, seconds);
        }

        /// <summary>The hand (its interactor, which buzzes) behind this fist ball or flying fist, or null.</summary>
        public static XRBaseInputInteractor HandOf(Component thing)
        {
            return thing != null && thing.TryGetComponent<Punch>(out var punch) && punch.owner != null ? punch.owner.rumbler : null;
        }

        // Power (it used to grow with the old Power bonus stat, which is Weapon Master now: held weapons only).
        private float Power => power;

        /// <summary>Whether this is a fist's punching ball (it has a Weapon Power like a weapon, but it's a hand).</summary>
        public static bool IsFistBall(Component thing)
        {
            return thing != null && thing.TryGetComponent<Punch>(out _);
        }

        /// <summary>
        /// How far a good flying jab flies on from the knuckles right now, in metres (Fist Size and the buffs included; the
        /// first jab's reach before jabs are unlocked). A punch knocks an enemy back a share of this, so the jab after it
        /// still reaches it: the combo.
        /// </summary>
        public static float JabReach
        {
            get
            {
                var fist = fists.Count > 0 ? fists[0] : null;
                if (fist == null) return 0.75f;
                return Mathf.Max(fist.jabReach.x, fist.jabReach.y) *
                       Mathf.Lerp(1f, fist.maxReachScale, Game.Leveling.PlayerLevel.PunchReachGrowth) + Game.Leveling.PlayerBuffs.FlyReach;
            }
        }

        /// <summary>Whether this is a flying punch (a jab carried on ahead of the fist), not the fist's own ball.</summary>
        public static bool IsFlyingFist(Component thing)
        {
            return thing != null && thing.TryGetComponent<FlyingFist>(out _);
        }

        /// <summary>The hand's model (its skin), once the fist has found it.</summary>
        public SkinnedMeshRenderer HandSkin => handSkin;

        /// <summary>Adds what looks like this hand: its model and its flying punches' echoes (Hot Hands makes them glow).</summary>
        public void CollectHandRenderers(List<Renderer> into)
        {
            if (handSkin != null) into.Add(handSkin);
            foreach (var flyer in flyers)
            {
                if (flyer != null && flyer.echoLook != null) into.Add(flyer.echoLook);
            }
        }

        private void Awake()
        {
            var hand = GetComponentInChildren<AnimateHandOnInput>(true);
            gripAction = Pick(grip, hand != null ? hand.gripAnimationAction : default);
            triggerAction = Pick(trigger, hand != null ? hand.pinchAnimationAction : default);
            if (gripAction == null) Debug.LogWarning($"[Fist] '{name}' has no grip input, so it can't make a fist.", this);
            if (hand != null)
            {
                handModel = hand.transform;
                handScale = handModel.localScale;
                handPosition = handModel.localPosition;
                handSkin = handModel.GetComponentInChildren<SkinnedMeshRenderer>(true);
            }

            GetComponentsInChildren(true, interactors);
            GetComponentsInChildren(true, nearFars);
            rumbler = GetComponentInChildren<XRBaseInputInteractor>(true);
            FindKnuckles();
            MakeBall();
            // Ready at load, so the first good punch doesn't stutter.
            Game.EffectPool.Warm(goodPunchEffect);
            // The Elon Boong's explosions too, with enough copies for a hand blasting as often as it may (Boong Blast
            // Every) for as long as a copy stays busy, so the first Elon fight doesn't make them mid-punch. The other
            // hand and the bottles share them, so nothing more is made for those.
            boongEffects = HasAny(boongBlastEffects) ? boongBlastEffects : new[] { goodPunchEffect };
            int copies = Mathf.Clamp(Mathf.CeilToInt(EffectBusySeconds / Mathf.Max(boongBlastEvery, 0.01f)), 1, MaxBoongBlastCopies);
            foreach (var effect in boongEffects) Game.EffectPool.Warm(effect, copies);
        }

        private static bool HasAny(GameObject[] things)
        {
            if (things == null) return false;
            foreach (var thing in things)
            {
                if (thing != null) return true;
            }
            return false;
        }

        private void OnEnable()
        {
            fists.Add(this);
            foreach (var nearFar in nearFars) nearFar.selectFilters.Add(this);
            if (body != null) body.gameObject.SetActive(true);
        }

        private void OnDisable()
        {
            fists.Remove(this);
            foreach (var nearFar in nearFars)
            {
                if (nearFar != null) nearFar.selectFilters.Remove(this);
            }
            Open();
            if (body != null) body.gameObject.SetActive(false);
            foreach (var flyer in flyers)
            {
                if (flyer != null) flyer.gameObject.SetActive(false);
            }
            grown = 1f;
            SetHandSize(1f);
            stroking = false;
            strokeMove = Move.None;
            hasLastHand = false;
        }

        private void OnDestroy()
        {
            if (body != null) Destroy(body.gameObject);
            if (punchSpeaker != null) Destroy(punchSpeaker.gameObject);
            foreach (var flyer in flyers)
            {
                if (flyer != null) Destroy(flyer.gameObject);
            }
        }

        /// <summary>
        /// The hand is squeezed into a fist right now (grip, and the trigger if Need Trigger), holding nothing. Unlike
        /// Is Fist it's read from the buttons at once, so it also works while the game is paused (a notice up).
        /// </summary>
        public bool Squeezing => Read(gripAction) >= squeeze && (!needTrigger || triggerAction == null || Read(triggerAction) >= squeeze) && !Holding();

        private void Update()
        {
            bool squeezing = Read(gripAction) >= squeeze && (!needTrigger || triggerAction == null || Read(triggerAction) >= squeeze);
            if (!squeezing || Holding())
            {
                squeezedSince = -1f;
                Open();
            }
            else
            {
                if (squeezedSince < 0f) squeezedSince = Time.time;
                if (!closed && Time.time - squeezedSince >= formSeconds) Close();
            }
            Grow();
            WatchPunches();
        }

        private void FixedUpdate()
        {
            if (closed)
            {
                ball.radius = radius * grown;
                // Only the hand's own movement hits: walking, turning or being carried along with a still fist adds
                // to how fast the ball goes, but not to the punch.
                float ballSpeed = body.linearVelocity.magnitude;
                weapon.Power = ballSpeed > 0.01f ? Power * Mathf.Clamp01(handVelocity.magnitude / ballSpeed) : Power;
                // A jab lands as hard as a flying jab; a swing and an uppercut land harder.
                weapon.Power *= LandedPower(CurrentMove);
            }
            // The Elon Boong buff: punches do double damage.
            weapon.boost = Game.Leveling.PlayerBuffs.PunchDamage;
            CarryOnRigJump();
            // ...and the ball reaches out past the knuckles, along the hand (0 without the buff), but only at Reach Out
            // Speed on top of the hand's own movement: closing a fist or turning the hand doesn't throw it out.
            var hand = FistPoint();
            if (closed)
                reachOut = Vector3.MoveTowards(reachOut, transform.forward * Game.Leveling.PlayerBuffs.PunchReach, ReachOutSpeed * Time.fixedDeltaTime);
            var target = hand + reachOut;
            var gap = target - body.position;
            var targetStep = target - lastTarget;
            lastTarget = target;
            float snap = SnapDistance + reachOut.magnitude;
            if (!closed || gap.sqrMagnitude > snap * snap || targetStep.sqrMagnitude > SnapDistance * SnapDistance)
            {
                // Back to the hand, never straight out to the reach, so it can't jump through a wall: it goes out by
                // physics from there, slowly again.
                body.position = hand;
                body.linearVelocity = Vector3.zero;
                reachOut = Vector3.zero;
                lastTarget = hand;
                return;
            }
            var velocity = Vector3.ClampMagnitude(gap / Time.fixedDeltaTime, MaxSpeed);
            bool reaching = reachOut != Vector3.zero;
            // Reaching out, the ball never moves faster than the point it follows plus Reach Out Speed, so after being
            // held back (by a wall, by something it rests on) it doesn't shoot out to catch up.
            if (reaching) velocity = Vector3.ClampMagnitude(velocity, targetStep.magnitude / Time.fixedDeltaTime + ReachOutSpeed);
            body.linearVelocity = velocity;
            // Reaching out (just closed, or the hand turning) the ball moves faster than the hand does: only the hand's
            // own speed hits, measured against the speed it's about to move at.
            if (reaching)
            {
                float newSpeed = body.linearVelocity.magnitude;
                if (newSpeed > 0.01f)
                    weapon.Power = Mathf.Min(weapon.Power, Power * Mathf.Clamp01(handVelocity.magnitude / newSpeed) * LandedPower(CurrentMove));
            }
        }

        // A snap turn or a teleport moves the whole play area at once (a hand near the face can jump less than Snap
        // Distance): the ball jumps along with it, so it doesn't sweep through the air to catch up and hit whatever is
        // on the way. Walking and smooth turning move the play area a little each step and are left to the physics.
        private void CarryOnRigJump()
        {
            var area = transform.parent;
            if (area == null) return;
            var now = area.localToWorldMatrix;
            var turn = area.rotation;
            if (hasArea && (Quaternion.Angle(turn, lastAreaTurn) > RigJumpDegrees ||
                            (area.position - lastAreaPosition).sqrMagnitude > RigJumpDistance * RigJumpDistance))
            {
                var carry = now * lastArea.inverse;
                body.position = carry.MultiplyPoint3x4(body.position);
                lastTarget = carry.MultiplyPoint3x4(lastTarget);
                reachOut = carry.MultiplyVector(reachOut);
            }
            lastArea = now;
            lastAreaTurn = turn;
            lastAreaPosition = area.position;
            hasArea = true;
        }

        // Called by the ball (or the flying fist) when it hits something: what the punch does besides the damage
        // enemies take from the hit itself, and a buzz if it hurt something (Hit Buzz).
        private void OnPunch(Collision collision, FlyingFist flyer, Rigidbody hitter)
        {
            bool flying = flyer != null;
            var move = flying ? flyer.Thrown : CurrentMove;
            // The fist counts only as fast as the hand itself moves (in the play area), so walking into things with a
            // fist held out isn't a punch.
            float punchSpeed = flying ? flyer.HitSpeed : Mathf.Min(collision.relativeVelocity.magnitude, handVelocity.magnitude);
            if (!flying)
            {
                if (punchSpeed < MinPunchSpeed) return;
                // The fist itself landed: that punch already hit, so it doesn't fly on too.
                lastBallHit = Time.time;
                // A jab lands as hard as a flying jab, a swing and an uppercut harder (a flying one already has this in
                // its speed).
                punchSpeed *= LandedPower(move);
            }
            PlayPunchHit(collision, punchSpeed);
            bool stroke = flying || stroking || Time.time < strokeMoveUntil;
            Landed?.Invoke(new PunchLanded
            {
                Hit = collision.collider,
                Stroke = flying ? flyer.Stroke : stroke ? strokeId : 0,
                Perfect = flying ? flyer.Perfect : stroke && strokeJab >= perfectJab,
            });
            HelpPlayMaker(collision, punchSpeed);
            Push(collision, punchSpeed, flyer, move);
            float puff = 1f;
            if (move == Move.Uppercut)
            {
                // Enemies walk by themselves and can't be pushed by physics: an uppercut knocks them back, hopping high,
                // and on the head it knocks them out.
                var enemy = collision.collider.GetComponentInParent<EnemyHitDamage>();
                // Furniture just takes the punch (and is pushed above); knock-outs are for enemies.
                if (enemy != null && !enemy.IsFurniture)
                {
                    var point = collision.contactCount > 0 ? collision.GetContact(0).point : collision.transform.position;
                    if (enemy.IsHead(point))
                    {
                        // A knock-out that hurt: the strongest buzz.
                        if (enemy.KnockOut(punchSpeed * Power * headPower, 1.5f, uppercutPush * 1.5f, knockOutSeconds, WeaponPower.DamageOf(hitter), point))
                        {
                            HitBuzz.Buzz(rumbler, 6f, true);
                            // A knock-out that killed it: the kill thump just after.
                            if (enemy.IsDead && enemy.CountsAsKill) HitBuzz.Killed(rumbler, 6f);
                        }
                        puff = 2f;
                    }
                    else
                    {
                        enemy.KnockBack(1f, uppercutPush);
                    }
                }
            }
            // The Elon Boong's explosion has its own smoke, so the good punch's puff isn't shown on top.
            bool blasted = BoongBlast(collision, hitter);
            if (!blasted && (flying || move != Move.None || punchSpeed >= jabSpeed)) ShowGoodPunch(collision, puff);
            // The hand buzzes only if the punch hurt something, as hard as it hurt (Hit Buzz): enemies and furniture with
            // Enemy Hit Damage report it themselves; PlayMaker breakables are watched for a moment.
            HitBuzz.Watch(collision, hitter);
        }

        // A thump where the punch landed (Punch Hit Sound), louder the faster it was; not when the two fists meet (the
        // Fist Bump has its own).
        private void PlayPunchHit(Collision collision, float punchSpeed)
        {
            if (punchHitSound == null || Time.time < nextPunchSound || IsFistBall(collision.rigidbody)) return;
            nextPunchSound = Time.time + PunchSoundEvery;
            if (punchSpeaker == null)
            {
                var speaker = new GameObject($"{name} Punch Hit Sound");
                punchSpeaker = speaker.AddComponent<AudioSource>();
                punchSpeaker.playOnAwake = false;
                punchSpeaker.spatialBlend = 1f;
            }
            punchSpeaker.transform.position = collision.contactCount > 0 ? collision.GetContact(0).point : collision.transform.position;
            punchSpeaker.pitch = Random.Range(punchHitPitch.x, punchHitPitch.y);
            float loud = Mathf.InverseLerp(MinPunchSpeed, jabSpeed, punchSpeed);
            punchSpeaker.PlayOneShot(punchHitSound, Mathf.Lerp(punchHitVolume.x, punchHitVolume.y, loud));
        }

        // PlayMaker breakables measure a hit by its speed alone, set for swung weapons, so a punch only ever shook
        // them. A punch counts as Pumpkin Force times faster: the force is put in and the check done again (Play Maker
        // Hits; also when the FSM heard the collision first and turned it down by its speed).
        private void HelpPlayMaker(Collision collision, float punchSpeed)
        {
            if (pumpkinForce <= 1f) return;
            var hit = collision.rigidbody != null ? collision.rigidbody.gameObject : collision.gameObject;
            // Enemy Hit Damage already counts the punch (by its speed and the fist's power): not twice.
            if (hit.TryGetComponent<EnemyHitDamage>(out _)) return;
            // The Elon Boong buff's double damage counts here too.
            PlayMakerHits.Judge(hit, collision, punchSpeed * pumpkinForce * Game.Leveling.PlayerBuffs.PunchDamage);
        }

        // Sends a loose thing off the way the punch was going, as a throw would: the fist's ball alone is light and
        // only nudges heavy things. An uppercut sends it harder, up and away. The heavier the thing, the less it gives
        // way (Push Weight). Things held in a hand, and things that don't move by physics, are left alone.
        private void Push(Collision collision, float punchSpeed, FlyingFist flyer, Move move)
        {
            var thing = collision.rigidbody;
            if (punchPush <= 0f || thing == null || thing.isKinematic || punchSpeed < MinPushSpeed) return;
            float share = PushWeight.Share(thing);
            if (share <= 0f) return;
            if (thing.TryGetComponent(out XRGrabInteractable grab) && grab.isSelected) return;

            Vector3 direction;
            if (flyer != null) direction = flyer.Direction;
            else if (handVelocity.sqrMagnitude > 0.25f)
                direction = (transform.parent != null ? transform.parent.TransformDirection(handVelocity) : handVelocity).normalized;
            else if (collision.contactCount > 0) direction = -collision.GetContact(0).normal;
            else return;

            float push = punchPush;
            if (move == Move.Uppercut)
            {
                push *= uppercutPush;
                direction = (direction * 0.5f + Vector3.up).normalized;
            }
            // Up to that speed along the punch, not on top of what the bump already gave it.
            float wanted = Mathf.Min(punchSpeed * push, MaxPushSpeed) * share;
            float has = Vector3.Dot(thing.linearVelocity, direction);
            if (has >= wanted) return;
            var at = collision.contactCount > 0 ? collision.GetContact(0).point : thing.worldCenterOfMass;
            thing.AddForceAtPosition(direction * (wanted - has), Vector3.Lerp(at, thing.worldCenterOfMass, 0.7f), ForceMode.VelocityChange);
        }

        // The Elon Boong buff, and the Explosive Fist bonus stat: a punch (the fist's own or a flying one) that hits anything
        // explodes once more where it hit, a small blast that hurts and pushes what's around it but never the player. Each
        // Explosive Fist point is one Boong Blast Damage (the buff alone: one). Once per Boong Blast Every per hand, as a hit
        // can be reported several times in one step. True if it went off.
        private bool BoongBlast(Collision collision, Rigidbody hitter)
        {
            int power = Mathf.Max(Game.Leveling.PlayerBuffs.ExplodingPunches ? 1 : 0, Game.Leveling.PlayerLevel.ExplosiveFist);
            if (power <= 0 || Time.time < nextBoongBlast) return false;
            nextBoongBlast = Time.time + boongBlastEvery;
            var at = collision.contactCount > 0 ? collision.GetContact(0).point : collision.transform.position;
            Game.Weapons.Blast.Explode(at, boongBlastRadius, HotHands.Boost(boongBlastDamage * power), boongBlastPush, hitter, boongEffects,
                boongBlastSound, boongBlastVolume, null, false, boongBlastEffectSize);
            return true;
        }

        // A puff where a good punch landed (bigger for a knock-out); the copy is destroyed after it has played. Not on
        // enemies: they show their own small pop where a hit hurts them (Enemy Hit Damage), and nothing for one that didn't.
        // Ones with Hide Hit Effect ticked (room doors, the start pumpkin) still get the puff.
        private void ShowGoodPunch(Collision collision, float scale)
        {
            if (goodPunchEffect == null || Time.time < nextPuff) return;
            var enemy = collision.collider != null ? collision.collider.GetComponentInParent<EnemyHitDamage>() : null;
            if (enemy != null && enemy.ShowsOwnHitPop) return;
            // A chase pumpkin shows its own block instead (Touch Only Damage).
            if (collision.collider != null && collision.collider.GetComponentInParent<TouchOnlyDamage>() != null) return;
            nextPuff = Time.time + 0.15f;
            var at = collision.contactCount > 0 ? collision.GetContact(0).point : collision.transform.position;
            Game.EffectPool.Play(goodPunchEffect, at, goodPunchEffectSize * scale);
        }

        private void Close()
        {
            closed = true;
            // Power and size are read each time, so they can be changed while playing.
            weapon.Power = Power;
            weapon.boost = Game.Leveling.PlayerBuffs.PunchDamage;
            ball.radius = radius * grown;
            // Starts right at the hand, so it doesn't sweep through whatever is between (with the Elon Boong it then
            // reaches out slowly).
            var hand = FistPoint();
            body.position = hand;
            body.linearVelocity = Vector3.zero;
            reachOut = Vector3.zero;
            lastTarget = hand;
            ball.enabled = true;
            if (formRumble > 0f) HitBuzz.Other(rumbler, formRumble, 0.05f);
        }

        // The hand swells into a big fist while it's closed, and back after. Fist Size makes it a little bigger (up to
        // Max Hand Growth); the flying punches grow much more.
        private void Grow()
        {
            float target = closed ? fistScale + Mathf.Min(Game.Leveling.PlayerLevel.FistSizeBonus, maxHandGrowth) : 1f;
            if (handModel == null || grown == target) return;
            float rate = growSeconds > 0f ? Mathf.Max(fistScale - 1f, 1f) / growSeconds : float.MaxValue;
            grown = Mathf.MoveTowards(grown, target, rate * Time.deltaTime);
            SetHandSize(grown);
        }

        // Sizes the hand model around Grow Around, so it stays on the player's real hand.
        private void SetHandSize(float size)
        {
            if (handModel == null) return;
            var around = transform.TransformPoint(growAround);
            if (handModel.parent != null) around = handModel.parent.InverseTransformPoint(around);
            handModel.localScale = handScale * size;
            handModel.localPosition = around + (handPosition - around) * size;
        }

        private void Open()
        {
            if (!closed) return;
            closed = false;
            if (ball != null) ball.enabled = false;
        }

        // Watches the hand for punches. A punch is one stroke: the hand gets going, then stops short (the snap) or
        // comes back. Positions are in the play area's space (the controller's parent), so walking and turning don't
        // count as punching.
        private void WatchPunches()
        {
            var eyes = Camera.main;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            var hand = InPlayArea(transform.position);
            if (!hasLastHand)
            {
                lastHand = hand;
                hasLastHand = true;
                return;
            }
            var step = hand - lastHand;
            lastHand = hand;
            // Smoothed a little: tracking jitters from frame to frame.
            handVelocity = Vector3.Lerp(handVelocity, step / dt, 0.6f);
            float speed = handVelocity.magnitude;
            // Watched also with no punch unlocked to fly: a perfect jab counts anyway (see Landed).
            if (eyes == null)
            {
                stroking = false;
                strokeMove = Move.None;
                return;
            }

            if (!stroking)
            {
                if (speed < StrokeSpeed)
                {
                    followingThrough = false;
                    return;
                }
                if (followingThrough && Vector3.Dot(handVelocity, lastStrokeDirection) > speed * 0.5f) return;
                stroking = true;
                followingThrough = false;
                strokeMove = Move.None;
                strokeJab = -1f;
                strokeId = ++strokeCount;
                strokeStart = hand - step;
                strokeStartTime = Time.time - dt;
                strokePath = step.magnitude;
                strokePeak = speed;
                return;
            }

            strokePath += step.magnitude;
            strokePeak = Mathf.Max(strokePeak, speed);
            var travel = hand - strokeStart;
            float took = Time.time - strokeStartTime;
            strokeMove = WhichMove(travel, took, eyes.transform, out strokeQuality);
            bool snapped = speed <= strokePeak * SnapShare || Vector3.Dot(handVelocity, travel) <= 0f;
            // Too long for a punch: a push, or the hand just moving about.
            if (!snapped && took <= Mathf.Max(jabSeconds, LongMoveSeconds)) return;

            stroking = false;
            followingThrough = true;
            lastStrokeDirection = travel.sqrMagnitude > 0.000001f ? travel.normalized : Vector3.zero;
            // The fist landing just after still counts as that punch.
            strokeMoveUntil = Time.time + 0.1f;
            if (snapped && strokeMove != Move.None) TryFly(strokeMove, strokeQuality);
        }

        // Which punch a stroke is, by where the hand went from the player's point of view (ahead where they look: a jab;
        // sideways: a swing; upwards: an uppercut; the closest fit wins), how fast and how straight; and how good it
        // was, from 0 (just good enough) to 1.
        private Move WhichMove(Vector3 travel, float took, Transform eyes, out float quality)
        {
            quality = 0f;
            float distance = travel.magnitude;
            if (distance < MoveTravel) return Move.None;
            var direction = travel / distance;
            float straightness = distance / Mathf.Max(strokePath, 0.0001f);

            var move = Move.None;
            // How far off its line the punch may be at most, as a share of what its kind allows.
            float off = 1f;
            float needed = 0f;
            strokeJab = -1f;
            if (strokePeak >= jabSpeed && took <= jabSeconds && straightness >= jabStraightness)
            {
                float jabOff = Vector3.Angle(direction, InPlayAreaDirection(eyes.forward)) / jabAim;
                if (jabOff <= 1f) strokeJab = Grade(jabSpeed, jabOff) * StraightGrade(straightness);
                if (CanJab && jabOff <= off)
                {
                    move = Move.Jab;
                    off = jabOff;
                    needed = jabSpeed;
                }
            }
            if (CanUppercut && strokePeak >= uppercutSpeed && took <= LongMoveSeconds)
            {
                float upOff = Vector3.Angle(direction, Vector3.up) / UppercutAngle;
                if (upOff < off)
                {
                    move = Move.Uppercut;
                    off = upOff;
                    needed = uppercutSpeed;
                }
            }
            if (CanSwing && strokePeak >= swingSpeed && took <= LongMoveSeconds)
            {
                var side = InPlayAreaDirection(eyes.right);
                float sideOff = Mathf.Min(Vector3.Angle(direction, side), Vector3.Angle(direction, -side)) / SwingAngle;
                if (sideOff < off)
                {
                    move = Move.Swing;
                    off = sideOff;
                    needed = swingSpeed;
                }
            }
            if (move == Move.None) return move;

            quality = Grade(needed, off);
            if (move == Move.Jab) quality *= StraightGrade(straightness);
            return move;
        }

        // How good a stroke is for a move that needs this speed, this far off its line (as a share of what it allows):
        // as much faster than needed as Top Jab Speed is than Jab Speed is the best.
        private float Grade(float needed, float off)
        {
            float top = needed * Mathf.Max(topJabSpeed, jabSpeed + 0.1f) / jabSpeed;
            return Mathf.InverseLerp(needed, top, strokePeak) * Mathf.Lerp(0.6f, 1f, 1f - off);
        }

        // A jab's straightness counts too.
        private float StraightGrade(float straightness) => Mathf.Lerp(0.6f, 1f, Mathf.InverseLerp(jabStraightness, 1f, straightness));

        // A good punch that hit nothing flies on from the knuckles, the way the fist went: a jab straight ahead, a swing
        // sideways (level, the way it swept), an uppercut up. The better it was, the farther; the more Fist Size, the
        // farther and bigger. At full Fist Size it throws three.
        private void TryFly(Move move, float quality)
        {
            if (!closed || Time.time < nextFly || lastBallHit >= strokeStartTime) return;

            var direction = transform.parent != null ? transform.parent.TransformDirection(lastStrokeDirection) : lastStrokeDirection;
            // A swing sweeps across, not into the floor or the sky.
            if (move == Move.Swing) direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;
            direction.Normalize();
            nextFly = Time.time + FlyCooldownNow;

            float reach = Mathf.Lerp(jabReach.x, jabReach.y, quality) * Mathf.Lerp(1f, maxReachScale, Game.Leveling.PlayerLevel.PunchReachGrowth);
            // Sized from the fist at Fist Scale, so it grows with Fist Size apart from the hand's own little growth.
            float flyRadius = radius * fistScale * FlySize(move);
            // It hits as hard as the fist would at the punch's speed (x Jab Power; a swing and an uppercut harder still).
            float hitSpeed = strokePeak * jabPower * MovePower(move);
            // Its back at the knuckles, so a big one starts out in front of the hand, not around the player's face.
            var from = FistPoint() + direction * Mathf.Max(0f, flyRadius - radius * grown);
            Throw(from, direction, reach, hitSpeed, move, flyRadius);

            if (TriplePunch)
            {
                // A fist's width apart, so they don't overlap.
                float gap = flyRadius * 2.2f;
                if (move == Move.Jab)
                {
                    // One more on each side, fanning out a little.
                    var side = Vector3.Cross(Vector3.up, direction);
                    if (side.sqrMagnitude < 0.01f) side = transform.right;
                    side.Normalize();
                    for (int s = -1; s <= 1; s += 2)
                        Throw(from + side * (s * gap), Quaternion.AngleAxis(s * tripleJabSpread, Vector3.up) * direction, reach, hitSpeed, move, flyRadius);
                }
                else
                {
                    // Two more in a row along the punch, each starting farther out and reaching farther.
                    for (int k = 1; k <= 2; k++)
                        Throw(from + direction * (k * gap), direction, reach * (1f + k * tripleReach), hitSpeed, move, flyRadius);
                }
            }

            HitBuzz.Other(rumbler, move == Move.Jab ? 0.5f : 0.7f, TriplePunch ? 0.1f : 0.06f);
            if (jabSound != null) AudioSource.PlayClipAtPoint(jabSound, from);
        }

        // How much harder a move hits than a plain punch: a swing and an uppercut more.
        private float MovePower(Move move)
        {
            return move == Move.Swing ? swingPower : move == Move.Uppercut ? uppercutPower : 1f;
        }

        // How much harder the fist itself lands, by the move: a jab as hard as a flying jab (Jab Power), a swing and an
        // uppercut as Move Power says.
        private float LandedPower(Move move) => move == Move.Jab ? jabPower : MovePower(move);

        // How big a flying punch is, compared with the fist at Fist Scale: Fly Size, bigger with each Fist Size point (up
        // to Max Fly Size); a swing and an uppercut bigger than a jab.
        private float FlySize(Move move)
        {
            float size = Mathf.Min(flySize * (1f + Game.Leveling.PlayerLevel.PunchSizeBonus), maxFlySize);
            return move == Move.Jab ? size : size * bigMoveSize;
        }

        // Sends a flying fist: one that has stopped, or a new one (or, with too many out, the one nearest the end of its
        // flight again).
        private void Throw(Vector3 from, Vector3 direction, float reach, float hitSpeed, Move move, float flyRadius)
        {
            FlyingFist flyer = null;
            foreach (var free in flyers)
            {
                if (free != null && !free.gameObject.activeSelf)
                {
                    flyer = free;
                    break;
                }
            }
            if (flyer == null)
            {
                flyers.RemoveAll(gone => gone == null);
                if (flyers.Count < MaxFlyers()) flyers.Add(flyer = MakeFlyer());
                else
                {
                    foreach (var busy in flyers)
                    {
                        if (flyer == null || busy.LeftToFly < flyer.LeftToFly) flyer = busy;
                    }
                }
            }
            // The Elon and Green Boong buffs: every fist flies farther by the same metres (not more for a Triple Punch's
            // farther ones).
            flyer.Launch(from, direction, reach + Game.Leveling.PlayerBuffs.FlyReach, hitSpeed, move, flyRadius);
            flyer.Stroke = strokeId;
            flyer.Perfect = strokeJab >= perfectJab;
        }

        // Most flying fists one hand may have out at once: as many as it can throw (a punch every Fly Cooldown, three
        // with Triple Punch) while the first of them still flies its farthest (Fist Size and the buffs included) and
        // shrinks away, so none is taken back mid-flight. They're only made when needed.
        private int MaxFlyers()
        {
            float farthest = Mathf.Max(jabReach.x, jabReach.y) * Mathf.Lerp(1f, maxReachScale, Game.Leveling.PlayerLevel.PunchReachGrowth);
            if (TriplePunch) farthest *= 1f + 2f * tripleReach;
            farthest += Game.Leveling.PlayerBuffs.FlyReach;
            // A physics step or two more, as it stops and fades step by step.
            float seconds = farthest / FlySpeed + FlyingFist.FadeSeconds + 2f * Time.fixedDeltaTime;
            return Mathf.Max(MinFlyers, (TriplePunch ? 3 : 1) * Mathf.CeilToInt(seconds / FlyCooldownNow));
        }

        // The time between two flying punches from this hand now: Fly Cooldown, shorter with each Explosive Fist point.
        private float FlyCooldownNow =>
            Mathf.Max(MinFlyCooldown, FlyCooldown * (1f - explosiveFistFaster * Game.Leveling.PlayerLevel.ExplosiveFist));

        private Vector3 InPlayArea(Vector3 point)
        {
            return transform.parent != null ? transform.parent.InverseTransformPoint(point) : point;
        }

        private Vector3 InPlayAreaDirection(Vector3 direction)
        {
            return transform.parent != null ? transform.parent.InverseTransformDirection(direction) : direction;
        }

        // The flying fist: a ball like the fist's, with a still copy of the hand riding on it.
        private FlyingFist MakeFlyer()
        {
            var go = new GameObject($"Flying Fist ({name})");
            go.SetActive(false);
            go.tag = "Weapon";
            var flyingBody = go.AddComponent<Rigidbody>();
            flyingBody.mass = 3f;
            flyingBody.useGravity = false;
            flyingBody.linearDamping = 0f;
            flyingBody.angularDamping = 0f;
            flyingBody.freezeRotation = true;
            flyingBody.interpolation = RigidbodyInterpolation.Interpolate;
            flyingBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            go.AddComponent<SphereCollider>();
            go.AddComponent<WeaponPower>();
            var punch = go.AddComponent<Punch>();
            punch.owner = this;

            var flying = go.AddComponent<FlyingFist>();
            flying.owner = this;
            punch.flyer = flying;
            if (handSkin != null)
            {
                // Echo: sized around the knuckles (the ball's middle); Echo Mesh: the copy of the hand inside it.
                var echo = new GameObject("Echo").transform;
                echo.SetParent(go.transform, false);
                var echoObject = new GameObject("Echo Mesh");
                echoObject.layer = handSkin.gameObject.layer;
                echoObject.transform.SetParent(echo, false);
                flying.echoMesh = new Mesh { name = "Fist Echo" };
                flying.echoMesh.MarkDynamic();
                echoObject.AddComponent<MeshFilter>().sharedMesh = flying.echoMesh;
                var look = echoObject.AddComponent<MeshRenderer>();
                look.sharedMaterials = handSkin.sharedMaterials;
                look.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                look.receiveShadows = false;
                flying.echo = echo;
                flying.echoShape = echoObject.transform;
                flying.echoLook = look;
            }
            if (wristSmoke != null)
            {
                // Its own copy of the effect, simulated in its own space so it rides along at the wrist (no trail),
                // played at each throw (Launch).
                var smoke = Instantiate(wristSmoke, go.transform, false);
                smoke.name = "Wrist Smoke";
                flying.smoke = smoke.transform;
                flying.smokeSystems = smoke.GetComponentsInChildren<ParticleSystem>(true);
                // Its star flash: a part called "Star" (Flying_Punch_Shockwave), or the system on the effect's own root
                // when the puffs are systems under it (Smoke_Blast).
                var star = smoke.transform.Find("Star");
                if (star != null) flying.smokeStar = star.GetComponent<ParticleSystemRenderer>();
                else if (flying.smokeSystems.Length > 1) flying.smokeStar = smoke.GetComponent<ParticleSystemRenderer>();
                flying.smokeColours = new ParticleSystem.MinMaxGradient[flying.smokeSystems.Length];
                for (int i = 0; i < flying.smokeSystems.Length; i++)
                {
                    var main = flying.smokeSystems[i].main;
                    main.simulationSpace = ParticleSystemSimulationSpace.Local;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                    main.playOnAwake = false;
                    flying.smokeColours[i] = main.startColor;
                    flying.smokeSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
            // It ignores the player's body each time it's thrown (Launch), once it's switched on.
            return flying;
        }

        // Fist balls and flying fists don't bump into the player's own body, or into each other.
        private void IgnorePlayer(Collider collider)
        {
            if (playerParts.Count == 0)
            {
                var rig = GetComponentInParent<XROrigin>(true);
                if (rig == null) rig = FindAnyObjectByType<XROrigin>();
                if (rig != null) rig.GetComponentsInChildren(true, playerParts);
            }
            foreach (var part in playerParts) Ignore(collider, part);
            foreach (var other in fists)
            {
                Ignore(collider, other.ball);
                foreach (var flyer in other.flyers)
                {
                    if (flyer != null) Ignore(collider, flyer.Ball);
                }
            }
        }

        private static void Ignore(Collider a, Collider b)
        {
            if (a != null && b != null && a != b) Physics.IgnoreCollision(a, b);
        }

        public bool canProcess => isActiveAndEnabled;

        // Asked by the hand's Near-Far Interactor before it grabs something: with the trigger squeezed (a fist), only
        // things within reach.
        public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {
            if (noFarGrabTrigger <= 0f || interactor is not NearFarInteractor || Read(triggerAction) < noFarGrabTrigger) return true;
            return GentleFarGrab.HandDistance(interactor.transform, interactable) <= Reach;
        }

        // Anything in this hand (a weapon, a lollipop, ...) means it isn't a fist.
        private bool Holding()
        {
            foreach (var interactor in interactors)
            {
                if (interactor != null && interactor.hasSelection) return true;
            }
            return false;
        }

        // The knuckles of the closed hand: between the palm and the first joints of the index and middle fingers.
        private Vector3 FistPoint()
        {
            Vector3 point;
            if (knuckleA != null && knuckleB != null)
            {
                var knuckles = (knuckleA.position + knuckleB.position) * 0.5f;
                point = palm != null ? Vector3.Lerp(palm.position, knuckles, 0.75f) : knuckles;
            }
            else
            {
                point = transform.TransformPoint(FallbackPoint);
            }
            return point + transform.TransformVector(offset);
        }

        private void FindKnuckles()
        {
            foreach (var bone in GetComponentsInChildren<Transform>(true))
            {
                string boneName = bone.name;
                if (knuckleA == null && boneName.StartsWith("f_index.01")) knuckleA = bone;
                else if (knuckleB == null && boneName.StartsWith("f_middle.01")) knuckleB = bone;
                else if (palm == null && boneName.StartsWith("palm.02")) palm = bone;
            }
        }

        // The ball lives outside the rig (a body that physics moves can't ride along inside a moving parent), and
        // doesn't bump into the player's own body or the other fist.
        private void MakeBall()
        {
            var go = new GameObject($"Fist ({name})");
            go.tag = "Weapon";
            body = go.AddComponent<Rigidbody>();
            body.mass = 3f;
            body.useGravity = false;
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.position = FistPoint();
            go.transform.position = body.position;

            ball = go.AddComponent<SphereCollider>();
            ball.radius = radius;
            ball.enabled = false;
            weapon = go.AddComponent<WeaponPower>();
            weapon.Power = Power;
            go.AddComponent<Punch>().owner = this;
            IgnorePlayer(ball);
        }

        // Its own input if one is set (an empty field still holds an action, just with no bindings), else the hand's.
        private static InputAction Pick(InputActionProperty own, InputActionProperty fallback)
        {
            if (own.reference != null || (own.action != null && own.action.bindings.Count > 0)) return own.action;
            return fallback.action;
        }

        private static float Read(InputAction action)
        {
            return action != null ? action.ReadValue<float>() : 0f;
        }

        /// <summary>On the fist ball and the flying fist: passes their hits back to the Fist.</summary>
        private class Punch : MonoBehaviour
        {
            public Fist owner;
            // On a flying fist: which one (empty on the fist's own ball).
            public FlyingFist flyer;
            private Rigidbody body;

            private void OnCollisionEnter(Collision collision)
            {
                if (body == null) TryGetComponent(out body);
                if (owner != null) owner.OnPunch(collision, flyer, body);
            }
        }

        /// <summary>
        /// A flying punch: the punch carried on ahead of the fist, seen as a still copy of the fist, bigger (its echo).
        /// It flies straight, hits the first thing in its way (enemies take damage as from the fist, ghost balloons get
        /// pushed, loose things get knocked), then quickly shrinks away where it stopped.
        /// </summary>
        private class FlyingFist : MonoBehaviour
        {
            /// <summary>How long the echo takes to shrink away once it has stopped, in seconds.</summary>
            public const float FadeSeconds = 0.12f;

            public Fist owner;
            public Transform echo;
            public Transform echoShape;
            public Mesh echoMesh;
            // Its puff of smoke at the wrist (Wrist Smoke), its particle systems (the first one plays them all), and its
            // star flash (only with Explosive Fist points).
            public Transform smoke;
            public ParticleSystem[] smokeSystems = System.Array.Empty<ParticleSystem>();
            public ParticleSystemRenderer smokeStar;
            // Each system's own colour (it burns red while the hands are hot), and the echo's renderer (it glows too).
            public ParticleSystem.MinMaxGradient[] smokeColours = System.Array.Empty<ParticleSystem.MinMaxGradient>();
            public MeshRenderer echoLook;

            private Rigidbody body;
            private SphereCollider ball;
            private WeaponPower weapon;
            private Vector3 direction;
            private float reach;
            private float flown;
            private float hitSpeed;
            private Move move;
            private bool done;
            private float fade;
            // How big its echo is, compared with the hand when it was thrown.
            private float size = 1f;

            public float HitSpeed => hitSpeed;
            public Move Thrown => move;
            /// <summary>The stroke that threw it, and whether that was a perfect jab (see Landed).</summary>
            public int Stroke;
            public bool Perfect;
            public Vector3 Direction => direction;
            /// <summary>How far it still has to fly, in metres; below 0 once it has stopped (the more shrunk away, the lower).</summary>
            public float LeftToFly => done ? -fade : reach - flown;
            public SphereCollider Ball => ball != null ? ball : ball = GetComponent<SphereCollider>();

            private void Awake()
            {
                body = GetComponent<Rigidbody>();
                ball = GetComponent<SphereCollider>();
                weapon = GetComponent<WeaponPower>();
            }

            /// <summary>
            /// Flies from a point (its middle, where the knuckles are) towards a direction for this many metres, hitting like
            /// a punch this fast, with a ball this big (its radius, in metres).
            /// </summary>
            public void Launch(Vector3 from, Vector3 towards, float distance, float punchSpeed, Move thrown, float flyRadius)
            {
                move = thrown;
                // The echo is the hand as it is now, sized up to the ball.
                size = Mathf.Max(flyRadius, 0.001f) / Mathf.Max(owner.radius * owner.grown, 0.001f);
                gameObject.SetActive(false);
                transform.SetPositionAndRotation(from, Quaternion.identity);
                if (echo != null && owner.handSkin != null)
                {
                    // A still copy of the hand as it is now (baked in world size), placed as the hand is around the
                    // knuckles, then sized up around them.
                    var skin = owner.handSkin.transform;
                    owner.handSkin.BakeMesh(echoMesh);
                    echo.localScale = Vector3.one;
                    echoShape.SetPositionAndRotation(skin.position + (from - owner.FistPoint()), skin.rotation);
                    echoShape.localScale = Vector3.one;
                    echo.localScale = Vector3.one * size;
                }
                gameObject.SetActive(true);
                // Switched off, a collider may forget what it ignores.
                owner.IgnorePlayer(Ball);

                // As hard as the fist at the punch's speed, however fast it flies.
                Ball.radius = Mathf.Max(flyRadius, 0.001f);
                Ball.enabled = true;
                weapon.Power = owner.Power * punchSpeed / FlySpeed;
                // The Elon Boong buff: double damage.
                weapon.boost = Game.Leveling.PlayerBuffs.PunchDamage;
                hitSpeed = punchSpeed;
                direction = towards.normalized;
                reach = Mathf.Max(distance, 0.01f);
                flown = 0f;
                PuffAtTheWrist();
                fade = 0f;
                done = false;
                body.position = from;
                body.linearVelocity = direction * FlySpeed;
            }

            private void FixedUpdate()
            {
                if (done)
                {
                    // Stopped: it hits nothing more, and the echo shrinks away where it is.
                    if (Ball.enabled) Ball.enabled = false;
                    fade += Time.fixedDeltaTime / FadeSeconds;
                    if (echo != null) echo.localScale = Vector3.one * (size * Mathf.Max(0f, 1f - fade));
                    if (fade >= 1f) gameObject.SetActive(false);
                    return;
                }
                flown += FlySpeed * Time.fixedDeltaTime;
                if (flown >= reach) Stop();
                else body.linearVelocity = direction * FlySpeed;
            }

            // A puff of smoke just behind it, at the wrist, as big as it is; it rides along (Wrist Smoke).
            private void PuffAtTheWrist()
            {
                if (smoke == null) return;
                smoke.localPosition = -direction * (Ball.radius * WristBehind);
                smoke.localRotation = Quaternion.LookRotation(-direction);
                smoke.localScale = Vector3.one * (owner.wristSmokeSize * size);
                // The star flash only with Explosive Fist points: a plain flying punch just puffs.
                if (smokeStar != null) smokeStar.enabled = Game.Leveling.PlayerLevel.ExplosiveFist > 0;
                // Overheated hands (Hot Hands): it burns their red.
                bool hot = HotHands.Active;
                var red = HotHands.GlowColor;
                red = new Color(Mathf.Min(1f, red.r), Mathf.Min(1f, red.g * 1.6f), Mathf.Min(1f, red.b * 1.6f), 1f);
                for (int i = 0; i < smokeSystems.Length; i++)
                {
                    var system = smokeSystems[i];
                    system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    if (i < smokeColours.Length)
                    {
                        var main = system.main;
                        main.startColor = hot ? new ParticleSystem.MinMaxGradient(red) : smokeColours[i];
                    }
                }
                // Every part plays (they needn't sit under one another).
                foreach (var system in smokeSystems) system.Play(false);
            }

            // Its collider goes off next step, so whatever it hit still gets this hit.
            private void Stop()
            {
                done = true;
                body.linearVelocity = Vector3.zero;
            }

            private void OnCollisionEnter(Collision collision)
            {
                if (done) return;
                Stop();
                // Ghost balloons don't take fists' bumps (a hand's punch pushes them by where the hand is), so a punch
                // that reaches one from afar pushes it here.
                var balloon = collision.collider.GetComponentInParent<Game.Enemies.GhostBalloon>();
                if (balloon != null)
                {
                    var point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
                    if (move == Move.Uppercut) balloon.Punched(hitSpeed * owner.uppercutPush, point, (direction * 0.5f + Vector3.up).normalized);
                    else balloon.Punched(hitSpeed, point, direction);
                }
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (knuckleA == null) FindKnuckles();
            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(FistPoint(), radius * grown);
        }
#endif
    }
}

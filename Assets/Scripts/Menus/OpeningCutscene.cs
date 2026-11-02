using System.Collections;
using System.Collections.Generic;
using Game.Combat;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

namespace Game.Menus
{
    /// <summary>
    /// The game's opening, once per launch, before the health warning (user, 2026-11-02). The player sits on the ground of a
    /// dream world (the ground is put a little under their eyes; their view never moves). Black, and muffled battle: a Knight
    /// standing over them, on their right or left at random, slaps them awake: each slap knocks their eyes open a bit more;
    /// they can look around and at their hands. Far off stands the giant pumpkin boss. On the right a Knight is stuck head first
    /// in the ground; on the left one has the boss's fists coming down at it, left and right, and rolls out from under each; a
    /// third runs about grabbing pumpkins off the ground and throwing them at it. The first Knight runs to the boss. As it gets in front of it, the boss spins and
    /// everyone gets flung, and lands head first, stuck in the ground. Then it turns slowly to the player, waits for them to look (a
    /// moment at most), jolts as it sees they're still alive ("!!!" over its head), laughs at them, and throws pumpkin after pumpkin: all right at them: one bursts just in front, the next flies by their head, one breaks in their face, and
    /// the screen goes dark as the boss jumps for its body slam, landing as it's black, and it laughs in the dark. Then the health warning and the
    /// save slots. Holding any button shows "Hold to skip" after a moment, and skips. Nothing flashes white. Made by Health
    /// Warning from Resources (Opening_Battle: Tools > 429 Game > Menu > Set Up Opening builds it); nothing to put in the scene.
    /// </summary>
    [DisallowMultipleComponent]
    public class OpeningCutscene : MonoBehaviour
    {
        private const string PrefabName = "Opening_Battle";

        [Tooltip("Plays in the Editor too. (Save Slot Menu's Editor Start Slot skips it either way.)")]
        [SerializeField] private bool playInEditor = true;

        [Header("Stage (placed where the player faces as it starts)")]
        [Tooltip("How far under the player's eyes the ground is, in metres: they're sitting on it.")]
        [Min(0.1f)]
        [SerializeField] private float sittingEyeHeight = 0.75f;
        [Tooltip("The flames round the field (Boss_Area), the ground's glow and the sky: their middle, this far ahead, in metres.")]
        [SerializeField] private float fieldAhead = 24f;
        [SerializeField] private Transform arena;
        [Tooltip("How many times bigger than Boss_Area the field is.")]
        [Min(0.1f)]
        [SerializeField] private float arenaSize = 4f;
        [SerializeField] private Transform dreamFloor;
        [SerializeField] private Transform sky;
        [Tooltip("A bit more light all round while it plays, so the fight can be seen.")]
        [SerializeField] private Color ambient = new(0.42f, 0.4f, 0.5f);
        [Tooltip("How far the camera sees while it plays, in metres (the boss is far off and there's no fog meanwhile).")]
        [Min(10f)]
        [SerializeField] private float seeingDistance = 220f;
        [SerializeField] private Light fightLight;

        [Header("Boss")]
        [SerializeField] private Animator boss;
        [Tooltip("How far off it stands, in metres.")]
        [Min(1f)]
        [SerializeField] private float bossDistance = 48f;
        [Tooltip("Its size (the game's true form is 2.1).")]
        [Min(0.1f)]
        [SerializeField] private float bossSize = 8f;
        [SerializeField] private string idleState = "idle_true";
        [SerializeField] private string spinState = "cyclone";
        [Tooltip("As it sees the player, and how many seconds into that \"!!!\" pops up over its head.")]
        [SerializeField] private string noticeState = "notice";
        [Min(0f)]
        [SerializeField] private float noticeMarksAt = 0.25f;
        [SerializeField] private Transform noticeMarks;
        [Tooltip("Over its head while it's dizzy (Hit Settings' Stun Stars), and how wide, in metres.")]
        [SerializeField] private GameObject stunStars;
        [Min(0.01f)]
        [SerializeField] private float stunStarsRadius = 3f;
        [Tooltip("Then it laughs at them, standing (its \"laugh\" clip is for its upper body).")]
        [SerializeField] private string laughState = "laugh_true";
        [Tooltip("After its spin, as in the game, until it sees the player.")]
        [SerializeField] private string dizzyState = "dizzy_true";
        [Tooltip("Seconds into its spin that everyone gets flung.")]
        [Min(0f)]
        [SerializeField] private float spinHit = 0.9f;
        [Tooltip("Its punches, left and right by turns, at the Knight on the left.")]
        [SerializeField] private string punchLeftState = "punch_L";
        [SerializeField] private string punchRightState = "punch_R";
        [Tooltip("How many times faster than their clips it punches.")]
        [Min(0.1f)]
        [SerializeField] private float punchSpeed = 1.6f;
        [Tooltip("Seconds from one punch to the next.")]
        [Min(0.1f)]
        [SerializeField] private float punchEvery = 1.05f;
        [Tooltip("Its throws, right and left hand by turns.")]
        [SerializeField] private string throwState = "throw";
        [SerializeField] private string throwLeftState = "throw_L";
        [Tooltip("Seconds into its throw that the pumpkin leaves its hand.")]
        [Min(0f)]
        [SerializeField] private float throwRelease = 0.65f;
        [Tooltip("How many times faster than its clip it throws.")]
        [Min(0.1f)]
        [SerializeField] private float throwSpeed = 1.3f;
        [Tooltip("Seconds from one throw to the next.")]
        [Min(0.1f)]
        [SerializeField] private float throwEvery = 0.85f;
        [Tooltip("How many pumpkins it takes to get them in the face: all come right at them, the first bursting just in front, the others flying by their head.")]
        [Min(0)]
        [SerializeField] private int throwsThatMiss = 2;
        [Tooltip("How far in front of the player the first bursts, in metres.")]
        [Min(0f)]
        [SerializeField] private float missShort = 4f;
        [Tooltip("How far to the side of their head the others fly by, in metres (the pumpkin is about 1.7 wide from its middle).")]
        [Min(0f)]
        [SerializeField] private float missBy = 1.8f;
        [Tooltip("Its body slam, as the screen goes dark: it flies at the player and lands this far short of them, in metres.")]
        [SerializeField] private string slamState = "jump_slam";
        [Min(0f)]
        [SerializeField] private float slamStopsShort = 5f;
        [Tooltip("The bones that hold the pumpkin in those throws.")]
        [SerializeField] private string throwingHand = "Hand.R";
        [SerializeField] private string throwingHandLeft = "Hand.L";
        [Tooltip("The pumpkin it throws (the look of its Bug Pumpkin).")]
        [SerializeField] private Transform flyingPumpkin;
        [Tooltip("What that pumpkin breaks into in the player's face: pieces, and an effect (Effect Pool).")]
        [SerializeField] private GameObject brokenPumpkin;
        [SerializeField] private GameObject splashEffect;
        [Min(0f)]
        [SerializeField] private float splashSize = 1f;
        [Tooltip("How big the pieces are.")]
        [Min(0.01f)]
        [SerializeField] private float brokenSize = 2.6f;
        [Tooltip("Played under its feet at the spin (Effect Pool), and their size.")]
        [SerializeField] private GameObject spinEffect;
        [Min(0f)]
        [SerializeField] private float spinEffectSize = 3f;
        [Tooltip("Played where a fist lands (Effect Pool), and its size.")]
        [SerializeField] private GameObject fistEffect;
        [Min(0f)]
        [SerializeField] private float fistEffectSize = 1.5f;

        [Header("Knights")]
        [Tooltip("The size of them all. Tall enough to stand over the player on the ground.")]
        [Min(0.1f)]
        [SerializeField] private float knightSize = 2f;
        [Tooltip("Dark on the ground under them, in the order: first, stuck, thrower, fighter.")]
        [SerializeField] private Transform[] knightShadows = System.Array.Empty<Transform>();
        [Tooltip("The Knight getting the player up, then running to the boss.")]
        [SerializeField] private Animator leadKnight;
        [Tooltip("Which side of the player it stands: 0 at random, 1 their right, -1 their left.")]
        [Range(-1, 1)]
        [SerializeField] private int callerSide;
        [Tooltip("Where it stands: how far to the side of them, and how far ahead, in metres.")]
        [SerializeField] private Vector2 leadSideAndAhead = new(0.9f, 1.1f);
        [Tooltip("The boss spins when it's this far in front of the boss, in metres.")]
        [Min(0.5f)]
        [SerializeField] private float frontOfBoss = 16f;
        [Tooltip("The one stuck head first in the ground, always on the right.")]
        [SerializeField] private Animator stuckKnight;
        [Tooltip("Where it is: how far to the right and how far ahead of the player, in metres.")]
        [SerializeField] private Vector2 stuckAt = new(7f, 10f);
        [Tooltip("The one with the boss's attention, rolling out from under its fists, always on the left.")]
        [SerializeField] private Animator fightKnight;
        [Tooltip("Where it is: how far to the left and how far ahead of the player, in metres.")]
        [SerializeField] private Vector2 fightAt = new(16f, 38f);
        [Tooltip("How far to each side of that spot its rolls end (x to y, in metres).")]
        [SerializeField] private Vector2 rollFar = new(2f, 3.5f);
        [Tooltip("The one running about, grabbing pumpkins and throwing them at the boss.")]
        [SerializeField] private Animator throwerKnight;
        [Tooltip("The bones holding what it grabs.")]
        [SerializeField] private string leftHand = "mixamorig:LeftHand";
        [SerializeField] private string rightHand = "mixamorig:RightHand";
        [SerializeField] private string helmetBone = "mixamorig:Head";
        [Tooltip("The Pumpkin_On_Tree pumpkins lying everywhere. The first ones lie where the thrower runs about.")]
        [SerializeField] private Transform[] pumpkins = System.Array.Empty<Transform>();
        [Tooltip("How far up from where it lies a pumpkin's middle is, in metres, so it sits on the ground.")]
        [Min(0f)]
        [SerializeField] private float pumpkinLift = 0.3f;
        [Tooltip("How fast they run, in metres per second.")]
        [Min(0.5f)]
        [SerializeField] private float runSpeed = 8f;
        [Tooltip("How far (x to y, in metres) the boss's spin flings them, and how high.")]
        [SerializeField] private Vector2 flingFar = new(16f, 26f);
        [SerializeField] private Vector2 flingHigh = new(8f, 16f);
        [Tooltip("How long they're in the air, in seconds (x to y).")]
        [SerializeField] private Vector2 flingSeconds = new(1.6f, 2.2f);
        [SerializeField] private string knightIdle = "Idle";
        [SerializeField] private string knightRoll = "Roll";
        [SerializeField] private string knightGetUpOnRight = "Get_Up_R";
        [SerializeField] private string knightGetUpOnLeft = "Get_Up_L";
        [SerializeField] private string knightRun = "Run";
        [SerializeField] private string knightAttack = "Attack";
        [SerializeField] private string knightStuck = "Stuck";
        [SerializeField] private string knightFlung = "Flung";
        [SerializeField] private string knightGrabThrow = "Grab_Throw";

        [Header("Timing (seconds)")]
        [Tooltip("Black before the eyes first open (the voice calls meanwhile).")]
        [Min(0f)]
        [SerializeField] private float blackSeconds = 3f;
        [Tooltip("How long after its first slap the Knight turns and runs.")]
        [Min(0f)]
        [SerializeField] private float getUpSeconds = 6.4f;
        [Tooltip("How long the boss waits for the player to look at it, at most.")]
        [Min(0f)]
        [SerializeField] private float waitForLook = 3.5f;
        [Tooltip("How long it takes to turn to them at least.")]
        [Min(0f)]
        [SerializeField] private float noticeSeconds = 2f;
        [Tooltip("How long it stares after that before it throws.")]
        [Min(0f)]
        [SerializeField] private float stareSeconds = 0.25f;
        [Tooltip("How long its pumpkin flies.")]
        [Min(0.1f)]
        [SerializeField] private float flightSeconds = 2.2f;
        [Tooltip("How long the broken pumpkin is seen before it all goes black.")]
        [Min(0f)]
        [SerializeField] private float splashSeconds = 0.35f;
        [Tooltip("How long the screen takes to go dark. The boss jumps for its body slam as it starts, and lands (1.33 s later) as it's black.")]
        [Min(0.01f)]
        [SerializeField] private float darkenSeconds = 1.33f;
        [Tooltip("How long it's dark, the boss laughing, before the health warning.")]
        [Min(0f)]
        [SerializeField] private float darkSeconds = 2f;

        [Header("Sounds")]
        [Tooltip("The voice calling the player to wake up (from the Knight's own speaker, so it says which side it's on).")]
        [SerializeField] private AudioClip wakeVoice;
        [Range(0f, 1f)]
        [SerializeField] private float wakeVoiceVolume = 1f;
        [Tooltip("The battle's muffled until the eyes open: how muffled (Hz; 22000 = not at all).")]
        [Min(100f)]
        [SerializeField] private float muffledHz = 700f;
        [Tooltip("The Knight's hand on the player's face.")]
        [SerializeField] private AudioClip slapSound;
        [Range(0f, 1f)]
        [SerializeField] private float slapVolume = 1f;
        [Tooltip("Metal on the ground, as a flung Knight lands.")]
        [SerializeField] private AudioClip[] clangs = System.Array.Empty<AudioClip>();
        [Range(0f, 1f)]
        [SerializeField] private float clangVolume = 0.7f;
        [SerializeField] private AudioClip[] bossLaughs = System.Array.Empty<AudioClip>();
        [SerializeField] private AudioClip bossRoar;
        [SerializeField] private AudioClip spinSound;
        [Tooltip("As a fist lands.")]
        [SerializeField] private AudioClip punchSound;
        [Range(0f, 1f)]
        [SerializeField] private float punchVolume = 0.7f;
        [SerializeField] private AudioClip flySound;
        [Tooltip("Played where a thrown pumpkin bursts on the boss, with the Effect below.")]
        [SerializeField] private AudioClip splatSound;
        [SerializeField] private GameObject splatEffect;
        [Tooltip("Played as the pumpkin breaks on the player (all of them).")]
        [SerializeField] private AudioClip[] hitSounds = System.Array.Empty<AudioClip>();

        [Header("Skipping")]
        [Tooltip("Any button held this long, in seconds, shows the text...")]
        [Min(0f)]
        [SerializeField] private float hintAfter = 1f;
        [Tooltip("...and this long skips it.")]
        [Min(0.1f)]
        [SerializeField] private float skipAfter = 2.5f;
        [SerializeField] private string skipText = "Hold to skip";
        [Tooltip("Where the text sits: how far ahead and how far below the eyes, in metres.")]
        [SerializeField] private Vector2 textAt = new(1.2f, 0.42f);

        [Header("Eyelids")]
        [SerializeField] private Material lidsMaterial;

        // Drawn after the lids.
        private const int TextQueue = (int)UnityEngine.Rendering.RenderQueue.Overlay + 3;
        private const string TextMaterial = "Damage Number";
        private const string HoldColor = "#FFD040";
        private const float LookAngle = 50f;
        // Where the boss's pumpkin breaks, in metres in front of the eyes, and how high it arcs on its way.
        private const float BreakDistance = 2.2f;
        private const float ArcHeight = 8f;
        private const float ClearHz = 22000f;
        // How fast it turns to the player (degrees a second), and how long its notice clip is.
        private const float NoticeTurn = 35f;
        private const float NoticeClipSeconds = 1.6f;
        private const float LaughClipSeconds = 1.6f;
        private const string BossHead = "Head";
        // The stun stars: how far above its head bone (share of its size), and how long at most (it notices the player first).
        private const float StarsAbove = 0.6f;
        private const float StarsSeconds = 20f;
        // "!!!" sits this far above its head bone (share of its size).
        private const float MarksAbove = 0.8f;
        // Its body slam clip, in its own seconds: it leaves the ground and lands.
        private const float SlamLeaves = 0.55f;
        private const float SlamLands = 1.33f;
        // A pumpkin bursting on the ground near the player.
        private const float NearSplatSize = 3f;
        // Those that fly by come down this far behind the player.
        private const float MissBehind = 3f;
        // The pumpkin in its hand sits on top of its fist, this far up (share of its size).
        private const float PumpkinOnFist = 0.3f;
        // How deep it sinks for its spin (share of its size): as in the game, its butt pumpkin is what stands on the ground.
        private const float ButtOnGround = 1.06f;
        private const float BossSinkSeconds = 0.3f;
        private const float BossRiseSeconds = 0.5f;
        // The Knight's slaps, in seconds into its clip at the speed the controller plays it (0.6). Its arm stretches for this
        // long to each side of them, and stops this far from the player's eyes, in metres.
        private static readonly float[] SlapAt = { 0.556f, 1.944f, 3.333f, 4.722f };
        private const float SlapSway = 0.2f;
        private const float SlapStopsShort = 0.5f;
        private const string SlapArmRight = "mixamorig:RightArm";
        private const string SlapArmLeft = "mixamorig:LeftArm";
        private const string SlapShoulderRight = "mixamorig:RightShoulder";
        private const string SlapShoulderLeft = "mixamorig:LeftShoulder";
        // The shoulder goes with the arm as it reaches (its pad is on it): this share of the turn, and of the stretch.
        private const float SlapShoulderTurn = 0.35f;
        private const float SlapShoulderSwell = 0.25f;
        // After each slap: how open the eyes are knocked, how far they droop again, and how clear the sound gets.
        private static readonly (float opens, float droops, float clear)[] EyesAfterSlap =
        {
            (0.22f, 0.06f, 0.5f),
            (0.45f, 0.2f, 0.8f),
            (0.7f, 1f, 1f),
        };
        private const float EyesKnockedOpen = 0.12f;
        private const float EyesDroop = 1.2f;
        // The Knight's soles are this far under its origin, in metres at size 1.
        private const float SolesBelow = 0.04f;
        // The punch clips, in their own seconds: the fist whips out from here, lands here, and comes back from here.
        private const float FistWhips = 0.72f;
        private const float FistLands = 0.83f;
        private const float FistBack = 1.3f;
        // How far the middle of a fist stays above the ground (share of the boss's size).
        private const float FistRadius = 0.3f;
        // Roll, in seconds at the speed the controller plays it (1.2); it starts this long before the fist lands, and ends
        // up to this far nearer the boss or further off, in metres.
        private const float RollSeconds = 0.83f;
        private const float RollBefore = 0.28f;
        private const float RollInOut = 1.5f;
        // How high on the boss a thrown pumpkin hits (share of its size).
        private const float BossChest = 1.2f;
        // Grab and Throw, in its clip's seconds at the speed the controller plays it (0.8): hands on the pumpkin; up over the
        // helmet (the arms are too short to lift it past it); let go; the end.
        private const float GrabTime = 0.71f;
        private const float OverheadTime = 1.2f;
        private const float ReleaseTime = 1.71f;
        private const float GrabThrowEnd = 2.7f;
        // Stops this far from a pumpkin to grab it, in metres at size 1.
        private const float GrabReach = 0.28f;
        // Over the helmet, in metres at size 1 from the head bone.
        private const float OverHelmet = 0.5f;
        private const float ThrownPumpkinSeconds = 1.2f;
        private const float ThrownPumpkinArc = 6f;
        private const float SplatSize = 3f;
        // Where the thrower runs about: how far to each side of the player and how far ahead, in metres.
        private static readonly Rect ThrowerRuns = new(-7f, 14f, 16f, 18f);
        // The nearest to the player that a flung Knight lands, in metres, off to the side.
        private const float LandClear = 5f;
        // Share of a flung Knight's flight that it tumbles, and how long before it lands it turns head down.
        private const float TumbleUntil = 0.75f;
        private const float HeadDownBefore = 0.25f;
        // The boss's Animator layer that holds its legs (Opening_Boss.controller).
        private const int LegsLayer = 1;
        private const int ScatterSeed = 429;
        // The first pumpkins lie where the thrower runs about, the others all round; none this near...
        private const int ThrowerPumpkins = 7;
        private const float NearToFar = 58f;
        private const float ClearOfKnights = 2f;
        private const float ClearOfFight = 7f;
        private const float ClearOfBoss = 8f;
        // ...and none in the first Knight's lane to the boss.
        private const float LaneWidth = 1.5f;

        private static bool playedThisLaunch;
        private static OpeningCutscene current;

        /// <summary>It's playing: the health warning waits for it.</summary>
        public static bool IsPlaying => current != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            playedThisLaunch = false;
            current = null;
        }

        /// <summary>Starts it if it hasn't played yet in this launch. False if it doesn't play (the warning goes on at once).</summary>
        public static bool TryStart()
        {
            if (playedThisLaunch || Game.Saving.SaveManager.CurrentSlot > 0) return false;
            playedThisLaunch = true;
            var prefab = Resources.Load<OpeningCutscene>(PrefabName);
            if (prefab == null) return false;
#if UNITY_EDITOR
            if (!prefab.playInEditor) return false;
            var menu = FindAnyObjectByType<SaveSlotMenu>(FindObjectsInactive.Include);
            if (menu != null && menu.StartsSlotInEditor) return false;
#endif
            Instantiate(prefab);
            return true;
        }

        private Transform head;
        private Camera eyes;
        private XROrigin player;
        private Material lids;
        private GameObject lidsObject;
        private AudioSource voice;
        private AudioLowPassFilter muffler;
        private TextMeshPro hint;
        private InputAction anyButton;
        private bool waitingForRelease;
        private float heldFor;
        private bool skipping;
        private bool over;
        private Vector3 floorPoint;
        private Vector3 facing;
        private Vector3 right;
        private Vector3 bossSpot;
        // Which side of the player the first Knight is on: 1 right, -1 left.
        private float side = 1f;
        private bool leadThere;
        private bool spinning;
        // The left Knight's spot, where its roll under way ends, and the punch under way: where its fist lands.
        private Vector3 fightHome;
        private Vector3 rollEnd;
        private Vector3 fistOn;
        private Transform punchArm;
        private Transform punchHand;
        private float punchStarted;
        // The first Knight's arm while it's slapping.
        private Transform slapArm;
        private Transform slapHand;
        private Transform slapShoulder;
        private Vector3 slapArmSize;
        private Vector3 slapShoulderSize;
        private float slapStarted;
        // The bone "!!!" follows.
        private Transform marksOver;
        private bool darkening;
        private Animator[] shadowed = System.Array.Empty<Animator>();
        private readonly HashSet<Transform> pumpkinsTaken = new();
        // What's switched off while it plays, and how it was.
        private readonly List<(XRBaseInteractor interactor, bool select, bool hover)> interactors = new();
        private readonly List<(Behaviour behaviour, bool enabled)> stopped = new();
        private readonly List<(LineRenderer line, bool enabled)> hiddenLines = new();
        private readonly List<AudioSource> mutedSpeakers = new();
        private readonly List<Renderer> shown = new();
        private bool sceneChanged;
        private Color ambientBefore;
        private bool fogBefore;
        private float farBefore;

        private static readonly int OpenId = Shader.PropertyToID("_Open");
        private static readonly int FadeId = Shader.PropertyToID("_Fade");

        private void Awake()
        {
            current = this;
            // Black from the very first frame.
            head = Camera.main != null ? Camera.main.transform : null;
            MakeLids();
            // Hidden until it's all placed; what's off in its own right stays off.
            foreach (var part in GetComponentsInChildren<Renderer>())
            {
                if (!part.enabled) continue;
                shown.Add(part);
                part.enabled = false;
            }
        }

        private void OnEnable()
        {
            anyButton = new InputAction("Any Button", InputActionType.Button);
            foreach (var hand in new[] { "LeftHand", "RightHand" })
            {
                foreach (var button in new[] { "triggerPressed", "gripPressed", "primaryButton", "secondaryButton", "thumbstickClicked", "menuButton" })
                    anyButton.AddBinding($"<XRController>{{{hand}}}/{button}");
            }
            anyButton.AddBinding("<Keyboard>/space");
            anyButton.AddBinding("<Keyboard>/enter");
            anyButton.Enable();
        }

        private void OnDisable()
        {
            anyButton?.Disable();
            anyButton?.Dispose();
            anyButton = null;
        }

        private void Start()
        {
            // A button that's down already (putting the headset on) doesn't count until it's let go.
            waitingForRelease = anyButton != null && anyButton.IsPressed();
            StartCoroutine(Play());
        }

        private void OnDestroy()
        {
            if (current == this) current = null;
        }

        private void Update()
        {
            if (over) return;
            WatchSkip();
        }

        // After the Animator has posed them. The dark on the ground follows each Knight, and spreads and fades as it goes up.
        private void LateUpdate()
        {
            AimPunch();
            AimSlap();
            if (marksOver != null) noticeMarks.position = marksOver.position + Vector3.up * (MarksAbove * bossSize);
            for (int i = 0; i < knightShadows.Length && i < shadowed.Length; i++)
            {
                if (knightShadows[i] == null || shadowed[i] == null) continue;
                var at = shadowed[i].transform.position;
                float up = Mathf.Clamp01((at.y - floorPoint.y) / 8f);
                knightShadows[i].position = new Vector3(at.x, floorPoint.y + 0.03f, at.z);
                knightShadows[i].localScale = Vector3.one * (knightSize * Mathf.Lerp(0.55f, 0.2f, up));
            }
        }

        // ---------- The story ----------

        private IEnumerator Play()
        {
            // The headset's pose isn't known in the very first moments.
            yield return new WaitForSeconds(0.25f);
            SetUp();
            if (over) yield break;

            if (leadKnight.TryGetComponent<AudioSource>(out var speaker) && wakeVoice != null) speaker.PlayOneShot(wakeVoice, wakeVoiceVolume);
            StartCoroutine(FightGoesOn());
            // Its first slap lands as the black ends.
            yield return new WaitForSeconds(Mathf.Max(0f, blackSeconds - SlapAt[0]));
            StartCoroutine(Slaps());
            yield return new WaitForSeconds(SlapAt[0]);
            StartCoroutine(ThrowerKnight());
            yield return new WaitForSeconds(getUpSeconds);
            StartCoroutine(LeadCharges());
            while (!leadThere) yield return null;
            yield return BossSpins();
            yield return BossNoticesAndThrows();
            yield return LightsOut();
            Finish();
        }

        // Each slap knocks the eyes open a bit more, and they droop again, slowly, until they stay open; the sound clears as they do.
        private IEnumerator Slaps()
        {
            Play(leadKnight, side > 0f ? knightGetUpOnRight : knightGetUpOnLeft, 0f);
            var body = leadKnight.transform;
            slapArm = body.FindDeep(side > 0f ? SlapArmRight : SlapArmLeft);
            slapHand = body.FindDeep(side > 0f ? rightHand : leftHand);
            slapShoulder = body.FindDeep(side > 0f ? SlapShoulderRight : SlapShoulderLeft);
            slapArmSize = slapArm.localScale;
            slapShoulderSize = slapShoulder.localScale;
            slapStarted = Time.time;
            for (int i = 0; i < SlapAt.Length; i++)
            {
                while (Time.time - slapStarted < SlapAt[i]) yield return null;
                if (slapSound != null) voice.PlayOneShot(slapSound, slapVolume);
                ViewShake.Send(ViewShake.Small);
                if (i >= EyesAfterSlap.Length) continue;
                yield return Lids(EyesAfterSlap[i].opens, EyesKnockedOpen, EyesAfterSlap[i].clear);
                yield return Lids(EyesAfterSlap[i].droops, EyesDroop, 0f);
            }
            while (Time.time - slapStarted < SlapAt[SlapAt.Length - 1] + SlapSway) yield return null;
            slapArm.localScale = slapArmSize;
            slapShoulder.localScale = slapShoulderSize;
            slapArm = null;
        }

        // The clip swings its arm; around each slap the shoulder and arm stretch, rubber-like, to the player's face wherever that is.
        private void AimSlap()
        {
            if (slapArm == null) return;
            slapArm.localScale = slapArmSize;
            slapShoulder.localScale = slapShoulderSize;
            float since = Time.time - slapStarted;
            float nearest = float.MaxValue;
            foreach (float at in SlapAt) nearest = Mathf.Min(nearest, Mathf.Abs(since - at));
            float reach = Mathf.SmoothStep(0f, 1f, 1f - nearest / SlapSway);
            if (reach <= 0f) return;
            TurnToFace(slapShoulder, reach * SlapShoulderTurn);
            slapShoulder.localScale = slapShoulderSize * Mathf.Lerp(1f, 1f + (SlapStretch() - 1f) * SlapShoulderSwell, reach);
            TurnToFace(slapArm, reach);
            slapArm.localScale = slapArmSize * Mathf.Lerp(1f, SlapStretch(), reach);
        }

        // Turns the bone so the hand comes that share of the way to the player's face.
        private void TurnToFace(Transform bone, float share)
        {
            var tilt = Quaternion.FromToRotation(slapHand.position - bone.position, SlapFace() - bone.position);
            bone.rotation = Quaternion.Slerp(Quaternion.identity, tilt, share) * bone.rotation;
        }

        private Vector3 SlapFace() => head.position + (slapArm.position - head.position).normalized * SlapStopsShort;

        // How many times longer the arm has to be from where it is now.
        private float SlapStretch() => (SlapFace() - slapArm.position).magnitude / Mathf.Max(0.01f, (slapHand.position - slapArm.position).magnitude);

        private IEnumerator Lids(float open, float seconds, float clear)
        {
            float from = lids.GetFloat(OpenId);
            float hz = muffler != null ? muffler.cutoffFrequency : ClearHz;
            float to = Mathf.Lerp(muffledHz, ClearHz, clear * clear);
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / seconds);
                lids.SetFloat(OpenId, Mathf.Lerp(from, open, k));
                if (muffler != null && clear > 0f) muffler.cutoffFrequency = Mathf.Lerp(hz, to, k);
                yield return null;
            }
            lids.SetFloat(OpenId, open);
            if (open >= 1f) lidsObject.SetActive(false);
        }

        // Out on the field: the stuck Knight kicking; the boss's fists coming down, left and right, on the Knight on the left,
        // which rolls out from under each one.
        private IEnumerator FightGoesOn()
        {
            Play(boss, idleState, 0f);
            Play(stuckKnight, knightStuck, Random.value);
            boss.speed = punchSpeed;
            bool left = Random.value < 0.5f;
            int way = Random.value < 0.5f ? 1 : -1;
            float lands = FistLands / punchSpeed;
            while (!spinning)
            {
                Punch(left);
                left = !left;
                bool rolled = false;
                bool landed = false;
                for (float t = 0f; t < punchEvery && !spinning; t += Time.deltaTime)
                {
                    TurnTo(boss.transform, fightKnight.transform.position, 40f);
                    if (!rolled && t >= lands - RollBefore)
                    {
                        rolled = true;
                        StartCoroutine(Roll(way));
                        way = -way;
                    }
                    if (!landed && t >= lands)
                    {
                        landed = true;
                        FistLanded();
                    }
                    yield return null;
                }
            }
        }

        // At where the Knight is, or is about to be.
        private void Punch(bool left)
        {
            Play(boss, left ? punchLeftState : punchRightState, 0f);
            punchArm = boss.transform.FindDeep(left ? "UpperArm.L" : "UpperArm.R");
            punchHand = boss.transform.FindDeep(left ? "Hand.L" : "Hand.R");
            fistOn = rollEnd;
            punchStarted = Time.time;
        }

        // The punch clips were made for its upper body, half in the ground, so its arm tilts to bring the fist down where it's
        // meant to land, and the fist stops on the ground there.
        private void AimPunch()
        {
            if (punchArm == null) return;
            float t = (Time.time - punchStarted) * punchSpeed;
            float aim = Mathf.Clamp01(Mathf.Min((t - FistWhips) / 0.08f, (FistBack - t) / 0.2f));
            float floor = floorPoint.y + FistRadius * bossSize;
            if (aim > 0f)
            {
                var wanted = new Vector3(fistOn.x, floor, fistOn.z) - punchArm.position;
                var tilt = Quaternion.FromToRotation(punchHand.position - punchArm.position, wanted);
                punchArm.rotation = Quaternion.Slerp(Quaternion.identity, tilt, aim) * punchArm.rotation;
            }
            var fist = punchHand.position;
            if (fist.y >= floor) return;
            var back = (punchArm.position - fist).normalized;
            punchHand.position = fist + back * ((floor - fist.y) / Mathf.Max(0.05f, back.y));
        }

        private void FistLanded()
        {
            if (fistEffect != null) EffectPool.Play(fistEffect, fistOn, fistEffectSize);
            if (punchSound != null) AudioSource.PlayClipAtPoint(punchSound, Heard(fistOn), punchVolume);
        }

        // Across in front of the boss, one way and the next the other, and up facing it again.
        private IEnumerator Roll(int way)
        {
            var body = fightKnight.transform;
            var from = body.position;
            var toBoss = Flat(bossSpot - fightHome).normalized;
            rollEnd = fightHome + Vector3.Cross(Vector3.up, toBoss) * (way * Random.Range(rollFar.x, rollFar.y)) + toBoss * Random.Range(-RollInOut, RollInOut);
            body.rotation = Quaternion.LookRotation(Flat(rollEnd - from));
            Play(fightKnight, knightRoll, 0f);
            for (float t = 0f; t < RollSeconds; t += Time.deltaTime)
            {
                if (spinning) yield break;
                body.position = Vector3.Lerp(from, rollEnd, Mathf.SmoothStep(0f, 1f, t / RollSeconds));
                yield return null;
            }
            if (spinning) yield break;
            body.position = rollEnd;
            Play(fightKnight, knightIdle, 0f);
            for (float t = 0f; t < 0.25f && !spinning; t += Time.deltaTime)
            {
                TurnTo(body, bossSpot, 720f);
                yield return null;
            }
        }

        // Runs to a pumpkin lying about, grabs it, heaves it over its head and throws it at the boss, and on to the next,
        // till the boss spins.
        private IEnumerator ThrowerKnight()
        {
            if (throwerKnight == null) yield break;
            var body = throwerKnight.transform;
            var handL = body.FindDeep(leftHand);
            var handR = body.FindDeep(rightHand);
            var helmet = body.FindDeep(helmetBone);
            while (!spinning)
            {
                var pumpkin = NearestPumpkin(body.position);
                if (pumpkin == null) yield break;
                pumpkinsTaken.Add(pumpkin);
                Play(throwerKnight, knightRun, 0f);
                var way = Flat(pumpkin.position - body.position);
                var spot = new Vector3(pumpkin.position.x, body.position.y, pumpkin.position.z) - way.normalized * (GrabReach * knightSize);
                while (!spinning && RunStep(body, spot)) yield return null;
                if (spinning) yield break;

                Play(throwerKnight, knightGrabThrow, 0f);
                var shadow = pumpkin.Find("Shadow");
                for (float t = 0f; t < ReleaseTime; t += Time.deltaTime)
                {
                    if (spinning) yield break;
                    if (t < GrabTime) TurnTo(body, pumpkin.position, 360f);
                    else
                    {
                        TurnTo(body, bossSpot, 120f);
                        if (shadow != null) shadow.gameObject.SetActive(false);
                        var inHands = (handL.position + handR.position) * 0.5f;
                        var above = helmet.position + Vector3.up * (OverHelmet * knightSize);
                        pumpkin.position = Vector3.Lerp(inHands, above, Mathf.InverseLerp(GrabTime, OverheadTime, t));
                    }
                    yield return null;
                }
                StartCoroutine(PumpkinHitsBoss(pumpkin));
                yield return new WaitForSeconds(GrabThrowEnd - ReleaseTime);
            }
        }

        private Transform NearestPumpkin(Vector3 from)
        {
            Transform best = null;
            float nearest = float.MaxValue;
            foreach (var pumpkin in pumpkins)
            {
                if (pumpkin == null || pumpkinsTaken.Contains(pumpkin)) continue;
                var local = pumpkin.position - floorPoint;
                var on = new Vector2(Vector3.Dot(local, right), Vector3.Dot(local, facing));
                if (!ThrowerRuns.Contains(on)) continue;
                float away = (pumpkin.position - from).sqrMagnitude;
                if (away < nearest)
                {
                    nearest = away;
                    best = pumpkin;
                }
            }
            return best;
        }

        // Up and over, and it bursts on the boss's chest.
        private IEnumerator PumpkinHitsBoss(Transform pumpkin)
        {
            var from = pumpkin.position;
            var to = bossSpot + Vector3.up * (bossSize * BossChest);
            var spin = Random.onUnitSphere * 300f;
            for (float t = 0f; t < ThrownPumpkinSeconds; t += Time.deltaTime)
            {
                float k = t / ThrownPumpkinSeconds;
                pumpkin.position = Vector3.Lerp(from, to, k) + Vector3.up * (4f * k * (1f - k) * ThrownPumpkinArc);
                pumpkin.Rotate(spin * Time.deltaTime, Space.World);
                yield return null;
            }
            foreach (var part in pumpkin.GetComponentsInChildren<Renderer>()) part.enabled = false;
            if (splatEffect != null) EffectPool.Play(splatEffect, to, SplatSize);
            if (splatSound != null) AudioSource.PlayClipAtPoint(splatSound, Heard(to));
        }

        // Done getting the player up: it turns round and runs to the front of the boss.
        private IEnumerator LeadCharges()
        {
            Play(leadKnight, knightRun, 0f);
            var body = leadKnight.transform;
            var to = bossSpot - facing * frontOfBoss;
            while (RunStep(body, to)) yield return null;
            body.rotation = Quaternion.LookRotation(Flat(bossSpot - body.position));
            Play(leadKnight, knightAttack, 0f);
            leadThere = true;
        }

        // One step of a run towards a spot; false once it's there.
        private bool RunStep(Transform body, Vector3 to)
        {
            var way = Flat(to - body.position);
            float step = runSpeed * Time.deltaTime;
            if (way.magnitude <= step) return false;
            body.rotation = Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(way), 360f * Time.deltaTime);
            body.position += way.normalized * step;
            return true;
        }

        // Its spinning move: everyone gets flung.
        private IEnumerator BossSpins()
        {
            spinning = true;
            punchArm = null;
            boss.speed = 1f;
            Play(boss, spinState, 0f);
            StartCoroutine(LegsLetGo());
            StartCoroutine(BossSinks(0f, ButtOnGround, BossSinkSeconds));
            if (spinEffect != null) EffectPool.Play(spinEffect, bossSpot, spinEffectSize);
            SayFromBoss(bossRoar);
            SayFromBoss(spinSound);
            yield return new WaitForSeconds(spinHit);
            ViewShake.Near(bossSpot, 10f, 60f);
            foreach (var knight in new[] { leadKnight, stuckKnight, throwerKnight, fightKnight })
            {
                if (knight != null) StartCoroutine(Flung(knight));
            }
            yield return new WaitForSeconds(0.1f);
            float rest = boss.GetCurrentAnimatorStateInfo(0).length - spinHit - 0.1f;
            if (rest > 0f) yield return new WaitForSeconds(rest);
            Play(boss, dizzyState, 0f);
            Game.VFX.StunStars.Show(stunStars, boss.transform, boss.transform.FindDeep(BossHead), StarsAbove * bossSize, stunStarsRadius, StarsSeconds);
            StartCoroutine(BossSinks(ButtOnGround, 0f, BossRiseSeconds));
            if (fistEffect != null) EffectPool.Play(fistEffect, bossSpot, spinEffectSize);
        }

        // Down into the ground or up out of it (shares of its size).
        private IEnumerator BossSinks(float from, float to, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                boss.transform.position = bossSpot + Vector3.down * (Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / seconds)) * bossSize);
                yield return null;
            }
            boss.transform.position = bossSpot + Vector3.down * (to * bossSize);
        }

        // Its legs stood still under its punches (those clips tuck them away); once the punch has faded into the next move, the
        // legs are that move's.
        private IEnumerator LegsLetGo()
        {
            yield return new WaitForSeconds(0.2f);
            for (float t = 0f; t < 0.2f; t += Time.deltaTime)
            {
                boss.SetLayerWeight(LegsLayer, 1f - t / 0.2f);
                yield return null;
            }
            boss.SetLayerWeight(LegsLayer, 0f);
        }

        // Up through the air, tumbling, arms and legs everywhere, and head first into the ground, stuck there kicking (well clear
        // of the player).
        private IEnumerator Flung(Animator knight)
        {
            Play(knight, knightFlung, 0f);
            var body = knight.transform;
            var from = body.position;
            var away = Flat(from - bossSpot);
            away = away.sqrMagnitude > 0.01f ? away.normalized : -facing;
            var to = from + away * Random.Range(flingFar.x, flingFar.y);
            to.y = floorPoint.y;
            float offLine = Vector3.Dot(to - floorPoint, right);
            if (Mathf.Abs(offLine) < LandClear) to += right * ((offLine < 0f ? -LandClear : LandClear) - offLine);
            float seconds = Random.Range(flingSeconds.x, flingSeconds.y);
            float high = Random.Range(flingHigh.x, flingHigh.y);
            var tumble = Random.onUnitSphere * Random.Range(300f, 600f);
            var landing = Quaternion.LookRotation(Flat(bossSpot - to));
            bool headDown = false;
            if (flySound != null) AudioSource.PlayClipAtPoint(flySound, Heard(from));
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = t / seconds;
                body.position = Vector3.Lerp(from, to, k) + Vector3.up * (4f * k * (1f - k) * high);
                body.Rotate(tumble * Time.deltaTime, Space.World);
                // It stops tumbling on the way down (the stuck clip itself is upside down).
                if (k > TumbleUntil) body.rotation = Quaternion.Slerp(body.rotation, landing, (k - TumbleUntil) / (1f - TumbleUntil));
                if (!headDown && t >= seconds - HeadDownBefore)
                {
                    headDown = true;
                    Play(knight, knightStuck, Random.value);
                }
                yield return null;
            }
            body.SetPositionAndRotation(to, landing);
            if (fistEffect != null) EffectPool.Play(fistEffect, to, fistEffectSize);
            if (clangs.Length > 0) AudioSource.PlayClipAtPoint(clangs[Random.Range(0, clangs.Length)], Heard(to), clangVolume);
        }

        // It turns to the player and waits for them to look (a moment at most), so nobody misses it, and throws.
        private IEnumerator BossNoticesAndThrows()
        {
            var bossBody = boss.transform;
            for (float waited = 0f; waited < waitForLook; waited += Time.deltaTime)
            {
                TurnTo(bossBody, head.position, NoticeTurn);
                if (waited > noticeSeconds && LooksAtBoss()) break;
                yield return null;
            }
            yield return BossSeesThem();
            Play(boss, laughState, 0f);
            SayFromBoss(RandomLaugh());
            for (float t = 0f; t < LaughClipSeconds; t += Time.deltaTime)
            {
                TurnTo(bossBody, head.position, 70f);
                yield return null;
            }
            boss.speed = throwSpeed;
            float release = throwRelease / throwSpeed;
            int way = Random.value < 0.5f ? 1 : -1;
            bool left = Random.value < 0.5f;
            // It's still throwing while the one for their face is in the air.
            Coroutine inTheFace = null;
            for (int i = 0; i <= throwsThatMiss + 2; i++)
            {
                bool forTheFace = i == throwsThatMiss;
                var pumpkin = forTheFace ? flyingPumpkin : Instantiate(flyingPumpkin, flyingPumpkin.parent);
                Play(boss, left ? throwLeftState : throwState, 0f);
                for (float t = 0f; t < release; t += Time.deltaTime)
                {
                    TurnTo(bossBody, head.position, 70f);
                    HoldPumpkin(pumpkin, left);
                    yield return null;
                }
                left = !left;
                if (forTheFace) inTheFace = StartCoroutine(Flight());
                else
                {
                    var at = i == 0
                        ? floorPoint + facing * missShort
                        : floorPoint - facing * MissBehind + right * (way * missBy);
                    StartCoroutine(PumpkinLandsAt(pumpkin, at));
                    way = -way;
                }
                for (float t = release; t < throwEvery; t += Time.deltaTime)
                {
                    TurnTo(bossBody, head.position, 70f);
                    yield return null;
                }
            }
            yield return inTheFace;
        }

        // Its notice clip: it jolts up, roaring, "!!!" over its head, and it stares a moment.
        private IEnumerator BossSeesThem()
        {
            var body = boss.transform;
            Game.VFX.StunStars.Hide(body);
            Play(boss, noticeState, 0f);
            bool marked = false;
            for (float t = 0f; t < NoticeClipSeconds + stareSeconds; t += Time.deltaTime)
            {
                TurnTo(body, head.position, 70f);
                if (!marked && t >= noticeMarksAt)
                {
                    marked = true;
                    SayFromBoss(bossRoar);
                    if (noticeMarks != null)
                    {
                        marksOver = body.FindDeep(BossHead);
                        foreach (var mark in noticeMarks.GetComponentsInChildren<ParticleSystem>()) mark.Play();
                    }
                }
                yield return null;
            }
            marksOver = null;
        }

        private void HoldPumpkin(Transform pumpkin, bool left)
        {
            if (pumpkin == null) return;
            var hand = boss.transform.FindDeep(left ? throwingHandLeft : throwingHand);
            if (hand == null) return;
            Show(pumpkin);
            pumpkin.position = hand.position + Vector3.up * (PumpkinOnFist * bossSize);
        }

        private static void Show(Transform pumpkin)
        {
            foreach (var part in pumpkin.GetComponentsInChildren<Renderer>()) part.enabled = true;
        }

        // One that only just misses, bursting on the ground there.
        private IEnumerator PumpkinLandsAt(Transform pumpkin, Vector3 to)
        {
            if (flySound != null) AudioSource.PlayClipAtPoint(flySound, Heard(pumpkin.position));
            var from = pumpkin.position;
            var spin = Random.onUnitSphere * 240f;
            for (float t = 0f; t < flightSeconds; t += Time.deltaTime)
            {
                float k = t / flightSeconds;
                pumpkin.position = Vector3.Lerp(from, to, k) + Vector3.up * (4f * k * (1f - k) * ArcHeight);
                pumpkin.Rotate(spin * Time.deltaTime, Space.World);
                yield return null;
            }
            Destroy(pumpkin.gameObject);
            // (Not one to be seen flashing on the black.)
            if (splatEffect != null && !darkening) EffectPool.Play(splatEffect, to, NearSplatSize);
            if (splatSound != null) AudioSource.PlayClipAtPoint(splatSound, to);
        }

        // As its Bug Pumpkin flies: an arc, tumbling, at where the player's head is.
        private IEnumerator Flight()
        {
            if (flyingPumpkin == null) yield break;
            Show(flyingPumpkin);
            if (flySound != null) AudioSource.PlayClipAtPoint(flySound, Heard(flyingPumpkin.position));
            var from = flyingPumpkin.position;
            var spin = Random.onUnitSphere * 240f;
            for (float t = 0f; t < flightSeconds; t += Time.deltaTime)
            {
                float k = t / flightSeconds;
                var at = Vector3.Lerp(from, head.position, k) + Vector3.up * (4f * k * (1f - k) * ArcHeight);
                flyingPumpkin.position = at;
                flyingPumpkin.Rotate(spin * Time.deltaTime, Space.World);
                if ((head.position - at).magnitude < BreakDistance) break;
                yield return null;
            }
        }

        // Up, at the player, and down with a boom as it's black.
        private IEnumerator BossBodySlams()
        {
            boss.speed = 1f;
            Play(boss, slamState, 0f);
            SayFromBoss(bossRoar);
            var body = boss.transform;
            var from = body.position;
            var to = floorPoint + Flat(from - floorPoint).normalized * slamStopsShort;
            for (float t = 0f; t < SlamLands; t += Time.deltaTime)
            {
                body.position = Vector3.Lerp(from, to, Mathf.InverseLerp(SlamLeaves, SlamLands, t));
                yield return null;
            }
            body.position = to;
            if (punchSound != null) AudioSource.PlayClipAtPoint(punchSound, head.position);
            ViewShake.Send(ViewShake.Big);
        }

        // It breaks in the player's face, and then black; the fight's gone and the boss laughs in the dark.
        private IEnumerator LightsOut()
        {
            var at = flyingPumpkin != null ? flyingPumpkin.position : head.position + head.forward;
            if (flyingPumpkin != null) flyingPumpkin.gameObject.SetActive(false);
            if (brokenPumpkin != null) Instantiate(brokenPumpkin, at, Quaternion.LookRotation(head.position - at), transform).transform.localScale = Vector3.one * brokenSize;
            if (splashEffect != null) EffectPool.Play(splashEffect, at, splashSize);
            foreach (var clip in hitSounds)
            {
                if (clip != null) voice.PlayOneShot(clip);
            }
            yield return new WaitForSeconds(splashSeconds);
            StartCoroutine(BossBodySlams());
            yield return FadeToBlack(darkenSeconds);
            if (muffler != null) muffler.cutoffFrequency = muffledHz * 2f;
            HideStage();
            yield return new WaitForSeconds(0.5f);
            var laugh = RandomLaugh();
            if (laugh != null) voice.PlayOneShot(laugh);
            yield return new WaitForSeconds(darkSeconds);
        }

        // ---------- Setting it up ----------

        private void SetUp()
        {
            player = FindAnyObjectByType<XROrigin>();
            eyes = Camera.main;
            head = eyes != null ? eyes.transform : head;
            if (head == null || player == null)
            {
                Finish();
                return;
            }
            LockPlayer();
            MuteOtherSpeakers();

            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;
            // It only works next to the Audio Listener.
            var ears = FindAnyObjectByType<AudioListener>();
            if (ears != null)
            {
                muffler = ears.gameObject.AddComponent<AudioLowPassFilter>();
                muffler.cutoffFrequency = muffledHz;
            }

            sceneChanged = true;
            ambientBefore = RenderSettings.ambientLight;
            fogBefore = RenderSettings.fog;
            farBefore = eyes.farClipPlane;
            RenderSettings.ambientLight = ambient;
            RenderSettings.fog = false;
            eyes.farClipPlane = seeingDistance;

            // The ground is a little under their eyes, whatever their height: they're sitting on it. Their view stays theirs.
            floorPoint = head.position + Vector3.down * sittingEyeHeight;
            facing = Flat(head.forward);
            if (facing.sqrMagnitude < 0.001f) facing = Flat(head.up);
            facing.Normalize();
            right = Vector3.Cross(Vector3.up, facing);
            side = callerSide != 0 ? callerSide : Random.value < 0.5f ? 1f : -1f;

            transform.SetPositionAndRotation(floorPoint, Quaternion.LookRotation(facing));
            foreach (var part in shown)
            {
                if (part != null) part.enabled = true;
            }
            if (flyingPumpkin != null) foreach (var part in flyingPumpkin.GetComponentsInChildren<Renderer>()) part.enabled = false;

            var middle = floorPoint + facing * fieldAhead;
            if (arena != null)
            {
                arena.SetPositionAndRotation(middle, Quaternion.LookRotation(facing));
                arena.localScale *= arenaSize;
            }
            if (dreamFloor != null) dreamFloor.position = middle;
            if (sky != null) sky.position = middle;

            bossSpot = floorPoint + facing * bossDistance;
            boss.transform.localScale = Vector3.one * bossSize;
            var fightSpot = floorPoint + facing * fightAt.y - right * fightAt.x;
            boss.transform.SetPositionAndRotation(bossSpot, Quaternion.LookRotation(Flat(fightSpot - bossSpot)));
            if (fightLight != null) fightLight.transform.position = bossSpot - facing * (bossSize * 2f) + Vector3.up * (bossSize * 1.4f);

            // Over the player, looking down at them.
            var leadAt = floorPoint + right * (side * leadSideAndAhead.x) + facing * leadSideAndAhead.y;
            Place(leadKnight, leadAt, floorPoint - leadAt);
            var stuckSpot = floorPoint + facing * stuckAt.y + right * stuckAt.x;
            // Not its soles that touch the ground, this one: its clip puts the head just under it.
            Place(stuckKnight, stuckSpot + Vector3.down * (SolesBelow * knightSize), floorPoint - stuckSpot);
            Place(fightKnight, fightSpot, bossSpot - fightSpot);
            fightHome = rollEnd = fightKnight.transform.position;
            var throwerSpot = floorPoint + facing * ThrowerRuns.center.y + right * ThrowerRuns.center.x;
            Place(throwerKnight, throwerSpot, bossSpot - throwerSpot);
            ScatterPumpkins(stuckSpot, fightSpot);
            shadowed = new[] { leadKnight, stuckKnight, throwerKnight, fightKnight };
        }

        // Everywhere on the ground, the same each time; never on a Knight, the boss or the first Knight's way to it.
        private void ScatterPumpkins(Vector3 stuckSpot, Vector3 fightSpot)
        {
            var dice = new System.Random(ScatterSeed);
            for (int i = 0; i < pumpkins.Length; i++)
            {
                if (pumpkins[i] == null) continue;
                var at = floorPoint;
                for (int tries = 0; tries < 30; tries++)
                {
                    float x, y;
                    if (i < ThrowerPumpkins)
                    {
                        x = ThrowerRuns.xMin + (float)dice.NextDouble() * ThrowerRuns.width;
                        y = ThrowerRuns.yMin + (float)dice.NextDouble() * ThrowerRuns.height;
                    }
                    else
                    {
                        // All round, more of them near: from low down, the far ones only line up along the horizon.
                        float near = (float)dice.NextDouble();
                        y = 2.5f + near * near * NearToFar;
                        x = ((float)dice.NextDouble() * 2f - 1f) * (3f + y * 0.9f);
                    }
                    at = floorPoint + right * x + facing * y;
                    bool clear = Mathf.Abs(x) >= LaneWidth && (at - bossSpot).magnitude >= ClearOfBoss &&
                                 (at - stuckSpot).magnitude >= ClearOfKnights && (at - fightSpot).magnitude >= ClearOfFight;
                    if (clear) break;
                }
                pumpkins[i].SetPositionAndRotation(at + Vector3.up * pumpkinLift, Quaternion.Euler(0f, (float)dice.NextDouble() * 360f, 0f));
            }
        }

        private void Place(Animator knight, Vector3 at, Vector3 looking)
        {
            if (knight == null) return;
            // On the ground, its soles and all.
            knight.transform.SetPositionAndRotation(at + Vector3.up * (SolesBelow * knightSize), Quaternion.LookRotation(Flat(looking)));
            knight.transform.localScale = Vector3.one * knightSize;
        }

        // Nobody walks, turns, grabs or points meanwhile; their hands are free to look at.
        private void LockPlayer()
        {
            PauseMenu.BlockOpening(this);
            foreach (var interactor in player.GetComponentsInChildren<XRBaseInteractor>(true))
            {
                interactors.Add((interactor, interactor.allowSelect, interactor.allowHover));
                interactor.allowSelect = false;
                interactor.allowHover = false;
            }
            foreach (var provider in player.GetComponentsInChildren<LocomotionProvider>(true))
            {
                stopped.Add((provider, provider.enabled));
                provider.enabled = false;
            }
            foreach (var visual in player.GetComponentsInChildren<Behaviour>(true))
            {
                if (visual == null) continue;
                string kind = visual.GetType().Name;
                if (!kind.Contains("LineVisual") && !kind.Contains("CurveVisual")) continue;
                stopped.Add((visual, visual.enabled));
                visual.enabled = false;
            }
            foreach (var line in player.GetComponentsInChildren<LineRenderer>(true))
            {
                hiddenLines.Add((line, line.enabled));
                line.enabled = false;
            }
        }

        // What the scene already started (its opening line): not over this.
        private void MuteOtherSpeakers()
        {
            foreach (var speaker in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            {
                if (speaker == null || !speaker.isPlaying || speaker.mute || speaker.transform.IsChildOf(transform)) continue;
                speaker.mute = true;
                mutedSpeakers.Add(speaker);
            }
        }

        private void MakeLids()
        {
            lidsObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lidsObject.name = "Eye Lids";
            Destroy(lidsObject.GetComponent<Collider>());
            if (head != null)
            {
                lidsObject.transform.SetParent(head, false);
                lidsObject.transform.localPosition = Vector3.zero;
                lidsObject.transform.localRotation = Quaternion.identity;
            }
            lidsObject.transform.localScale = Vector3.one;
            var look = lidsObject.GetComponent<MeshRenderer>();
            look.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            look.receiveShadows = false;
            lids = new Material(lidsMaterial);
            lids.SetFloat(OpenId, 0f);
            lids.SetFloat(FadeId, 1f);
            look.sharedMaterial = lids;
        }

        // ---------- Skipping ----------

        private void WatchSkip()
        {
            if (anyButton == null || skipping) return;
            bool down = anyButton.IsPressed();
            if (waitingForRelease)
            {
                if (!down) waitingForRelease = false;
                return;
            }
            heldFor = down ? heldFor + Time.unscaledDeltaTime : 0f;
            ShowHint();
            if (heldFor < skipAfter) return;
            skipping = true;
            StopAllCoroutines();
            StartCoroutine(SkipOut());
        }

        // Shows while it's held, and fills up with colour, low in the view, following the head.
        private void ShowHint()
        {
            if (heldFor < hintAfter)
            {
                if (hint != null) hint.gameObject.SetActive(false);
                return;
            }
            if (hint == null) MakeHint();
            if (hint == null || head == null) return;
            hint.gameObject.SetActive(true);
            int done = Mathf.RoundToInt(skipText.Length * Mathf.InverseLerp(hintAfter, skipAfter, heldFor));
            hint.text = $"<color={HoldColor}>{skipText.Substring(0, done)}</color>{skipText.Substring(done)}";
            var flat = Flat(head.forward);
            if (flat.sqrMagnitude < 0.001f) flat = head.up;
            var want = head.position + flat.normalized * textAt.x + Vector3.down * textAt.y;
            hint.transform.position = Vector3.Lerp(hint.transform.position, want, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
            hint.transform.rotation = Quaternion.LookRotation(hint.transform.position - head.position);
        }

        private void MakeHint()
        {
            var font = TMP_Settings.defaultFontAsset;
            if (font == null) return;
            var go = new GameObject("Hold To Skip");
            go.transform.localScale = Vector3.one * 0.05f;
            hint = go.AddComponent<TextMeshPro>();
            hint.font = font;
            var look = Resources.Load<Material>(TextMaterial);
            if (look != null) hint.fontSharedMaterial = look;
            hint.fontSize = 10f;
            hint.alignment = TextAlignmentOptions.Center;
            hint.textWrappingMode = TextWrappingModes.NoWrap;
            hint.richText = true;
            hint.color = Color.white;
            hint.fontMaterial.renderQueue = TextQueue;
            if (head != null) go.transform.position = head.position + head.forward * textAt.x + Vector3.down * textAt.y;
        }

        // Black quickly, then on with the game.
        private IEnumerator SkipOut()
        {
            yield return FadeToBlack(0.25f);
            HideStage();
            Finish();
        }

        // Plain black, all over: not the eyelids closing. (Skipped while they're still half shut, it goes black at once.)
        private IEnumerator FadeToBlack(float seconds)
        {
            darkening = true;
            bool fade = !lidsObject.activeSelf;
            lids.SetFloat(OpenId, 0f);
            lids.SetFloat(FadeId, fade ? 0f : 1f);
            lidsObject.SetActive(true);
            for (float t = 0f; fade && t < seconds; t += Time.deltaTime)
            {
                lids.SetFloat(FadeId, t * t / (seconds * seconds));
                yield return null;
            }
            lids.SetFloat(FadeId, 1f);
        }

        // ---------- Ending ----------

        private void HideStage()
        {
            if (boss != null) Game.VFX.StunStars.Hide(boss.transform);
            foreach (var part in GetComponentsInChildren<Renderer>()) part.enabled = false;
            foreach (var part in GetComponentsInChildren<ParticleSystem>()) part.gameObject.SetActive(false);
            foreach (var part in GetComponentsInChildren<Light>()) part.enabled = false;
        }

        // The scene's own light and fog and the player come back; the dark lifts as the health warning comes.
        private void Finish()
        {
            if (over) return;
            over = true;
            StartCoroutine(Lift());
        }

        private IEnumerator Lift()
        {
            if (hint != null) Destroy(hint.gameObject);
            if (muffler != null) Destroy(muffler);
            if (sceneChanged)
            {
                RenderSettings.ambientLight = ambientBefore;
                RenderSettings.fog = fogBefore;
                if (eyes != null) eyes.farClipPlane = farBefore;
            }
            HideStage();
            foreach (var (interactor, select, hover) in interactors)
            {
                if (interactor == null) continue;
                interactor.allowSelect = select;
                interactor.allowHover = hover;
            }
            foreach (var (behaviour, enabled) in stopped)
            {
                if (behaviour != null) behaviour.enabled = enabled;
            }
            foreach (var (line, enabled) in hiddenLines)
            {
                if (line != null) line.enabled = enabled;
            }
            foreach (var speaker in mutedSpeakers)
            {
                if (speaker != null) speaker.mute = false;
            }
            PauseMenu.AllowOpening(this);
            // The warning can go on now.
            current = null;

            const float seconds = 0.8f;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                lids.SetFloat(FadeId, 1f - t / seconds);
                yield return null;
            }
            if (lidsObject != null) Destroy(lidsObject);
            Destroy(lids);
            Destroy(gameObject);
        }

        // ---------- Helpers ----------

        private bool LooksAtBoss()
        {
            var to = bossSpot + Vector3.up * (bossSize * BossChest) - head.position;
            return Vector3.Angle(head.forward, to) <= LookAngle;
        }

        // The field is so wide that a one-shot sound at its far end isn't heard: it plays a good way nearer.
        private Vector3 Heard(Vector3 at) => Vector3.Lerp(at, head.position, 0.7f);

        private static void TurnTo(Transform body, Vector3 point, float degreesPerSecond)
        {
            var to = Flat(point - body.position);
            if (to.sqrMagnitude < 0.001f) return;
            body.rotation = Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(to), degreesPerSecond * Time.deltaTime);
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        private static void Play(Animator animator, string state, float at)
        {
            if (animator != null) animator.CrossFadeInFixedTime(state, 0.2f, 0, at * 0.5f);
        }

        private AudioClip RandomLaugh() => bossLaughs.Length > 0 ? bossLaughs[Random.Range(0, bossLaughs.Length)] : null;

        private void SayFromBoss(AudioClip clip)
        {
            if (clip != null && boss.TryGetComponent<AudioSource>(out var speaker)) speaker.PlayOneShot(clip);
        }
    }

    internal static class OpeningFind
    {
        /// <summary>The child with this name, however deep.</summary>
        public static Transform FindDeep(this Transform parent, string name)
        {
            foreach (var part in parent.GetComponentsInChildren<Transform>(true))
            {
                if (part.name == name) return part;
            }
            return null;
        }
    }
}

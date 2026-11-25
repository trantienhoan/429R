using UnityEngine;

namespace Game.Story
{
    /// <summary>
    /// The Fairy's new ways (user 2026-11-25): her teleport (a green flash), dodging the first pumpkin, the send-off lines
    /// when she leaves ("Good luck", "Have fun", "Be careful out there": the user's recordings, to come), how she turns
    /// to look at the board's card or the player, and her free idle. Assets/Resources/Story Fairy Settings.asset (made by
    /// Tools > 429 Game > Story > Set Up Fairy Acts). Story Fairy Acts and Story Fairy Board read it.
    /// </summary>
    [CreateAssetMenu(menuName = "429 Game/Story/Story Fairy Settings", fileName = "Story Fairy Settings")]
    public class StoryFairySettings : ScriptableObject
    {
        private const string ResourceName = "Story Fairy Settings";

        [Header("Her teleport")]
        [Tooltip("The green flash left where she was (Lana's Flash_heal_green).")]
        [SerializeField] private GameObject flashPrefab;
        [Tooltip("Size of the flash against the prefab's own.")]
        [Min(0.01f)] [SerializeField] private float flashScale = 1f;
        [Tooltip("The flash's light: colour.")]
        [SerializeField] private Color flashColour = new Color(0.45f, 1f, 0.5f, 1f);
        [Tooltip("The flash's light: strength at its brightest.")]
        [Min(0f)] [SerializeField] private float flashIntensity = 6f;
        [Tooltip("The flash's light: how far it reaches, in metres.")]
        [Min(0.5f)] [SerializeField] private float flashRange = 7f;
        [Tooltip("Seconds the flash's light takes to go out.")]
        [Min(0.05f)] [SerializeField] private float flashSeconds = 0.7f;
        [Tooltip("Her teleport's sound (played where she was).")]
        [SerializeField] private AudioClip teleportSound;
        [Tooltip("Her teleport's sounds: one at random each time (fairy_teleport_blink_1/2). Empty = Teleport Sound.")]
        [SerializeField] private AudioClip[] teleportSounds = new AudioClip[0];
        [Range(0f, 1f)] [SerializeField] private float teleportVolume = 1f;

        [Header("Her send-off lines (the user's recordings)")]
        [Tooltip("One is said at random as she leaves after a pick on her board: \"Good luck\", \"Have fun\", " +
                 "\"Be careful out there\". Empty = only the teleport sound.")]
        [SerializeField] private AudioClip[] sendOffLines = new AudioClip[0];
        [Range(0f, 1f)] [SerializeField] private float lineVolume = 1f;
        [Tooltip("Seconds between the start of her line and her teleport (she says it, then goes).")]
        [Min(0f)] [SerializeField] private float lineLead = 0.6f;
        [Tooltip("She stays until her send-off line is said: her flash comes this many seconds before the line's end " +
                 "(the recording's own quiet end).")]
        [Min(0f)] [SerializeField] private float lineTail = 0.2f;

        [Header("Her other lines (the user's recordings)")]
        [Tooltip("One at random when her board opens (\"Oh! Hello, Knight!\"). Empty = none.")]
        [SerializeField] private AudioClip[] helloLines = new AudioClip[0];
        [Tooltip("Her hello isn't said again sooner than this (seconds), so opening her board again and again stays quiet.")]
        [Min(0f)] [SerializeField] private float helloEvery = 20f;
        [Tooltip("After the final boss, while she cheers: one at random (FairyHealth Win, Story Fairy Say).")]
        [SerializeField] private AudioClip[] thankYouLines = new AudioClip[0];
        [Tooltip("After the final boss, before she teleports away: said one after another (FairyHealth Win 2, Story " +
                 "Fairy Say: the state waits for them).")]
        [SerializeField] private AudioClip[] talkLines = new AudioClip[0];
        [Tooltip("Seconds of quiet between two of her talk lines.")]
        [Min(0f)] [SerializeField] private float talkGap = 0.4f;

        [Header("Dodging the first pumpkin")]
        [Tooltip("She teleports when the falling pumpkin (it drops from about 86 m, fast) is this many seconds from her.")]
        [Min(0f)] [SerializeField] private float dodgeLead = 0.35f;
        [Tooltip("...or at the latest when it is this close above her middle (metres).")]
        [Min(0f)] [SerializeField] private float dodgeHeight = 1.5f;
        [Tooltip("Only a pumpkin this close to her along the floor counts (metres).")]
        [Min(0.1f)] [SerializeField] private float dodgeReach = 2.5f;

        [Header("Looking")]
        [Tooltip("How fast she turns her body to what she looks at (1/s; higher = quicker).")]
        [Min(0.1f)] [SerializeField] private float turnSpeed = 6f;
        [Tooltip("Turn added to her body so her face looks at the target (degrees), if the model faces another way.")]
        [SerializeField] private float faceYaw = 0f;
        [Tooltip("Her head turns and tips at most this much toward what she looks at (degrees); 0 = body only. Her body " +
                 "always faces the player.")]
        [Range(0f, 80f)] [SerializeField] private float headTilt = 35f;
        [Tooltip("Her head bone's name.")]
        [SerializeField] private string headBone = "head";
        [Tooltip("Her head's tilt axis: right = the body's right (flip the sign if she looks the wrong way up/down).")]
        [SerializeField] private float headTiltSign = -1f;

        [Header("Her free idle")]
        [Tooltip("Her Animator state while free (Fairy_Controller): the Fairy_Girl_FreeIdle hover. Empty or missing = " +
                 "as before.")]
        [SerializeField] private string idleState = "free_idle";
        [Tooltip("Her Animator states for the moments (Fairy_Controller): hello when her board opens, good luck before " +
                 "she leaves after a pick, teleport right before every flash.")]
        [SerializeField] private string helloState = "hello";
        [SerializeField] private string goodLuckState = "goodluck";
        [SerializeField] private string teleportState = "teleport";
        [Tooltip("Her clips' names, for their lengths (Fairy_Girl.fbx).")]
        [SerializeField] private string goodLuckClip = "Fairy_Girl_GoodLuck";
        [SerializeField] private string teleportClip = "Fairy_Girl_Teleport";
        [Tooltip("Seconds her clips blend into each other.")]
        [Min(0f)] [SerializeField] private float fade = 0.15f;
        [Tooltip("While free, her yellow distress bolts (the Sound_Bone part of her mesh) stay hidden whatever clip plays.")]
        [SerializeField] private bool hideBolts = true;
        [Tooltip("The bone that carries her distress bolts.")]
        [SerializeField] private string boltBone = "Sound_Bone";
        [Tooltip("Pointing at her and clicking brings up her board from this far (metres); touching still works.")]
        [Min(0.5f)] [SerializeField] private float clickReach = 3.5f;

        public string HelloState => helloState;
        public string GoodLuckState => goodLuckState;
        public string TeleportState => teleportState;
        public string GoodLuckClip => goodLuckClip;
        public string TeleportClip => teleportClip;
        public float Fade => fade;
        public bool HideBolts => hideBolts;
        public string BoltBone => boltBone;
        public GameObject FlashPrefab => flashPrefab;
        public float FlashScale => flashScale;
        public Color FlashColour => flashColour;
        public float FlashIntensity => flashIntensity;
        public float FlashRange => flashRange;
        public float FlashSeconds => flashSeconds;
        public AudioClip TeleportSound => teleportSound;
        public float TeleportVolume => teleportVolume;
        public AudioClip[] SendOffLines => sendOffLines;
        public float LineVolume => lineVolume;
        public float LineLead => lineLead;
        public float LineTail => lineTail;
        public AudioClip[] HelloLines => helloLines;
        public float HelloEvery => helloEvery;
        public AudioClip[] ThankYouLines => thankYouLines;
        public AudioClip[] TalkLines => talkLines;
        public float TalkGap => talkGap;
        public float DodgeLead => dodgeLead;
        public float DodgeHeight => dodgeHeight;
        public float DodgeReach => dodgeReach;
        public float TurnSpeed => turnSpeed;
        public float FaceYaw => faceYaw;
        public float HeadTilt => headTilt;
        public string HeadBone => headBone;
        public float HeadTiltSign => headTiltSign;
        public string IdleState => idleState;
        public float ClickReach => clickReach;

        /// <summary>A send-off line at random, or null when there are none yet.</summary>
        public AudioClip RandomLine() => Pick(sendOffLines);

        /// <summary>Her teleport sound: one of Teleport Sounds at random, or Teleport Sound.</summary>
        public AudioClip RandomTeleportSound() => Pick(teleportSounds) ?? teleportSound;

        /// <summary>One of the clips at random (empty slots skipped), or null when there are none.</summary>
        public static AudioClip Pick(AudioClip[] clips)
        {
            if (clips == null) return null;
            int count = 0;
            foreach (var clip in clips) if (clip != null) count++;
            if (count == 0) return null;
            int pick = Random.Range(0, count);
            foreach (var clip in clips)
            {
                if (clip == null) continue;
                if (pick-- == 0) return clip;
            }
            return null;
        }

        private static StoryFairySettings instance;

        /// <summary>The settings in Resources, or the defaults when there are none.</summary>
        public static StoryFairySettings Instance
        {
            get
            {
                if (instance != null) return instance;
                instance = Resources.Load<StoryFairySettings>(ResourceName);
                if (instance == null) instance = CreateInstance<StoryFairySettings>();
                return instance;
            }
        }

#if UNITY_EDITOR
        /// <summary>Editor menu: fills the empty slots (the flash and her teleport sound).</summary>
        public void FillDefaults(GameObject flash, AudioClip teleport)
        {
            if (flashPrefab == null) flashPrefab = flash;
            if (teleportSound == null) teleportSound = teleport;
        }
#endif
    }
}

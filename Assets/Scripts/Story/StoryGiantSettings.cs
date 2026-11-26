using UnityEngine;

namespace Game.Story
{
    /// <summary>
    /// Two story hints about the giant pumpkin (user 2026-11-26, visual story ideas 6 and 7), Story Giant Hints reads it:
    /// - the watcher: now and then its glowing eyes drift past a window (the bedroom kit's), or open out in the dark while
    ///   a room's walls are down for the fight; bigger as the run goes on;
    /// - the laugh's shadow: each laugh of a room's laughing pumpkin throws the giant's laughing shadow on the wall behind
    ///   it: what it will become.
    /// Assets/Resources/Story Giant Settings.asset (Tools > 429 Game > Story > Set Up Story Hints). Main game rooms only.
    /// </summary>
    [CreateAssetMenu(menuName = "429 Game/Story/Story Giant Settings", fileName = "Story Giant Settings")]
    public class StoryGiantSettings : ScriptableObject
    {
        private const string ResourceName = "Story Giant Settings";

        [Header("The giant (a picture of pumpkin_boss_Z laughing, made by the setup menu)")]
        [SerializeField] private Sprite silhouette;
        [Tooltip("Where its eyes are on the picture (0-1 across, 0-1 up).")]
        [SerializeField] private Vector2 leftEye = new Vector2(0.42f, 0.8f);
        [SerializeField] private Vector2 rightEye = new Vector2(0.58f, 0.8f);
        [Tooltip("The material its picture is drawn with (Sprites/Default: black, see-through).")]
        [SerializeField] private Material shadowMaterial;
        [Tooltip("The glow of its eyes: a soft round picture and an additive material (Lana's Circle01 / Circles_Additive).")]
        [SerializeField] private Texture2D eyeTexture;
        [SerializeField] private Material eyeMaterial;
        [SerializeField] private Color eyeColor = new Color(1f, 0.55f, 0.1f, 1f);

        [Header("It grows as the run goes on: one step per seed piece taken (these GAMESTAGES bools)")]
        [SerializeField] private string[] growBools = { "Bracer_Gear", "Shield_Gear", "Head_Gear", "Chest_Gear" };
        [SerializeField] private float startScale = 0.85f;
        [SerializeField] private float scalePerPiece = 0.3f;

        [Header("The watcher")]
        [Tooltip("Window pieces it can pass behind (by name).")]
        [SerializeField] private string[] windowNames = { "BR_Kit_Window" };
        [Tooltip("The fights (GAMESTAGES states while a room's walls are down): its eyes can open out in the dark then.")]
        [SerializeField] private string[] fightStates = { "Mini Enemy", "Enemy1 - Wait", "Enemy2 - Wait", "Enemy 3 Stuffs" };
        [Tooltip("The player must look at a window (degrees off, metres away) or into the dark (nothing within this many metres).")]
        [SerializeField] private float lookAngle = 22f;
        [SerializeField] private float windowReach = 9f;
        [SerializeField] private float darkClear = 12f;
        [Tooltip("How far outside it is: behind a window, in the dark (metres).")]
        [SerializeField] private float behindWindow = 3f;
        [SerializeField] private float inTheDark = 16f;
        [Tooltip("Height of the giant's picture at scale 1 (metres).")]
        [SerializeField] private float watcherHeight = 3.2f;
        [Tooltip("Chance each half second while the player looks (0-1), and the quiet seconds after it was seen.")]
        [Range(0f, 1f)] [SerializeField] private float watchChance = 0.06f;
        [SerializeField] private Vector2 watchEvery = new Vector2(60f, 120f);
        [Tooltip("Seconds it stays (fading in, drifting, blinking once, fading out) and metres it drifts sideways.")]
        [SerializeField] private float watchSeconds = 4.5f;
        [SerializeField] private float drift = 2.2f;
        [Range(0f, 1f)] [SerializeField] private float watcherDarkness = 0.9f;

        [Header("The laugh's shadow")]
        [Tooltip("Height of the shadow at scale 1 (metres), how dark it is, and how far behind the pumpkin a wall may be.")]
        [SerializeField] private float shadowHeight = 2.6f;
        [Range(0f, 1f)] [SerializeField] private float shadowDarkness = 0.65f;
        [SerializeField] private float wallReach = 6f;
        [Tooltip("Only a surface at least this tall counts as a wall (metres).")]
        [SerializeField] private float tallestFurniture = 1.8f;
        [Tooltip("Seconds it takes to come and to go.")]
        [SerializeField] private float shadowIn = 0.2f;
        [SerializeField] private float shadowOut = 0.7f;

        public Sprite Silhouette => silhouette;
        public Vector2 LeftEye => leftEye;
        public Vector2 RightEye => rightEye;
        public Material ShadowMaterial => shadowMaterial;
        public Texture2D EyeTexture => eyeTexture;
        public Material EyeMaterial => eyeMaterial;
        public Color EyeColor => eyeColor;
        public string[] GrowBools => growBools;
        public float StartScale => startScale;
        public float ScalePerPiece => scalePerPiece;
        public string[] WindowNames => windowNames;
        public string[] FightStates => fightStates;
        public float LookAngle => lookAngle;
        public float WindowReach => windowReach;
        public float DarkClear => darkClear;
        public float BehindWindow => behindWindow;
        public float InTheDark => inTheDark;
        public float WatcherHeight => watcherHeight;
        public float WatchChance => watchChance;
        public Vector2 WatchEvery => watchEvery;
        public float WatchSeconds => watchSeconds;
        public float Drift => drift;
        public float WatcherDarkness => watcherDarkness;
        public float ShadowHeight => shadowHeight;
        public float ShadowDarkness => shadowDarkness;
        public float WallReach => wallReach;
        public float TallestFurniture => tallestFurniture;
        public float ShadowIn => shadowIn;
        public float ShadowOut => shadowOut;

        private static StoryGiantSettings instance;
        private static bool looked;

        /// <summary>The settings in Resources, or null (then no hints).</summary>
        public static StoryGiantSettings Instance
        {
            get
            {
                if (!looked)
                {
                    instance = Resources.Load<StoryGiantSettings>(ResourceName);
                    looked = true;
                }
                return instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            looked = false;
        }
    }
}

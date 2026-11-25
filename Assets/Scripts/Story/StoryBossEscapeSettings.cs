using UnityEngine;

namespace Game.Story
{
    /// <summary>
    /// The room's mini boss escaping (user, 2026-11-25: "when the laughing pumpkin of that room break, the miniboss of
    /// that room will pop out of that laughing pumpkin and quickly run away, dig to the ground even, then EnemyManager
    /// starts normally and the flow is kept the same"). The laughing pumpkin held a small piece of the Green Seed; the
    /// boss carries it now. The game reads Assets/Resources/Story Boss Escape Settings.asset; without it, these defaults
    /// with no effects or sounds. Tools > 429 Game > Story > Set Up Boss Escape makes it.
    /// </summary>
    [CreateAssetMenu(menuName = "429 Game/Story/Story Boss Escape Settings", fileName = "Story Boss Escape Settings")]
    public class StoryBossEscapeSettings : ScriptableObject
    {
        [Header("Popping out of the laughing pumpkin")]
        [Tooltip("The puff where the pumpkin broke as the boss pops out.")]
        [SerializeField] private GameObject popPuff;
        [Min(0f)]
        [SerializeField] private float popPuffSize = 0.35f;
        [SerializeField] private AudioClip popSound;
        [Tooltip("Seconds it takes to pop out to its full size.")]
        [Min(0.05f)]
        [SerializeField] private float popSeconds = 0.35f;
        [Tooltip("Seconds it rears up and hisses at the player (its own alert look and sound) before it runs.")]
        [Min(0f)]
        [SerializeField] private float hissSeconds = 0.8f;

        [Header("Running off")]
        [Tooltip("Its running speed, in metres a second.")]
        [Min(0.5f)]
        [SerializeField] private float runSpeed = 4f;
        [Tooltip("How far from the broken pumpkin it digs in, in metres (nearest, farthest).")]
        [SerializeField] private Vector2 runDistance = new(2.5f, 4.5f);
        [Tooltip("It digs in at least this far from the player, in metres along the floor.")]
        [Min(0f)]
        [SerializeField] private float keepFromPlayer = 2.5f;
        [Tooltip("Its hole keeps this far from walls and furniture (the floor's edge), in metres.")]
        [Min(0f)]
        [SerializeField] private float wallGap = 0.45f;

        [Header("Digging in")]
        [Tooltip("Seconds it digs: first its legs scratch (its run, fast, on the spot), then it dives under the floor " +
                 "(its climbing-out look played backwards).")]
        [Min(0.3f)]
        [SerializeField] private float digSeconds = 1.3f;
        [Tooltip("Share of the dig spent scratching before it dives.")]
        [Range(0f, 0.9f)]
        [SerializeField] private float scratchShare = 0.45f;
        [Tooltip("Dirt thrown up while it digs (one every Dust Every seconds).")]
        [SerializeField] private GameObject digDust;
        [Min(0f)]
        [SerializeField] private float digDustSize = 0.6f;
        [Min(0.05f)]
        [SerializeField] private float dustEvery = 0.2f;
        [SerializeField] private AudioClip digSound;
        [Range(0f, 1f)]
        [SerializeField] private float digVolume = 0.8f;
        [Tooltip("The hole it leaves in the floor (Story_Burrow_Hole). Its size follows the boss's.")]
        [SerializeField] private GameObject hole;
        [Tooltip("The hole's radius as a share of the boss's body length.")]
        [Min(0.1f)]
        [SerializeField] private float holeSize = 0.45f;

        [Header("Coming back")]
        [Tooltip("When its EnemyManager calls it at the end of the room's waves, it climbs out of its hole (instead of " +
                 "its usual spot).")]
        [SerializeField] private bool comeBackFromHole = true;
        [Tooltip("Dirt thrown up as it climbs out (the dig dust when empty).")]
        [SerializeField] private GameObject comeBackDust;
        [Tooltip("Seconds after it climbs out that the hole closes.")]
        [Min(0f)]
        [SerializeField] private float holeClosesAfter = 4f;

        [Header("The seed piece it carries (the laughing pumpkin's piece of the Green Seed)")]
        [Tooltip("A seed prefab shown on its back, glowing green (only its glow shows: nothing to take). Empty = none.")]
        [SerializeField] private GameObject seedPiece;
        [Tooltip("The piece's size, times the seed prefab's own.")]
        [Min(0.1f)]
        [SerializeField] private float seedSize = 1.2f;
        [Tooltip("The piece floats this far above its back, in metres.")]
        [SerializeField] private float seedAbove = 0.06f;
        [Tooltip("The real boss carries it too while the room's fight goes on (also a chaos one coming back).")]
        [SerializeField] private bool seedOnBoss = true;

        [Header("Sound")]
        [Range(0f, 1f)]
        [SerializeField] private float volume = 1f;

        private const string ResourcePath = "Story Boss Escape Settings";
        private static StoryBossEscapeSettings instance;

        /// <summary>The settings in Resources (or the defaults).</summary>
        public static StoryBossEscapeSettings Instance
        {
            get
            {
                if (instance != null) return instance;
                instance = Resources.Load<StoryBossEscapeSettings>(ResourcePath);
                if (instance == null)
                {
                    instance = CreateInstance<StoryBossEscapeSettings>();
                    instance.hideFlags = HideFlags.HideAndDontSave;
                }
                return instance;
            }
        }

        public GameObject PopPuff => popPuff;
        public float PopPuffSize => popPuffSize;
        public AudioClip PopSound => popSound;
        public float PopSeconds => popSeconds;
        public float HissSeconds => hissSeconds;
        public float RunSpeed => runSpeed;
        public Vector2 RunDistance => runDistance;
        public float KeepFromPlayer => keepFromPlayer;
        public float WallGap => wallGap;
        public float DigSeconds => digSeconds;
        public float ScratchShare => scratchShare;
        public GameObject DigDust => digDust;
        public float DigDustSize => digDustSize;
        public float DustEvery => dustEvery;
        public AudioClip DigSound => digSound;
        public float DigVolume => digVolume;
        public GameObject Hole => hole;
        public float HoleSize => holeSize;
        public bool ComeBackFromHole => comeBackFromHole;
        public GameObject ComeBackDust => comeBackDust != null ? comeBackDust : digDust;
        public float HoleClosesAfter => holeClosesAfter;
        public GameObject SeedPiece => seedPiece;
        public float SeedSize => seedSize;
        public float SeedAbove => seedAbove;
        public bool SeedOnBoss => seedOnBoss;
        public float Volume => volume;

#if UNITY_EDITOR
        /// <summary>For the setup menu: fills the empty slots only (tuned values stay).</summary>
        public void FillEmpty(GameObject puff, AudioClip pop, GameObject dust, AudioClip dig, GameObject holePrefab, GameObject seed)
        {
            if (popPuff == null) popPuff = puff;
            if (popSound == null) popSound = pop;
            if (digDust == null) digDust = dust;
            if (digSound == null) digSound = dig;
            if (hole == null) hole = holePrefab;
            if (seedPiece == null) seedPiece = seed;
        }

        /// <summary>For the setup menu: the dig dust becomes Dust unless someone picked another one than Old.</summary>
        public bool UseDigDust(GameObject old, GameObject dust)
        {
            if (dust == null || digDust == dust || (digDust != null && digDust != old)) return false;
            digDust = dust;
            return true;
        }
#endif
    }
}

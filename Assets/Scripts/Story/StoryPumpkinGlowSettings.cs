using UnityEngine;

namespace Game.Story
{
    /// <summary>
    /// The laughing pumpkin's green glow (user's story, 2026-11-25: a room's small laughing pumpkin holds a small piece of
    /// the Green Seed and runs and hides until it has taken in the power of the Green, then turns into a giant pumpkin
    /// monster). Its candle-lit eyes and grin turn green and the seed's glow seeps out around it, faint at first and
    /// stronger as its countdown to the giant runs (Warn Before Transform), flaring with each laugh. The game reads
    /// Assets/Resources/Story Pumpkin Glow Settings.asset (made by Tools > 429 Game > Story > Set Up Laughing Pumpkin Glow);
    /// without it, no glow.
    /// </summary>
    [CreateAssetMenu(menuName = "429 Game/Story/Story Pumpkin Glow Settings", fileName = "Story Pumpkin Glow Settings")]
    public class StoryPumpkinGlowSettings : ScriptableObject
    {
        [Header("Its face (the candle light in its eyes and grin)")]
        [Tooltip("The eyes and grin only (made from the pumpkin's mesh by the setup menu), laid over the face.")]
        [SerializeField] private Mesh faceMesh;
        [Tooltip("Additive, its mask is the face's candle-lit parts.")]
        [SerializeField] private Material faceMaterial;
        [SerializeField] private Color glow = new(0.2f, 1f, 0.25f, 1f);
        [Tooltip("How bright the face glows when the countdown starts, and at its end (times Glow).")]
        [SerializeField] private Vector2 faceBrightness = new(0.35f, 1.8f);

        [Header("The seed's glow seeping out (seen from every side)")]
        [Tooltip("A seed prefab: only its glow particles show, tinted green; it sits inside the pumpkin. Empty = none.")]
        [SerializeField] private GameObject seedGlow;
        [Tooltip("The glow's size when the countdown starts, and at its end (times the seed's own size).")]
        [SerializeField] private Vector2 seedGlowSize = new(0.6f, 1.05f);

        [Header("Over the countdown")]
        [Tooltip("How the glow grows from start (0) to the giant (1): 1 = evenly, more = slow first and fast at the end.")]
        [Min(0.2f)]
        [SerializeField] private float growth = 1.6f;
        [Tooltip("In the last seconds of the countdown (its Warn Before Transform warnings) it throbs this much...")]
        [Range(0f, 1f)]
        [SerializeField] private float throb = 0.35f;
        [Tooltip("...this many times a second at first, and at the end.")]
        [SerializeField] private Vector2 throbSpeed = new(1f, 5f);
        [Min(0f)]
        [SerializeField] private float throbLast = 60f;

        [Header("Each laugh (from hiding)")]
        [Tooltip("A laugh flares the glow up by this much (1 = to full), fading over Flare Seconds.")]
        [Range(0f, 1f)]
        [SerializeField] private float laughFlare = 0.5f;
        [Min(0.1f)]
        [SerializeField] private float flareSeconds = 1.5f;

        [Tooltip("Seconds the glow takes to fade when the countdown stops (a pumpkin with no countdown never glows).")]
        [Min(0.05f)]
        [SerializeField] private float fadeSeconds = 0.5f;

        private const string ResourcePath = "Story Pumpkin Glow Settings";
        private static StoryPumpkinGlowSettings instance;
        private static bool looked;

        /// <summary>The settings in Resources, or null (no glow).</summary>
        public static StoryPumpkinGlowSettings Instance
        {
            get
            {
                if (instance != null || looked) return instance;
                looked = true;
                instance = Resources.Load<StoryPumpkinGlowSettings>(ResourcePath);
                return instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Forget()
        {
            looked = false;
            instance = null;
        }

        public Mesh FaceMesh => faceMesh;
        public Material FaceMaterial => faceMaterial;
        public Color Glow => glow;
        public Vector2 FaceBrightness => faceBrightness;
        public GameObject SeedGlow => seedGlow;
        public Vector2 SeedGlowSize => seedGlowSize;
        public float Growth => growth;
        public float Throb => throb;
        public Vector2 ThrobSpeed => throbSpeed;
        public float ThrobLast => throbLast;
        public float LaughFlare => laughFlare;
        public float FlareSeconds => flareSeconds;
        public float FadeSeconds => fadeSeconds;

#if UNITY_EDITOR
        /// <summary>For the setup menu: the made face mesh and material, and the seed when none is picked.</summary>
        public void Fill(Mesh mesh, Material material, GameObject seed)
        {
            faceMesh = mesh;
            faceMaterial = material;
            if (seedGlow == null) seedGlow = seed;
        }
#endif
    }
}

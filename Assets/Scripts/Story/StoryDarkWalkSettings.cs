using UnityEngine;

namespace Game.Story
{
    /// <summary>
    /// The settings of the story's dark walk (user 2026-11-25: "re-do the 1st tutorial stage a little bit, make everything
    /// dark except the fairy, so player have to walk in the dark mostly with only fairy is the light source"; the story:
    /// "This place is dark ... Walking in the dark, the voice keeps getting louder ... Then I see it, a light, a tiny girl
    /// with a green light"). Assets/Resources/Story Dark Walk Settings.asset (made by Tools > 429 Game > Story > Set Up
    /// Dark Walk). Story Dark Walk reads it.
    /// </summary>
    [CreateAssetMenu(menuName = "429 Game/Story/Story Dark Walk Settings", fileName = "Story Dark Walk Settings")]
    public class StoryDarkWalkSettings : ScriptableObject
    {
        private const string ResourceName = "Story Dark Walk Settings";

        [Header("The dark")]
        [Tooltip("Share of the ambient light left in the dark (0 = black).")]
        [Range(0f, 1f)] [SerializeField] private float ambientLeft = 0.03f;
        [Tooltip("Share of the sky (moon, stars) left in the dark.")]
        [Range(0f, 1f)] [SerializeField] private float skyLeft = 0.02f;
        [Tooltip("Share of every other light (the rooftop's lamp, the candles) left in the dark.")]
        [Range(0f, 1f)] [SerializeField] private float lightsLeft = 0f;
        [Tooltip("Self-lit particles (candle flames, the dream smoke) are hidden in the dark, so the Fairy is the only light.")]
        [SerializeField] private bool hideFlames = true;
        [Tooltip("Particles this close to her (metres) stay: her beam and ring.")]
        [Min(0f)] [SerializeField] private float keepNearHer = 2.5f;

        [Tooltip("GAMESTAGES states the dark belongs to; in any other state it ends by itself (a checkpoint jump or a " +
                 "shortcut that skips \"Send death\" must not leave the run dark).")]
        [SerializeField] private string[] darkStates = { "Init - Stage1", "Resume?", "Walk Tuttorial", "Move to Fairy" };
        [Tooltip("Seconds the dark takes to go when it ends by itself.")]
        [Min(0.01f)] [SerializeField] private float selfEndSeconds = 2.5f;

        [Header("The Fairy's light")]
        [Tooltip("Her light's colour.")]
        [SerializeField] private Color fairyColour = new Color(0.45f, 1f, 0.55f, 1f);
        [Tooltip("Her light's strength at her normal (weak) glow.")]
        [Min(0f)] [SerializeField] private float fairyIntensity = 2.6f;
        [Tooltip("How far her light reaches, in metres.")]
        [Min(0.5f)] [SerializeField] private float fairyRange = 6f;
        [Tooltip("Her light breathes and nearly goes out with her weak glow (Story Weak Fairy).")]
        [SerializeField] private bool followHerGlow = true;
        [Tooltip("The least her light gets while her glow nearly goes out (against normal), so she never vanishes.")]
        [Range(0f, 1f)] [SerializeField] private float leastShare = 0.2f;

        public float AmbientLeft => ambientLeft;
        public float SkyLeft => skyLeft;
        public float LightsLeft => lightsLeft;
        public bool HideFlames => hideFlames;
        public float KeepNearHer => keepNearHer;
        public string[] DarkStates => darkStates;
        public float SelfEndSeconds => selfEndSeconds;
        public Color FairyColour => fairyColour;
        public float FairyIntensity => fairyIntensity;
        public float FairyRange => fairyRange;
        public bool FollowHerGlow => followHerGlow;
        public float LeastShare => leastShare;

        private static StoryDarkWalkSettings instance;

        /// <summary>The settings in Resources, or the defaults when there are none.</summary>
        public static StoryDarkWalkSettings Instance
        {
            get
            {
                if (instance != null) return instance;
                instance = Resources.Load<StoryDarkWalkSettings>(ResourceName);
                if (instance == null) instance = CreateInstance<StoryDarkWalkSettings>();
                return instance;
            }
        }
    }
}

using UnityEngine;

namespace Game.Story
{
    /// <summary>
    /// The laugh that calls something (user's story, 2026-11-23: "each time that pumpkin laughs, the light on the ceiling
    /// gets weird, the room gets colder ... the laughing sound is what calling it"). Read by Laugh Chill from
    /// Assets/Resources/Story Laugh Settings.asset; without it, these defaults. Main game rooms only.
    /// </summary>
    [CreateAssetMenu(menuName = "429 Game/Story/Story Laugh Settings", fileName = "Story Laugh Settings")]
    public class StoryLaughSettings : ScriptableObject
    {
        [Header("Which rooms")]
        [Tooltip("A laugh counts only in a room whose base object's name starts with one of these (the main game's rooms); " +
                 "their one Point Light is the ceiling light. The start rooftop and the gates have none.")]
        [SerializeField] private string[] roomBases = { "Bedroom_Base", "Livingroom_Base" };
        [Tooltip("The ceiling light must be this close to the laughing pumpkin, in metres.")]
        [Min(1f)]
        [SerializeField] private float lightRange = 25f;

        [Header("When the player dies: the laughing pumpkin's HEE HEE (user 2026-11-26)")]
        [Tooltip("One at random as the HEE HEE picture comes up (Game Over Hold), never the same twice in a row. Empty = none.")]
        [SerializeField] private AudioClip[] deathLaughs = new AudioClip[0];
        [Range(0f, 1f)]
        [SerializeField] private float deathLaughVolume = 0.9f;

        [Header("The light gets weird: its own Lamp Flicker stutters at each laugh, and it turns this colour")]
        [Tooltip("The colour the ceiling light turns toward while the laugh sounds.")]
        [SerializeField] private Color weirdColor = new Color(0.62f, 0.75f, 1f, 1f);
        [Range(0f, 1f)]
        [SerializeField] private float weirdMix = 0.45f;
        [Tooltip("Seconds the light's colour takes to come back after the laugh (or when the player sees the pumpkin).")]
        [Min(0.05f)]
        [SerializeField] private float lightBackSeconds = 0.4f;

        [Header("The room gets colder (each laugh adds to it)")]
        [Range(0f, 1f)]
        [SerializeField] private float coldPerLaugh = 0.35f;
        [Range(0f, 1f)]
        [SerializeField] private float mostCold = 0.8f;
        [Tooltip("Seconds the cold takes to come in after a laugh.")]
        [Min(0.05f)]
        [SerializeField] private float coldInSeconds = 0.8f;
        [Tooltip("How fast the cold goes, per second, while the laughing pumpkin is still around.")]
        [Min(0f)]
        [SerializeField] private float coldFadeWhileThere = 0.02f;
        [Tooltip("How fast the cold goes, per second, once that pumpkin is broken.")]
        [Min(0f)]
        [SerializeField] private float coldFadeWhenGone = 0.12f;
        [Tooltip("The colour the view is filtered with when fully cold.")]
        [SerializeField] private Color coldFilter = new Color(0.85f, 0.93f, 1f, 1f);
        [Tooltip("White balance when fully cold (below 0 = bluer).")]
        [Range(-100f, 0f)]
        [SerializeField] private float coldTemperature = -25f;
        [Tooltip("Colour saturation when fully cold (below 0 = greyer).")]
        [Range(-100f, 0f)]
        [SerializeField] private float coldSaturation = -12f;

        private const string ResourcePath = "Story Laugh Settings";
        private static StoryLaughSettings instance;

        /// <summary>The settings in Resources (or the defaults).</summary>
        public static StoryLaughSettings Instance
        {
            get
            {
                if (instance != null) return instance;
                instance = Resources.Load<StoryLaughSettings>(ResourcePath);
                if (instance == null)
                {
                    instance = CreateInstance<StoryLaughSettings>();
                    instance.hideFlags = HideFlags.HideAndDontSave;
                }
                return instance;
            }
        }

        public string[] RoomBases => roomBases;
        public float LightRange => lightRange;
        public AudioClip[] DeathLaughs => deathLaughs;
        public float DeathLaughVolume => deathLaughVolume;
        public Color WeirdColor => weirdColor;
        public float WeirdMix => weirdMix;
        public float LightBackSeconds => lightBackSeconds;
        public float ColdPerLaugh => coldPerLaugh;
        public float MostCold => mostCold;
        public float ColdInSeconds => coldInSeconds;
        public float ColdFadeWhileThere => coldFadeWhileThere;
        public float ColdFadeWhenGone => coldFadeWhenGone;
        public Color ColdFilter => coldFilter;
        public float ColdTemperature => coldTemperature;
        public float ColdSaturation => coldSaturation;
    }
}

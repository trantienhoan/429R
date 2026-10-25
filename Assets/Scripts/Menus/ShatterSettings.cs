using UnityEngine;

namespace Game.Menus
{
    /// <summary>
    /// How a punched pop-up shatters (Punch To Shatter): its sound, how hard the punch has to be, and its shards. The game
    /// reads the one at Assets/Resources/Shatter Settings.asset; without it, these defaults and no sound.
    /// </summary>
    [CreateAssetMenu(menuName = "429 Game/Menus/Shatter Settings", fileName = "Shatter Settings")]
    public class ShatterSettings : ScriptableObject
    {
        [Tooltip("Played when a pop-up shatters (heard even while a notice has the game paused).")]
        [SerializeField] private AudioClip sound;
        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.8f;
        [Tooltip("How fast a fist has to go through a pop-up to shatter it, in metres per second (a touch doesn't).")]
        [Min(0f)]
        [SerializeField] private float minSpeed = 1.5f;
        [Tooltip("How many shards it breaks into.")]
        [Min(1)]
        [SerializeField] private int shards = 36;
        [Tooltip("Seconds the shards fly, spinning and fading, before they're gone.")]
        [Min(0.1f)]
        [SerializeField] private float shardSeconds = 1.1f;

        private const string ResourcePath = "Shatter Settings";
        private static ShatterSettings instance;

        /// <summary>The settings in Resources (or the defaults).</summary>
        public static ShatterSettings Instance
        {
            get
            {
                if (instance != null) return instance;
                instance = Resources.Load<ShatterSettings>(ResourcePath);
                if (instance == null)
                {
                    instance = CreateInstance<ShatterSettings>();
                    instance.hideFlags = HideFlags.HideAndDontSave;
                }
                return instance;
            }
        }

        public AudioClip Sound => sound;
        public float Volume => volume;
        public float MinSpeed => minSpeed;
        public int Shards => shards;
        public float ShardSeconds => shardSeconds;
    }
}

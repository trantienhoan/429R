using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// The bugs' moving sounds (user 2026-11-25, ElevenLabs), one setting for every Spider Brain bug: a skitter loop while
    /// a bug walks (spiders, roaches, a bee that lost its wings), louder the faster it goes, and a buzz loop while it
    /// flies. So the player hears them coming, even from behind. Assets/Resources/Bug Sounds.asset (Tools > 429 Game >
    /// Audio > Wire ElevenLabs Sounds (P1)); without it, the bugs move silently as before.
    /// </summary>
    [CreateAssetMenu(menuName = "429 Game/Enemies/Bug Sounds", fileName = "Bug Sounds")]
    public class BugSoundSettings : ScriptableObject
    {
        private const string ResourceName = "Bug Sounds";

        [Header("Walking")]
        [Tooltip("Looped while a bug walks: one at random for each bug (bug_skitter_loop_1/2). Empty = silent.")]
        [SerializeField] private AudioClip[] walkLoops = new AudioClip[0];
        [Range(0f, 1f)]
        [SerializeField] private float walkVolume = 0.7f;
        [Tooltip("Walking speed (m/s) at which the skitter is at full volume; slower is quieter, standing still is silent.")]
        [Min(0.05f)]
        [SerializeField] private float walkFullSpeed = 1.2f;

        [Header("Flying")]
        [Tooltip("Looped while a bug flies (bee_buzz_loop). Empty = silent.")]
        [SerializeField] private AudioClip[] flyLoops = new AudioClip[0];
        [Range(0f, 1f)]
        [SerializeField] private float flyVolume = 0.5f;

        [Header("How far it carries")]
        [Tooltip("Full volume within this distance (metres).")]
        [Min(0.1f)]
        [SerializeField] private float nearDistance = 1f;
        [Tooltip("Silent beyond this distance (metres).")]
        [Min(1f)]
        [SerializeField] private float farDistance = 12f;
        [Tooltip("Each bug's pitch is picked in this range; a boss sounds lower (x Boss Pitch), a small bug higher.")]
        [SerializeField] private Vector2 pitchRange = new Vector2(0.92f, 1.08f);
        [SerializeField] private float bossPitch = 0.8f;
        [SerializeField] private float smallPitch = 1.15f;
        [Tooltip("Unity plays only so many sounds at once: bug loops give way first (higher = less important).")]
        [Range(0, 256)]
        [SerializeField] private int priority = 200;

        public AudioClip[] WalkLoops => walkLoops;
        public float WalkVolume => walkVolume;
        public float WalkFullSpeed => walkFullSpeed;
        public AudioClip[] FlyLoops => flyLoops;
        public float FlyVolume => flyVolume;
        public float NearDistance => nearDistance;
        public float FarDistance => farDistance;
        public Vector2 PitchRange => pitchRange;
        public float BossPitch => bossPitch;
        public float SmallPitch => smallPitch;
        public int Priority => priority;

        /// <summary>Whether any loop is set (no asset or no clips: the bugs stay silent).</summary>
        public bool Any => Has(walkLoops) || Has(flyLoops);

        public static bool Has(AudioClip[] clips)
        {
            if (clips == null) return false;
            foreach (var clip in clips) if (clip != null) return true;
            return false;
        }

        /// <summary>One of the clips at random (empty slots skipped), or null.</summary>
        public static AudioClip Pick(AudioClip[] clips)
        {
            if (!Has(clips)) return null;
            while (true)
            {
                var clip = clips[Random.Range(0, clips.Length)];
                if (clip != null) return clip;
            }
        }

        private static BugSoundSettings instance;
        private static bool looked;

        /// <summary>The settings in Resources, or null when there are none.</summary>
        public static BugSoundSettings Instance
        {
            get
            {
                if (!looked)
                {
                    instance = Resources.Load<BugSoundSettings>(ResourceName);
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

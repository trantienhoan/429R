using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// Caps how many enemies are alive at once. Enemy managers wait for room with PlayMaker's "Wait For Enemy Slot"
    /// action before each spawn, so a wave the player falls behind on waits instead of piling up and dragging the
    /// frame rate down. Enemies are the objects tagged Enemy; only the top one of each enemy counts, because its
    /// parts carry the tag too. Keep one in the scene to change the numbers; without one the defaults are used.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyLimit : MonoBehaviour
    {
        public const int DefaultMaxOnQuest = 12;
        public const int DefaultMaxOnPC = 20;
        private const string DefaultTag = "Enemy";
        // Counting looks at every tagged object, so it's done a few times a second, not on every ask.
        private const float RecountInterval = 0.2f;

        [Tooltip("Most enemies alive at once in the Quest (Android) build.")]
        [Min(1)]
        [SerializeField] private int maxOnQuest = DefaultMaxOnQuest;
        [Tooltip("Most enemies alive at once in the PC build.")]
        [Min(1)]
        [SerializeField] private int maxOnPC = DefaultMaxOnPC;
        [Tooltip("The tag every enemy has.")]
        [SerializeField] private string enemyTag = DefaultTag;

        private static EnemyLimit active;
        private static int count;
        private static float nextCount;

        public int MaxOnQuest => maxOnQuest;
        public int MaxOnPC => maxOnPC;

        /// <summary>The most enemies allowed at once on this platform.</summary>
        public static int Max
        {
            get
            {
#if UNITY_ANDROID
                return active != null ? active.MaxOnQuest : DefaultMaxOnQuest;
#else
                return active != null ? active.MaxOnPC : DefaultMaxOnPC;
#endif
            }
        }

        /// <summary>How many enemies are alive, refreshed a few times a second.</summary>
        public static int AliveCount
        {
            get
            {
                if (Time.unscaledTime >= nextCount) Recount();
                return count;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            active = null;
            count = 0;
            nextCount = 0f;
        }

        /// <summary>
        /// Takes a place for one more enemy if fewer than <paramref name="max"/> are alive (0 = this platform's limit).
        /// The place counts straight away, so spawners asking at the same moment can't overshoot together.
        /// </summary>
        public static bool TryTakeSlot(int max = 0)
        {
            if (AliveCount >= (max > 0 ? max : Max)) return false;

            count++;
            return true;
        }

        private void OnEnable()
        {
            active = this;
            nextCount = 0f;
        }

        private void OnDisable()
        {
            if (active == this) active = null;
        }

        private static void Recount()
        {
            nextCount = Time.unscaledTime + RecountInterval;
            string tag = active != null && !string.IsNullOrEmpty(active.enemyTag) ? active.enemyTag : DefaultTag;

            GameObject[] tagged;
            try
            {
                tagged = GameObject.FindGameObjectsWithTag(tag);
            }
            catch (UnityException)
            {
                // The tag isn't defined in this project.
                count = 0;
                return;
            }

            count = 0;
            foreach (var go in tagged)
            {
                if (!HasTaggedParent(go.transform, tag)) count++;
            }
        }

        private static bool HasTaggedParent(Transform part, string tag)
        {
            for (var parent = part.parent; parent != null; parent = parent.parent)
            {
                if (parent.CompareTag(tag)) return true;
            }
            return false;
        }
    }
}

using UnityEngine;

namespace Game.Bowling
{
    /// <summary>
    /// A SlimeBowl kit ball vine (gate 2, 2026-11-23: balls wait in its leafy cup at hand height, not on the floor). Hidden
    /// until the Bowling Ball Supply opens its spot (a chest seed eaten), then it pops up out of the dirt. Its cradle (three
    /// small colliders under the cup) holds the ball. Put on each vine by Tools > 429 Game > Bowling > Build SlimeBowl Kit.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KitSprout : MonoBehaviour
    {
        [Tooltip("The arena's ball supply.")]
        [SerializeField] private BowlingBallSupply supply;
        [Tooltip("This vine's ball spot (the supply opens spots in this order).")]
        [SerializeField] private int spot;
        [Tooltip("Seconds it takes to pop up.")]
        [Min(0.05f)] [SerializeField] private float growSeconds = 0.6f;
        [Tooltip("What shows only once it's open: the vine's looks and its cradle.")]
        [SerializeField] private GameObject[] parts = System.Array.Empty<GameObject>();

        private Vector3 fullScale;
        private float openedAt = -1f;

        /// <summary>Set up by the kit menu.</summary>
        public void Setup(BowlingBallSupply ballSupply, int index, GameObject[] shown)
        {
            supply = ballSupply;
            spot = index;
            parts = shown ?? System.Array.Empty<GameObject>();
        }

        private void Awake()
        {
            fullScale = transform.localScale;
            Show(false);
        }

        private void Update()
        {
            if (supply == null) return;
            bool open = spot < supply.OpenSpots;
            if (open && openedAt < 0f)
            {
                openedAt = Time.time;
                Show(true);
            }
            if (openedAt < 0f) return;
            float k = Mathf.Clamp01((Time.time - openedAt) / growSeconds);
            float s = Mathf.SmoothStep(0.05f, 1f, k) + Mathf.Sin(k * Mathf.PI) * 0.1f;
            transform.localScale = fullScale * s;
            if (k >= 1f) enabled = false;
        }

        private void Show(bool on)
        {
            foreach (var part in parts) if (part != null) part.SetActive(on);
            transform.localScale = on ? fullScale * 0.05f : fullScale;
        }
    }
}

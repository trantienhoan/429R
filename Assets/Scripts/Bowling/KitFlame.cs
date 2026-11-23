using UnityEngine;

namespace Game.Bowling
{
    /// <summary>
    /// A SlimeBowl kit flame (a mesh, no particles: gate 2's braziers, 2026-11-23): it flickers by stretching up and down
    /// and swaying a little, each flame out of step with the others. Put on the flame by Tools > 429 Game > Bowling >
    /// Build SlimeBowl Kit.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KitFlame : MonoBehaviour
    {
        [Tooltip("How fast it flickers.")]
        [SerializeField] private float speed = 6f;
        [Tooltip("How much taller and shorter it gets (share of its height).")]
        [Range(0f, 0.5f)] [SerializeField] private float stretch = 0.14f;
        [Tooltip("How far it leans, degrees.")]
        [Range(0f, 30f)] [SerializeField] private float sway = 7f;

        private Vector3 baseScale;
        private Quaternion baseTurn;
        private float seed;

        private void Awake()
        {
            baseScale = transform.localScale;
            baseTurn = transform.localRotation;
            seed = Random.value * 100f;
        }

        private void Update()
        {
            float t = Time.time * speed + seed;
            float up = 1f + stretch * (Mathf.PerlinNoise(t, 0.3f) * 2f - 1f);
            float wide = 1f / Mathf.Sqrt(Mathf.Max(0.5f, up));
            transform.localScale = new Vector3(baseScale.x * wide, baseScale.y * up, baseScale.z * wide);
            transform.localRotation = baseTurn * Quaternion.Euler(sway * (Mathf.PerlinNoise(t * 0.7f, 5.1f) * 2f - 1f), 0f,
                sway * (Mathf.PerlinNoise(9.3f, t * 0.7f) * 2f - 1f));
        }
    }
}

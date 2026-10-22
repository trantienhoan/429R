using UnityEngine;

namespace Game.VFX
{
    /// <summary>
    /// Makes a fire's light flicker: its intensity wobbles round the starting value (smooth noise, a little different on
    /// every fire). Put it on the Light. Used by Danger_Flame.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class FlameFlicker : MonoBehaviour
    {
        [Tooltip("Lowest share of the starting intensity.")]
        [Range(0f, 1f)]
        [SerializeField] private float low = 0.7f;
        [Tooltip("Highest share of the starting intensity.")]
        [Range(1f, 2f)]
        [SerializeField] private float high = 1.15f;
        [Tooltip("How fast it flickers.")]
        [Min(0.1f)]
        [SerializeField] private float speed = 7f;

        private Light fire;
        private float baseIntensity;
        private float seed;

        private void Awake()
        {
            fire = GetComponent<Light>();
            baseIntensity = fire.intensity;
            seed = Random.value * 100f;
        }

        private void OnDisable()
        {
            if (fire != null) fire.intensity = baseIntensity;
        }

        private void Update()
        {
            // Two noise speeds: a slow sway with quick little jumps on top.
            float t = Time.time * speed;
            float n = Mathf.PerlinNoise(seed, t) * 0.7f + Mathf.PerlinNoise(seed + 37f, t * 2.7f) * 0.3f;
            fire.intensity = baseIntensity * Mathf.Lerp(low, high, n);
        }
    }
}

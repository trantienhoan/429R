using UnityEngine;

namespace Game.VFX
{
    /// <summary>
    /// A failing ceiling lamp: it hums (a faint, steady wobble) and every few seconds it stutters, dipping dim a few times
    /// in quick succession, now and then going nearly dark for a moment. Put it on the Light. Unscaled time, so it keeps
    /// going while the game is paused. Used on the Bedroom and Living Room lamps.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class LampFlicker : MonoBehaviour
    {
        [Tooltip("Seconds between stutters (random in this range).")]
        [SerializeField] private Vector2 calmSeconds = new(3f, 9f);
        [Tooltip("Dips in one stutter (random in this range).")]
        [SerializeField] private Vector2Int dips = new(2, 6);
        [Tooltip("How dim a dip gets: share of the normal brightness (random in this range).")]
        [SerializeField] private Vector2 dipLevel = new(0.1f, 0.55f);
        [Tooltip("How much it wobbles while steady (0.05 = 5%).")]
        [Range(0f, 0.3f)]
        [SerializeField] private float hum = 0.05f;
        [Tooltip("Chance that a stutter ends in a short blackout (0-1).")]
        [Range(0f, 1f)]
        [SerializeField] private float blackoutChance = 0.25f;

        private Light lamp;
        private float baseIntensity;
        private float nextStutter;
        private float dipUntil;
        private float dipValue = 1f;
        private int dipsLeft;
        private float seed;

        private void Awake()
        {
            lamp = GetComponent<Light>();
            baseIntensity = lamp.intensity;
            seed = Random.value * 100f;
            nextStutter = Time.unscaledTime + Random.Range(calmSeconds.x, calmSeconds.y);
        }

        private void OnDisable()
        {
            if (lamp != null) lamp.intensity = baseIntensity;
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            float level = 1f + (Mathf.PerlinNoise(seed, now * 6f) - 0.5f) * 2f * hum;

            if (dipsLeft <= 0 && now >= nextStutter) dipsLeft = Random.Range(dips.x, dips.y + 1);
            if (dipsLeft > 0 && now >= dipUntil)
            {
                // Alternate: a dip, then a short flash back on, until the stutter is used up.
                bool dim = dipValue >= 1f;
                if (dim)
                {
                    bool blackout = dipsLeft == 1 && Random.value < blackoutChance;
                    dipValue = blackout ? 0.02f : Random.Range(dipLevel.x, dipLevel.y);
                    dipUntil = now + (blackout ? Random.Range(0.25f, 0.6f) : Random.Range(0.03f, 0.12f));
                }
                else
                {
                    dipValue = 1f;
                    dipUntil = now + Random.Range(0.03f, 0.15f);
                    dipsLeft--;
                    if (dipsLeft == 0) nextStutter = now + Random.Range(calmSeconds.x, calmSeconds.y);
                }
            }
            if (dipsLeft <= 0) dipValue = 1f;
            lamp.intensity = baseIntensity * level * dipValue;
        }
    }
}

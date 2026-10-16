using UnityEngine;

namespace Game.Weapons
{
    /// <summary>
    /// The charge you can see on a shake-to-charge bottle (Grenade): a glow in the bottle's own colour that isn't there
    /// at first and grows as it's shaken, bigger, brighter and thicker, up to the moment it would go off in the hand.
    /// It stays at the size it reached (a charged bottle glows in flight too). Tools > 429 Game > Weapons > Add Bottle
    /// Glow adds it, with its particle system, coloured from the bottle's texture.
    /// </summary>
    [RequireComponent(typeof(Grenade))]
    public class ChargeGlow : MonoBehaviour
    {
        [Tooltip("The glow's particle system (on a child in the middle of the bottle).")]
        [SerializeField] private ParticleSystem glow;
        [Tooltip("The glow's colour: the bottle's own.")]
        [SerializeField] private Color color = Color.white;
        [Tooltip("How big the glow is (metres across): just after the shaking starts (x), and just before it goes off (y).")]
        [SerializeField] private Vector2 size = new(0.12f, 0.7f);
        [Tooltip("Glow particles a second: at the start (x) and at the end (y).")]
        [SerializeField] private Vector2 rate = new(8f, 30f);
        [Tooltip("How strong the glow is (0-1): at the start (x) and at the end (y).")]
        [SerializeField] private Vector2 strength = new(0.4f, 1f);

        private Grenade grenade;
        private float shownProgress = -1f;

        private void Awake()
        {
            grenade = GetComponent<Grenade>();
            if (glow == null) return;
            var emission = glow.emission;
            emission.rateOverTime = 0f;
            glow.Play();
        }

        private void Update()
        {
            if (glow == null) return;
            float progress = grenade.ChargeProgress;
            if (Mathf.Approximately(progress, shownProgress)) return;
            shownProgress = progress;

            var emission = glow.emission;
            if (progress <= 0f)
            {
                emission.rateOverTime = 0f;
                return;
            }
            var main = glow.main;
            var tint = color;
            tint.a = Mathf.Lerp(strength.x, strength.y, progress);
            main.startColor = tint;
            main.startSize = Mathf.Lerp(size.x, size.y, progress);
            emission.rateOverTime = Mathf.Lerp(rate.x, rate.y, progress);
        }
    }
}

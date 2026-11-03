using UnityEngine;

namespace Game.Bonus
{
    /// <summary>
    /// One of the Paint Bees round's floating paint buckets (Paint_Bucket: the pumpkin bucket, its paint and a glow on
    /// the floor under it). It rises out of the floor, floats there bobbing gently, and can't be moved; a hand dipped in
    /// its paint takes its colour (the round checks the hands, see Dips). The paint wobbles and ripples at each dip.
    /// </summary>
    public class PaintBucket : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        [Tooltip("The paint's surface inside the bucket.")]
        [SerializeField] private Renderer paint;
        [Tooltip("The soft glow on the floor under it, in its colour. Empty: none.")]
        [SerializeField] private Renderer floorGlow;
        [Tooltip("The ring that spreads over the paint at a dip (a Line Renderer round the paint's middle). Empty: none.")]
        [SerializeField] private LineRenderer ripple;
        [Tooltip("How far from the paint's middle a hand counts as in it, in metres (the bucket's inside).")]
        [Min(0.05f)]
        [SerializeField] private float reach = 0.24f;
        [Tooltip("A hand this far under the paint's surface and up to Above Paint over it counts as dipped, in metres.")]
        [Min(0.05f)]
        [SerializeField] private float depth = 0.3f;
        [Min(0f)]
        [SerializeField] private float abovePaint = 0.05f;
        [Tooltip("How much the paint glows (a share of its colour), so it reads in the dark.")]
        [Range(0f, 1f)]
        [SerializeField] private float paintGlow = 0.3f;
        [Tooltip("Up and down while it floats, in metres, and how many times a second.")]
        [Min(0f)]
        [SerializeField] private float bob = 0.02f;
        [Min(0.01f)]
        [SerializeField] private float bobsPerSecond = 0.45f;

        private const float RiseFrom = 0.9f;
        private const float RippleSeconds = 0.5f;

        private Vector3 rest;
        private float riseStarted = -1f;
        private float riseSeconds = 0.6f;
        private bool sinking;
        private float phase;
        private float dippedAt = -10f;
        private Vector3 paintRest;
        private Color colour = Color.white;
        private MaterialPropertyBlock block;

        /// <summary>The paint's colour index (see the round's colours).</summary>
        public int Colour { get; private set; }

        /// <summary>Its paint colour.</summary>
        public Color PaintColour => colour;

        /// <summary>Where the paint's surface is, its middle.</summary>
        public Vector3 PaintMiddle => paint != null ? paint.bounds.center : transform.position;

        /// <summary>It takes this colour, rests at 'at' and rises there out of the floor over 'seconds'.</summary>
        public void Setup(int index, Color paintColour, Vector3 at, float seconds)
        {
            Colour = index;
            colour = paintColour;
            rest = at;
            riseSeconds = Mathf.Max(0.05f, seconds);
            riseStarted = Time.time;
            phase = Random.value * 10f;
            if (paint != null) paintRest = paint.transform.localPosition;
            transform.position = at + Vector3.down * RiseFrom;
            block ??= new MaterialPropertyBlock();
            Tint(paint, colour, colour * paintGlow);
            var glow = colour;
            glow.a = 0.6f;
            Tint(floorGlow, glow, Color.black);
            if (ripple != null) ripple.enabled = false;
        }

        /// <summary>It sinks back into the floor over 'seconds', and is gone.</summary>
        public void Sink(float seconds)
        {
            sinking = true;
            riseStarted = Time.time;
            riseSeconds = Mathf.Max(0.05f, seconds);
            Destroy(gameObject, seconds + 0.05f);
        }

        /// <summary>Whether this point (a hand) is in its paint.</summary>
        public bool Dips(Vector3 point)
        {
            if (sinking || riseStarted < 0f || Time.time - riseStarted < riseSeconds) return false;
            var middle = PaintMiddle;
            var off = point - middle;
            float up = off.y;
            off.y = 0f;
            return off.sqrMagnitude <= reach * reach && up <= abovePaint && up >= -depth;
        }

        /// <summary>A hand went in: the paint wobbles and a ring spreads over it.</summary>
        public void Splash() => dippedAt = Time.time;

        private void Update()
        {
            if (riseStarted < 0f) return;
            float k = Mathf.Clamp01((Time.time - riseStarted) / riseSeconds);
            // Up past where it floats and back (sinking: just down).
            float lift = sinking ? -RiseFrom * k * k : -RiseFrom * (1f - Overshoot(k));
            float t = Time.time + phase;
            transform.position = rest + Vector3.up * (lift + bob * Mathf.Sin(t * bobsPerSecond * 2f * Mathf.PI));
            transform.rotation = Quaternion.Euler(2f * Mathf.Sin(t * 0.7f), transform.eulerAngles.y, 2f * Mathf.Cos(t * 0.6f));

            float since = Time.time - dippedAt;
            if (paint != null) paint.transform.localPosition = paintRest + Vector3.up * (0.012f * Mathf.Exp(-since * 6f) * Mathf.Sin(since * 30f));
            if (ripple != null)
            {
                bool on = since < RippleSeconds;
                ripple.enabled = on;
                if (on)
                {
                    float r = since / RippleSeconds;
                    ripple.transform.localScale = Vector3.one * Mathf.Lerp(0.15f, 1f, r);
                    var c = Color.Lerp(colour, Color.white, 0.5f);
                    c.a = 1f - r;
                    ripple.startColor = c;
                    ripple.endColor = c;
                }
            }
        }

        // Eases out past 1 and settles back: a pop up out of the floor.
        private static float Overshoot(float k)
        {
            const float s = 1.7f;
            k -= 1f;
            return k * k * ((s + 1f) * k + s) + 1f;
        }

        private void Tint(Renderer part, Color baseColour, Color emission)
        {
            if (part == null) return;
            part.GetPropertyBlock(block);
            block.SetColor(BaseColor, baseColour);
            block.SetColor(EmissionColor, emission);
            part.SetPropertyBlock(block);
        }
    }
}

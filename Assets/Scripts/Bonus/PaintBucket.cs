using UnityEngine;

namespace Game.Bonus
{
    /// <summary>
    /// One of the Paint Bees round's floating paint buckets (Paint_Bucket: the pumpkin bucket, its liquid paint and a glow
    /// on the floor under it). It rises out of the floor, floats there bobbing gently, and can't be moved; a hand dipped in
    /// its paint takes its colour (the round checks the hands, see Dips). The paint (429/Paint Liquid) waves gently; it
    /// wobbles and ripples where a hand goes in, and a hand stirring it makes small ripples.
    /// It's drippy (user, 2026-11-03): paint runs down its side and drips off, splatting on the floor, into a puddle of
    /// its colour under it that grows as the round goes on; what's on the floor stays still while the bucket bobs.
    /// </summary>
    public class PaintBucket : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        // The paint's ripple (429/Paint Liquid): x, z on the paint disc, seconds since, how strong.
        private static readonly int RippleId = Shader.PropertyToID("_Ripple");

        [Tooltip("The paint's surface inside the bucket.")]
        [SerializeField] private Renderer paint;
        [Tooltip("The soft glow on the floor under it, in its colour. Empty: none.")]
        [SerializeField] private Renderer floorGlow;
        [Tooltip("A ring that spreads over the paint at a dip (a Line Renderer round the paint's middle). Empty: none (the " +
                 "liquid paint ripples by itself).")]
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
        [Tooltip("Paint running down its side and dripping off, in its colour (its drops splat on the floor). Empty: none.")]
        [SerializeField] private ParticleSystem drips;
        [Tooltip("The paint puddle on the floor under it, in its colour. Empty: none.")]
        [SerializeField] private Renderer puddle;
        [Tooltip("The puddle grows from this size (x) to this (y), metres across, over Puddle Seconds.")]
        [SerializeField] private Vector2 puddleSize = new(0.45f, 1.1f);
        [Min(1f)]
        [SerializeField] private float puddleSeconds = 30f;
        [Tooltip("What lies on the floor under it (the glow, the puddle, the floor the drips splat on): kept still there " +
                 "while the bucket bobs.")]
        [SerializeField] private Transform[] onFloor = System.Array.Empty<Transform>();
        [Tooltip("Up and down while it floats, in metres, and how many times a second.")]
        [Min(0f)]
        [SerializeField] private float bob = 0.02f;
        [Min(0.01f)]
        [SerializeField] private float bobsPerSecond = 0.45f;
        [Tooltip("How far it rocks while it floats, in degrees (0 = still: a bucket standing on the floor).")]
        [Min(0f)]
        [SerializeField] private float tilt = 2f;

        private const float RiseFrom = 0.9f;
        private const float RippleSeconds = 0.5f;
        // A hand stirring the paint makes a new, smaller ripple this often at the most (and only when it moved 2 cm).
        private const float StirEvery = 0.35f;
        private const float StirMove = 0.02f;

        private Vector3 rest;
        private float riseStarted = -1f;
        private float riseSeconds = 0.6f;
        private bool sinking;
        private float phase;
        private float dippedAt = -10f;
        private Vector3 paintRest;
        private Color colour = Color.white;
        private Vector3[] floorAt = System.Array.Empty<Vector3>();
        private Quaternion[] floorTurn = System.Array.Empty<Quaternion>();
        private MaterialPropertyBlock block;
        private Vector2 rippleAt;
        private float rippleStarted = -100f;
        private float rippleStrength;
        private Vector3 lastStir;

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
            var wet = colour;
            wet.a = 0.92f;
            Tint(puddle, wet, Color.black);
            if (puddle != null) puddle.transform.localScale = Vector3.one * puddleSize.x;
            if (drips != null)
            {
                var main = drips.main;
                main.startColor = colour;
            }
            // The floor under where it'll float.
            floorAt = new Vector3[onFloor.Length];
            floorTurn = new Quaternion[onFloor.Length];
            for (int i = 0; i < onFloor.Length; i++)
            {
                if (onFloor[i] == null) continue;
                floorAt[i] = at + (onFloor[i].position - transform.position);
                floorTurn[i] = onFloor[i].rotation;
            }
        }

        /// <summary>It sinks back into the floor over 'seconds', and is gone.</summary>
        public void Sink(float seconds)
        {
            sinking = true;
            if (drips != null) drips.Stop(true, ParticleSystemStopBehavior.StopEmitting);
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
        public void Splash() => Splash(PaintMiddle);

        /// <summary>A hand went in here: the paint wobbles and ripples spread from the spot.</summary>
        public void Splash(Vector3 at)
        {
            dippedAt = Time.time;
            Ripple(at, 1f);
        }

        /// <summary>A hand held in the paint here: moving, it stirs up small ripples now and then.</summary>
        public void Stir(Vector3 at)
        {
            if (Time.time - rippleStarted < StirEvery || (at - lastStir).sqrMagnitude < StirMove * StirMove) return;
            Ripple(at, 0.5f);
        }

        private void Ripple(Vector3 at, float strength)
        {
            lastStir = at;
            if (paint == null) return;
            var local = paint.transform.InverseTransformPoint(at);
            rippleAt = new Vector2(Mathf.Clamp(local.x, -0.9f, 0.9f), Mathf.Clamp(local.z, -0.9f, 0.9f));
            rippleStarted = Time.time;
            rippleStrength = strength;
        }

        private void Update()
        {
            if (riseStarted < 0f) return;
            float k = Mathf.Clamp01((Time.time - riseStarted) / riseSeconds);
            // Up past where it floats and back (sinking: just down).
            float lift = sinking ? -RiseFrom * k * k : -RiseFrom * (1f - Overshoot(k));
            float t = Time.time + phase;
            transform.position = rest + Vector3.up * (lift + bob * Mathf.Sin(t * bobsPerSecond * 2f * Mathf.PI));
            transform.rotation = Quaternion.Euler(tilt * Mathf.Sin(t * 0.7f), transform.eulerAngles.y, tilt * Mathf.Cos(t * 0.6f));
            for (int i = 0; i < onFloor.Length && i < floorAt.Length; i++)
                if (onFloor[i] != null) onFloor[i].SetPositionAndRotation(floorAt[i], floorTurn[i]);

            // Up and floating, it drips, and its puddle grows; sinking, the puddle shrinks away with it.
            bool up = !sinking && k >= 1f;
            if (drips != null && up && !drips.isEmitting) drips.Play(true);
            if (puddle != null)
            {
                float grown = Mathf.Clamp01((Time.time - riseStarted - riseSeconds) / puddleSeconds);
                float size = sinking ? Mathf.Lerp(puddle.transform.localScale.x, 0f, k) : Mathf.Lerp(puddleSize.x, puddleSize.y, Mathf.Sqrt(grown));
                puddle.transform.localScale = Vector3.one * size;
            }

            float since = Time.time - dippedAt;
            if (paint != null)
            {
                paint.transform.localPosition = paintRest + Vector3.up * (0.006f * Mathf.Exp(-since * 6f) * Mathf.Sin(since * 30f));
                block ??= new MaterialPropertyBlock();
                paint.GetPropertyBlock(block);
                block.SetVector(RippleId, new Vector4(rippleAt.x, rippleAt.y, Time.time - rippleStarted, rippleStrength));
                paint.SetPropertyBlock(block);
            }
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

using UnityEngine;

namespace Balls
{
    /// <summary>
    /// Animates a slime's two-part model the way 429R's Fat Pumpkin is animated (same poses and timings, from
    /// FatPumpkinAnimationMenu): the head (Giant_Slime_Below, hinged at the back) tips up to open the mouth, and the
    /// whole body leans, rocks and hops. Done in code instead of an Animator so it also runs on network copies and
    /// needs no prefab. On top: jelly squash and stretch from movement, landings and hits, and a little breathing.
    /// </summary>
    public class SlimeAnim : MonoBehaviour
    {
        public enum Clip : byte { Idle, Walk, Wake, Eat, Laugh, Hit }

        // A moment of a clip: seconds; mouth (degrees, more negative = wider); body leaning forward, rocking sideways
        // (degrees) and off the floor (Fat Pumpkin metres).
        readonly struct Pose
        {
            public readonly float at, mouth, lean, rock, hop;

            public Pose(float at, float mouth, float lean = 0f, float rock = 0f, float hop = 0f)
            {
                this.at = at;
                this.mouth = mouth;
                this.lean = lean;
                this.rock = rock;
                this.hop = hop;
            }
        }

        // Fat Pumpkin's clips, as they are.
        static readonly Pose[] Idle = { new(0f, 0f), new(1.5f, -2f, -1f, 0f, 0.004f), new(3f, 0f) };

        static readonly Pose[] Wake =
        {
            new(0f, 0f), new(0.08f, 0f, 4f, -6f), new(0.16f, 0f, 4f, 6f), new(0.24f, 0f, 4f, -6f), new(0.32f, 0f, 4f, 6f),
            new(0.42f, 0f, 6f), new(0.5f, -42f, -8f, 0f, 0.05f), new(0.58f, -24f, -6f, 0f, 0.02f),
            new(0.65f, -40f, -8f, 0f, 0.04f), new(0.72f, -22f, -5f, 0f, 0.02f), new(0.8f, -12f, -2f),
        };

        static readonly Pose[] Walk =
        {
            new(0f, 0f, 3f), new(0.225f, -14f, 3f, 9f, 0.03f), new(0.45f, -2f, 3f), new(0.675f, -14f, 3f, -9f, 0.03f), new(0.9f, 0f, 3f),
        };

        static readonly Pose[] Eat =
        {
            new(0f, 0f), new(0.12f, -40f, -6f, 0f, 0.01f), new(0.22f, -42f, -7f, 0f, 0.01f), new(0.27f, 0f, 5f), new(0.33f, -8f, 2f, 0f, 0.012f),
            new(0.4f, 0f), new(0.5f, -10f, -1f, 0f, 0.008f), new(0.6f, 0f), new(0.7f, -7f, -1f, 0f, 0.005f), new(0.85f, 0f),
        };

        static readonly Pose[] Laugh =
        {
            new(0f, 0f), new(0.15f, -26f, -7f, 0f, 0.02f), new(0.3f, -8f, -4f), new(0.45f, -28f, -8f, 0f, 0.02f), new(0.6f, -8f, -4f),
            new(0.75f, -24f, -6f, 0f, 0.015f), new(0.9f, 0f),
        };

        static readonly Pose[] Hit =
        {
            new(0f, 0f), new(0.05f, -28f, -18f, 0f, 0.07f), new(0.18f, -14f, 8f), new(0.3f, -18f, -4f, 0f, 0.01f), new(0.42f, -4f, 2f), new(0.55f, 0f),
        };

        static readonly Pose[][] Clips = { Idle, Walk, Wake, Eat, Laugh, Hit };
        static readonly bool[] Loops = { true, true, false, false, false, false };

        const float BlendSeconds = 0.15f, QuickBlendSeconds = 0.04f;
        const float HopScale = 2.5f;          // Fat Pumpkin metres -> slime body units (radius 1)
        const float LiftPerDegree = 0.006f;   // tilted, the round bottom would dig into the floor
        const float JellySlice = 1f / 60f;    // the jelly spring's longest step (it's only stable below ~1/9 s)
        const float MaxJellyDt = 0.25f;       // a longer frame plays only this much of the wobble

        /// <summary>Squashes and stretches; holds the model (pivot at the bottom middle).</summary>
        public Transform jelly;
        /// <summary>Leans, rocks and hops (the model holder, under <see cref="jelly"/>).</summary>
        public Transform body;
        /// <summary>The head that opens the mouth (Giant_Slime_Below), turning on its own X axis.</summary>
        public Transform head;
        /// <summary>Movement speed decides walk/idle; vertical speed drives the jelly.</summary>
        public System.Func<Vector3> velocity;

        Clip clip = Clip.Idle, fromClip = Clip.Idle;
        float time, fromTime, blend = 1f, blendSpeed = 1f / BlendSeconds;
        Quaternion headRest = Quaternion.identity;
        Vector3 bodyRest;
        float squash, squashVel, lastVy, phase;

        public Clip Current => clip;

        void Start()
        {
            if (head) headRest = head.localRotation;
            if (body) bodyRest = body.localPosition;
            phase = Random.value * 10f;
        }

        /// <summary>Starts a one-shot clip (Wake, Eat, Laugh, Hit); it returns to walking or idling by itself.</summary>
        public void Play(Clip c)
        {
            fromClip = clip;
            fromTime = time;
            clip = c;
            time = 0f;
            blend = 0f;
            blendSpeed = 1f / (c == Clip.Hit || c == Clip.Eat ? QuickBlendSeconds : BlendSeconds);
            if (c == Clip.Hit) Splat(-0.35f);
        }

        /// <summary>Squash (negative) or stretch (positive) the jelly right now; it wobbles back.</summary>
        public void Splat(float amount) => squashVel += amount * 12f;

        void Update()
        {
            float dt = Time.deltaTime;
            Vector3 v = velocity != null ? velocity() : Vector3.zero;
            float flat = new Vector2(v.x, v.z).magnitude;
            bool walking = flat > (clip == Clip.Walk ? 0.35f : 0.9f);   // a little hysteresis: hops slow down in between

            // Loops follow the movement; one-shots run out and hand back to them.
            var poses = Clips[(int)clip];
            float length = poses[poses.Length - 1].at;
            float speed = clip == Clip.Walk ? Mathf.Clamp(flat / 3f, 0.7f, 1.8f) : 1f;
            time += dt * speed;
            if (Loops[(int)clip])
            {
                var want = walking ? Clip.Walk : Clip.Idle;
                if (want != clip) Switch(want);
                else if (time > length) time %= Mathf.Max(0.01f, length);
            }
            else if (time >= length) Switch(walking ? Clip.Walk : Clip.Idle);

            blend = Mathf.Min(1f, blend + dt * blendSpeed);
            fromTime += dt;
            Sample(clip, time, out float mouth, out float lean, out float rock, out float hop);
            if (blend < 1f)
            {
                Sample(fromClip, fromTime, out float m0, out float l0, out float r0, out float h0);
                float b = blend * blend * (3f - 2f * blend);
                mouth = Mathf.Lerp(m0, mouth, b);
                lean = Mathf.Lerp(l0, lean, b);
                rock = Mathf.Lerp(r0, rock, b);
                hop = Mathf.Lerp(h0, hop, b);
            }

            if (head) head.localRotation = headRest * Quaternion.Euler(mouth, 0f, 0f);
            if (body)
            {
                float lift = Mathf.Max(hop * HopScale, LiftPerDegree * Mathf.Sqrt(lean * lean + rock * rock));
                body.localPosition = bodyRest + new Vector3(0f, lift, 0f);
                body.localRotation = Quaternion.Euler(lean, 0f, rock);
            }

            // Jelly: a spring that stretches while rising, squashes on landing, plus breathing. It's stiff, so it moves in
            // slices of at most 1/60 s: in one slice longer than ~1/9 s (a struggling phone) it would flip between fully
            // squashed and stretched every frame, and below ~4 fps swing wider each frame until the scale breaks (NaN).
            float vy = v.y;
            if (lastVy < -2f && vy > lastVy + 2f) Splat(Mathf.Clamp(lastVy * 0.05f, -0.5f, 0f));   // just landed
            lastVy = vy;
            float target = Mathf.Clamp(vy * 0.05f, -0.2f, 0.3f);
            float jellyDt = Mathf.Min(dt, MaxJellyDt);
            int slices = Mathf.CeilToInt(jellyDt / JellySlice);
            float slice = slices > 0 ? jellyDt / slices : 0f;
            for (int i = 0; i < slices; i++)
            {
                squashVel += ((target - squash) * 160f - squashVel * 9f) * slice;
                squash = Mathf.Clamp(squash + squashVel * slice, -0.45f, 0.45f);
            }
            if (jelly)
            {
                float breath = Mathf.Sin((Time.time + phase) * 2.2f) * 0.025f;
                float tall = 1f + squash + breath;
                float wide = 1f / Mathf.Sqrt(Mathf.Max(0.3f, tall));
                jelly.localScale = new Vector3(wide, tall, wide);
            }
        }

        void Switch(Clip c)
        {
            fromClip = clip;
            fromTime = time;
            clip = c;
            time = 0f;
            blend = 0f;
            blendSpeed = 1f / BlendSeconds;
        }

        static void Sample(Clip c, float t, out float mouth, out float lean, out float rock, out float hop)
        {
            var poses = Clips[(int)c];
            int i = 1;
            while (i < poses.Length - 1 && poses[i].at < t) i++;
            var a = poses[i - 1];
            var b = poses[i];
            float u = Mathf.Clamp01((t - a.at) / Mathf.Max(0.0001f, b.at - a.at));
            u = u * u * (3f - 2f * u);   // ease in and out of every pose
            mouth = Mathf.Lerp(a.mouth, b.mouth, u);
            lean = Mathf.Lerp(a.lean, b.lean, u);
            rock = Mathf.Lerp(a.rock, b.rock, u);
            hop = Mathf.Lerp(a.hop, b.hop, u);
        }
    }
}

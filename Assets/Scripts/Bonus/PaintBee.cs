using UnityEngine;

namespace Game.Bonus
{
    /// <summary>
    /// A bee of the Paint Bees round (Paint_Bee: the bee's model, its buzzing wings and buzz, no Spider Brain): it flies
    /// in weaving from far in front of the player, hovers in front of their face a moment (its warning), then dives at
    /// it. The round swats it (a hand of its colour: it bursts) or bounces it off (the wrong colour: it's knocked back
    /// and comes again); one that reaches the face stings and flies off. A rainbow bee (Colour -1) is painted in every
    /// colour (its own look, Rainbow Look) and any painted hand swats it. Every bee has an aura in its colour (a glow, sparkles).
    /// </summary>
    public class PaintBee : MonoBehaviour
    {
        private enum State { Arrive, Hover, Dive, Bounced, Leave }

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        [Tooltip("Its model's Animator (Bee_Animator: run, alert, attack, dodge_back).")]
        [SerializeField] private Animator animator;
        [Tooltip("Its looks, one picked at random (the bee's three meshes).")]
        [SerializeField] private GameObject[] looks = System.Array.Empty<GameObject>();
        [Tooltip("Its buzz (looping), higher as it hovers and dives.")]
        [SerializeField] private AudioSource buzz;
        [Tooltip("How big it is to hit, its radius in metres.")]
        [Min(0.02f)]
        [SerializeField] private float radius = 0.13f;
        [Tooltip("How much it glows in its colour (a share of it), so it reads in the dark.")]
        [Range(0f, 1f)]
        [SerializeField] private float glow = 1f;
        [Tooltip("The rainbow bee's look: its own rainbow-painted material (not tinted).")]
        [SerializeField] private Material rainbowLook;
        [Tooltip("Its aura (a soft glow round it and a trail of sparkles), in its colour; the rainbow bee's in every colour.")]
        [SerializeField] private ParticleSystem[] auras = System.Array.Empty<ParticleSystem>();

        private const float WeaveWidth = 0.35f;
        private const float WeavesPerSecond = 0.9f;
        private const float StingReach = 0.2f;
        private const float BounceSeconds = 0.55f;
        private const float LeaveSeconds = 0.9f;

        private State state;
        private float stateStarted;
        private PaintBeesRound round;
        private Vector3 hoverOffset;
        private float approachSpeed;
        private float hoverSeconds;
        private float diveSpeed;
        private Vector3 velocity;
        private float weavePhase;
        private float pitch = 1f;
        private Renderer[] parts = System.Array.Empty<Renderer>();
        private MaterialPropertyBlock block;

        /// <summary>Its colour index; -1 = the rainbow bee.</summary>
        public int Colour { get; private set; }

        /// <summary>The rainbow bee (any painted hand swats it).</summary>
        public bool Rainbow => Colour < 0;

        /// <summary>How big it is to hit (radius, metres).</summary>
        public float Radius => radius * transform.localScale.x;

        /// <summary>It's on its way out (stung, or swatted): no more hits.</summary>
        public bool Leaving => state == State.Leave;

        /// <summary>
        /// Sets it off from where it is: its colour, how fast it comes (metres a second), how long it hovers before it
        /// dives, how fast it dives, and where in front of the face it hovers (from the head, in the head's flat frame:
        /// x right, y up, z towards it).
        /// </summary>
        public void Launch(PaintBeesRound owner, int colour, float speed, float hover, float dive, Vector3 hoverAt)
        {
            round = owner;
            Colour = colour;
            approachSpeed = speed;
            hoverSeconds = hover;
            diveSpeed = dive;
            hoverOffset = hoverAt;
            weavePhase = Random.value * 10f;
            if (looks.Length > 0)
            {
                int pick = Random.Range(0, looks.Length);
                for (int i = 0; i < looks.Length; i++) if (looks[i] != null) looks[i].SetActive(i == pick);
            }
            Dress(Rainbow, Rainbow ? Color.white : round.ColourOf(colour));
            if (buzz != null)
            {
                buzz.time = Random.value * Mathf.Max(0.01f, buzz.clip != null ? buzz.clip.length - 0.05f : 0f);
                buzz.Play();
            }
            Enter(State.Arrive, "run");
        }

        /// <summary>Knocked back along 'push' (metres a second) by the wrong hand: it tumbles off, then comes again.</summary>
        public void Bounce(Vector3 push)
        {
            if (state == State.Leave) return;
            velocity = push;
            Enter(State.Bounced, "dodge_back");
        }

        /// <summary>It flies off up and away (it stung), and is gone.</summary>
        public void FlyOff()
        {
            if (state == State.Leave) return;
            var away = transform.position - round.Head.position;
            away.y = 0f;
            velocity = (away.sqrMagnitude > 0.001f ? away.normalized : -transform.forward) * 2.5f + Vector3.up * 2f;
            Enter(State.Leave, "run");
            Destroy(gameObject, LeaveSeconds);
        }

        private void Update()
        {
            if (round == null || round.Head == null) return;
            var head = round.Head.position;
            float t = Time.time - stateStarted;
            switch (state)
            {
                case State.Arrive:
                {
                    var spot = HoverSpot(head);
                    var to = spot - transform.position;
                    float step = approachSpeed * Time.deltaTime;
                    if (to.magnitude <= step + 0.03f) { transform.position = spot; Enter(State.Hover, "alert"); break; }
                    // It weaves from side to side on the way, less as it gets close.
                    var dir = to.normalized;
                    var side = Vector3.Cross(Vector3.up, dir).normalized;
                    float weave = Mathf.Sin((Time.time + weavePhase) * WeavesPerSecond * 2f * Mathf.PI) * WeaveWidth * Mathf.Clamp01(to.magnitude / 3f);
                    var lastWeave = Mathf.Sin((Time.time - Time.deltaTime + weavePhase) * WeavesPerSecond * 2f * Mathf.PI) * WeaveWidth * Mathf.Clamp01(to.magnitude / 3f);
                    transform.position += dir * step + side * (weave - lastWeave);
                    Face(head);
                    pitch = 1f;
                    break;
                }
                case State.Hover:
                    // Still in front of the face (it follows the head), shaking a little: it's about to dive.
                    transform.position = HoverSpot(head) + Random.insideUnitSphere * (0.012f * Mathf.Clamp01(t / hoverSeconds));
                    Face(head);
                    pitch = Mathf.Lerp(1.1f, 1.4f, t / hoverSeconds);
                    if (t >= hoverSeconds) Enter(State.Dive, "attack");
                    break;
                case State.Dive:
                {
                    var to = head - transform.position;
                    float step = diveSpeed * Time.deltaTime;
                    transform.position += to.normalized * Mathf.Min(step, to.magnitude);
                    Face(head);
                    pitch = 1.5f;
                    if ((head - transform.position).magnitude <= StingReach) round.Stung(this);
                    break;
                }
                case State.Bounced:
                    transform.position += velocity * Time.deltaTime;
                    velocity = Vector3.Lerp(velocity, Vector3.zero, 1f - Mathf.Exp(-5f * Time.deltaTime));
                    transform.Rotate(Vector3.up, 720f * Time.deltaTime, Space.World);
                    pitch = 0.9f;
                    if (t >= BounceSeconds) Enter(State.Arrive, "run");
                    break;
                case State.Leave:
                    transform.position += velocity * Time.deltaTime;
                    transform.localScale *= Mathf.Max(0f, 1f - 1.5f * Time.deltaTime);
                    break;
            }
            if (buzz != null) buzz.pitch = Mathf.Lerp(buzz.pitch, pitch, 1f - Mathf.Exp(-8f * Time.deltaTime));
        }

        // Where it hovers: in front of the face, on the side it came from, wherever the head goes.
        private Vector3 HoverSpot(Vector3 head)
        {
            var toward = round.Front;
            var right = Vector3.Cross(Vector3.up, toward);
            return head + right * hoverOffset.x + Vector3.up * hoverOffset.y + toward * hoverOffset.z;
        }

        private void Face(Vector3 head)
        {
            var look = head - transform.position;
            if (look.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), 1f - Mathf.Exp(-10f * Time.deltaTime));
        }

        private void Enter(State next, string clip)
        {
            state = next;
            stateStarted = Time.time;
            if (animator != null && !string.IsNullOrEmpty(clip)) animator.CrossFadeInFixedTime(clip, 0.12f);
        }

        /// <summary>
        /// Takes its look for the active model: the rainbow bee's own rainbow paint (Rainbow Look), or the light bee tinted
        /// 'colour'; its aura to match (the rainbow bee's sparkles come in every colour).
        /// </summary>
        public void Dress(bool rainbow, Color colour)
        {
            var bodies = new System.Collections.Generic.List<Renderer>();
            foreach (var part in GetComponentsInChildren<Renderer>(false))
            {
                if (!(part is ParticleSystemRenderer)) bodies.Add(part);
            }
            parts = bodies.ToArray();
            block ??= new MaterialPropertyBlock();
            if (rainbow && rainbowLook != null)
            {
                foreach (var part in parts) part.sharedMaterial = rainbowLook;
            }
            Paint(rainbow ? Color.white : colour);
            foreach (var aura in auras)
            {
                if (aura == null) continue;
                var main = aura.main;
                float alpha = main.startColor.mode == ParticleSystemGradientMode.Color ? main.startColor.color.a : 1f;
                main.startColor = rainbow
                    ? new ParticleSystem.MinMaxGradient(RainbowGradient(alpha)) { mode = ParticleSystemGradientMode.RandomColor }
                    : new ParticleSystem.MinMaxGradient(new Color(colour.r, colour.g, colour.b, alpha));
                aura.Clear(true);
                aura.Play(true);
            }
        }

        private static Gradient RainbowGradient(float alpha)
        {
            var g = new Gradient();
            g.SetKeys(new[]
                {
                    new GradientColorKey(new Color(1f, 0.2f, 0.2f), 0f), new GradientColorKey(new Color(1f, 0.85f, 0.15f), 0.2f),
                    new GradientColorKey(new Color(0.25f, 0.95f, 0.3f), 0.4f), new GradientColorKey(new Color(0.2f, 0.9f, 1f), 0.6f),
                    new GradientColorKey(new Color(0.25f, 0.4f, 1f), 0.8f), new GradientColorKey(new Color(0.8f, 0.3f, 1f), 1f),
                },
                new[] { new GradientAlphaKey(alpha, 0f), new GradientAlphaKey(alpha, 1f) });
            return g;
        }

        private void Paint(Color colour)
        {
            foreach (var part in parts)
            {
                if (part == null) continue;
                part.GetPropertyBlock(block);
                block.SetColor(BaseColor, colour);
                block.SetColor(EmissionColor, colour * glow);
                part.SetPropertyBlock(block);
            }
        }
    }
}

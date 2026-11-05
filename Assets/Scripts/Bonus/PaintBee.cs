using UnityEngine;

namespace Game.Bonus
{
    /// <summary>
    /// A bee of the Paint Bees round (Paint_Bee: the bee's model, its buzzing wings and buzz, no Spider Brain): it flies
    /// in weaving from far in front of the player, hovers in front of their face (its warning), then dives at it, all on
    /// the beat of the round's song (user, 2026-11-03: the bees "follow fight song beat"): the round gives it the song
    /// times it reaches its hover spot, starts its dive and reaches the face, each on a beat, and it throbs to every beat.
    /// The round swats it (a hand of its colour: it bursts) or bounces it off (the wrong colour: it's knocked back and
    /// comes again, on the beat); one that reaches the face stings and flies off. A rainbow bee (Colour -1) is painted in
    /// every colour (its own look, Rainbow Look) and any painted hand swats it. Every bee has an aura in its colour (a
    /// glow, sparkles) and a light trail (red hot while it dives; the rainbow bee's in every colour).
    /// With a Flying Slime on it (Paint_Slime, user 2026-11-05: the stage's bees became flying paint slimes) the slime is
    /// its look: it takes the colour (Dress), squishes when bounced or swatted (Bonk), winds up before each dive, and its
    /// falling drops outlive it when it bursts; this only flies it.
    /// </summary>
    public class PaintBee : MonoBehaviour
    {
        private enum State { Arrive, Hover, Dive, Bounced, Leave, Popping }

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
        [Tooltip("How much bigger it throbs on each beat (0.12 = 12%).")]
        [Range(0f, 0.5f)]
        [SerializeField] private float beatThrob = 0.12f;
        [Tooltip("It dives only once the player has seen it this long, in seconds (within Seen Angle of where they look); " +
                 "till then it hovers on, a beat at a time, Most Waits beats at the most (user, 2026-11-04: \"I want Player to " +
                 "see them first before them start diving\").")]
        [Min(0f)]
        [SerializeField] private float seenSeconds = 0.4f;
        [Range(10f, 90f)]
        [SerializeField] private float seenAngle = 40f;
        [Min(0)]
        [SerializeField] private int mostWaits = 4;
        [Tooltip("The light trail it leaves, in its colour; red hot while it dives. Empty: none.")]
        [SerializeField] private TrailRenderer trail;
        [Tooltip("The rainbow bee's look: its own rainbow-painted material (not tinted).")]
        [SerializeField] private Material rainbowLook;
        [Tooltip("Its aura (a soft glow round it and a trail of sparkles), in its colour; the rainbow bee's in every colour.")]
        [SerializeField] private ParticleSystem[] auras = System.Array.Empty<ParticleSystem>();

        // A slime winds up this long (seconds, at most 3/4 of a beat) before it dives.
        private const float WindUpLead = 0.3f;
        private const float WeaveWidth = 0.35f;
        private const float WeavesPerSecond = 0.9f;
        private const float BounceSeconds = 0.55f;
        // Leaving: gone once this far off where the player looks (out of sight), or after Leave Seconds anyway.
        private const float LeaveSeconds = 4f;
        private const float LeaveSightAngle = 70f;
        private static readonly Color Hot = new(1f, 0.25f, 0.08f, 1f);

        private State state;
        private float stateStarted;
        private PaintBeesRound round;
        private Vector3 hoverOffset;
        // Song times (the round's clock): set off, at the hover spot, the dive starts, at the face.
        private float leftAt, arriveAt, diveAt, hitAt;
        private Vector3 from;
        private Vector3 velocity;
        private float weavePhase;
        private float pitch = 1f;
        private Vector3 size = Vector3.one;
        private Renderer[] parts = System.Array.Empty<Renderer>();
        private MaterialPropertyBlock block;
        private Color shownColour = Color.white;
        // Swatted: what to do when it bursts, how long it holds, how big it swells, where it was hit.
        private System.Action popped;
        private float popSeconds;
        private float popSwell = 1.35f;
        private Vector3 popAt;
        // How long the player has had it in view, and how many beats it has waited for that.
        private float seenFor;
        private int waited;
        // Its look, if it's a slime (Paint_Slime); and whether it has wound up for this dive yet.
        private Game.Enemies.FlyingSlime slime;
        private bool woundUp;

        /// <summary>Its colour index; -1 = the rainbow bee.</summary>
        public int Colour { get; private set; }

        /// <summary>The rainbow bee (any painted hand swats it).</summary>
        public bool Rainbow => Colour < 0;

        /// <summary>How big it is to hit (radius, metres).</summary>
        public float Radius => radius * size.x;

        /// <summary>It's on its way out (stung, or swatted): no more hits.</summary>
        public bool Leaving => state == State.Leave || state == State.Popping;

        /// <summary>It's diving at the face right now.</summary>
        public bool Diving => state == State.Dive;

        /// <summary>A small one, split off a swatted big one (it doesn't split again).</summary>
        public bool Small { get; private set; }

        /// <summary>Where in front of the face it hovers (the head's flat frame: x right, y up, z towards it).</summary>
        public Vector3 HoverOffset => hoverOffset;

        /// <summary>Comes again this many beats later than the round says, once (two split-off halves, one after the other).</summary>
        public int LaterBeats { get; set; }

        /// <summary>Makes it a small one (the round sizes it).</summary>
        public void MakeSmall() => Small = true;

        /// <summary>
        /// Sets it off from where it is: its colour, where in front of the face it hovers (from the head, in the head's
        /// flat frame: x right, y up, z towards it), and the song times it gets there, starts its dive and reaches the face.
        /// </summary>
        public void Launch(PaintBeesRound owner, int colour, Vector3 hoverAt, float arrive, float dive, float hit)
        {
            round = owner;
            Colour = colour;
            slime = GetComponent<Game.Enemies.FlyingSlime>();
            hoverOffset = hoverAt;
            size = transform.localScale;
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
            Schedule(arrive, dive, hit);
        }

        /// <summary>Knocked back along 'push' (metres a second) by the wrong hand: it tumbles off, then comes again.</summary>
        public void Bounce(Vector3 push)
        {
            if (state == State.Leave) return;
            velocity = push;
            Enter(State.Bounced, "dodge_back");
            if (slime != null) slime.Bonk(transform.position - push.normalized * 0.3f, 1f);
        }

        /// <summary>
        /// Swatted (user, 2026-11-05: burst "like a balloon of paint", "nhoe nhoét"): it holds still for Seconds (the
        /// hit-stop), squashed flat by the fist and flashing pale, then swells to Swell times its size like a balloon about
        /// to pop; then Burst is called (the round's splash) and it's gone.
        /// </summary>
        public void Pop(float seconds, float swell, System.Action burst)
        {
            if (state == State.Leave || state == State.Popping) return;
            popped = burst;
            popSeconds = Mathf.Max(0.01f, seconds);
            popSwell = swell;
            popAt = transform.position;
            Enter(State.Popping, null);
            if (animator != null) animator.speed = 0f;
            if (trail != null) trail.emitting = false;
            if (buzz != null) buzz.Stop();
            // A slime: squashed by the fist, flashing white, flinging a few drops.
            if (slime != null && round != null && round.Head != null) slime.Bonk(round.Head.position, 1.3f);
            // Pale and bright for the moment it's about to go.
            foreach (var part in parts)
            {
                if (part == null) continue;
                part.GetPropertyBlock(block);
                block.SetColor(BaseColor, Color.Lerp(shownColour, Color.white, 0.5f));
                block.SetColor(EmissionColor, Color.Lerp(shownColour, Color.white, 0.6f) * 1.4f);
                part.SetPropertyBlock(block);
            }
        }

        /// <summary>
        /// It flies off (it stung, or the round ended): out to its side and up, fast, out of the player's sight, and is gone
        /// there (user, 2026-11-05: "it flies away from the game and DIES off screen").
        /// </summary>
        public void FlyOff()
        {
            if (state == State.Leave || state == State.Popping) return;
            var head = round != null && round.Head != null ? round.Head : null;
            var look = round != null ? round.Looking : transform.forward;
            var side = Vector3.Cross(Vector3.up, look);
            if (head != null && Vector3.Dot(transform.position - head.position, side) < 0f) side = -side;
            velocity = (side * 1f + Vector3.up * 0.55f - look * 0.25f).normalized * 4f;
            Enter(State.Leave, "run");
            if (trail != null) trail.emitting = false;
        }

        // From where it is now: at the hover spot at Arrive, diving at Dive, at the face at Hit (song times).
        private void Schedule(float arrive, float dive, float hit)
        {
            leftAt = round.SongTime;
            arriveAt = Mathf.Max(arrive, leftAt + 0.05f);
            diveAt = Mathf.Max(dive, arriveAt);
            hitAt = Mathf.Max(hit, diveAt + 0.05f);
            from = transform.position;
            woundUp = false;
            Enter(State.Arrive, "run");
        }

        private void Update()
        {
            if (round == null || round.Head == null) return;
            var head = round.Head.position;
            float now = round.SongTime;
            switch (state)
            {
                case State.Arrive:
                {
                    // Slowing as it comes, weaving from side to side less and less, at its spot on the beat.
                    var spot = HoverSpot(head);
                    float u = Mathf.Clamp01(Mathf.InverseLerp(leftAt, arriveAt, now));
                    float eased = 1f - (1f - u) * (1f - u);
                    var way = spot - from;
                    way.y = 0f;
                    var side = way.sqrMagnitude > 0.0001f ? Vector3.Cross(Vector3.up, way.normalized) : Vector3.right;
                    float weave = Mathf.Sin((Time.time + weavePhase) * WeavesPerSecond * 2f * Mathf.PI) * WeaveWidth * (1f - u);
                    transform.position = Vector3.Lerp(from, spot, eased) + side * weave;
                    Face(head);
                    pitch = 1f;
                    if (InView()) seenFor += Time.deltaTime;
                    if (u >= 1f) Enter(State.Hover, "alert");
                    break;
                }
                case State.Hover:
                {
                    // Still in front of the face (it follows the head), shaking more and more: it's about to dive.
                    float k = Mathf.Clamp01(Mathf.InverseLerp(arriveAt, diveAt, now));
                    transform.position = HoverSpot(head) + Random.insideUnitSphere * (0.012f * k);
                    Face(head);
                    pitch = Mathf.Lerp(1.1f, 1.4f, k);
                    if (InView()) seenFor += Time.deltaTime;
                    // Not seen long enough yet: it waits a beat more (still on the beat).
                    if (now >= diveAt && seenFor < seenSeconds && waited < mostWaits)
                    {
                        waited++;
                        float beat = round.BeatLength;
                        diveAt += beat;
                        hitAt += beat;
                        woundUp = false;
                    }
                    // A slime winds up just before it dives (flat, leaning back, wings beating hard).
                    float lead = Mathf.Min(WindUpLead, round.BeatLength * 0.75f);
                    if (slime != null && !woundUp && now >= diveAt - lead)
                    {
                        woundUp = true;
                        slime.WindUp(Mathf.Max(0.05f, diveAt - now));
                    }
                    if (now >= diveAt)
                    {
                        from = transform.position;
                        Enter(State.Dive, "attack");
                    }
                    break;
                }
                case State.Dive:
                {
                    // Faster and faster, at the face on the beat.
                    float u = Mathf.Clamp01(Mathf.InverseLerp(diveAt, hitAt, now));
                    transform.position = Vector3.Lerp(from, head, u * u);
                    Face(head);
                    pitch = 1.5f;
                    if (u >= 1f) round.Stung(this);
                    break;
                }
                case State.Bounced:
                    transform.position += velocity * Time.deltaTime;
                    velocity = Vector3.Lerp(velocity, Vector3.zero, 1f - Mathf.Exp(-5f * Time.deltaTime));
                    transform.Rotate(Vector3.up, 720f * Time.deltaTime, Space.World);
                    pitch = 0.9f;
                    if (Time.time - stateStarted >= BounceSeconds)
                    {
                        round.ComeAgain(out float arrive, out float dive, out float hit);
                        if (LaterBeats > 0)
                        {
                            float later = LaterBeats * round.BeatLength;
                            arrive += later;
                            dive += later;
                            hit += later;
                            LaterBeats = 0;
                        }
                        Schedule(arrive, dive, hit);
                    }
                    break;
                case State.Leave:
                {
                    // Faster and faster; gone once out of sight (or after a while).
                    velocity *= 1f + 1.5f * Time.deltaTime;
                    transform.position += velocity * Time.deltaTime;
                    var eyes = round.Head;
                    var to = transform.position - eyes.position;
                    bool outOfSight = Vector3.Angle(eyes.forward, to) > LeaveSightAngle && to.magnitude > 1.2f;
                    if (outOfSight || Time.time - stateStarted > LeaveSeconds)
                    {
                        if (slime != null) slime.ReleaseDrops();
                        Destroy(gameObject);
                        return;
                    }
                    break;
                }
                case State.Popping:
                {
                    // Held where it was hit, shuddering: squashed flat (towards the face, where the fist comes from) for
                    // the first part, then swelling round past its size, and it bursts.
                    float k = Mathf.Clamp01((Time.time - stateStarted) / popSeconds);
                    transform.position = popAt + Random.insideUnitSphere * 0.006f;
                    var squashed = new Vector3(1.22f, 1.22f, 0.72f);
                    var shape = k < 0.4f ? Vector3.Lerp(Vector3.one, squashed, k / 0.4f) : Vector3.Lerp(squashed, Vector3.one * popSwell, (k - 0.4f) / 0.6f);
                    transform.localScale = Vector3.Scale(size, shape);
                    if (k >= 1f)
                    {
                        var burst = popped;
                        popped = null;
                        state = State.Leave;
                        burst?.Invoke();
                        if (slime != null) slime.ReleaseDrops();
                        Destroy(gameObject);
                    }
                    return;
                }
            }
            // It throbs to the beat while it comes and hovers.
            bool throbs = state == State.Arrive || state == State.Hover;
            transform.localScale = size * (1f + (throbs ? beatThrob * round.BeatPulse : 0f));
            if (buzz != null) buzz.pitch = Mathf.Lerp(buzz.pitch, pitch, 1f - Mathf.Exp(-8f * Time.deltaTime));
            // Its trail: its colour, red hot while it dives (the rainbow bee's stays every colour: see Dress).
            if (trail != null && !Rainbow)
            {
                var front = state == State.Dive ? Color.Lerp(shownColour, Hot, 0.75f) : shownColour;
                front.a = 0.85f;
                var tail = front;
                tail.a = 0f;
                trail.startColor = front;
                trail.endColor = tail;
            }
        }

        // Where it hovers: in front of the face, on the side it came from, wherever the head goes.
        private Vector3 HoverSpot(Vector3 head)
        {
            var toward = round.Looking;
            var right = Vector3.Cross(Vector3.up, toward);
            return head + right * hoverOffset.x + Vector3.up * hoverOffset.y + toward * hoverOffset.z;
        }

        // In the player's view: within Seen Angle of where they look.
        private bool InView()
        {
            var eyes = round.Head;
            var to = transform.position - eyes.position;
            return to.sqrMagnitude > 0.0001f && Vector3.Angle(eyes.forward, to) <= seenAngle;
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
            // A slime takes the colour itself (only its painted parts; the rainbow in its shader).
            slime ??= GetComponent<Game.Enemies.FlyingSlime>();
            if (slime != null)
            {
                slime.Dress(colour, rainbow);
                parts = System.Array.Empty<Renderer>();
                block ??= new MaterialPropertyBlock();
                shownColour = rainbow ? Color.white : colour;
                DressExtras(rainbow, colour);
                return;
            }
            // Its body (not its aura or trail, which take its colour by themselves).
            var bodies = new System.Collections.Generic.List<Renderer>();
            foreach (var part in GetComponentsInChildren<Renderer>(false))
            {
                if (!(part is ParticleSystemRenderer) && !(part is TrailRenderer)) bodies.Add(part);
            }
            parts = bodies.ToArray();
            block ??= new MaterialPropertyBlock();
            if (rainbow && rainbowLook != null)
            {
                foreach (var part in parts) part.sharedMaterial = rainbowLook;
            }
            Paint(rainbow ? Color.white : colour);
            DressExtras(rainbow, colour);
        }

        // Its trail and aura in its colour (the rainbow bee's in every colour).
        private void DressExtras(bool rainbow, Color colour)
        {
            if (rainbow && trail != null)
            {
                var g = RainbowGradient(0.85f);
                g.SetKeys(g.colorKeys, new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) });
                trail.colorGradient = g;
            }
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
            shownColour = colour;
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

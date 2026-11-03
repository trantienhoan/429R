using UnityEngine;

namespace Game.Bonus
{
    /// <summary>
    /// A bee of the Paint Bees round (Paint_Bee: the bee's model, its buzzing wings and buzz, no Spider Brain): it flies
    /// in weaving from far in front of the player, hovers in front of their face (its warning), then dives at it, all on
    /// the beat of the round's song (user, 2026-11-03: the bees "follow fight song beat"): the round gives it the song
    /// times it reaches its hover spot, starts its dive and reaches the face, each on a beat, and it throbs to every beat.
    /// The round swats it (a hand of its colour: it bursts) or bounces it off (the wrong colour: it's knocked back and
    /// comes again, on the beat); one that reaches the face stings and flies off. A rainbow bee (Colour -1) cycles
    /// through the colours and any painted hand swats it.
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
        [SerializeField] private float glow = 0.65f;
        [Tooltip("How much bigger it throbs on each beat (0.12 = 12%).")]
        [Range(0f, 0.5f)]
        [SerializeField] private float beatThrob = 0.12f;
        [Tooltip("The light trail it leaves, in its colour; red hot while it dives. Empty: none.")]
        [SerializeField] private TrailRenderer trail;

        private const float WeaveWidth = 0.35f;
        private const float WeavesPerSecond = 0.9f;
        private const float BounceSeconds = 0.55f;
        private const float LeaveSeconds = 0.9f;
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

        /// <summary>Its colour index; -1 = the rainbow bee.</summary>
        public int Colour { get; private set; }

        /// <summary>The rainbow bee (any painted hand swats it).</summary>
        public bool Rainbow => Colour < 0;

        /// <summary>How big it is to hit (radius, metres).</summary>
        public float Radius => radius * size.x;

        /// <summary>It's on its way out (stung, or swatted): no more hits.</summary>
        public bool Leaving => state == State.Leave;

        /// <summary>It's diving at the face right now.</summary>
        public bool Diving => state == State.Dive;

        /// <summary>
        /// Sets it off from where it is: its colour, where in front of the face it hovers (from the head, in the head's
        /// flat frame: x right, y up, z towards it), and the song times it gets there, starts its dive and reaches the face.
        /// </summary>
        public void Launch(PaintBeesRound owner, int colour, Vector3 hoverAt, float arrive, float dive, float hit)
        {
            round = owner;
            Colour = colour;
            hoverOffset = hoverAt;
            size = transform.localScale;
            weavePhase = Random.value * 10f;
            if (looks.Length > 0)
            {
                int pick = Random.Range(0, looks.Length);
                for (int i = 0; i < looks.Length; i++) if (looks[i] != null) looks[i].SetActive(i == pick);
            }
            // Its body (not its trail, which takes its colour by itself).
            parts = System.Array.FindAll(GetComponentsInChildren<Renderer>(false), r => !(r is TrailRenderer));
            block = new MaterialPropertyBlock();
            Paint(Rainbow ? Color.white : round.ColourOf(colour));
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
        }

        /// <summary>It flies off up and away (it stung), and is gone.</summary>
        public void FlyOff()
        {
            if (state == State.Leave) return;
            var away = transform.position - round.Head.position;
            away.y = 0f;
            velocity = (away.sqrMagnitude > 0.001f ? away.normalized : -transform.forward) * 2.5f + Vector3.up * 2f;
            Enter(State.Leave, "run");
            if (trail != null) trail.emitting = false;
            Destroy(gameObject, LeaveSeconds);
        }

        // From where it is now: at the hover spot at Arrive, diving at Dive, at the face at Hit (song times).
        private void Schedule(float arrive, float dive, float hit)
        {
            leftAt = round.SongTime;
            arriveAt = Mathf.Max(arrive, leftAt + 0.05f);
            diveAt = Mathf.Max(dive, arriveAt);
            hitAt = Mathf.Max(hit, diveAt + 0.05f);
            from = transform.position;
            Enter(State.Arrive, "run");
        }

        private void Update()
        {
            if (round == null || round.Head == null) return;
            var head = round.Head.position;
            float now = round.SongTime;
            if (Rainbow) Paint(Color.HSVToRGB(Mathf.Repeat(Time.time * 0.6f, 1f), 0.75f, 1f));
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
                        Schedule(arrive, dive, hit);
                    }
                    break;
                case State.Leave:
                    transform.position += velocity * Time.deltaTime;
                    size *= Mathf.Max(0f, 1f - 1.5f * Time.deltaTime);
                    break;
            }
            // It throbs to the beat while it comes and hovers.
            bool throbs = state == State.Arrive || state == State.Hover;
            transform.localScale = size * (1f + (throbs ? beatThrob * round.BeatPulse : 0f));
            if (buzz != null) buzz.pitch = Mathf.Lerp(buzz.pitch, pitch, 1f - Mathf.Exp(-8f * Time.deltaTime));
            // Its trail: its colour, red hot while it dives.
            if (trail != null)
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

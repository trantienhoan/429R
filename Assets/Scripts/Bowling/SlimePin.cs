using Game.Enemies;
using UnityEngine;

namespace Game.Bowling
{
    /// <summary>
    /// How a pin set's pins grow and what they hatch into (set by the bowling stage for each poop). Runtime only.
    /// </summary>
    public sealed class PinGrowth
    {
        /// <summary>Seconds from landing until the pin is full fat (then the set hatches it).</summary>
        public float seconds = 24f;
        /// <summary>The last seconds of the growth it shivers.</summary>
        public float shiverLast = 4f;
        /// <summary>The flyer a pin hatches into (Slime_Pin_Flyer).</summary>
        public GameObject flyerPrefab;
        /// <summary>The Enemy Wave the flyers join (the stage).</summary>
        public object waveOwner;
        /// <summary>No flyer comes out while that wave has this many alive (the pin just pops).</summary>
        public int maxFlyersAlive = 8;
        /// <summary>Where the flyers are put (CurrentlySpawnedStuffs, so the game's clean-up removes them).</summary>
        public Transform flyerParent;
    }

    /// <summary>
    /// One slime pin of Gate 2 ("Strike the Slimes"): a little wingless slime that a giant flying slime poops onto a lane.
    /// Drop flies it down kinematic and lands it upright; then it is a normal physics pin (tiny foot, capsule body) that
    /// grows from thin and tall to fat and short over its grow seconds, breathing a little, and shivers the last seconds.
    /// Its Pin Set decides when it is down, pops it (a paint burst) or hatches it (a flyer, if the caps allow).
    /// The root holds the Rigidbody and colliders; the child "Shape" (pivot at the foot) holds the look.
    /// Built by Tools > 429 Game > Bowling > Set Up Slime Pins (Slime_Pin).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SlimePin : MonoBehaviour
    {
        [Header("Parts (Set Up Slime Pins fills these)")]
        [Tooltip("The look (pivot at the foot): scaled as the pin grows. Never the root.")]
        [SerializeField] private Transform shape;
        [Tooltip("The slime's body renderer: it takes the paint colour.")]
        [SerializeField] private Renderer body;
        [Tooltip("The tiny foot box it stands on.")]
        [SerializeField] private BoxCollider foot;
        [Tooltip("The body collider, resized to the pin's size as it grows.")]
        [SerializeField] private CapsuleCollider capsule;
        [Tooltip("Plays the shiver.")]
        [SerializeField] private AudioSource speaker;
        [Tooltip("Size of everything under Shape at scale 1, in metres (x wide, y tall, z deep). Measured by the menu.")]
        [SerializeField] private Vector3 modelSize = new(0.26f, 0.33f, 0.25f);

        [Header("Size")]
        [Tooltip("Height and width when it lands, in metres (thin and tall).")]
        [SerializeField] private Vector2 startSize = new(0.66f, 0.24f);
        [Tooltip("Height and width when full grown, in metres (fat and short, about the same volume).")]
        [SerializeField] private Vector2 fullSize = new(0.36f, 0.40f);
        [Tooltip("Size of the breathing (0.02 = 2%).")]
        [Range(0f, 0.2f)]
        [SerializeField] private float breathe = 0.02f;
        [Tooltip("Breaths per second.")]
        [Min(0f)]
        [SerializeField] private float breathsPerSecond = 0.6f;
        [Tooltip("Size of the shiver at its end (0.04 = 4%).")]
        [Range(0f, 0.2f)]
        [SerializeField] private float shiver = 0.04f;
        [Tooltip("Shivers per second at its start and at its end.")]
        [SerializeField] private Vector2 shiverRate = new(6f, 14f);
        [Tooltip("Seconds between resizes of the capsule.")]
        [Min(0.02f)]
        [SerializeField] private float resizeEvery = 0.25f;
        [Tooltip("How far above the pin's foot point the capsule's bottom is, in metres (it stands on the foot box).")]
        [Min(0f)]
        [SerializeField] private float capsuleLift = 0.02f;

        [Header("Pop and hatch")]
        [Tooltip("The paint burst when it pops (Slime_Pin_Pop).")]
        [SerializeField] private GameObject popPrefab;
        [Tooltip("The body width of the pop prefab at scale 1, in metres (Flying_Slime: 0.26).")]
        [Min(0.01f)]
        [SerializeField] private float popBodyWidth = 0.26f;
        [Tooltip("The puff of smoke (Smoke_Flash_Small_Puff).")]
        [SerializeField] private GameObject puff;
        [Tooltip("Puff size when it hatches.")]
        [Min(0f)]
        [SerializeField] private float hatchPuff = 0.35f;
        [Tooltip("How fast the flyer is flung up when it hatches, in metres per second.")]
        [SerializeField] private float hatchPush = 1.5f;

        [Header("Sounds")]
        [Tooltip("Once when it starts to shiver (slime_000).")]
        [SerializeField] private AudioClip shiverSound;
        [Range(0f, 1f)]
        [SerializeField] private float shiverVolume = 0.4f;
        [Tooltip("When it hatches (Balloon_Pop).")]
        [SerializeField] private AudioClip hatchSound;
        [Range(0f, 1f)]
        [SerializeField] private float hatchVolume = 0.7f;

        /// <summary>The set it belongs to.</summary>
        public PinSet Set { get; private set; }
        /// <summary>Its rack slot (0 = head pin).</summary>
        public int Slot { get; private set; }
        /// <summary>It has landed (it is a physics pin and growing).</summary>
        public bool Landed { get; private set; }
        /// <summary>Its set saw it knocked over (it pops soon).</summary>
        public bool Down { get; private set; }
        /// <summary>Popped or hatched: ignore it.</summary>
        public bool Done { get; private set; }
        /// <summary>How far it has grown, 0 (thin) to 1 (full fat).</summary>
        public float Grown { get; private set; }
        /// <summary>It hatched (gone, not down).</summary>
        public bool Hatched { get; private set; }
        /// <summary>Its Rigidbody.</summary>
        public Rigidbody Body => rigid;
        /// <summary>Its height and width now, in metres.</summary>
        public Vector2 SizeNow => new(height, width);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private Rigidbody rigid;
        private MaterialPropertyBlock block;
        private Color colour = Color.white;
        private float growSeconds = 24f;
        private float shiverLast = 4f;
        private float age;
        private bool flying;
        private float flyTime, flySeconds;
        private Vector3 from, to;
        private Quaternion upright;
        private RigidbodyInterpolation interpolation = RigidbodyInterpolation.Interpolate;
        private CollisionDetectionMode detection = CollisionDetectionMode.Continuous;
        private float height, width;
        private float flightScale = 1f;
        private float nextResize;
        private bool shivering;
        private float shiverPhase;
        private float breathOffset;

        private void Awake()
        {
            rigid = GetComponent<Rigidbody>();
            if (shape == null) shape = transform.Find("Shape");
            if (speaker == null) speaker = GetComponent<AudioSource>();
            if (capsule == null) capsule = GetComponent<CapsuleCollider>();
            if (foot == null) foot = GetComponent<BoxCollider>();
            block = new MaterialPropertyBlock();
            breathOffset = Random.value * 10f;
            height = startSize.x;
            width = startSize.y;
            ApplyShape();
            ResizeCapsule();
            if (puff != null) EffectPool.Warm(puff);
        }

        /// <summary>Joins it to its set (Pin Set calls this before Drop).</summary>
        internal void Bind(PinSet set, int slot)
        {
            Set = set;
            Slot = slot;
            if (set != null && set.Growth != null) shiverLast = Mathf.Max(0f, set.Growth.shiverLast);
        }

        /// <summary>
        /// Flies it from From to To (on the floor) in Seconds, kinematic: flat straight, falling faster and faster, its
        /// look growing from 0.6 to full size. It lands upright at Yaw degrees, dressed in Colour, then it is a physics
        /// pin and grows over Grow Seconds.
        /// </summary>
        public void Drop(Vector3 from, Vector3 to, float yaw, float seconds, Color colour, float growSeconds)
        {
            if (Done) return;
            this.from = from;
            this.to = to;
            this.colour = colour;
            this.growSeconds = Mathf.Max(0.1f, growSeconds);
            upright = Quaternion.Euler(0f, yaw, 0f);
            flySeconds = Mathf.Max(0.01f, seconds);
            flyTime = 0f;
            flying = true;
            Landed = false;
            age = 0f;
            Grown = 0f;
            Paint();
            if (rigid != null)
            {
                interpolation = rigid.interpolation;
                detection = rigid.collisionDetectionMode;
                rigid.interpolation = RigidbodyInterpolation.None;
                // Kinematic bodies can't use Continuous: speculative while it flies.
                rigid.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                rigid.isKinematic = true;
            }
            transform.SetPositionAndRotation(from, upright);
            flightScale = 0.6f;
            height = startSize.x;
            width = startSize.y;
            ApplyShape();
        }

        /// <summary>Its paint burst (Slime_Pin_Pop, with its own sounds), blobs flying away from From if given. No flyer.</summary>
        public void Pop(Vector3? from)
        {
            if (Done) return;
            Done = true;
            if (popPrefab != null)
            {
                var burst = Instantiate(popPrefab, Middle(), transform.rotation);
                burst.transform.localScale = Vector3.one * Mathf.Max(0.05f, width / popBodyWidth);
                if (burst.TryGetComponent(out FlyingSlime look))
                {
                    look.Dress(colour);
                    look.Pop(from);
                }
                else Destroy(burst, 3f);
            }
            Destroy(gameObject);
        }

        /// <summary>
        /// Hatches it: a puff and a pop, the pin is gone, and a flyer bursts out of its top if the wave has fewer than
        /// Growth's cap alive and an enemy slot is free (else just the puff).
        /// </summary>
        public void Hatch(PinGrowth growth, Color colour)
        {
            if (Done) return;
            Done = true;
            Hatched = true;
            var middle = Middle();
            var top = transform.position + transform.up * height;
            if (puff != null) EffectPool.Play(puff, middle, hatchPuff);
            PlayAt(hatchSound, middle, hatchVolume);
            var g = growth ?? new PinGrowth();
            if (g.flyerPrefab != null && EnemyWave.Alive(g.waveOwner) < g.maxFlyersAlive && EnemyLimit.TryTakeSlot())
            {
                var head = SlimeKit.Head;
                var face = head != null ? head.position - top : transform.forward;
                face.y = 0f;
                var turn = face.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(face.normalized, Vector3.up) : Quaternion.identity;
                var flyer = Instantiate(g.flyerPrefab, top, turn, g.flyerParent);
                if (flyer.TryGetComponent(out SlimeBomber bomber)) bomber.Launch(colour, Vector3.up * hatchPush);
                EnemyWave.Add(g.waveOwner, flyer);
            }
            Destroy(gameObject);
        }

        /// <summary>Its set saw it knocked over (Pin Set calls this).</summary>
        internal void MarkDown() => Down = true;

        private void OnCollisionEnter(Collision collision)
        {
            if (Done || !Landed || Set == null) return;
            Set.PinContact(this, collision);
        }

        private void Update()
        {
            if (Done) return;
            float dt = Time.deltaTime;
            if (flying)
            {
                Fly(dt);
                return;
            }
            if (!Landed || dt <= 0f) return;

            age += dt;
            Grown = Mathf.Clamp01(age / growSeconds);
            float g = Mathf.SmoothStep(0f, 1f, Grown);
            height = Mathf.Lerp(startSize.x, fullSize.x, g);
            width = Mathf.Lerp(startSize.y, fullSize.y, g);

            // A tiny breath (taller and thinner, then back), and a shiver that grows over the last seconds.
            float breath = Mathf.Sin((age + breathOffset) * 2f * Mathf.PI * breathsPerSecond) * breathe;
            float shake = 0f;
            float shiverFrom = growSeconds - shiverLast;
            if (shiverLast > 0f && age >= shiverFrom)
            {
                if (!shivering)
                {
                    shivering = true;
                    if (shiverSound != null && speaker != null) speaker.PlayOneShot(shiverSound, shiverVolume);
                }
                float u = Mathf.Clamp01((age - shiverFrom) / shiverLast);
                shiverPhase += Mathf.Lerp(shiverRate.x, shiverRate.y, u) * dt;
                shake = Mathf.Sin(shiverPhase * 2f * Mathf.PI) * shiver * u * u;
            }
            ApplyShape(breath + shake, -breath * 0.5f + shake);

            if (Time.time >= nextResize)
            {
                nextResize = Time.time + resizeEvery;
                ResizeCapsule();
            }
        }

        private void Fly(float dt)
        {
            flyTime += dt;
            float s = Mathf.Clamp01(flyTime / flySeconds);
            var flat = Vector3.Lerp(from, to, s);
            flat.y = from.y + (to.y - from.y) * s * s;
            transform.SetPositionAndRotation(flat, upright);
            flightScale = Mathf.Lerp(0.6f, 1f, s);
            ApplyShape();
            if (s >= 1f) Land();
        }

        private void Land()
        {
            flying = false;
            Landed = true;
            flightScale = 1f;
            transform.SetPositionAndRotation(to, upright);
            ApplyShape();
            ResizeCapsule();
            nextResize = Time.time + resizeEvery;
            if (rigid != null)
            {
                rigid.position = to;
                rigid.rotation = upright;
                rigid.isKinematic = false;
                rigid.collisionDetectionMode = detection;
                rigid.interpolation = interpolation;
                rigid.linearVelocity = Vector3.zero;
                rigid.angularVelocity = Vector3.zero;
            }
        }

        // Shape's scale for the current height and width (times the flight's grow-in), plus a stretch (share of the size).
        private void ApplyShape(float stretchUp = 0f, float stretchSide = 0f)
        {
            if (shape == null) return;
            float mx = Mathf.Max(0.001f, modelSize.x);
            float my = Mathf.Max(0.001f, modelSize.y);
            float f = flightScale;
            float side = width / mx * f * (1f + stretchSide);
            shape.localScale = new Vector3(side, height / my * f * (1f + stretchUp), side);
        }

        // The capsule to the pin's size now (a sphere when it's fatter than it is tall).
        private void ResizeCapsule()
        {
            if (capsule == null) return;
            float tall = Mathf.Max(0.05f, height - capsuleLift);
            float radius = Mathf.Min(width * 0.5f, tall * 0.5f);
            capsule.direction = 1;
            capsule.radius = radius;
            capsule.height = tall;
            capsule.center = new Vector3(0f, capsuleLift + tall * 0.5f, 0f);
        }

        private void Paint()
        {
            if (body == null) return;
            block ??= new MaterialPropertyBlock();
            body.GetPropertyBlock(block);
            block.SetColor(BaseColorId, colour);
            body.SetPropertyBlock(block);
        }

        // The middle of the pin (half its height up its own up).
        private Vector3 Middle() => transform.position + transform.up * (height * 0.5f);

        // A sound that outlives the pin, heard like the pin's own speaker.
        private void PlayAt(AudioClip clip, Vector3 at, float volume)
        {
            if (clip == null) return;
            var go = new GameObject("Slime Pin Sound");
            go.transform.position = at;
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            if (speaker != null)
            {
                source.outputAudioMixerGroup = speaker.outputAudioMixerGroup;
                source.spatialBlend = speaker.spatialBlend;
                source.rolloffMode = speaker.rolloffMode;
                source.minDistance = speaker.minDistance;
                source.maxDistance = speaker.maxDistance;
                source.dopplerLevel = speaker.dopplerLevel;
            }
            else source.spatialBlend = 1f;
            source.PlayOneShot(clip, volume);
            Destroy(go, clip.length + 0.1f);
        }
    }
}

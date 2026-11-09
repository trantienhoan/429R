using System;
using System.Collections;
using System.Collections.Generic;
using Game.Enemies;
using UnityEngine;

namespace Game.Bowling
{
    /// <summary>
    /// The Giant Flying Slime of Gate 2 ("Strike the Slimes"): a Flying Slime blown up big, as a puppet. It has no
    /// collider, no brain, no NavMesh and no Enemy tag, so throws never aim at it and it takes no enemy slot. The
    /// bowling stage tells it what to do:
    /// - Setup: its colour, size and wing sound. Arrive: an eased spiral down from high up to its circle, its wing
    ///   sound growing from nothing. Circle: round the player's head (it looks at the player).
    /// - Fly To: a straight, eased flight to a point, then it hovers there looking at Look At.
    /// - Strain: the squeeze before a poop (pins or divers come out of its Bottom Point), sometimes a giggle after.
    /// - Bonk (hurt), Burst (split: a paint burst), Lose Wing (one falls off and tumbles down), Grow In (a split-off
    ///   half), Drop (falls to the floor, then it is gone) and Leave (flies up and away, then it is gone).
    /// It moves itself in Update (scaled time) and bobs, banks into turns and, with one wing, flies tilted.
    /// Tools > 429 Game > Bowling > Set Up Giant Slime And Showdown builds it (Prefabs/Bowling/Giant_Flying_Slime).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GiantFlyingSlime : MonoBehaviour
    {
        /// <summary>What it is doing now.</summary>
        public enum Mode { Idle, Arriving, Circling, Travelling, Hovering, Straining, Dropping, Leaving, Gone }

        [Header("Parts (the setup menu fills these)")]
        [Tooltip("The Flying Slime look (wings, squash, colour, pop).")]
        [SerializeField] private FlyingSlime look;
        [Tooltip("The left wing (Slime_Wing_L under Model): the first to come off.")]
        [SerializeField] private Transform wingLeft;
        [Tooltip("The right wing (Slime_Wing_R under Model).")]
        [SerializeField] private Transform wingRight;
        [Tooltip("Its looping wing sound (bee_fly).")]
        [SerializeField] private AudioSource voice;
        [Tooltip("The smoke puff (Smoke_Flash_Small_Puff).")]
        [SerializeField] private GameObject puff;

        [Header("Sounds")]
        [Tooltip("Straining to poop (slime_000, slime_001).")]
        [SerializeField] private AudioClip[] poopSounds = Array.Empty<AudioClip>();
        [Range(0f, 1f)]
        [SerializeField] private float poopVolume = 0.9f;
        [SerializeField] private float poopPitch = 0.6f;
        [Tooltip("A giggle after a poop (cute_laugh, cute_laugh_2).")]
        [SerializeField] private AudioClip[] laughSounds = Array.Empty<AudioClip>();
        [Range(0f, 1f)]
        [SerializeField] private float laughVolume = 0.8f;
        [SerializeField] private float laughPitch = 0.7f;
        [Tooltip("Chance (0 to 1) of a giggle after a poop (only with both wings).")]
        [Range(0f, 1f)]
        [SerializeField] private float laughChance = 0.3f;
        [Tooltip("Bonked (slime_001).")]
        [SerializeField] private AudioClip[] hitSounds = Array.Empty<AudioClip>();
        [Range(0f, 1f)]
        [SerializeField] private float hitVolume = 1f;
        [SerializeField] private float hitPitch = 0.5f;
        [Tooltip("Bursting to split (balloon_explode_1).")]
        [SerializeField] private AudioClip[] burstSounds = Array.Empty<AudioClip>();
        [Range(0f, 1f)]
        [SerializeField] private float burstVolume = 0.8f;
        [Tooltip("A wing coming off (Balloon_Pop).")]
        [SerializeField] private AudioClip[] wingOffSounds = Array.Empty<AudioClip>();
        [Range(0f, 1f)]
        [SerializeField] private float wingOffVolume = 0.8f;
        [Tooltip("Falling with no wings (Cartoon_fall_1, Cartoon_fall_2).")]
        [SerializeField] private AudioClip[] fallSounds = Array.Empty<AudioClip>();
        [Range(0f, 1f)]
        [SerializeField] private float fallVolume = 0.9f;
        [Tooltip("Landing on the floor (pumpkin_face_splat).")]
        [SerializeField] private AudioClip[] landSounds = Array.Empty<AudioClip>();
        [Range(0f, 1f)]
        [SerializeField] private float landVolume = 1f;
        [Tooltip("Flying away (fairy_fly_away).")]
        [SerializeField] private AudioClip[] leaveSounds = Array.Empty<AudioClip>();
        [Range(0f, 1f)]
        [SerializeField] private float leaveVolume = 0.8f;
        [Tooltip("One-shot sounds are heard this far (metres, fading in a straight line).")]
        [SerializeField] private float soundReach = 40f;

        [Header("Flight")]
        [Tooltip("How far it bobs up and down while hovering or circling, in metres.")]
        [Min(0f)]
        [SerializeField] private float bob = 0.25f;
        [Tooltip("How fast it bobs (radians per second).")]
        [Min(0f)]
        [SerializeField] private float bobSpeed = 2f;
        [Tooltip("The most it leans into a turn, in degrees.")]
        [Range(0f, 45f)]
        [SerializeField] private float bank = 12f;
        [Tooltip("With one wing it flies this many degrees tilted toward the missing wing.")]
        [Range(0f, 45f)]
        [SerializeField] private float oneWingRoll = 20f;
        [Tooltip("With one wing it wobbles this much (1 = a bonk's squash) every so often (seconds, random between).")]
        [SerializeField] private float oneWingWobble = 0.3f;
        [SerializeField] private Vector2 oneWingWobbleEvery = new Vector2(2f, 3f);
        [Tooltip("Seconds it takes to turn round to face a new way (about).")]
        [Min(0.01f)]
        [SerializeField] private float turnSeconds = 0.4f;
        [Tooltip("The most it looks down at what it faces (the player's head), in degrees.")]
        [Range(0f, 60f)]
        [SerializeField] private float lookDownMost = 25f;
        [Tooltip("Circling, it looks at a point this high over the circle's centre (the player's head).")]
        [SerializeField] private float headHeight = 1.5f;
        [Tooltip("Metres a second it flies to join its circle when it is not on it yet (or the last Fly To speed, if faster).")]
        [Min(0.1f)]
        [SerializeField] private float joinSpeed = 6f;

        [Header("Circle (when Arrive is called with none set)")]
        [SerializeField] private float defaultRadius = 3f;
        [SerializeField] private float defaultHeight = 5f;
        [Min(0.1f)]
        [SerializeField] private float defaultLapSeconds = 5f;

        [Header("Strain, wings, splits")]
        [Tooltip("The squeeze when it strains to poop (1 = a bonk's squash).")]
        [SerializeField] private float strainWobble = 1.2f;
        [Tooltip("Seconds a falling wing tumbles before it is gone.")]
        [Min(0.1f)]
        [SerializeField] private float wingFallSeconds = 1.2f;
        [Tooltip("Seconds after a wing comes off before Lose Wing's 'done' (so the player sees it go).")]
        [Min(0f)]
        [SerializeField] private float wingOffPause = 0.4f;
        [Tooltip("Size of the puff where a wing comes off.")]
        [SerializeField] private float wingPuffScale = 0.6f;
        [Tooltip("Size of the puff when it bursts, and when it lands.")]
        [SerializeField] private float bigPuffScale = 2f;
        [Tooltip("Seconds a split-off half is flung apart.")]
        [Min(0.01f)]
        [SerializeField] private float flingSeconds = 0.5f;

        [Header("Leaving")]
        [Tooltip("Seconds it takes to fly away (then it is gone).")]
        [Min(0.1f)]
        [SerializeField] private float leaveSeconds = 3f;
        [Tooltip("How far out and how high it flies as it leaves, in metres.")]
        [SerializeField] private float leaveOut = 15f;
        [SerializeField] private float leaveUp = 12f;

        // Its body is 0.26 m across and 0.24 m tall at scale 1 (Flying_Slime.fbx); pins come out a little under it.
        private const float BottomShare = 0.13f;
        private const float HalfHeight = 0.12f;

        private enum Motion { Idle, Arrive, Circle, Travel, Hover, Drop, Leave, Gone }

        private Motion motion = Motion.Idle;
        private Color colour = new Color(0.45f, 0.85f, 0.25f);
        private float size = 1f;
        private int wingsLeft = 2;
        private float rollSide;            // +1: tilts so its left side is lower (left wing gone); -1: the right

        private Vector3 pos;               // where it is, without the bob
        private Vector3 written;           // where it was put last frame (to notice being moved from outside)
        private float yaw, yawSpeed, pitch, pitchSpeed, roll;
        private Vector3 velocity, acceleration, lastPos;
        private float bobTime, bobWeight;

        // Circle
        private bool circleSet;
        private Vector3 circleCentre;
        private float circleRadius, circleHeight, circleLap = 5f, circlePhase;

        // Arrive
        private Vector3 arriveFrom;
        private float arriveStart, arriveSeconds;
        private Action arriveDone;
        private bool pendingFly;
        private Vector3 pendingPoint;
        private float pendingSpeed;
        private Action pendingArrived;

        // Travel / hover
        private Vector3 travelFrom, travelTo, anchor;
        private float travelStart, travelSeconds, lastSpeed;
        private Action travelArrived;
        private bool hasLook;
        private Vector3 lookTarget;

        // Strain
        private bool straining;
        private float strainStart, strainSeconds;
        private bool strainSecondWobble;
        private Action strainDone;

        // Drop / leave
        private Vector3 dropFrom, dropTo, dropGround;
        private float dropStart, dropSeconds;
        private Action<Vector3> dropLanded;
        private Vector3 leaveFrom, leaveTo;
        private float leaveStart;

        // Grow / fling
        private float growFrom = 1f, growStart, growSeconds;
        private Vector3 fling;
        private float flingStart = -10f;

        // Voice
        private float voiceVolume = 0.8f, voiceWings = 1f, voiceLeave = 1f;
        private float nextOneWingWobble;

        private readonly List<GameObject> fallingWings = new();
        private MaterialPropertyBlock block;

        /// <summary>What it is doing now.</summary>
        public Mode State
        {
            get
            {
                switch (motion)
                {
                    case Motion.Gone: return Mode.Gone;
                    case Motion.Leave: return Mode.Leaving;
                    case Motion.Drop: return Mode.Dropping;
                }
                if (straining) return Mode.Straining;
                switch (motion)
                {
                    case Motion.Arrive: return Mode.Arriving;
                    case Motion.Travel: return Mode.Travelling;
                    case Motion.Circle: return Mode.Circling;
                    case Motion.Hover: return Mode.Hovering;
                    default: return Mode.Idle;
                }
            }
        }

        /// <summary>Its paint colour.</summary>
        public Color Colour => colour;

        /// <summary>Its root scale (the body is 0.26 m times this).</summary>
        public float Size => size;

        /// <summary>Wings it still has: 2, 1 or 0.</summary>
        public int WingsLeft => wingsLeft;

        /// <summary>Just under its body: where pins and divers come out.</summary>
        public Vector3 BottomPoint => transform.position + Vector3.down * (BottomShare * Mathf.Abs(transform.lossyScale.y));

        /// <summary>Arriving, travelling, straining, dropping or leaving.</summary>
        public bool Busy
        {
            get
            {
                var s = State;
                return s == Mode.Arriving || s == Mode.Travelling || s == Mode.Straining || s == Mode.Dropping || s == Mode.Leaving;
            }
        }

        private bool Ended => motion == Motion.Drop || motion == Motion.Leave || motion == Motion.Gone;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            if (look == null) look = GetComponent<FlyingSlime>();
            if (voice == null) voice = GetComponent<AudioSource>();
            if (look != null) colour = look.Colour;
            size = Mathf.Max(0.01f, Mathf.Abs(transform.localScale.x));
            wingsLeft = (wingLeft != null && wingLeft.gameObject.activeSelf ? 1 : 0) + (wingRight != null && wingRight.gameObject.activeSelf ? 1 : 0);
            pos = written = lastPos = transform.position;
            anchor = pos;
            yaw = transform.eulerAngles.y;
            bobTime = UnityEngine.Random.Range(0f, 10f);
        }

        private void Start()
        {
            StartVoice();
        }

        private void OnDestroy()
        {
            foreach (var wing in fallingWings)
            {
                if (wing != null) Destroy(wing);
            }
            fallingWings.Clear();
        }

        // ---------------- Commands ----------------

        /// <summary>Its colour (Flying Slime Dress), size (root scale) and wing sound (pitch, volume).</summary>
        public void Setup(Color colour, float size, float pitch, float volume)
        {
            this.colour = colour;
            this.size = Mathf.Max(0.01f, size);
            if (look != null) look.Dress(colour);
            ApplyScale();
            voiceVolume = Mathf.Max(0f, volume);
            if (voice != null) voice.pitch = pitch;
            StartVoice();
            UpdateVoice();
        }

        /// <summary>
        /// Appears at From and comes down over Seconds onto its circle (Circle, or round the player's head if none was
        /// set), spiralling in as the circle turns; its wing sound grows from nothing. Then it circles, and Done is called.
        /// </summary>
        public void Arrive(Vector3 from, float seconds, Action done)
        {
            if (Ended) return;
            if (!circleSet)
            {
                var centre = HeadFloor();
                var d = from - centre;
                float start = d.sqrMagnitude > 0.0001f ? Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg : 0f;
                Circle(centre, defaultRadius, defaultHeight, defaultLapSeconds, 0f);
                // Starting over From's side of the circle.
                circlePhase = start - 360f * Time.time / circleLap;
            }
            pos = from;
            written = transform.position = from;
            lastPos = from;
            velocity = acceleration = Vector3.zero;
            arriveFrom = from;
            arriveStart = Time.time;
            arriveSeconds = Mathf.Max(0.1f, seconds);
            arriveDone = done;
            motion = Motion.Arrive;
            StartVoice();
            UpdateVoice();
        }

        /// <summary>
        /// Keeps circling Centre, Radius out and Height above it, once every Lap Seconds, facing the centre. Start Degrees
        /// is a phase: slimes given different ones (and the same lap) stay that far apart round the circle, whenever each
        /// joined. Away from the circle it first flies to it. Stops a Fly To (its 'arrived' is never called). While it is
        /// arriving this only changes the circle it arrives on.
        /// </summary>
        public void Circle(Vector3 centre, float radius, float height, float lapSeconds, float startDegrees)
        {
            if (Ended) return;
            circleSet = true;
            circleCentre = centre;
            circleRadius = Mathf.Max(0f, radius);
            circleHeight = height;
            circleLap = Mathf.Max(0.1f, lapSeconds);
            circlePhase = startDegrees;
            if (motion == Motion.Arrive) return;
            travelArrived = null;
            motion = Motion.Circle;
        }

        /// <summary>Moves the circle's centre (the stage feeds the player's floor point every frame).</summary>
        public void SetCircleCentre(Vector3 centre)
        {
            circleCentre = centre;
        }

        /// <summary>
        /// Flies in a straight line to Point at about Speed metres a second (eased at both ends), facing the way it goes;
        /// then hovers there, turns to Look At and Arrived is called. A new Fly To replaces the old one (whose 'arrived'
        /// is never called). While arriving, it waits until it has arrived. Nothing once it drops, leaves or is gone.
        /// </summary>
        public void FlyTo(Vector3 point, float speed, Action arrived)
        {
            if (Ended) return;
            if (motion == Motion.Arrive)
            {
                pendingFly = true;
                pendingPoint = point;
                pendingSpeed = speed;
                pendingArrived = arrived;
                return;
            }
            StartTravel(point, speed, arrived);
        }

        /// <summary>What it faces while hovering (the arena's centre, the player).</summary>
        public void LookAt(Vector3 point)
        {
            lookTarget = point;
            hasLook = true;
        }

        /// <summary>
        /// Strains for Seconds (a squeeze and a squelch, where it is), then Done is called (the stage drops the pins or
        /// divers from Bottom Point); now and then it giggles. Once it drops, leaves or is gone, Done is called at once.
        /// </summary>
        public void Strain(float seconds, Action done)
        {
            if (Ended)
            {
                done?.Invoke();
                return;
            }
            if (straining)
            {
                // A new strain: the old one is over.
                var old = strainDone;
                strainDone = null;
                old?.Invoke();
            }
            straining = true;
            strainStart = Time.time;
            strainSeconds = Mathf.Max(0f, seconds);
            strainSecondWobble = strainSeconds >= 0.8f;
            strainDone = done;
            if (look != null) look.Wobble(strainWobble);
            PlayAt(poopSounds, BottomPoint, poopVolume, poopPitch);
        }

        /// <summary>Hurt: it squashes, flashes and rocks away from From, with a low squelch.</summary>
        public void Bonk(Vector3 from)
        {
            if (motion == Motion.Gone) return;
            if (look != null) look.Bonk(from, 1f);
            PlayAt(hitSounds, transform.position, hitVolume, hitPitch);
        }

        /// <summary>
        /// A wing comes off (the left first, then the right) with a puff and a pop, and tumbles down. With one left it
        /// flies tilted toward the missing one; with none its wing sound stops. Done is called a moment later (at once
        /// when it has no wings left).
        /// </summary>
        public void LoseWing(Action done = null)
        {
            Transform wing = null;
            if (wingLeft != null && wingLeft.gameObject.activeSelf) wing = wingLeft;
            else if (wingRight != null && wingRight.gameObject.activeSelf) wing = wingRight;
            if (wing == null || motion == Motion.Gone)
            {
                wingsLeft = 0;
                done?.Invoke();
                return;
            }

            var hinge = wing.position;
            TumbleCopy(wing);
            wing.gameObject.SetActive(false);
            wingsLeft = Mathf.Max(0, wingsLeft - 1);
            if (wingsLeft == 1)
            {
                // Tilted toward the missing wing: positive roll lowers the left side.
                float side = Vector3.Dot(hinge - transform.position, transform.right);
                rollSide = side >= 0f ? -1f : 1f;
                nextOneWingWobble = Time.time + UnityEngine.Random.Range(oneWingWobbleEvery.x, oneWingWobbleEvery.y);
            }
            if (puff != null) EffectPool.Play(puff, hinge, wingPuffScale);
            PlayAt(wingOffSounds, hinge, wingOffVolume, 1f);
            if (look != null) look.Wobble(0.6f);
            if (done == null) return;
            if (wingOffPause <= 0f) done();
            else StartCoroutine(After(wingOffPause, done));
        }

        /// <summary>Bursts into paint (Flying Slime Pop) with a big puff and a bang, and is gone.</summary>
        public void Burst()
        {
            if (motion == Motion.Gone) return;
            Stop();
            motion = Motion.Gone;
            if (puff != null) EffectPool.Play(puff, transform.position, bigPuffScale);
            PlayAt(burstSounds, transform.position, burstVolume, 1f);
            if (look != null && look.isActiveAndEnabled)
            {
                look.Pop();
                // The look destroys it when it has burst (Destroy On Pop); this makes sure.
                Destroy(gameObject, 1f);
            }
            else Destroy(gameObject);
        }

        /// <summary>
        /// Falls onto Ground over Seconds (faster and faster, spinning) with a falling whistle; lands with a splat and a
        /// big puff, Landed is called with Ground, and it is gone.
        /// </summary>
        public void Drop(Vector3 ground, float seconds, Action<Vector3> landed)
        {
            if (motion == Motion.Gone || motion == Motion.Drop) return;
            Stop();
            dropFrom = pos;
            dropGround = ground;
            dropTo = ground + Vector3.up * (HalfHeight * size);
            dropStart = Time.time;
            dropSeconds = Mathf.Max(0.05f, seconds);
            dropLanded = landed;
            motion = Motion.Drop;
            voiceWings = 0f;
            PlayAt(fallSounds, transform.position, fallVolume, 1f);
        }

        /// <summary>A split-off half: grows from From times its size to full over Seconds, flung along Fling (metres a second) for a moment.</summary>
        public void GrowIn(float from, float seconds, Vector3 fling)
        {
            growFrom = Mathf.Clamp(from, 0.001f, 1f);
            growStart = Time.time;
            growSeconds = Mathf.Max(0.01f, seconds);
            this.fling = fling;
            flingStart = Time.time;
            ApplyScale();
        }

        /// <summary>Flies up and away over a few seconds, its wing sound fading, and is gone.</summary>
        public void Leave()
        {
            if (Ended) return;
            Stop();
            leaveFrom = pos;
            var away = pos - (circleSet ? circleCentre : HeadFloor());
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = transform.forward;
            away.y = 0f;
            away = away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.forward;
            leaveTo = pos + away * leaveOut + Vector3.up * leaveUp;
            leaveStart = Time.time;
            motion = Motion.Leave;
            PlayAt(leaveSounds, transform.position, leaveVolume, 1f);
        }

        /// <summary>A giggle (a cute laugh, low).</summary>
        public void Laugh()
        {
            if (motion == Motion.Gone) return;
            PlayAt(laughSounds, transform.position, laughVolume, laughPitch);
        }

        // ---------------- Per frame ----------------

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || motion == Motion.Gone) return;
            float now = Time.time;

            // Moved from outside (e.g. placed after it was made): it carries on from there.
            var moved = transform.position - written;
            if (moved.sqrMagnitude > 1e-6f)
            {
                pos += moved;
                anchor += moved;
                lastPos += moved;
            }

            float bobWant = 1f;
            bool faceTravel = false;
            Vector3 travelDir = Vector3.zero;
            Vector3? face = null;

            switch (motion)
            {
                case Motion.Idle:
                case Motion.Hover:
                    pos = Vector3.MoveTowards(pos, anchor, Mathf.Max(joinSpeed, lastSpeed) * dt);
                    if (hasLook) face = lookTarget;
                    break;

                case Motion.Arrive:
                {
                    float u = Mathf.Clamp01((now - arriveStart) / arriveSeconds);
                    float e = Mathf.SmoothStep(0f, 1f, u);
                    pos = Vector3.Lerp(arriveFrom, CirclePoint(now), e);
                    bobWant = e;
                    face = circleCentre + Vector3.up * headHeight;
                    if (u >= 1f) Arrived();
                    break;
                }

                case Motion.Circle:
                {
                    var goal = CirclePoint(now);
                    float round = 2f * Mathf.PI * circleRadius / circleLap;
                    float speed = Mathf.Max(joinSpeed, lastSpeed, round * 1.3f + 1f);
                    pos = Vector3.MoveTowards(pos, goal, speed * dt);
                    face = circleCentre + Vector3.up * headHeight;
                    break;
                }

                case Motion.Travel:
                {
                    float u = Mathf.Clamp01((now - travelStart) / travelSeconds);
                    pos = Vector3.Lerp(travelFrom, travelTo, Mathf.SmoothStep(0f, 1f, u));
                    bobWant = 0.3f;
                    travelDir = travelTo - travelFrom;
                    faceTravel = u < 0.85f;
                    if (!faceTravel && hasLook) face = lookTarget;
                    if (u >= 1f) Travelled();
                    break;
                }

                case Motion.Drop:
                {
                    float u = Mathf.Clamp01((now - dropStart) / dropSeconds);
                    var flat = Vector3.Lerp(dropFrom, dropTo, u);
                    flat.y = Mathf.Lerp(dropFrom.y, dropTo.y, u * u);
                    pos = flat;
                    bobWant = 0f;
                    yaw += 220f * dt;
                    if (u >= 1f)
                    {
                        Landed();
                        return;
                    }
                    break;
                }

                case Motion.Leave:
                {
                    float u = Mathf.Clamp01((now - leaveStart) / leaveSeconds);
                    pos = Vector3.Lerp(leaveFrom, leaveTo, u * u);
                    voiceLeave = 1f - u;
                    bobWant = 0f;
                    travelDir = leaveTo - leaveFrom;
                    faceTravel = true;
                    if (u >= 1f)
                    {
                        motion = Motion.Gone;
                        Destroy(gameObject);
                        return;
                    }
                    break;
                }
            }

            // A split-off half is flung apart for a moment.
            float flung = (now - flingStart) / flingSeconds;
            if (flung < 1f)
            {
                var push = fling * ((1f - flung * flung) * dt);
                pos += push;
                // Its hover place goes with it, or Idle/Hover would pull it straight back.
                if (motion == Motion.Idle || motion == Motion.Hover) anchor += push;
            }

            if (straining) Straining(now);
            if (motion == Motion.Gone) return;

            if (wingsLeft == 1 && now >= nextOneWingWobble && motion != Motion.Drop)
            {
                nextOneWingWobble = now + UnityEngine.Random.Range(oneWingWobbleEvery.x, oneWingWobbleEvery.y);
                if (look != null) look.Wobble(oneWingWobble);
            }

            ApplyScale();
            Place(dt, bobWant, faceTravel, travelDir, face);
            UpdateVoice();
        }

        // Puts it where it is plus the bob, turned the way it faces, banked into turns.
        private void Place(float dt, float bobWant, bool faceTravel, Vector3 travelDir, Vector3? face)
        {
            bobTime += dt;
            bobWeight = Mathf.Lerp(bobWeight, bobWant, 1f - Mathf.Exp(-dt * 3f));
            var at = pos + Vector3.up * (bob * bobWeight * Mathf.Sin(bobSpeed * bobTime));

            // Its speed and how it speeds up (for the bank).
            var v = (at - lastPos) / dt;
            lastPos = at;
            if (v.sqrMagnitude > 40f * 40f) v = velocity;
            var before = velocity;
            velocity = Vector3.Lerp(velocity, v, 1f - Mathf.Exp(-dt / 0.1f));
            acceleration = Vector3.Lerp(acceleration, (velocity - before) / dt, 1f - Mathf.Exp(-dt / 0.15f));

            float wantYaw = yaw, wantPitch = 0f;
            if (faceTravel)
            {
                travelDir.y = 0f;
                if (travelDir.sqrMagnitude > 0.0001f) wantYaw = Mathf.Atan2(travelDir.x, travelDir.z) * Mathf.Rad2Deg;
            }
            else if (face.HasValue)
            {
                var to = face.Value - at;
                var flat = new Vector3(to.x, 0f, to.z);
                if (flat.sqrMagnitude > 0.01f)
                {
                    wantYaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
                    wantPitch = Mathf.Clamp(Mathf.Atan2(-to.y, flat.magnitude) * Mathf.Rad2Deg, 0f, lookDownMost);
                }
            }
            if (motion == Motion.Drop) wantPitch = 25f;
            else yaw = Mathf.SmoothDampAngle(yaw, wantYaw, ref yawSpeed, turnSeconds);
            pitch = Mathf.SmoothDampAngle(pitch, wantPitch, ref pitchSpeed, turnSeconds);

            // Banked into turns (positive roll lowers the left side), tilted toward a missing wing.
            var right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            float side = Vector3.Dot(acceleration, right);
            float wantRoll = -Mathf.Clamp(Mathf.Atan2(side, 9.81f) * Mathf.Rad2Deg, -bank, bank);
            if (wingsLeft == 1) wantRoll += rollSide * oneWingRoll;
            roll = Mathf.Lerp(roll, wantRoll, 1f - Mathf.Exp(-dt * 4f));

            transform.SetPositionAndRotation(at, Quaternion.Euler(pitch, yaw, roll));
            written = at;
        }

        private void Arrived()
        {
            motion = Motion.Circle;
            var done = arriveDone;
            arriveDone = null;
            done?.Invoke();
            if (!pendingFly) return;
            pendingFly = false;
            var arrived = pendingArrived;
            pendingArrived = null;
            // Unless Done gave it something else to do.
            if (motion == Motion.Circle) StartTravel(pendingPoint, pendingSpeed, arrived);
        }

        private void StartTravel(Vector3 point, float speed, Action arrived)
        {
            lastSpeed = Mathf.Max(0.1f, speed);
            travelFrom = pos;
            travelTo = point;
            // Never instant (Arrived always comes from a later frame).
            travelSeconds = Mathf.Max(0.3f, Vector3.Distance(pos, point) / lastSpeed);
            travelStart = Time.time;
            travelArrived = arrived;
            motion = Motion.Travel;
        }

        private void Travelled()
        {
            motion = Motion.Hover;
            anchor = travelTo;
            var arrived = travelArrived;
            travelArrived = null;
            arrived?.Invoke();
        }

        private void Straining(float now)
        {
            float t = now - strainStart;
            if (strainSecondWobble && t >= strainSeconds * 0.5f)
            {
                strainSecondWobble = false;
                if (look != null) look.Wobble(strainWobble * 0.5f);
            }
            if (t < strainSeconds) return;
            straining = false;
            if (wingsLeft >= 2 && UnityEngine.Random.value < laughChance) Laugh();
            var done = strainDone;
            strainDone = null;
            done?.Invoke();
        }

        private void Landed()
        {
            motion = Motion.Gone;
            transform.position = written = dropTo;
            PlayAt(landSounds, dropGround, landVolume, 1f);
            if (puff != null) EffectPool.Play(puff, dropGround, bigPuffScale);
            // Hidden at once (its drops left to fade), gone when its falling wings are done.
            if (look != null)
            {
                look.ReleaseDrops();
                look.enabled = false;
            }
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            if (voice != null) voice.Stop();
            var landed = dropLanded;
            dropLanded = null;
            landed?.Invoke(dropGround);
            Destroy(gameObject, wingFallSeconds + 0.1f);
        }

        // Whatever it was doing stops (strain's and travel's callbacks are dropped).
        private void Stop()
        {
            straining = false;
            strainDone = null;
            travelArrived = null;
            arriveDone = null;
            pendingFly = false;
            pendingArrived = null;
        }

        // ---------------- Helpers ----------------

        private Vector3 CirclePoint(float now)
        {
            float degrees = circlePhase + 360f * now / circleLap;
            return circleCentre + Quaternion.Euler(0f, degrees, 0f) * Vector3.forward * circleRadius + Vector3.up * circleHeight;
        }

        private static Vector3 HeadFloor()
        {
            var head = SlimeKit.Head;
            if (head == null) return Vector3.zero;
            var p = head.position;
            return new Vector3(p.x, SlimeKit.FloorUnder(p), p.z);
        }

        private void ApplyScale()
        {
            float k = 1f;
            if (growSeconds > 0f)
            {
                float u = Mathf.Clamp01((Time.time - growStart) / growSeconds);
                k = Mathf.Lerp(growFrom, 1f, Mathf.SmoothStep(0f, 1f, u));
                if (u >= 1f) growSeconds = 0f;
            }
            transform.localScale = Vector3.one * (size * k);
        }

        private void StartVoice()
        {
            if (voice == null || voice.clip == null || voice.isPlaying || !isActiveAndEnabled || !Application.isPlaying) return;
            voice.loop = true;
            // Not always from the start of the clip.
            voice.time = UnityEngine.Random.Range(0f, voice.clip.length * 0.9f);
            voice.Play();
        }

        private void UpdateVoice()
        {
            if (voice == null) return;
            float ramp = motion == Motion.Arrive ? Mathf.Clamp01((Time.time - arriveStart) / arriveSeconds) : 1f;
            float wings = motion == Motion.Drop ? 0f : wingsLeft >= 2 ? 1f : wingsLeft == 1 ? 0.75f : 0f;
            voiceWings = Mathf.MoveTowards(voiceWings, wings, Time.deltaTime / 0.4f);
            voice.volume = voiceVolume * ramp * voiceWings * voiceLeave;
        }

        // A falling copy of a wing: tumbles, falls and shrinks (never to 0), then is gone.
        private void TumbleCopy(Transform wing)
        {
            var copy = Instantiate(wing.gameObject, wing.position, wing.rotation);
            copy.name = wing.name + " (falling)";
            copy.transform.localScale = wing.lossyScale;
            copy.SetActive(true);
            var from = wing.GetComponent<Renderer>();
            var to = copy.GetComponent<Renderer>();
            if (from != null && to != null)
            {
                from.GetPropertyBlock(block);
                to.SetPropertyBlock(block);
            }
            fallingWings.Add(copy);
            var outward = wing.position - transform.position;
            outward.y = 0f;
            outward = outward.sqrMagnitude > 0.0001f ? outward.normalized : UnityEngine.Random.onUnitSphere;
            StartCoroutine(Tumble(copy, velocity * 0.5f + outward * 1.5f + Vector3.up * 1f));
        }

        private IEnumerator Tumble(GameObject copy, Vector3 speed)
        {
            var t = copy.transform;
            var startScale = t.localScale;
            var axis = UnityEngine.Random.onUnitSphere;
            float spin = UnityEngine.Random.Range(300f, 500f);
            float start = Time.time;
            while (copy != null)
            {
                float u = (Time.time - start) / wingFallSeconds;
                if (u >= 1f) break;
                float dt = Time.deltaTime;
                speed += Physics.gravity * (0.5f * dt);
                t.position += speed * dt;
                t.rotation = Quaternion.AngleAxis(spin * dt, axis) * t.rotation;
                t.localScale = startScale * Mathf.Max(0.001f, 1f - u * u);
                yield return null;
            }
            fallingWings.Remove(copy);
            if (copy != null) Destroy(copy);
        }

        private static IEnumerator After(float seconds, Action then)
        {
            yield return new WaitForSeconds(seconds);
            then?.Invoke();
        }

        // One of the clips at a point (3D, heard far: it is a giant), at this pitch.
        private void PlayAt(AudioClip[] clips, Vector3 at, float volume, float pitch)
        {
            if (clips == null || clips.Length == 0) return;
            var clip = clips[UnityEngine.Random.Range(0, clips.Length)];
            if (clip == null) return;
            var go = new GameObject("Giant Slime Sound");
            go.transform.position = at;
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.pitch = Mathf.Max(0.1f, pitch);
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 3f;
            source.maxDistance = Mathf.Max(4f, soundReach);
            source.dopplerLevel = 0f;
            source.Play();
            Destroy(go, clip.length / source.pitch + 0.1f);
        }
    }
}

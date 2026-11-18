using System;
using System.Collections.Generic;
using Game.Combat;
using Unity.XR.CoreUtils;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Game.Gates
{
    /// <summary>
    /// The gates' last fight (user, 2026-11-18, gate 3: "drop a giant boss ball, a pumpkin head ball as a boss, this
    /// pumpkin should be bigger than the hole, so it can't be pushed down hole and too heavy for being pushed out the
    /// border, only attack, punch, special move decrease the giant boss health, break the pumpkin boss here to drop the
    /// seed"; gates 1 and 2 end with it too). A giant rolling pumpkin (the Bowling Pumpkin's model, grown to Radius):
    /// - it drops from the sky onto its spot and lands with a thump that shoves what's round it, the player too;
    /// - it rolls after the player; every Charge Every seconds it stops, laughs and shakes (the Bowling Pumpkin's roll
    ///   warning), then charges in a straight line at where the player was, rolling over and over, until something stops
    ///   it: a wall, a rock or the arena's edge leaves it dizzy (stars) for Dizzy Seconds, and hits then hurt Dizzy Damage
    ///   Times as much: the time to punch it;
    /// - rolling into the player hurts and throws them (a charge: Charge Hit Event and a long throw; a roll: Bump Event
    ///   and a short one): near a hole or an edge that can be the end;
    /// - nothing pushes it (its Mass is Push Weight's Immovable) and it never falls: it keeps its height over holes and
    ///   gaps, bigger than any of them, and stays inside the arena;
    /// - only the player hurts it (Enemy Hit Damage's rule: a fist, a thing they hold or threw, a shot; their blasts
    ///   through IHittable; the gate's special move through Special Hit), with damage numbers;
    /// - at 0 health it bursts (pieces, candies, EXP) and Broken tells the gate, which drops The_Seed (Gate Exit).
    /// The gate sets Floor Y, the arena and, for its own things (Ball Valley's balls), Touched and Landed.
    /// Tools > 429 Game > Gates > Set Up Pumpkin Ball Boss makes Prefabs/Gates/Pumpkin_Ball_Boss.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class PumpkinBallBoss : MonoBehaviour, IHittable
    {
        public enum State { Dropping, Hop, Landed, Chase, WindUp, Charge, Dizzy, Broken }

        [Header("Body")]
        [Tooltip("Its radius in metres: bigger than any hole (Ball Valley's pits are 2.4-2.6 m across from the middle).")]
        [Min(0.5f)] public float radius = 2.6f;
        [Tooltip("What it looks like: a pumpkin model (the Bowling Pumpkin's), grown so its ball is Radius.")]
        public GameObject model;
        [Tooltip("The model's own ball radius (the Bowling Pumpkin's: 0.23 m).")]
        [Min(0.01f)] public float modelRadius = 0.23f;
        [Tooltip("The model's place and turn inside the ball, as on the Bowling Pumpkin (grown with it).")]
        public Vector3 modelOffset = new Vector3(0f, -0.027f, 0f);
        public Vector3 modelEuler = new Vector3(0f, 55f, 0f);

        [Header("Health")]
        [Min(1f)] public float health = 150f;
        [Tooltip("Divides a hit's power, as an enemy's Toughness does (Hit Settings decides what a hit does).")]
        [Min(0.1f)] public float toughness = 1f;
        public HitSettings hitSettings;
        [Tooltip("A fist (or thing) hurts it again only after this many seconds; the other fist's punch still lands.")]
        [Min(0f)] public float hitCooldown = 0.25f;
        [Tooltip("Dizzy, hits hurt this many times as much.")]
        [Min(1f)] public float dizzyDamageTimes = 2f;
        [Min(0)] public int exp = 30;

        [Header("Rolling")]
        [Min(0f)] public float chaseSpeed = 3.2f;
        [Min(0.1f)] public float chaseAccel = 4f;
        [Tooltip("Seconds between charges, picked in this range each time (counted while it chases).")]
        public Vector2 chargeEvery = new Vector2(3f, 5f);
        [Tooltip("Seconds it shakes and laughs before a charge (the warning: step aside).")]
        [Min(0.2f)] public float windUpSeconds = 1.6f;
        [Min(1f)] public float chargeSpeed = 13f;
        [Min(1f)] public float chargeAccel = 30f;
        [Tooltip("A charge that hits nothing stops after this many seconds (no dizziness).")]
        [Min(0.5f)] public float chargeMaxSeconds = 3f;
        [Tooltip("A charge stopped (by a wall, a rock, the arena's edge) at least this fast leaves it dizzy.")]
        [Min(0f)] public float slamSpeed = 6f;
        [Min(0.5f)] public float dizzySeconds = 3.5f;

        [Header("Hitting the player")]
        [Tooltip("PlayerHealth FSM events: a charge into them, and a rolling bump.")]
        public string chargeHitEvent = "DamageHeavy";
        public string bumpEvent = "Damage";
        [Tooltip("How far a charge throws them: x up, y along (metres; their Toughness cuts it, not below a third).")]
        public Vector2 chargeThrow = new Vector2(1.2f, 6f);
        public Vector2 bumpThrow = new Vector2(0.4f, 2.5f);
        [Tooltip("The player's body round the point under their head (metres).")]
        [Min(0.05f)] public float playerReach = 0.4f;
        [Min(0.2f)] public float hitPlayerEvery = 1.2f;

        [Header("Drop in")]
        [Min(1f)] public float dropHeight = 18f;
        [Min(0.2f)] public float dropSeconds = 1.4f;
        [Tooltip("Its landing shoves what's within this many metres of its edge (the player: Land Throw).")]
        [Min(0f)] public float landShoveReach = 4f;
        public Vector2 landThrow = new Vector2(0.6f, 3f);
        [Tooltip("Lanes (gate 2): a leap to the next lane takes this long (seconds) and goes this high (metres).")]
        [Min(0.2f)] public float hopSeconds = 1.2f;
        [Min(0f)] public float hopHeight = 6f;

        [Header("Looks and sounds")]
        [Tooltip("Laughs: as it lands, after hitting the player, and now and then.")]
        public AudioClip[] laughSounds = Array.Empty<AudioClip>();
        [Tooltip("The charge's warning (the Bowling Pumpkin's roll warning).")]
        public AudioClip windUpSound;
        [Tooltip("Looped while it rolls (louder the faster).")]
        public AudioClip rollSound;
        public AudioClip landSound;
        public AudioClip breakSound;
        public AudioClip[] hurtSounds = Array.Empty<AudioClip>();
        [Tooltip("Shown where it bursts (the Bowling Pumpkin's break-out pieces), Break Effect Size times bigger.")]
        public GameObject[] breakEffects = Array.Empty<GameObject>();
        [Min(0.1f)] public float breakEffectSize = 8f;
        [Tooltip("The shared 3D smoke puff (Smoke_Flash_Small_Puff): landing, slams, the charge's trail.")]
        public GameObject puff;
        [Min(0.1f)] public float puffSize = 3f;
        [Min(0.05f)] public float trailEvery = 0.3f;
        [Tooltip("The dizzy stars (Stun_Stars).")]
        public GameObject stunStars;
        [Tooltip("Candies that burst out of it (the candies in the game).")]
        public GameObject[] candies = Array.Empty<GameObject>();
        [Min(0)] public int breakCandies = 14;

        /// <summary>It burst (health 0): the gate drops The_Seed (Gate Exit).</summary>
        public event Action<PumpkinBallBoss> Broken;
        /// <summary>It took damage (how much): for the gate's board.</summary>
        public event Action<PumpkinBallBoss, float> Hurt;

        /// <summary>The floor's height under it (world Y); the gate sets it before it lands (else: where it was spawned).</summary>
        [NonSerialized] public float floorY = float.NaN;
        /// <summary>
        /// Where its middle may be (the gate's arena, already kept a Radius in from the edges where it must stay on): past
        /// it is a wall, and a charge stopped there leaves it dizzy. Null: anywhere.
        /// </summary>
        [NonSerialized] public Func<Vector3, bool> canBeAt;
        /// <summary>The arena's middle: where it heads if it's ever outside (a shrinking arena).</summary>
        [NonSerialized] public Vector3 arenaCentre;
        /// <summary>
        /// Lanes (gate 2): instead of chasing, it leaps to this floor point after every dizzy spell or missed charge, lands
        /// with its thump, and charges again from there. Null: it rolls after the player.
        /// </summary>
        [NonSerialized] public Func<Vector3> hopTo;
        /// <summary>Lanes (gate 2): the way a charge from here goes (down its lane). Null: straight at the player.</summary>
        [NonSerialized] public Func<Vector3, Vector3> chargeAlong;
        /// <summary>It ran into a body of the gate's (Ball Valley: a ball): the collider, its flat velocity before.</summary>
        [NonSerialized] public Action<Collider, Vector3> touched;
        /// <summary>It landed (the middle, the reach of its thump in metres): the gate shoves its own things.</summary>
        [NonSerialized] public Action<Vector3, float> landed;

        public State Now => state;
        public float Health => hp;
        public float MaxHealth => health;
        public bool IsDizzy => state == State.Dizzy;
        public bool IsBroken => state == State.Broken;

        private Rigidbody body;
        private SphereCollider ball;
        private Transform look;
        private AudioSource voice;
        private AudioSource roller;
        private XROrigin origin;
        private Transform head;
        private PlayMakerFSM healthFsm;
        private HutongGames.PlayMaker.FsmFloat playerHealth;
        private readonly Dictionary<UnityEngine.Object, float> nextHitBy = new();
        private readonly List<Renderer> looks = new();
        private readonly List<Color> lookColours = new();
        private MaterialPropertyBlock block;

        private State state = State.Dropping;
        private float hp;
        private float stateAt;
        private float nextCharge;
        private Vector3 chargeDir = Vector3.forward;
        private Vector3 faceDir = Vector3.forward;
        private float spin;
        private float speedBefore;
        private float nextPlayerHit;
        private float nextTrail;
        private float nextLaugh;
        private float flash;
        private float squash;
        private float dizzyFor;
        // In the air (falling in, or a leap to another lane): from, to (its middle), how long, how high the arc.
        private Vector3 airFrom;
        private Vector3 airTo;
        private float airSeconds;
        private float airArc;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        /// <summary>
        /// A boss from <paramref name="prefab"/>, dropping from the sky onto <paramref name="floorPoint"/> (a point on the
        /// floor; its middle lands Radius over it), under <paramref name="parent"/>.
        /// </summary>
        public static PumpkinBallBoss Spawn(GameObject prefab, Vector3 floorPoint, Transform parent)
        {
            if (prefab == null) return null;
            var made = Instantiate(prefab, floorPoint, Quaternion.identity, parent);
            made.name = prefab.name;
            var boss = made.GetComponent<PumpkinBallBoss>();
            if (boss == null)
            {
                Debug.LogError("[Gates] " + prefab.name + " has no Pumpkin Ball Boss.", prefab);
                Destroy(made);
                return null;
            }
            boss.floorY = floorPoint.y;
            return boss;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            // Nothing pushes it: punches, blasts and balls (Push Weight's Immovable), and it holds its height.
            body.mass = PushWeight.Immovable;
            body.useGravity = false;
            body.isKinematic = true;   // (falling in: moved by hand until it lands)
            body.interpolation = RigidbodyInterpolation.Interpolate;
            // (Speculative: it goes kinematic in the air and back; Continuous Dynamic isn't for kinematic bodies.)
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            ball = GetComponent<SphereCollider>();
            if (ball == null) ball = gameObject.AddComponent<SphereCollider>();
            ball.radius = radius / Mathf.Max(0.0001f, transform.lossyScale.x);
            ball.enabled = false;
            hp = health;   // (again in Start: a gate may set Health right after spawning it)
            block = new MaterialPropertyBlock();
            BuildLook();
            voice = MakeSpeaker("Voice", false);
            roller = MakeSpeaker("Roll", true);
        }

        private void Start()
        {
            hp = health;
            origin = FindAnyObjectByType<XROrigin>();
            var cam = origin != null && origin.Camera != null ? origin.Camera : Camera.main;
            head = cam != null ? cam.transform : null;
            if (origin != null)
            {
                foreach (var fsm in origin.GetComponents<PlayMakerFSM>())
                {
                    if (fsm.FsmName != "PlayerHealth") continue;
                    healthFsm = fsm;
                    playerHealth = fsm.FsmVariables.FindFsmFloat("PlayerHealth");
                }
            }
            if (float.IsNaN(floorY)) floorY = transform.position.y;
            airTo = new Vector3(transform.position.x, floorY + radius + 0.02f, transform.position.z);
            airFrom = airTo + Vector3.up * dropHeight;
            airSeconds = dropSeconds;
            airArc = 0f;
            transform.position = airFrom;
            body.position = airFrom;
            Go(State.Dropping);
            PlayVoice(laughSounds, 1f);
        }

        /// <summary>
        /// Its size here (gate 2's lanes are 2 m wide): set by the gate right after spawning it, before it lands. The ball
        /// and its look follow.
        /// </summary>
        public void Resize(float newRadius)
        {
            newRadius = Mathf.Max(0.3f, newRadius);
            if (Mathf.Approximately(newRadius, radius)) return;
            lookScale *= newRadius / radius;
            radius = newRadius;
            if (ball != null) ball.radius = radius / Mathf.Max(0.0001f, transform.lossyScale.x);
        }

        // The look's size against the one it was built at (Resize).
        private float lookScale = 1f;

        // Lanes: a leap from here to the gate's next spot (Hop To), landing with the thump.
        private void StartHop()
        {
            var to = hopTo != null ? hopTo() : transform.position;
            airFrom = transform.position;
            airTo = new Vector3(to.x, floorY + radius + 0.02f, to.z);
            airSeconds = hopSeconds;
            airArc = hopHeight;
            body.linearVelocity = Vector3.zero;
            body.isKinematic = true;
            ball.enabled = false;
            Go(State.Hop);
            PlayVoice(laughSounds, 1f);
        }

        // A charge spent or a dizzy spell over: chase again, or (lanes) leap to the next lane.
        private void Onward()
        {
            if (hopTo != null) StartHop();
            else Go(State.Chase);
        }

        // ---------------------------------------------------------------- looks

        private void BuildLook()
        {
            look = new GameObject("Look").transform;
            look.SetParent(transform, false);
            if (model != null)
            {
                float grow = radius / Mathf.Max(0.0001f, modelRadius);
                var made = Instantiate(model, look, false);
                made.name = model.name;
                made.transform.localPosition = modelOffset * grow;
                made.transform.localRotation = Quaternion.Euler(modelEuler);
                made.transform.localScale = made.transform.localScale * grow;
                // Only looks: the ball is the one collider.
                foreach (var c in made.GetComponentsInChildren<Collider>(true)) Destroy(c);
                foreach (var b in made.GetComponentsInChildren<Rigidbody>(true)) Destroy(b);
                foreach (var a in made.GetComponentsInChildren<Animator>(true)) a.enabled = false;
            }
            else
            {
                // No model set: a plain orange ball, so it still works.
                var stand = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(stand.GetComponent<Collider>());
                stand.transform.SetParent(look, false);
                stand.transform.localScale = Vector3.one * (radius * 2f);
                var r = stand.GetComponent<Renderer>();
                if (r != null) r.material.color = new Color(1f, 0.45f, 0.05f);
            }
            foreach (var r in look.GetComponentsInChildren<Renderer>(true))
            {
                looks.Add(r);
                var m = r.sharedMaterial;
                lookColours.Add(m == null ? Color.white : m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) : m.HasProperty(ColorId) ? m.GetColor(ColorId) : Color.white);
            }
        }

        private AudioSource MakeSpeaker(string name, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 1f;
            s.minDistance = 6f;
            s.maxDistance = 60f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.loop = loop;
            return s;
        }

        private void PlayVoice(AudioClip[] clips, float volume)
        {
            if (voice == null || clips == null || clips.Length == 0) return;
            var clip = clips[Random.Range(0, clips.Length)];
            if (clip != null) voice.PlayOneShot(clip, volume);
        }

        private void PlayVoice(AudioClip clip, float volume)
        {
            if (voice != null && clip != null) voice.PlayOneShot(clip, volume);
        }

        private void Puff(Vector3 at, float size)
        {
            if (puff == null) return;
            var p = Instantiate(puff, at, Quaternion.identity);
            p.transform.localScale *= size;
            Destroy(p, 6f);
        }

        // ---------------------------------------------------------------- states

        private void Go(State next)
        {
            state = next;
            stateAt = Time.time;
            switch (next)
            {
                case State.Chase:
                    nextCharge = Time.time + Random.Range(Mathf.Min(chargeEvery.x, chargeEvery.y), Mathf.Max(chargeEvery.x, chargeEvery.y));
                    break;
                case State.WindUp:
                    PlayVoice(laughSounds, 1f);
                    PlayVoice(windUpSound, 1f);
                    break;
                case State.Charge:
                    nextTrail = Time.time;
                    break;
                case State.Dizzy:
                    if (stunStars != null && dizzyFor > 0f)
                        Game.VFX.StunStars.Show(stunStars, transform, transform, radius * 1.05f, radius * 0.9f, dizzyFor);
                    break;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (state == State.Dropping || state == State.Hop)
            {
                // Falling in (speeding up like a real fall) or leaping (an arc), then the thump.
                float t = Mathf.Clamp01((Time.time - stateAt) / Mathf.Max(0.01f, airSeconds));
                var at = state == State.Dropping
                    ? Vector3.Lerp(airFrom, airTo, t * t)
                    : Vector3.Lerp(airFrom, airTo, t) + Vector3.up * (4f * airArc * t * (1f - t));
                transform.position = at;
                body.position = at;
                if (t >= 1f) Land();
            }
            UpdateLook(dt);
            UpdateRollSound();
        }

        private void Land()
        {
            var at = airTo;
            transform.position = at;
            body.position = at;
            body.isKinematic = false;
            body.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
            body.linearVelocity = Vector3.zero;
            ball.enabled = true;
            Physics.SyncTransforms();
            Go(State.Landed);
            squash = 0.35f;
            PlayVoice(landSound, 1f);
            var floor = new Vector3(at.x, floorY, at.z);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                Puff(floor + new Vector3(Mathf.Cos(a), 0.2f, Mathf.Sin(a)) * radius, puffSize);
            }
            landed?.Invoke(at, radius + landShoveReach);
            // The player near it: thrown off, away from it.
            if (!PlayerDown() && head != null)
            {
                var feet = PlayerFeet();
                float d = Flat(feet - at).magnitude;
                if (d < radius + landShoveReach)
                {
                    float share = 1f - Mathf.Clamp01((d - radius) / Mathf.Max(0.1f, landShoveReach));
                    PlayerLaunch.Launch(at, landThrow.x, Mathf.Max(1f, landThrow.y * share), true, true, 0.35f, gameObject);
                    if (d < radius + playerReach) SendPlayer(bumpEvent);
                }
            }
        }

        private void FixedUpdate()
        {
            if (state == State.Dropping || state == State.Hop || state == State.Broken) return;
            float dt = Time.fixedDeltaTime;
            var pos = body.position;
            var vel = body.linearVelocity;
            vel.y = 0f;
            speedBefore = vel.magnitude;
            bool down = PlayerDown();
            var toPlayer = Flat(PlayerFeet() - pos);
            float dPlayer = toPlayer.magnitude;
            var dirPlayer = dPlayer > 0.01f ? toPlayer / dPlayer : faceDir;

            var want = Vector3.zero;
            float accel = chaseAccel;
            switch (state)
            {
                case State.Landed:
                    accel = 10f;
                    if (Time.time - stateAt > 1.2f) Go(hopTo != null ? State.WindUp : State.Chase);   // (lanes: straight to the next charge)
                    break;
                case State.Chase:
                    if (!down) want = dirPlayer * (chaseSpeed * (dPlayer < radius + 1.5f ? 0.6f : 1f));
                    if (!down && Time.time >= nextCharge && dPlayer > radius + 1.5f) Go(State.WindUp);
                    break;
                case State.WindUp:
                    accel = 12f;
                    if (down) { Go(State.Chase); break; }
                    if (Time.time - stateAt >= windUpSeconds)
                    {
                        var along = chargeAlong != null ? Flat(chargeAlong(pos)) : Vector3.zero;
                        chargeDir = along.sqrMagnitude > 0.0001f ? along.normalized : dirPlayer;
                        // Already against the arena's edge that way (the player beyond it): no charge, it would only
                        // push into it (play test 2026-11-18: 3 s stuck in a corner).
                        if (canBeAt != null && !canBeAt(pos + chargeDir * 0.6f))
                        {
                            Onward();
                            return;
                        }
                        Go(State.Charge);
                    }
                    break;
                case State.Charge:
                    want = chargeDir * chargeSpeed;
                    accel = chargeAccel;
                    if (Time.time - stateAt > chargeMaxSeconds)
                    {
                        PlayVoice(laughSounds, 0.8f);
                        Onward();
                        return;
                    }
                    break;
                case State.Dizzy:
                    accel = 14f;
                    if (Time.time - stateAt > dizzyFor)
                    {
                        Onward();
                        return;
                    }
                    break;
            }

            vel = Vector3.MoveTowards(vel, want, accel * dt);
            // The arena's edge is a wall: it doesn't roll past it (it slides along it where it can); a charge stopped
            // there leaves it dizzy.
            if (canBeAt != null)
            {
                if (!canBeAt(pos + vel * dt))
                {
                    var slideX = new Vector3(vel.x, 0f, 0f);
                    var slideZ = new Vector3(0f, 0f, vel.z);
                    bool okX = slideX.sqrMagnitude > 0.0001f && canBeAt(pos + slideX * dt);
                    bool okZ = slideZ.sqrMagnitude > 0.0001f && canBeAt(pos + slideZ * dt);
                    float into = vel.magnitude;
                    if (okX && !okZ) { into = Mathf.Abs(vel.z); vel = slideX; }
                    else if (okZ && !okX) { into = Mathf.Abs(vel.x); vel = slideZ; }
                    else vel = Vector3.zero;
                    if (state == State.Charge && into > slamSpeed * 0.6f) Slam(pos + chargeDir * radius);
                }
                // Outside (an arena that shrank past it): back towards its middle.
                if (!canBeAt(pos))
                {
                    var back = Flat(arenaCentre - pos);
                    if (back.sqrMagnitude > 0.01f) vel = back.normalized * Mathf.Max(3f, vel.magnitude);
                }
            }
            if (state == State.Dizzy) vel = Vector3.MoveTowards(vel, Vector3.zero, 14f * dt);
            body.linearVelocity = new Vector3(vel.x, 0f, vel.z);

            // Rolling into the player: a charge throws them far, a roll a little.
            if (!down && head != null && Time.time >= nextPlayerHit && dPlayer < radius + playerReach)
            {
                float closing = Vector3.Dot(vel, dirPlayer);
                if (state == State.Charge && speedBefore > 4f) HitPlayer(true, dirPlayer);
                else if (closing > 0.8f) HitPlayer(false, dirPlayer);
            }

            if (state == State.Charge && Time.time >= nextTrail)
            {
                nextTrail = Time.time + trailEvery;
                Puff(new Vector3(pos.x, floorY + 0.3f, pos.z) - chargeDir * radius * 0.8f, puffSize * 0.6f);
            }
            if (Time.time >= nextLaugh && state == State.Chase)
            {
                nextLaugh = Time.time + Random.Range(6f, 10f);
                PlayVoice(laughSounds, 0.7f);
            }
        }

        private void HitPlayer(bool charge, Vector3 dirPlayer)
        {
            nextPlayerHit = Time.time + hitPlayerEvery;
            var throwBy = charge ? chargeThrow : bumpThrow;
            // A charge throws them along its way; a roll, away from its middle.
            var from = charge ? PlayerFeet() - chargeDir : body.position;
            SendPlayer(charge ? chargeHitEvent : bumpEvent);
            PlayerLaunch.Launch(from, throwBy.x, throwBy.y, true, true, 0.35f, gameObject);
            PlayVoice(laughSounds, 1f);
            squash = Mathf.Max(squash, 0.15f);
        }

        private void SendPlayer(string ev)
        {
            if (healthFsm != null && !string.IsNullOrEmpty(ev)) healthFsm.SendEvent(ev);
        }

        private void Slam(Vector3 at)
        {
            if (state != State.Charge) return;
            dizzyFor = dizzySeconds;
            body.linearVelocity = Vector3.zero;
            squash = 0.3f;
            Puff(new Vector3(at.x, floorY + 0.5f, at.z), puffSize);
            PlayVoice(hurtSounds, 1f);
            Go(State.Dizzy);
        }

        // ---------------------------------------------------------------- being hit

        private void OnCollisionEnter(Collision collision)
        {
            if (state == State.Broken || state == State.Dropping || state == State.Hop) return;
            var hit = EnemyHitDamage.Judge(collision, toughness, hitSettings, out bool byPlayer);
            if (byPlayer)
            {
                PlayerHit(collision, hit);
                return;
            }
            var other = collision.collider;
            if (other == null || other.GetComponentInParent<XROrigin>() != null) return;   // the player's body: Hit Player does it
            var otherBody = collision.rigidbody;
            if (otherBody != null && !otherBody.isKinematic)
            {
                // A loose body of the gate's (a Ball Valley ball): the gate knocks it on.
                var flat = Flat(body.linearVelocity);
                if (flat.sqrMagnitude < speedBefore * speedBefore) flat = flat.sqrMagnitude > 0.0001f ? flat.normalized * speedBefore : chargeDir * speedBefore;
                touched?.Invoke(other, flat);
                return;
            }
            // Something that stays put (a wall, a rock, a tree): a charge into it leaves it dizzy.
            if (state == State.Charge && speedBefore >= slamSpeed && collision.contactCount > 0)
            {
                var n = collision.GetContact(0).normal;
                if (Vector3.Dot(Flat(n).normalized, chargeDir) < -0.4f) Slam(collision.GetContact(0).point);
            }
        }

        private void PlayerHit(Collision collision, HitResult hit)
        {
            var key = (UnityEngine.Object)collision.rigidbody ?? collision.collider;
            if (key != null && nextHitBy.TryGetValue(key, out float next) && Time.time < next) return;
            if (hit.Damage <= 0f)
            {
                // Too soft to hurt: a wobble.
                squash = Mathf.Max(squash, 0.05f);
                return;
            }
            if (key != null) nextHitBy[key] = Time.time + hitCooldown;
            var at = collision.contactCount > 0 ? collision.GetContact(0).point : collision.transform.position;
            var settings = hitSettings != null ? hitSettings : HitSettings.Shared;
            if (settings.ShowsEffect(hit, true)) settings.PlayEffect(at, hit, 2f);
            float damage = Mathf.Max(1f, HitSettings.Whole(hit.Damage * (IsDizzy ? dizzyDamageTimes : 1f)));
            HitBuzz.Dealt(collision, damage);
            if (hp - damage <= 0f) HitBuzz.Killed(collision, damage);
            TakeDamage(damage, hit.IsBig);
        }

        /// <summary>A blast of the player's (a grenade, a bottle, an exploding punch).</summary>
        public void Hit(float strength, Vector3 point)
        {
            if (state == State.Broken || state == State.Dropping || state == State.Hop) return;
            TakeDamage(Mathf.Max(1f, HitSettings.Whole(strength / toughness * (IsDizzy ? dizzyDamageTimes : 1f))), false);
        }

        /// <summary>The gate's special move ran into it: <paramref name="damage"/> (times Dizzy Damage Times when dizzy).</summary>
        public void SpecialHit(float damage)
        {
            if (state == State.Broken || state == State.Dropping || state == State.Hop) return;
            TakeDamage(Mathf.Max(1f, HitSettings.Whole(damage * (IsDizzy ? dizzyDamageTimes : 1f))), true);
        }

        private void TakeDamage(float damage, bool big)
        {
            hp -= damage;
            Game.Combat.DamageNumbers.Show(transform, damage, big, hp <= 0f);
            flash = 1f;
            squash = Mathf.Max(squash, big ? 0.18f : 0.1f);
            Hurt?.Invoke(this, damage);
            if (hp <= 0f)
            {
                Break();
                return;
            }
            if (Random.value < 0.5f) PlayVoice(hurtSounds, 0.8f);
        }

        private void Break()
        {
            if (state == State.Broken) return;
            state = State.Broken;
            stateAt = Time.time;
            body.linearVelocity = Vector3.zero;
            body.isKinematic = true;
            ball.enabled = false;
            if (roller != null) roller.Stop();
            if (stunStars != null) Game.VFX.StunStars.Hide(transform);
            var middle = transform.position;
            var floor = new Vector3(middle.x, floorY, middle.z);
            if (breakSound != null) AudioSource.PlayClipAtPoint(breakSound, middle, 1f);
            foreach (var fx in breakEffects)
            {
                if (fx == null) continue;
                var made = Instantiate(fx, middle, Quaternion.identity);
                made.transform.localScale *= breakEffectSize;
                Destroy(made, 8f);
            }
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                Puff(middle + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius * 0.7f, puffSize * 1.3f);
            }
            if (breakCandies > 0 && candies != null && candies.Length > 0)
                Game.Bonus.CandyFountain.Play(floor + Vector3.up * 1.2f, breakCandies, candies, 1.2f, new Vector2(4f, 7f), new Vector2(2f, 5f));
            if (exp > 0) Game.Leveling.PlayerLevel.AddExp(exp);
            Debug.Log($"[Gates] {name} broke.", this);
            Broken?.Invoke(this);
            Destroy(gameObject, 0.5f);
        }

        // ---------------------------------------------------------------- every frame

        private void UpdateLook(float dt)
        {
            if (look == null) return;
            if (state == State.Broken)
            {
                // Bursts: swells, then gone.
                float t = (Time.time - stateAt) / 0.25f;
                look.localScale = Vector3.one * (lookScale * (t < 1f ? 1f + 0.3f * t : 0.01f));
                return;
            }
            var vel = Flat(body.isKinematic ? Vector3.zero : body.linearVelocity);
            float speed = vel.magnitude;
            // Faces the player (its way, while charging); rolls over and over on a charge, then rights itself.
            var faceTo = state == State.Charge ? chargeDir : Flat(PlayerFeet() - transform.position);
            if (faceTo.sqrMagnitude > 0.0001f) faceDir = Vector3.Slerp(faceDir, faceTo.normalized, 1f - Mathf.Exp(-(state == State.Charge ? 12f : 5f) * dt));
            if (state == State.Charge) spin += speed * dt / Mathf.Max(0.1f, radius);
            else spin = Mathf.MoveTowards(spin, Mathf.Round(spin / (Mathf.PI * 2f)) * Mathf.PI * 2f, 7f * dt);
            var turn = Quaternion.LookRotation(faceDir, Vector3.up) * Quaternion.Euler(spin * Mathf.Rad2Deg, 0f, 0f);
            if (state == State.Dizzy) turn = turn * Quaternion.Euler(Mathf.Sin(Time.time * 7f) * 8f, 0f, Mathf.Cos(Time.time * 5f) * 8f);
            look.rotation = turn;
            var shake = state == State.WindUp ? Random.insideUnitSphere * (radius * 0.03f) : Vector3.zero;
            look.localPosition = shake;
            squash = Mathf.MoveTowards(squash, 0f, dt * 1.5f);
            float s = squash * Mathf.Cos((Time.time - stateAt) * 25f);
            if (state == State.WindUp) s += 0.04f * Mathf.Sin(Time.time * 30f);
            look.localScale = new Vector3(1f + s, 1f - s, 1f + s) * lookScale;
            // A hit flashes it red.
            flash = Mathf.MoveTowards(flash, 0f, dt * 4f);
            for (int i = 0; i < looks.Count; i++)
            {
                var r = looks[i];
                if (r == null) continue;
                r.GetPropertyBlock(block);
                var c = Color.Lerp(lookColours[i], new Color(1f, 0.25f, 0.2f), flash * 0.8f);
                block.SetColor(BaseColorId, c);
                block.SetColor(ColorId, c);
                r.SetPropertyBlock(block);
            }
        }

        private void UpdateRollSound()
        {
            if (roller == null || rollSound == null) return;
            float speed = state == State.Dropping || state == State.Broken || body.isKinematic ? 0f : Flat(body.linearVelocity).magnitude;
            if (speed > 1f)
            {
                if (!roller.isPlaying)
                {
                    roller.clip = rollSound;
                    roller.Play();
                }
                roller.volume = Mathf.Clamp01(speed / Mathf.Max(1f, chargeSpeed)) * 0.9f + 0.1f;
                roller.pitch = Mathf.Lerp(0.8f, 1.1f, Mathf.Clamp01(speed / Mathf.Max(1f, chargeSpeed)));
            }
            else if (roller.isPlaying) roller.Stop();
        }

        // ---------------------------------------------------------------- the player

        private Vector3 PlayerFeet()
        {
            if (head == null) return transform.position + faceDir * 10f;
            float y = origin != null ? origin.transform.position.y : floorY;
            return new Vector3(head.position.x, y, head.position.z);
        }

        private bool PlayerDown()
        {
            return Game.Saving.Checkpoints.IsDying ||
                   (PlayerRevive.Instance != null && PlayerRevive.Instance.IsReviving) ||
                   (playerHealth != null && playerHealth.Value <= 0f);
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}

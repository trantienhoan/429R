using Game.Combat;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// A small flying slime the Rainbow Slime poops out (user 2026-11-05: "sometime poop out a colored flying slime that
    /// dive bomb into Player's face"). It plops out and drops a little, flies up in front of the player's face, hovers a
    /// moment, winds up and dive-bombs the face (a falling whistle). On the face: a bite (Face Share of a bee's), its
    /// paint splatted on the view, and it's gone. Dodged: it overshoots, turns and comes again (Dives times at most, then
    /// it pops by itself). One good punch (or anything that hurts it) pops it. Its Flying Slime is its look; Enemy Hit
    /// Damage and its SlimeHealth FSM take the hits. Set Up Slime Enemies builds Prefabs/Enemies/Slime_Bomber.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FlyingSlime))]
    public class SlimeBomber : MonoBehaviour
    {
        private enum State { Drop, Approach, Hover, WindUp, Dive, Overshoot, Gone }

        [Header("Dive bomb")]
        [Tooltip("It hovers this far in front of the face (metres) before it dives.")]
        [SerializeField] private float hoverDistance = 1.5f;
        [Tooltip("Metres a second it flies about, and dives (it speeds up from the first to the second as it dives).")]
        [SerializeField] private float flySpeed = 2.4f;
        [SerializeField] private Vector2 diveSpeed = new(2f, 5.5f);
        [Tooltip("Seconds it hovers in front of the face, then winds up, before the dive.")]
        [SerializeField] private float hoverSeconds = 0.45f;
        [SerializeField] private float windUpSeconds = 0.35f;
        [Tooltip("Dives at most, then it pops by itself.")]
        [Min(1)]
        [SerializeField] private int dives = 3;
        [Tooltip("It hits the face within this (metres) of the head.")]
        [SerializeField] private float faceRadius = 0.22f;
        [Tooltip("A hit on the face is this share of a bee's bite.")]
        [Range(0f, 2f)]
        [SerializeField] private float faceShare = 0.5f;
        [Tooltip("Seconds it lives at most (it pops by itself after this).")]
        [SerializeField] private float lifeSeconds = 16f;

        [Header("Look and sound")]
        [Tooltip("Paint splatted on the view when it hits the face (the paint stage's splat blob).")]
        [SerializeField] private Material splatMaterial;
        [SerializeField] private AudioClip[] diveSounds = System.Array.Empty<AudioClip>();
        [SerializeField] private AudioClip[] faceSounds = System.Array.Empty<AudioClip>();
        [SerializeField] private AudioClip[] popSounds = System.Array.Empty<AudioClip>();
        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.8f;

        private FlyingSlime look;
        private EnemyHitDamage hits;
        private State state;
        private float stateAt;
        private float bornAt;
        private Vector3 velocity;
        private Vector3 diveFrom;
        private Vector3 diveTo;
        private int divesLeft;
        private float sideOffset;
        private bool gone;

        /// <summary>Its paint colour (it's pooped out in this one).</summary>
        public Color Colour => look != null ? look.Colour : Color.white;

        /// <summary>Starts it off: its colour, and how it's flung out (metres a second).</summary>
        public void Launch(Color colour, Vector3 push)
        {
            if (look == null) look = GetComponent<FlyingSlime>();
            look.Dress(colour, false);
            velocity = push;
            Enter(State.Drop);
        }

        private void Awake()
        {
            look = GetComponent<FlyingSlime>();
            TryGetComponent(out hits);
            divesLeft = dives;
            bornAt = Time.time;
            sideOffset = Random.Range(-0.35f, 0.35f);
        }

        private void OnEnable()
        {
            EnemyHitDamage.AnyHit += OnHit;
            EnemyHitDamage.WeakHit += OnWeakHit;
        }

        private void OnDisable()
        {
            EnemyHitDamage.AnyHit -= OnHit;
            EnemyHitDamage.WeakHit -= OnWeakHit;
        }

        private void Enter(State next)
        {
            state = next;
            stateAt = Time.time;
        }

        private float InState => Time.time - stateAt;

        private void Update()
        {
            if (gone) return;
            if (hits != null && hits.IsDead)
            {
                // Popped by the player (its EXP is the hit's).
                Burst(true);
                return;
            }
            var head = SlimeKit.Head;
            if (head == null) return;
            if (Time.time - bornAt > lifeSeconds)
            {
                Burst(false);
                return;
            }
            float dt = Time.deltaTime;
            var pos = transform.position;
            var eyes = head.position;
            switch (state)
            {
                case State.Drop:
                    // Plopped out: it drops a little, slowing, then flaps up.
                    velocity += Vector3.down * 4f * dt;
                    velocity = Vector3.Lerp(velocity, Vector3.zero, 1f - Mathf.Exp(-3f * dt));
                    pos += velocity * dt;
                    if (InState > 0.45f) Enter(State.Approach);
                    break;
                case State.Approach:
                {
                    var spot = HoverSpot(head);
                    var want = spot - pos;
                    var desired = want.sqrMagnitude > 0.0001f ? want.normalized * Mathf.Min(flySpeed, want.magnitude * 3f) : Vector3.zero;
                    velocity = Vector3.MoveTowards(velocity, desired, 8f * dt);
                    pos += velocity * dt;
                    Face(eyes, 8f);
                    if (want.magnitude < 0.12f) Enter(State.Hover);
                    break;
                }
                case State.Hover:
                {
                    // In front of the face (it follows the head), getting ready.
                    pos = Vector3.Lerp(pos, HoverSpot(head), 1f - Mathf.Exp(-6f * dt));
                    velocity = Vector3.zero;
                    Face(eyes, 10f);
                    if (InState >= hoverSeconds)
                    {
                        look.WindUp(windUpSeconds);
                        Enter(State.WindUp);
                    }
                    break;
                }
                case State.WindUp:
                    pos = Vector3.Lerp(pos, HoverSpot(head), 1f - Mathf.Exp(-6f * dt));
                    Face(eyes, 12f);
                    if (InState >= windUpSeconds)
                    {
                        diveFrom = pos;
                        diveTo = eyes;
                        SlimeKit.Play(diveSounds, pos, volume, new Vector2(1f, 1.15f));
                        Enter(State.Dive);
                    }
                    break;
                case State.Dive:
                {
                    // At the face, faster and faster (following the head a little).
                    diveTo = Vector3.Lerp(diveTo, eyes, 1f - Mathf.Exp(-2f * dt));
                    var way = diveTo - pos;
                    float speed = Mathf.Lerp(diveSpeed.x, diveSpeed.y, Mathf.Clamp01(InState / 0.4f));
                    var dir = way.sqrMagnitude > 0.0001f ? way.normalized : transform.forward;
                    velocity = dir * speed;
                    pos += velocity * dt;
                    Face(pos + velocity, 15f);
                    if ((eyes - pos).magnitude <= faceRadius)
                    {
                        HitFace();
                        return;
                    }
                    // Past where the face was (dodged): it carries on, then comes round again.
                    if (Vector3.Dot(pos - diveTo, diveTo - diveFrom) > 0f && (pos - diveTo).magnitude > 0.25f) Enter(State.Overshoot);
                    break;
                }
                case State.Overshoot:
                    velocity = Vector3.Lerp(velocity, Vector3.zero, 1f - Mathf.Exp(-3f * dt));
                    pos += velocity * dt;
                    if (InState > 0.6f)
                    {
                        divesLeft--;
                        if (divesLeft <= 0)
                        {
                            Burst(false);
                            return;
                        }
                        Enter(State.Approach);
                    }
                    break;
            }
            transform.position = pos;
        }

        // In front of the face, where the player looks (a little to one side), at eye height.
        private Vector3 HoverSpot(Transform head)
        {
            var ahead = head.forward;
            ahead.y = 0f;
            if (ahead.sqrMagnitude < 0.0001f) ahead = Vector3.forward;
            ahead.Normalize();
            var right = Vector3.Cross(Vector3.up, ahead);
            return head.position + ahead * hoverDistance + right * sideOffset + Vector3.up * 0.05f;
        }

        private void Face(Vector3 target, float sharpness)
        {
            var to = target - transform.position;
            if (to.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 1f - Mathf.Exp(-sharpness * Time.deltaTime));
        }

        // Splat on the face: the bite, its paint on the view, and it's gone.
        private void HitFace()
        {
            SlimeKit.HurtPlayer(faceShare);
            Game.VFX.PaintOnView.Splat(splatMaterial, Colour);
            SlimeKit.Play(faceSounds, transform.position, volume, new Vector2(0.95f, 1.1f));
            if (hits != null) hits.noReward = true;
            Pop(false);
        }

        // Hit but not hurt to death: knocked back, then it comes again.
        private void OnHit(EnemyHitDamage who, HitResult hit)
        {
            if (who != hits || gone || (hits != null && hits.IsDead)) return;
            Knocked(1f);
        }

        private void OnWeakHit(EnemyHitDamage who)
        {
            if (who != hits || gone) return;
            Knocked(0.6f);
        }

        private void Knocked(float strength)
        {
            var head = SlimeKit.Head;
            var from = head != null ? head.position : transform.position - transform.forward;
            look.Bonk(from, strength);
            var away = transform.position - from;
            velocity = (away.sqrMagnitude > 0.0001f ? away.normalized : -transform.forward) * 2.5f * strength + Vector3.up * 0.5f;
            Enter(State.Overshoot);
        }

        private void Burst(bool byPlayer)
        {
            if (byPlayer) SlimeKit.Play(popSounds, transform.position, volume, new Vector2(1f, 1.3f));
            Pop(byPlayer);
        }

        private void Pop(bool byPlayer)
        {
            if (gone) return;
            gone = true;
            state = State.Gone;
            var head = SlimeKit.Head;
            look.Pop(byPlayer && head != null ? head.position : (Vector3?)null);
        }
    }
}

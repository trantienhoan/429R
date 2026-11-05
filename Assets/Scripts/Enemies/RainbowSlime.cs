using System.Collections.Generic;
using Game.Combat;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// The rare rainbow flying slime (user 2026-11-05: it takes the white cockroach's place: "make it fly everywhere,
    /// make fun of the Player, sometime poop out a colored flying slime that dive bomb into Player's face"). Any bug
    /// that turns out to be the rare one (Spider Brain's Anomaly Chance) becomes this instead, in the air where it
    /// appeared. It flies about the room above and round the player, out of reach: it darts off when they come close or
    /// a fist comes at it. Now and then it stops in front of them and teases (wiggles, laughs); now and then it strains
    /// and poops out a Slime Bomber of a random paint colour, which dive-bombs their face. Killing it (2 health: a good
    /// throw, a lucky punch) gives the Candy Helmet, as the white cockroach did; left alone, after its Lifetime it
    /// laughs and flies off, gone. Set Up Slime Enemies builds Prefabs/Enemies/Rainbow_Slime and sets it on the bugs.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FlyingSlime))]
    public class RainbowSlime : MonoBehaviour
    {
        private enum State { Rise, Roam, Linger, Dart, Taunt, Poop, Leave, Gone }

        [Header("Flying about")]
        [Tooltip("Metres a second it flies about, and when it darts off.")]
        [SerializeField] private float speed = 2.6f;
        [SerializeField] private float dartSpeed = 6f;
        [Tooltip("How high it flies (metres above the player's floor) and how far from them (random between).")]
        [SerializeField] private Vector2 height = new(1.3f, 2.6f);
        [SerializeField] private Vector2 distance = new(2.5f, 6.5f);
        [Tooltip("It darts off when the player's head comes within this (metres), or a fist moving at it this fast " +
                 "(metres a second) comes within Fist Dodge.")]
        [SerializeField] private float keepAway = 2.1f;
        [SerializeField] private float fistDodge = 1.3f;
        [SerializeField] private float fistSpeed = 1.5f;

        [Header("Teasing")]
        [Tooltip("Seconds between teases (random between): it stops in front of the player, wiggles and laughs.")]
        [SerializeField] private Vector2 tauntEvery = new(5f, 9f);
        [SerializeField] private float tauntSeconds = 1.8f;
        [SerializeField] private float tauntDistance = 2.6f;
        [SerializeField] private AudioClip[] laughs = System.Array.Empty<AudioClip>();
        [Range(0f, 1f)]
        [SerializeField] private float laughVolume = 0.85f;
        [SerializeField] private Vector2 laughPitch = new(1.1f, 1.35f);

        [Header("Pooping")]
        [Tooltip("The slime it poops out (Slime_Bomber), in one of Poop Colours at random.")]
        [SerializeField] private SlimeBomber bomber;
        [SerializeField] private Color[] poopColours =
        {
            new(0.89f, 0.15f, 0.18f), new(0.2f, 0.45f, 1f), new(1f, 0.85f, 0.12f), new(0.25f, 0.9f, 0.3f),
        };
        [Tooltip("Seconds between poops (random between), and the most of its bombers about at once.")]
        [SerializeField] private Vector2 poopEvery = new(6f, 10f);
        [Min(1)]
        [SerializeField] private int mostBombers = 2;
        [Tooltip("Seconds it strains before one comes out.")]
        [SerializeField] private float strainSeconds = 0.55f;
        [SerializeField] private AudioClip[] poopSounds = System.Array.Empty<AudioClip>();
        [Range(0f, 1f)]
        [SerializeField] private float poopVolume = 0.9f;

        [Header("Reward and leaving")]
        [Tooltip("Killing it: the Candy Helmet for this many seconds (the white cockroach's reward), this effect where it " +
                 "pops (Effect Pool) and this sound.")]
        [SerializeField] private float helmetSeconds = 10f;
        [SerializeField] private GameObject rewardPop;
        [SerializeField] private AudioClip rewardSound;
        [SerializeField] private AudioClip[] popSounds = System.Array.Empty<AudioClip>();
        [Tooltip("Seconds it stays (random between) before it laughs and flies off, gone (no reward).")]
        [SerializeField] private Vector2 lifetime = new(25f, 40f);

        private const float BodyRoom = 0.22f;
        private static readonly RaycastHit[] rayHits = new RaycastHit[16];

        private FlyingSlime look;
        private EnemyHitDamage hits;
        private State state;
        private float stateAt;
        private float stateFor;
        private Vector3 target;
        private Vector3 velocity;
        private float nextTaunt, nextPoop, leaveAt, nextDart, nextPick;
        private float wigglePhase;
        private float poopSide;
        private readonly List<SlimeBomber> bombers = new();
        private bool gone;

        private void Awake()
        {
            look = GetComponent<FlyingSlime>();
            TryGetComponent(out hits);
            look.Dress(Color.white, true);
            float now = Time.time;
            nextTaunt = now + Random.Range(2.5f, 4.5f);
            nextPoop = now + Random.Range(poopEvery.x * 0.6f, poopEvery.x);
            leaveAt = now + Random.Range(lifetime.x, Mathf.Max(lifetime.x, lifetime.y));
        }

        private void Start()
        {
            // It appears where a bug would have (on the floor): up it goes, giggling.
            target = transform.position + Vector3.up * 1.4f;
            Enter(State.Rise, 1.2f);
            Laugh();
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

        private void Enter(State next, float seconds = 0f)
        {
            state = next;
            stateAt = Time.time;
            stateFor = seconds;
        }

        private float InState => Time.time - stateAt;

        private void Update()
        {
            if (gone) return;
            if (hits != null && hits.IsDead)
            {
                Killed();
                return;
            }
            var head = SlimeKit.Head;
            if (head == null) return;
            float dt = Time.deltaTime;
            float now = Time.time;
            bombers.RemoveAll(b => b == null);

            if (state != State.Leave && now >= leaveAt) StartLeaving(head);
            // Too close, or a fist coming at it: it darts off (giggling now and then).
            if (state != State.Leave && state != State.Dart && now >= nextDart && Threatened(head, out var away))
            {
                Dart(away);
                if (Random.value < 0.5f) Laugh();
            }

            switch (state)
            {
                case State.Rise:
                case State.Roam:
                    FlyTo(target, speed, dt);
                    if ((target - transform.position).magnitude < 0.35f || now >= nextPick || InState > 6f)
                    {
                        if (state == State.Rise || Random.value < 0.4f) Enter(State.Linger, Random.Range(0.3f, 1f));
                        else PickRoam(head);
                    }
                    FaceTravel(head);
                    if (state != State.Linger) DecideNext(head, now);
                    break;
                case State.Linger:
                    // A pause in the air, bobbing, watching the player.
                    velocity = Vector3.Lerp(velocity, Vector3.zero, 1f - Mathf.Exp(-4f * dt));
                    transform.position += velocity * dt;
                    Face(head.position, 6f);
                    if (InState >= stateFor) PickRoam(head);
                    DecideNext(head, now);
                    break;
                case State.Dart:
                    FlyTo(target, dartSpeed, dt, 30f);
                    FaceTravel(head);
                    if (InState >= stateFor || (target - transform.position).magnitude < 0.3f) PickRoam(head);
                    break;
                case State.Taunt:
                {
                    var spot = InFront(head, tauntDistance, 0.05f, 0f);
                    if (stateFor < 0f)
                    {
                        // Getting in front of the player; there (or after a while), the tease starts.
                        FlyTo(spot, speed * 1.3f, dt);
                        Face(head.position, 8f);
                        if ((spot - transform.position).magnitude <= 0.25f || InState > 2.5f)
                        {
                            stateFor = tauntSeconds;
                            stateAt = now;
                            Laugh();
                        }
                        break;
                    }
                    Tease(head, spot, dt);
                    if (InState >= stateFor)
                    {
                        nextTaunt = now + Random.Range(tauntEvery.x, tauntEvery.y);
                        PickRoam(head);
                    }
                    break;
                }
                case State.Poop:
                {
                    var spot = InFront(head, 2.3f, 0.75f, poopSide);
                    if (stateFor < 0f)
                    {
                        FlyTo(spot, speed * 1.2f, dt);
                        Face(head.position, 6f);
                        if ((spot - transform.position).magnitude < 0.25f || InState > 3f)
                        {
                            // Straining (flat, shaking)...
                            stateFor = strainSeconds;
                            stateAt = now;
                            look.WindUp(strainSeconds);
                        }
                        break;
                    }
                    velocity = Vector3.zero;
                    transform.position = Vector3.Lerp(transform.position, spot, 1f - Mathf.Exp(-4f * dt)) + Random.insideUnitSphere * 0.004f;
                    Face(head.position, 6f);
                    if (InState >= stateFor)
                    {
                        // ...and plop, one comes out underneath.
                        PoopOne();
                        nextPoop = now + Random.Range(poopEvery.x, poopEvery.y);
                        if (Random.value < 0.7f) Laugh();
                        PickRoam(head);
                    }
                    break;
                }
                case State.Leave:
                    FlyTo(target, speed * 1.4f, dt);
                    FaceTravel(head);
                    transform.localScale = Vector3.Lerp(transform.localScale, Vector3.zero, 1f - Mathf.Exp(-1.2f * dt));
                    if (InState > 2.2f) Vanish();
                    break;
            }
        }

        // Teasing: in front of the player, wiggling side to side and bobbing, laughing.
        private void Tease(Transform head, Vector3 spot, float dt)
        {
            wigglePhase += dt * 2f * Mathf.PI * 3.5f;
            transform.position = Vector3.Lerp(transform.position, spot + Vector3.up * (Mathf.Abs(Mathf.Sin(wigglePhase * 0.5f)) * 0.08f), 1f - Mathf.Exp(-8f * dt));
            velocity = Vector3.zero;
            var to = head.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(to) * Quaternion.Euler(0f, Mathf.Sin(wigglePhase) * 28f, Mathf.Sin(wigglePhase * 0.5f) * 12f);
            if (Mathf.Sin(wigglePhase) > 0.98f) look.Wobble(0.25f);
        }

        // Time for a tease or a poop (a poop only with room for another bomber)?
        private void DecideNext(Transform head, float now)
        {
            if (bomber != null && now >= nextPoop && bombers.Count < mostBombers)
            {
                poopSide = Random.Range(-0.7f, 0.7f);
                Enter(State.Poop, -1f);
                return;
            }
            if (now >= nextTaunt) Enter(State.Taunt, -1f);
        }

        private void PoopOne()
        {
            if (bomber == null) return;
            float scale = Mathf.Abs(transform.lossyScale.y);
            var at = transform.position + Vector3.down * 0.13f * scale;
            var made = Instantiate(bomber, at, transform.rotation, transform.parent);
            made.Launch(poopColours.Length > 0 ? poopColours[Random.Range(0, poopColours.Length)] : Color.red,
                Vector3.down * 0.8f + Random.insideUnitSphere * 0.3f);
            bombers.Add(made);
            SlimeKit.Play(poopSounds, at, poopVolume, new Vector2(0.9f, 1.15f));
            look.Wobble(1.2f);
            // A little hop up as it comes out.
            velocity = Vector3.up * 1.2f;
        }

        // Somewhere in the room round the player, up in the air, that it can get to in a straight line.
        private void PickRoam(Transform head)
        {
            Enter(State.Roam);
            nextPick = Time.time + Random.Range(2.5f, 4.5f);
            target = FindSpot(head, distance, height, 0.5f);
        }

        private Vector3 FindSpot(Transform head, Vector2 range, Vector2 up, float inViewChance)
        {
            var eyes = head.position;
            float floor = SlimeKit.FloorUnder(eyes);
            var ahead = head.forward;
            ahead.y = 0f;
            if (ahead.sqrMagnitude < 0.0001f) ahead = Vector3.forward;
            ahead.Normalize();
            var best = transform.position;
            float bestScore = float.MinValue;
            bool inView = Random.value < inViewChance;
            for (int i = 0; i < 14; i++)
            {
                float angle = inView ? Random.Range(-55f, 55f) : Random.Range(-180f, 180f);
                var way = Quaternion.Euler(0f, angle, 0f) * ahead;
                var spot = new Vector3(eyes.x, floor, eyes.z) + way * Random.Range(range.x, range.y) + Vector3.up * Random.Range(up.x, up.y);
                if (!Clear(transform.position, spot)) continue;
                // Far from where it is (it moves about), not too near the player.
                float score = (spot - transform.position).magnitude * 0.5f + Mathf.Min((spot - eyes).magnitude, 4f) + Random.value;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = spot;
                }
            }
            return bestScore > float.MinValue ? best : transform.position + Vector3.up * 0.3f;
        }

        // Room for its body there, and nothing in the way on the straight line (its own collider and fists don't count).
        private bool Clear(Vector3 from, Vector3 to)
        {
            float scale = Mathf.Abs(transform.lossyScale.y);
            float r = BodyRoom * scale;
            if (Physics.CheckSphere(to, r, ~0, QueryTriggerInteraction.Ignore)) return false;
            var way = to - from;
            float length = way.magnitude;
            if (length < 0.01f) return true;
            int n = Physics.SphereCastNonAlloc(from, r * 0.8f, way / length, rayHits, length, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var hit = rayHits[i].collider;
                if (hit == null || hit.transform.IsChildOf(transform)) continue;
                if (hit.attachedRigidbody != null && Fist.IsFistBall(hit.attachedRigidbody)) continue;
                return false;
            }
            return true;
        }

        // The spot in front of the player's eyes (flat), this far, this much above them, this far to the side.
        private static Vector3 InFront(Transform head, float far, float above, float side)
        {
            var ahead = head.forward;
            ahead.y = 0f;
            if (ahead.sqrMagnitude < 0.0001f) ahead = Vector3.forward;
            ahead.Normalize();
            return head.position + ahead * far + Vector3.up * above + Vector3.Cross(Vector3.up, ahead) * side;
        }

        private bool Threatened(Transform head, out Vector3 away)
        {
            var pos = transform.position;
            away = pos - head.position;
            if (away.magnitude < keepAway) return true;
            foreach (var fist in SlimeKit.Fists)
            {
                if (fist == null) continue;
                var at = fist.Knuckles;
                var to = pos - at;
                if (to.magnitude > fistDodge) continue;
                if (Vector3.Dot(fist.HandVelocity, to.normalized) < fistSpeed) continue;
                // Off to the side of the punch (and away).
                var side = Vector3.Cross(fist.HandVelocity, Vector3.up);
                if (side.sqrMagnitude < 0.0001f) side = Vector3.Cross(to, Vector3.up);
                away = side.normalized * (Random.value < 0.5f ? -1f : 1f) + to.normalized * 0.6f;
                return true;
            }
            return false;
        }

        private void Dart(Vector3 away)
        {
            away.y = Mathf.Max(away.y, 0.15f);
            var dir = away.normalized;
            var to = transform.position + dir * 1.8f;
            if (!Clear(transform.position, to)) to = transform.position + Vector3.Reflect(dir, Vector3.up).normalized * 1.2f + Vector3.up * 0.6f;
            target = to;
            nextDart = Time.time + 0.35f;
            Enter(State.Dart, 0.45f);
        }

        // Steers there (a little swoop on the way), slowing as it gets close.
        private void FlyTo(Vector3 to, float topSpeed, float dt, float steer = 7f)
        {
            var pos = transform.position;
            var want = to - pos;
            float dist = want.magnitude;
            var desired = dist > 0.001f ? want / dist * Mathf.Min(topSpeed, dist * 2.5f) : Vector3.zero;
            var side = Vector3.Cross(Vector3.up, desired);
            desired += side.normalized * Mathf.Sin(Time.time * 2.3f) * 0.35f * Mathf.Min(1f, dist);
            velocity = Vector3.MoveTowards(velocity, desired, steer * dt);
            transform.position = pos + velocity * dt;
        }

        private void FaceTravel(Transform head)
        {
            // Quick: face where it's going; slow: face the player.
            var flat = velocity;
            flat.y = 0f;
            Face(flat.magnitude > 1.2f ? transform.position + flat : head.position, 6f);
        }

        private void Face(Vector3 at, float sharpness)
        {
            var to = at - transform.position;
            to.y *= 0.3f;
            if (to.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 1f - Mathf.Exp(-sharpness * Time.deltaTime));
        }

        private void Laugh() => SlimeKit.Play(laughs, transform.position, laughVolume, laughPitch);

        private void OnHit(EnemyHitDamage who, HitResult hit)
        {
            if (who != hits || gone || (hits != null && hits.IsDead)) return;
            var head = SlimeKit.Head;
            var from = head != null ? head.position : transform.position - transform.forward;
            look.Bonk(from, 1f);
            Dart(transform.position - from);
        }

        private void OnWeakHit(EnemyHitDamage who)
        {
            if (who != hits || gone) return;
            var head = SlimeKit.Head;
            look.Bonk(head != null ? head.position : transform.position - transform.forward, 0.6f);
            if (Random.value < 0.6f) Laugh();
        }

        // Its time is up: one more laugh and off it goes, up and away, shrinking, gone (no reward).
        private void StartLeaving(Transform head)
        {
            Laugh();
            var away = transform.position - head.position;
            away.y = 0f;
            target = transform.position + (away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.forward) * 3f + Vector3.up * 2.5f;
            Enter(State.Leave);
        }

        private void Vanish()
        {
            gone = true;
            state = State.Gone;
            if (rewardPop != null) EffectPool.Play(rewardPop, transform.position, 0.6f);
            look.ReleaseDrops();
            Destroy(gameObject);
        }

        // Killed by the player: the Candy Helmet (as the white cockroach), its sparkle, and it bursts into rainbow paint.
        private void Killed()
        {
            gone = true;
            state = State.Gone;
            var at = transform.position;
            if (hits == null || !hits.noReward)
            {
                if (rewardPop != null) EffectPool.Play(rewardPop, at);
                if (rewardSound != null) AudioSource.PlayClipAtPoint(rewardSound, at);
                if (helmetSeconds > 0f) Game.Leveling.PlayerBuffs.Give(Game.Leveling.Buff.CandyHelmet, helmetSeconds);
            }
            SlimeKit.Play(popSounds, at, 0.9f, new Vector2(1f, 1.2f));
            var head = SlimeKit.Head;
            look.Pop(head != null ? head.position : (Vector3?)null);
        }
    }
}

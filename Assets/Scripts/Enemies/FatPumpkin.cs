using System.Collections.Generic;
using Game.Combat;
using Game.Inventory;
using Game.VFX;
using UnityEngine;
using UnityEngine.AI;
using FsmVariables = HutongGames.PlayMaker.FsmVariables;

namespace Game.Enemies
{
    /// <summary>
    /// A greedy tree pumpkin (Turns Into Fat Pumpkin makes it): it wakes up where a Pumpkin_On_Tree sat among the candies,
    /// then waddles after every candy lying on the floor, nearest first, sucks each one in and gets fatter and slower. It
    /// never hurts the player: it only steals their candy. A good punch or throw of theirs breaks it (Health 2: a hard hit
    /// at once, or two normal ones; the player's blasts too), and it gives back only a few of what it ate (Drop Share).
    /// Goes on the prefab's root, next to its NavMesh Agent. Its look is under Model > Jiggle: an Animator with a controller
    /// found under it replaces the waddle, and gets the Walking bool, the Walk Speed float and the Eat, Hit and Laugh triggers
    /// if its controller has them (Tools > 429 Game > Enemies > Set Up Fat Pumpkin Animation makes all that).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public class FatPumpkin : MonoBehaviour, IHittable
    {
        [Header("Look")]
        [Tooltip("The pivot that waddles (on the floor, under the model).")]
        [SerializeField] private Transform model;
        [Tooltip("Wobbles at every gulp and hit.")]
        [SerializeField] private Jiggle jiggle;
        [Tooltip("Its body's radius, in metres at size 1: how far its mouth is from its middle.")]
        [Min(0.01f)]
        [SerializeField] private float radius = 0.27f;
        [Tooltip("Where the candy flies to. Empty: just inside the front of its body.")]
        [SerializeField] private Transform mouth;
        [Tooltip("How far it rocks from side to side as it walks, in degrees, and how high it hops, in metres. Not with an animation.")]
        [SerializeField] private Vector2 waddle = new(9f, 0.04f);
        [Tooltip("Steps per second at full walking speed.")]
        [Min(0f)]
        [SerializeField] private float waddlePerSecond = 2.2f;

        [Header("Waking up")]
        [Tooltip("Played where it bursts out of the tree pumpkin (Effect Pool), and their size.")]
        [SerializeField] private GameObject[] breakOutEffects = System.Array.Empty<GameObject>();
        [Min(0f)]
        [SerializeField] private float breakOutEffectSize = 0.6f;
        [SerializeField] private AudioClip breakOutSound;
        [Tooltip("Seconds it looks at the player, laughing, before it goes for the candy.")]
        [Min(0f)]
        [SerializeField] private float wakeSeconds = 0.8f;
        [Tooltip("Its laugh as it wakes up, and now and then while it eats (one at random).")]
        [SerializeField] private AudioClip[] laughs = System.Array.Empty<AudioClip>();
        [Tooltip("Seconds between laughs (min, max).")]
        [SerializeField] private Vector2 laughEvery = new(4f, 8f);
        [Tooltip("Its voice's pitch: lower is fatter.")]
        [Range(0.5f, 2f)]
        [SerializeField] private float voicePitch = 0.85f;

        [Header("Eating")]
        [Tooltip("How fast it waddles when it's thin, in metres per second.")]
        [Min(0.1f)]
        [SerializeField] private float walkSpeed = 1.5f;
        [Tooltip("The share of that speed left once it's as fat as it gets.")]
        [Range(0.1f, 1f)]
        [SerializeField] private float fullSpeedShare = 0.6f;
        [Tooltip("How far off it notices a candy, in metres.")]
        [Min(1f)]
        [SerializeField] private float sightRange = 30f;
        [Tooltip("A candy this close to its side is sucked in, in metres.")]
        [Min(0f)]
        [SerializeField] private float eatReach = 0.35f;
        [Tooltip("Seconds a candy takes to fly into its mouth, and how long it stands still for each gulp.")]
        [Min(0.05f)]
        [SerializeField] private float eatSeconds = 0.25f;
        [Tooltip("It gets this much bigger for every candy (0.03 = 3%)...")]
        [Min(0f)]
        [SerializeField] private float growPerCandy = 0.03f;
        [Tooltip("...up to this many times its first size.")]
        [Min(1f)]
        [SerializeField] private float mostGrowth = 1.6f;
        [Tooltip("Played at every gulp (at most one every Gulp Gap).")]
        [SerializeField] private AudioClip eatSound;
        [Range(0f, 1f)]
        [SerializeField] private float eatVolume = 0.8f;

        [Header("Breaking")]
        [Tooltip("Its health, in the game's whole-number damage: a normal punch does 1, a hard punch or most throws 2.")]
        [Min(1f)]
        [SerializeField] private float health = 2f;
        [Tooltip("How hits turn into damage (the enemies' shared Hit Settings asset). Empty: the default numbers.")]
        [SerializeField] private HitSettings hitSettings;
        [Tooltip("Divides the hit power, like an enemy's Toughness.")]
        [Min(0.1f)]
        [SerializeField] private float toughness = 1f;
        [Tooltip("After a hit that hurts, more hits are ignored this long, in seconds, so one swing counts once.")]
        [Min(0f)]
        [SerializeField] private float hitCooldown = 0.2f;
        [Tooltip("A hit that doesn't break it knocks it back this far, in metres, and it stands stunned this long, in seconds.")]
        [SerializeField] private Vector2 knock = new(0.8f, 0.5f);
        [Tooltip("Played at a hit that doesn't break it (one at random).")]
        [SerializeField] private AudioClip[] hitSounds = System.Array.Empty<AudioClip>();
        [Tooltip("Played where it bursts (Effect Pool), and their size.")]
        [SerializeField] private GameObject[] burstEffects = System.Array.Empty<GameObject>();
        [Min(0f)]
        [SerializeField] private float burstEffectSize = 0.7f;
        [SerializeField] private AudioClip burstSound;
        [Tooltip("The pieces it bursts into (a Broken_Stuffs prefab), and their size for each of its own. Empty: none.")]
        [SerializeField] private GameObject pieces;
        [Min(0.01f)]
        [SerializeField] private float piecesSize = 0.55f;
        [Tooltip("EXP the player gets for breaking it.")]
        [Min(0)]
        [SerializeField] private int exp = 2;

        [Header("Candy back")]
        [Tooltip("One of these, picked at random, per candy it gives back.")]
        [SerializeField] private GameObject[] candies = System.Array.Empty<GameObject>();
        [Tooltip("It gives back this share of what it ate, rounded down...")]
        [Range(0f, 1f)]
        [SerializeField] private float dropShare = 0.25f;
        [Tooltip("...but at least this many...")]
        [Min(0)]
        [SerializeField] private int fewestCandies = 1;
        [Tooltip("...and at most this many.")]
        [Min(0)]
        [SerializeField] private int mostCandies = 4;
        [Tooltip("How hard they pop up, in metres per second. Candies break on a hard landing, so keep it low.")]
        [Min(0f)]
        [SerializeField] private float candyLaunchSpeed = 2.2f;

        [Header("Animation (only if its controller has them)")]
        [SerializeField] private string walkingBool = "Walking";
        [SerializeField] private string eatTrigger = "Eat";
        [SerializeField] private string hitTrigger = "Hit";
        [SerializeField] private string laughTrigger = "Laugh";
        [Tooltip("Its walking speed as a share of its thin speed: how fast it waddles.")]
        [SerializeField] private string walkSpeedFloat = "WalkSpeed";

        private enum Phase { Waking, Hunting, Gone }

        private class Swallow
        {
            public GameObject candy;
            public Vector3 from;
            public Vector3 size;
            public float start;
        }

        // How often it looks round for candy, in seconds.
        private const float LookEvery = 0.25f;
        // A candy it can't get to (no path, or it got stuck) is left alone this long, in seconds.
        private const float LeaveAloneSeconds = 6f;
        private const float StuckSeconds = 1.5f;
        // How far from the NavMesh a candy can lie and still be gone for, in metres.
        private const float NavMeshReach = 1f;
        // A candy this far above or below it is out of its reach, in metres.
        private const float EatHeight = 1.2f;
        // At most this many candies in the air to its mouth at once.
        private const int MostSwallowing = 3;
        private const float GulpGap = 0.2f;
        private const float GulpWobble = 1f;
        private const float HitWobble = 1.5f;
        private const float SoftWobble = 0.4f;
        // Its pop out of the tree pumpkin: seconds, and how small it starts.
        private const float PopSeconds = 0.3f;
        private const float PopFrom = 0.5f;
        private const float GrowPerSecond = 0.5f;
        private const float TurnDegrees = 240f;
        private const float MinWalkAnimation = 0.3f;
        private const float RepathMoved = 0.4f;
        private const float KnockSeconds = 0.35f;
        private const float KnockHop = 0.2f;
        // The ring its candies pop out of: how far out from its side, how high, and how fast sideways.
        private const float CandyRingGap = 0.1f;
        private const float CandyHeight = 0.3f;
        private const float CandyOutward = 1f;
        private const string SpawnedStuffVariable = "CurrentlySpawnedStuffs";

        // The candies lying about, as seen by the last one who looked.
        private static readonly List<GameObject> loose = new();

        /// <summary>How many are out right now.</summary>
        public static int Alive { get; private set; }

        /// <summary>Set while the game runs: it goes without the player earning it, so no EXP. See PlayerLevel.NoRewardFor.</summary>
        [System.NonSerialized] public bool noReward;

        /// <summary>How many candies it has eaten.</summary>
        public int Eaten { get; private set; }

        private NavMeshAgent agent;
        private NavMeshPath path;
        private AudioSource speaker;
        private Animator animator;
        private bool hasWalking, hasWalkSpeed, hasEat, hasHit, hasLaugh;
        private Knockback knockback;
        private Transform head;
        private Phase phase;
        private Vector3 fullScale;
        private Vector3 modelHome;
        private float bornAt;
        private float shownGrowth = 1f;
        private float waddleClock;
        private float nextLook;
        private float nextLaugh;
        private float nextGulpSound;
        private float nextHurt;
        private float nextSoftWobble;
        private float pausedUntil;
        private float stuckFor;
        private GameObject target;
        private Vector3 targetWasAt;
        private readonly List<Swallow> swallows = new();
        private readonly Dictionary<GameObject, float> leftAlone = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Alive = 0;

        private void OnEnable() => Alive++;
        private void OnDisable() => Alive--;

        private void Start()
        {
            agent = GetComponent<NavMeshAgent>();
            agent.updateRotation = false;
            if (agent.isOnNavMesh) agent.isStopped = true;
            path = new NavMeshPath();
            TryGetComponent(out speaker);
            if (speaker != null) speaker.pitch = voicePitch;
            FindAnimation();
            if (model != null) modelHome = model.localPosition;
            fullScale = transform.localScale;
            bornAt = Time.time;
            head = Camera.main != null ? Camera.main.transform : null;
            nextLaugh = Time.time + Random.Range(laughEvery.x, laughEvery.y);

            foreach (var effect in breakOutEffects)
            {
                if (effect != null) EffectPool.Play(effect, Middle(), breakOutEffectSize);
            }
            if (breakOutSound != null) AudioSource.PlayClipAtPoint(breakOutSound, Middle());
            Laugh();
            phase = Phase.Waking;
        }

        // The model that replaces the first one brings its own animation.
        private void FindAnimation()
        {
            foreach (var found in GetComponentsInChildren<Animator>(true))
            {
                if (found.runtimeAnimatorController == null) continue;
                animator = found;
                foreach (var parameter in found.parameters)
                {
                    if (parameter.name == walkingBool) hasWalking = true;
                    else if (parameter.name == eatTrigger) hasEat = true;
                    else if (parameter.name == hitTrigger) hasHit = true;
                    else if (parameter.name == laughTrigger) hasLaugh = true;
                    else if (parameter.name == walkSpeedFloat) hasWalkSpeed = true;
                }
                return;
            }
        }

        private void Update()
        {
            if (phase == Phase.Gone) return;
            FlySwallows();
            Grow();
            if (phase == Phase.Waking)
            {
                TurnTo(HeadFlat());
                if (Time.time - bornAt >= wakeSeconds) phase = Phase.Hunting;
                return;
            }
            Hunt();
        }

        // ---------- Going after the candy ----------

        private void Hunt()
        {
            if (Time.time >= nextLook)
            {
                nextLook = Time.time + LookEvery;
                LookForCandy();
            }
            bool standing = Time.time < pausedUntil || target == null;
            bool knocked = knockback != null && knockback.IsKnockedBack;
            if (agent.isOnNavMesh)
            {
                agent.isStopped = standing || knocked;
                // The fatter, the slower.
                float fatness = Mathf.InverseLerp(1f, Mathf.Max(1.01f, mostGrowth), Growth);
                agent.speed = walkSpeed * Mathf.Lerp(1f, fullSpeedShare, fatness);
            }

            var velocity = agent.velocity;
            velocity.y = 0f;
            float speed = velocity.magnitude;
            WatchForStuck(standing || knocked, speed);
            if (speed > 0.1f) TurnTo(velocity);
            else if (target != null) TurnTo(target.transform.position - transform.position);
            else TurnTo(HeadFlat());
            Waddle(speed);
            if (hasWalking) animator.SetBool(walkingBool, speed > 0.1f);
            if (hasWalkSpeed) animator.SetFloat(walkSpeedFloat, Mathf.Clamp(speed / walkSpeed, MinWalkAnimation, 1f));

            if (Time.time >= nextLaugh)
            {
                nextLaugh = Time.time + Random.Range(laughEvery.x, laughEvery.y);
                Laugh();
            }
        }

        // Sucks in what's in reach, and goes for the nearest of the others.
        private void LookForCandy()
        {
            CandyHelmet.FindLoose(loose);
            var here = transform.position;
            float mouthReach = radius * Size + eatReach;
            GameObject nearest = null;
            float best = sightRange * sightRange;
            foreach (var candy in loose)
            {
                var to = candy.transform.position - here;
                float height = Mathf.Abs(to.y);
                to.y = 0f;
                float away = to.sqrMagnitude;
                if (away <= mouthReach * mouthReach && height <= EatHeight)
                {
                    if (swallows.Count < MostSwallowing) Take(candy);
                    continue;
                }
                if (away >= best || LeftAlone(candy)) continue;
                best = away;
                nearest = candy;
            }
            if (target == null || !loose.Contains(target) || nearest != target) GoFor(nearest);
            else if ((target.transform.position - targetWasAt).sqrMagnitude > RepathMoved * RepathMoved) GoFor(target);
        }

        private void GoFor(GameObject candy)
        {
            target = candy;
            stuckFor = 0f;
            if (!agent.isOnNavMesh) return;
            if (candy == null)
            {
                agent.ResetPath();
                return;
            }
            targetWasAt = candy.transform.position;
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (NavMesh.SamplePosition(targetWasAt, out var spot, NavMeshReach, filter) && agent.CalculatePath(spot.position, path) &&
                path.status == NavMeshPathStatus.PathComplete)
            {
                agent.SetPath(path);
                return;
            }
            LeaveAlone(candy);
            target = null;
            agent.ResetPath();
        }

        // Walking at nothing (furniture in the way): that candy is left alone a while.
        private void WatchForStuck(bool standing, float speed)
        {
            if (standing || target == null || speed > 0.1f)
            {
                stuckFor = 0f;
                return;
            }
            stuckFor += Time.deltaTime;
            if (stuckFor < StuckSeconds) return;
            LeaveAlone(target);
            GoFor(null);
        }

        private void LeaveAlone(GameObject candy)
        {
            if (leftAlone.Count > 32) leftAlone.Clear();
            leftAlone[candy] = Time.time + LeaveAloneSeconds;
        }

        private bool LeftAlone(GameObject candy) => leftAlone.TryGetValue(candy, out float until) && Time.time < until;

        // ---------- Eating ----------

        private void Take(GameObject candy)
        {
            if (!CandyHelmet.Seize(candy)) return;
            swallows.Add(new Swallow { candy = candy, from = candy.transform.position, size = candy.transform.localScale, start = Time.time });
            pausedUntil = Mathf.Max(pausedUntil, Time.time + eatSeconds);
            if (hasEat) animator.SetTrigger(eatTrigger);
        }

        // Into its mouth, shrinking; then it's gone, and it's fatter.
        private void FlySwallows()
        {
            for (int i = swallows.Count - 1; i >= 0; i--)
            {
                var swallow = swallows[i];
                if (swallow.candy == null)
                {
                    swallows.RemoveAt(i);
                    continue;
                }
                float t = Mathf.Clamp01((Time.time - swallow.start) / eatSeconds);
                float k = t * t;
                swallow.candy.transform.position = Vector3.Lerp(swallow.from, Mouth(), k);
                swallow.candy.transform.localScale = swallow.size * Mathf.Lerp(1f, 0.3f, k);
                if (t < 1f) continue;
                Destroy(swallow.candy);
                swallows.RemoveAt(i);
                Gulp();
            }
        }

        private void Gulp()
        {
            Eaten++;
            if (jiggle != null) jiggle.Hit(GulpWobble);
            if (eatSound != null && speaker != null && Time.time >= nextGulpSound)
            {
                nextGulpSound = Time.time + GulpGap;
                speaker.PlayOneShot(eatSound, eatVolume);
            }
        }

        private float Growth => Mathf.Min(Mathf.Max(1f, mostGrowth), 1f + growPerCandy * Eaten);

        // Pops up to its size as it wakes up, then swells a little with each candy.
        private void Grow()
        {
            shownGrowth = Mathf.MoveTowards(shownGrowth, Growth, GrowPerSecond * Time.deltaTime);
            float popped = Mathf.Clamp01((Time.time - bornAt) / PopSeconds);
            transform.localScale = fullScale * (shownGrowth * Mathf.Lerp(PopFrom, 1f, 1f - (1f - popped) * (1f - popped)));
        }

        // ---------- Being hit ----------

        // The player's hits, by the one rule: a fist, a thing they hold or just threw, a shot.
        private void OnCollisionEnter(Collision collision)
        {
            if (phase == Phase.Gone || Time.time < nextHurt) return;
            var hit = EnemyHitDamage.Judge(collision, toughness, hitSettings, out bool byPlayer);
            if (!byPlayer) return;
            if (hit.Damage <= 0f)
            {
                // Too soft to hurt: it only wobbles.
                if (jiggle != null && Time.time >= nextSoftWobble)
                {
                    nextSoftWobble = Time.time + hitCooldown;
                    jiggle.Hit(SoftWobble);
                }
                return;
            }
            var at = collision.contactCount > 0 ? collision.GetContact(0).point : collision.transform.position;
            var settings = hitSettings != null ? hitSettings : HitSettings.Shared;
            if (settings.ShowsEffect(hit, true)) settings.PlayEffect(at, hit);
            HitBuzz.Dealt(collision, hit.Damage);
            bool breaks = health - hit.Damage <= 0f;
            if (breaks) HitBuzz.Killed(collision, hit.Damage);
            TakeDamage(hit.Damage);
        }

        /// <summary>The player's blast (a grenade, a bottle), or a rolling pumpkin running it over.</summary>
        public void Hit(float strength, Vector3 point)
        {
            if (phase == Phase.Gone) return;
            TakeDamage(Mathf.Max(1f, HitSettings.Whole(strength / toughness)));
        }

        private void TakeDamage(float damage)
        {
            health -= damage;
            nextHurt = Time.time + hitCooldown;
            if (health <= 0f)
            {
                Break();
                return;
            }
            if (jiggle != null) jiggle.Hit(HitWobble);
            pausedUntil = Mathf.Max(pausedUntil, Time.time + KnockSeconds + knock.y);
            if (hitSounds.Length > 0 && speaker != null) speaker.PlayOneShot(hitSounds[Random.Range(0, hitSounds.Length)]);
            if (hasHit) animator.SetTrigger(hitTrigger);
            if (head == null) return;
            if (knockback == null && !TryGetComponent(out knockback)) knockback = gameObject.AddComponent<Knockback>();
            knockback.Push(head.position, knock.x, KnockSeconds, KnockHop, knock.y);
        }

        // It bursts and gives back a few of the candies it ate. What's still on its way to its mouth counts as eaten.
        private void Break()
        {
            phase = Phase.Gone;
            foreach (var swallow in swallows)
            {
                if (swallow.candy == null) continue;
                Destroy(swallow.candy);
                Eaten++;
            }
            swallows.Clear();

            var at = Middle();
            float size = Size;
            foreach (var effect in burstEffects)
            {
                if (effect != null) EffectPool.Play(effect, at, burstEffectSize * size);
            }
            if (burstSound != null) AudioSource.PlayClipAtPoint(burstSound, at);
            var stuff = SpawnedStuff();
            var parts = BurstInto(stuff);
            DropCandies(stuff, parts);
            if (exp > 0 && !noReward) Game.Leveling.PlayerLevel.AddExp(exp);
            Destroy(gameObject);
        }

        private Collider[] BurstInto(Transform stuff)
        {
            if (pieces == null) return System.Array.Empty<Collider>();
            var burst = Instantiate(pieces, transform.position, transform.rotation, stuff);
            burst.transform.localScale = Vector3.one * (piecesSize * Size);
            return burst.GetComponentsInChildren<Collider>(true);
        }

        // Out of a ring round it, low over the floor: candies break on a hard bump, so they go through its pieces.
        private void DropCandies(Transform stuff, Collider[] parts)
        {
            if (candies.Length == 0) return;
            int amount = Mathf.Clamp(Mathf.FloorToInt(Eaten * dropShare), fewestCandies, Mathf.Max(fewestCandies, mostCandies));
            float ring = radius * Size + CandyRingGap;
            float turn = Random.Range(0f, 360f);
            for (int i = 0; i < amount; i++)
            {
                var prefab = candies[Random.Range(0, candies.Length)];
                if (prefab == null) continue;
                var outward = Quaternion.Euler(0f, turn + 360f * i / amount, 0f) * Vector3.forward;
                var candy = Instantiate(prefab, transform.position + outward * ring + Vector3.up * CandyHeight,
                    Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), stuff);
                foreach (var part in candy.GetComponentsInChildren<Collider>(true))
                {
                    foreach (var piece in parts)
                    {
                        if (piece != null) Physics.IgnoreCollision(part, piece);
                    }
                }
                if (candy.TryGetComponent<Rigidbody>(out var body))
                    body.linearVelocity = outward * CandyOutward + Vector3.up * candyLaunchSpeed;
            }
        }

        // ---------- Helpers ----------

        private void Laugh()
        {
            if (laughs.Length > 0 && speaker != null) speaker.PlayOneShot(laughs[Random.Range(0, laughs.Length)]);
            // Waking up, the laugh is part of its animation.
            if (hasLaugh && phase != Phase.Waking) animator.SetTrigger(laughTrigger);
        }

        // Rocks from side to side round its foot as it walks, a little hop at each step. An animation does that instead.
        private void Waddle(float speed)
        {
            if (model == null || animator != null) return;
            float amount = Mathf.Clamp01(speed / walkSpeed);
            waddleClock += waddlePerSecond * amount * Time.deltaTime;
            float swing = Mathf.Sin(waddleClock * Mathf.PI);
            model.localRotation = Quaternion.Euler(0f, 0f, swing * waddle.x * amount);
            model.localPosition = modelHome + Vector3.up * (Mathf.Abs(swing) * waddle.y * amount / Size);
        }

        private void TurnTo(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), TurnDegrees * Time.deltaTime);
        }

        private Vector3 HeadFlat() => head != null ? head.position - transform.position : transform.forward;

        private float Size => Mathf.Max(0.01f, transform.lossyScale.y);

        private Vector3 Middle() => transform.position + Vector3.up * (radius * Size);

        private Vector3 Mouth() => mouth != null ? mouth.position : Middle() + transform.forward * (radius * Size * 0.8f);

        private static Transform SpawnedStuff()
        {
            var stuff = FsmVariables.GlobalVariables.FindFsmGameObject(SpawnedStuffVariable);
            return stuff != null && stuff.Value != null ? stuff.Value.transform : null;
        }

        private void OnDestroy()
        {
            // Taken away (a stage's clean-up) with candies on their way to its mouth.
            foreach (var swallow in swallows)
            {
                if (swallow.candy != null) Destroy(swallow.candy);
            }
        }
    }
}

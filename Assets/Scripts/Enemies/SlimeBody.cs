using System;
using System.Collections.Generic;
using Game.Combat;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace Game.Enemies
{
    /// <summary>
    /// The PlayGround's ground slimes (user, 2026-11-07): Slime_Big (what a wrong recipe makes), the round mini bosses
    /// Slime_Big_Blue, Slime_Big_Red and Slime_Big_Yellow, and Slime_Small. Spider Brain is the brain (it chases, takes
    /// its turn, attacks, does its special move); this does the rest.
    /// The look: the user's Giant_Slime model has no rig, so it's moved from here, from the brain's Signalled events: a
    /// jelly spring on Body (squash and stretch), little hops while it moves, a crouch while it winds up, a hop with each
    /// attack (the attack's FSM event Chomp or Slam), and the mouth (the head, Giant_Slime_Below, tips back) opening to
    /// bite or spit.
    /// Its death (the brain's "Died"): it bursts (smoke, a pop and a squelch). Killed by the player (a hit or a blast on
    /// it a moment before, or its own Ticking Bomb), a big one splits into Split Count small ones, once (a slime made by
    /// a split never splits again), and a small one drops a few candies. Killed any other way (a wave cleared, a fall,
    /// the Live Limit, a blast that wasn't the player's, Clear All) it only bursts. The Fairy Shield pops it: no EXP, no
    /// split, no candy.
    /// Tools > 429 Game > PlayGround > Set Up PlayGround Slimes builds the prefabs.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpiderBrain))]
    public class SlimeBody : MonoBehaviour
    {
        [Header("Look")]
        [Tooltip("What squashes, stretches and hops (its pivot at the bottom middle, under the brain's Models).")]
        [SerializeField] private Transform body;
        [Tooltip("The head (Giant_Slime_Below): it tips back round its own X axis, hinged at the back, to open the mouth.")]
        [SerializeField] private Transform head;
        [Tooltip("How far the head tips back with the mouth open, in degrees.")]
        [Range(5f, 80f)]
        [SerializeField] private float mouthOpenAngle = 35f;
        [Tooltip("How stiff and how damped its jelly wobble is.")]
        [Min(1f)]
        [SerializeField] private float stiffness = 160f;
        [Min(0f)]
        [SerializeField] private float damping = 7f;
        [Tooltip("While it moves it bobs along in little hops: how high (metres at size 1)...")]
        [Min(0f)]
        [SerializeField] private float bobHeight = 0.06f;
        [Tooltip("...and how many hops a second.")]
        [Min(0.1f)]
        [SerializeField] private float bobsPerSecond = 3f;
        [Tooltip("How much it crouches while it winds up an attack (0.25 = a quarter lower, and wider).")]
        [Range(0f, 0.5f)]
        [SerializeField] private float crouch = 0.25f;
        [Tooltip("Its bite (an attack whose FSM Event is Chomp) is a little hop: how high, in metres at size 1...")]
        [Min(0f)]
        [SerializeField] private float chompHop = 0.12f;
        [Tooltip("...from when until when, in seconds after the attack starts (match the attack's Hit From and Hit Until).")]
        [SerializeField] private Vector2 chompHopTime = new(0.15f, 0.5f);
        [Tooltip("Its body slam (an attack whose FSM Event is Slam) is a big hop at the player: how high, in metres at " +
                 "size 1...")]
        [Min(0f)]
        [SerializeField] private float slamHop = 0.4f;
        [Tooltip("...from when until when, in seconds after the attack starts.")]
        [SerializeField] private Vector2 slamHopTime = new(0.2f, 0.6f);

        [Header("Splitting (big ones)")]
        [Tooltip("What it splits into when the player kills it (Slime_Small). Empty: it doesn't split.")]
        [SerializeField] private GameObject splitInto;
        [Tooltip("How many it splits into (user, 2026-11-07: exactly 3).")]
        [Min(0)]
        [SerializeField] private int splitCount = 3;
        [Tooltip("How far from its middle they land, in metres times its size.")]
        [Min(0f)]
        [SerializeField] private float splitRing = 0.5f;
        [Tooltip("How far they're flung on out of the splash, in metres.")]
        [Min(0f)]
        [SerializeField] private float splitFling = 0.6f;

        [Header("Candies (small ones)")]
        [Tooltip("The candies it can drop when the player kills it (Candy_1..8, one at random each). Empty: none.")]
        [SerializeField] private GameObject[] candies = Array.Empty<GameObject>();
        [Tooltip("How many, random between (both included).")]
        [SerializeField] private Vector2Int candyCount = new(0, 2);
        [Tooltip("How fast they're tossed up, in m/s (gently: a candy breaks on a hard landing).")]
        [Min(0f)]
        [SerializeField] private float candyLaunchSpeed = 2.2f;

        [Header("Bursting")]
        [Tooltip("Played where it bursts (Effect Pool), this big times its size.")]
        [SerializeField] private GameObject[] burstEffects = Array.Empty<GameObject>();
        [Min(0f)]
        [SerializeField] private float burstEffectSize = 0.5f;
        [Tooltip("One of these at random as it bursts (a pop)...")]
        [SerializeField] private AudioClip[] popSounds = Array.Empty<AudioClip>();
        [Tooltip("...and one of these with it (a wet squelch). A big slime sounds lower, a small one higher.")]
        [SerializeField] private AudioClip[] squelchSounds = Array.Empty<AudioClip>();
        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.9f;
        [Tooltip("Touching something with this tag pops it (the Fairy Shield), as it pops the bugs.")]
        [SerializeField] private string shieldTag = "Fairy_Shield";

        private const string SpawnedStuffVariable = "CurrentlySpawnedStuffs";
        // A hit, a blast or its own bomb this many seconds before its death makes the death the player's doing.
        private const float EarnedWindow = 0.5f;
        private const float CandyHeight = 0.3f;
        private const float CandyOutward = 1f;

        private static readonly List<SlimeBody> all = new();
        private static readonly List<object> found = new();
        // Until then (Clear All) no slime splits or drops candy, whatever killed it.
        private static float noSplitUntil = -1f;

        private SpiderBrain brain;
        private EnemyHitDamage hits;
        private NavMeshAgent agent;
        // The waves (spawners) it counts in, so what it splits into joins them.
        private readonly List<object> waves = new();
        private bool madeBySplit;
        private bool done;
        private float lastHurtAt = -10f;

        private Vector3 bodyScale = Vector3.one;
        private Vector3 bodyHome;
        private Quaternion headRest = Quaternion.identity;
        // The jelly: + squashed, - stretched; what it springs back to (a crouch, a held stretch), and until when.
        private float squash;
        private float squashSpeed;
        private float squashTarget;
        private float holdUntil;
        private float mouthNow;
        private float openUntil;
        private float bobPhase;
        private float bobAmount;
        private float breathSeed;
        // An attack's hop: when it starts and ends (start -1: none), and how high.
        private float hopStart = -1f;
        private float hopEnd;
        private float hopHeight;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            all.Clear();
            found.Clear();
            noSplitUntil = -1f;
        }

        /// <summary>How big it is (its scale).</summary>
        public float Size => Mathf.Max(0.01f, transform.lossyScale.y);

        /// <summary>Made by a big one's split: it never splits itself.</summary>
        public bool MadeBySplitting => madeBySplit;

        /// <summary>
        /// Pops every live slime (no EXP, no split, no candy): the PlayGround's clear after a seed. Slimes already dying
        /// (e.g. a wave just cleared) die their own way, and none of them splits or drops candy for a second.
        /// </summary>
        public static void ClearAll()
        {
            noSplitUntil = Time.time + 1f;
            foreach (var slime in all.ToArray())
            {
                if (slime == null || slime.done) continue;
                if (slime.hits != null && slime.hits.IsDead) continue;
                slime.Pop();
            }
        }

        /// <summary>
        /// Called on a slime just made by a big one's split (straight after Instantiate): it never splits again, whatever
        /// its prefab says, and wears its parent's skin (Skin; none: its own).
        /// </summary>
        public void MadeBySplit(Material skin)
        {
            madeBySplit = true;
            if (skin == null) return;
            var under = body != null ? body : transform;
            foreach (var look in under.GetComponentsInChildren<Renderer>(true))
            {
                if (look is ParticleSystemRenderer) continue;
                var mats = look.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = skin;
                look.sharedMaterials = mats;
            }
        }

        private void Awake()
        {
            brain = GetComponent<SpiderBrain>();
            TryGetComponent(out hits);
            TryGetComponent(out agent);
            if (body == null) body = transform.Find("Model/Jiggle/Body");
            if (body != null)
            {
                bodyScale = body.localScale;
                bodyHome = body.localPosition;
            }
            if (head != null) headRest = head.localRotation;
            breathSeed = Random.Range(0f, 10f);
        }

        private void OnEnable()
        {
            all.Add(this);
            if (brain != null) brain.Signalled += OnSignal;
            EnemyHitDamage.AnyHit += OnAnyHit;
        }

        private void OnDisable()
        {
            all.Remove(this);
            if (brain != null) brain.Signalled -= OnSignal;
            EnemyHitDamage.AnyHit -= OnAnyHit;
        }

        // A spawner adds it to its wave straight after Instantiate, so by now it's known.
        private void Start()
        {
            RememberWaves();
        }

        // Adds the waves it counts in now (never forgets one: a wave drops its dead before the split happens).
        private void RememberWaves()
        {
            EnemyWave.SpawnersOf(gameObject, found);
            foreach (var spawner in found)
            {
                if (!waves.Contains(spawner)) waves.Add(spawner);
            }
            found.Clear();
        }

        private void OnAnyHit(EnemyHitDamage enemy, HitResult hit)
        {
            if (enemy == null || enemy != hits) return;
            lastHurtAt = Time.time;
            RememberWaves();
        }

        private void OnSignal(SpiderBrain from, string what)
        {
            switch (what)
            {
                case "Prepare":
                    Hold(crouch, 1.5f);
                    break;
                case "SpecialWindUp":
                    Hold(crouch * 1.4f, 2f);
                    OpenFor(1f);
                    break;
                case "Attack":
                    Hold(0f, 0f);
                    Kick(-0.5f);
                    OpenFor(0.45f);
                    break;
                case "Chomp":
                    HopAt(chompHop, chompHopTime);
                    break;
                case "Slam":
                    HopAt(slamHop, slamHopTime);
                    break;
                case "Bite":
                case "Missed":
                    openUntil = Mathf.Min(openUntil, Time.time + 0.05f);
                    Kick(0.35f);
                    break;
                case "Blocked":
                case "Flinch":
                case "WeakFlinch":
                    hopStart = -1f;
                    Hold(0f, 0f);
                    Kick(0.4f);
                    break;
                case "WebShot":
                    OpenFor(0.8f);
                    Kick(-0.2f);
                    break;
                case "Charge":
                    // Stretched forward the whole run, mouth open.
                    Hold(-0.2f, 1.5f);
                    OpenFor(1f);
                    break;
                case "SpecialHit":
                    Kick(0.5f);
                    break;
                case "Noticed":
                    Kick(-0.35f);
                    break;
                case "BombBlast":
                    // Its own bomb kills it next: that counts as the player's doing (it splits out of the blast).
                    lastHurtAt = Time.time;
                    break;
                case "Died":
                    Died();
                    break;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (done || string.IsNullOrEmpty(shieldTag) || other == null || !other.CompareTag(shieldTag)) return;
            Pop();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || body == null) return;

            // The jelly: a spring back to its shape (or to its crouch while it holds one).
            if (Time.time >= holdUntil) squashTarget = 0f;
            squashSpeed += (-stiffness * (squash - squashTarget) - damping * squashSpeed) * dt;
            squash = Mathf.Clamp(squash + squashSpeed * dt, -0.6f, 0.6f);

            float lift = 0f, stretch = 0f;
            // Little hops while it moves; each landing squashes it a little.
            bool hopping = hopStart >= 0f;
            bool moving = !hopping && agent != null && agent.isActiveAndEnabled && FlatSpeed() > 0.1f;
            bobAmount = Mathf.MoveTowards(bobAmount, moving ? 1f : 0f, dt * 4f);
            if (bobAmount > 0f)
            {
                float before = bobPhase;
                bobPhase += dt * bobsPerSecond;
                if (Mathf.Floor(bobPhase) != Mathf.Floor(before)) Kick(0.12f * bobAmount);
                float arc = Mathf.Sin(bobPhase % 1f * Mathf.PI);
                lift += bobHeight * arc * bobAmount;
                stretch -= 0.08f * arc * bobAmount;
            }
            // An attack's hop: up stretched along an arc, down with a squash.
            if (hopping && Time.time >= hopStart)
            {
                float k = (Time.time - hopStart) / Mathf.Max(0.05f, hopEnd - hopStart);
                if (k >= 1f)
                {
                    hopStart = -1f;
                    Kick(0.5f);
                }
                else
                {
                    lift += 4f * hopHeight * k * (1f - k);
                    stretch -= 0.2f * (1f - Mathf.Abs(2f * k - 1f));
                }
            }

            float breath = Mathf.Sin((Time.time + breathSeed) * 2.1f) * 0.03f;
            float s = squash + stretch;
            float wide = 1f + s * 0.5f + breath * 0.5f;
            float tall = Mathf.Max(0.2f, 1f - s - breath * 0.3f);
            body.localScale = new Vector3(bodyScale.x * wide, bodyScale.y * tall, bodyScale.z * wide);
            body.localPosition = bodyHome + Vector3.up * lift;

            // The mouth: the head tips back round its hinge.
            bool open = Time.time < openUntil;
            mouthNow = Mathf.MoveTowards(mouthNow, open ? 1f : 0f, dt / (open ? 0.12f : 0.25f));
            if (head != null)
            {
                float ease = mouthNow * mouthNow * (3f - 2f * mouthNow);
                head.localRotation = headRest * Quaternion.Euler(-mouthOpenAngle * ease, 0f, 0f);
            }
        }

        private float FlatSpeed()
        {
            var v = agent.velocity;
            v.y = 0f;
            return v.magnitude;
        }

        // Gives its jelly a shove: + squashes it, - stretches it.
        private void Kick(float amount) => squashSpeed += amount * 12f;

        // Springs to this shape (+ squashed, - stretched) and holds it for this many seconds, or until the next move.
        private void Hold(float shape, float seconds)
        {
            squashTarget = shape;
            holdUntil = Time.time + seconds;
        }

        private void OpenFor(float seconds) => openUntil = Mathf.Max(openUntil, Time.time + seconds);

        private void HopAt(float height, Vector2 time)
        {
            if (height <= 0f) return;
            hopHeight = height;
            hopStart = Time.time + Mathf.Max(0f, time.x);
            hopEnd = Time.time + Mathf.Max(time.x + 0.05f, time.y);
        }

        // ---- Death ----

        // The brain's "Died": it bursts; the player's kill also splits a big one and gets candy from a small one.
        private void Died()
        {
            if (done) return;
            done = true;
            bool earned = hits != null && hits.IsDead && !hits.noReward && Time.time - lastHurtAt <= EarnedWindow &&
                          Time.time >= noSplitUntil && gameObject.scene.isLoaded;
            Burst();
            if (earned && !madeBySplit && splitInto != null && splitCount > 0) Split();
            if (earned && candies != null && candies.Length > 0)
                DropCandies(Random.Range(Mathf.Max(0, candyCount.x), Mathf.Max(candyCount.x, candyCount.y) + 1));
            // Gone from sight now, destroyed a moment later: Enemy Hit Damage gets one more Update to see the health at
            // 0 and pay its EXP (a wave cleared mid frame, after its Update had already run).
            Hide();
            Destroy(gameObject, 0.1f);
        }

        // Out of sight and out of the way: no looks, no colliders, no walking.
        private void Hide()
        {
            foreach (var look in GetComponentsInChildren<Renderer>(true)) look.enabled = false;
            foreach (var part in GetComponentsInChildren<Collider>(true)) part.enabled = false;
            if (agent != null && agent.enabled) agent.enabled = false;
        }

        // Popped (the Fairy Shield, Clear All): it bursts, and the player gets nothing for it.
        private void Pop()
        {
            if (done) return;
            done = true;
            Game.Leveling.PlayerLevel.NoRewardFor(gameObject);
            Burst();
            Destroy(gameObject);
        }

        private Vector3 Middle => hits != null ? hits.BodyCenter : transform.position + Vector3.up * (0.4f * Size);

        private void Burst()
        {
            var at = Middle;
            float size = Size;
            foreach (var effect in burstEffects)
            {
                if (effect != null) EffectPool.Play(effect, at, burstEffectSize * size);
            }
            // A big one sounds lower, a small one higher.
            float pitch = Mathf.Clamp(1f / Mathf.Sqrt(size), 0.6f, 1.5f);
            var range = new Vector2(0.92f * pitch, 1.05f * pitch);
            SlimeKit.Play(popSounds, at, volume, range);
            SlimeKit.Play(squelchSounds, at, volume, range);
        }

        // Exactly Split Count small ones round where it was, on the NavMesh, flung out of the splash; they count in its
        // waves and come for the player at once. All of them, even with the Enemy Limit full (a small one never splits).
        private void Split()
        {
            var at = transform.position;
            var parent = SpawnedStuff();
            if (parent == null) parent = transform.parent;
            float size = Size;
            var skin = Skin();
            var filter = new NavMeshQueryFilter
            {
                agentTypeID = agent != null ? agent.agentTypeID : 0,
                areaMask = NavMesh.AllAreas,
            };
            float turn = Random.Range(0f, 360f);
            for (int i = 0; i < splitCount; i++)
            {
                var way = Quaternion.Euler(0f, turn + i * 360f / splitCount, 0f) * Vector3.forward;
                var spot = at + way * (splitRing * size);
                spot = NavMesh.SamplePosition(spot, out var hit, 1.5f, filter) && !NavMesh.Raycast(at, hit.position, out _, filter)
                    ? hit.position
                    : at;
                var child = Instantiate(splitInto, spot, Quaternion.LookRotation(way), parent);
                if (child.TryGetComponent(out SlimeBody slime)) slime.MadeBySplit(skin);
                // It counts at once (the answer doesn't matter: the split always happens).
                EnemyLimit.TryTakeSlot();
                foreach (var spawner in waves) EnemyWave.Add(spawner, child);
                if (child.TryGetComponent(out SpiderBrain childBrain))
                {
                    childBrain.HoldOff(0.4f);
                    childBrain.ComeAtOnce();
                }
                if (splitFling > 0f)
                {
                    if (!child.TryGetComponent(out Knockback fling)) fling = child.AddComponent<Knockback>();
                    fling.Push(at, splitFling, 0.3f, 0.35f, 0.1f);
                }
            }
        }

        // Its skin (the material on its model), for the small ones it splits into.
        private Material Skin()
        {
            var under = body != null ? body : transform;
            foreach (var look in under.GetComponentsInChildren<Renderer>(true))
            {
                if (look is ParticleSystemRenderer || look.sharedMaterial == null) continue;
                return look.sharedMaterial;
            }
            return null;
        }

        // Out of a ring round it, low and slow (candies break on a hard landing), into CurrentlySpawnedStuffs.
        private void DropCandies(int amount)
        {
            if (amount <= 0) return;
            var parent = SpawnedStuff();
            float ring = 0.35f * Size + 0.1f;
            float turn = Random.Range(0f, 360f);
            for (int i = 0; i < amount; i++)
            {
                var prefab = candies[Random.Range(0, candies.Length)];
                if (prefab == null) continue;
                var outward = Quaternion.Euler(0f, turn + 360f * i / amount, 0f) * Vector3.forward;
                var candy = Instantiate(prefab, transform.position + outward * ring + Vector3.up * CandyHeight,
                    Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), parent);
                if (candy.TryGetComponent<Rigidbody>(out var thrown) && !thrown.isKinematic)
                    thrown.linearVelocity = outward * CandyOutward + Vector3.up * candyLaunchSpeed;
            }
        }

        private static Transform SpawnedStuff()
        {
            var holder = HutongGames.PlayMaker.FsmVariables.GlobalVariables.FindFsmGameObject(SpawnedStuffVariable);
            return holder != null && holder.Value != null ? holder.Value.transform : null;
        }
    }
}

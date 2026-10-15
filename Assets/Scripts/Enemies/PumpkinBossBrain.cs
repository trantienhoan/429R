using System.Collections.Generic;
using Game.Combat;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.AI;
using FsmFloat = HutongGames.PlayMaker.FsmFloat;
using FsmVariables = HutongGames.PlayMaker.FsmVariables;

namespace Game.Enemies
{
    /// <summary>
    /// The Pumpkin boss (pumpkin_boss_Z). It laughs non-stop from the moment it appears, and breaks the room it comes
    /// up in (the bedroom or the living room). It starts as just its head sitting on the floor; every hit the player
    /// lands raises it one pumpkin out of the ground, and after the third one its arms and body come out, it slams
    /// both arms down and then chases the player, punching and slamming; hurt a lot very fast, it stands there dizzy
    /// for a few seconds. Left alone it wakes itself up once, after Wake Up Seconds. Below True Form Below of its
    /// health it climbs out on two legs,
    /// grows, and runs with more attacks: a pumpkin full of bugs thrown at the player, a jump with a body slam, bugs
    /// vomited out, and a rare cyclone spin that leaves it dizzy; hits never stop it. Its hits throw the player
    /// (Player Launch) and only hurt when they really land. Every health it loses drops a pumpkin to throw back at it.
    /// When it dies it shrinks back into one big pumpkin head that bursts like the other pumpkins, and sets
    /// GAMESTAGES' BOSS flag.
    /// Hits on it come from Enemy Hit Damage, which lowers the "health" of its (switched off) Health FSM.
    /// Tools > 429 Game > Enemies > Set Up Pumpkin Boss builds it.
    /// </summary>
    [DisallowMultipleComponent]
    public class PumpkinBossBrain : MonoBehaviour
    {
        private enum Phase { Head, Rising, Upper, Transforming, True, Dying }

        private enum Act { None, Taunt, PunchL, PunchR, Slam, Throw, JumpSlam, Vomit, Cyclone, Dizzy }

        [Header("Parts (set by the setup menu)")]
        [SerializeField] private Animator animator;
        [Tooltip("The child that holds the model: moved up and down for how far the boss is out of the ground, and scaled.")]
        [SerializeField] private Transform model;
        [SerializeField] private Transform head;
        [SerializeField] private Transform jaw;
        [SerializeField] private Transform handL;
        [SerializeField] private Transform handR;

        [Header("Health")]
        [SerializeField] private string healthFsm = "Health";
        [SerializeField] private string healthVariable = "health";
        [Tooltip("It goes true form below this share of its health.")]
        [Range(0.05f, 0.95f)]
        [SerializeField] private float trueFormBelow = 0.4f;

        [Header("Waking up")]
        [Tooltip("Hits it takes as a head before its arms and body come out.")]
        [Min(1)]
        [SerializeField] private int revealHits = 3;
        [Tooltip("With no hit for this long as a head, it wakes itself up (once).")]
        [Min(1f)]
        [SerializeField] private float wakeUpSeconds = 33f;
        [Tooltip("How far the model sits under the floor (model units, before its scale): head only, then one more " +
                 "pumpkin out per hit; the last one is the upper body, with the vines under its bottom pumpkin kept " +
                 "under the floor.")]
        [SerializeField] private float[] buried = { 1.72f, 1.41f, 1.22f, 1.06f };

        [Header("Size and speed")]
        [SerializeField] private float headScale = 1.7f;
        [SerializeField] private float upperScale = 1.7f;
        [SerializeField] private float trueScale = 2.1f;
        [Tooltip("Running speed in true form, in m/s. The upper body moves at Upper Speed of it.")]
        [Min(0.1f)]
        [SerializeField] private float trueSpeed = 5f;
        [Range(0.1f, 1f)]
        [SerializeField] private float upperSpeed = 0.6f;

        [Header("Getting dizzy (upper body)")]
        [Tooltip("It's stunned once it loses this share of its health in quick succession (0.0294 of 429 = about 13 " +
                 "health). What it has lost fades away by Dizzy Fade Per Second, so slow hits never add up.")]
        [Range(0.02f, 1f)]
        [SerializeField] private float dizzyAfterDamage = 0.0294f;
        [Range(0f, 0.5f)]
        [SerializeField] private float dizzyFadePerSecond = 0.0029f;
        [Tooltip("How long it stands there dizzy (being_hit), in seconds.")]
        [Min(0.5f)]
        [SerializeField] private float dizzySeconds = 3f;
        [Tooltip("After coming round, it can't be made dizzy again for this many seconds.")]
        [Min(0f)]
        [SerializeField] private float dizzyCooldown = 20f;

        [Header("Hitting the player")]
        [Tooltip("Sent to the player's PlayerHealth FSM when an upper-body hit lands (35-45) / a true-form hit (55-70).")]
        [SerializeField] private string upperHitEvent = "PumpkinBossHit";
        [SerializeField] private string trueHitEvent = "PumpkinBossHitHeavy";
        [SerializeField] private string playerHealthFsm = "PlayerHealth";
        [Tooltip("Throws: x = how high, y = how far, in metres.")]
        [SerializeField] private Vector2 slamLaunch = new(3.2f, 2.5f);
        [SerializeField] private Vector2 punchLaunch = new(1.0f, 3.0f);
        [SerializeField] private Vector2 jumpSlamLaunch = new(3.6f, 3.0f);
        [Tooltip("The cyclone is its special attack: it throws the player highest and furthest of all.")]
        [SerializeField] private Vector2 cycloneLaunch = new(4.5f, 7f);
        [Tooltip("How long the whole cyclone lasts, in seconds: the length of its clip (wind-up, spin, hard stop).")]
        [SerializeField] private float cycloneSeconds = 3f;
        [Tooltip("The cyclone is rare: this is the chance each time it picks an attack with the player within 6 m, " +
                 "and it never comes again within Cyclone Cooldown seconds (nor in the first half of that after it " +
                 "turns into its true form).")]
        [Range(0f, 1f)]
        [SerializeField] private float cycloneChance = 0.2f;
        [Min(0f)]
        [SerializeField] private float cycloneCooldown = 15f;
        [Tooltip("After its cyclone it's spun itself dizzy (dizzy_true) for this many seconds: the player's chance " +
                 "to hit back. 0 = never dizzy.")]
        [Min(0f)]
        [SerializeField] private float cycloneDizzySeconds = 3f;

        [Header("Bugs")]
        [Tooltip("Small bugs it vomits and that come out of its thrown pumpkin; one at random each time.")]
        [SerializeField] private GameObject[] bugs = System.Array.Empty<GameObject>();
        [Tooltip("Bugs per vomit. It only vomits in its true form, so always below True Form Below (40%) of its health.")]
        [Min(0)] [SerializeField] private int vomitBugs = 6;
        [Tooltip("As it turns into its true form it calls its bugs: a vomit of this many, right away (if the game isn't " +
                 "already full of enemies). 0 = none.")]
        [Min(0)] [SerializeField] private int trueFormVomitBugs = 6;
        [Tooltip("The pumpkin full of bugs it throws (Bug Pumpkin).")]
        [SerializeField] private GameObject bugPumpkin;

        [Header("Pumpkins to throw back")]
        [Tooltip("Dropped for every health it loses.")]
        [SerializeField] private GameObject pumpkinDrop;
        [Min(1)] [SerializeField] private int maxDropsAround = 10;

        [Header("Dying")]
        [Tooltip("What its big head bursts into (the pieces of a broken pumpkin).")]
        [SerializeField] private GameObject burstInto;
        [SerializeField] private GameObject smoke;
        [SerializeField] private string gameStagesBool = "BOSS";
        [SerializeField] private string gameStagesEvent = "Win";

        [Header("Breaking the room")]
        [Tooltip("GAMESTAGES' global variables holding the rooms. When it appears it breaks whichever is standing: the " +
                 "walls fall away and the room is removed (there's ground under them).")]
        [SerializeField] private string[] roomVariables = { "Bedroom_Base", "Livingroom_Base" };
        [Tooltip("Seconds from the walls starting to fall until the room is removed (the fall-away clips last 3.3 s).")]
        [Min(0f)]
        [SerializeField] private float roomGoneAfter = 3.5f;

        [Header("Sounds")]
        [Tooltip("It laughs non-stop until it dies: one of these after another, never the same twice in a row.")]
        [SerializeField] private AudioClip[] laughs = System.Array.Empty<AudioClip>();
        [Tooltip("Seconds of quiet between two laughs.")]
        [Min(0f)]
        [SerializeField] private float laughGap = 0.1f;
        [Range(0f, 1f)]
        [SerializeField] private float laughVolume = 1f;
        [SerializeField] private AudioClip attackSound;
        [SerializeField] private AudioClip hurtSound;
        [SerializeField] private AudioClip roarSound;

        // Animator states and their timing (seconds): when the hit lands, when the move is over.
        private const string Laugh = "laugh", GetHit = "gethit", Come = "come", Move = "move", Slam = "attack",
            Clap = "clap", BeingHit = "being_hit", PunchRState = "punch_R", PunchLState = "punch_L",
            TransformState = "transform", Run = "run", IdleTrue = "idle_true", ThrowState = "throw", JumpSlamState = "jump_slam",
            VomitState = "vomit", CycloneState = "cyclone", DizzyTrue = "dizzy_true", Death = "death";

        // When the vomit clip retches (seconds in: frames 38, 55, 76 and 84 at 60 fps); the bugs come up then.
        private static readonly float[] VomitRetches = { 0.62f, 0.90f, 1.25f, 1.38f };

        // The cyclone clip winds up until 0.5 s, spins seven times until 2.5 s, then stops dead.
        private const float CycloneSpinFrom = 0.5f, CycloneSpinTo = 2.5f;

        private readonly List<GameObject> drops = new();
        private readonly Dictionary<Transform, Vector3> fistTracked = new();
        private readonly List<PlayMakerFSM> fsmBuffer = new();
        private NavMeshAgent agent;
        private AudioSource speaker;
        private AudioSource laughSpeaker;
        private int lastLaugh = -1;
        private float nextLaugh;
        private bool actImpact;
        private FsmFloat health;
        private float maxHealth;
        private float lastHealth;
        private Phase phase = Phase.Head;
        private Act act = Act.None;
        private float actStarted;
        private float actUntil;
        private bool actHit;
        private float nextAct;
        private float nextFlinch;
        private float daze;
        private float nextDizzy;
        private int hitsTaken;
        private float lastHitAt;
        private bool wokeItself;
        private float phaseUntil;
        private float targetBuried;
        private float targetScale;
        private Vector3 jumpFrom, jumpTo;
        private float nextCyclone;
        private int bugsLeft;
        private int retchesDone;
        private string playing = "";
        private XROrigin player;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            speaker = GetComponent<AudioSource>();
            // Its laughing has its own speaker, so yelps and roars play over it instead of cutting it off.
            if (speaker != null)
            {
                laughSpeaker = gameObject.AddComponent<AudioSource>();
                laughSpeaker.playOnAwake = false;
                laughSpeaker.outputAudioMixerGroup = speaker.outputAudioMixerGroup;
                laughSpeaker.spatialBlend = speaker.spatialBlend;
                laughSpeaker.rolloffMode = speaker.rolloffMode;
                laughSpeaker.minDistance = speaker.minDistance;
                laughSpeaker.maxDistance = speaker.maxDistance;
                laughSpeaker.priority = speaker.priority;
                laughSpeaker.dopplerLevel = speaker.dopplerLevel;
                laughSpeaker.spread = speaker.spread;
            }
            foreach (var fsm in GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName != healthFsm) continue;
                if (!fsm.Fsm.Initialized) fsm.Fsm.Init(fsm);
                health = fsm.FsmVariables.FindFsmFloat(healthVariable);
            }
        }

        private void Start()
        {
            maxHealth = lastHealth = health != null ? Mathf.Max(1f, health.Value) : 429f;
            targetBuried = buried.Length > 0 ? buried[0] : 0f;
            targetScale = headScale;
            if (model != null) { model.localScale = Vector3.one * headScale * 0.1f; SetBuried(targetBuried + 1f, model.localScale.x); }
            lastHitAt = Time.time;
            Stop();
            PlayState(Laugh);
            LaughNow();
            BreakRooms();
            EnemyHitDamage.AnyHit += OnAnyHit;
        }

        private void OnDestroy() => EnemyHitDamage.AnyHit -= OnAnyHit;

        private void OnAnyHit(EnemyHitDamage enemy, HitResult result)
        {
            if (enemy == null || enemy.gameObject != gameObject || phase == Phase.Dying) return;
            lastHitAt = Time.time;
            if (phase == Phase.Head)
            {
                hitsTaken++;
                PlayState(GetHit, true);
                PlaySound(hurtSound);
                Puff();
                int step = Mathf.Min(hitsTaken, buried.Length - 1);
                targetBuried = buried[step];
                if (hitsTaken >= revealHits) StartRising();
                return;
            }
            // Hits never stop it: between bigger moves it only yelps now and then. It reels only when dizzy (the upper
            // body from too much damage, see TrackHealth; the true form after its own cyclone).
            if (act != Act.None && act != Act.Taunt && act != Act.Dizzy) return;
            if (Time.time < nextFlinch || phase == Phase.Transforming || phase == Phase.Rising) return;
            nextFlinch = Time.time + 1.8f;
            PlaySound(hurtSound);
        }

        private void Update()
        {
            KeepLaughing();
            if (!FindPlayer()) return;
            TrackHealth();
            UpdateModel();
            switch (phase)
            {
                case Phase.Head: UpdateHead(); break;
                case Phase.Rising: if (Time.time >= phaseUntil) EnterUpper(); break;
                case Phase.Upper: UpdateFighting(false); break;
                case Phase.Transforming: if (Time.time >= phaseUntil) EnterTrue(); break;
                case Phase.True: UpdateFighting(true); break;
                case Phase.Dying: UpdateDying(); break;
            }
        }

        // ---------- health ----------

        private void TrackHealth()
        {
            if (health == null || phase == Phase.Dying) return;
            float now = health.Value;
            daze = Mathf.Max(0f, daze - dizzyFadePerSecond * Time.deltaTime);
            if (now < lastHealth)
            {
                int lost = Mathf.Max(1, Mathf.RoundToInt(lastHealth - now));
                for (int i = 0; i < lost; i++) DropPumpkin();
                if (act != Act.Dizzy && Time.time >= nextDizzy) daze += (lastHealth - now) / maxHealth;
            }
            lastHealth = now;
            if (now <= 0f) { StartDying(); return; }
            if (phase != Phase.Upper) return;
            if (now <= maxHealth * trueFormBelow && act == Act.None) StartTransform();
            // Too much damage too fast makes it dizzy, but only between moves: a punch or slam under way always finishes.
            else if (daze >= dizzyAfterDamage && act == Act.None) StartDizzy(BeingHit, dizzySeconds);
        }

        // Whatever it was doing, it stands there swaying, arms hanging, for a few seconds: being_hit for the upper body
        // (too much damage too fast), dizzy_true for the true form (spun itself dizzy with its cyclone).
        private void StartDizzy(string state, float seconds)
        {
            daze = 0f;
            Begin(Act.Dizzy, state, seconds);
            PlaySound(hurtSound);
            Puff();
        }

        private void DropPumpkin()
        {
            drops.RemoveAll(d => d == null);
            if (pumpkinDrop == null || drops.Count >= maxDropsAround) return;
            var around = Random.insideUnitCircle.normalized * Random.Range(1f, 2.2f);
            var at = transform.position + new Vector3(around.x, 1.6f, around.y);
            var drop = Instantiate(pumpkinDrop, at, Random.rotation, transform.parent);
            if (drop.TryGetComponent(out Rigidbody body))
                body.linearVelocity = new Vector3(around.x, 2.5f, around.y) * 1.2f;
            drops.Add(drop);
        }

        // ---------- head phase ----------

        private void UpdateHead()
        {
            Face(player.Camera.transform.position, 90f);
            if (!wokeItself && Time.time - lastHitAt >= wakeUpSeconds)
            {
                // Bored of waiting: it punches itself awake, once.
                wokeItself = true;
                PlayState(GetHit, true);
                LaughNow();
                Puff();
                StartRising();
                return;
            }
            if (playing == GetHit && Time.time >= actUntil) PlayState(Laugh);
        }

        private void StartRising()
        {
            phase = Phase.Rising;
            targetBuried = buried[buried.Length - 1];
            targetScale = upperScale;
            PlayState(Come, true);
            LaughNow();
            phaseUntil = Time.time + 2.3f;
        }

        // Out of the ground it comes out swinging: a two-arm slam on the spot first, then the chase.
        private void EnterUpper()
        {
            phase = Phase.Upper;
            // Hits it took as a head or while rising don't count towards its first dizzy spell.
            daze = 0f;
            agent.speed = trueSpeed * upperSpeed;
            Begin(Act.Slam, Slam, 2.75f);
            PlaySound(attackSound);
        }

        // ---------- fighting ----------

        private void UpdateFighting(bool trueForm)
        {
            if (act != Act.None) { UpdateAct(trueForm); return; }
            var target = PlayerFloor();
            float distance = Flat(target - transform.position);
            float scale = model != null ? model.localScale.x : 1f;
            // The upper body's rubber-arm punch reaches far, so it stops well away and punches.
            float reach = trueForm ? 3.2f : 2.6f * scale;
            if (Time.time >= nextAct && ChooseAttack(trueForm, distance)) return;
            if (distance > reach * 0.85f)
            {
                agent.isStopped = false;
                agent.stoppingDistance = reach * 0.6f;
                agent.SetDestination(target);
                PlayState(trueForm ? Run : Move);
            }
            else
            {
                Stop();
                Face(target, 240f);
                PlayState(trueForm ? IdleTrue : Move);
            }
        }

        private bool ChooseAttack(bool trueForm, float distance)
        {
            float scale = model != null ? model.localScale.x : 1f;
            if (!trueForm)
            {
                // Long rubber-arm punches from afar; the two-arm slam when the player is close.
                if (distance > 3.1f * scale) return false;
                float r = Random.value;
                if (distance <= 1.3f * scale && r < 0.6f) Begin(Act.Slam, Slam, 2.75f);
                else if (Random.value < 0.5f) Begin(Act.PunchR, PunchRState, 1.6f);
                else Begin(Act.PunchL, PunchLState, 1.6f);
                PlaySound(attackSound);
                if (Random.value < 0.15f) nextAct += 0.8f;
                return true;
            }
            // The cyclone is its special: rare, and never twice close together.
            if (distance <= 6f && Time.time >= nextCyclone && Random.value < cycloneChance)
            {
                StartCyclone();
                return true;
            }
            // A vomit while the game is already full of enemies would bring up nothing: then it slams or throws instead.
            bool canVomit = CanVomit();
            if (distance <= 3.2f)
            {
                if (!canVomit || Random.value < 0.7f) StartJumpSlam(); else StartVomit(vomitBugs);
                return true;
            }
            if (distance <= 14f)
            {
                float r = Random.value;
                if (r < 0.38f && distance <= 9f) StartJumpSlam();
                else if (r < 0.72f || !canVomit) { Begin(Act.Throw, ThrowState, 1.5f); PlaySound(attackSound); }
                else StartVomit(vomitBugs);
                return true;
            }
            return false;
        }

        private void Begin(Act what, string state, float seconds)
        {
            fistTracked.Clear();
            act = what;
            actStarted = Time.time;
            actUntil = Time.time + seconds;
            actHit = false;
            actImpact = false;
            Stop();
            PlayState(state, true);
        }

        private void UpdateAct(bool trueForm)
        {
            float t = Time.time - actStarted;
            var target = PlayerFloor();
            switch (act)
            {
                case Act.PunchL:
                case Act.PunchR:
                    // It charges (aiming) and lets fly at about 0.8 s; the stretched fist flies out to its full reach.
                    if (t < 0.7f) Face(target, 300f);
                    if (t >= 0.76f && t <= 1.08f) TryHit(act == Act.PunchR ? handR : handL, 0.32f, punchLaunch, false);
                    break;
                case Act.Slam:
                    if (t < 1.3f) Face(target, 200f);
                    if (t >= 1.5f && t <= 1.8f) { TryHit(handL, 0.42f, slamLaunch, false); TryHit(handR, 0.42f, slamLaunch, false); }
                    // The fists smash into the floor: dust and a shake.
                    if (!actImpact && t >= 1.58f)
                    {
                        actImpact = true;
                        SendShake("ShakeMed");
                        if (smoke != null)
                        {
                            foreach (var fist in new[] { handL, handR })
                                if (fist != null) Instantiate(smoke, new Vector3(fist.position.x, transform.position.y, fist.position.z), Quaternion.identity, transform.parent);
                        }
                    }
                    break;
                case Act.Throw:
                    Face(target, 260f);
                    if (!actHit && t >= 0.65f) { actHit = true; ThrowBugPumpkin(); }
                    break;
                case Act.JumpSlam:
                    UpdateJumpSlam(t);
                    break;
                case Act.Vomit:
                    if (t < 0.6f) Face(target, 200f);
                    // The bugs come up with its retches, shared out between them.
                    if (retchesDone < VomitRetches.Length && t >= VomitRetches[retchesDone])
                    {
                        int retchesLeft = VomitRetches.Length - retchesDone++;
                        for (int n = Mathf.CeilToInt(bugsLeft / (float)retchesLeft); n > 0; n--) SpitBug();
                        Retch();
                    }
                    break;
                case Act.Cyclone:
                    // Winds up on the spot, spins after the player, then stops dead.
                    bool spinning = t >= CycloneSpinFrom && t <= CycloneSpinTo;
                    if (spinning) { agent.isStopped = false; agent.SetDestination(target); }
                    else { Stop(); if (t < CycloneSpinFrom) Face(target, 200f); }
                    // One hit per cyclone, from the flying rubber fists or the spinning body: it throws the player
                    // clear instead of grinding them down.
                    if (spinning && !actHit)
                    {
                        TryHit(handL, 0.3f, cycloneLaunch, true);
                        TryHit(handR, 0.3f, cycloneLaunch, true);
                        float span = 1.15f * (model != null ? model.localScale.x : 1f);
                        if (!actHit && PlayerNear(transform.position + Vector3.up * 1.6f, span))
                        {
                            actHit = true;
                            HitPlayer(cycloneLaunch, true);
                        }
                    }
                    break;
            }
            if (Time.time < actUntil) return;
            if (act == Act.Cyclone)
            {
                agent.speed = trueSpeed;
                // It spun itself dizzy (the clip ends swaying into dizzy_true).
                if (cycloneDizzySeconds > 0f) { StartDizzy(DizzyTrue, cycloneDizzySeconds); return; }
            }
            if (act == Act.Dizzy) nextDizzy = Time.time + dizzyCooldown;
            act = Act.None;
            nextAct = Time.time + (trueForm ? Random.Range(0.6f, 1.2f) : Random.Range(0.8f, 1.6f));
        }

        private void StartJumpSlam()
        {
            Begin(Act.JumpSlam, JumpSlamState, 2.2f);
            PlaySound(roarSound != null ? roarSound : attackSound);
            jumpFrom = transform.position;
            var to = PlayerFloor();
            var leap = to - jumpFrom;
            leap.y = 0f;
            if (leap.magnitude > 8f) leap = leap.normalized * 8f;
            // Lands just short of where the player stood, so the shockwave reaches them.
            jumpTo = jumpFrom + leap * 0.85f;
            if (NavMesh.SamplePosition(jumpTo, out var hit, 3f, NavMesh.AllAreas)) jumpTo = hit.position;
        }

        private void UpdateJumpSlam(float t)
        {
            if (t < 0.55f) Face(jumpTo, 240f);
            if (t >= 0.6f && t <= 1.32f)
            {
                float k = Mathf.InverseLerp(0.6f, 1.32f, t);
                var p = Vector3.Lerp(jumpFrom, jumpTo, k);
                agent.Warp(p);
            }
            if (!actHit && t >= 1.32f)
            {
                actHit = true;
                Puff();
                SendShake("ShakeBig");
                float radius = 1.7f * (model != null ? model.localScale.x : 1f);
                var flat = PlayerFloor() - transform.position;
                flat.y = 0f;
                if (flat.magnitude <= radius && !PlayerFlying()) HitPlayer(jumpSlamLaunch, true);
            }
        }

        private void StartCyclone()
        {
            Begin(Act.Cyclone, CycloneState, cycloneSeconds);
            agent.speed = trueSpeed * 0.7f;
            nextCyclone = Time.time + cycloneSeconds + cycloneCooldown;
            PlaySound(roarSound != null ? roarSound : attackSound);
        }

        private void StartVomit(int count)
        {
            Begin(Act.Vomit, VomitState, 2.0f);
            bugsLeft = count;
            retchesDone = 0;
        }

        private bool CanVomit() => bugs.Length > 0 && EnemyLimit.AliveCount < EnemyLimit.Max;

        // A puff out of its mouth on every retch, bugs or not.
        private void Retch()
        {
            if (smoke == null) return;
            var mouth = jaw != null ? jaw.position : head != null ? head.position + transform.forward * 0.5f : transform.position;
            Instantiate(smoke, mouth, Quaternion.identity, transform.parent);
        }

        private void SpitBug()
        {
            bugsLeft--;
            if (bugs.Length == 0 || !EnemyLimit.TryTakeSlot()) return;
            var prefab = bugs[Random.Range(0, bugs.Length)];
            if (prefab == null) return;
            var forward = transform.forward;
            var spot = transform.position + forward * Random.Range(1.5f, 3.5f) + transform.right * Random.Range(-1.5f, 1.5f);
            if (!NavMesh.SamplePosition(spot, out var hit, 2.5f, NavMesh.AllAreas)) return;
            var bug = Instantiate(prefab, hit.position, Quaternion.LookRotation(forward), transform.parent);
            // Vomited up to fight: it goes for the player at once instead of looking around first.
            if (bug.TryGetComponent(out SpiderBrain brain)) brain.ComeAtOnce();
        }

        private void ThrowBugPumpkin()
        {
            if (bugPumpkin == null) return;
            var from = handR != null ? handR.position : transform.position + Vector3.up * 3f;
            var to = PlayerFloor() + Vector3.up * 0.3f;
            const float flight = 1.0f;
            var velocity = (to - from) / flight - 0.5f * Physics.gravity * flight;
            var thrown = Instantiate(bugPumpkin, from, Random.rotation, transform.parent);
            if (thrown.TryGetComponent(out BugPumpkin pumpkin)) pumpkin.Launch(velocity, gameObject, bugs);
        }

        // ---------- hitting the player ----------

        private void TryHit(Transform hand, float radius, Vector2 launch, bool trueForm)
        {
            if (actHit || hand == null) return;
            // The fist's own hit sphere when it has one (it grows and flies out with the fist); otherwise round the hand.
            Vector3 point;
            float r;
            if (hand.TryGetComponent(out SphereCollider fist))
            {
                var b = fist.bounds;
                point = b.center;
                r = Mathf.Max(b.extents.x, Mathf.Max(b.extents.y, b.extents.z));
            }
            else
            {
                point = hand.position;
                r = radius * (model != null ? model.localScale.x : 1f);
            }
            // The rubber fist covers metres in a frame or two: everything along its path since last frame counts.
            bool reached = PlayerNear(point, r);
            if (!reached && fistTracked.TryGetValue(hand, out var last))
            {
                for (int i = 1; i < 8 && !reached; i++) reached = PlayerNear(Vector3.Lerp(last, point, i / 8f), r);
            }
            fistTracked[hand] = point;
            if (!reached) return;
            actHit = true;
            HitPlayer(launch, trueForm || phase == Phase.True);
        }

        private void HitPlayer(Vector2 launch, bool heavy)
        {
            var target = player.gameObject;
            target.GetComponents(fsmBuffer);
            foreach (var fsm in fsmBuffer)
            {
                if (fsm.FsmName == playerHealthFsm) fsm.SendEvent(heavy ? trueHitEvent : upperHitEvent);
            }
            PlayerLaunch.Launch(transform.position, launch.x, launch.y);
        }

        // A sphere touching the player's body: a standing cylinder from their feet to their head.
        private bool PlayerNear(Vector3 point, float radius)
        {
            var headPos = player.Camera.transform.position;
            float feet = player.transform.position.y;
            var closest = new Vector3(headPos.x, Mathf.Clamp(point.y, feet, headPos.y), headPos.z);
            return (closest - point).magnitude <= radius + 0.25f;
        }

        private bool PlayerFlying()
        {
            return player.TryGetComponent(out PlayerLaunch launch) && launch.Flying;
        }

        // ---------- transforming and dying ----------

        private void StartTransform()
        {
            phase = Phase.Transforming;
            Stop();
            act = Act.None;
            targetBuried = 0f;
            targetScale = trueScale;
            PlayState(TransformState, true);
            PlaySound(roarSound != null ? roarSound : laughs.Length > 0 ? laughs[0] : null);
            SendShake("ShakeMed");
            phaseUntil = Time.time + 2.0f;
        }

        private void EnterTrue()
        {
            phase = Phase.True;
            agent.speed = trueSpeed;
            nextAct = Time.time + 0.4f;
            nextCyclone = Time.time + cycloneCooldown * 0.5f;
            // Its true form opens by calling its bugs: a big vomit, right away (unless the game is already full).
            if (trueFormVomitBugs > 0 && CanVomit()) StartVomit(trueFormVomitBugs);
            else PlayState(Run);
        }

        private void StartDying()
        {
            phase = Phase.Dying;
            act = Act.None;
            Stop();
            // A boss killed before it came out of the ground still stands up to die.
            targetBuried = Mathf.Min(targetBuried, buried.Length > 0 ? buried[buried.Length - 1] : 0f);
            PlayState(Death, true);
            PlaySound(hurtSound);
            phaseUntil = Time.time + 2.5f;
        }

        private void UpdateDying()
        {
            if (Time.time < phaseUntil || model == null || !model.gameObject.activeSelf) return;
            var at = head != null ? head.position : transform.position;
            if (burstInto != null)
            {
                var bits = Instantiate(burstInto, at, transform.rotation, transform.parent);
                bits.transform.localScale *= Mathf.Max(1f, model.localScale.x * 1.2f);
            }
            if (smoke != null) Instantiate(smoke, at, Quaternion.identity, transform.parent);
            PlaySound(laughs);
            SendShake("ShakeBig");
            model.gameObject.SetActive(false);
            var stages = FsmVariables.GlobalVariables.FindFsmGameObject("GAMESTAGES");
            if (stages != null && stages.Value != null)
            {
                foreach (var fsm in stages.Value.GetComponents<PlayMakerFSM>())
                {
                    if (fsm.FsmName != "GAMESTAGES") continue;
                    var flag = fsm.FsmVariables.FindFsmBool(gameStagesBool);
                    if (flag != null) flag.Value = true;
                    if (!string.IsNullOrEmpty(gameStagesEvent)) fsm.SendEvent(gameStagesEvent);
                }
            }
            Destroy(gameObject, 1.6f);
        }

        // ---------- laughing and breaking the room ----------

        // Non-stop: the next laugh starts Laugh Gap after the last one ends, until it dies.
        private void KeepLaughing()
        {
            if (phase == Phase.Dying || laughSpeaker == null) return;
            if (!laughSpeaker.isPlaying && Time.time >= nextLaugh) LaughNow();
        }

        // A new laugh right now (its big moments: appearing, waking, coming out of the ground).
        private void LaughNow()
        {
            if (laughSpeaker == null || laughs.Length == 0) return;
            int i = Random.Range(0, laughs.Length);
            if (laughs.Length > 1 && i == lastLaugh) i = (i + 1 + Random.Range(0, laughs.Length - 1)) % laughs.Length;
            lastLaugh = i;
            var clip = laughs[i];
            if (clip == null) return;
            laughSpeaker.clip = clip;
            laughSpeaker.volume = laughVolume;
            laughSpeaker.Play();
            nextLaugh = Time.time + clip.length + laughGap;
        }

        // The walls of whichever room is standing fall away (its own fall-away clip), then the room is removed.
        private void BreakRooms()
        {
            bool broke = false;
            foreach (var name in roomVariables)
            {
                var room = FsmVariables.GlobalVariables.FindFsmGameObject(name)?.Value;
                if (room == null || !room.activeInHierarchy) continue;
                var walls = room.GetComponentInChildren<Animator>();
                var fallAway = walls != null ? FallAwayState(walls) : null;
                if (fallAway != null) walls.CrossFadeInFixedTime(fallAway, 0.1f, 0, 0f);
                Destroy(room, fallAway != null ? roomGoneAfter : 0.5f);
                broke = true;
            }
            if (!broke) return;
            SendShake("ShakeBig");
            Puff();
        }

        // Which clip takes the room's walls down from where they are now; none when they're already down.
        private static string FallAwayState(Animator walls)
        {
            var now = walls.GetCurrentAnimatorStateInfo(0);
            foreach (var (from, to) in FallAway)
            {
                if (!now.IsName(from)) continue;
                return to != null && walls.HasState(0, Animator.StringToHash(to)) ? to : null;
            }
            return null;
        }

        // Room animator states (Bedroom_Base_Z, Livingroom_Base_Z) and the clip that takes those walls down.
        private static readonly (string from, string to)[] FallAway =
        {
            ("Bedroom_Mini_COME", "Bedroom_Mini_GO"), ("Bedroom_Mini_GO2", "Bedroom_Mini_GO"),
            ("Bedroom_COME", "Bedroom_GO"), ("Bedroom_GO2", "Bedroom_GO"), ("Bedroom_Base_Z_Setup", "Bedroom_GO"),
            ("COME", "LivingRoom_GO"), ("LivingRoom_GO2", "LivingRoom_GO"), ("Livingroom_Setup", "LivingRoom_GO"),
            ("COME2", "GO4"),
        };

        // ---------- helpers ----------

        private void UpdateModel()
        {
            if (model == null) return;
            float s = Mathf.MoveTowards(model.localScale.x, targetScale, Time.deltaTime * 2.5f);
            float current = -model.localPosition.y / Mathf.Max(0.01f, s);
            float b = Mathf.MoveTowards(current, targetBuried, Time.deltaTime * (phase == Phase.Head ? 2.5f : 0.9f));
            model.localScale = Vector3.one * s;
            SetBuried(b, s);
        }

        private void SetBuried(float amount, float scale)
        {
            var p = model.localPosition;
            p.y = -amount * scale;
            model.localPosition = p;
        }

        private void PlayState(string state, bool restart = false)
        {
            if (animator == null || (!restart && playing == state)) return;
            playing = state;
            animator.CrossFadeInFixedTime(state, 0.15f, 0, 0f);
            if (phase == Phase.Head && state == GetHit) actUntil = Time.time + 0.65f;
        }

        private void Stop()
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh) { agent.isStopped = true; agent.velocity = Vector3.zero; }
        }

        private void Face(Vector3 point, float degreesPerSecond)
        {
            var flat = point - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.0001f) return;
            var want = Quaternion.LookRotation(flat);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, degreesPerSecond * Time.deltaTime);
        }

        private Vector3 PlayerFloor()
        {
            var headPos = player.Camera.transform.position;
            return new Vector3(headPos.x, player.transform.position.y, headPos.z);
        }

        private static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

        private bool FindPlayer()
        {
            if (player != null && player.Camera != null) return true;
            player = FindAnyObjectByType<XROrigin>();
            return player != null && player.Camera != null;
        }

        private void Puff()
        {
            if (smoke == null) return;
            var at = head != null ? head.position : transform.position;
            Instantiate(smoke, at, Quaternion.identity, transform.parent);
        }

        private void PlaySound(AudioClip clip)
        {
            if (clip == null || speaker == null) return;
            speaker.PlayOneShot(clip);
        }

        private void PlaySound(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0) return;
            PlaySound(clips[Random.Range(0, clips.Length)]);
        }

        private static void SendShake(string eventName)
        {
            var cam = FsmVariables.GlobalVariables.FindFsmGameObject("MainCam");
            if (cam == null || cam.Value == null) return;
            foreach (var fsm in cam.Value.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName == "Info") fsm.SendEvent(eventName);
            }
        }
    }
}

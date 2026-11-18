using System;
using System.Collections;
using System.Collections.Generic;
using Game.Combat;
using Game.Enemies;
using Game.Gates;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using FsmVariables = HutongGames.PlayMaker.FsmVariables;
using Random = UnityEngine.Random;

namespace Game.PlayGround
{
    /// <summary>
    /// Gate 1's PlayGround as "defend the cauldron" (user, 2026-11-16: "less cooking, more fighting and more feeling"):
    /// 1. The start chest's seed lets the cauldron out with a real pop: a burst of smoke, a thump, the pot springs up to
    ///    its size, and a player standing right there is pushed back a couple of metres (no damage).
    /// 2. Its fire burns small. The first firewood thrown in lights the big fire for good, and a right recipe waits for
    ///    that fire before it cooks ("LIGHT THE FIRE!"); after Spare Log After seconds of waiting a log pops out of the
    ///    fire pit, so the stage can never stall for want of firewood.
    /// 3. The magic hat and the recipe work as before, but there's ONE dish (Recipe Candies + Recipe Pumpkins) and it
    ///    cooks Cook Seconds (10 minutes). A wrong recipe still spits its slime.
    /// 4. The right recipe starts the countdown (big, above the pot, with the cauldron's health under it): the walls drop
    ///    and monsters come from the edges for the whole time (Phases, harder as it cooks). A player within Near Radius of
    ///    the cauldron is attacked; one further away (past Far Radius) leaves the cauldron to them, and they go and bite
    ///    it (Cauldron Health, like the Pumpkin Tree). The mini bosses always hunt the player.
    /// 5. At set shares of the cook a mini boss comes (Mini Bosses); each drops its chest where it dies, whose seed sets its
    ///    gear bool (with the start chest's Bracer that's all four GAMESTAGES needs).
    /// 6. Won (the countdown runs out, the cauldron standing): the Fairy Shield sweeps the field, gear seeds still lying
    ///    are eaten for the player and any gear bool still missing is set, then the pot swells and bursts (the Cauldron's
    ///    Burst: Pot Broken; the first-clear prize, Cooking Rewards). Since 2026-11-18 (user: gates 1 and 2 end like gate 3)
    ///    with Pumpkin Finale on, the giant pumpkin (Finale Boss, a Pumpkin Ball Boss) drops where the pot stood and rolls
    ///    about the PlayGround (its roam area): broken, it bursts into Finale Candies and The_Seed (Gate Exit), and
    ///    GAMESTAGES takes the player to the rooftop's tree defence. Off: the pot's burst takes the player to the Daruma
    ///    game (Special Stage 3 Trigger), whose win hands over The_Seed, as before.
    /// 7. Lost (its health runs out): the pot is smashed, the chaos Pumpkin Boss climbs out where it stood, the monsters keep
    ///    coming, all for the player now, and the teleport ground breaks (no teleporting until the run is over). It's
    ///    meant to be the end (user: "make it a sure death situation"); the normal death chain takes it from there, and a
    ///    lucky coin only gets the player up in the same mess. Killing that boss after all is the boss's own win.
    /// While this is on, the cauldron's own rounds, laughing pumpkins, wild bell slimes and candy pot are off, and the
    /// PlayGround Rounds director next to it must be off (it's switched off here if it isn't). Goes on the cauldron's root
    /// (Fire_Woods_n_Cauldron) with a Cauldron Health; Tools > 429 Game > PlayGround > Use Defend The Cauldron sets it up,
    /// "Use Cooking Rounds" switches back.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Cauldron))]
    [RequireComponent(typeof(CauldronHealth))]
    public class CauldronDefence : MonoBehaviour
    {
        /// <summary>The monsters while the dish cooks between From and the next phase's From.</summary>
        [Serializable]
        public class Phase
        {
            [Tooltip("Just a name for the Inspector.")]
            public string label;
            [Tooltip("From this share of the cook (0 to 1).")]
            [Range(0f, 1f)] public float from;
            [Tooltip("Each spawn picks one of these at random.")]
            public GameObject[] enemies = Array.Empty<GameObject>();
            [Tooltip("Most of them alive at once (the mini bosses don't count; the game's Enemy Limit also applies).")]
            [Min(1)] public int maxAlive = 5;
            [Tooltip("Seconds between two of them (min, max).")]
            public Vector2 spawnWait = new(2f, 3.5f);
        }

        /// <summary>A mini boss that comes at a share of the cook and drops a chest with a gear seed where it dies.</summary>
        [Serializable]
        public class MiniBoss
        {
            [Tooltip("Just a name for the Inspector.")]
            public string label;
            [Tooltip("It comes at this share of the cook (0.25 of 10 minutes = 2:30 in).")]
            [Range(0f, 1f)] public float at = 0.25f;
            public GameObject boss;
            [Tooltip("Dropped where it dies (a PG_ chest: candies, EXP and its gear seed).")]
            public GameObject chest;
            [Tooltip("The GAMESTAGES bool its chest's seed sets (set for the player at the win if it's still false).")]
            public string gearBool;
        }

        /// <summary>An effect played at once, this many times its prefab's size, this high over the cauldron's foot.</summary>
        [Serializable]
        public class Burst
        {
            public GameObject effect;
            [Min(0.01f)] public float scale = 1f;
            public float height;
        }

        [Header("The dish")]
        [Tooltip("Random candies the one recipe asks for (the magic hat shows them).")]
        [Min(0)] [SerializeField] private int recipeCandies = 4;
        [Tooltip("Tree pumpkins (Pumpkin_On_Tree) it asks for.")]
        [Min(0)] [SerializeField] private int recipePumpkins = 1;
        [Tooltip("How long it cooks, i.e. how long the cauldron has to be defended, in seconds (user: 10 minutes; " +
                 "2026-11-18: 5 minutes).")]
        [Min(10f)] [SerializeField] private float cookSeconds = 300f;
        [Tooltip("Firewood's heat still makes it cook faster (as it always did: each log takes roughly 10 s off the " +
                 "countdown while it burns). Off: always the full Cook Seconds.")]
        [SerializeField] private bool fuelSpeedsCountdown = true;
        [Tooltip("The right recipe has waited this long for the fire: one log pops out of the fire pit (0 = never).")]
        [Min(0f)] [SerializeField] private float spareLogAfter = 45f;
        [Tooltip("That log (Dropped_Wood_Fuel).")]
        [SerializeField] private GameObject spareLog;
        [Tooltip("Above the countdown.")]
        [SerializeField] private string countdownTitle = "DEFEND THE CAULDRON!";

        [Header("The pop (as it appears)")]
        [SerializeField] private Burst[] popEffects = Array.Empty<Burst>();
        [SerializeField] private AudioClip popSound;
        [Range(0f, 1f)] [SerializeField] private float popVolume = 1f;
        [Tooltip("Seconds it springs up from small to its size (with an overshoot).")]
        [Min(0.05f)] [SerializeField] private float popGrowSeconds = 0.4f;
        [Tooltip("A head within this many metres of it (along the floor) is pushed back...")]
        [Min(0f)] [SerializeField] private float popPushRadius = 3f;
        [Tooltip("...to about this far from it (at least Pop Min Push metres), Pop Push Height up. No damage; Toughness " +
                 "doesn't cut it.")]
        [Min(0f)] [SerializeField] private float popClear = 2.8f;
        [Min(0f)] [SerializeField] private float popMinPush = 1f;
        [Min(0f)] [SerializeField] private float popPushHeight = 0.5f;
        [Tooltip("A camera shake with the push (a camera shake event, never controller buzz).")]
        [SerializeField] private bool popShake;

        [Header("Monsters")]
        [Tooltip("In order of From. The last one goes on after a loss.")]
        [SerializeField] private Phase[] phases = Array.Empty<Phase>();
        [Tooltip("Seconds after the countdown starts before the first one comes.")]
        [Min(0f)] [SerializeField] private float firstSpawnAfter = 2f;
        [Tooltip("Puff where one comes in (pooled; e.g. Smoke_Flash_Small_Puff).")]
        [SerializeField] private GameObject spawnEffect;
        [Min(0.01f)] [SerializeField] private float spawnEffectScale = 0.5f;
        [Tooltip("They never come in closer than this to the player, in metres.")]
        [Min(0f)] [SerializeField] private float minPlayerDistance = 5f;
        [Tooltip("How far in from the PlayGround's edge they come in, in metres.")]
        [Min(0f)] [SerializeField] private float edgeInset = 1f;
        [Tooltip("A player this close to the cauldron (metres along the floor) is attacked...")]
        [Min(1f)] [SerializeField] private float nearRadius = 7f;
        [Tooltip("...and once further than this, the monsters go for the cauldron instead (between the two nothing " +
                 "changes, so they don't flip back and forth at the edge).")]
        [Min(1f)] [SerializeField] private float farRadius = 8.5f;
        [Tooltip("Where they stop and bite the cauldron: this far from its middle, in metres (outside its ring of stones).")]
        [Min(0.2f)] [SerializeField] private float standOff = 1.05f;

        [Header("Mini bosses")]
        [SerializeField] private MiniBoss[] miniBosses = Array.Empty<MiniBoss>();
        [Tooltip("Played as one comes in.")]
        [SerializeField] private AudioClip bossSound;
        [SerializeField] private string bossNote = "MINI BOSS!";
        [Tooltip("Puff where its chest drops.")]
        [SerializeField] private GameObject chestEffect;
        [Min(0.01f)] [SerializeField] private float chestEffectScale = 0.6f;

        [Header("Won")]
        [Tooltip("The Fairy Shield (GAMESTAGES' Shield_Sphere, with its green smoke) sweeps the field first.")]
        [SerializeField] private bool cleanWithFairyShield = true;
        [SerializeField] private string shieldName = "Shield_Sphere";
        [SerializeField] private string shieldSmokeName = "Smoke_Blast_Green";
        [Tooltip("The start chest's gear bool, also set at the win if it's somehow still false.")]
        [SerializeField] private string startGearBool = "Bracer_Gear";
        [Tooltip("Seconds from the countdown's end to the pot's swell (the sweep first).")]
        [Min(0f)] [SerializeField] private float burstDelay = 2.5f;

        [Header("Won: the giant pumpkin (user, 2026-11-18)")]
        [Tooltip("On: after the pot bursts the giant pumpkin drops in; breaking it gives The_Seed (then the rooftop's tree " +
                 "defence). Off: the Daruma game, as before.")]
        [SerializeField] private bool pumpkinFinale = true;
        [Tooltip("Prefabs/Gates/Pumpkin_Ball_Boss.")]
        [SerializeField] private GameObject finaleBoss;
        [Tooltip("What it leaves (Items/Chest/The_Seed).")]
        [SerializeField] private GameObject finaleSeed;
        [Tooltip("Seconds from the pot's swell to the drop (the swell and the burst take about 2).")]
        [Min(0f)] [SerializeField] private float finaleDelay = 4f;
        [Tooltip("Candies it bursts into (the Daruma game's prize was 42).")]
        [Min(0)] [SerializeField] private int finaleCandies = 42;
        [Tooltip("Its health here (the prefab's own if 0).")]
        [Min(0f)] [SerializeField] private float finaleHealth;
        [Tooltip("Its size here (metres): the PlayGround is only about 11 x 12 m (play test 2026-11-18: at 2.6 it could " +
                 "hardly move).")]
        [Min(0.5f)] [SerializeField] private float finaleRadius = 1.5f;

        [Header("Lost: the cauldron breaks")]
        [Tooltip("What climbs out where the pot stood, always as a chaos boss: pumpkin_boss_Z_Doom (made by the setup menu: " +
                 "a variant of pumpkin_boss_Z that rises out of the floor after 4 s instead of 33, chaos every time).")]
        [SerializeField] private GameObject chaosBoss;
        [Tooltip("Seconds from the smash to the boss.")]
        [Min(0f)] [SerializeField] private float bossDelay = 1.5f;
        [SerializeField] private Burst[] breakEffects = Array.Empty<Burst>();
        [SerializeField] private AudioClip breakSound;
        [SerializeField] private AudioClip rumbleSound;
        [Tooltip("Camera shake event as it breaks (MainCam's Info FSM). Empty: none.")]
        [SerializeField] private string breakShake = "ShakeBig";
        [Tooltip("More monsters alive at once after a loss, and spawning this share as far apart.")]
        [Min(0)] [SerializeField] private int doomExtraAlive = 2;
        [Range(0.1f, 1f)] [SerializeField] private float doomSpawnWaitShare = 0.7f;
        [Tooltip("The teleport ground breaks: no teleporting (puffs round the player, this sound, the words below).")]
        [SerializeField] private GameObject groundBreakEffect;
        [Min(0.01f)] [SerializeField] private float groundBreakScale = 0.35f;
        [SerializeField] private AudioClip groundBreakSound;
        [SerializeField] private string groundBreakText = "NO ESCAPE!";
        [SerializeField] private TMP_FontAsset font;

        /// <summary>The countdown started (the right recipe cooks).</summary>
        public static event Action<CauldronDefence> Started;
        /// <summary>A mini boss came in.</summary>
        public static event Action<CauldronDefence, GameObject> MiniBossCame;
        /// <summary>A mini boss's chest dropped.</summary>
        public static event Action<CauldronDefence, GameObject> ChestDropped;
        /// <summary>The countdown ran out with the cauldron standing.</summary>
        public static event Action<CauldronDefence> Won;
        /// <summary>The cauldron broke.</summary>
        public static event Action<CauldronDefence> Lost;

        /// <summary>The one cauldron defence out now (Play tests read it).</summary>
        public static CauldronDefence Current { get; private set; }

        public bool Running => running;
        public bool Doomed => doomed;
        public bool HasWon => over && !doomed;
        /// <summary>The monsters go for the player now (near the cauldron, or it's gone).</summary>
        public bool PlayerNear => playerNear;
        /// <summary>The monsters' phase now (index into Phases), -1 before the countdown.</summary>
        public int PhaseIndex => running ? CurrentPhaseIndex() : -1;
        public GameObject ChaosBoss => chaosBossMade;
        public bool TeleportBroken => teleportBroken;
        public int MiniBossesSent => sent.Count;
        public int ChestsDropped => chestsDropped;

        private const string SpawnedStuffVariable = "CurrentlySpawnedStuffs";
        // GAMESTAGES' chest seeds wait in this FSM state; "next" eats them (item, heal, gear bool).
        private const string SeedWaitState = "Rotate and Wait";
        private const string SeedEatenEvent = "next";
        // How often the monsters' target is checked, and the list of them refreshed.
        private const float TargetEvery = 0.25f;
        private const float ListEvery = 0.5f;

        private class LiveBoss
        {
            public MiniBoss kind;
            public GameObject made;
            public Vector3 last;
            public bool done;
        }

        private Cauldron cauldron;
        private CauldronHealth health;
        private bool running;
        private bool over;
        private bool doomed;
        private bool playerNear = true;
        private Coroutine waves;
        private readonly HashSet<MiniBoss> sent = new();
        private readonly List<LiveBoss> bosses = new();
        private readonly List<SpiderBrain> brains = new();
        private float nextTarget;
        private float nextList;
        private float fireWaitSince = -1f;
        private bool spareGiven;
        private int chestsDropped;
        private GameObject chaosBossMade;
        private Transform head;
        // The teleport broken on a loss, given back when this goes (the Game Over clears the PlayGround).
        private bool teleportBroken;
        private TeleportationProvider teleport;
        private GameObject teleportRay;
        private bool teleportWas, rayWas;
        private readonly List<BaseTeleportationInteractable> areasOff = new();

        private void Awake()
        {
            cauldron = GetComponent<Cauldron>();
            health = GetComponent<CauldronHealth>();
            // Switched off (Use Cooking Rounds): the cauldron keeps its rounds; nothing here touches it.
            if (!enabled) return;
            // The rounds director and this can't both run the cauldron: this wins.
            if (TryGetComponent(out PlayGroundRounds rounds) && rounds.enabled)
            {
                Debug.LogWarning("[Cauldron Defence] PlayGround Rounds is on too: switched off (run Tools > 429 Game > PlayGround > Use Defend The Cauldron).", this);
                rounds.enabled = false;
            }
            cauldron.UseOneDish(recipeCandies, recipePumpkins, cookSeconds);
            cauldron.HoldBetweenRounds = true;
            cauldron.MaxWildSlimes = 0;
            cauldron.FireNeedsLighting = true;
            cauldron.NeedsFireToCook = true;
            cauldron.HeatSpeedsCooking = fuelSpeedsCountdown;
            cauldron.ShowCountdown = true;
            cauldron.CountdownTitle = countdownTitle;
        }

        private void OnEnable()
        {
            Current = this;
            Cauldron.CookingStarted += OnCookingStarted;
            Cauldron.CookDone += OnCookDone;
            if (health != null) health.Broken += OnBroken;
        }

        private void OnDisable()
        {
            if (Current == this) Current = null;
            Cauldron.CookingStarted -= OnCookingStarted;
            Cauldron.CookDone -= OnCookDone;
            if (health != null) health.Broken -= OnBroken;
        }

        private void OnDestroy()
        {
            // Never leave the player without teleporting (the Game Over clears the PlayGround, a new run starts).
            GiveTeleportBack();
        }

        private void Start()
        {
            foreach (var burst in popEffects) if (burst != null && burst.effect != null) EffectPool.Warm(burst.effect);
            if (spawnEffect != null) EffectPool.Warm(spawnEffect, 2);
            StartCoroutine(PopIn());
        }

        private void Update()
        {
            WatchFire();
            if (!running) return;
            if (Time.time >= nextList) ListBrains();
            if (Time.time >= nextTarget) AimBrains();
            if (!doomed && !over) SendMiniBosses();
            WatchMiniBosses();
        }

        // ------------------------------------------------------------------ 1. the pop

        // It springs up out of the seed's spot with smoke and a thump, and a player right there is pushed back.
        private IEnumerator PopIn()
        {
            var size = transform.localScale;
            var foot = transform.position;
            foreach (var burst in popEffects)
            {
                if (burst != null && burst.effect != null) EffectPool.Play(burst.effect, foot + Vector3.up * burst.height, burst.scale);
            }
            if (popSound != null) AudioSource.PlayClipAtPoint(popSound, foot + Vector3.up, popVolume);
            PushPlayer(foot);
            for (float t = 0f; t < popGrowSeconds; t += Time.deltaTime)
            {
                float u = t / popGrowSeconds;
                // From a quarter of its size, past it (x1.15) and back: a springy pop.
                float s = Mathf.LerpUnclamped(0.25f, 1f, BackOut(u));
                transform.localScale = size * s;
                yield return null;
            }
            transform.localScale = size;
        }

        private static float BackOut(float t)
        {
            const float back = 2.2f;
            float u = t - 1f;
            return 1f + (back + 1f) * u * u * u + back * u * u;
        }

        // Back to about Pop Clear metres from it; nothing if they're further than Pop Push Radius. Its own colliders are
        // flown through (it may have appeared right on top of them).
        private void PushPlayer(Vector3 from)
        {
            var eye = Head();
            if (eye == null) return;
            var flat = eye.position - from;
            flat.y = 0f;
            float distance = flat.magnitude;
            if (distance > popPushRadius) return;
            float push = Mathf.Max(popMinPush, popClear - distance);
            PlayerLaunch.Launch(from, popPushHeight, push, popShake, false, 0f, gameObject);
        }

        // ------------------------------------------------------------------ 2. the fire

        // A right recipe waiting for the fire too long: one log pops out of the fire pit, towards the player.
        private void WatchFire()
        {
            if (cauldron == null || !cauldron.WaitingForFire)
            {
                fireWaitSince = -1f;
                return;
            }
            if (fireWaitSince < 0f) fireWaitSince = Time.time;
            if (spareGiven || spareLog == null || spareLogAfter <= 0f || Time.time - fireWaitSince < spareLogAfter) return;
            spareGiven = true;
            var eye = Head();
            var toward = eye != null ? eye.position - transform.position : transform.forward;
            toward.y = 0f;
            toward = toward.sqrMagnitude > 0.0001f ? toward.normalized : Vector3.forward;
            var from = transform.position + Vector3.up * 0.6f + toward * 0.6f;
            var log = Instantiate(spareLog, from, Quaternion.Euler(0f, Random.Range(0f, 360f), 90f), SpawnedStuff());
            if (log.TryGetComponent<Rigidbody>(out var body) && !body.isKinematic) body.linearVelocity = toward * 2.2f + Vector3.up * 3f;
            if (spawnEffect != null) EffectPool.Play(spawnEffect, from, spawnEffectScale);
            Debug.Log("[Cauldron Defence] The recipe waited " + spareLogAfter + " s for the fire: a log popped out of the fire pit.", this);
        }

        // ------------------------------------------------------------------ 3-4. the countdown

        private void OnCookingStarted(Cauldron who)
        {
            if (who != cauldron || running || over) return;
            running = true;
            playerNear = true;
            health.Arm();
            var field = FindAnyObjectByType<PlayGroundField>();
            if (field != null) field.WallsDown(0);
            EnemyWave.Start(this);
            waves = StartCoroutine(RunWaves());
            Debug.Log("[Cauldron Defence] The countdown started: " + cauldron.DishSeconds.ToString("0") + " s, walls down, monsters coming.", this);
            Started?.Invoke(this);
        }

        // Non-stop: one whenever there's room, harder as it cooks; after a loss the last phase goes on, fuller and faster.
        private IEnumerator RunWaves()
        {
            if (firstSpawnAfter > 0f) yield return new WaitForSeconds(firstSpawnAfter);
            while (!over)
            {
                int index = CurrentPhaseIndex();
                var phase = index >= 0 ? phases[index] : null;
                if (phase == null)
                {
                    yield return new WaitForSeconds(1f);
                    continue;
                }
                int most = phase.maxAlive + (doomed ? doomExtraAlive : 0);
                if (Minions() >= most || !EnemyLimit.TryTakeSlot())
                {
                    yield return new WaitForSeconds(0.25f);
                    continue;
                }
                Spawn(Pick(phase.enemies), false);
                float wait = Random.Range(phase.spawnWait.x, Mathf.Max(phase.spawnWait.x, phase.spawnWait.y));
                yield return new WaitForSeconds(wait * (doomed ? doomSpawnWaitShare : 1f));
            }
        }

        // The phase for the share cooked now (the last one after a loss).
        private int CurrentPhaseIndex()
        {
            if (phases == null || phases.Length == 0) return -1;
            if (doomed) return phases.Length - 1;
            float done = cauldron != null ? cauldron.CookProgress : 0f;
            int at = 0;
            for (int i = 0; i < phases.Length; i++)
            {
                if (phases[i] != null && done >= phases[i].from) at = i;
            }
            return at;
        }

        // The wave's monsters alive, not counting the mini bosses.
        private int Minions()
        {
            int alive = EnemyWave.Alive(this);
            foreach (var boss in bosses)
            {
                if (EnemyWave.IsAlive(boss.made)) alive--;
            }
            return Mathf.Max(0, alive);
        }

        // ------------------------------------------------------------------ who they go for

        // Every monster on the PlayGround with a Spider Brain (the wave's, what slimes split into, a wrong recipe's slime),
        // not the mini bosses or the white bug.
        private void ListBrains()
        {
            nextList = Time.time + ListEvery;
            brains.Clear();
            var spawned = SpawnedStuff();
            var area = GhostRoamArea.Around(transform.position);
            foreach (var brain in FindObjectsByType<SpiderBrain>(FindObjectsSortMode.None))
            {
                if (brain == null || brain.IsBoss || brain.IsAnomaly) continue;
                if (spawned != null && !brain.transform.IsChildOf(spawned)) continue;
                if (area != null && !area.ContainsFlat(brain.transform.position) && FlatDistance(brain.transform.position, transform.position) > 30f) continue;
                brains.Add(brain);
            }
        }

        // Near the cauldron the player is the target; far from it, the cauldron (user, 2026-11-16). After a loss there's
        // only the player.
        private void AimBrains()
        {
            nextTarget = Time.time + TargetEvery;
            var eye = Head();
            if (eye != null && !doomed)
            {
                float distance = FlatDistance(eye.position, transform.position);
                if (playerNear && distance > farRadius) playerNear = false;
                else if (!playerNear && distance <= nearRadius) playerNear = true;
            }
            if (doomed || over) playerNear = true;
            var cauldronSpot = !playerNear && !over && !doomed && !health.IsBroken ? transform : null;
            foreach (var brain in brains)
            {
                if (brain == null) continue;
                brain.GoFor(cauldronSpot, standOff);
                // After the player: make sure it has noticed them (spawned at the far edge, it may not see them yet).
                if (cauldronSpot == null) brain.Notice();
            }
        }

        // ------------------------------------------------------------------ 5. mini bosses

        private void SendMiniBosses()
        {
            float done = cauldron.CookProgress;
            foreach (var kind in miniBosses)
            {
                if (kind == null || kind.boss == null || sent.Contains(kind) || done < kind.at) continue;
                sent.Add(kind);
                var made = Spawn(kind.boss, true);
                if (made == null)
                {
                    // No spot on the edge right now: it comes a moment later.
                    sent.Remove(kind);
                    continue;
                }
                bosses.Add(new LiveBoss { kind = kind, made = made, last = made.transform.position });
                if (bossSound != null) AudioSource.PlayClipAtPoint(bossSound, made.transform.position, 1f);
                cauldron.ShowNote(bossNote, new Color(1f, 0.45f, 0.9f), 4f);
                Debug.Log("[Cauldron Defence] Mini boss " + made.name + " came at " + (done * 100f).ToString("0") + "% (" + kind.gearBool + ").", this);
                MiniBossCame?.Invoke(this, made);
            }
        }

        // A mini boss gone (killed, popped, fallen) drops its chest where it was last seen, once.
        private void WatchMiniBosses()
        {
            foreach (var boss in bosses)
            {
                if (boss.done) continue;
                if (EnemyWave.IsAlive(boss.made))
                {
                    boss.last = boss.made.transform.position;
                    continue;
                }
                boss.done = true;
                // The win's sweep kills what's left: the gear is given there.
                if (over) continue;
                DropChest(boss.kind, boss.last);
            }
        }

        private void DropChest(MiniBoss kind, Vector3 near)
        {
            if (kind.chest == null)
            {
                if (!string.IsNullOrEmpty(kind.gearBool)) GameStages.SetBool(kind.gearBool, true, this);
                return;
            }
            var at = near;
            // Not inside the cauldron's ring of stones.
            var away = at - transform.position;
            away.y = 0f;
            if (away.magnitude < standOff + 0.8f) at = transform.position + (away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.forward) * (standOff + 0.8f);
            if (Physics.Raycast(at + Vector3.up * 2f, Vector3.down, out var floor, 10f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                at = floor.point;
            if (NavMesh.SamplePosition(at, out var onMesh, 3f, NavMesh.AllAreas)) at = onMesh.position;
            var made = Instantiate(kind.chest, at, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), SpawnedStuff());
            // A kinematic chest (held still until opened) has no velocity to clear, and Unity warns if it's set.
            if (made.TryGetComponent<Rigidbody>(out var body) && !body.isKinematic) body.linearVelocity = Vector3.zero;
            if (chestEffect != null) EffectPool.Play(chestEffect, at + Vector3.up * 0.3f, chestEffectScale);
            chestsDropped++;
            Debug.Log("[Cauldron Defence] " + kind.label + ": its chest " + kind.chest.name + " dropped (" + kind.gearBool + ").", this);
            ChestDropped?.Invoke(this, made);
        }

        // ------------------------------------------------------------------ 6. won

        private void OnCookDone(Cauldron who)
        {
            if (who != cauldron || !running || over || doomed) return;
            StartCoroutine(Win());
        }

        private IEnumerator Win()
        {
            over = true;
            health.Disarm();
            if (waves != null) StopCoroutine(waves);
            waves = null;
            if (cleanWithFairyShield) FireFairyShield();
            EnemyWave.KillAll(this);
            // In the same frame: no slime the player hit a moment ago splits as the wave dies, and every other slime pops.
            SlimeBody.ClearAll();
            GearGuard();
            Debug.Log("[Cauldron Defence] Won: the cauldron stood " + cauldron.DishSeconds.ToString("0") + " s (" + health.Health.ToString("0") + "/" + health.MaxHealth.ToString("0") + " health left). The pot bursts in " + burstDelay + " s.", this);
            Won?.Invoke(this);
            // The giant pumpkin instead of the Daruma game: the pot's burst no longer takes the player away.
            bool finale = pumpkinFinale && finaleBoss != null;
            if (pumpkinFinale && finaleBoss == null)
                Debug.LogWarning("[Cauldron Defence] Pumpkin Finale is on but Finale Boss is empty (Tools > 429 Game > PlayGround > " +
                                 "Use Defend The Cauldron sets it): the Daruma game, as before.", this);
            if (finale && TryGetComponent(out SpecialStage3Trigger daruma)) daruma.enabled = false;
            if (burstDelay > 0f) yield return new WaitForSeconds(burstDelay);
            cauldron.Burst();
            if (finale) StartFinale();
        }

        // The giant pumpkin drops where the pot stood (once it has burst) and rolls about the PlayGround's roam area. It
        // runs on its own object: this cauldron is gone 3 s after the burst.
        private void StartFinale()
        {
            // The floor where the pot stands (not the pot itself: it's still there as this runs).
            var spot = transform.position;
            float best = float.NegativeInfinity;
            foreach (var hit in Physics.RaycastAll(spot + Vector3.up * 2f, Vector3.down, 10f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(transform) || hit.rigidbody != null) continue;
                if (hit.point.y > best) best = hit.point.y;
            }
            if (!float.IsNegativeInfinity(best)) spot.y = best;
            var area = GhostRoamArea.Around(transform.position);
            var middle = area != null ? new Vector3(area.Middle.x, spot.y, area.Middle.z) : spot;
            GateBossFinale.Begin(finaleBoss, finaleSeed, spot, finaleDelay, boss =>
            {
                boss.Resize(finaleRadius);
                if (finaleCandies > 0) boss.breakCandies = finaleCandies;
                if (finaleHealth > 0f) boss.health = finaleHealth;
                boss.arenaCentre = middle;
                // On the PlayGround, all of it inside its roam area.
                if (area != null) boss.canBeAt = p => area.ContainsFlat(p, boss.radius);
            }, at => area != null && !area.ContainsFlat(at, 1f) ? middle : at, "[Cauldron Defence]");
        }

        // Gear seeds still lying are eaten for the player (item, heal, gear bool, as the bowling stage does), then any gear
        // bool still false is set: GAMESTAGES' Path 1 ON waits for all four (and The_Seed, from the Daruma game).
        private void GearGuard()
        {
            var spawned = SpawnedStuff();
            if (spawned != null)
            {
                foreach (var fsm in spawned.GetComponentsInChildren<PlayMakerFSM>())
                {
                    if (fsm == null || fsm.ActiveStateName != SeedWaitState || !IsGearSeed(fsm.transform, spawned)) continue;
                    Debug.Log("[Cauldron Defence] " + fsm.gameObject.name + " not eaten: eaten for the player.", this);
                    fsm.SendEvent(SeedEatenEvent);
                }
            }
            if (GameStages.Fsm() == null) return;
            var gears = new List<string> { startGearBool };
            foreach (var kind in miniBosses) if (kind != null) gears.Add(kind.gearBool);
            foreach (var gear in gears)
            {
                if (string.IsNullOrEmpty(gear) || GameStages.GetBool(gear)) continue;
                GameStages.SetBool(gear, true, this);
                Debug.Log("[Cauldron Defence] " + gear + " was still false at the win (chest not opened?): set so GAMESTAGES goes on.", this);
            }
        }

        // The FSM's object, or one of its parents below Currently Spawned Stuffs, is a gear seed ("Seed_", not the
        // cauldron's own seed).
        private static bool IsGearSeed(Transform t, Transform spawned)
        {
            for (; t != null && t != spawned; t = t.parent)
            {
                if (t.name.StartsWith("Seed_")) return !t.name.Contains("Cauldron");
            }
            return false;
        }

        // Switches the GAMESTAGES Fairy Shield on, and its green smoke with it (as PlayGround Rounds does): the shield grows
        // over the stage by itself and switches itself off again, and so does the smoke. The cauldron ignores it.
        private void FireFairyShield()
        {
            GameObject shield = null, smoke = null;
            foreach (var candidate in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (!candidate.scene.IsValid()) continue;
                if (shield == null && candidate.name == shieldName && candidate.CompareTag("Fairy_Shield")) shield = candidate;
                else if (smoke == null && !string.IsNullOrEmpty(shieldSmokeName) && candidate.name == shieldSmokeName) smoke = candidate;
            }
            if (shield == null) return;
            if (shield.activeSelf) shield.SetActive(false);
            shield.SetActive(true);
            if (smoke == null) return;
            if (smoke.activeSelf) smoke.SetActive(false);
            smoke.SetActive(true);
        }

        // ------------------------------------------------------------------ 7. lost

        private void OnBroken(CauldronHealth who)
        {
            if (doomed || over) return;
            StartCoroutine(Doom());
        }

        private IEnumerator Doom()
        {
            doomed = true;
            playerNear = true;
            var foot = transform.position;
            float done = cauldron.CookProgress;
            cauldron.Wreck();
            foreach (var burst in breakEffects)
            {
                if (burst != null && burst.effect != null) EffectPool.Play(burst.effect, foot + Vector3.up * burst.height, burst.scale);
            }
            if (breakSound != null) AudioSource.PlayClipAtPoint(breakSound, foot + Vector3.up, 1f);
            if (rumbleSound != null) AudioSource.PlayClipAtPoint(rumbleSound, foot, 1f);
            Shake(breakShake);
            // Every monster turns on the player at once.
            ListBrains();
            AimBrains();
            Debug.Log("[Cauldron Defence] LOST: the cauldron broke " + (done * 100f).ToString("0") + "% into its countdown; the chaos boss comes, the teleport ground breaks.", this);
            Lost?.Invoke(this);
            yield return new WaitForSeconds(bossDelay * 0.5f);
            BreakTeleport();
            yield return new WaitForSeconds(bossDelay * 0.5f);
            SpawnChaosBoss(foot);
        }

        private void SpawnChaosBoss(Vector3 foot)
        {
            if (chaosBoss == null)
            {
                Debug.LogWarning("[Cauldron Defence] No Chaos Boss set: only the monsters come.", this);
                return;
            }
            var spot = foot;
            var filter = new NavMeshQueryFilter { areaMask = NavMesh.AllAreas };
            if (chaosBoss.TryGetComponent<NavMeshAgent>(out var agent)) filter.agentTypeID = agent.agentTypeID;
            if (NavMesh.SamplePosition(spot, out var onMesh, 4f, filter) || NavMesh.SamplePosition(spot, out onMesh, 4f, NavMesh.AllAreas))
                spot = onMesh.position;
            var eye = Head();
            var look = eye != null ? eye.position - spot : transform.forward;
            look.y = 0f;
            // Always the chaos one (user, 2026-11-16: "Pumpkin Boss Z chaos will come out"); the roll is in its Awake.
            PumpkinBossBrain.ForceNextChaos = true;
            chaosBossMade = Instantiate(chaosBoss, spot, look.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(look) : Quaternion.identity, SpawnedStuff());
            PumpkinBossBrain.ForceNextChaos = false;
            Debug.Log("[Cauldron Defence] " + chaosBossMade.name + " climbed out of the broken cauldron.", this);
        }

        // The teleport ground breaks (user, 2026-11-16): the player's teleport is switched off, its ray hidden and the
        // PlayGround's teleport floors off, with a ring of puffs round their feet, a crack and the words. Given back when
        // this goes (Game Over) or the scene closes.
        private void BreakTeleport()
        {
            if (teleportBroken) return;
            var origin = FindAnyObjectByType<XROrigin>();
            if (origin == null) return;
            teleportBroken = true;
            teleport = origin.GetComponentInChildren<TeleportationProvider>(true);
            teleportWas = teleport != null && teleport.enabled;
            if (teleport != null) teleport.enabled = false;
            foreach (var t in origin.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "Teleport Interactor") continue;
                teleportRay = t.gameObject;
                rayWas = teleportRay.activeSelf;
                teleportRay.SetActive(false);
                break;
            }
            var spawned = SpawnedStuff();
            foreach (var area in FindObjectsByType<BaseTeleportationInteractable>(FindObjectsSortMode.None))
            {
                if (area == null || !area.enabled || (spawned != null && !area.transform.IsChildOf(spawned))) continue;
                area.enabled = false;
                areasOff.Add(area);
            }
            var eye = Head();
            if (eye == null) return;
            var feet = new Vector3(eye.position.x, origin.transform.position.y, eye.position.z);
            if (groundBreakEffect != null)
            {
                for (int i = 0; i < 6; i++)
                {
                    var at = feet + Quaternion.Euler(0f, i * 60f + Random.Range(-15f, 15f), 0f) * Vector3.forward * Random.Range(1.6f, 2.6f);
                    EffectPool.Play(groundBreakEffect, at + Vector3.up * 0.05f, groundBreakScale);
                }
            }
            if (groundBreakSound != null) AudioSource.PlayClipAtPoint(groundBreakSound, eye.position, 1f);
            ShowWords(groundBreakText, eye);
        }

        private void GiveTeleportBack()
        {
            if (!teleportBroken) return;
            teleportBroken = false;
            if (teleport != null) teleport.enabled = teleportWas;
            if (teleportRay != null) teleportRay.SetActive(rayWas);
            foreach (var area in areasOff) if (area != null) area.enabled = true;
            areasOff.Clear();
            teleport = null;
            teleportRay = null;
        }

        // Big red words in front of the player for a few seconds (no pause).
        private void ShowWords(string words, Transform eye)
        {
            if (string.IsNullOrEmpty(words)) return;
            var panel = WorldUI.CreatePanel("Cauldron Defence Words", 520f, 110f, 0.0024f, new Color(0f, 0f, 0f, 0f));
            var text = WorldUI.CreateText("Words", panel, words, 84f, FontStyles.Bold, TextAlignmentOptions.Center, new Color(1f, 0.2f, 0.15f), font);
            WorldUI.Place((Graphic)text, 0f, 0f, 520f, 110f);
            var forward = Vector3.ProjectOnPlane(eye.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            panel.position = eye.position + forward.normalized * 1.6f - Vector3.up * 0.25f;
            WorldUI.Face(panel, eye);
            Destroy(panel.gameObject, 3f);
        }

        // ------------------------------------------------------------------ spawning

        private GameObject Spawn(GameObject prefab, bool isBoss)
        {
            if (prefab == null || !SpawnPoint(out var at)) return null;
            var enemy = Instantiate(prefab, at, Quaternion.identity, SpawnedStuff());
            var eye = Head();
            if (eye != null)
            {
                var look = eye.position - at;
                look.y = 0f;
                if (look.sqrMagnitude > 0.01f) enemy.transform.rotation = Quaternion.LookRotation(look);
            }
            EnemyWave.Add(this, enemy);
            if (spawnEffect != null) EffectPool.Play(spawnEffect, at + Vector3.up * 0.2f, spawnEffectScale * (isBoss ? 2f : 1f));
            if (enemy.TryGetComponent(out SpiderBrain brain))
            {
                // Straight into the fight, at whoever it's sent at (a mini boss hunts the player by itself).
                if (!isBoss && !brain.IsAnomaly && !playerNear && !doomed) brain.GoFor(transform, standOff);
                brain.ComeAtOnce();
            }
            return enemy;
        }

        // A spot on the PlayGround's edge (its Ghost Roam Area), on the NavMesh, not right next to the player.
        private bool SpawnPoint(out Vector3 at)
        {
            var area = GhostRoamArea.Around(transform.position);
            var eye = Head();
            var headAt = eye != null ? eye.position : transform.position;
            for (int i = 0; i < 12; i++)
            {
                var spot = area != null
                    ? area.RandomEdgePoint(edgeInset)
                    : transform.position + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * Random.Range(8f, 12f);
                if (Physics.Raycast(spot + Vector3.up * 1f, Vector3.down, out var floor, 15f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    spot = floor.point;
                if (!NavMesh.SamplePosition(spot, out var hit, 2f, NavMesh.AllAreas)) continue;
                if (FlatDistance(hit.position, headAt) < minPlayerDistance) continue;
                at = hit.position;
                return true;
            }
            at = default;
            return false;
        }

        // ------------------------------------------------------------------ tests

        /// <summary>Test only: the cauldron loses all its health at once (the countdown must be running).</summary>
        [ContextMenu("Test: Break The Cauldron")]
        public void TestBreak()
        {
            if (!running || health == null) return;
            health.TakeDamage(health.Health + 1f, transform.position + transform.forward);
        }

        /// <summary>Test only: the countdown jumps to this share done (0 to 1); mini bosses due come at once.</summary>
        public void TestSkipTo(float share)
        {
            if (cauldron != null) cauldron.TestSetProgress(share);
        }

        // ------------------------------------------------------------------ helpers

        private Transform Head()
        {
            if (head == null && Camera.main != null) head = Camera.main.transform;
            return head;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            float x = a.x - b.x, z = a.z - b.z;
            return Mathf.Sqrt(x * x + z * z);
        }

        private static GameObject Pick(GameObject[] choices)
        {
            if (choices == null || choices.Length == 0) return null;
            return choices[Random.Range(0, choices.Length)];
        }

        private static void Shake(string eventName)
        {
            if (string.IsNullOrEmpty(eventName)) return;
            var cam = FsmVariables.GlobalVariables.FindFsmGameObject("MainCam");
            if (cam == null || cam.Value == null) return;
            foreach (var fsm in cam.Value.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName == "Info") fsm.SendEvent(eventName);
            }
        }

        private static Transform SpawnedStuff()
        {
            var holder = FsmVariables.GlobalVariables.FindFsmGameObject(SpawnedStuffVariable);
            return holder != null && holder.Value != null ? holder.Value.transform : null;
        }
    }
}

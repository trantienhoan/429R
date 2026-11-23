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
    ///    cooks Cook Seconds (5 minutes, user 2026-11-18). A wrong recipe still spits its slime.
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
    ///    coming, all for the player now, and the teleport ground breaks (no teleporting until that boss is beaten). It's
    ///    meant to be the end (user: "make it a sure death situation"); the normal death chain takes it from there, and a
    ///    lucky coin only gets the player up in the same mess. Killing that boss after all only ends the gate (user,
    ///    2026-11-23; it used to win the whole game): The_Seed drops where it burst and Gate Exit takes the player to the
    ///    rooftop, as after the giant pumpkin.
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
            [Tooltip("It comes at this share of the cook (0.25 of 5 minutes = 1:15 in; firewood makes the cook, and so this, " +
                     "come sooner).")]
            [Range(0f, 1f)] public float at = 0.25f;
            public GameObject boss;
            [Tooltip("Dropped where it dies (a PG_ chest: candies, EXP and its gear seed).")]
            public GameObject chest;
            [Tooltip("The GAMESTAGES bool its chest's seed sets (set for the player at the win if it's still false).")]
            public string gearBool;
        }

        /// <summary>
        /// One round of the cook in the shared gate flow (Play Ground Game drives the cauldron through Gate Run, 2026-11-23):
        /// its recipe, its cook, the monsters while it cooks, and the mini bosses that come out when the dish is done.
        /// </summary>
        [Serializable]
        public class Round
        {
            [Tooltip("Just a name for the Inspector.")]
            public string label;
            [Tooltip("Random candies the recipe asks for (the magic hat shows them).")]
            [Min(0)] public int candies = 2;
            [Tooltip("Tree pumpkins it asks for.")]
            [Min(0)] public int pumpkins = 1;
            [Tooltip("Seconds it cooks with no firewood (logs make it faster).")]
            [Min(5f)] public float cookSeconds = 45f;
            [Tooltip("The monsters while it cooks and while its mini bosses are out; each spawn picks one at random.")]
            public GameObject[] enemies = Array.Empty<GameObject>();
            [Tooltip("Most of them alive at once (the mini bosses don't count).")]
            [Min(1)] public int maxAlive = 4;
            [Tooltip("Seconds between two of them (min, max).")]
            public Vector2 spawnWait = new(2.5f, 4f);
            [Tooltip("Monster gates open at once (of the field's 4 corners).")]
            [Range(1, 4)] public int openGates = 1;
            [Tooltip("They come out when the dish is done; the round is won when they're all beaten.")]
            public GameObject[] miniBosses = Array.Empty<GameObject>();
        }

        /// <summary>Where a round of the gate flow is.</summary>
        public enum RoundStep { None, Gathering, Cooking, Bosses, Done }

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
        [Tooltip("How long it cooks with no firewood, i.e. how long the cauldron has to be defended, in seconds (user " +
                 "2026-11-18: 5 minutes; it was 10).")]
        [Min(10f)] [SerializeField] private float cookSeconds = 300f;
        [Tooltip("Firewood's heat still makes it cook faster (as it always did: each log takes roughly 10 s off the " +
                 "countdown while it burns). Off: always the full Cook Seconds.")]
        [SerializeField] private bool fuelSpeedsCountdown = true;
        [Tooltip("The right recipe has waited this long for the fire: a log pops out of the fire pit, and another each " +
                 "time it waits as long again (0 = never).")]
        [Min(0f)] [SerializeField] private float spareLogAfter = 45f;
        [Tooltip("That log (Dropped_Wood_Fuel).")]
        [SerializeField] private GameObject spareLog;
        [Tooltip("Above the countdown.")]
        [SerializeField] private string countdownTitle = "DEFEND THE CAULDRON!";

        [Header("Never stuck (2026-11-23)")]
        [Tooltip("The recipe needs a tree pumpkin and none has been about for this long (none lying on the PlayGround or " +
                 "held): the pot spits one out towards the player (0 = never). Pumpkins only come from broken props, and " +
                 "the purple hole of a death can eat those.")]
        [Min(0f)] [SerializeField] private float sparePumpkinAfter = 40f;
        [Tooltip("The magic hat is gone while its recipe is still unread (eaten by the purple hole of a death, fallen out " +
                 "of the world): after this many seconds a new one pops up a few metres from the pot (0 = never).")]
        [Min(0f)] [SerializeField] private float newHatAfter = 4f;
        [Tooltip("That hat (Prefabs/PlayGround/Recipe_Hat).")]
        [SerializeField] private GameObject hatPrefab;

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
        [Tooltip("This share of the monsters (not the mini bosses) always go for the cauldron, wherever the player is, so " +
                 "it's under real pressure (2026-11-23). 0 = the old rule only (near the pot they go for the player).")]
        [Range(0f, 1f)] [SerializeField] private float potBiterShare = 0.25f;

        [Header("Monster gates (2026-11-23)")]
        [Tooltip("The monsters come in at a few gates (the field's corners) instead of anywhere along its edge, so the " +
                 "player sees where they come from. Off: anywhere on the edge, as before.")]
        [SerializeField] private bool spawnFromGates = true;
        [Tooltip("Gates open at once in each phase (the last number goes on for later phases; all 4 after a loss).")]
        [SerializeField] private int[] openGates = { 1, 2, 2, 3 };
        [Tooltip("The open gates move to other corners this often, in seconds.")]
        [Min(5f)] [SerializeField] private float gateSwitchSeconds = 25f;
        [Tooltip("A gate stands this far in from the field's corner, in metres.")]
        [Min(0f)] [SerializeField] private float gateInset = 1.2f;
        [Tooltip("A monster comes in within this many metres of its gate.")]
        [Min(0f)] [SerializeField] private float gateSpread = 1.2f;

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
                 "a variant of pumpkin_boss_Z that rises out of the floor after 4 s instead of 33, chaos every time). Killing " +
                 "it only ends the gate (The_Seed, then the rooftop): the variant sends GAMESTAGES no Win and sets no BOSS.")]
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
        /// <summary>Where the monster gates stand (the field's corners; empty until the first spawn) and which are open.</summary>
        public IReadOnlyList<Vector3> GateSpots => gateSpots;
        public IReadOnlyList<int> OpenGates => openGateIndex;
        /// <summary>The open monster gates changed (a gate's look can follow it).</summary>
        public static event Action<CauldronDefence> GatesMoved;
        /// <summary>Driven round by round by the gate flow (Play Ground Game), not the old single 5-minute dish.</summary>
        public bool Driven => driven;
        public RoundStep Step => step;
        /// <summary>This round's mini bosses (out and still to come), and how many are beaten.</summary>
        public int RoundBossesTotal => roundBosses.Count + bossesToCome.Count;
        public int RoundBossesBeaten => roundBossesBeaten;
        public Cauldron TheCauldron => cauldron;

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
        // The monsters that always go for the cauldron (Pot Biter Share).
        private readonly HashSet<SpiderBrain> potBiters = new();
        private readonly List<Vector3> gateSpots = new();
        private readonly List<int> openGateIndex = new();
        private float nextGateSwitch;
        private object gatesKey;
        // The gate flow's round (Begin Round): its plan, where it is, its mini bosses, and who to tell when it's won.
        private bool driven;
        private Round plan;
        private RoundStep step = RoundStep.None;
        private Action roundWon;
        private readonly List<GameObject> roundBosses = new();
        private readonly List<GameObject> bossesToCome = new();
        private int roundBossesBeaten;
        private bool wallsDown;
        private float nextTarget;
        private float nextList;
        private float fireWaitSince = -1f;
        private int sparesGiven;
        private float nextIngredientCheck;
        private float noPumpkinSince = -1f;
        private float noHatSince = -1f;
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
            // Its wave is forgotten (after a loss the monsters were still coming when the Game Over cleared them).
            EnemyWave.Start(this);
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
            if (!over && !doomed) WatchIngredients();
            if (driven && step == RoundStep.Bosses && !over && !doomed) WatchRoundBosses();
            if (!running) return;
            if (Time.time >= nextList) ListBrains();
            if (Time.time >= nextTarget) AimBrains();
            if (driven) return;
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

        // A right recipe waiting for the fire too long: a log pops out of the fire pit, towards the player; another one
        // every Spare Log After seconds while it still waits (a log thrown away, or rolled off, can't stall the stage).
        private void WatchFire()
        {
            if (cauldron == null || !cauldron.WaitingForFire)
            {
                fireWaitSince = -1f;
                return;
            }
            if (fireWaitSince < 0f) fireWaitSince = Time.time;
            if (spareLog == null || spareLogAfter <= 0f || Time.time - fireWaitSince < spareLogAfter) return;
            fireWaitSince = Time.time;
            sparesGiven++;
            var eye = Head();
            var toward = eye != null ? eye.position - transform.position : transform.forward;
            toward.y = 0f;
            toward = toward.sqrMagnitude > 0.0001f ? toward.normalized : Vector3.forward;
            var from = transform.position + Vector3.up * 0.6f + toward * 0.6f;
            var log = Instantiate(spareLog, from, Quaternion.Euler(0f, Random.Range(0f, 360f), 90f), SpawnedStuff());
            if (log.TryGetComponent<Rigidbody>(out var body) && !body.isKinematic) body.linearVelocity = toward * 2.2f + Vector3.up * 3f;
            if (spawnEffect != null) EffectPool.Play(spawnEffect, from, spawnEffectScale);
            Debug.Log("[Cauldron Defence] The recipe waited " + spareLogAfter + " s for the fire: log " + sparesGiven + " popped out of the fire pit.", this);
        }

        // Never stuck while the pot takes ingredients: a new magic hat when it's gone with its recipe unread, and a tree
        // pumpkin out of the pot when the recipe needs one and none is about.
        private void WatchIngredients()
        {
            if (Time.time < nextIngredientCheck) return;
            nextIngredientCheck = Time.time + 1f;
            if (cauldron == null || !cauldron.TakingIngredients)
            {
                noPumpkinSince = noHatSince = -1f;
                return;
            }
            if (hatPrefab != null && newHatAfter > 0f && !cauldron.RecipeKnown && FindAnyObjectByType<MagicHat>() == null)
            {
                if (noHatSince < 0f) noHatSince = Time.time;
                else if (Time.time - noHatSince >= newHatAfter)
                {
                    noHatSince = -1f;
                    NewHat();
                }
            }
            else noHatSince = -1f;

            string pumpkinId = cauldron.PumpkinId;
            if (sparePumpkinAfter > 0f && cauldron.PumpkinPrefab != null && cauldron.Missing(pumpkinId) > 0 && !IngredientAbout(pumpkinId))
            {
                if (noPumpkinSince < 0f) noPumpkinSince = Time.time;
                else if (Time.time - noPumpkinSince >= sparePumpkinAfter)
                {
                    noPumpkinSince = -1f;
                    SpitTowardsPlayer(cauldron.PumpkinPrefab, 1.0f, 0.7f);
                    Debug.Log("[Cauldron Defence] The recipe needs a tree pumpkin and none was about for " + sparePumpkinAfter + " s: the pot spat one out.", this);
                }
            }
            else noPumpkinSince = -1f;
        }

        // An ingredient of this kind lies on the PlayGround (within 30 m of the pot) or is held.
        private bool IngredientAbout(string id)
        {
            foreach (var ingredient in FindObjectsByType<CookingIngredient>(FindObjectsSortMode.None))
            {
                if (ingredient == null || !ingredient.isActiveAndEnabled || ingredient.Id != id) continue;
                if (FlatDistance(ingredient.transform.position, transform.position) <= 30f) return true;
            }
            return false;
        }

        // Something hops out over the pot's rim towards the player with a puff (it lands about 1-2 m from the pot; a softer
        // throw for something that breaks, like a tree pumpkin).
        private GameObject SpitTowardsPlayer(GameObject prefab, float height, float speed = 1f)
        {
            var eye = Head();
            var toward = eye != null ? eye.position - transform.position : transform.forward;
            toward.y = 0f;
            toward = toward.sqrMagnitude > 0.0001f ? toward.normalized : Vector3.forward;
            var from = transform.position + Vector3.up * height + toward * 0.9f;
            var made = Instantiate(prefab, from, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), SpawnedStuff());
            if (made.TryGetComponent<Rigidbody>(out var body) && !body.isKinematic) body.linearVelocity = (toward * 2.2f + Vector3.up * 3f) * speed;
            if (spawnEffect != null) EffectPool.Play(spawnEffect, from, spawnEffectScale);
            if (popSound != null) AudioSource.PlayClipAtPoint(popSound, from, popVolume * 0.5f);
            return made;
        }

        // A new magic hat pops up 3-5 m from the pot, on the floor, lying any way (the old one is gone).
        private void NewHat()
        {
            var spot = transform.position + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * Random.Range(3f, 5f);
            for (int i = 0; i < 8; i++)
            {
                var at = transform.position + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * Random.Range(3f, 5f);
                if (!NavMesh.SamplePosition(at, out var onMesh, 1.5f, NavMesh.AllAreas)) continue;
                spot = onMesh.position;
                break;
            }
            var hat = Instantiate(hatPrefab, spot + Vector3.up * 0.05f, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), SpawnedStuff());
            if (hat.TryGetComponent<MagicHat>(out var magic)) magic.LieAnyWay();
            if (spawnEffect != null) EffectPool.Play(spawnEffect, spot + Vector3.up * 0.3f, spawnEffectScale * 2f);
            if (popSound != null) AudioSource.PlayClipAtPoint(popSound, spot, popVolume * 0.6f);
            Debug.Log("[Cauldron Defence] The magic hat was gone with its recipe unread: a new one popped up " + FlatDistance(spot, transform.position).ToString("0.0") + " m from the pot.", this);
        }

        // ------------------------------------------------------------------ 3-4. the countdown

        private void OnCookingStarted(Cauldron who)
        {
            if (who != cauldron || running || over) return;
            running = true;
            playerNear = true;
            health.Arm();
            if (!wallsDown)
            {
                // Once: they stay down (the gate flow cooks a dish each round).
                wallsDown = true;
                var field = FindAnyObjectByType<PlayGroundField>();
                if (field != null) field.WallsDown(0);
            }
            EnemyWave.Start(this);
            waves = StartCoroutine(RunWaves());
            if (driven) step = RoundStep.Cooking;
            Debug.Log("[Cauldron Defence] The countdown started: " + cauldron.DishSeconds.ToString("0") + " s, walls down, monsters coming.", this);
            Started?.Invoke(this);
        }

        // Non-stop: one whenever there's room, harder as it cooks; after a loss the last phase goes on, fuller and faster.
        private IEnumerator RunWaves()
        {
            if (firstSpawnAfter > 0f) yield return new WaitForSeconds(firstSpawnAfter);
            while (!over && running)
            {
                if (!WaveNow(out var enemies, out int most, out var spawnWait))
                {
                    yield return new WaitForSeconds(1f);
                    continue;
                }
                if (doomed) most += doomExtraAlive;
                if (Minions() >= most || !EnemyLimit.TryTakeSlot())
                {
                    yield return new WaitForSeconds(0.25f);
                    continue;
                }
                Spawn(Pick(enemies), false);
                float wait = Random.Range(spawnWait.x, Mathf.Max(spawnWait.x, spawnWait.y));
                yield return new WaitForSeconds(wait * (doomed ? doomSpawnWaitShare : 1f));
            }
        }

        // The monsters now: the gate flow's round's (until a loss), else the phase for the share cooked (the last one
        // after a loss).
        private bool WaveNow(out GameObject[] enemies, out int most, out Vector2 spawnWait)
        {
            if (driven && plan != null && !doomed)
            {
                enemies = plan.enemies;
                most = plan.maxAlive;
                spawnWait = plan.spawnWait;
                return enemies != null && enemies.Length > 0;
            }
            int index = CurrentPhaseIndex();
            var phase = index >= 0 ? phases[index] : null;
            enemies = phase != null ? phase.enemies : null;
            most = phase != null ? phase.maxAlive : 0;
            spawnWait = phase != null ? phase.spawnWait : Vector2.one;
            return enemies != null && enemies.Length > 0;
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
            foreach (var boss in roundBosses)
            {
                if (EnemyWave.IsAlive(boss)) alive--;
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
            bool potStands = !over && !doomed && !health.IsBroken;
            var cauldronSpot = !playerNear && potStands ? transform : null;
            potBiters.RemoveWhere(b => b == null);
            foreach (var brain in brains)
            {
                if (brain == null) continue;
                var target = potStands && potBiters.Contains(brain) ? transform : cauldronSpot;
                brain.GoFor(target, standOff);
                // After the player: make sure it has noticed them (spawned at the far edge, it may not see them yet).
                if (target == null) brain.Notice();
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
            if (driven)
            {
                // The gate flow: the dish is done, the round's mini bosses come out.
                if (step == RoundStep.Cooking) StartRoundBosses();
                return;
            }
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
            FinaleSpot(out var spot, out var area, out var middle);
            GateBossFinale.Begin(finaleBoss, finaleSeed, spot, finaleDelay, boss => SetUpFinaleBoss(boss, area, middle),
                at => area != null && !area.ContainsFlat(at, 1f) ? middle : at, "[Cauldron Defence]");
        }

        // The floor where the pot stands (not the pot itself: it's still there as this runs), the PlayGround's roam area
        // and its middle on that floor.
        private void FinaleSpot(out Vector3 spot, out GhostRoamArea area, out Vector3 middle)
        {
            spot = transform.position;
            float best = float.NegativeInfinity;
            foreach (var hit in Physics.RaycastAll(spot + Vector3.up * 2f, Vector3.down, 10f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(transform) || hit.rigidbody != null) continue;
                if (hit.point.y > best) best = hit.point.y;
            }
            if (!float.IsNegativeInfinity(best)) spot.y = best;
            area = GhostRoamArea.Around(transform.position);
            middle = area != null ? new Vector3(area.Middle.x, spot.y, area.Middle.z) : spot;
        }

        private void SetUpFinaleBoss(PumpkinBallBoss boss, GhostRoamArea area, Vector3 middle)
        {
            boss.Resize(finaleRadius);
            if (finaleCandies > 0) boss.breakCandies = finaleCandies;
            if (finaleHealth > 0f) boss.health = finaleHealth;
            boss.arenaCentre = middle;
            // On the PlayGround, all of it inside its roam area.
            if (area != null) boss.canBeAt = p => area.ContainsFlat(p, boss.radius);
        }

        // ------------------------------------------------------------------ the gate flow's rounds (2026-11-23)

        /// <summary>
        /// A round of the gate flow (Play Ground Game): a new recipe (read off the magic hat again), its cook with its
        /// monsters, then its mini bosses; <paramref name="won"/> is called when they're all beaten. Ignored after a loss or
        /// once the finale has begun.
        /// </summary>
        public void BeginRound(Round round, Action won)
        {
            if (round == null || over || doomed) return;
            driven = true;
            StopWaves(false);
            plan = round;
            roundWon = won;
            step = RoundStep.Gathering;
            roundBosses.Clear();
            bossesToCome.Clear();
            roundBossesBeaten = 0;
            health.Disarm();
            cauldron.UseOneDish(round.candies, round.pumpkins, round.cookSeconds);
            Debug.Log("[Cauldron Defence] Round " + round.label + ": " + round.candies + " candies + " + round.pumpkins +
                      " tree pumpkin(s), cooks " + round.cookSeconds + " s, then " + (round.miniBosses != null ? round.miniBosses.Length : 0) +
                      " mini boss(es).", this);
        }

        /// <summary>
        /// The gate flow's round ran out of time: its mini bosses and monsters go (no reward for the bosses) and nothing
        /// cooks on. After a loss the doom goes on.
        /// </summary>
        public void EndRoundNow()
        {
            if (!driven || doomed || over || step == RoundStep.Done) return;
            foreach (var boss in roundBosses)
            {
                if (!EnemyWave.IsAlive(boss)) continue;
                Game.Leveling.PlayerLevel.NoRewardFor(boss);
                EnemyWave.Kill(boss);
            }
            bossesToCome.Clear();
            cauldron.StopDish();
            EndRound(false);
        }

        /// <summary>
        /// After round 3 of the gate flow: the field is swept (the Fairy Shield's purple hole), the pot swells and bursts,
        /// and the gate's giant pumpkin drops where it stood (Gate Run's finale: its own giant pumpkin and seed).
        /// </summary>
        public void FinaleFor(GateRun run)
        {
            if (run == null || over || doomed) return;
            over = true;
            step = RoundStep.Done;
            StopWaves(true);
            health.Disarm();
            if (cleanWithFairyShield) FireFairyShield();
            SlimeBody.ClearAll();
            if (TryGetComponent(out SpecialStage3Trigger daruma)) daruma.enabled = false;
            Debug.Log("[Cauldron Defence] The 3 dishes are cooked (" + health.Health.ToString("0") + "/" + health.MaxHealth.ToString("0") +
                      " pot health): the pot bursts in " + burstDelay + " s, then the giant pumpkin.", this);
            Won?.Invoke(this);
            StartCoroutine(BurstThen(run));
        }

        private IEnumerator BurstThen(GateRun run)
        {
            if (burstDelay > 0f) yield return new WaitForSeconds(burstDelay);
            cauldron.Burst();
            FinaleSpot(out var spot, out var area, out var middle);
            run.Finale(spot, finaleDelay, boss => SetUpFinaleBoss(boss, area, middle),
                at => area != null && !area.ContainsFlat(at, 1f) ? middle : at);
        }

        // The dish is done: the round's mini bosses come out, one at a time at the open gates (see Watch Round Bosses).
        private void StartRoundBosses()
        {
            step = RoundStep.Bosses;
            bossesToCome.Clear();
            if (plan != null && plan.miniBosses != null)
            {
                foreach (var boss in plan.miniBosses) if (boss != null) bossesToCome.Add(boss);
            }
            if (bossesToCome.Count == 0) EndRound(true);
        }

        // The next mini boss out when there's a spot for it; the round is won when all of them are beaten.
        private void WatchRoundBosses()
        {
            if (bossesToCome.Count > 0)
            {
                var made = Spawn(bossesToCome[0], true);
                if (made != null)
                {
                    bossesToCome.RemoveAt(0);
                    roundBosses.Add(made);
                    if (bossSound != null) AudioSource.PlayClipAtPoint(bossSound, made.transform.position, 1f);
                    Debug.Log("[Cauldron Defence] Mini boss " + made.name + " came out (round " + plan?.label + ").", this);
                    MiniBossCame?.Invoke(this, made);
                }
            }
            int beaten = 0;
            foreach (var boss in roundBosses) if (!EnemyWave.IsAlive(boss)) beaten++;
            roundBossesBeaten = beaten;
            if (bossesToCome.Count == 0 && beaten >= roundBosses.Count) EndRound(true);
        }

        // The round is over: its monsters go (they drop what they drop), the pot rests until the next round.
        private void EndRound(bool won)
        {
            step = RoundStep.Done;
            StopWaves(true);
            health.Disarm();
            Debug.Log("[Cauldron Defence] Round " + plan?.label + (won ? " won" : " over (its time ran out)") + "; the pot has " +
                      health.Health.ToString("0") + "/" + health.MaxHealth.ToString("0") + " health.", this);
            var tell = roundWon;
            roundWon = null;
            if (won) tell?.Invoke();
        }

        // The waves stop (a round is over, or the finale); with Kill the monsters still out die too.
        private void StopWaves(bool kill)
        {
            if (waves != null) StopCoroutine(waves);
            waves = null;
            running = false;
            if (kill) EnemyWave.KillAll(this);
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
            // They sit under GAMESTAGES: looked for there (a search of the whole scene hitched at the win). The smoke is
            // only switched on if it's there too (the purple hole on the shield hides it anyway).
            var stages = GameStages.Fsm();
            if (stages != null)
            {
                foreach (var t in stages.GetComponentsInChildren<Transform>(true))
                {
                    if (shield == null && t.name == shieldName && t.CompareTag("Fairy_Shield")) shield = t.gameObject;
                    else if (smoke == null && !string.IsNullOrEmpty(shieldSmokeName) && t.name == shieldSmokeName) smoke = t.gameObject;
                }
            }
            if (shield == null)
            {
                foreach (var candidate in Resources.FindObjectsOfTypeAll<GameObject>())
                {
                    if (!candidate.scene.IsValid() || candidate.name != shieldName || !candidate.CompareTag("Fairy_Shield")) continue;
                    shield = candidate;
                    break;
                }
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
            StartCoroutine(WatchChaosBoss(chaosBossMade));
        }

        // Killing the chaos boss only ends the gate (user, 2026-11-23; it used to win the whole game). Once it has burst
        // and gone: the gate's monsters go, the teleport ground comes back, Gate Run's rounds stop, and The_Seed drops where
        // it fell (inside the field); Gate Exit takes the player to the rooftop, as after the giant pumpkin. Gone in a Game
        // Over: nothing (the new run clears everything). The giant pumpkin already out, or a Gate Exit under way: that ends
        // the gate instead.
        private IEnumerator WatchChaosBoss(GameObject boss)
        {
            if (boss == null) yield break;
            var last = boss.transform.position;
            while (boss != null)
            {
                last = boss.transform.position;
                yield return null;
            }
            if (over || GameStages.ActiveState() == "Game Over") yield break;
            var run = GateRun.Current;
            if (GateExit.Running || (run != null && run.FinaleRun != null)) yield break;
            EndGateByChaosBoss(last, run);
        }

        private void EndGateByChaosBoss(Vector3 at, GateRun run)
        {
            over = true;
            step = RoundStep.Done;
            foreach (var boss in roundBosses)
            {
                if (!EnemyWave.IsAlive(boss)) continue;
                Game.Leveling.PlayerLevel.NoRewardFor(boss);
                EnemyWave.Kill(boss);
            }
            bossesToCome.Clear();
            StopWaves(true);
            SlimeBody.ClearAll();
            GiveTeleportBack();
            // Gate Run's flow (its rounds' clocks, chests and finale) stops: the gate is over. Its HUD hides while Gate Exit
            // runs, and it goes with the PlayGround.
            if (run != null) run.StopAllCoroutines();
            FinaleSpot(out _, out var area, out var middle);
            if (area != null && !area.ContainsFlat(at, 1f)) at = new Vector3(middle.x, at.y, middle.z);
            if (NavMesh.SamplePosition(at, out var onMesh, 3f, NavMesh.AllAreas)) at = onMesh.position;
            var seed = finaleSeed != null ? finaleSeed : run != null ? run.finaleSeed : null;
            // Its own object under Currently Spawned Stuffs (a Game Over clears it) until Gate Exit makes it stand alone.
            var end = new GameObject("Chaos Boss Exit");
            var spawned = SpawnedStuff();
            if (spawned != null) end.transform.SetParent(spawned, false);
            end.transform.position = at;
            GateExit.Begin(end, seed, at, "[Cauldron Defence]");
            Debug.Log("[Cauldron Defence] The chaos boss is beaten: the gate is over (The_Seed, then the rooftop).", this);
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
                // Straight into the fight, at whoever it's sent at (a mini boss hunts the player by itself). Some always go
                // for the cauldron (Pot Biter Share).
                bool biter = !isBoss && !brain.IsAnomaly && !doomed && Random.value < potBiterShare;
                if (biter) potBiters.Add(brain);
                if (!isBoss && !brain.IsAnomaly && (!playerNear || biter) && !doomed) brain.GoFor(transform, standOff);
                brain.ComeAtOnce();
            }
            return enemy;
        }

        // A spot on the PlayGround's edge (its Ghost Roam Area), on the NavMesh, not right next to the player.
        private bool SpawnPoint(out Vector3 at)
        {
            if (spawnFromGates && GateSpawnPoint(out at)) return true;
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

        // A spot at one of the open monster gates (the field's corners), on the NavMesh, not right next to the player. The
        // other gates are tried when the player stands in the way of the open ones; false without a measured field.
        private bool GateSpawnPoint(out Vector3 at)
        {
            at = default;
            if (!FindGates()) return false;
            UpdateOpenGates();
            var eye = Head();
            var headAt = eye != null ? eye.position : transform.position;
            var order = new List<int>(openGateIndex);
            Shuffle(order);
            var others = new List<int>();
            for (int i = 0; i < gateSpots.Count; i++) if (!order.Contains(i)) others.Add(i);
            Shuffle(others);
            order.AddRange(others);
            foreach (int g in order)
            {
                for (int k = 0; k < 4; k++)
                {
                    var spot = gateSpots[g] + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * Random.Range(0f, gateSpread);
                    if (Physics.Raycast(spot + Vector3.up * 1f, Vector3.down, out var floor, 15f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                        spot = floor.point;
                    if (!NavMesh.SamplePosition(spot, out var hit, 2f, NavMesh.AllAreas)) continue;
                    if (FlatDistance(hit.position, headAt) < minPlayerDistance) continue;
                    at = hit.position;
                    return true;
                }
            }
            return false;
        }

        // The 4 corners of the PlayGround's field (as its walls stand), Gate Inset metres in.
        private bool FindGates()
        {
            if (gateSpots.Count > 0) return true;
            var field = FindAnyObjectByType<PlayGroundField>();
            if (field == null || field.Field.width < 2f || field.Field.height < 2f) return false;
            var r = field.Field;
            float k = Mathf.Min(gateInset, Mathf.Min(r.width, r.height) * 0.25f);
            float y = transform.position.y;
            gateSpots.Add(new Vector3(r.xMin + k, y, r.yMin + k));
            gateSpots.Add(new Vector3(r.xMax - k, y, r.yMin + k));
            gateSpots.Add(new Vector3(r.xMax - k, y, r.yMax - k));
            gateSpots.Add(new Vector3(r.xMin + k, y, r.yMax - k));
            return true;
        }

        // The phase's number of open gates (Open Gates; all of them after a loss), moved to other corners every Gate Switch
        // Seconds. A gate that opens puffs, so the player sees where the monsters come from now.
        private void UpdateOpenGates()
        {
            int phase = Mathf.Max(0, CurrentPhaseIndex());
            bool byRound = driven && plan != null && !doomed;
            int want = byRound ? plan.openGates
                : openGates != null && openGates.Length > 0 ? openGates[Mathf.Min(phase, openGates.Length - 1)] : gateSpots.Count;
            if (doomed) want = gateSpots.Count;
            want = Mathf.Clamp(want, 1, gateSpots.Count);
            object key = byRound ? (object)plan : phase;
            if (openGateIndex.Count == want && Equals(gatesKey, key) && Time.time < nextGateSwitch) return;
            var before = new List<int>(openGateIndex);
            var fresh = new List<int>();
            var stay = new List<int>();
            for (int i = 0; i < gateSpots.Count; i++) (before.Contains(i) ? stay : fresh).Add(i);
            Shuffle(fresh);
            Shuffle(stay);
            fresh.AddRange(stay);
            openGateIndex.Clear();
            for (int i = 0; i < want; i++) openGateIndex.Add(fresh[i]);
            gatesKey = key;
            nextGateSwitch = Time.time + gateSwitchSeconds;
            foreach (int g in openGateIndex)
            {
                if (before.Contains(g) || spawnEffect == null) continue;
                EffectPool.Play(spawnEffect, gateSpots[g] + Vector3.up * 0.4f, spawnEffectScale * 2.5f);
            }
            GatesMoved?.Invoke(this);
        }

        private static void Shuffle(List<int> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
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

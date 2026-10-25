using System.Collections;
using System.Collections.Generic;
using Game.Combat;
using Game.Inventory;
using Game.Leveling;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace Game.Bonus
{
    /// <summary>
    /// The nesting pumpkin bonus round (Bonus_Round). The Play Bonus Round action makes it when the player earned it,
    /// together with the bonus stage (e.g. --Bedroom--MiniGame--: the field and the pumpkin chest):
    /// - punching the stage's chest open lets the giant laughing pumpkin out (Giant Times the start pumpkin's size). It
    ///   grows out of the chest, throwing the player clear if they stand where it grows, and the countdown (Seconds)
    ///   starts at once;
    /// - every pumpkin broken bursts, drops its candies (its Enemy Hit Damage gives the EXP), adds Bonus Seconds to the
    ///   countdown, and the next one inside it pops out where it was: smaller (Shrink Each less than the one before)
    ///   but tougher (more health, less jiggle; the giant jiggles most), down to the last one (Count);
    /// - every pumpkin broken gives the player Helmet Seconds more of the invisible Candy Helmet (a timed buff that stacks:
    ///   every candy around flies into their mouth, even ones still coming out of the pumpkin);
    /// - when they're all broken or time's up (the one left blows up, throwing the player back) the player keeps what they got: the helmet eats
    ///   every candy left, then the round is over (Finished) and Play Bonus Round goes on. The Bonus Board shows how many
    ///   punches the pumpkins took and how many were perfect jabs, with the player's leaderboard (saved, Bonus Scores).
    /// A player earns it in a stage by killing every enemy before taking the Yellow Seed: the Watch Bonus Round action
    /// (in the stage's enemy-wave state) notes the seeds and the enemies; Play Bonus Round asks Earned.
    /// </summary>
    [DisallowMultipleComponent]
    public class BonusRound : MonoBehaviour
    {
        [Header("Start")]
        [Tooltip("Seconds the giant takes to burst out of the chest (it shoots up, swells past its size and lands wobbling).")]
        [Min(0.05f)]
        [SerializeField] private float growSeconds = 0.45f;
        [Tooltip("A pumpkin coming where the player stands throws them clear of it: this high, in metres. 0 = no throw.")]
        [Min(0f)]
        [SerializeField] private float launchHeight = 1.2f;
        [Tooltip("... landing this far clear of the pumpkin's side, in metres.")]
        [Min(0f)]
        [SerializeField] private float launchMargin = 1f;
        [Tooltip("The giant bursting out shoves the player back if they're within this many metres of its side (inside it: " +
                 "thrown clear as above).")]
        [Min(0f)]
        [SerializeField] private float giantPushReach = 2f;
        [Tooltip("... this high (x) and at least this far (y), in metres. Their Toughness doesn't cut it.")]
        [SerializeField] private Vector2 giantPush = new(0.7f, 2.5f);

        [Header("Pumpkins")]
        [Tooltip("The giant, the first one (Nesting_Pumpkin_1): the stage's chest makes it.")]
        [SerializeField] private NestingPumpkin giant;
        [Tooltip("Every pumpkin after the giant (Nesting_Pumpkin_2): each pops out where the last one broke.")]
        [SerializeField] private NestingPumpkin smaller;
        [Tooltip("The start pumpkin (StartGamePumpkin): the giant is Giant Times its size.")]
        [SerializeField] private GameObject startPumpkin;
        [Min(0.1f)]
        [SerializeField] private float giantTimes = 1.3f;
        [Tooltip("How many pumpkins there are: the giant and the ones inside it.")]
        [Min(1)]
        [SerializeField] private int count = 9;
        [Tooltip("Each pumpkin is this much smaller than the one before (0.1 = 10%: 100%, 90%, 81%, 73% ...).")]
        [Range(0.01f, 0.5f)]
        [SerializeField] private float shrinkEach = 0.1f;
        [Tooltip("The giant's health. The smaller, the tougher: each one after it has Tougher Per Pumpkin of this more.")]
        [Min(1f)]
        [SerializeField] private float giantHealth = 120f;
        [Tooltip("Each pumpkin has this share of the giant's health more than the one before (0.125 = +12.5%: 120, 135, " +
                 "150 ... 240 for the 9th).")]
        [Range(0f, 1f)]
        [SerializeField] private float tougherPerPumpkin = 0.125f;
        [Tooltip("How much the giant jiggles when hit (1 = as its Jiggle is set; more = wobblier).")]
        [Min(0f)]
        [SerializeField] private float giantJiggle = 1.6f;
        [Tooltip("The giant itself (the first) jiggles this many times more on top (the ones inside it don't).")]
        [Min(0f)]
        [SerializeField] private float giantExtraJiggle = 1.5f;
        [Tooltip("... and each one after it jiggles this share of the giant's jiggle less (0.09 = 100%, 91%, 82% ...).")]
        [Range(0f, 0.3f)]
        [SerializeField] private float stifferPerPumpkin = 0.09f;
        [Tooltip("Candies each pumpkin drops (the last one: Last Candies).")]
        [Min(0)]
        [SerializeField] private int candiesEach = 3;
        [Min(0)]
        [SerializeField] private int lastCandies = 15;
        [Tooltip("EXP each pumpkin gives (the last one: Last Exp).")]
        [Min(0)]
        [SerializeField] private int expEach = 3;
        [Min(0)]
        [SerializeField] private int lastExp = 25;
        [Tooltip("The last one's laugh pitch; they go up to it from the giant's 1.")]
        [Range(0.5f, 2f)]
        [SerializeField] private float lastPitch = 1.6f;
        [Tooltip("Seconds between a pumpkin breaking and the next one popping out where it was.")]
        [Min(0f)]
        [SerializeField] private float nextPumpkinDelay = 0.8f;
        [Tooltip("One of these, picked at random, per candy.")]
        [SerializeField] private GameObject[] candies = System.Array.Empty<GameObject>();
        [Tooltip("How hard the candies pop up out of a broken pumpkin (metres per second). Candies break on a hard " +
                 "landing, so keep it low.")]
        [Min(0f)]
        [SerializeField] private float candyLaunchSpeed = 2.2f;

        [Header("Time")]
        [Tooltip("The countdown, in seconds. It starts the moment the giant comes out.")]
        [Min(1f)]
        [SerializeField] private float seconds = 30f;
        [Tooltip("Seconds added for every pumpkin broken.")]
        [Min(0f)]
        [SerializeField] private float bonusSeconds = 6f;
        [Tooltip("Played when seconds are added.")]
        [SerializeField] private AudioClip bonusSound;
        [Tooltip("The countdown blinks red and ticks for this many last seconds.")]
        [Min(0f)]
        [SerializeField] private float warnSeconds = 10f;
        [SerializeField] private AudioClip tickSound;
        [Range(0f, 1f)]
        [SerializeField] private float tickVolume = 0.6f;

        [Header("Candy Helmet")]
        [Tooltip("Seconds of the Candy Helmet (the candy vacuum: every candy around flies into the player's mouth) each " +
                 "pumpkin broken gives. They add up: two pumpkins quickly one after the other, 10 s. 0 = none.")]
        [Min(0f)]
        [SerializeField] private float helmetSeconds = 5f;
        [Tooltip("Played when the helmet comes on (not when a pumpkin only adds to it).")]
        [SerializeField] private AudioClip helmetSound;

        [Header("Countdown sign")]
        [SerializeField] private TMP_FontAsset font;
        [Tooltip("How high above the pumpkin's top the sign floats (metres); it comes down as they get smaller.")]
        [SerializeField] private float signAbove = 0.9f;
        [Tooltip("The lowest the sign goes (metres above the floor).")]
        [SerializeField] private float signLowest = 2.6f;
        [Tooltip("The sign's size (metres per panel pixel).")]
        [Min(0.0001f)]
        [SerializeField] private float signScale = 0.007f;

        [Header("End")]
        [Tooltip("At the end the helmet eats every candy left before the stage goes on: for this many seconds at the most.")]
        [Min(0f)]
        [SerializeField] private float candyWaitSeconds = 20f;
        [Tooltip("A puff where things appear and go, e.g. Smoke_Blast.")]
        [SerializeField] private GameObject puffEffect;
        [Tooltip("Seconds the board shows when the round is over: the punches the pumpkins took, the perfect jabs and the " +
                 "player's leaderboard (saved, see Bonus Scores). 0 = no board (the round still goes on the leaderboard).")]
        [Min(0f)]
        [SerializeField] private float boardSeconds = 12f;

        [Header("Time's up: the pumpkin left blows up")]
        [Tooltip("Particle effects where it blows up (the grenades': Flash_round_yellow, Hit_Blast, Smoke_Blast).")]
        [SerializeField] private GameObject[] blastEffects = System.Array.Empty<GameObject>();
        [Tooltip("Size of the blast effects per metre of the pumpkin's size.")]
        [Min(0f)]
        [SerializeField] private float blastEffectSize = 1.2f;
        [SerializeField] private AudioClip blastSound;
        [Range(0f, 1f)]
        [SerializeField] private float blastVolume = 1f;
        [Tooltip("A player this close to it (metres, from its side) is thrown...")]
        [Min(0f)]
        [SerializeField] private float blastReach = 6f;
        [Tooltip("...this high (metres)...")]
        [Min(0f)]
        [SerializeField] private float blastHeight = 1.6f;
        [Tooltip("...and this far away from it (metres). Their Toughness doesn't cut it.")]
        [Min(0f)]
        [SerializeField] private float blastDistance = 4f;
        [Tooltip("How fast its pieces fly off, in metres per second.")]
        [Min(0f)]
        [SerializeField] private float blastPush = 7f;

        private static readonly Color SignColor = new(1f, 0.82f, 0.25f, 1f);
        private static readonly Color WarnColor = new(1f, 0.25f, 0.2f, 1f);
        private static readonly Color BonusColor = new(0.45f, 1f, 0.45f, 1f);
        private const float PanelWidth = 220f;
        private const float PanelHeight = 110f;
        // Seconds a broken pumpkin gets to burst by itself before it's taken away.
        private const float BurstWait = 3f;
        // The candy fountain: ring radius and height (metres) and the time between candies (seconds).
        private const float CandyRing = 0.35f;
        // ... as wide as the pumpkin that broke (its scale times this), so they land clear of the next one.
        private const float CandyRingPerSize = 0.55f;
        // Half a nesting pumpkin's width per unit of its size (the start pumpkin's mesh), in metres.
        private const float RadiusPerSize = 0.53f;
        // Smallest share of the giant's size a pumpkin can have, and of its jiggle.
        private const float SmallestShare = 0.05f;
        private const float MinJiggle = 0.1f;
        private const float BiggestPuff = 3.5f;
        // The start pumpkin's burst pieces (Nesting_Pumpkin_N > pumpkin_Start > pumpkin_0_drop).
        private const string PiecesName = "pumpkin_0_drop";
        private const string CandyName = "Candy_";
        private const float CandyHeight = 0.3f;
        private const float CandyGap = 0.08f;
        // How far from the pumpkins the candies left at the end are looked for, in metres.
        private const float CandyReach = 15f;
        private const float PopSeconds = 1.6f;
        // The camera shake of a blast that doesn't throw the player (one that does shakes by itself).
        private const string BlastShake = "ShakeBigger";
        // The longest a throw clear of a pumpkin goes through it, in seconds.
        private const float ThrowSeconds = 3f;
        private const float DefaultBodyRadius = 0.25f;

        /// <summary>The round running now (a pumpkin the stage's chest makes asks it, see TakeGiant).</summary>
        public static BonusRound Running { get; private set; }

        /// <summary>The round is over (all broken or out of time) and the candies are eaten: Play Bonus Round goes on.</summary>
        public bool Finished { get; private set; }

        /// <summary>Pumpkins broken so far.</summary>
        public int Broken { get; private set; }

        /// <summary>Punches the pumpkins took during the countdown (a punch's flying fists count once).</summary>
        public int Punches { get; private set; }

        /// <summary>... and how many of them were perfect jabs (see Fist's Perfect Jab).</summary>
        public int PerfectJabs { get; private set; }

        /// <summary>Seconds left on the countdown.</summary>
        public float TimeLeft => timeLeft;

        /// <summary>How many pumpkins this round has.</summary>
        public int PumpkinCount => Mathf.Max(1, count);

        private RectTransform sign;
        private TMP_Text signText;
        private TMP_Text popText;
        private float popUntil;
        private float popFor = 1f;
        private float flashUntil;
        private AudioSource voice;
        private Transform head;
        private NestingPumpkin current;
        // The pumpkin there now: its candies and size (kept, as it can be gone the moment it breaks).
        private int currentCandies;
        private float currentSize = 1f;
        // Where the pumpkins are (where the giant came out, then where the last one broke).
        private Vector3 spot;
        // The colliders of the candies dropped so far (gone ones are cleared now and then).
        private readonly List<Collider> liveCandies = new();
        // Candy fountains still going.
        private int fountains;
        private float timeLeft;
        private int lastTick = -1;
        private float signY;
        // Punches count while the countdown runs; the strokes already counted.
        private bool counting;
        private readonly HashSet<int> countedStrokes = new();

        // ---------- Earning it (the Watch Bonus Round and Play Bonus Round actions) ----------

        private static int seedsAtStart = -1;
        private static int aliveAtSeed = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            seedsAtStart = -1;
            aliveAtSeed = -1;
            Running = null;
        }

        /// <summary>A stage's enemy waves start: notes the player's Yellow Seeds.</summary>
        public static void NoteStageStart()
        {
            seedsAtStart = YellowSeeds();
            aliveAtSeed = -1;
        }

        /// <summary>The stage's seed was taken (its wave state ends): notes how many enemies were still alive.</summary>
        public static void NoteSeedTaken() => aliveAtSeed = AliveEnemies();

        /// <summary>
        /// The player earned the bonus round in the stage just played: they took a Yellow Seed (one that timed out
        /// doesn't count) and no enemy was alive at that moment.
        /// </summary>
        public static bool Earned => seedsAtStart >= 0 && aliveAtSeed == 0 && YellowSeeds() > seedsAtStart;

        /// <summary>Forgets the stage, so the next one starts over.</summary>
        public static void ForgetStage()
        {
            seedsAtStart = -1;
            aliveAtSeed = -1;
        }

        private static int YellowSeeds()
        {
            var inventory = PlayerInventory.Instance;
            var seed = inventory != null && inventory.Database != null ? inventory.Database.Find("Yellow_Seed") : null;
            return seed != null ? inventory.GetCount(seed) : 0;
        }

        // Enemies still alive: bugs and bosses that give EXP (not furniture, doors or the start pumpkin, not the ones a
        // broken wall's portal keeps letting in, and not the rare white bug).
        private static int AliveEnemies()
        {
            int alive = 0;
            foreach (var enemy in FindObjectsByType<EnemyHitDamage>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (enemy.enabled && enemy.CountsAsKill && !enemy.IsDead && !Game.Enemies.WallPortal.IsPortalEnemy(enemy.gameObject) &&
                    !Game.Enemies.SpiderBrain.IsAnomalyBug(enemy.gameObject)) alive++;
            }
            return alive;
        }

        // ---------- The pumpkins ----------

        /// <summary>Pumpkin number 'index' (0 = the giant): its share of the giant's size.</summary>
        public float ShareOf(int index) => Mathf.Max(SmallestShare, Mathf.Pow(1f - shrinkEach, index));

        /// <summary>The giant's size (its scale): Giant Times the start pumpkin's.</summary>
        public float GiantSize
        {
            get
            {
                if (startPumpkin != null) return giantTimes * startPumpkin.transform.localScale.x;
                return giant != null ? giant.transform.localScale.x * giantTimes : giantTimes;
            }
        }

        /// <summary>Pumpkin number 'index''s health: the smaller, the tougher (Tougher Per Pumpkin more each).</summary>
        public float HealthOf(int index) => Mathf.Round(giantHealth * (1f + tougherPerPumpkin * index));

        /// <summary>How much pumpkin number 'index' jiggles when hit: the giant most, the smaller ones stiffer.</summary>
        public float JiggleOf(int index) =>
            Mathf.Max(MinJiggle, giantJiggle * (1f - stifferPerPumpkin * index)) * (index == 0 ? giantExtraJiggle : 1f);

        /// <summary>
        /// Called by a nesting pumpkin something else made (the bonus stage's chest), before its FSMs start: it becomes
        /// this round's giant if the round is still waiting for one (false: it isn't, and the pumpkin stays as it is). It
        /// stands up on the floor (this object's height) and grows out of where it is; the countdown starts.
        /// </summary>
        public bool TakeGiant(NestingPumpkin pumpkin)
        {
            if (pumpkin == null || current != null || Broken > 0 || Finished || !isActiveAndEnabled) return false;
            var at = pumpkin.transform.position;
            at.y = transform.position.y;
            pumpkin.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, pumpkin.transform.eulerAngles.y, 0f));
            Prepare(pumpkin, 0);
            pumpkin.GrowHere(growSeconds, true);
            pumpkin.Setup();
            // Candies already lying about (e.g. the stage's) are looked after like the pumpkins' (pumpkins go through them).
            AdoptCandies();
            Took(pumpkin);
            return true;
        }

        // ---------- The round ----------

        private void Awake()
        {
            Running = this;
        }

        private void Start()
        {
            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;
            MakeSign();
            StartCoroutine(Run());
        }

        private void OnEnable()
        {
            Fist.Landed += OnPunchLanded;
        }

        private void OnDisable()
        {
            Fist.Landed -= OnPunchLanded;
        }

        // A punch on the pumpkin there now (the fist, or its flying fists: one punch counts once).
        private void OnPunchLanded(Fist.PunchLanded punch)
        {
            if (!counting || current == null || punch.Hit == null || punch.Hit.GetComponentInParent<NestingPumpkin>() != current) return;
            if (punch.Stroke != 0 && !countedStrokes.Add(punch.Stroke)) return;
            Punches++;
            if (punch.Perfect) PerfectJabs++;
        }

        private void OnDestroy()
        {
            if (Running == this) Running = null;
            if (sign != null) Destroy(sign.gameObject);
            // Gone before it was over (the stage was left, e.g. by a shortcut): its pumpkin goes with it.
            if (!Finished && current != null && !current.IsBroken) Destroy(current.gameObject);
        }

        private IEnumerator Run()
        {
            // The stage's chest lets the giant out (it hands itself over, see TakeGiant): the countdown starts at once.
            while (current == null) yield return null;
            timeLeft = seconds;
            sign.gameObject.SetActive(true);
            ShowTime();
            counting = true;

            bool won = false;
            // The one there gets broken, bursts (its own Health FSM), then the next pops out where it was.
            bool waitingForBurst = false;
            float brokeAt = 0f;
            float nextAt = -1f;
            var lastSeen = spot;
            int total = PumpkinCount;
            while (true)
            {
                timeLeft -= Time.deltaTime;
                if (current != null && current.Body != null) lastSeen = current.Body.position;
                // Broken: its health ran out, or it's already gone (its FSM can burst it the same frame the hit lands).
                if (!waitingForBurst && nextAt < 0f && (current == null || current.IsBroken))
                {
                    Broken++;
                    var at = lastSeen;
                    Puff(at, currentSize * 1.2f);
                    DropCandies(at, currentCandies);
                    GiveHelmet();
                    if (Broken >= total)
                    {
                        won = true;
                        break;
                    }
                    AddSeconds();
                    spot = new Vector3(at.x, spot.y, at.z);
                    waitingForBurst = true;
                    brokeAt = Time.time;
                }
                ShowTime();
                if (waitingForBurst && (current == null || Time.time - brokeAt > BurstWait))
                {
                    // Its FSM normally bursts it at once; one that didn't is taken away with a puff.
                    if (current != null)
                    {
                        Puff(current.transform.position + Vector3.up * 0.5f, 1.5f);
                        Destroy(current.gameObject);
                    }
                    waitingForBurst = false;
                    nextAt = Time.time + nextPumpkinDelay;
                }
                if (nextAt >= 0f && Time.time >= nextAt)
                {
                    nextAt = -1f;
                    Spawn(Broken, spot);
                }
                if (timeLeft <= 0f) break;
                yield return null;
            }

            // No end sign: the countdown just goes. Time's up: the one left blows up (meanwhile the helmet eats).
            counting = false;
            if (sign != null) sign.gameObject.SetActive(false);
            bool blowing = false;
            if (!won)
            {
                timeLeft = 0f;
                if (current != null && !current.IsBroken)
                {
                    blowing = true;
                    StartCoroutine(BlowUp(current, () => blowing = false));
                }
            }
            yield return EatTheCandies();
            while (blowing) yield return null;
            ShowBoard();
            Finished = true;
        }

        // The round goes on the player's leaderboard (saved), and the board shows it: it stays a while by itself, as the
        // stages go on.
        private void ShowBoard()
        {
            int place = Game.Saving.BonusScores.Add(Broken, PumpkinCount, Punches, PerfectJabs);
            if (boardSeconds <= 0f) return;
            var board = BonusBoard.Show(Punches, PerfectJabs, place, boardSeconds, font);
            if (bonusSound != null) AudioSource.PlayClipAtPoint(bonusSound, board.transform.position);
        }

        // The player keeps every candy they got: the ones still coming out of the last pumpkins, then the helmet eats
        // every one lying about (it comes back for that if it ran out). Candy Wait Seconds at the most.
        private IEnumerator EatTheCandies()
        {
            if (Broken == 0 || helmetSeconds <= 0f) yield break;
            for (float waited = 0f; waited < candyWaitSeconds; waited += Time.deltaTime)
            {
                if (fountains == 0 && CandyHelmet.Flying == 0 && CandyHelmet.LyingAround(spot, CandyReach) == 0) break;
                if (!PlayerBuffs.CandyHelmet) PlayerBuffs.Give(Buff.CandyHelmet, candyWaitSeconds - waited);
                yield return null;
            }
        }

        private void Spawn(int index, Vector3 at)
        {
            if (index >= PumpkinCount || smaller == null) return;
            var pumpkin = Instantiate(smaller, at, Quaternion.identity, SpawnedStuff());
            Prepare(pumpkin, index);
            pumpkin.Setup();
            Took(pumpkin);
            Puff(at + Vector3.up * 0.5f * currentSize, currentSize);
        }

        // Its size, health, candies, laugh, EXP and jiggle as pumpkin number 'index' (before its Setup).
        private void Prepare(NestingPumpkin pumpkin, int index)
        {
            int total = PumpkinCount;
            bool last = index == total - 1;
            float pitch = total > 1 ? Mathf.Lerp(1f, lastPitch, index / (float)(total - 1)) : 1f;
            pumpkin.Configure(GiantSize * ShareOf(index), HealthOf(index), last ? lastCandies : candiesEach, pitch, last ? lastExp : expEach, JiggleOf(index));
        }

        // It's the one there now.
        private void Took(NestingPumpkin pumpkin)
        {
            current = pumpkin;
            spot = pumpkin.transform.position;
            currentCandies = pumpkin.Candies;
            currentSize = pumpkin.transform.localScale.x;
            // It comes out where candies may be lying: they mustn't be knocked (a hard bump breaks a candy).
            IgnoreCandies(pumpkin.GetComponentsInChildren<Collider>(true));
            ThrowClear(pumpkin);
        }

        // A pumpkin coming where the player stands (it reaches their body) throws them clear of it, landing Launch Margin
        // from its side, so they can walk back up to it and punch. One that comes next to them (each is a little smaller
        // than the last, which they stood outside of) leaves them be. The giant bursting out also shoves back a player
        // standing near it (Giant Push Reach), at least Giant Push far.
        private void ThrowClear(NestingPumpkin pumpkin)
        {
            var eyes = Head();
            if (eyes == null || launchHeight <= 0f) return;
            var middle = pumpkin.transform.position;
            float radius = RadiusPerSize * pumpkin.transform.localScale.x;
            float off = Flat(eyes.position - middle).magnitude;
            bool inside = off < radius + BodyRadius();
            bool giant = Broken == 0;
            if (giant && (inside || off < radius + BodyRadius() + giantPushReach))
                StartCoroutine(Throw(pumpkin, middle, inside ? launchHeight : giantPush.x, Mathf.Max(giantPush.y, radius + launchMargin - off)));
            else if (inside) StartCoroutine(Throw(pumpkin, middle, launchHeight, radius + launchMargin - off));
        }

        // How wide the player's body is (the XR Origin's Character Controller).
        private static float BodyRadius()
        {
            var origin = FindAnyObjectByType<XROrigin>();
            return origin != null && origin.TryGetComponent<CharacterController>(out var body) ? body.radius : DefaultBodyRadius;
        }

        // The throw goes through the pumpkin growing round the player (their Character Controller ignores it meanwhile);
        // the player's Toughness doesn't cut it, so they always land clear.
        private IEnumerator Throw(NestingPumpkin pumpkin, Vector3 middle, float height, float distance)
        {
            var origin = FindAnyObjectByType<XROrigin>();
            var body = origin != null ? origin.GetComponent<CharacterController>() : null;
            var parts = pumpkin.GetComponentsInChildren<Collider>(true);
            SetIgnored(body, parts, true);
            if (PlayerLaunch.Launch(middle, height, distance, true, false))
            {
                var flight = origin.GetComponent<PlayerLaunch>();
                yield return null;
                for (float t = 0f; t < ThrowSeconds && flight != null && flight.Flying; t += Time.deltaTime) yield return null;
            }
            SetIgnored(body, parts, false);
        }

        private static void SetIgnored(Collider body, Collider[] parts, bool ignore)
        {
            if (body == null) return;
            foreach (var part in parts)
            {
                if (part != null) Physics.IgnoreCollision(body, part, ignore);
            }
        }

        private void AddSeconds()
        {
            if (bonusSeconds <= 0f) return;
            timeLeft += bonusSeconds;
            lastTick = -1;
            Pop($"+{bonusSeconds:0}", BonusColor, PopSeconds);
            flashUntil = Time.time + 0.6f;
            if (bonusSound != null) voice.PlayOneShot(bonusSound);
        }

        // Every pumpkin broken: more seconds of the candy vacuum, on top of what's left.
        private void GiveHelmet()
        {
            if (helmetSeconds <= 0f) return;
            bool was = PlayerBuffs.CandyHelmet;
            PlayerBuffs.Add(Buff.CandyHelmet, helmetSeconds);
            if (!was && helmetSound != null) voice.PlayOneShot(helmetSound);
        }

        private void DropCandies(Vector3 at, int amount)
        {
            if (candies.Length > 0 && amount > 0) StartCoroutine(CandyFountain(at, amount, currentSize, current));
        }

        // One by one, out of a ring as wide as the pumpkin that broke and low over the floor, so they land clear of the
        // next one: candies break on a hard bump (their Damage FSM), so they mustn't start inside each other or fall far.
        // The burst's pieces and the next pumpkin go through them.
        private IEnumerator CandyFountain(Vector3 at, int amount, float size, NestingPumpkin broken)
        {
            fountains++;
            try
            {
                // Once it has burst (its FSM does it at once, or after its entrance), so its pieces are there.
                float waitedFrom = Time.time;
                while (broken != null && Time.time - waitedFrom < BurstWait) yield return null;
                // One that still hasn't burst is taken away by the round: the candies go through it meanwhile.
                var stillThere = broken != null ? broken.GetComponentsInChildren<Collider>(true) : System.Array.Empty<Collider>();
                var ground = new Vector3(at.x, transform.position.y, at.z);
                float ring = Mathf.Max(CandyRing, size * CandyRingPerSize);
                // Its pieces fall on the candies already lying there too: one candy knocked breaks and its little blast
                // knocks the others, so they all go through the pieces.
                var pieces = BurstPieces();
                IgnoreCandies(pieces);
                float turn = Random.Range(0f, 360f);
                for (int i = 0; i < amount; i++)
                {
                    var prefab = candies[Random.Range(0, candies.Length)];
                    if (prefab == null) continue;
                    var outward = Quaternion.Euler(0f, turn + 360f * i / amount, 0f) * Vector3.forward;
                    var candy = Instantiate(prefab, ground + outward * ring + Vector3.up * CandyHeight, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), SpawnedStuff());
                    var parts = candy.GetComponentsInChildren<Collider>(true);
                    IgnoreEach(parts, pieces);
                    IgnoreEach(parts, stillThere);
                    if (current != null && current != broken) IgnoreEach(parts, current.GetComponentsInChildren<Collider>(true));
                    liveCandies.AddRange(parts);
                    if (candy.TryGetComponent<Rigidbody>(out var body))
                        body.linearVelocity = outward * Random.Range(0.8f, 1.3f) + Vector3.up * candyLaunchSpeed;
                    yield return new WaitForSeconds(CandyGap);
                }
            }
            finally { fountains--; }
        }

        // The pieces of the pumpkins that burst (their Health FSM puts them under CurrentlySpawnedStuffs).
        private static Collider[] BurstPieces()
        {
            var stuff = SpawnedStuff();
            if (stuff == null) return System.Array.Empty<Collider>();
            var found = new List<Collider>();
            foreach (Transform child in stuff)
            {
                if (child.name.StartsWith(PiecesName)) found.AddRange(child.GetComponentsInChildren<Collider>(true));
            }
            return found.ToArray();
        }

        // Candies already lying about join the ones looked after.
        private void AdoptCandies()
        {
            var stuff = SpawnedStuff();
            if (stuff == null) return;
            foreach (Transform child in stuff)
            {
                if (child.name.StartsWith(CandyName)) liveCandies.AddRange(child.GetComponentsInChildren<Collider>(true));
            }
        }

        // The candies still lying about go through these.
        private void IgnoreCandies(Collider[] these)
        {
            liveCandies.RemoveAll(c => c == null);
            IgnoreEach(liveCandies, these);
        }

        private static void IgnoreEach(IEnumerable<Collider> a, Collider[] b)
        {
            foreach (var x in a)
            {
                if (x == null) continue;
                foreach (var y in b)
                {
                    if (y != null) Physics.IgnoreCollision(x, y);
                }
            }
        }

        // Time's up: it blows up. Its own Health FSM bursts it (pieces and smoke) with no EXP or candies (it stops
        // taking hits first), with a bang, a flash and a shake; its pieces fly off, and a player near it is thrown back
        // a few metres (their Toughness doesn't cut it). 'done' is called once it's gone.
        private IEnumerator BlowUp(NestingPumpkin pumpkin, System.Action done)
        {
            var middle = pumpkin.Body != null ? pumpkin.Body.position : pumpkin.transform.position;
            float size = pumpkin.transform.localScale.x;
            float radius = RadiusPerSize * size;
            pumpkin.StopTakingHits();
            pumpkin.Burst();
            foreach (var effect in blastEffects)
            {
                if (effect != null) EffectPool.Play(effect, middle, Mathf.Max(0.5f, size * blastEffectSize));
            }
            if (blastSound != null) AudioSource.PlayClipAtPoint(blastSound, middle, blastVolume);
            var eyes = Head();
            bool thrown = false;
            if (eyes != null && Flat(eyes.position - middle).magnitude <= radius + blastReach)
                thrown = PlayerLaunch.Launch(middle, blastHeight, blastDistance, true, false);
            if (!thrown) ShakeView(BlastShake);
            // Its FSM bursts it within a moment (from its laugh it can take up to its Wait); then the pieces fly.
            for (float t = 0f; t < BurstWait && pumpkin != null; t += Time.deltaTime) yield return null;
            if (pumpkin != null)
            {
                Puff(middle, BiggestPuff);
                Destroy(pumpkin.gameObject);
            }
            yield return null;
            PushPieces(middle, radius + blastReach);
            done?.Invoke();
        }

        // The burst pieces near the blast fly away from it (not the candies: a hard knock breaks them).
        private void PushPieces(Vector3 middle, float reach)
        {
            var stuff = SpawnedStuff();
            if (stuff == null || blastPush <= 0f) return;
            foreach (Transform child in stuff)
            {
                if (!child.name.StartsWith(PiecesName)) continue;
                foreach (var body in child.GetComponentsInChildren<Rigidbody>())
                {
                    if (body.isKinematic) continue;
                    var away = body.worldCenterOfMass - middle;
                    if (away.sqrMagnitude > reach * reach) continue;
                    away.y = Mathf.Max(away.y, 0f) + 0.6f * away.magnitude;
                    body.AddForce(away.normalized * blastPush, ForceMode.VelocityChange);
                }
            }
        }

        // A camera shake event sent to the main camera's Info FSM (ShakeSmall, ShakeMed, ShakeBigger).
        private static void ShakeView(string eventName)
        {
            var cam = HutongGames.PlayMaker.FsmVariables.GlobalVariables.FindFsmGameObject("MainCam");
            if (cam == null || cam.Value == null) return;
            foreach (var fsm in cam.Value.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName == "Info") fsm.SendEvent(eventName);
            }
        }

        // ---------- The countdown sign ----------

        // Made hidden: it shows when the giant comes out.
        private void MakeSign()
        {
            sign = WorldUI.CreatePanel("Bonus Countdown", PanelWidth, PanelHeight, signScale, new Color(0.08f, 0.05f, 0.12f, 0.7f));
            var face = font != null ? font : TMP_Settings.defaultFontAsset;
            signText = WorldUI.CreateText("Time", sign, "", 80f, FontStyles.Bold, TextAlignmentOptions.Center, SignColor, face);
            WorldUI.Stretch(signText.rectTransform);
            signText.textWrappingMode = TextWrappingModes.NoWrap;
            signText.enableAutoSizing = true;
            signText.fontSizeMin = 30f;
            signText.fontSizeMax = 80f;
            signText.margin = new Vector4(10f, 0f, 10f, 0f);

            // "+5" pops up under it.
            popText = WorldUI.CreateText("Pop", sign, "", 60f, FontStyles.Bold, TextAlignmentOptions.Center, BonusColor, face);
            var rect = popText.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(PanelWidth * 2.2f, 70f);
            rect.anchoredPosition = new Vector2(0f, -6f);
            popText.textWrappingMode = TextWrappingModes.NoWrap;
            popText.enableAutoSizing = true;
            popText.fontSizeMin = 24f;
            popText.fontSizeMax = 60f;
            popText.outlineWidth = 0.2f;
            popText.outlineColor = new Color(0.08f, 0.05f, 0.12f, 1f);
            popText.gameObject.SetActive(false);
            sign.gameObject.SetActive(false);
            // Up where the giant's top will be (it grows): it comes down as they get smaller.
            signY = transform.position.y + Mathf.Max(signLowest, RadiusPerSize * 2f * GiantSize * 0.85f + signAbove);
        }

        private void LateUpdate()
        {
            if (sign == null || !sign.gameObject.activeSelf) return;
            // Above the pumpkin there now (once it has grown), coming down as they get smaller.
            if (current != null && current.Ready)
            {
                float target = Mathf.Max(spot.y + signLowest, current.Top + signAbove);
                signY = Mathf.Lerp(signY, target, 1f - Mathf.Exp(-3f * Time.deltaTime));
            }
            sign.position = new Vector3(spot.x, signY, spot.z);
            WorldUI.Face(sign, Head());

            if (popText != null && popText.gameObject.activeSelf)
            {
                float left = popUntil - Time.time;
                if (left <= 0f) popText.gameObject.SetActive(false);
                else
                {
                    float age = popFor - left;
                    popText.rectTransform.localScale = Vector3.one * (1f + 0.5f * Mathf.Exp(-age * 9f));
                    var c = popText.color;
                    c.a = Mathf.Clamp01(left / 0.4f);
                    popText.color = c;
                }
            }
        }

        private void Pop(string text, Color color, float forSeconds)
        {
            if (popText == null) return;
            popText.text = text;
            popText.color = color;
            popFor = forSeconds;
            popUntil = Time.time + forSeconds;
            popText.gameObject.SetActive(true);
        }

        private void ShowTime()
        {
            if (signText == null) return;
            int whole = Mathf.Max(0, Mathf.CeilToInt(timeLeft));
            bool warn = timeLeft <= warnSeconds;
            // Green just after seconds were added; blinks twice a second near the end.
            if (Time.time < flashUntil) signText.color = BonusColor;
            else signText.color = warn && Mathf.Repeat(Time.time * 2f, 1f) < 0.6f ? WarnColor : SignColor;
            signText.text = whole.ToString();
            if (warn && whole != lastTick && whole > 0)
            {
                lastTick = whole;
                if (tickSound != null) voice.PlayOneShot(tickSound, tickVolume);
            }
        }

        // ---------- Helpers ----------

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        private void Puff(Vector3 at, float size)
        {
            if (puffEffect != null) EffectPool.Play(puffEffect, at, Mathf.Min(size, BiggestPuff));
        }

        private Transform Head()
        {
            if (head == null && Camera.main != null) head = Camera.main.transform;
            return head;
        }

        // Spawned things go under the global CurrentlySpawnedStuffs, so the stages' clean-ups remove them.
        private static Transform SpawnedStuff()
        {
            var stuff = HutongGames.PlayMaker.FsmVariables.GlobalVariables.FindFsmGameObject("CurrentlySpawnedStuffs");
            return stuff != null && stuff.Value != null ? stuff.Value.transform : null;
        }
    }
}

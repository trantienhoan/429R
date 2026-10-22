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
    ///   countdown, and the next one inside it pops out where it was: smaller (Shrink Per Pumpkin of the giant's size less
    ///   each time) but tougher (more health, less jiggle), down to the last one (Count);
    /// - breaking the giant gives the player the invisible Candy Helmet (a timed buff: candies lying around fly into
    ///   their mouth);
    /// - when they're all broken or time's up (the one left sinks away) the player keeps what they got: the helmet eats
    ///   every candy left, then the round is over (Finished) and Play Bonus Round goes on.
    /// A player earns it in a stage by killing every enemy before taking the Yellow Seed: the Watch Bonus Round action
    /// (in the stage's enemy-wave state) notes the seeds and the enemies; Play Bonus Round asks Earned.
    /// </summary>
    [DisallowMultipleComponent]
    public class BonusRound : MonoBehaviour
    {
        [Header("Start")]
        [Tooltip("Seconds the giant takes to grow out of the chest.")]
        [Min(0.05f)]
        [SerializeField] private float growSeconds = 1.2f;
        [Tooltip("A pumpkin coming where the player stands throws them clear of it: this high, in metres. 0 = no throw.")]
        [Min(0f)]
        [SerializeField] private float launchHeight = 1.2f;
        [Tooltip("... landing this far clear of the pumpkin's side, in metres.")]
        [Min(0f)]
        [SerializeField] private float launchMargin = 1f;

        [Header("Pumpkins")]
        [Tooltip("The giant, the first one (Nesting_Pumpkin_1): the stage's chest makes it.")]
        [SerializeField] private NestingPumpkin giant;
        [Tooltip("Every pumpkin after the giant (Nesting_Pumpkin_2): each pops out where the last one broke.")]
        [SerializeField] private NestingPumpkin smaller;
        [Tooltip("The start pumpkin (StartGamePumpkin): the giant is Giant Times its size.")]
        [SerializeField] private GameObject startPumpkin;
        [Min(0.1f)]
        [SerializeField] private float giantTimes = 1.5f;
        [Tooltip("How many pumpkins there are: the giant and the ones inside it.")]
        [Min(1)]
        [SerializeField] private int count = 6;
        [Tooltip("Each pumpkin is this share of the giant's size smaller than the one before (0.07 = 7%).")]
        [Range(0.01f, 0.2f)]
        [SerializeField] private float shrinkPerPumpkin = 0.07f;
        [Tooltip("The giant's health. The smaller, the tougher: each one after it has Tougher Per Pumpkin of this more.")]
        [Min(1f)]
        [SerializeField] private float giantHealth = 120f;
        [Tooltip("Each pumpkin has this share of the giant's health more than the one before (0.2 = +20%: 120, 144, 168 ...).")]
        [Range(0f, 1f)]
        [SerializeField] private float tougherPerPumpkin = 0.2f;
        [Tooltip("... and jiggles this share of the giant's jiggle less (0.15 = 100%, 85%, 70% ...).")]
        [Range(0f, 0.3f)]
        [SerializeField] private float stifferPerPumpkin = 0.15f;
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
        [Tooltip("Seconds the Candy Helmet lasts (given when the giant breaks). 0 = none.")]
        [Min(0f)]
        [SerializeField] private float helmetSeconds = 60f;
        [Tooltip("Played when the helmet is given.")]
        [SerializeField] private AudioClip helmetSound;
        [SerializeField] private string helmetText = "CANDY HELMET!";

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
        [Tooltip("Seconds the pumpkin left at time's up takes to sink away.")]
        [Min(0.1f)]
        [SerializeField] private float sinkSeconds = 2f;
        [Tooltip("A puff where things appear and go, e.g. Smoke_Blast.")]
        [SerializeField] private GameObject puffEffect;

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
        private const float HelmetPopSeconds = 2.5f;
        // The longest a throw clear of a pumpkin goes through it, in seconds.
        private const float ThrowSeconds = 3f;
        private const float DefaultBodyRadius = 0.25f;

        /// <summary>The round running now (a pumpkin the stage's chest makes asks it, see TakeGiant).</summary>
        public static BonusRound Running { get; private set; }

        /// <summary>The round is over (all broken or out of time) and the candies are eaten: Play Bonus Round goes on.</summary>
        public bool Finished { get; private set; }

        /// <summary>Pumpkins broken so far.</summary>
        public int Broken { get; private set; }

        /// <summary>Seconds left on the countdown.</summary>
        public float TimeLeft => timeLeft;

        /// <summary>How many pumpkins this round has.</summary>
        public int PumpkinCount => Mathf.Clamp(count, 1, Mathf.FloorToInt((1f - SmallestShare) / shrinkPerPumpkin) + 1);

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

        // Enemies still alive: bugs and bosses that give EXP (not furniture, doors or the start pumpkin, and not the ones a
        // broken wall's portal keeps letting in).
        private static int AliveEnemies()
        {
            int alive = 0;
            foreach (var enemy in FindObjectsByType<EnemyHitDamage>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (enemy.enabled && enemy.CountsAsKill && !enemy.IsDead && !Game.Enemies.WallPortal.IsPortalEnemy(enemy.gameObject)) alive++;
            }
            return alive;
        }

        // ---------- The pumpkins ----------

        /// <summary>Pumpkin number 'index' (0 = the giant): its share of the giant's size.</summary>
        public float ShareOf(int index) => Mathf.Max(SmallestShare, 1f - shrinkPerPumpkin * index);

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

        /// <summary>How much pumpkin number 'index' jiggles when hit: the smaller, the stiffer (1 = the giant's).</summary>
        public float JiggleOf(int index) => Mathf.Max(MinJiggle, 1f - stifferPerPumpkin * index);

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
            pumpkin.GrowHere(growSeconds);
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
                    if (Broken == 1) GiveHelmet();
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

            // No end sign: the countdown just goes. Time's up: the one left sinks away (meanwhile the helmet eats).
            if (sign != null) sign.gameObject.SetActive(false);
            NestingPumpkin sinking = null;
            if (!won)
            {
                timeLeft = 0f;
                if (current != null && !current.IsBroken)
                {
                    sinking = current;
                    StartCoroutine(SinkAway(current));
                }
            }
            yield return EatTheCandies();
            while (sinking != null) yield return null;
            Finished = true;
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
        // than the last, which they stood outside of) leaves them be.
        private void ThrowClear(NestingPumpkin pumpkin)
        {
            var eyes = Head();
            if (eyes == null || launchHeight <= 0f) return;
            var middle = pumpkin.transform.position;
            float radius = RadiusPerSize * pumpkin.transform.localScale.x;
            float off = Flat(eyes.position - middle).magnitude;
            if (off < radius + BodyRadius()) StartCoroutine(Throw(pumpkin, middle, radius + launchMargin - off));
        }

        // How wide the player's body is (the XR Origin's Character Controller).
        private static float BodyRadius()
        {
            var origin = FindAnyObjectByType<XROrigin>();
            return origin != null && origin.TryGetComponent<CharacterController>(out var body) ? body.radius : DefaultBodyRadius;
        }

        // The throw goes through the pumpkin growing round the player (their Character Controller ignores it meanwhile);
        // the player's Toughness doesn't cut it, so they always land clear.
        private IEnumerator Throw(NestingPumpkin pumpkin, Vector3 middle, float distance)
        {
            var origin = FindAnyObjectByType<XROrigin>();
            var body = origin != null ? origin.GetComponent<CharacterController>() : null;
            var parts = pumpkin.GetComponentsInChildren<Collider>(true);
            SetIgnored(body, parts, true);
            if (PlayerLaunch.Launch(middle, launchHeight, distance, true, false))
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

        private void GiveHelmet()
        {
            if (helmetSeconds <= 0f) return;
            PlayerBuffs.Give(Buff.CandyHelmet, helmetSeconds);
            Pop(helmetText, PlayerBuffs.ColorOf(Buff.CandyHelmet), HelmetPopSeconds);
            if (helmetSound != null) voice.PlayOneShot(helmetSound);
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

        // Time's up: it stops taking hits, keeps laughing and sinks into the ground.
        private IEnumerator SinkAway(NestingPumpkin pumpkin)
        {
            pumpkin.StopTakingHits();
            var start = pumpkin.transform.position;
            var depth = Vector3.down * (2.5f * pumpkin.transform.lossyScale.y);
            for (float t = 0f; t < sinkSeconds && pumpkin != null; t += Time.deltaTime)
            {
                float k = t / sinkSeconds;
                pumpkin.transform.position = start + depth * (k * k);
                yield return null;
            }
            if (pumpkin != null)
            {
                Puff(start, 1.5f);
                Destroy(pumpkin.gameObject);
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

            // "+5" and "CANDY HELMET!" pop up under it.
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

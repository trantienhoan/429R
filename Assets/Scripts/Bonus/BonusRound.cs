using System.Collections;
using Game.Combat;
using Game.Inventory;
using TMPro;
using UnityEngine;

namespace Game.Bonus
{
    /// <summary>
    /// The nesting pumpkin bonus round, on Bonus_Area (a copy of Boss_Area):
    /// - a pumpkin chest rises in the middle, facing the player; punching it open drops the giant laughing pumpkin right
    ///   where it was and starts the countdown;
    /// - every pumpkin broken bursts, drops its candies (its Enemy Hit Damage gives the EXP) and the next, smaller one pops
    ///   out where it was (Pumpkins: one prefab each, with its own size, health and candies),
    ///   down to the last one;
    /// - when they're all broken or time's up (the one left laughs and sinks away) the player keeps what they got, has a
    ///   few seconds to grab the candies, and the round is over (Finished): the Play Bonus Round action goes on.
    /// A player earns it in a stage by killing every enemy before taking the Yellow Seed: the Watch Bonus Round action
    /// (in the stage's enemy-wave state) notes the seeds and the enemies; Play Bonus Round asks Earned.
    /// </summary>
    [DisallowMultipleComponent]
    public class BonusRound : MonoBehaviour
    {
        [Header("Start")]
        [Tooltip("The chest that starts the round when it's punched open (Bonus_Chest_Pumpkin).")]
        [SerializeField] private GameObject chestPrefab;
        [Tooltip("Where the chest comes up: the middle of the area. The giant comes down right where the chest broke.")]
        [SerializeField] private Transform chestPoint;

        [Header("Pumpkins")]
        [Tooltip("The Russian-doll pumpkins in order, the giant first (Nesting_Pumpkin_1 ... _5). Each one's own Nesting " +
                 "Pumpkin sets its size, health and candies; its EXP is on its Enemy Hit Damage.")]
        [SerializeField] private NestingPumpkin[] pumpkins = System.Array.Empty<NestingPumpkin>();
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
        [Min(1f)]
        [SerializeField] private float seconds = 60f;
        [Tooltip("The countdown blinks red and ticks for this many last seconds.")]
        [Min(0f)]
        [SerializeField] private float warnSeconds = 10f;
        [SerializeField] private AudioClip tickSound;
        [Range(0f, 1f)]
        [SerializeField] private float tickVolume = 0.6f;

        [Header("Countdown sign")]
        [SerializeField] private TMP_FontAsset font;
        [Tooltip("How high above the middle of the area the sign floats (metres).")]
        [SerializeField] private float signHeight = 3.4f;
        [Tooltip("The sign's size (metres per panel pixel).")]
        [Min(0.0001f)]
        [SerializeField] private float signScale = 0.006f;
        [SerializeField] private string waitingText = "BONUS!";
        [SerializeField] private string wonText = "WOW!";
        [SerializeField] private string timeUpText = "TIME!";

        [Header("End")]
        [Tooltip("Played when the last pumpkin breaks in time.")]
        [SerializeField] private AudioClip winSound;
        [Tooltip("Seconds after the round ends before it's over, to grab the candies.")]
        [Min(0f)]
        [SerializeField] private float endSeconds = 6f;
        [Tooltip("Seconds the pumpkin left at time's up takes to sink away.")]
        [Min(0.1f)]
        [SerializeField] private float sinkSeconds = 2f;
        [Tooltip("A puff where things appear and go, e.g. Smoke_Blast.")]
        [SerializeField] private GameObject puffEffect;

        private static readonly Color SignColor = new(1f, 0.82f, 0.25f, 1f);
        private static readonly Color WarnColor = new(1f, 0.25f, 0.2f, 1f);
        private const float PanelWidth = 220f;
        private const float PanelHeight = 110f;
        // Seconds a broken pumpkin gets to burst by itself before it's taken away.
        private const float BurstWait = 3f;
        // The candy fountain: ring radius and height (metres) and the time between candies (seconds).
        private const float CandyRing = 0.35f;
        // ... as wide as the pumpkin that broke (its scale times this), so they land clear of the next one.
        private const float CandyRingPerSize = 0.55f;
        // The start pumpkin's burst pieces (Nesting_Pumpkin_N > pumpkin_Start > pumpkin_0_drop).
        private const string PiecesName = "pumpkin_0_drop";
        private const string CandyName = "Candy_";
        private const float CandyHeight = 0.3f;
        private const float CandyGap = 0.08f;

        /// <summary>The round is over (won or out of time): the Play Bonus Round action goes on.</summary>
        public bool Finished { get; private set; }

        /// <summary>Pumpkins broken so far.</summary>
        public int Broken { get; private set; }

        private RectTransform sign;
        private TMP_Text signText;
        private AudioSource voice;
        private Transform head;
        private NestingPumpkin current;
        // The pumpkin there now: its candies and size (kept, as it can be gone the moment it breaks).
        private int currentCandies;
        private float currentSize = 1f;
        // The colliders of the candies dropped so far (gone ones are cleared now and then).
        private readonly System.Collections.Generic.List<Collider> liveCandies = new();
        private float timeLeft;
        private int lastTick = -1;

        // ---------- Earning it (the Watch Bonus Round and Play Bonus Round actions) ----------

        private static int seedsAtStart = -1;
        private static int aliveAtSeed = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            seedsAtStart = -1;
            aliveAtSeed = -1;
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

        // Enemies still alive: bugs and bosses that give EXP (not furniture, doors or the start pumpkin).
        private static int AliveEnemies()
        {
            int alive = 0;
            foreach (var enemy in FindObjectsByType<EnemyHitDamage>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (enemy.enabled && enemy.CountsAsKill && !enemy.IsDead) alive++;
            }
            return alive;
        }

        // ---------- The round ----------

        private void Start()
        {
            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;
            MakeSign();
            Puff(transform.position, 3f);
            StartCoroutine(Run());
        }

        private void OnDestroy()
        {
            if (sign != null) Destroy(sign.gameObject);
        }

        private IEnumerator Run()
        {
            ShowText(waitingText, SignColor);
            // The chest: punched open (or opening by itself after a while), it's gone.
            var point = chestPoint != null ? chestPoint : transform;
            var chestSpot = point.position;
            var chest = chestPrefab != null ? Instantiate(chestPrefab, chestSpot, point.rotation, SpawnedStuff()) : null;
            while (chest != null)
            {
                chestSpot = chest.transform.position;
                yield return null;
            }

            timeLeft = seconds;
            // The chest's own candies are looked after like the pumpkins' (bursts and pumpkins go through them).
            AdoptCandies();
            // The giant comes down right where the chest broke, on the floor.
            var spot = new Vector3(chestSpot.x, transform.position.y, chestSpot.z);
            Spawn(0, spot);
            bool won = false;
            // The one there gets broken, bursts (its own Health FSM), then the next pops out where it was.
            bool waitingForBurst = false;
            float brokeAt = 0f;
            float nextAt = -1f;
            var lastSeen = spot;
            while (true)
            {
                timeLeft -= Time.deltaTime;
                ShowTime();
                if (current != null && current.Body != null) lastSeen = current.Body.position;
                // Broken: its health ran out, or it's already gone (its FSM can burst it the same frame the hit lands).
                if (!waitingForBurst && nextAt < 0f && (current == null || current.IsBroken))
                {
                    Broken++;
                    var at = lastSeen;
                    Puff(at, currentSize * 1.2f);
                    DropCandies(at, currentCandies);
                    if (Broken >= pumpkins.Length)
                    {
                        won = true;
                        break;
                    }
                    spot = new Vector3(at.x, spot.y, at.z);
                    waitingForBurst = true;
                    brokeAt = Time.time;
                }
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

            if (won)
            {
                ShowText(wonText, SignColor);
                if (winSound != null) voice.PlayOneShot(winSound);
            }
            else
            {
                ShowText(timeUpText, WarnColor);
                if (current != null && !current.IsBroken) StartCoroutine(SinkAway(current));
            }
            yield return new WaitForSeconds(endSeconds);
            if (sign != null) sign.gameObject.SetActive(false);
            Finished = true;
        }

        private void Spawn(int index, Vector3 at)
        {
            if (index >= pumpkins.Length || pumpkins[index] == null) return;
            current = Instantiate(pumpkins[index], at, Quaternion.identity, SpawnedStuff());
            current.Setup();
            currentCandies = current.Candies;
            currentSize = current.transform.localScale.x;
            // It pops out where the candies just fell: they mustn't be knocked (a hard bump breaks a candy).
            IgnoreCandies(current.GetComponentsInChildren<Collider>(true));
            if (index > 0) Puff(at + Vector3.up * 0.5f * currentSize, currentSize);
        }

        private void DropCandies(Vector3 at, int count)
        {
            if (candies.Length > 0 && count > 0) StartCoroutine(CandyFountain(at, count, currentSize, current));
        }

        // One by one, out of a ring as wide as the pumpkin that broke and low over the floor, so they land clear of the
        // next one: candies break on a hard bump (their Damage FSM), so they mustn't start inside each other or fall far.
        // The burst's pieces and the next pumpkin go through them.
        private IEnumerator CandyFountain(Vector3 at, int count, float size, NestingPumpkin broken)
        {
            // Once it has burst (its FSM does it at once, or at its next laugh), so its pieces are there.
            float waitedFrom = Time.time;
            while (broken != null && Time.time - waitedFrom < 1f) yield return null;
            var ground = new Vector3(at.x, transform.position.y, at.z);
            float ring = Mathf.Max(CandyRing, size * CandyRingPerSize);
            // Its pieces fall on the candies already lying there too: one candy knocked breaks and its little blast
            // knocks the others, so they all go through the pieces.
            var pieces = BurstPieces();
            IgnoreCandies(pieces);
            float turn = Random.Range(0f, 360f);
            for (int i = 0; i < count; i++)
            {
                var prefab = candies[Random.Range(0, candies.Length)];
                if (prefab == null) continue;
                var outward = Quaternion.Euler(0f, turn + 360f * i / count, 0f) * Vector3.forward;
                var candy = Instantiate(prefab, ground + outward * ring + Vector3.up * CandyHeight, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), SpawnedStuff());
                var parts = candy.GetComponentsInChildren<Collider>(true);
                IgnoreEach(parts, pieces);
                if (current != null && current != broken) IgnoreEach(parts, current.GetComponentsInChildren<Collider>(true));
                liveCandies.AddRange(parts);
                if (candy.TryGetComponent<Rigidbody>(out var body))
                    body.linearVelocity = outward * Random.Range(0.8f, 1.3f) + Vector3.up * candyLaunchSpeed;
                yield return new WaitForSeconds(CandyGap);
            }
        }

        // The pieces of the pumpkins that burst (their Health FSM puts them under CurrentlySpawnedStuffs).
        private static Collider[] BurstPieces()
        {
            var stuff = SpawnedStuff();
            if (stuff == null) return System.Array.Empty<Collider>();
            var found = new System.Collections.Generic.List<Collider>();
            foreach (Transform child in stuff)
            {
                if (child.name.StartsWith(PiecesName)) found.AddRange(child.GetComponentsInChildren<Collider>(true));
            }
            return found.ToArray();
        }

        // Candies already lying about (e.g. the chest's) join the ones looked after.
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

        private static void IgnoreEach(System.Collections.Generic.IEnumerable<Collider> a, Collider[] b)
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

        private void MakeSign()
        {
            sign = WorldUI.CreatePanel("Bonus Countdown", PanelWidth, PanelHeight, signScale, new Color(0.08f, 0.05f, 0.12f, 0.7f));
            sign.position = transform.position + Vector3.up * signHeight;
            signText = WorldUI.CreateText("Time", sign, "", 80f, FontStyles.Bold, TextAlignmentOptions.Center, SignColor, font != null ? font : TMP_Settings.defaultFontAsset);
            WorldUI.Stretch(signText.rectTransform);
            // One line, shrinking to fit ("BONUS!" is wider than the numbers).
            signText.textWrappingMode = TextWrappingModes.NoWrap;
            signText.enableAutoSizing = true;
            signText.fontSizeMin = 30f;
            signText.fontSizeMax = 80f;
            signText.margin = new Vector4(10f, 0f, 10f, 0f);
        }

        private void LateUpdate()
        {
            if (sign != null) WorldUI.Face(sign, Head());
        }

        private void ShowText(string text, Color color)
        {
            if (signText == null) return;
            signText.text = text;
            signText.color = color;
        }

        private void ShowTime()
        {
            if (signText == null) return;
            int whole = Mathf.Max(0, Mathf.CeilToInt(timeLeft));
            bool warn = timeLeft <= warnSeconds;
            // Blinks twice a second near the end.
            signText.color = warn && Mathf.Repeat(Time.time * 2f, 1f) < 0.6f ? WarnColor : SignColor;
            signText.text = whole.ToString();
            if (warn && whole != lastTick && whole > 0)
            {
                lastTick = whole;
                if (tickSound != null) voice.PlayOneShot(tickSound, tickVolume);
            }
        }

        // ---------- Helpers ----------

        private void Puff(Vector3 at, float size)
        {
            if (puffEffect != null) EffectPool.Play(puffEffect, at, size);
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

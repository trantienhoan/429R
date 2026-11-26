using System.Collections;
using System.Collections.Generic;
using Game.Inventory;
using Game.Leveling;
using Game.Locomotion;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

namespace Game.Bonus
{
    /// <summary>
    /// Whack-a-pumpkin, Special Stage Type 4 (user, 2026-11-23), the bonus after the Playroom: the Play Bonus Round action
    /// makes it at the bonus stage when the player earned it (every enemy killed before taking the Yellow Seed).
    /// - The chest (the nesting round's, Treasure_Chest_PUMPKIN_EXTRA) comes; touching or punching it open makes the
    ///   machine rise out of the floor where it was, facing away from the player: a half-ring table with 20 holes round
    ///   a spot to stand on, two mallets waiting on posts and a scoreboard behind it.
    /// - Taking BOTH mallets starts the countdown (Seconds). (Walking and teleporting stayed off until it was over; Keep
    ///   Still is off since 2026-11-26: the player walks as usual, and the machine is Machine Scale 0.8 of its size.)
    /// - Pumpkin heads pop up and go back down. A quick downward blow with a mallet whacks one (hands do nothing):
    ///   normal 1 candy, gold 10 (rare, spinning), rainbow 15, time +1 to +3 seconds (rare, the head shows how many),
    ///   skull: don't (it takes 3 candies and the combo). A gold head whacked starts a gold frenzy: for 3 s every head
    ///   is gold (3 candies each, faster, more at once, missing one costs nothing).
    /// - Every whack in a row makes the heads come faster, stay up shorter, and more at once. A head that goes back
    ///   down unwhacked (not a skull) is a miss: the combo and the speed start over. Whatever the combo, the round gets
    ///   quicker towards its end, with one more head at once in the last (ticking) seconds.
    /// - The song (the jigsaw's 90 s songs, user 2026-11-23) plays from GO and ends with the countdown.
    /// - Time's up: the heads drop, the mallets go, the round goes on the leaderboard (saved, WhackScores) and the
    ///   machine pours out the candies won times the share of heads hit (Payout: whacked / (whacked + missed); user
    ///   2026-11-26 "Though I missed a lot, I still get a lot of reward candies"), which the Candy Helmet feeds the
    ///   player; the board shows WHACKED and MISSED, the time's number off; then it sinks and the round is over
    ///   (Finished), and Play Bonus Round goes on.
    /// Inside it the player plays with level-0 stats (Play Bonus Round adds Special Stage Stats).
    /// </summary>
    [DisallowMultipleComponent]
    public class WhackRound : MonoBehaviour, IBonusGame
    {
        [Header("Stage")]
        [Tooltip("The machine's size, times its prefab's (user 2026-11-26: \"the machine is too big\"). 1 = as built: " +
                 "heads 0.64-0.93 m from its middle, 0.82 m high.")]
        [Range(0.5f, 1.2f)]
        [SerializeField] private float machineScale = 0.8f;
        [Tooltip("Walking and teleporting stay off while the countdown runs. Off (user 2026-11-26: \"Player can't move, I " +
                 "could not move at all in that stage\"): the player moves as usual.")]
        [SerializeField] private bool keepStill;
        [Tooltip("The sky over the field while the round is on (Bonus_Stage_Sky). Empty: none.")]
        [SerializeField] private GameObject stageSky;
        [Tooltip("The chest that comes first (Treasure_Chest_PUMPKIN_EXTRA): opened, the machine rises where it was.")]
        [SerializeField] private GameObject chest;
        [Tooltip("The machine (Whack_Machine).")]
        [SerializeField] private WhackMachine machine;
        [Tooltip("A mallet (Whack_Hammer): two are made, one on each post.")]
        [SerializeField] private WhackHammer hammer;
        [Tooltip("A puff where things appear and go (Smoke_Blast).")]
        [SerializeField] private GameObject puffEffect;
        [Min(0f)]
        [SerializeField] private float puffSize = 1f;
        [SerializeField] private AudioClip riseSound;

        [Header("Time")]
        [Tooltip("The countdown, in seconds. It starts when both mallets are taken.")]
        [Min(1f)]
        [SerializeField] private float seconds = 60f;
        [Tooltip("The time turns red and ticks for this many last seconds.")]
        [Min(0f)]
        [SerializeField] private float warnSeconds = 10f;
        [SerializeField] private AudioClip tickSound;
        [Range(0f, 1f)]
        [SerializeField] private float tickVolume = 0.6f;
        [SerializeField] private AudioClip goSound;
        [SerializeField] private AudioClip timeUpSound;

        [Header("Speed (every whack in a row speeds it up; a miss starts it over)")]
        [Tooltip("Seconds between heads popping up, at the start (and after a miss).")]
        [Min(0.05f)]
        [SerializeField] private float startPopEvery = 1f;
        [Tooltip("Seconds a head stays up, at the start (and after a miss).")]
        [Min(0.1f)]
        [SerializeField] private float startUpSeconds = 1.4f;
        [Tooltip("Each whack in a row makes both of those this share shorter (0.04 = 4%: 1, 0.96, 0.92 ...).")]
        [Range(0f, 0.3f)]
        [SerializeField] private float fasterPerWhack = 0.04f;
        [Tooltip("The quickest the heads ever come, and the shortest they stay up (seconds).")]
        [Min(0.05f)]
        [SerializeField] private float fastestPopEvery = 0.3f;
        [Min(0.1f)]
        [SerializeField] private float shortestUpSeconds = 0.55f;
        [Tooltip("Heads up at once at the start; one more every this many whacks in a row; never more than Most At Once.")]
        [Min(1)]
        [SerializeField] private int startAtOnce = 2;
        [Min(1)]
        [SerializeField] private int oneMoreEvery = 6;
        [Min(1)]
        [SerializeField] private int mostAtOnce = 6;
        [Tooltip("A blow counts as a whack from this speed (the mallet's head, metres per second) and not going up.")]
        [Min(0f)]
        [SerializeField] private float minWhackSpeed = 1.2f;
        [Tooltip("Whatever the combo, by the end of the countdown the heads come this much faster and stay up this much " +
                 "shorter (0.25 = 25%, growing evenly from GO), so the round builds up with the song.")]
        [Range(0f, 0.6f)]
        [SerializeField] private float speedUpByTheEnd = 0.25f;
        [Tooltip("More heads up at once in the last Warn Seconds (while the time ticks): a rush to the end.")]
        [Min(0)]
        [SerializeField] private int rushHeads = 1;

        [Header("Heads (the rest are normal ones)")]
        [Tooltip("Gold and time heads come rarely (user, 2026-11-23).")]
        [Range(0f, 1f)]
        [SerializeField] private float goldChance = 0.04f;
        [Range(0f, 1f)]
        [SerializeField] private float rainbowChance = 0.025f;
        [Range(0f, 1f)]
        [SerializeField] private float clockChance = 0.03f;
        [Range(0f, 1f)]
        [SerializeField] private float skullChance = 0.1f;
        [Min(0)]
        [SerializeField] private int normalCandies = 1;
        [Tooltip("A gold head is rare, so it gives a lot.")]
        [Min(0)]
        [SerializeField] private int goldCandies = 10;
        [Min(0)]
        [SerializeField] private int rainbowCandies = 15;
        [Tooltip("Seconds a time (clock) head adds: a whole number from Least to Most at random, shown on the head (\"+2s\").")]
        [Min(0)]
        [SerializeField] private int leastClockSeconds = 1;
        [Min(0)]
        [SerializeField] private int mostClockSeconds = 3;
        [Tooltip("Candies a skull head takes away (never below 0); it also ends the combo.")]
        [Min(0)]
        [SerializeField] private int skullCost = 3;
        [Tooltip("How long each kind stays up, times the normal time: gold, rainbow, clock, skull.")]
        [SerializeField] private Vector4 upTimes = new(0.8f, 0.5f, 0.8f, 1.2f);
        [Tooltip("No skulls (and no specials) in the first heads, so the start is plain.")]
        [Min(0)]
        [SerializeField] private int plainFirst = 4;

        [Header("Gold frenzy (user, 2026-11-23): a gold head whacked makes every head gold for a while")]
        [Tooltip("Seconds of frenzy after a gold head is whacked (0: none). Every head that pops meanwhile is gold, they come " +
                 "faster and more at once, each gives Frenzy Candies, and one going down unwhacked isn't a miss. Its gold " +
                 "heads don't start another frenzy (a real gold whacked during one starts it over).")]
        [Min(0f)]
        [SerializeField] private float frenzySeconds = 3f;
        [Min(0)]
        [SerializeField] private int frenzyCandies = 3;
        [Tooltip("In a frenzy the heads come this much more often (0.5 = twice as often).")]
        [Range(0.1f, 1f)]
        [SerializeField] private float frenzyPopEvery = 0.5f;
        [Tooltip("More heads up at once in a frenzy.")]
        [Min(0)]
        [SerializeField] private int frenzyHeads = 2;
        [SerializeField] private AudioClip frenzySound;

        [Header("Sounds")]
        [SerializeField] private AudioClip popSound;
        [Range(0f, 1f)]
        [SerializeField] private float popVolume = 0.35f;
        [SerializeField] private AudioClip[] whackSounds = System.Array.Empty<AudioClip>();
        [SerializeField] private AudioClip goldSound;
        [SerializeField] private AudioClip rainbowSound;
        [SerializeField] private AudioClip clockSound;
        [SerializeField] private AudioClip skullSound;
        [SerializeField] private AudioClip missSound;
        [Range(0f, 1f)]
        [SerializeField] private float missVolume = 0.5f;
        [Tooltip("Played every Combo Every whacks in a row.")]
        [SerializeField] private AudioClip comboSound;
        [Min(2)]
        [SerializeField] private int comboEvery = 10;

        [Header("Music")]
        [Tooltip("The round's song, one at random (user, 2026-11-23: the jigsaw puzzle's 90 s songs). It starts at GO, from " +
                 "where it ends just as the countdown does (like the jigsaw's); a time head's extra seconds put it back as " +
                 "many. Other music playing (a radio) is muted meanwhile. Empty: no song.")]
        [SerializeField] private AudioClip[] music = System.Array.Empty<AudioClip>();
        [Range(0f, 1f)]
        [SerializeField] private float musicVolume = 0.6f;

        [Header("Look")]
        [SerializeField] private TMP_FontAsset font;
        [Tooltip("Size of the +1 / MISS pop-ups over the heads (metres per point of font size).")]
        [Min(0.001f)]
        [SerializeField] private float popupScale = 0.02f;
        [Tooltip("A sparkle on a gold or rainbow whack (e.g. confetti). Empty: the puff.")]
        [SerializeField] private GameObject sparkleEffect;

        [Header("End")]
        [Tooltip("One of these, picked at random, per candy won.")]
        [SerializeField] private GameObject[] candies = System.Array.Empty<GameObject>();
        [Tooltip("The machine pours the candies out over about this long (seconds; less for a few).")]
        [Min(0f)]
        [SerializeField] private float payoutSeconds = 3.5f;
        [Tooltip("The Candy Helmet eats every candy before the machine goes: for this many seconds at the most.")]
        [Min(0f)]
        [SerializeField] private float candyWaitSeconds = 20f;
        [Tooltip("Seconds the scoreboard stays up after the candies, before the machine sinks.")]
        [Min(0f)]
        [SerializeField] private float boardSeconds = 4f;
        [Tooltip("Fireworks over the machine when the round gave candies (Firework_Win). Empty: none.")]
        [SerializeField] private GameObject winFireworks;
        [Min(0.1f)]
        [SerializeField] private float fireworkScale = 1f;

        private static readonly Color Gold = new(1f, 0.82f, 0.25f, 1f);
        private static readonly Color Green = new(0.45f, 1f, 0.45f, 1f);
        private static readonly Color Red = new(1f, 0.3f, 0.25f, 1f);
        private static readonly Color Pale = new(1f, 1f, 1f, 1f);
        private const float CandyReach = 15f;
        // Music this long or longer that's playing when the song starts (a radio) is muted meanwhile (as Paint Bees does).
        private const float OtherMusicLength = 30f;
        private const float PopupSeconds = 0.9f;
        private const float PopupRise = 0.3f;

        /// <summary>The round running now.</summary>
        public static WhackRound Running { get; private set; }

        /// <summary>The round is over and its candies are eaten: Play Bonus Round goes on.</summary>
        public bool Finished { get; private set; }

        /// <summary>The countdown is on (both mallets taken, time left).</summary>
        public bool Playing => playing;

        public int Candies { get; private set; }
        public int Whacked { get; private set; }
        public int Misses { get; private set; }

        /// <summary>The candies paid out at the end: those won times the share of heads hit (0 before the end).</summary>
        public int Payout { get; private set; }

        /// <summary>Candies won times hits over hits and misses (rounded): 13 hits and 39 misses pay a quarter.</summary>
        public static int PayoutFor(int candies, int whacked, int misses)
        {
            int tries = whacked + misses;
            if (candies <= 0 || tries <= 0) return 0;
            return Mathf.RoundToInt(candies * (float)whacked / tries);
        }
        public int Combo { get; private set; }
        public int BestCombo { get; private set; }
        public float TimeLeft => timeLeft;

        /// <summary>The machine, once it's out (null before the chest opens and after it sinks).</summary>
        public WhackMachine Machine => made;

        /// <summary>The two mallets, once they're out.</summary>
        public IReadOnlyList<WhackHammer> Hammers => hammers;

        private class Popup
        {
            public TextMeshPro text;
            public Vector3 from;
            public float born;
        }

        private readonly List<Popup> popups = new();
        private readonly List<WhackHammer> hammers = new();
        private readonly List<int> lastHoles = new();
        private readonly Dictionary<ContinuousMoveProvider, bool> moveWas = new();
        private WhackMachine made;
        private GameObject madeChest;
        private bool chestOpened;
        private Vector3 chestAt;
        private bool playing;
        private float timeLeft;
        private float nextPop;
        private int popped;
        private int lastTick = -1;
        private float messageUntil = -1f;
        private float nextStill;
        private float playedFor;
        private Transform head;
        private AudioSource song;
        private float nextHush;
        private readonly List<AudioSource> hushed = new();
        private float frenzyUntil = -1f;
        // Heads up now as a frenzy's gold (worth Frenzy Candies, no miss, no new frenzy).
        private readonly HashSet<WhackHead> frenzyGold = new();

        /// <summary>A gold frenzy is on: every head that pops is gold.</summary>
        public bool Frenzy => playing && Time.time < frenzyUntil;

        private void Awake()
        {
            Running = this;
        }

        private void Start()
        {
            if (stageSky != null) Instantiate(stageSky, transform.position, Quaternion.identity, transform);
            StartCoroutine(Run());
        }

        private void OnDestroy()
        {
            if (Running == this) Running = null;
            MovementBack();
            if (song != null) song.Stop();
            Unhush();
            foreach (var p in popups) if (p.text != null) Destroy(p.text.gameObject);
            popups.Clear();
            // Gone before it was over (e.g. a game over): its things go with it.
            foreach (var h in hammers) RemoveHammer(h, false);
            hammers.Clear();
            if (made != null) Destroy(made.gameObject);
            if (madeChest != null) Destroy(madeChest);
        }

        /// <summary>The chest broke open here (see WhackChestSeed).</summary>
        public void ChestOpened(Vector3 at)
        {
            if (chestOpened) return;
            chestOpened = true;
            chestAt = at;
        }

        private IEnumerator Run()
        {
            // 1. The chest.
            if (chest != null)
            {
                madeChest = Instantiate(chest, transform.position, Quaternion.identity, SpawnedStuff());
                PrepareChest(madeChest);
            }
            else ChestOpened(transform.position);
            while (!chestOpened) yield return null;
            madeChest = null;

            // 2. The machine rises where the chest was, facing away from the player (they come up behind its stand spot).
            var spot = new Vector3(chestAt.x, transform.position.y, chestAt.z);
            var eyes = Head();
            var forward = eyes != null ? Flat(spot - eyes.position) : Vector3.zero;
            if (forward.sqrMagnitude < 0.01f) forward = Flat(transform.forward);
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            yield return new WaitForSeconds(0.3f);
            if (machine == null) { Finished = true; yield break; }
            made = Instantiate(machine, spot, Quaternion.LookRotation(forward.normalized, Vector3.up), SpawnedStuff());
            made.transform.localScale = machine.transform.localScale * machineScale;
            made.Hide();
            foreach (var h in made.Heads)
            {
                if (h == null) continue;
                h.Whacked += OnWhacked;
                h.WentDown += OnWentDown;
            }
            Puff(spot + Vector3.up * 0.6f, puffSize * 2f);
            Play(riseSound, spot, 1f);
            ShakeView("ShakeMed");
            made.ShowTime(seconds, false);
            made.ShowCandies(0);
            made.ShowCombo(0);
            made.ShowBest(Game.Saving.WhackScores.BestCandies);
            made.ShowMessage("", Pale);
            yield return made.Rise();

            // 3. The mallets, one on each post.
            foreach (var post in made.HammerSpots)
            {
                if (post == null || hammer == null) continue;
                var h = Instantiate(hammer, post.position, post.rotation, SpawnedStuff());
                h.SetHome(post.position, post.rotation);
                hammers.Add(h);
                Puff(post.position, puffSize * 0.5f);
            }
            made.ShowRing(true);
            made.ShowMessage("GRAB BOTH\nMALLETS!", Gold);

            // 4. Both taken: go.
            while (!BothHeld()) yield return null;
            made.ShowMessage("GO!", Green);
            messageUntil = Time.time + 1f;
            Play(goSound, made.transform.position, 1f);
            timeLeft = seconds;
            playedFor = 0f;
            playing = true;
            nextPop = Time.time + 0.5f;
            KeepStill(true);
            StartMusic();
            while (timeLeft > 0f)
            {
                timeLeft -= Time.deltaTime;
                playedFor += Time.deltaTime;
                Tick();
                // Music started meanwhile (a radio's next song) is muted too.
                if (song != null && Time.unscaledTime >= nextHush) Hush();
                made.ShowTime(timeLeft, timeLeft <= warnSeconds);
                if (messageUntil > 0f && Time.time > messageUntil)
                {
                    made.ShowMessage("", Pale);
                    messageUntil = -1f;
                }
                KeepStill(false);
                if (Time.time >= nextPop && UpNow() < AtOnce() && PopOne())
                    nextPop = Time.time + PopEvery() * Random.Range(0.85f, 1.15f);
                yield return null;
            }

            // 5. Time's up.
            playing = false;
            timeLeft = 0f;
            frenzyUntil = -1f;
            frenzyGold.Clear();
            made.ShowTime(0f, true);
            foreach (var h in made.Heads) if (h != null) h.Hide();
            StartCoroutine(FadeMusic(1f));
            Play(timeUpSound, made.transform.position, 1f);
            made.ShowMessage("TIME'S UP!", Red);
            made.ShowRing(false);
            MovementBack();
            foreach (var h in hammers) RemoveHammer(h, true);
            hammers.Clear();
            // What's paid out: the candies won times the share of heads hit (user 2026-11-26: "Though I missed a lot, I
            // still get a lot of reward candies").
            Payout = PayoutFor(Candies, Whacked, Misses);
            int place = Game.Saving.WhackScores.Add(Payout, Whacked, BestCombo);
            yield return new WaitForSeconds(1f);
            made.ShowCombo(BestCombo);
            if (BestCombo >= 2) made.ShowCombo(0);
            made.HideTime();
            made.ShowCandies(Payout);
            made.ShowMessage((place == 0 && Payout > 0 ? "NEW BEST!\n" : "") + "WHACKED " + Whacked + "  MISSED " + Misses +
                             "\nBEST COMBO " + BestCombo, Gold);
            made.ShowBest(Game.Saving.WhackScores.BestCandies);

            // 6. The candies won pour out of the machine, and the helmet feeds them to the player.
            if (Payout > 0)
            {
                var spout = made.CandySpout;
                CandyFountain.Play(spout, Payout, candies, Mathf.Min(payoutSeconds, Payout * 0.08f), new Vector2(2f, 3.2f), new Vector2(0.5f, 1.4f));
                PlayerBuffs.Give(Buff.CandyHelmet, candyWaitSeconds);
                StartCoroutine(Fireworks(made.transform.position, made.transform.forward));
                yield return new WaitForSeconds(0.3f);
                for (float waited = 0f; waited < candyWaitSeconds; waited += Time.deltaTime)
                {
                    if (FindAnyObjectByType<CandyFountain>() == null && CandyHelmet.Flying == 0 && CandyHelmet.LyingAround(spout, CandyReach) == 0) break;
                    if (!PlayerBuffs.CandyHelmet) PlayerBuffs.Give(Buff.CandyHelmet, candyWaitSeconds - waited);
                    yield return null;
                }
            }
            yield return new WaitForSeconds(boardSeconds);

            // 7. The machine sinks back into the floor.
            Puff(made.transform.position + Vector3.up * 0.6f, puffSize * 2f);
            Play(riseSound, made.transform.position, 0.8f);
            yield return made.Sink();
            Destroy(made.gameObject);
            made = null;
            Finished = true;
        }

        private void Update()
        {
            UpdatePopups();
        }

        // ---------- The chest ----------

        // The nesting round's chest makes the giant from its Seed_Pos when it breaks open: here that FSM is off and
        // Whack Chest Seed tells this round instead.
        private void PrepareChest(GameObject made)
        {
            var seed = FindChild(made.transform, "Seed_Pos");
            if (seed == null)
            {
                StartCoroutine(OpenWhenGone(made));
                return;
            }
            foreach (var fsm in seed.GetComponents<PlayMakerFSM>()) fsm.enabled = false;
            seed.gameObject.AddComponent<WhackChestSeed>().Round = this;
        }

        // A chest with no Seed_Pos: opened = gone.
        private IEnumerator OpenWhenGone(GameObject made)
        {
            var at = made.transform.position;
            while (made != null)
            {
                at = made.transform.position;
                yield return null;
            }
            ChestOpened(at);
        }

        // ---------- The game ----------

        private bool BothHeld()
        {
            if (hammers.Count == 0) return true;
            foreach (var h in hammers) if (h == null || !h.Held) return false;
            return true;
        }

        private float Faster => Mathf.Pow(1f - fasterPerWhack, Combo);

        // Whatever the combo: evenly quicker from GO to the end of the countdown (Speed Up By The End).
        private float Later => 1f - speedUpByTheEnd * Mathf.Clamp01(playedFor / Mathf.Max(1f, seconds));

        // A gold frenzy makes them come Frenzy Pop Every as often.
        private float PopEvery() => Mathf.Max(fastestPopEvery, startPopEvery * Faster * Later) * (Frenzy ? frenzyPopEvery : 1f);

        private float UpSeconds() => Mathf.Max(shortestUpSeconds, startUpSeconds * Faster * Later);

        // The last Warn Seconds (the ticking) bring Rush Heads more at once, a gold frenzy Frenzy Heads more.
        private int AtOnce() => Mathf.Min(mostAtOnce, startAtOnce + Combo / Mathf.Max(1, oneMoreEvery)) +
                                (timeLeft <= warnSeconds ? rushHeads : 0) + (Frenzy ? frenzyHeads : 0);

        private int UpNow()
        {
            int up = 0;
            foreach (var h in made.Heads) if (h != null && h.Busy) up++;
            return up;
        }

        // A random free hole (not one of the last two used, if another is free) pops a head up.
        private bool PopOne()
        {
            var heads = made.Heads;
            var free = new List<int>();
            for (int i = 0; i < heads.Length; i++)
                if (heads[i] != null && !heads[i].Busy && !lastHoles.Contains(i)) free.Add(i);
            if (free.Count == 0)
                for (int i = 0; i < heads.Length; i++)
                    if (heads[i] != null && !heads[i].Busy) free.Add(i);
            if (free.Count == 0) return false;
            int index = free[Random.Range(0, free.Count)];
            lastHoles.Add(index);
            if (lastHoles.Count > 2) lastHoles.RemoveAt(0);

            var kind = PickKind();
            float up = UpSeconds() * kind switch
            {
                WhackHead.Kind.Gold => upTimes.x,
                WhackHead.Kind.Rainbow => upTimes.y,
                WhackHead.Kind.Clock => upTimes.z,
                WhackHead.Kind.Skull => upTimes.w,
                _ => 1f,
            };
            // A time head gives a whole number of seconds, Least to Most, and shows it ("+2s").
            int extra = kind == WhackHead.Kind.Clock
                ? Random.Range(Mathf.Min(leastClockSeconds, mostClockSeconds), Mathf.Max(leastClockSeconds, mostClockSeconds) + 1)
                : 0;
            heads[index].Pop(kind, up, minWhackSpeed, extra);
            if (Frenzy) frenzyGold.Add(heads[index]);
            else frenzyGold.Remove(heads[index]);
            popped++;
            Play(popSound, heads[index].transform.position, popVolume);
            return true;
        }

        private WhackHead.Kind PickKind()
        {
            if (Frenzy) return WhackHead.Kind.Gold;
            if (popped < plainFirst) return WhackHead.Kind.Normal;
            float r = Random.value;
            if ((r -= rainbowChance) < 0f) return WhackHead.Kind.Rainbow;
            if ((r -= goldChance) < 0f) return WhackHead.Kind.Gold;
            if ((r -= clockChance) < 0f) return WhackHead.Kind.Clock;
            if ((r -= skullChance) < 0f) return WhackHead.Kind.Skull;
            return WhackHead.Kind.Normal;
        }

        private void OnWhacked(WhackHead whacked, WhackHammer by)
        {
            if (!playing) return;
            var top = whacked.Top;
            if (whacked.Current == WhackHead.Kind.Skull)
            {
                int lost = Mathf.Min(skullCost, Candies);
                Candies -= lost;
                Combo = 0;
                Play(skullSound, top, 1f);
                ShowPopup(top, lost > 0 ? "-" + lost : "OOPS", Red, 1.2f);
                Puff(top, puffSize * 0.6f);
                made.ShowCandies(Candies);
                made.ShowCombo(0);
                return;
            }
            Whacked++;
            Combo++;
            BestCombo = Mathf.Max(BestCombo, Combo);
            if (whackSounds.Length > 0) Play(whackSounds[Random.Range(0, whackSounds.Length)], top, 1f);
            switch (whacked.Current)
            {
                case WhackHead.Kind.Gold:
                {
                    // A real gold head starts a frenzy (or starts it over); a frenzy's own gold heads give Frenzy Candies.
                    bool fromFrenzy = frenzyGold.Remove(whacked);
                    int got = fromFrenzy ? frenzyCandies : goldCandies;
                    Candies += got;
                    Play(goldSound, top, 1f);
                    Sparkle(top);
                    ShowPopup(top, "+" + got, Gold, fromFrenzy ? 1.1f : 1.4f);
                    if (!fromFrenzy) StartFrenzy();
                    break;
                }
                case WhackHead.Kind.Rainbow:
                    Candies += rainbowCandies;
                    Play(rainbowSound, top, 1f);
                    Sparkle(top);
                    ShowPopup(top, "+" + rainbowCandies, Color.HSVToRGB(Random.value, 0.6f, 1f), 1.8f);
                    break;
                case WhackHead.Kind.Clock:
                {
                    // Exactly the seconds its label showed.
                    int extra = whacked.ClockSeconds;
                    timeLeft += extra;
                    StartCoroutine(MusicBack(extra));
                    Play(clockSound, top, 1f);
                    ShowPopup(top, "+" + extra + "s", Green, 1.4f);
                    made.ShowMessage("+" + extra + (extra == 1 ? " SECOND" : " SECONDS"), Green);
                    messageUntil = Time.time + 1f;
                    break;
                }
                default:
                    Candies += normalCandies;
                    ShowPopup(top, "+" + normalCandies, Pale, 1f);
                    break;
            }
            Puff(top, puffSize * 0.35f);
            if (Combo % comboEvery == 0)
            {
                Play(comboSound, top, 1f);
                // (The board keeps "GOLD FRENZY!" while one is on.)
                if (!Frenzy)
                {
                    made.ShowMessage("COMBO " + Combo + "!", Gold);
                    messageUntil = Time.time + 1f;
                }
            }
            made.ShowCandies(Candies);
            made.ShowCombo(Combo);
        }

        // A real gold head whacked: Frenzy Seconds of nothing but gold heads, coming faster and more at once; the first
        // comes at once. The board says so and the bell rings (not again when a frenzy is started over).
        private void StartFrenzy()
        {
            if (frenzySeconds <= 0f || !playing) return;
            bool already = Frenzy;
            frenzyUntil = Time.time + frenzySeconds;
            nextPop = Mathf.Min(nextPop, Time.time + 0.05f);
            made.ShowMessage("GOLD FRENZY!", Gold);
            messageUntil = frenzyUntil;
            if (!already) Play(frenzySound, made.transform.position, 1f);
        }

        // A head went back down unwhacked: a miss (not a skull: leaving those is right). The combo and speed start over.
        private void OnWentDown(WhackHead head)
        {
            // A frenzy's gold head missed costs nothing.
            if (frenzyGold.Remove(head)) return;
            if (!playing || head.Current == WhackHead.Kind.Skull) return;
            Misses++;
            Combo = 0;
            Play(missSound, head.transform.position, missVolume);
            ShowPopup(head.transform.position + Vector3.up * 0.12f, "MISS", Red, 0.9f);
            made.ShowCombo(0);
        }

        // Ticks over the last Warn Seconds.
        private void Tick()
        {
            if (timeLeft > warnSeconds || tickSound == null) return;
            int whole = Mathf.CeilToInt(timeLeft);
            if (whole == lastTick || whole <= 0) return;
            lastTick = whole;
            Play(tickSound, made.transform.position, tickVolume);
        }

        // ---------- The song ----------

        // One of Music at random, from where it ends just as the countdown does (like the jigsaw's songs; one shorter
        // than the countdown loops from its start). Other music playing is muted meanwhile.
        private void StartMusic()
        {
            var songs = new List<AudioClip>();
            foreach (var clip in music) if (clip != null) songs.Add(clip);
            if (songs.Count == 0) return;
            var pick = songs[Random.Range(0, songs.Count)];
            if (song == null)
            {
                song = gameObject.AddComponent<AudioSource>();
                song.playOnAwake = false;
                song.spatialBlend = 0f;
                song.priority = 16;
            }
            song.clip = pick;
            song.loop = pick.length < seconds;
            song.volume = musicVolume;
            Hush();
            song.Play();
            // (Set once playing: a streamed song ignores it before.)
            if (!song.loop && pick.length - seconds > 0.05f) song.time = pick.length - seconds;
        }

        // A time head's extra seconds: the song goes back as many (with a quick dip), so it still ends with the countdown.
        private IEnumerator MusicBack(float extra)
        {
            if (song == null || !song.isPlaying || song.loop || extra <= 0f) yield break;
            const float down = 0.08f, up = 0.15f;
            for (float t = 0f; t < down && song != null; t += Time.unscaledDeltaTime)
            {
                song.volume = musicVolume * (1f - t / down);
                yield return null;
            }
            if (song == null) yield break;
            song.time = Mathf.Max(0f, song.time - extra);
            for (float t = 0f; t < up && song != null; t += Time.unscaledDeltaTime)
            {
                song.volume = musicVolume * (t / up);
                yield return null;
            }
            if (song != null) song.volume = musicVolume;
        }

        // Time's up: whatever is left of the song fades out, and the other music comes back.
        private IEnumerator FadeMusic(float fade)
        {
            if (song != null && song.isPlaying)
            {
                float from = song.volume;
                for (float t = 0f; t < fade && song != null; t += Time.unscaledDeltaTime)
                {
                    song.volume = from * (1f - t / fade);
                    yield return null;
                }
                if (song != null) song.Stop();
            }
            Unhush();
        }

        // Other music playing is muted (not paused: a radio's FSM would take a paused song for finished and play the next).
        private void Hush()
        {
            nextHush = Time.unscaledTime + 1f;
            foreach (var source in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            {
                if (source == null || source == song || source.mute || !source.isPlaying || source.clip == null ||
                    source.clip.length < OtherMusicLength) continue;
                source.mute = true;
                hushed.Add(source);
            }
        }

        private void Unhush()
        {
            foreach (var source in hushed) if (source != null) source.mute = false;
            hushed.Clear();
        }

        // ---------- The player stays put ----------

        // Thumbstick and arm-swing walking off (each one's state remembered once, given back by Movement Back), and no
        // teleporting (renewed every half second, so it lapses a moment after the round).
        private void KeepStill(bool now)
        {
            if (!keepStill) return;
            if (!now && Time.time < nextStill) return;
            nextStill = Time.time + 0.5f;
            foreach (var mover in FindObjectsByType<ContinuousMoveProvider>(FindObjectsSortMode.None))
            {
                if (mover == null) continue;
                if (!moveWas.ContainsKey(mover)) moveWas[mover] = mover.enabled;
                if (mover.enabled) mover.enabled = false;
            }
            PlayerSlow.Apply(1f, 1.2f, noTeleport: true);
        }

        private void MovementBack()
        {
            foreach (var pair in moveWas) if (pair.Key != null) pair.Key.enabled = pair.Value;
            moveWas.Clear();
        }

        // ---------- The mallets ----------

        // Out of the hands (if held) and gone, with a puff.
        private void RemoveHammer(WhackHammer h, bool puff)
        {
            if (h == null) return;
            var grab = h.Grab;
            if (grab != null && grab.isSelected && grab.interactionManager != null)
                grab.interactionManager.CancelInteractableSelection((UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)grab);
            if (puff) Puff(h.transform.position, puffSize * 0.5f);
            Destroy(h.gameObject);
        }

        // ---------- Pop-ups, puffs, sounds ----------

        private void ShowPopup(Vector3 at, string text, Color colour, float size)
        {
            var go = new GameObject("Whack Popup");
            go.transform.SetParent(SpawnedStuff(), true);
            go.transform.position = at;
            var tmp = go.AddComponent<TextMeshPro>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = 6f * size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = colour;
            tmp.outlineWidth = 0.2f;
            tmp.outlineColor = new Color32(30, 10, 40, 255);
            tmp.rectTransform.sizeDelta = new Vector2(8f, 3f);
            go.transform.localScale = Vector3.one * popupScale;
            popups.Add(new Popup { text = tmp, from = at, born = Time.time });
        }

        private void UpdatePopups()
        {
            var eyes = Head();
            for (int i = popups.Count - 1; i >= 0; i--)
            {
                var p = popups[i];
                float k = (Time.time - p.born) / PopupSeconds;
                if (p.text == null || k >= 1f)
                {
                    if (p.text != null) Destroy(p.text.gameObject);
                    popups.RemoveAt(i);
                    continue;
                }
                var t = p.text.transform;
                t.position = p.from + Vector3.up * (PopupRise * (1f - (1f - k) * (1f - k)));
                if (eyes != null) t.rotation = Quaternion.LookRotation(t.position - eyes.position, Vector3.up);
                var c = p.text.color;
                c.a = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
                p.text.color = c;
            }
        }

        private void Puff(Vector3 at, float size)
        {
            if (puffEffect != null && size > 0f) EffectPool.Play(puffEffect, at, size);
        }

        private void Sparkle(Vector3 at)
        {
            if (sparkleEffect != null) EffectPool.Play(sparkleEffect, at, 1f);
            else Puff(at, puffSize * 0.6f);
        }

        private static void Play(AudioClip clip, Vector3 at, float volume)
        {
            if (clip != null && volume > 0f) AudioSource.PlayClipAtPoint(clip, at, volume);
        }

        // Two fireworks beyond the machine, one each side, then one in the middle.
        private IEnumerator Fireworks(Vector3 at, Vector3 away)
        {
            if (winFireworks == null) yield break;
            away = Flat(away).sqrMagnitude > 0.01f ? Flat(away).normalized : Vector3.forward;
            var side = Vector3.Cross(Vector3.up, away);
            Firework(at + away * 3f + side * 2.2f);
            Firework(at + away * 3f - side * 2.2f);
            yield return new WaitForSeconds(1.4f);
            Firework(at + away * 4f);
        }

        private void Firework(Vector3 at)
        {
            var show = Instantiate(winFireworks, at, Quaternion.identity, SpawnedStuff());
            show.transform.localScale = Vector3.one * fireworkScale;
            Destroy(show, 5f);
        }

        // A camera shake event sent to the main camera's Info FSM (ShakeSmall, ShakeMed, ShakeBigger).
        private static void ShakeView(string eventName)
        {
            var cam = HutongGames.PlayMaker.FsmVariables.GlobalVariables.FindFsmGameObject("MainCam");
            if (cam == null || cam.Value == null) return;
            foreach (var fsm in cam.Value.GetComponents<PlayMakerFSM>())
                if (fsm.FsmName == "Info") fsm.SendEvent(eventName);
        }

        // ---------- Helpers ----------

        private Transform Head()
        {
            if (head == null && Camera.main != null) head = Camera.main.transform;
            return head;
        }

        private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

        private static Transform FindChild(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                var found = FindChild(child, name);
                if (found != null) return found;
            }
            return null;
        }

        // Spawned things go under the global CurrentlySpawnedStuffs, so the stages' clean-ups remove them.
        private static Transform SpawnedStuff()
        {
            var stuff = HutongGames.PlayMaker.FsmVariables.GlobalVariables.FindFsmGameObject("CurrentlySpawnedStuffs");
            return stuff != null && stuff.Value != null ? stuff.Value.transform : null;
        }
    }
}

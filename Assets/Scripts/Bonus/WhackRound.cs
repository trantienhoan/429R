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
    /// - Taking BOTH mallets starts the countdown (Seconds). Walking and teleporting stay off until it's over.
    /// - Pumpkin heads pop up and go back down. A quick downward blow with a mallet whacks one (hands do nothing):
    ///   normal 1 candy, gold 5, rainbow 15, clock +3 seconds, skull: don't (it takes 3 candies and the combo).
    /// - Every whack in a row makes the heads come faster, stay up shorter, and more at once. A head that goes back
    ///   down unwhacked (not a skull) is a miss: the combo and the speed start over.
    /// - Time's up: the heads drop, the mallets go, the round goes on the leaderboard (saved, WhackScores) and the
    ///   machine pours out every candy won, which the Candy Helmet feeds the player; then it sinks and the round is over
    ///   (Finished), and Play Bonus Round goes on.
    /// Inside it the player plays with level-0 stats (Play Bonus Round adds Special Stage Stats).
    /// </summary>
    [DisallowMultipleComponent]
    public class WhackRound : MonoBehaviour, IBonusGame
    {
        [Header("Stage")]
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

        [Header("Heads (the rest are normal ones)")]
        [Range(0f, 1f)]
        [SerializeField] private float goldChance = 0.1f;
        [Range(0f, 1f)]
        [SerializeField] private float rainbowChance = 0.025f;
        [Range(0f, 1f)]
        [SerializeField] private float clockChance = 0.06f;
        [Range(0f, 1f)]
        [SerializeField] private float skullChance = 0.1f;
        [Min(0)]
        [SerializeField] private int normalCandies = 1;
        [Min(0)]
        [SerializeField] private int goldCandies = 5;
        [Min(0)]
        [SerializeField] private int rainbowCandies = 15;
        [Tooltip("Seconds a clock head adds.")]
        [Min(0f)]
        [SerializeField] private float clockSeconds = 3f;
        [Tooltip("Candies a skull head takes away (never below 0); it also ends the combo.")]
        [Min(0)]
        [SerializeField] private int skullCost = 3;
        [Tooltip("How long each kind stays up, times the normal time: gold, rainbow, clock, skull.")]
        [SerializeField] private Vector4 upTimes = new(0.8f, 0.5f, 0.8f, 1.2f);
        [Tooltip("No skulls (and no specials) in the first heads, so the start is plain.")]
        [Min(0)]
        [SerializeField] private int plainFirst = 4;

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
        private Transform head;

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
            playing = true;
            nextPop = Time.time + 0.5f;
            KeepStill(true);
            while (timeLeft > 0f)
            {
                timeLeft -= Time.deltaTime;
                Tick();
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
            made.ShowTime(0f, true);
            foreach (var h in made.Heads) if (h != null) h.Hide();
            Play(timeUpSound, made.transform.position, 1f);
            made.ShowMessage("TIME'S UP!", Red);
            made.ShowRing(false);
            MovementBack();
            foreach (var h in hammers) RemoveHammer(h, true);
            hammers.Clear();
            int place = Game.Saving.WhackScores.Add(Candies, Whacked, BestCombo);
            yield return new WaitForSeconds(1f);
            made.ShowCombo(BestCombo);
            if (BestCombo >= 2) made.ShowCombo(0);
            made.ShowMessage((place == 0 && Candies > 0 ? "NEW BEST!\n" : "") + "WHACKED " + Whacked + "\nBEST COMBO " + BestCombo, Gold);
            made.ShowBest(Game.Saving.WhackScores.BestCandies);

            // 6. The candies won pour out of the machine, and the helmet feeds them to the player.
            if (Candies > 0)
            {
                var spout = made.CandySpout;
                CandyFountain.Play(spout, Candies, candies, Mathf.Min(payoutSeconds, Candies * 0.08f), new Vector2(2f, 3.2f), new Vector2(0.5f, 1.4f));
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

        private float PopEvery() => Mathf.Max(fastestPopEvery, startPopEvery * Faster);

        private float UpSeconds() => Mathf.Max(shortestUpSeconds, startUpSeconds * Faster);

        private int AtOnce() => Mathf.Min(mostAtOnce, startAtOnce + Combo / Mathf.Max(1, oneMoreEvery));

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
            heads[index].Pop(kind, up, minWhackSpeed);
            popped++;
            Play(popSound, heads[index].transform.position, popVolume);
            return true;
        }

        private WhackHead.Kind PickKind()
        {
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
                    Candies += goldCandies;
                    Play(goldSound, top, 1f);
                    Sparkle(top);
                    ShowPopup(top, "+" + goldCandies, Gold, 1.4f);
                    break;
                case WhackHead.Kind.Rainbow:
                    Candies += rainbowCandies;
                    Play(rainbowSound, top, 1f);
                    Sparkle(top);
                    ShowPopup(top, "+" + rainbowCandies, Color.HSVToRGB(Random.value, 0.6f, 1f), 1.8f);
                    break;
                case WhackHead.Kind.Clock:
                    timeLeft += clockSeconds;
                    Play(clockSound, top, 1f);
                    ShowPopup(top, "+" + Mathf.RoundToInt(clockSeconds) + "s", Green, 1.4f);
                    made.ShowMessage("+" + Mathf.RoundToInt(clockSeconds) + " SECONDS", Green);
                    messageUntil = Time.time + 1f;
                    break;
                default:
                    Candies += normalCandies;
                    ShowPopup(top, "+" + normalCandies, Pale, 1f);
                    break;
            }
            Puff(top, puffSize * 0.35f);
            if (Combo % comboEvery == 0)
            {
                Play(comboSound, top, 1f);
                made.ShowMessage("COMBO " + Combo + "!", Gold);
                messageUntil = Time.time + 1f;
            }
            made.ShowCandies(Candies);
            made.ShowCombo(Combo);
        }

        // A head went back down unwhacked: a miss (not a skull: leaving those is right). The combo and speed start over.
        private void OnWentDown(WhackHead head)
        {
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

        // ---------- The player stays put ----------

        // Thumbstick and arm-swing walking off (each one's state remembered once, given back by Movement Back), and no
        // teleporting (renewed every half second, so it lapses a moment after the round).
        private void KeepStill(bool now)
        {
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

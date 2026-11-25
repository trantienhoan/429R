using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace Game.Gates
{
    /// <summary>
    /// A gate's own game inside the shared gate flow (GateRun calls these on a component of the same object).
    /// </summary>
    public interface IGateGame
    {
        /// <summary>The start chest's seed is eaten (or its wait ran out): set the gate up for round 1 (pop the cauldron,
        /// grow the ball vines, drop the balls in...). Must also work when the seed was never eaten.</summary>
        void GateStart(GateRun run);

        /// <summary>Round 1, 2 or 3 begins: start its play. Show its goal with <c>run.Goal(text, needed)</c>, its count with
        /// <c>run.Progress(done)</c>, and call <c>run.RoundWon()</c> when it's done.</summary>
        void RoundBegin(GateRun run, int round);

        /// <summary>The round's time limit ran out: tidy it up. It counts as won: its chest still drops.</summary>
        void RoundTimeUp(GateRun run, int round);

        /// <summary>After round 3's chest: bring the giant pumpkin, normally with <c>run.Finale(...)</c>.</summary>
        void FinaleBegin(GateRun run);
    }

    /// <summary>
    /// The one flow every gate follows (user 2026-11-23: "make the flow simple and resemble for 3 gates, only the gameplay
    /// is different a bit for each gate"): the gate's card (a title and one line) -> the start chest, whose seed (Bracer)
    /// starts the gate -> rounds 1, 2 and 3, each ending with its gear chest (Chest, Head, Shield) -> the giant pumpkin
    /// (Gate Boss Finale) -> The_Seed -> Gate Exit -> the rooftop. The gate's own component (an <see cref="IGateGame"/>
    /// on this object) plays its rounds and reports them; this component does the rest the same way in every gate: the
    /// card, the chests and their seeds, the rounds' time limits (a round that runs out ends anyway, so a gate can't get
    /// stuck), one HUD line under the view, and the hand-off to the finale.
    /// Without an IGateGame (a test) the rounds end on their time limits and the finale drops in front of the player.
    /// </summary>
    [DisallowMultipleComponent]
    public class GateRun : MonoBehaviour
    {
        public enum Step { Arriving, StartChest, Round, RoundChest, Finale, Done }

        [Header("Card (when the gate opens)")]
        [Tooltip("Title of the card (empty: no card). The user's rule: a title and one short line.")]
        public string cardTitle = "COOK AND DEFEND";
        public string cardLine = "Cook the potion. Keep the pot safe.";
        public string cardCloseLine = "Pull a trigger to play";
        [Tooltip("Seconds after arriving before the card shows (it also waits while any other notice is up).")]
        [Min(0f)] public float cardDelay = 2.5f;

        [Header("Chests (the same chests in every gate = the same candy and EXP)")]
        [Tooltip("Dropped after the card; its seed (Bracer) starts the gate.")]
        public GameObject startChest;
        [Tooltip("Dropped at the end of rounds 1, 2 and 3 (their seeds: Chest, Head, Shield gear).")]
        public GameObject[] roundChests = new GameObject[3];
        [Tooltip("GAMESTAGES bools the 4 chests' seeds set: start, round 1, 2, 3. A bool already true = that chest's seed " +
                 "counts as eaten. Empty = not watched (a test).")]
        public string[] gearBools = { "Bracer_Gear", "Chest_Gear", "Head_Gear", "Shield_Gear" };
        [Tooltip("Seconds after the card before the start chest drops.")]
        [Min(0f)] public float startChestDelay = 1f;
        [Tooltip("A chest drops this far in front of the player (metres), unless the gate sets Chest Spot.")]
        [Min(0.3f)] public float chestAhead = 1.4f;
        [Tooltip("A chest's seed not eaten after this many seconds: the gate goes on anyway (the chest stays; Gate Exit " +
                 "eats any seed still lying about for the player).")]
        [Min(3f)] public float chestWaitMax = 25f;
        [Tooltip("The shared smoke puff where a chest drops (Smoke_Flash_Small_Puff).")]
        public GameObject chestPuff;
        [Min(0.01f)] public float chestPuffScale = 0.12f;

        [Header("Rounds")]
        [Tooltip("Each round's time limit in seconds: when it runs out the round ends anyway and its chest drops.")]
        public float[] roundSeconds = { 150f, 150f, 150f };
        [Tooltip("Seconds after a round's chest seed is eaten before the next round starts.")]
        [Min(0f)] public float roundBreak = 3f;
        [Tooltip("The HUD line during a round: {0} the round, {1} the rounds, {2} the goal and its count.")]
        public string roundLine = "ROUND {0}/{1}   {2}";
        [Tooltip("Flashed on the HUD line as a round starts ({0} the round).")]
        public string roundFlash = "ROUND {0}";
        [Tooltip("The time left shows on the line under this many seconds.")]
        [Min(0f)] public float showTimeUnder = 30f;

        [Header("Finale")]
        [Tooltip("The gate's giant pumpkin: its own variant of Prefabs/Gates/Pumpkin_Ball_Boss.")]
        public GameObject finaleBoss;
        [Tooltip("What it leaves (Items/Chest/The_Seed).")]
        public GameObject finaleSeed;
        public string finaleFlash = "BREAK IT!";
        [Tooltip("The HUD line while it's in ({0} its health).")]
        public string finaleLine = "GIANT PUMPKIN {0}";

        [Header("HUD line (under the view)")]
        [Tooltip("Metres ahead of the head, metres under the eyes.")]
        public Vector2 hudPlace = new Vector2(1.8f, 0.55f);
        [Min(0.1f)] public float hudSize = 2.2f;
        public Color hudColour = new Color(1f, 0.85f, 0.35f);

        [Header("Sounds (the user's own clips; empty = none)")]
        public AudioClip roundStartSound;
        public AudioClip roundWonSound;
        [Tooltip("A round's time ran out (in place of Round Won Sound; the round still counts).")]
        public AudioClip roundTimeUpSound;
        [Range(0f, 1f)] public float soundVolume = 0.8f;

        /// <summary>A round started (1-3).</summary>
        public event Action<GateRun, int> RoundStarted;
        /// <summary>A round ended (won, or its time ran out); its chest drops next.</summary>
        public event Action<GateRun, int> RoundEnded;
        /// <summary>The finale began (round 3's chest seed eaten).</summary>
        public event Action<GateRun> FinaleStarted;

        /// <summary>Where chests drop (a floor point), e.g. kept on an island; null = in front of the player.</summary>
        [NonSerialized] public Func<Vector3> chestSpot;

        /// <summary>The gate running now (null between gates).</summary>
        public static GateRun Current { get; private set; }
        public Step Now { get; private set; } = Step.Arriving;
        /// <summary>The round being played (1-3), 0 before the first, 4 in the finale.</summary>
        public int Round { get; private set; }
        public int Rounds => 3;
        public float RoundTimeLeft => Now == Step.Round ? Mathf.Max(0f, roundEndsAt - Time.time) : 0f;
        /// <summary>The finale's runner once Finale() was called (its Boss is the giant pumpkin once it's in).</summary>
        public GateBossFinale FinaleRun { get; private set; }

        private IGateGame game;
        private string goal = "";
        private int done, needed;
        private bool roundWon, chestSkip, toFinale;
        private float roundEndsAt;
        private TextMeshPro hud;
        private Transform hudRoot, head;
        private AudioSource speaker;
        private string hudOverride, hudFlash = "";
        private float hudFlashUntil, hudPulse;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Current = null;

        // ---------------------------------------------------------------- what the gate calls

        /// <summary>The round's goal on the HUD line, e.g. Goal("STRIKES", 3) shows "STRIKES 0/3"; needed 0 = just the text.</summary>
        public void Goal(string text, int need = 0)
        {
            goal = text ?? "";
            needed = Mathf.Max(0, need);
            done = 0;
        }

        /// <summary>The round's count so far (the HUD line pulses when it goes up).</summary>
        public void Progress(int count)
        {
            if (count > done) hudPulse = 1f;
            done = Mathf.Max(0, count);
        }

        /// <summary>The round is won: its chest drops, then the next round (or the finale).</summary>
        public void RoundWon()
        {
            if (Now == Step.Round) roundWon = true;
        }

        /// <summary>A short big text on the HUD line (round starts, the finale).</summary>
        public void Flash(string text, float seconds = 1.5f)
        {
            hudFlash = text ?? "";
            hudFlashUntil = Time.time + seconds;
            hudPulse = 1f;
        }

        /// <summary>Replaces the HUD line with the gate's own text (null = back to the round line).</summary>
        public void Line(string text) => hudOverride = text;

        /// <summary>
        /// The giant pumpkin: <paramref name="delay"/> seconds from now it drops onto <paramref name="landAt"/> (a floor
        /// point); <paramref name="setUp"/> sets it up (arena, lanes); broken, The_Seed drops where it broke (moved by
        /// <paramref name="seedSpot"/> if given) and Gate Exit takes the player to the rooftop. Called once.
        /// </summary>
        public GateBossFinale Finale(Vector3 landAt, float delay = 1f, Action<PumpkinBallBoss> setUp = null,
            Func<Vector3, Vector3> seedSpot = null)
        {
            if (FinaleRun != null) return FinaleRun;
            FinaleRun = GateBossFinale.Begin(finaleBoss, finaleSeed, landAt, delay, setUp, seedSpot, "[" + name + "]");
            return FinaleRun;
        }

        // ---------------------------------------------------------------- the flow

        private void Awake()
        {
            foreach (var c in GetComponents<MonoBehaviour>())
            {
                if (c is IGateGame g) { game = g; break; }
            }
        }

        private void OnEnable()
        {
            Current = this;
            // Launches close the comfort vignette in while a gate runs (gate 3's request; the main game unchanged).
            Game.Locomotion.ScriptedMoveVignette.Launches = true;
        }

        private void Start()
        {
            MakeHud();
            StartCoroutine(Run());
        }

        private void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
                Game.Locomotion.ScriptedMoveVignette.Launches = false;
            }
            if (hudRoot != null) Destroy(hudRoot.gameObject);
        }

        private IEnumerator Run()
        {
            Now = Step.Arriving;
            if (!string.IsNullOrEmpty(cardTitle))
            {
                yield return new WaitForSeconds(cardDelay);
                float giveUp = Time.realtimeSinceStartup + 15f;
                while (NoticeUp() && Time.realtimeSinceStartup < giveUp) yield return null;
                ShowCard();
                yield return null;
                while (NoticeUp()) yield return null;   // the card pauses the game until a trigger or a punch closes it
            }

            Now = Step.StartChest;
            yield return new WaitForSeconds(startChestDelay);
            yield return Chest(0);
            Call(g => g.GateStart(this), "GateStart");

            for (int r = 1; r <= Rounds && !toFinale; r++)
            {
                yield return new WaitForSeconds(r == 1 ? 0.5f : roundBreak);
                yield return PlayRound(r);
                Now = Step.RoundChest;
                if (!toFinale) yield return Chest(r);
            }

            Now = Step.Finale;
            Round = Rounds + 1;
            goal = "";
            Flash(finaleFlash, 2f);
            FinaleStarted?.Invoke(this);
            if (game != null) Call(g => g.FinaleBegin(this), "FinaleBegin");
            // Never stuck: a gate that didn't bring its giant pumpkin gets it in front of the player.
            for (float until = Time.time + 3f; FinaleRun == null && Time.time < until;) yield return null;
            if (FinaleRun == null)
            {
                if (game != null) Debug.LogWarning($"[{name}] FinaleBegin brought no giant pumpkin: it drops in front of the player.", this);
                Finale(Ahead(6f), 1f);
            }
            while (!GateExit.Running && !(FinaleRun != null && FinaleRun.Boss != null && FinaleRun.Boss.IsBroken)) yield return null;
            Now = Step.Done;
        }

        private IEnumerator PlayRound(int r)
        {
            Now = Step.Round;
            Round = r;
            roundWon = false;
            goal = "";
            done = needed = 0;
            float limit = roundSeconds != null && roundSeconds.Length >= r && roundSeconds[r - 1] > 0f ? roundSeconds[r - 1] : 150f;
            roundEndsAt = Time.time + limit;
            Play(roundStartSound);
            Flash(string.Format(roundFlash, r, Rounds), 1.5f);
            RoundStarted?.Invoke(this, r);
            Call(g => g.RoundBegin(this, r), "RoundBegin");
            bool timeUp = false;
            while (!roundWon && !toFinale)
            {
                if (Time.time >= roundEndsAt)
                {
                    Debug.Log($"[{name}] Round {r} ran out of time: it ends anyway.", this);
                    Call(g => g.RoundTimeUp(this, r), "RoundTimeUp");
                    timeUp = true;
                    break;
                }
                yield return null;
            }
            Play(timeUp && roundTimeUpSound != null ? roundTimeUpSound : roundWonSound);
            RoundEnded?.Invoke(this, r);
        }

        // Drops chest i (0 start, 1-3 rounds) and waits for its seed: its gear bool true, its wait run out, or a skip.
        private IEnumerator Chest(int i)
        {
            chestSkip = false;
            var prefab = i == 0 ? startChest : roundChests != null && roundChests.Length >= i ? roundChests[i - 1] : null;
            string gear = gearBools != null && gearBools.Length > i ? gearBools[i] : null;
            bool watch = !string.IsNullOrEmpty(gear) && GameStages.Fsm() != null;
            if (toFinale && i > 0) yield break;   // a test skipping to the finale: Gate Exit sets the missing gear
            var at = ChestSpot();
            GameObject chest = null;
            if (prefab != null)
            {
                chest = Instantiate(prefab, at, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f), Holder());
                if (chest.TryGetComponent<Rigidbody>(out var body) && !body.isKinematic) body.linearVelocity = Vector3.zero;
                chest.AddComponent<Game.Rooms.KeepFromPurpleHole>();   // a death mid-gate mustn't eat it (gate 3's request)
                if (chestPuff != null) EffectPool.Play(chestPuff, at, chestPuffScale);
                Debug.Log($"[{name}] Chest {i} ({prefab.name}) dropped" + (watch ? $", waiting for {gear}." : "."), this);
            }
            else Debug.LogWarning($"[{name}] No chest {i}: going on.", this);
            if (watch && GameStages.GetBool(gear)) yield break;   // already had it: its chest still gives candy and EXP
            if (prefab == null) yield break;

            float giveUpAt = Time.time + chestWaitMax;
            float goneAt = -1f;
            while (!chestSkip && !(toFinale && i > 0))
            {
                if (watch && GameStages.GetBool(gear)) break;
                // No GAMESTAGES (a test scene): the chest opened (gone) and a moment for its seed.
                if (!watch && chest == null)
                {
                    if (goneAt < 0f) goneAt = Time.time;
                    else if (Time.time - goneAt > 2f) break;
                }
                if (Time.time >= giveUpAt)
                {
                    Debug.Log($"[{name}] Chest {i}'s seed not eaten in {chestWaitMax} s: going on (it stays to open).", this);
                    break;
                }
                yield return new WaitForSeconds(0.25f);
            }
        }

        // ---------------------------------------------------------------- helpers

        private void Call(Action<IGateGame> call, string what)
        {
            if (game == null) return;
            try { call(game); }
            catch (Exception e) { Debug.LogException(e, this); Debug.LogError($"[{name}] {what} failed: the flow goes on.", this); }
        }

        private void ShowCard()
        {
            var notice = Game.Menus.TutorialNotice.Find();
            if (notice == null) return;
            notice.Show(new[] { new Game.Menus.TutorialNotice.Page(cardTitle, cardLine, null) },
                string.IsNullOrEmpty(cardCloseLine) ? null : cardCloseLine);
        }

        private static bool NoticeUp()
        {
            var notice = Game.Menus.TutorialNotice.Find();
            return notice != null && notice.IsShowing;
        }

        private Transform Holder()
        {
            var spawned = GateExit.Global("CurrentlySpawnedStuffs");
            return spawned != null ? spawned.transform : transform;
        }

        private Vector3 ChestSpot()
        {
            if (chestSpot != null)
            {
                try { return chestSpot(); }
                catch (Exception e) { Debug.LogException(e, this); }
            }
            return Ahead(chestAhead);
        }

        // A floor point this far in front of the player's head (straight ahead on the flat), or under the head.
        private Vector3 Ahead(float metres)
        {
            var cam = HeadCamera();
            if (cam == null) return transform.position;
            var fwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
            var p = cam.position + fwd.normalized * metres;
            if (Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out var hit, 6f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point;
            return new Vector3(p.x, cam.position.y - 1.6f, p.z);
        }

        private Transform HeadCamera()
        {
            if (head == null || !head.gameObject.activeInHierarchy)
            {
                var cam = Camera.main;
                head = cam != null ? cam.transform : null;
            }
            return head;
        }

        private void Play(AudioClip clip)
        {
            if (clip == null || speaker == null) return;
            speaker.PlayOneShot(clip, soundVolume);
        }

        // ---------------------------------------------------------------- the HUD line

        private void MakeHud()
        {
            hudRoot = new GameObject(name + " Line").transform;   // not under the stage: it follows the head, unscaled
            speaker = hudRoot.gameObject.AddComponent<AudioSource>();
            speaker.playOnAwake = false;
            speaker.spatialBlend = 0f;
            var go = new GameObject("Text");
            go.transform.SetParent(hudRoot, false);
            hud = go.AddComponent<TextMeshPro>();
            hud.alignment = TextAlignmentOptions.Center;
            hud.textWrappingMode = TextWrappingModes.NoWrap;
            hud.fontSize = hudSize;
            hud.fontStyle = FontStyles.Bold;
            hud.color = hudColour;
            hud.outlineWidth = 0.2f;
            hud.outlineColor = new Color32(30, 10, 0, 255);
            hud.rectTransform.sizeDelta = new Vector2(3f, 0.6f);
            hud.text = "";
        }

        private string LineNow()
        {
            if (hudOverride != null) return hudOverride;
            switch (Now)
            {
                case Step.Round:
                    string g = needed > 0 ? $"{goal} {Mathf.Min(done, needed)}/{needed}" : goal;
                    string line = string.Format(roundLine, Round, Rounds, g).TrimEnd();
                    float left = RoundTimeLeft;
                    if (showTimeUnder > 0f && left < showTimeUnder) line += $"   {Mathf.CeilToInt(left)}";
                    return line;
                case Step.Finale:
                    var boss = FinaleRun != null ? FinaleRun.Boss : null;
                    return boss != null && !boss.IsBroken ? string.Format(finaleLine, Mathf.CeilToInt(Mathf.Max(0f, boss.Health))) : "";
                default:
                    return "";
            }
        }

        private void LateUpdate()
        {
            if (hud == null) return;
            bool hidden = GateExit.Running || Now == Step.Done;
            bool flashing = !hidden && Time.time < hudFlashUntil && !string.IsNullOrEmpty(hudFlash);
            hud.text = hidden ? "" : flashing ? hudFlash : LineNow();
            hudPulse = Mathf.MoveTowards(hudPulse, 0f, Time.unscaledDeltaTime * 2.5f);
            hud.transform.localScale = Vector3.one * ((flashing ? 1.6f : 1f) * (1f + 0.25f * hudPulse));
            var cam = HeadCamera();
            if (cam == null) return;
            // Lazily under the view: follows the head's turn, never jumps.
            var fwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
            if (fwd.sqrMagnitude < 0.0001f) fwd = hudRoot.forward;
            fwd.Normalize();
            var want = cam.position + fwd * hudPlace.x - Vector3.up * hudPlace.y;
            float k = 1f - Mathf.Exp(-4f * Time.unscaledDeltaTime);
            hudRoot.position = Vector3.Lerp(hudRoot.position == Vector3.zero ? want : hudRoot.position, want, k);
            var look = hudRoot.position - cam.position;
            if (look.sqrMagnitude > 0.0001f) hudRoot.rotation = Quaternion.LookRotation(look, Vector3.up);
        }

        // ---------------------------------------------------------------- tests

        [ContextMenu("Test: Win This Round")]
        private void TestWinRound() => RoundWon();

        [ContextMenu("Test: Stop Waiting For This Chest")]
        private void TestSkipChest() => chestSkip = true;

        [ContextMenu("Test: Skip To The Finale")]
        private void TestToFinale()
        {
            toFinale = true;
            chestSkip = true;
        }

#if UNITY_EDITOR
        // Added in the editor: the shared chests, the giant pumpkin, The_Seed and the shared puff.
        private void Reset()
        {
            startChest = Load("Assets/Prefabs/Items/Chest/Treasure_Chest_and_Seed_Bracer.prefab");
            roundChests = new[]
            {
                Load("Assets/Prefabs/PlayGround/PG_Wood/PG_Treasure_Chest_and_Seed_Chest.prefab"),
                Load("Assets/Prefabs/PlayGround/PG_Wood/PG_Treasure_Chest_and_Seed_Head.prefab"),
                Load("Assets/Prefabs/PlayGround/PG_Wood/PG_Treasure_Chest_and_Seed_Shield.prefab"),
            };
            finaleBoss = Load("Assets/Prefabs/Gates/Pumpkin_Ball_Boss.prefab");
            finaleSeed = Load("Assets/Prefabs/Items/Chest/The_Seed.prefab");
            chestPuff = Load("Assets/Prefabs/Effects/Smoke/Smoke_Flash_Small_Puff.prefab");
        }

        private static GameObject Load(string path) => UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
#endif
    }
}

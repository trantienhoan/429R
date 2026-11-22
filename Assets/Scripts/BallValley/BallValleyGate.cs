using System.Collections.Generic;
using Game.Gates;
using TMPro;
using UnityEngine;

namespace Balls
{
    /// <summary>
    /// 429R gate 3 (user, 2026-11-18): Ball Valley as the gate's stage. "Running around, surviving, eating candy, pushing
    /// balls into holes; make it 15 balls; all 15 balls drop into holes or the last phase comes: the field reaches its final
    /// small stage, drop a giant boss ball, a pumpkin head ball ... only attack, punch, special move decrease the giant boss
    /// health; break the pumpkin boss to drop the seed to get out of here and move to defend tree stage like the main game."
    /// - Balls (15) drop into the valley; pocketed, a ball is gone for good (Ball Valley Stage's gate rules). The player
    ///   (Ball Valley Player) punches them, walks into them, dashes into them, eats the field's candies, and stays out of
    ///   the holes.
    /// - The valley shrinks in Phases on its clock (First Shrink, then evenly over Shrink Span) down to Smallest.
    /// - Every ball pocketed, or the last phase reached: the finale. The valley goes straight to its smallest (Finale
    ///   Shrink Seconds), and Boss Delay later the giant pumpkin (Boss Prefab, a Pumpkin Ball Boss) drops in, away from the
    ///   player. Balls still left stay in (it can knock them into the holes).
    /// - The boss broken: The_Seed drops where it broke (Gate Exit): eaten, GAMESTAGES takes the player to the rooftop
    ///   (the tree defence, as in the main game).
    /// A short line floats under the player's view: the balls left, what's happening, the boss's health.
    /// Set up by Tools > 429 Game > Ball Valley > Set Up Ball Valley (on Prefabs/BallValley/Ball_Valley).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BallValleyStage))]
    public class BallValleyGate : MonoBehaviour
    {
        [Header("The balls")]
        [Tooltip("Balls (bots) in the valley; each pocketed one is gone for good.")]
        [Min(1)] public int balls = 15;
        [Tooltip("Their tempers, x = least to y = most (0 = grazes and rarely fights, 1 = hunts nonstop).")]
        public Vector2 temper = new Vector2(0.5f, 0.75f);
        [Tooltip("Most balls hunting the player at once (the rest hunt each other). Play test 2026-11-18: all 15 going for a " +
                 "player standing still was a death in a minute.")]
        [Min(0)] public int maxHunters = 1;

        [Header("The valley")]
        [Tooltip("Seconds before the valley first shrinks.")]
        [Min(5f)] public float firstShrink = 40f;
        [Tooltip("Seconds from the first shrink to the last (the phases evenly between).")]
        [Min(5f)] public float shrinkSpan = 150f;
        [Tooltip("How many sizes the valley has, full size first, Smallest last.")]
        [Range(2, 8)] public int phases = 5;
        [Tooltip("Its last, smallest size (share of full size): the boss's arena. 0.5 = 25 x 12.5 m.")]
        [Range(0.3f, 1f)] public float smallest = 0.5f;
        [Tooltip("All balls pocketed early: seconds the valley takes to shrink straight to its smallest.")]
        [Min(0.5f)] public float finaleShrinkSeconds = 6f;

        [Header("The boss")]
        [Tooltip("The giant pumpkin (Prefabs/Gates/Pumpkin_Ball_Boss).")]
        public GameObject bossPrefab;
        [Tooltip("Seconds after the valley is at its smallest before it drops in.")]
        [Min(0f)] public float bossDelay = 2f;
        [Tooltip("What it leaves when it breaks (Items/Chest/The_Seed): eaten, GAMESTAGES goes to the rooftop.")]
        public GameObject seedPrefab;
        [Tooltip("A ball the boss runs into is knocked on at least this fast (m/s).")]
        [Min(0f)] public float bossKnock = 9f;

        [Header("The player's view")]
        [Tooltip("The rules when the valley opens (pauses until a trigger or a punch). Empty title: none.")]
        public string rulesTitle = "BALL VALLEY";
        public string rulesBody = "Punch all 15 balls into the holes.\nDon't fall in!";
        [Tooltip("The line floating under the view: metres ahead, metres under the eyes.")]
        public Vector2 hudPlace = new Vector2(1.8f, 0.55f);
        [Min(0.1f)] public float hudSize = 0.9f;

        public enum Step { Balls, Finale, Boss, Won }

        /// <summary>Where the gate is.</summary>
        public Step Now { get; private set; } = Step.Balls;
        /// <summary>Balls still in play (in the valley, or still to drop in).</summary>
        public int BallsLeft { get; private set; }
        /// <summary>The boss, once it's in.</summary>
        public PumpkinBallBoss Boss => boss;

        private BallValleyStage stage;
        private PumpkinBallBoss boss;
        private float bossAt = -1f;
        private bool rulesShown;
        private TextMeshPro hud;
        private Transform hudRoot;
        private Transform head;
        private string hudLine = "";
        private string hudFlash = "";
        private float hudFlashUntil;
        private float hudPulse;

        private void Awake()
        {
            stage = GetComponent<BallValleyStage>();
            // The gate's own clock and sizes, read by the stage as it runs (its Tuning's numbers are BALLS' rounds).
            var t = stage.tune;
            t.shrinkDelay = firstShrink;
            t.shrinkDuration = shrinkSpan;
            t.phases = phases;
            t.minMapScale = smallest;
            t.selfPocketGift = 0;
            t.momentOfTruthCrumble = false;   // the border stays: the boss can't roll out, nor the player fall off an edge
        }

        private void OnEnable()
        {
            stage = GetComponent<BallValleyStage>();
            stage.BallPocketed += OnPocketed;
            stage.LastPhaseReached += OnLastPhase;
            stage.Shrinks += OnShrinks;
        }

        private void OnDisable()
        {
            if (stage == null) return;
            stage.BallPocketed -= OnPocketed;
            stage.LastPhaseReached -= OnLastPhase;
            stage.Shrinks -= OnShrinks;
        }

        private void OnDestroy()
        {
            if (hudRoot != null) Destroy(hudRoot.gameObject);
        }

        private float bornAt;

        private void Start()
        {
            bornAt = Time.time;
            MakeHud();
            CountBalls();
            Line($"BALLS {BallsLeft}");
        }

        private void Update()
        {
            // The rules once the view is back (GAMESTAGES fades in as the valley opens), after any other notice that's up
            // (the gates' WORKING SITE note shows at the same moment: it would be replaced, user 2026-11-20).
            if (!rulesShown && Time.time - bornAt > 1.5f && !OtherNoticeUp())
            {
                rulesShown = true;
                ShowRules();
            }
            switch (Now)
            {
                case Step.Balls:
                    CountBalls();
                    Line($"BALLS {BallsLeft}");
                    // Every ball gone (once they've all dropped in): the finale, early.
                    if (BallsLeft == 0 && stage.players.Count > 0) Finale("ALL IN!");
                    break;
                case Step.Finale:
                    CountBalls();
                    if (bossAt < 0f && stage.AtLastPhase) bossAt = Time.time + bossDelay;
                    if (bossAt >= 0f && Time.time >= bossAt) DropBoss();
                    break;
                case Step.Boss:
                    if (boss != null && !boss.IsBroken) Line($"BOSS {Mathf.Max(0f, boss.Health):0}");
                    break;
            }
            UpdateHud();
        }

        private void CountBalls()
        {
            int left = 0;
            foreach (var p in stage.players)
            {
                if (p == null || p.isHuman) continue;
                if (p.alive || p.CanDropIn) left++;
            }
            BallsLeft = left;
        }

        private void OnPocketed(BallPlayer p, BallPlayer by)
        {
            if (p == null || p.isHuman) return;
            CountBalls();
            hudPulse = 1f;
            if (Now == Step.Balls && BallsLeft > 0) Flash(BallsLeft == 1 ? "1 BALL LEFT!" : $"{BallsLeft} LEFT!", 1.2f);
        }

        private void OnShrinks(int toPhase, bool last)
        {
            if (Now != Step.Balls) return;
            Flash(last ? "LAST STAND!" : "FIELD SHRINKS!", 2f);
        }

        private void OnLastPhase()
        {
            if (Now == Step.Balls) Finale("BOSS INCOMING!");
        }

        /// <summary>The finale: the valley to its smallest, then the boss (Boss Delay after it's there).</summary>
        private void Finale(string say)
        {
            if (Now != Step.Balls) return;
            Now = Step.Finale;
            stage.ShrinkToLast(finaleShrinkSeconds);
            Flash(say, 2.5f);
            stage.AddFeed("Gate 3: the finale (" + BallsLeft + " balls left)", new Color(1f, 0.5f, 0.2f));
        }

        private void DropBoss()
        {
            Now = Step.Boss;
            if (bossPrefab == null)
            {
                Debug.LogError("[BallValley] No Boss Prefab on the Ball Valley Gate (Set Up Ball Valley sets it): the seed drops now.", this);
                Won(Vector3.zero);
                return;
            }
            // In the middle of the field's far half from the player, so it doesn't land on them.
            var playerAt = PlayerFeet();
            var valley = stage.valley;
            float side = playerAt.x > 0f ? -1f : 1f;
            var at = new Vector3(side * valley.HalfX * 0.35f, 0f, 0f);
            boss = PumpkinBallBoss.Spawn(bossPrefab, at, transform);
            if (boss == null)
            {
                Won(Vector3.zero);
                return;
            }
            boss.floorY = 0f;   // the lawn's top
            boss.arenaCentre = Vector3.zero;
            // On the lawn, all of it inside the border (so never into a hole's gap either).
            boss.canBeAt = p =>
            {
                var v = stage.valley;
                if (!v) return true;
                float r = boss.radius;
                return Mathf.Abs(p.x) <= v.HalfX - r && Mathf.Abs(p.z) <= v.HalfZ - r;
            };
            boss.touched = BossTouched;
            boss.landed = BossLanded;
            boss.Broken += OnBossBroken;
            Flash("BREAK IT!", 2.5f);
            stage.AddFeed("Gate 3: the giant pumpkin drops in", new Color(1f, 0.5f, 0.2f));
        }

        // The boss ran into one of the valley's balls: it flies off, knocked (no steering until it rolls out).
        private void BossTouched(Collider c, Vector3 bossVelocity)
        {
            if (boss == null || c == null) return;
            Rigidbody rb = null;
            BallPart part = c.GetComponent<BallPart>();
            LooseBall loose = null;
            if (part != null && part.owner != null && !part.owner.vrBody) rb = part.rb;
            else if (c.TryGetComponent(out loose)) rb = loose.rb;
            else if (c.TryGetComponent(out Slime _)) rb = c.attachedRigidbody;
            if (rb == null || rb.isKinematic) return;
            var away = BallValleyStage.Flat(rb.position - boss.transform.position);
            away = away.sqrMagnitude > 0.0001f ? away.normalized : BallValleyStage.Flat(bossVelocity).normalized;
            float speed = Mathf.Max(bossKnock, bossVelocity.magnitude * 1.3f);
            var v = away * speed;
            rb.linearVelocity = new Vector3(v.x, Mathf.Min(rb.linearVelocity.y, 0f), v.z);
            if (part != null && part.isHead) part.owner.Knock(Time.time + 0.3f);
            else if (part != null) part.freeUntil = Time.time + 0.5f;
            else if (loose != null) loose.Knock(Time.time + 0.3f);
            Sfx.Play(Sfx.Sound.SpecialHit, rb.position, 0.7f);
        }

        // Its landing throws the valley's balls round it outwards.
        private void BossLanded(Vector3 middle, float reach)
        {
            foreach (var p in stage.players)
            {
                if (p == null || !p.alive || p.vrBody || p.head == null) continue;
                Shove(p.head.rb, middle, reach);
                if (BallValleyStage.Flat(p.Position - middle).magnitude < reach) p.Knock(Time.time + 0.3f);
                foreach (var b in p.babies) if (b != null) Shove(b.rb, middle, reach);
            }
            foreach (var l in stage.looseBalls) if (l != null) Shove(l.rb, middle, reach);
            foreach (var s in stage.slimes) if (s != null) Shove(s.GetComponent<Rigidbody>(), middle, reach);
        }

        private static void Shove(Rigidbody rb, Vector3 middle, float reach)
        {
            if (rb == null || rb.isKinematic) return;
            var away = BallValleyStage.Flat(rb.position - middle);
            float d = away.magnitude;
            if (d >= reach) return;
            away = d > 0.01f ? away / d : Vector3.right;
            float speed = Mathf.Lerp(14f, 4f, d / reach);
            rb.linearVelocity = away * speed + Vector3.up * Mathf.Min(rb.linearVelocity.y, 0f);
        }

        private void OnBossBroken(PumpkinBallBoss b)
        {
            var at = b != null ? new Vector3(b.transform.position.x, 0f, b.transform.position.z) : Vector3.zero;
            Won(at);
        }

        // The seed drops (kept on the lawn, clear of the holes), and Gate Exit takes it from there.
        private void Won(Vector3 at)
        {
            Now = Step.Won;
            var valley = stage.valley;
            if (valley)
            {
                float lx = Mathf.Max(0.5f, valley.HalfX - 2f), lz = Mathf.Max(0.5f, valley.HalfZ - 2f);
                at.x = Mathf.Clamp(at.x, -lx, lx);
                at.z = Mathf.Clamp(at.z, -lz, lz);
                // Pulled towards the middle until it's well clear of every pit.
                for (int i = 0; i < 20 && valley.DistanceToNearestPit(at) < 2.5f; i++) at *= 0.85f;
            }
            at.y = 0f;
            Flash("TAKE THE SEED!", 4f);
            Line("");
            GateExit.Begin(gameObject, seedPrefab, at, "[BallValley]");
        }

        // ---------------------------------------------------------------- the player's view

        private static bool OtherNoticeUp()
        {
            var notice = Game.Menus.TutorialNotice.Find();
            return notice != null && notice.IsShowing;
        }

        private void ShowRules()
        {
            if (string.IsNullOrEmpty(rulesTitle)) return;
            var notice = Game.Menus.TutorialNotice.Find();
            if (notice == null) return;
            notice.Show(new[] { new Game.Menus.TutorialNotice.Page(rulesTitle, rulesBody.Replace("15", balls.ToString()), null) },
                "Pull a trigger to play");
        }

        private void MakeHud()
        {
            hudRoot = new GameObject("Ball Valley Line").transform;   // not under the stage: it follows the head, unscaled
            var go = new GameObject("Text");
            go.transform.SetParent(hudRoot, false);
            hud = go.AddComponent<TextMeshPro>();
            hud.alignment = TextAlignmentOptions.Center;
            hud.textWrappingMode = TextWrappingModes.NoWrap;
            hud.fontSize = hudSize;
            hud.fontStyle = FontStyles.Bold;
            hud.color = new Color(1f, 0.85f, 0.35f);
            hud.outlineWidth = 0.2f;
            hud.outlineColor = new Color32(30, 10, 0, 255);
            hud.rectTransform.sizeDelta = new Vector2(3f, 0.6f);
            hud.text = "";
        }

        private void Line(string text) => hudLine = text ?? "";

        private void Flash(string text, float seconds)
        {
            hudFlash = text;
            hudFlashUntil = Time.time + seconds;
            hudPulse = 1f;
        }

        private void UpdateHud()
        {
            if (hud == null) return;
            if (head == null || !head.gameObject.activeInHierarchy)
            {
                var cam = Camera.main;
                head = cam != null ? cam.transform : null;
            }
            bool flashing = Time.time < hudFlashUntil && !string.IsNullOrEmpty(hudFlash);
            hud.text = flashing ? hudFlash : hudLine;
            hudPulse = Mathf.MoveTowards(hudPulse, 0f, Time.deltaTime * 2.5f);
            hud.transform.localScale = Vector3.one * ((flashing ? 1.6f : 1f) * (1f + 0.25f * hudPulse));
            if (head == null) return;
            // Lazily under the view: follows the head's turn, never jumps.
            var fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (fwd.sqrMagnitude < 0.0001f) fwd = hudRoot.forward;
            fwd.Normalize();
            var want = head.position + fwd * hudPlace.x - Vector3.up * hudPlace.y;
            float k = 1f - Mathf.Exp(-4f * Time.unscaledDeltaTime);
            hudRoot.position = Vector3.Lerp(hudRoot.position == Vector3.zero ? want : hudRoot.position, want, k);
            var look = hudRoot.position - head.position;
            if (look.sqrMagnitude > 0.0001f) hudRoot.rotation = Quaternion.LookRotation(look, Vector3.up);
        }

        private Vector3 PlayerFeet()
        {
            var cam = Camera.main;
            return cam != null ? new Vector3(cam.transform.position.x, 0f, cam.transform.position.z) : Vector3.zero;
        }
    }
}

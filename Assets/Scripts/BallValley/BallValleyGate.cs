using System.Collections;
using Game.Gates;
using UnityEngine;

namespace Balls
{
    /// <summary>
    /// 429R gate 3, Ball Valley: "running and snookering" (user, 2026-11-23: one flow for all three gates, only the
    /// gameplay differs a bit). The shared Gate Run on this object does the card, the chests, the HUD line, the rounds'
    /// time limits and the finale hand-off; this component plays the valley's part (Gate Run's IGateGame):
    /// - Start (the Bracer seed eaten): the valley is open, its candies out, no balls yet.
    /// - Round 1, 2, 3: Balls Per Round balls drop in (tempers a bit harder each round) and, from round 2, the valley shrinks
    ///   a step; the goal is to pocket that many (any pocket counts; the player's own also pops candy out of the hole).
    ///   Bots don't hunt each other here, only the player, a few at a time: the pocketing is the player's. A round's time
    ///   limit ends it anyway; its leftover balls stay for the next round.
    /// - Finale: the leftover balls go (a puff each), the valley shrinks to its smallest and the giant pumpkin (Gate Run's
    ///   Finale Boss) drops in on the far half from the player; broken, The_Seed and Gate Exit (shared) take the player to
    ///   the rooftop. The valley stays (out of Currently Spawned Stuffs, which GAMESTAGES clears as the seed is eaten)
    ///   until the player is on the rooftop.
    /// The 11-18 version (15 balls at once on a shrink clock, its own rules notice and HUD line, the gear bools flipped at
    /// the end) is gone. Set up by Tools > 429 Game > Ball Valley > Set Up Ball Valley (on Prefabs/BallValley/Ball_Valley).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BallValleyStage))]
    public class BallValleyGate : MonoBehaviour, IGateGame
    {
        [Header("The balls (rounds 1, 2, 3)")]
        [Tooltip("Balls dropped in at each round's start; pocketing this many wins the round.")]
        [Min(1)] public int ballsPerRound = 5;
        [Tooltip("Their tempers each round, x = least to y = most (0 = grazes and rarely fights, 1 = hunts nonstop).")]
        public Vector2[] tempers = { new Vector2(0.35f, 0.55f), new Vector2(0.45f, 0.65f), new Vector2(0.55f, 0.8f) };
        [Tooltip("Most balls hunting the player at once, each round (the rest graze: here they never hunt each other).")]
        public int[] maxHuntersPerRound = { 1, 1, 2 };
        [Tooltip("Seconds over which a round's balls drop in.")]
        [Min(0f)] public float dropInSeconds = 3f;
        [Tooltip("A ball the player pocketed pops this many candies out of the hole onto the lawn (0 = none).")]
        [Min(0)] public int pocketCandies = 3;
        [Tooltip("A punched (knocked) ball's braking while it rolls out (m/s^2; BALLS: 5): lower = it rolls further, like a " +
                 "snooker ball, before its bot steers it again.")]
        [Min(0f)] public float punchedBallBraking = 1.5f;
        [Tooltip("...and the speed under which its bot has it back (m/s; BALLS: 5).")]
        [Min(0f)] public float punchedBallControlBelow = 2.5f;

        [Header("The valley (these replace the Ball Valley Stage's own Tuning when the gate starts)")]
        [Tooltip("Its smallest size (share of the full 50 x 25 m), the giant pumpkin's arena. Rounds 1, 2, 3 and the finale " +
                 "go evenly from full size to this: 0.5 = 1, 0.83, 0.67, 0.5.")]
        [Range(0.3f, 1f)] public float smallest = 0.5f;
        [Tooltip("Seconds one shrink step takes (rounds 2 and 3).")]
        [Min(0.5f)] public float shrinkSeconds = 4f;
        [Tooltip("Seconds the valley takes to shrink to its smallest for the finale.")]
        [Min(0.5f)] public float finaleShrinkSeconds = 6f;
        [Tooltip("A new candy on the lawn at most this often (seconds) while there are fewer than its cap (2026-11-23: was " +
                 "0.16 s; each is a whole candy prefab, three FSMs).")]
        [Min(0.05f)] public float candyEvery = 0.3f;

        [Header("The boss (Gate Run's Finale Boss)")]
        [Tooltip("Seconds after the valley is at its smallest before it drops in.")]
        [Min(0f)] public float bossDelay = 2f;
        [Tooltip("A ball the boss runs into is knocked on at least this fast (m/s).")]
        [Min(0f)] public float bossKnock = 9f;

        /// <summary>The giant pumpkin, once it's in.</summary>
        public PumpkinBallBoss Boss => run != null && run.FinaleRun != null ? run.FinaleRun.Boss : null;
        /// <summary>Balls pocketed this round.</summary>
        public int Pocketed { get; private set; }
        /// <summary>The round being played (1-3), 0 before, 4 in the finale.</summary>
        public int Round => round;

        private BallValleyStage stage;
        private GateRun run;
        private int round;
        private bool leaving;

        private void Awake()
        {
            stage = GetComponent<BallValleyStage>();
            run = GetComponent<GateRun>();
            // The gate drives the valley's sizes itself (no clock); BALLS' gifts, crumbling and Moment of Truth off (the
            // finale is the giant pumpkin's); its candy speed; punched balls roll on like snooker balls.
            var t = stage.tune;
            t.phases = 4;
            t.minMapScale = smallest;
            t.shrinkDelay = 1e6f;
            t.selfPocketGift = 0;
            t.momentOfTruth = false;
            t.momentOfTruthCrumble = false;
            t.pelletSpawnInterval = candyEvery * Mathf.Max(0.1f, t.foodBoost);   // (the stage divides it by Food Boost)
            t.knockFriction = punchedBallBraking;
            t.knockRecoverSpeed = punchedBallControlBelow;
            stage.botsHuntBots = false;
            stage.startBalls = 0;   // they drop in round by round
            SetRoundTemper(1);
        }

        private void OnEnable()
        {
            stage = GetComponent<BallValleyStage>();
            stage.BallPocketed += OnPocketed;
        }

        private void OnDisable()
        {
            if (stage != null) stage.BallPocketed -= OnPocketed;
        }

        private void Start()
        {
            if (run == null)
            {
                Debug.LogError("[BallValley] No Gate Run on the Ball Valley (Set Up Ball Valley adds it): no rounds, no chests.", this);
                return;
            }
            run.chestSpot = ChestSpot;
        }

        private void Update()
        {
            // The exit has begun (the seed is out): the valley stands alone until the player is on the rooftop.
            if (!leaving && GateExit.Running)
            {
                leaving = true;
                transform.SetParent(null, true);
                StartCoroutine(GoWhenOnTheRoof());
            }
        }

        // ---------------------------------------------------------------- Gate Run's calls

        public void GateStart(GateRun gateRun) { }   // (the valley is open from the start; round 1 drops the balls in)

        public void RoundBegin(GateRun gateRun, int r)
        {
            round = r;
            Pocketed = 0;
            SetRoundTemper(r);
            if (r > 1)
            {
                stage.ShrinkToPhase(r - 1, shrinkSeconds);
                Sfx.Play(Sfx.Sound.Shrink, PlayerFeet());
            }
            stage.DropBalls(ballsPerRound, dropInSeconds);
            run.Goal("BALLS", ballsPerRound);
            run.Progress(0);
        }

        public void RoundTimeUp(GateRun gateRun, int r) { }   // (its leftover balls stay for the next round)

        public void FinaleBegin(GateRun gateRun)
        {
            round = 4;
            ClearBalls();
            float shrink = stage.AtLastPhase ? 0f : finaleShrinkSeconds;
            stage.ShrinkToLast(finaleShrinkSeconds);
            if (shrink > 0f) Sfx.Play(Sfx.Sound.Shrink, PlayerFeet());
            run.Finale(BossSpot(), shrink + bossDelay, SetUpBoss, SeedSpot);
        }

        // ---------------------------------------------------------------- the rounds

        private void SetRoundTemper(int r)
        {
            int i = Mathf.Clamp(r - 1, 0, Mathf.Max(0, tempers.Length - 1));
            var tm = tempers.Length > 0 ? tempers[i] : new Vector2(0.4f, 0.6f);
            stage.tune.botAggressionMin = Mathf.Clamp01(Mathf.Min(tm.x, tm.y));
            stage.tune.botAggressionMax = Mathf.Clamp01(Mathf.Max(tm.x, tm.y));
            stage.maxHunters = maxHuntersPerRound.Length > 0 ? Mathf.Max(0, maxHuntersPerRound[Mathf.Clamp(r - 1, 0, maxHuntersPerRound.Length - 1)]) : 1;
        }

        private void OnPocketed(BallPlayer p, BallPlayer by)
        {
            if (p == null || p.isHuman) return;
            if (by != null && by.isHuman) PocketCandy(p);
            if (round < 1 || round > 3 || run == null || run.Now != GateRun.Step.Round) return;
            Pocketed++;
            run.Progress(Pocketed);
            if (Pocketed >= ballsPerRound) run.RoundWon();
        }

        // The player's own pocket: a ka-ching and candies popping out of the hole onto the lawn.
        private void PocketCandy(BallPlayer p)
        {
            var valley = stage.valley;
            var cave = valley != null ? valley.NearestCave(p.lastPosition) : null;
            var from = cave != null ? cave.mouth + cave.openingOffset : p.lastPosition;
            from.y = 0.6f;
            Sfx.Play(Sfx.Sound.PlayerPocket, from);
            var inward = cave != null ? -BallValleyStage.Flat(cave.outward).normalized : Vector3.zero;
            for (int i = 0; i < pocketCandies; i++)
            {
                float a = (i - (pocketCandies - 1) * 0.5f) * 25f;
                var dir = Quaternion.Euler(0f, a, 0f) * inward;
                var fly = dir * Random.Range(3.5f, 5f) + Vector3.up * Random.Range(5f, 6.5f);
                stage.SpawnPellet(from, fly, stage.RandomBerryColor());
            }
        }

        // The finale: the balls still in the valley go (a puff where each was).
        private void ClearBalls()
        {
            foreach (var p in stage.players.ToArray())
            {
                if (p == null || p.isHuman) continue;
                if (p.alive) Gfx.Puff(p.Position, new Color(0.88f, 0.88f, 0.88f, 0.8f), 14, p.radius * 2f, 4f);
                stage.RemovePlayer(p);
            }
        }

        // ---------------------------------------------------------------- the boss

        // In the middle of the smallest field's far half from the player, so it doesn't land on them.
        private Vector3 BossSpot()
        {
            var playerAt = PlayerFeet();
            float side = playerAt.x > 0f ? -1f : 1f;
            return new Vector3(side * stage.tune.halfX * smallest * 0.35f, 0f, 0f);
        }

        private void SetUpBoss(PumpkinBallBoss boss)
        {
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
            boss.touched = (c, velocity) => BossTouched(boss, c, velocity);
            boss.landed = BossLanded;
        }

        // The boss ran into one of the valley's balls (or slimes): it flies off, knocked.
        private void BossTouched(PumpkinBallBoss boss, Collider c, Vector3 bossVelocity)
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

        // Its landing throws what's round it outwards.
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

        // The seed: on the lawn, clear of the holes.
        private Vector3 SeedSpot(Vector3 at)
        {
            var valley = stage.valley;
            if (valley)
            {
                float lx = Mathf.Max(0.5f, valley.HalfX - 2f), lz = Mathf.Max(0.5f, valley.HalfZ - 2f);
                at.x = Mathf.Clamp(at.x, -lx, lx);
                at.z = Mathf.Clamp(at.z, -lz, lz);
                for (int i = 0; i < 20 && valley.DistanceToNearestPit(at) < 2.5f; i++) at *= 0.85f;
            }
            at.y = 0f;
            return at;
        }

        // ---------------------------------------------------------------- the chests and the way out

        // A chest drops on the lawn in front of the player, clear of the holes and the border (inside the size it's
        // shrinking to).
        private Vector3 ChestSpot()
        {
            var cam = Camera.main;
            var at = Vector3.zero;
            if (cam != null)
            {
                var fwd = BallValleyStage.Flat(cam.transform.forward);
                if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
                at = BallValleyStage.Flat(cam.transform.position) + fwd.normalized * 1.6f;
            }
            var valley = stage.valley;
            if (valley)
            {
                float lx = Mathf.Max(0.5f, valley.SafeHalfX - 1.5f), lz = Mathf.Max(0.5f, valley.SafeHalfZ - 1.5f);
                at.x = Mathf.Clamp(at.x, -lx, lx);
                at.z = Mathf.Clamp(at.z, -lz, lz);
                for (int i = 0; i < 20 && valley.DistanceToNearestPit(at) < 2f; i++) at *= 0.85f;
            }
            at.y = 0.05f;
            return at;
        }

        // Gate Exit has the player: the valley goes as soon as they're on the rooftop (its fade-in has just begun, so the
        // house's fog and far plane come back unseen), or when a Game Over took them.
        private IEnumerator GoWhenOnTheRoof()
        {
            while (GateExit.Running && Game.GameStages.ActiveState() != "Game Over") yield return null;
            Destroy(gameObject);
        }

        private static Vector3 PlayerFeet()
        {
            var cam = Camera.main;
            return cam != null ? new Vector3(cam.transform.position.x, 0f, cam.transform.position.z) : Vector3.zero;
        }
    }
}

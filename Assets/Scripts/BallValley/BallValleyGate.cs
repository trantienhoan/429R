using System.Collections;
using System.Collections.Generic;
using Game.Gates;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace Balls
{
    /// <summary>
    /// 429R gate 3, Ball Valley (user, 2026-11-23): "We walk into gate 3 ... screen black out, we open our eyes in middle
    /// of a field, crazy balls moving and hitting each other, even the Player ... The field gets smaller as time go ... a 5
    /// minutes song that slow at first ... then keep getting faster and faster till the end where the field gets really
    /// small, to a point where 6 holes meet each others and kill the Player. Player have to push all the balls on the field
    /// into holes before that, the last boss Black Pumpkin Ball only comes out when all other 15 balls are pocketed ...
    /// when that black pumpkin ball Dies make it rain candies and create chest that contains The Seed inside ... No need
    /// for other seeds in this gate game."
    /// - The card (a title and one short line), after any other notice; it pauses the game until it's closed.
    /// - 15 balls drop in at once; bots hunt each other and the player (more of them, and angrier, as the song speeds up).
    /// - A ball counts only if the player pocketed it (they touched it, or sent a ball into it, within Credit Seconds):
    ///   gone for good, 3 candies pop out of the hole. Knocked in by the others, it drops back in from the sky.
    /// - The song: one long shrink of the field over Song Seconds (slow at first, faster and faster, Shrink Ease), from full
    ///   size to End Scale, where the six holes meet; the music plays faster with it. At the end the player dies (no
    ///   revive: the field is gone), as the Daruma stage's loss does.
    /// - All 15 pocketed: the Black Pumpkin Ball drops in (the kit's jack-o'-lantern, blackened; the same size as a ball,
    ///   Black Weight times as heavy, Black Health). Broken by the player, or pocketed: the field stops shrinking, the music
    ///   fades, it rains candies (Rain Candies) and the main game's chest with The Seed inside drops in front of the player
    ///   (Seed Chest: Items/Chest/Treasure_Chest_and_Seed, as Whack 7's barrel pumpkin drops it). Eating The Seed sets
    ///   GAMESTAGES' The_Seed: Gate Exit (shared) sets the gear bools (no other seeds here) and takes the player to the
    ///   rooftop (defend the tree, then the boss). Never stuck: The_Seed is set for them after Seed Give Up Seconds.
    /// - Trees (candy trees: a hit knocks a candy out), boulders (break into candy) and jello volcanoes (spit candy) stand
    ///   anywhere on the lawn; slimes run about (a punch pops one into candy).
    /// - Seed balls (user, 2026-11-23 evening: "make some balls bigger a little and have red aura, push that ball into hole
    ///   and the hole will spit out a seed"): one ball per Gear Seed is Seed Ball Size bigger and glows red; the player's
    ///   pocket of one spits its gear seed (Bracer, Chest, Head, Shield: the heal, health bonus and gear bool a gear chest
    ///   gives) out of the hole onto the lawn. Gate Exit eats any still waiting for the player.
    /// No Gate Run: its start chest, rounds and gear chests aren't part of this gate (the user's call); this does the card,
    /// the HUD line and what Gate Run does while a gate runs (the comfort vignette on launches, seeds kept from the purple
    /// hole). Set up by Tools > 429 Game > Ball Valley > Set Up Ball Valley (Prefabs/BallValley/Ball_Valley).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BallValleyStage))]
    public class BallValleyGate : MonoBehaviour
    {
        public enum Step { Arriving, Balls, BlackPumpkin, Ending, Collapsed }

        [Header("The card (a title and one short line; no title: none)")]
        public string cardTitle = "RUN AND SNOOKER";
        public string cardLine = "Push all 15 balls in before the holes meet.";
        public string cardCloseLine = "Pull a trigger to play";
        [Tooltip("Seconds after arriving before the card shows (it also waits while any other notice is up).")]
        [Min(0f)] public float cardDelay = 2f;

        [Header("The balls")]
        [Tooltip("Balls that drop in at the start; pocketing them all brings the Black Pumpkin Ball.")]
        [Min(1)] public int balls = 15;
        [Tooltip("Seconds over which they drop in.")]
        [Min(0f)] public float dropInSeconds = 4f;
        [Tooltip("The balls' tempers at the song's start, x = least to y = most (0 = grazes, 1 = hunts nonstop) ...")]
        public Vector2 temperStart = new Vector2(0.45f, 0.65f);
        [Tooltip("... and at its end (in between: as far along as the song is).")]
        public Vector2 temperEnd = new Vector2(0.8f, 1f);
        [Tooltip("Most balls hunting the player at once at the song's start, and at its end.")]
        public Vector2Int huntersStartEnd = new Vector2Int(1, 3);
        [Tooltip("A ball the player touched (a punch, the dash, their body, or a ball they sent into it) this many seconds " +
                 "before it went down is their pocket.")]
        [Min(0f)] public float creditSeconds = 6f;
        [Tooltip("A ball the others knocked in drops back in from the sky after this many seconds (least, most).")]
        public Vector2 backInSeconds = new Vector2(3f, 6f);
        [Tooltip("A ball the player pocketed pops this many candies out of the hole onto the lawn (0 = none).")]
        [Min(0)] public int pocketCandies = 3;
        [Tooltip("A punched (knocked) ball's braking while it rolls out (m/s^2; BALLS: 5): lower = it rolls further, like a " +
                 "snooker ball, before its bot steers it again.")]
        [Min(0f)] public float punchedBallBraking = 1.5f;
        [Tooltip("...and the speed under which its bot has it back (m/s; BALLS: 5).")]
        [Min(0f)] public float punchedBallControlBelow = 2.5f;

        [Header("The field and the song")]
        [Tooltip("The song's length: the field shrinks over it from full size (50 x 25 m) to End Scale.")]
        [Min(10f)] public float songSeconds = 300f;
        [Tooltip("The field's size at the song's end (share of the full size): at 0.1 (5 x 2.5 m) the six holes have met and " +
                 "no lawn is left.")]
        [Range(0.05f, 0.5f)] public float endScale = 0.1f;
        [Tooltip("How the shrink speeds up: 1 = steady, 2.2 = slow at first then faster and faster (half way through the song " +
                 "the field is still 80% of its size).")]
        [Range(1f, 4f)] public float shrinkEase = 2.2f;
        [Tooltip("The song, played in order (the last one loops): chill at first, faster later. Swap in a 5-minute song when " +
                 "there is one.")]
        public AudioClip[] song = new AudioClip[0];
        [Range(0f, 1f)] public float songVolume = 0.5f;
        [Tooltip("The music's speed at the song's start and at its end (it rises with the shrink).")]
        public Vector2 songPitch = new Vector2(0.92f, 1.3f);
        [Tooltip("A new candy on the lawn at most this often (seconds) while there are fewer than its cap (each is a whole " +
                 "candy prefab, three FSMs).")]
        [Min(0.05f)] public float candyEvery = 0.3f;

        [Header("The field's things")]
        [Min(0)] public int trees = 5;
        [Min(0)] public int boulders = 6;
        [Min(0)] public int volcanoes = 3;
        [Tooltip("Tree health (times its size): a punch does the Ball Valley Player's Prop Punch Damage, a ball's crash about 5-15.")]
        [Min(1f)] public float treeHealth = 90f;
        [Tooltip("Boulder health (times its size).")]
        [Min(1f)] public float boulderHealth = 160f;
        [Tooltip("Candies a tree's hits knock out of it (then it only shakes; broken, it still bursts into candy).")]
        [Min(0)] public int treeHitCandies = 10;
        [Tooltip("Slimes on the lawn at full size, and at the smallest.")]
        public Vector2Int slimes = new Vector2Int(5, 8);

        [Header("The Black Pumpkin Ball (after the last ball)")]
        [Tooltip("Its look: the kit's jack-o'-lantern (Build Ball Valley Kit sets it), blackened by Black Tint.")]
        public GameObject blackPumpkinLook;
        public Color blackTint = new Color(0.13f, 0.1f, 0.15f);
        [Tooltip("Its radius (m): a ball's size (no bigger).")]
        [Min(0.3f)] public float blackRadius = 1.2f;
        [Tooltip("Times as heavy as a ball its size (crashes, and a punch moves it only this much as far).")]
        [Min(1f)] public float blackWeight = 3f;
        [Min(1f)] public float blackHealth = 150f;
        [Tooltip("Its laugh, break sound and break effects come from this giant pumpkin (gate 3's own).")]
        public PumpkinBallBoss voiceFrom;

        [Header("The end")]
        [Tooltip("Candies that rain on the field when it dies (42: every gate's giant pumpkin).")]
        [Min(0)] public int rainCandies = 42;
        [Min(0.1f)] public float rainSeconds = 3f;
        [Min(0)] public int exp = 30;
        [Tooltip("The chest with The Seed inside (Items/Chest/Treasure_Chest_and_Seed, the main game's).")]
        public GameObject seedChest;
        [Tooltip("The Seed not eaten this many seconds after the chest drops: The_Seed is set for them (never stuck).")]
        [Min(5f)] public float seedGiveUpSeconds = 90f;

        [Header("Seed balls (bigger, a red aura: the player's pocket spits out a gear seed)")]
        [Tooltip("The gear seeds, one seed ball each, in this order (Items/Chest/Seed_Bracer, Seed_Chest, Seed_Head, " +
                 "Seed_Shield). Empty: no seed balls.")]
        public GameObject[] gearSeeds = new GameObject[0];
        [Tooltip("Times a seed ball's size (a little bigger than the others).")]
        [Range(1f, 1.6f)] public float seedBallSize = 1.25f;
        [Tooltip("A seed ball's glow and sparks, and the streak of the seed the hole spits out.")]
        public Color seedBallAura = new Color(1f, 0.12f, 0.08f);
        [Tooltip("The aura round a seed ball (Effects/Chaos_Aura: the game's red aura, as the pumpkin boss wears it).")]
        public GameObject seedBallAuraLook;

        [Header("The HUD line")]
        public string ballsLine = "BALLS {0}/{1}";
        public string blackLine = "BLACK PUMPKIN {0}";
        [Tooltip("The seconds left show on the line under this many.")]
        [Min(0f)] public float showTimeUnder = 60f;
        [Tooltip("Metres ahead of the head, metres under the eyes.")]
        public Vector2 hudPlace = new Vector2(1.8f, 0.55f);
        [Min(0.1f)] public float hudSize = 2.2f;
        public Color hudColour = new Color(1f, 0.85f, 0.35f);

        /// <summary>No giant pumpkin in this gate any more (Ball Valley Player still asks).</summary>
        public PumpkinBallBoss Boss => null;
        /// <summary>Where the gate is.</summary>
        public Step Now { get; private set; } = Step.Arriving;
        /// <summary>Balls the player has pocketed.</summary>
        public int Pocketed { get; private set; }
        /// <summary>Seconds into the song (the field's clock; stops while the game is paused).</summary>
        public float Clock => clockOn ? Time.time - clockAt : clockStopped;
        /// <summary>The Black Pumpkin Ball, once it's in (null after it died).</summary>
        public BallPlayer Black { get; private set; }
        /// <summary>Its health (0 when there's none).</summary>
        public float BlackHealth => blackHp != null ? blackHp.Health : 0f;

        private BallValleyStage stage;
        private bool leaving, clockOn, blackDead;
        private float clockAt, clockStopped;
        private BlackPumpkinBall blackHp;
        private Material blackMaterial;
        private Transform hudRoot, headCam;
        private TextMeshPro hud;
        private float hudPulse;
        private AudioSource music;
        private int songIndex;
        private bool songOn, songPaused;
        private Vector3 chestAt;
        private readonly Dictionary<BallPlayer, int> seedBalls = new Dictionary<BallPlayer, int>();
        private readonly List<GameObject> spatSeeds = new List<GameObject>();
        private float nextSeedKeep;

        // ---------------------------------------------------------------- set up

        private void Awake()
        {
            stage = GetComponent<BallValleyStage>();
            // The gate drives the valley's size itself (no phases); no BALLS gifts, crumbling or Moment of Truth; its
            // candy speed; punched balls roll on like snooker balls; its own trees, boulders, volcanoes and slimes.
            var t = stage.tune;
            t.shrinkDelay = 1e6f;
            t.selfPocketGift = 0;
            t.momentOfTruth = false;
            t.momentOfTruthCrumble = false;
            t.pelletSpawnInterval = candyEvery * Mathf.Max(0.1f, t.foodBoost);   // (the stage divides it by Food Boost)
            t.knockFriction = punchedBallBraking;
            t.knockRecoverSpeed = punchedBallControlBelow;
            t.treeCount = trees;
            t.rockCount = boulders;
            t.treeHealth = treeHealth;
            t.rockHealth = boulderHealth;
            t.slimeCount = slimes.x;
            t.slimeCountSmallest = slimes.y;
            stage.botsHuntBots = true;          // crazy balls hitting each other (only the player's pockets count)
            stage.startBalls = 0;               // they drop in after the card
            stage.treesAnywhere = true;
            stage.volcanoCount = volcanoes;
            stage.treeHitCandies = treeHitCandies;
            stage.driveScale = 1f;
            stage.drivePending = -1f;
            SetTemper(0f);
        }

        private void OnEnable()
        {
            stage = GetComponent<BallValleyStage>();
            stage.BallPocketed += OnPocketed;
            // What Gate Run does while a gate runs: launches (a ball's shove) close the comfort vignette in.
            Game.Locomotion.ScriptedMoveVignette.Launches = true;
        }

        private void OnDisable()
        {
            if (stage != null) stage.BallPocketed -= OnPocketed;
        }

        private void OnDestroy()
        {
            Game.Locomotion.ScriptedMoveVignette.Launches = false;
            JelloVolcano.Hurry = 1f;
            if (hudRoot != null) Destroy(hudRoot.gameObject);
            if (blackMaterial != null) Destroy(blackMaterial);
        }

        private void Start()
        {
            MakeHud();
            StartCoroutine(Run());
        }

        // ---------------------------------------------------------------- the flow

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

            Now = Step.Balls;
            MarkSeedBalls(stage.DropBalls(balls, dropInSeconds));
            clockAt = Time.time;
            clockOn = true;
            StartSong();
            Debug.Log($"[BallValley] {balls} balls drop in; the field closes over {songSeconds:0} s.", this);

            while (!blackDead)
            {
                if (Clock >= songSeconds)
                {
                    yield return Collapse();
                    yield break;
                }
                if (Pocketed >= balls && Black == null) DropBlack();
                yield return null;
            }
            yield return Ending();
        }

        private void Update()
        {
            // The exit has begun: the valley stands alone until the player is on the rooftop.
            if (!leaving && GateExit.Running)
            {
                leaving = true;
                transform.SetParent(null, true);
                StartCoroutine(GoWhenOnTheRoof());
            }
            if (clockOn) Drive(Mathf.Clamp01(Clock / songSeconds));
            UpdateSong();
            KeepGearSeeds();
        }

        // The song's share k (0..1): the field's size, where it's heading, the balls' tempers, the volcanoes, the music.
        private void Drive(float k)
        {
            stage.driveScale = SizeAt(k);
            stage.drivePending = SizeAt(Mathf.Clamp01(k + 4f / songSeconds));
            if (Now == Step.Balls) SetTemper(k);
            JelloVolcano.Hurry = Mathf.Lerp(1f, 2.2f, k);
            if (music != null) music.pitch = Mathf.Lerp(songPitch.x, songPitch.y, Mathf.Pow(k, 1.4f));
        }

        private float SizeAt(float k) => Mathf.Lerp(1f, endScale, Mathf.Pow(Mathf.Clamp01(k), shrinkEase));

        private void SetTemper(float k)
        {
            var a = Vector2.Lerp(temperStart, temperEnd, k);
            stage.tune.botAggressionMin = Mathf.Clamp01(Mathf.Min(a.x, a.y));
            stage.tune.botAggressionMax = Mathf.Clamp01(Mathf.Max(a.x, a.y));
            stage.maxHunters = Mathf.Max(0, Mathf.RoundToInt(Mathf.Lerp(huntersStartEnd.x, huntersStartEnd.y, k)));
        }

        private void StopClock()
        {
            if (!clockOn) return;
            clockStopped = Clock;
            clockOn = false;
        }

        // ---------------------------------------------------------------- the balls

        private void OnPocketed(BallPlayer p, BallPlayer by)
        {
            if (p == null || p.isHuman) return;
            if (p == Black)
            {
                Debug.Log($"[BallValley] The Black Pumpkin Ball went down a hole ({Clock:0.0} s).", this);
                BlackDied(p.lastPosition, false);
                return;
            }
            bool mine = (by != null && by.isHuman) || Time.time - p.lastHumanTouch < creditSeconds;
            if (!mine || Now != Step.Balls)
            {
                // The others knocked it in: it drops back in (only the player's pockets count).
                stage.DropBackIn(p, Random.Range(backInSeconds.x, backInSeconds.y));
                return;
            }
            Pocketed++;
            hudPulse = 1f;
            PocketCandy(p);
            if (seedBalls.TryGetValue(p, out int seed))
            {
                seedBalls.Remove(p);
                StartCoroutine(SpitSeed(p.lastPosition, seed));
            }
            stage.RemovePlayer(p);   // gone for good
            Debug.Log($"[BallValley] The player pocketed {p.displayName}: {Pocketed}/{balls} ({Clock:0.0} s).", this);
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

        // ---------------------------------------------------------------- the seed balls

        // One ball per gear seed (picked at random) is a little bigger and glows red. It keeps that through a drop back in.
        private void MarkSeedBalls(List<BallPlayer> dropped)
        {
            seedBalls.Clear();
            if (dropped == null || gearSeeds == null) return;
            var pick = new List<BallPlayer>(dropped);
            for (int i = 0; i < gearSeeds.Length && pick.Count > 0; i++)
            {
                if (gearSeeds[i] == null) continue;
                var b = pick[Random.Range(0, pick.Count)];
                pick.Remove(b);
                b.sizeTimes = seedBallSize;
                b.aura = seedBallAura;
                b.auraLook = seedBallAuraLook;
                seedBalls[b] = i;
            }
            if (seedBalls.Count > 0)
                Debug.Log($"[BallValley] {seedBalls.Count} seed ball(s): bigger, a red aura; the player's pocket spits out a gear seed.", this);
        }

        // A seed ball the player pocketed: its hole spits the gear seed out (a red streak arcing onto the lawn), where it
        // pops up and waits to be eaten.
        private IEnumerator SpitSeed(Vector3 pocketedAt, int index)
        {
            var prefab = gearSeeds != null && index >= 0 && index < gearSeeds.Length ? gearSeeds[index] : null;
            if (prefab == null) yield break;
            var valley = stage.valley;
            var cave = valley != null ? valley.NearestCave(pocketedAt) : null;
            var from = cave != null ? cave.mouth + cave.openingOffset : pocketedAt;
            from.y = 0.6f;
            var to = SeedSpot(from);
            const float fly = 0.6f;
            for (float t = 0f; t < fly; t += Time.deltaTime)
            {
                float k = t / fly;
                Gfx.Sparks(Vector3.Lerp(from, to, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 2.5f), seedBallAura, 2, 1.2f, false);
                yield return null;
            }
            if (GateExit.Running || Game.GameStages.ActiveState() == "Game Over") yield break;
            var seed = Instantiate(prefab, to, Quaternion.identity, Holder());
            seed.AddComponent<Game.Rooms.KeepFromPurpleHole>();   // a death meanwhile mustn't let the purple hole eat it
            spatSeeds.Add(seed);
            Gfx.Sparks(to, seedBallAura, 12, 3f, false);
            Debug.Log($"[BallValley] The hole spat out {prefab.name} at {to}.", this);
        }

        // Where a spat seed waits: half way from its hole to the middle, on the lawn, clear of the holes, about head-high.
        private Vector3 SeedSpot(Vector3 from)
        {
            var at = BallValleyStage.Flat(from) * 0.5f;
            var valley = stage.valley;
            if (valley)
            {
                float lx = Mathf.Max(0.5f, valley.SafeHalfX - 1.5f), lz = Mathf.Max(0.5f, valley.SafeHalfZ - 1.5f);
                at.x = Mathf.Clamp(at.x, -lx, lx);
                at.z = Mathf.Clamp(at.z, -lz, lz);
                for (int i = 0; i < 20 && valley.DistanceToNearestPit(at) < 2.5f; i++) at *= 0.85f;
            }
            at.y = 1f;
            return at;
        }

        // The field keeps shrinking: a spat seed the holes come near (or the border passes) moves in toward the middle.
        private void KeepGearSeeds()
        {
            if (spatSeeds.Count == 0 || Time.time < nextSeedKeep) return;
            nextSeedKeep = Time.time + 0.5f;
            spatSeeds.RemoveAll(s => s == null);
            var valley = stage.valley;
            if (!valley || leaving || GateExit.Running) return;
            foreach (var s in spatSeeds)
            {
                var at = s.transform.position;
                var flat = BallValleyStage.Flat(at);
                bool outside = Mathf.Abs(flat.x) > valley.SafeHalfX - 1f || Mathf.Abs(flat.z) > valley.SafeHalfZ - 1f;
                if (!outside && valley.DistanceToNearestPit(flat) >= 1.5f) continue;
                var to = SeedSpot(flat);
                to.y = at.y;
                s.transform.position = to;
                Gfx.Sparks(to, seedBallAura, 8, 2f, false);
            }
        }

        // ---------------------------------------------------------------- the Black Pumpkin Ball

        private void DropBlack()
        {
            Now = Step.BlackPumpkin;
            // The pocketed balls' tails (BALLS turns them into loose food balls) go with a puff: a clean field for it.
            foreach (var l in stage.looseBalls.ToArray())
            {
                if (l == null) continue;
                Gfx.Puff(l.transform.position, new Color(0.88f, 0.88f, 0.88f, 0.8f), 8, l.transform.localScale.x, 3f);
                l.Eat();
            }
            var b = stage.CreateBot();
            b.displayName = "Black Pumpkin";
            b.fixedRadius = blackRadius;
            b.neverGrows = true;
            b.massTimes = blackWeight;
            b.dressHead = DressBlack;
            b.SetColor(blackTint);
            Black = b;
            // The only ball left: it goes for the player, all out.
            stage.tune.botAggressionMin = 0.9f;
            stage.tune.botAggressionMax = 1f;
            stage.maxHunters = Mathf.Max(1, stage.maxHunters);
            b.freeDrop = true;
            stage.TryRevive(b);   // dropped in from the sky, clear of the holes
            if (b.head != null) Gfx.Puff(b.Position, new Color(0.12f, 0.08f, 0.16f, 0.9f), 24, blackRadius * 3f, 5f);
            if (voiceFrom != null && voiceFrom.laughSounds != null && voiceFrom.laughSounds.Length > 0)
                PlayWide(voiceFrom.laughSounds[Random.Range(0, voiceFrom.laughSounds.Length)], b.Position, 1f);
            hudPulse = 1f;
            Debug.Log($"[BallValley] All {balls} balls in: the Black Pumpkin Ball drops in ({Clock:0.0} s; mass {(b.head != null ? b.head.rb.mass : 0f):0.0}).", this);
        }

        // Its main ball wears the blackened jack-o'-lantern (the same size), and takes the player's hits.
        private void DressBlack(BallPart head)
        {
            if (head == null) return;
            if (head.mr != null) head.mr.enabled = false;
            foreach (Transform child in head.transform)
                if (child.name == "Spot") Destroy(child.gameObject);
            if (blackPumpkinLook != null)
            {
                var look = Instantiate(blackPumpkinLook, head.transform, false);
                look.name = "Black Pumpkin Look";
                float modelRadius = 1f;
                var mf = look.GetComponentInChildren<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    var b = mf.sharedMesh.bounds;
                    modelRadius = Mathf.Max(0.01f, Mathf.Max(Mathf.Max(-b.min.x, b.max.x), Mathf.Max(-b.min.z, b.max.z)));
                }
                // The main ball's object is scaled to its diameter: the look's radius comes out at the ball's.
                look.transform.localScale = Vector3.one * (0.5f / modelRadius);
                foreach (var r in look.GetComponentsInChildren<Renderer>())
                {
                    if (r.sharedMaterial == null) continue;
                    if (blackMaterial == null)
                    {
                        blackMaterial = new Material(r.sharedMaterial) { name = "Black Pumpkin (gate 3)" };
                        blackMaterial.SetColor("_BaseColor", blackTint);
                    }
                    r.sharedMaterial = blackMaterial;
                }
            }
            else if (head.mr != null)
            {
                head.mr.enabled = true;   // no look set: a black ball
                head.mr.material.SetColor("_BaseColor", blackTint);
            }
            blackHp = head.gameObject.AddComponent<BlackPumpkinBall>();
            blackHp.Init(Black, blackHealth);
            blackHp.Broken += _ =>
            {
                Debug.Log($"[BallValley] The player broke the Black Pumpkin Ball ({Clock:0.0} s).", this);
                BlackDied(head.transform.position, true);
            };
        }

        private void BlackDied(Vector3 at, bool broken)
        {
            if (blackDead) return;
            blackDead = true;
            Now = Step.Ending;
            StopClock();
            if (broken && voiceFrom != null)
            {
                if (voiceFrom.breakSound != null) PlayWide(voiceFrom.breakSound, at, 1f);
                foreach (var fx in voiceFrom.breakEffects)
                {
                    if (fx == null) continue;
                    var made = Instantiate(fx, at, Quaternion.identity);
                    made.transform.localScale *= blackRadius * 2f;
                    Destroy(made, 8f);
                }
            }
            Gfx.Puff(at, new Color(0.12f, 0.08f, 0.16f, 0.9f), 24, blackRadius * 3f, 6f);
            var b = Black;
            Black = null;
            blackHp = null;
            if (b != null) stage.RemovePlayer(b);   // (pocketed or broken: no drop-in)
        }

        // ---------------------------------------------------------------- the end

        private IEnumerator Ending()
        {
            FadeSong(2.5f);
            if (exp > 0) Game.Leveling.PlayerLevel.AddExp(exp);
            StartCoroutine(RainCandy());
            yield return new WaitForSeconds(1.5f);
            chestAt = ChestSpot();
            if (seedChest != null)
            {
                var face = HeadCam();
                var toPlayer = face != null ? BallValleyStage.Flat(face.position - chestAt) : Vector3.back;
                var chest = Instantiate(seedChest, chestAt,
                    Quaternion.LookRotation(toPlayer.sqrMagnitude > 0.01f ? toPlayer.normalized : Vector3.back, Vector3.up), Holder());
                chest.AddComponent<Game.Rooms.KeepFromPurpleHole>();
                Gfx.Puff(chestAt + Vector3.up * 0.5f, new Color(1f, 0.9f, 0.6f, 0.8f), 16, 1.5f, 4f);
                Debug.Log($"[BallValley] The chest with The Seed dropped at {chestAt}.", this);
            }
            else Debug.LogWarning("[BallValley] No Seed Chest set: The_Seed is set for the player.", this);

            float giveUpAt = Time.time + seedGiveUpSeconds;
            float nextKeep = 0f;
            while (Game.GameStages.Fsm() != null && !Game.GameStages.GetBool("The_Seed"))
            {
                if (Game.GameStages.ActiveState() == "Game Over") yield break;
                if (seedChest == null || Time.time >= giveUpAt)
                {
                    if (seedChest != null) Debug.Log($"[BallValley] The Seed not eaten in {seedGiveUpSeconds:0} s: set for the player.", this);
                    Game.GameStages.SetBool("The_Seed", true, this);
                    break;
                }
                if (Time.time >= nextKeep)
                {
                    nextKeep = Time.time + 0.5f;
                    KeepSeeds();
                }
                yield return null;
            }
            Debug.Log($"[BallValley] The Seed eaten: off to the rooftop.", this);
            GateExit.Begin(gameObject, null, chestAt, "[BallValley]");
        }

        // It rains candies on the lawn (clear of the holes).
        private IEnumerator RainCandy()
        {
            var valley = stage.valley;
            float each = rainSeconds / Mathf.Max(1, rainCandies);
            for (int i = 0; i < rainCandies; i++)
            {
                var at = Vector3.zero;
                for (int tries = 0; tries < 12; tries++)
                {
                    at = valley != null
                        ? new Vector3(Random.Range(-valley.HalfX + 1f, valley.HalfX - 1f), 0f, Random.Range(-valley.HalfZ + 1f, valley.HalfZ - 1f))
                        : Random.insideUnitSphere * 5f;
                    at.y = 0f;
                    if (valley == null || valley.DistanceToNearestPit(at) > 1.2f) break;
                }
                stage.SpawnPellet(at + Vector3.up * Random.Range(10f, 15f), Vector3.down * Random.Range(1f, 3f), stage.RandomBerryColor());
                yield return new WaitForSeconds(each);
            }
        }

        // The seed the chest let out: a death meanwhile mustn't let the purple hole eat it (Gate Run does this for its gates).
        private void KeepSeeds()
        {
            var holder = Holder();
            if (holder == null) return;
            foreach (Transform t in holder)
                if (t.name.StartsWith("The_Seed") && t.GetComponent<Game.Rooms.KeepFromPurpleHole>() == null)
                    t.gameObject.AddComponent<Game.Rooms.KeepFromPurpleHole>();
        }

        // The song ran out: the six holes have met and the lawn is gone. The player dies (no revive: there's no field to
        // stand on), the way the Daruma stage's loss does; GAMESTAGES' Game Over clears the valley.
        private IEnumerator Collapse()
        {
            Now = Step.Collapsed;
            StopClock();
            stage.driveScale = endScale;
            Debug.Log($"[BallValley] The song is over: the holes meet ({Pocketed}/{balls} pocketed, the Black Pumpkin Ball " +
                      (Black != null ? "still in" : "not out") + ").", this);
            FadeSong(1.5f);
            yield return new WaitForSeconds(1f);
            string here = Game.GameStages.ActiveState();
            // Play test 2026-11-23: a revive coin was still getting the player up when the field closed; the kill landed
            // in the middle of it and the death never went on (health 0, no Game Over). So, as the Daruma stage does: not
            // while dying, reviving, paused, flying from a push or fading; and again if it didn't take.
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                for (float until = Time.unscaledTime + 12f; Time.unscaledTime < until && PlayerBusy();) yield return null;
                if (!KillPlayer())
                {
                    Debug.LogWarning("[BallValley] No PlayerHealth FSM on the XR Origin: the player isn't killed (a test scene?).", this);
                    yield break;
                }
                for (float until = Time.unscaledTime + 6f; Time.unscaledTime < until; )
                {
                    if (Game.GameStages.ActiveState() != here || Game.Saving.Checkpoints.IsDying) yield break;   // the death goes on
                    yield return null;
                }
                Debug.LogWarning($"[BallValley] The holes met but the death didn't go on (kill {attempt}): again.", this);
            }
        }

        // Dying (the coins), getting up from a revive, paused, flying from a push, or the view fading.
        private static bool PlayerBusy()
        {
            var revive = Game.Combat.PlayerRevive.Instance;
            var origin = FindAnyObjectByType<XROrigin>();
            var launch = origin != null ? origin.GetComponent<Game.Combat.PlayerLaunch>() : null;
            var fade = Game.Locomotion.HeadsetFade.Find();
            return Paused() || Game.Saving.Checkpoints.IsDying || (revive != null && revive.IsReviving) ||
                   (launch != null && launch.Flying) || (fade != null && fade.IsFading);
        }

        private static bool KillPlayer()
        {
            var origin = FindAnyObjectByType<XROrigin>();
            if (origin == null) return false;
            PlayMakerFSM health = null;
            foreach (var fsm in origin.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName != "PlayerHealth") continue;
                health = fsm;
                break;
            }
            if (health == null) return false;
            var launch = origin.GetComponent<Game.Combat.PlayerLaunch>();
            if (launch != null) launch.Stop();
            Game.Saving.Checkpoints.UseAllRevives();
            var hp = health.FsmVariables.FindFsmFloat("PlayerHealth");
            if (hp != null) hp.Value = 0f;
            health.SendEvent("DamageToPlayer");
            return true;
        }

        // A chest drops on the lawn in front of the player, clear of the holes and the border.
        private Vector3 ChestSpot()
        {
            var cam = HeadCam();
            var at = Vector3.zero;
            if (cam != null)
            {
                var fwd = BallValleyStage.Flat(cam.forward);
                if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
                at = BallValleyStage.Flat(cam.position) + fwd.normalized * 1.6f;
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

        private Transform Holder()
        {
            var spawned = GateExit.Global("CurrentlySpawnedStuffs");
            return spawned != null ? spawned.transform : transform;
        }

        private Transform HeadCam()
        {
            if (headCam == null || !headCam.gameObject.activeInHierarchy)
            {
                var cam = Camera.main;
                headCam = cam != null ? cam.transform : null;
            }
            return headCam;
        }

        private static bool Paused() => Time.timeScale <= 0f;

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

        // ---------------------------------------------------------------- the song

        private void StartSong()
        {
            if (song == null || song.Length == 0 || hudRoot == null) return;
            music = hudRoot.gameObject.AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.spatialBlend = 0f;
            music.volume = songVolume;
            music.pitch = songPitch.x;
            songIndex = -1;
            songOn = true;
            NextSongPart();
        }

        private void NextSongPart()
        {
            if (music == null) return;
            for (int tries = 0; tries < song.Length; tries++)
            {
                songIndex = Mathf.Min(songIndex + 1, song.Length - 1);   // the last part loops
                if (song[songIndex] != null) break;
            }
            if (song[songIndex] == null) { songOn = false; return; }
            music.clip = song[songIndex];
            music.Play();
        }

        private void UpdateSong()
        {
            if (music == null || !songOn) return;
            // The game paused (a notice): the music waits too, as the field's clock does.
            if (Paused() != songPaused)
            {
                songPaused = Paused();
                if (songPaused) music.Pause();
                else music.UnPause();
            }
            if (!songPaused && !music.isPlaying) NextSongPart();
        }

        private void FadeSong(float seconds)
        {
            songOn = false;
            if (music != null) StartCoroutine(Fade(music, seconds));
        }

        private static IEnumerator Fade(AudioSource source, float seconds)
        {
            float from = source.volume;
            for (float t = 0f; t < seconds && source != null; t += Time.unscaledDeltaTime)
            {
                source.volume = Mathf.Lerp(from, 0f, t / seconds);
                yield return null;
            }
            if (source != null) source.Stop();
        }

        // ---------------------------------------------------------------- the HUD line

        private void MakeHud()
        {
            hudRoot = new GameObject(name + " Line").transform;   // not under the stage: it follows the head, unscaled
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
            string line;
            switch (Now)
            {
                case Step.Balls: line = string.Format(ballsLine, Pocketed, balls); break;
                case Step.BlackPumpkin: line = string.Format(blackLine, Mathf.CeilToInt(BlackHealth)); break;
                default: return "";
            }
            float left = songSeconds - Clock;
            if (showTimeUnder > 0f && left < showTimeUnder) line += $"   {Mathf.Max(0, Mathf.CeilToInt(left))}";
            return line;
        }

        private void LateUpdate()
        {
            if (hud == null) return;
            hud.text = GateExit.Running ? "" : LineNow();
            hudPulse = Mathf.MoveTowards(hudPulse, 0f, Time.unscaledDeltaTime * 2.5f);
            hud.transform.localScale = Vector3.one * (1f + 0.25f * hudPulse);
            var cam = HeadCam();
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

        /// <summary>Test: an alive ball (not the Black Pumpkin Ball) goes down the nearest hole as the player's pocket.</summary>
        [ContextMenu("Test: Pocket A Ball (Mine)")]
        public bool TestPocketOne()
        {
            foreach (var p in stage.players.ToArray())
            {
                if (p == null || p.isHuman || !p.alive || p == Black) continue;
                p.lastHumanTouch = Time.time;
                p.Die(stage.valley != null ? stage.valley.NearestCave(p.Position) : null);
                return true;
            }
            return false;
        }

        /// <summary>Test: a seed ball on the lawn goes down the nearest hole as the player's pocket (its seed is spat out).</summary>
        [ContextMenu("Test: Pocket A Seed Ball (Mine)")]
        public bool TestPocketSeedBall()
        {
            foreach (var p in new List<BallPlayer>(seedBalls.Keys))   // (a copy: the pocket takes it off the list)
            {
                if (p == null || !p.alive) continue;
                p.lastHumanTouch = Time.time;
                p.Die(stage.valley != null ? stage.valley.NearestCave(p.Position) : null);
                return true;
            }
            return false;
        }

        /// <summary>Test: every ball on the lawn goes down as the player's pocket (balls still dropping in aren't).</summary>
        [ContextMenu("Test: Pocket All Balls (Mine)")]
        public int TestPocketAll()
        {
            int n = 0;
            while (TestPocketOne() && n < 100) n++;
            return n;
        }

        /// <summary>Test: the Black Pumpkin Ball takes this much (the dash's way).</summary>
        [ContextMenu("Test: Break The Black Pumpkin")]
        public void TestBreakBlack() => TestHitBlack(99999f);

        public void TestHitBlack(float damage)
        {
            if (blackHp != null) blackHp.SpecialHit(damage);
        }

        /// <summary>Test: the song's clock jumps to <paramref name="seconds"/> (the field's size follows at once).</summary>
        public void TestClockTo(float seconds)
        {
            if (clockOn) clockAt = Time.time - Mathf.Clamp(seconds, 0f, songSeconds);
        }

        [ContextMenu("Test: Clock To The Last 20 s")]
        private void TestLast20() => TestClockTo(songSeconds - 20f);

        // A one-shot heard across the field: Unity's default 3D fade (PlayClipAtPoint) is near silent a few metres away.
        private static void PlayWide(AudioClip clip, Vector3 at, float volume)
        {
            if (clip == null) return;
            var go = new GameObject("Wide Sound: " + clip.name);
            go.transform.position = at;
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.volume = volume;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.minDistance = 8f;
            src.maxDistance = 80f;
            src.dopplerLevel = 0f;
            src.Play();
            Destroy(go, clip.length + 0.1f);
        }
    }
}

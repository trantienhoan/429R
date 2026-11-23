using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Balls
{
    /// <summary>
    /// 429R Gate 3, "Ball Valley" (step 1 of 6, 2026-11-16): BALLS' valley as a gate stage. This is BALLS' ValleyGame
    /// (BALLS: Assets/Balls/Scripts/ValleyGame.cs) trimmed down: it builds the valley at the world origin and runs it -
    /// the shrinking valley, limited revives, the Moment of Truth, the last ball standing - with bots only for now, the
    /// player watching. Not here: the start screen, your own ball and its camera, online play, music, Poki, the HUD.
    /// Everything it makes goes under this object, so GAMESTAGES' Clear Spawned Stuffs (or destroying it) takes it all
    /// away, and the shared statics (Gfx, Sfx, BallPhysics, ModelLib, StunStars) are reset, so another stage can follow in
    /// the same session. While it runs: its own sun and ambient light, no fog, the head's far plane pushed out (429R's
    /// camera sees 40 m) and a blue sky that follows the head (all given back after).
    /// Rounds: Bots Per Round / Aggression Per Round (4). In Watch Mode the next round starts by itself (after the last,
    /// round 1 again); otherwise a later step calls <see cref="NextRound"/>. Seams for the later steps: the events
    /// <see cref="RoundStarted"/>, <see cref="RoundEnded"/> and <see cref="BallPocketed"/>, <see cref="AddExternalBall"/>
    /// (the VR player's ball, steered by its own IBallBrain) and BallPlayer.neverGrows.
    /// Tools > 429 Game > Ball Valley > Set Up Ball Valley builds the prefab (Prefabs/BallValley/Ball_Valley); Watch Ball
    /// Valley (Play mode) shows it. Changes from BALLS are marked "429R:".
    /// 2026-11-18 (user): gate 3 itself. With a Ball Valley Gate on this object (Gate Mode) it plays one round of the gate's
    /// balls, pocketed = gone (no drop-ins, no gifts, no last ball standing: the gate ends it with its giant pumpkin), and
    /// Ball Valley Player puts the VR player in the valley on foot. The berries are random candies (Candy Prefabs).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public class BallValleyStage : MonoBehaviour
    {
        public static BallValleyStage I { get; private set; }

        [Tooltip("BALLS' numbers. 429R gate 3: with a Ball Valley Gate on this object, the gate's own numbers replace some " +
                 "of these when it starts (the shrink clock and sizes, gifts, crumble, candy speed): change those on the Ball " +
                 "Valley Gate, editing them here does nothing.")]
        public Tuning tune = new Tuning();
        [Tooltip("BALLS/VertexColorLit, only for the code-built terrain used if the map model is missing (empty: plain Lit).")]
        public Shader vertexColorShader;

        [Header("429R: rounds")]
        [Tooltip("Bots in round 1, 2, 3, 4 (a round past the list's end uses its last number).")]
        public int[] botsPerRound = { 5, 6, 7, 8 };
        [Tooltip("Each round's bot tempers, x = least to y = most (0 = grazes and rarely fights, 1 = hunts nonstop).")]
        public Vector2[] aggressionPerRound =
        {
            new Vector2(0.45f, 0.6f), new Vector2(0.6f, 0.75f), new Vector2(0.75f, 0.9f), new Vector2(0.9f, 1f),
        };
        [Tooltip("On: rounds follow each other by themselves (after the last, round 1 again) and the player only watches. " +
                 "Off: a round ends and waits for NextRound() (the gate's chests and seeds, a later step). With a Ball Valley " +
                 "Gate on this object (gate 3), neither: one round, the gate's way.")]
        public bool watchMode = true;

        [Header("429R: food")]
        [Tooltip("The candies the field's berries are (user, 2026-11-18: \"change all the berry on the field to random candy\"): " +
                 "each berry is one of these at random, a real candy the player can pick up and eat. Empty: BALLS' berries.")]
        public GameObject[] candyPrefabs = new GameObject[0];

        [Header("429R: look")]
        [Tooltip("The sky sphere's material (Assets/BallValley/Sky/BallValley_Sky_Mat: unlit, the sky picture).")]
        public Material skyMaterial;
        [Tooltip("The head's far plane while the stage runs (m), put back after: 429R's camera only sees 40 m (the house), " +
                 "which would cut off the valley's far end, the mountains and the sky (the opening cutscene does the same, 220 m).")]
        public float seeingDistance = 240f;
        [Tooltip("Sky sphere radius (m). It follows the head, so it never clips: keep it outside the mountains and the far " +
                 "ground (~150 m from anywhere in the valley) and under Seeing Distance.")]
        public float skyRadius = 200f;
        // 429R: Halloween dusk (user, 2026-11-16: "more 429R style, halloween vibe"): the light comes from the sky picture's
        // harvest moon (32 degrees up, longitude 200: tools/halloween_sky.py), warm orange; a violet ambient. BALLS' day
        // sun was (52, -35, 0), (1, 0.95, 0.86) x 1.3, ambient (0.46, 0.5, 0.58).
        [Tooltip("The valley's light: from the sky picture's moon (keep them together if either moves), warm dusk orange.")]
        public Vector3 sunAngles = new Vector3(32f, 20f, 0f);
        public Color sunColour = new Color(1f, 0.8f, 0.58f);
        public float sunIntensity = 1.15f;
        [Tooltip("The sun's shadows: None is cheapest on Quest; balls sit on the lawn better with Hard (BALLS: Soft).")]
        public LightShadows sunShadows = LightShadows.Hard;
        [Tooltip("Flat ambient light while the stage runs: dusk violet, so the shadow sides stay readable.")]
        public Color ambient = new Color(0.36f, 0.31f, 0.48f);

        [Header("429R: kit (gate 3's own look; Tools > 429 Game > Ball Valley > Build Ball Valley Kit fills these; empty = BALLS' models)")]
        [Tooltip("BallValley_Kit_Mat (the kit's palette): with it set the valley wears the kit. The lawn, its cliffs and the " +
                 "holes take their colours from it.")]
        public Material kitMaterial;
        [Tooltip("The rim between the holes (BallValley_Kit_Rim_Straight_4m): laid side by side and stretched to fit each wall.")]
        public GameObject kitRim;
        [Tooltip("The corner and side holes (their pit, rune ring, collar and lanterns); each turned to its own corner or side.")]
        public GameObject kitHoleCorner, kitHoleSide;
        [Tooltip("Trees on the lawn (one picked per tree), and rocks (boulders, gravestones: one picked per rock).")]
        public GameObject[] kitTrees = new GameObject[0], kitRocks = new GameObject[0];
        [Tooltip("Pieces dressing the ground round the valley (outside its full size, no colliders).")]
        public KitPlace[] kitDecor = new KitPlace[0];
        [Tooltip("Gate 3's jello volcanoes (BallValley_Kit_Jello_Volcano): they stand on the lawn like boulders, never break " +
                 "and spit candy (Jello Volcano). How many: the Ball Valley Gate's Volcanoes. Empty: a code-built jelly cone.")]
        public GameObject[] kitVolcanoes = new GameObject[0];
        [Tooltip("A night re-tint of BALLS_Palette for BALLS' models still in the valley with the kit on (the mountains, the " +
                 "ground round it, the grass tufts). Empty: their day colours.")]
        public Texture2D nightPalette;

        /// <summary>429R: a kit piece and where it stands (x, z in metres, turn in degrees, size).</summary>
        [System.Serializable]
        public struct KitPlace
        {
            public GameObject piece;
            public Vector2 at;
            public float turn;
            [Min(0.05f)] public float size;
        }

        /// <summary>429R: the valley wears the kit (its material is set).</summary>
        public bool KitOn => kitMaterial != null;

        [Header("429R: sounds and log")]
        [Tooltip("The clips BALLS' sound table names, found by Set Up Ball Valley (429R's Audio/SFX, and BallValley/Sfx).")]
        public NamedSound[] sounds = new NamedSound[0];
        [Tooltip("Write the kill feed (pockets, shrinks, round ends) to the console.")]
        public bool logFeed = true;

        /// <summary>429R: a clip and the name BALLS' Sfx table knows it by.</summary>
        [System.Serializable]
        public struct NamedSound
        {
            public string name;
            public AudioClip clip;
        }

        /// <summary>429R: a round started (its number, 1..).</summary>
        public event System.Action<int> RoundStarted;
        /// <summary>429R: a round ended: its number, and the last ball standing (null: nobody).</summary>
        public event System.Action<int, BallPlayer> RoundEnded;
        /// <summary>429R: a ball went down a hole (or off the edge): which, and who pushed it in (null: nobody).</summary>
        public event System.Action<BallPlayer, BallPlayer> BallPocketed;

        /// <summary>429R: the round being played (1..; 0 before the first).</summary>
        public int Round { get; private set; }
        /// <summary>429R: how many rounds (Bots Per Round's length).</summary>
        public int RoundCount => Mathf.Max(1, botsPerRound != null ? botsPerRound.Length : 0);

        /// <summary>
        /// 429R: gate 3's own rules (Ball Valley Gate on this object): one round of its balls, no drop-ins after a pocket,
        /// no gifts, no last ball standing (the gate ends it with its boss).
        /// </summary>
        public bool GateMode => gate && gate.enabled;
        BallValleyGate gate;

        /// <summary>429R: the valley has reached its last, smallest phase (the gate's boss comes then at the latest).</summary>
        public event System.Action LastPhaseReached;

        /// <summary>
        /// 429R (gate 3, play test 2026-11-18: 15 bots all going for a player standing still took 106 health in a minute):
        /// may one more bot hunt the VR player? Only while fewer than the gate's Max Hunters do.
        /// </summary>
        public bool HumanHuntable()
        {
            if (!GateMode) return true;
            int hunting = 0;
            foreach (var p in players)
                if (p && !p.isHuman && p.alive && p.brain is BotBrain b && b.HuntsHuman) hunting++;
            return hunting < Mathf.Max(0, maxHunters);
        }

        /// <summary>429R gate 3: most bots hunting the VR player at once (the gate sets it each round).</summary>
        [System.NonSerialized] public int maxHunters = 1;
        /// <summary>
        /// 429R gate 3: may bots hunt each other? Off in the gate (2026-11-23 baseline: the bots pocketed 14 of 15 balls
        /// themselves): they graze and go for the player, so the pocketing is the player's.
        /// </summary>
        [System.NonSerialized] public bool botsHuntBots = true;
        /// <summary>429R gate 3: bots at the first round's start (below 0: the round's own number); the gate drops its balls in later.</summary>
        [System.NonSerialized] public int startBalls = -1;
        /// <summary>
        /// 429R gate 3 (2026-11-23: one long shrink over the gate's song): above 0, the valley is this size, set by the gate
        /// every frame (the round clock's phases are skipped); <see cref="drivePending"/> is where it's heading in a few
        /// seconds (bots, drop-ins and candy keep inside that).
        /// </summary>
        [System.NonSerialized] public float driveScale = -1f;
        [System.NonSerialized] public float drivePending = -1f;
        /// <summary>429R gate 3: trees stand anywhere on the lawn (BALLS: near its edges).</summary>
        [System.NonSerialized] public bool treesAnywhere;
        /// <summary>429R gate 3: jello volcanoes on the lawn (Kit Volcanoes).</summary>
        [System.NonSerialized] public int volcanoCount;
        /// <summary>429R gate 3: candies each tree's hits knock out of it (Field Prop's Hit Candies Left).</summary>
        [System.NonSerialized] public int treeHitCandies;

        /// <summary>
        /// 429R: the layers the valley's physics queries look at. Everything it makes is on Default; this keeps its bots,
        /// berries and drop-ins from seeing 429R's things round it (the Ground box, the teleport areas, the player's body).
        /// </summary>
        public static int QueryMask = 1;
        /// <summary>
        /// 429R: layers the valley's balls, slimes and berries never touch: 429R's big invisible Ground box under the start
        /// area (its top is the lawn's height, so balls would roll on across the holes) and the Teleport layer.
        /// </summary>
        public static int IgnoreLayers;

        /// <summary>
        /// 429R: every body the valley makes (balls, loose balls, slimes, berries): 429R's Ground and Teleport layers kept
        /// out, and BALLS' solver steps (6 + 1; 429R's project default is 12 + 3, it would play a little differently).
        /// </summary>
        public static void SetupBody(Rigidbody rb)
        {
            rb.excludeLayers = IgnoreLayers;
            rb.solverIterations = 6;
            rb.solverVelocityIterations = 1;
        }

        /// <summary>429R: <paramref name="m"/> metres of BALLS' 200 x 100 m layout (edge insets, prop spacing), in this valley.</summary>
        public float FieldM(float m) => m * tune.LayoutScale;

        /// <summary>429R: <paramref name="m"/> metres of room kept from BALLS' 9 m holes, for this valley's holes.</summary>
        public float HoleM(float m) => m * tune.HoleScale;

        [System.NonSerialized] public Valley valley;
        [System.NonSerialized] public BallPlayer human;   // 429R: the ball AddExternalBall made (none yet: watching)
        [System.NonSerialized] public BallPlayer king;
        [System.NonSerialized] public int seed;
        [System.NonSerialized] public Transform world;
        public readonly List<BallPlayer> players = new List<BallPlayer>();
        public readonly List<Pellet> pellets = new List<Pellet>();
        public readonly List<LooseBall> looseBalls = new List<LooseBall>();
        public readonly List<Destructible> destructibles = new List<Destructible>();
        public readonly List<Slime> slimes = new List<Slime>();
        public readonly List<GrassTuft> grass = new List<GrassTuft>();
        /// <summary>Rocks on the field (slimes squeeze out of these when there are no slime rocks).</summary>
        public readonly List<Collider> fieldRocks = new List<Collider>();
        /// <summary>The holes in the slime rocks (SlimeExit in BALLS_Map.blend): where slimes come out.</summary>
        public readonly List<Transform> slimeExits = new List<Transform>();
        public readonly List<FeedLine> feed = new List<FeedLine>();

        public struct FeedLine
        {
            public string text;
            public Color color;
            public float time;
        }

        readonly List<KeyValuePair<BallPlayer, float>> respawnQueue = new List<KeyValuePair<BallPlayer, float>>();
        float pelletTimer, grassTimer, slimeTimer, cullTimer;
        int botCounter;
        // 429R: no King of the Valley crown (user, 2026-11-16: "remove the king crown in gate 3, we no need that"). The
        // king is still worked out (bots and the log use it); nothing shows it.
        bool owner;   // 429R: this one set the shared statics up (a second stage at once is turned away)

        static readonly string[] BotNames =
            { "Rex", "Olga", "Pip", "Bongo", "Zuzu", "Kiki", "Moss", "Dot", "Fizz", "Taro", "Nyx", "Bolt", "Mochi", "Gus", "Lulu", "Yeti" };

        /// <summary>
        /// The order bots take the ball colours in (indices into <see cref="BallColors"/>): the most different ones
        /// first, so a small game never has two look-alikes.
        /// </summary>
        static readonly int[] BotColorOrder = { 0, 13, 5, 8, 19, 23, 4, 16, 11, 7, 21, 18, 12, 1, 9, 14, 3, 10, 17, 6, 2, 15, 20, 22 };

        static readonly Color[] PelletColors =
        {
            new Color(1f, 0.85f, 0.2f), new Color(1f, 0.4f, 0.6f), new Color(0.4f, 0.9f, 1f), new Color(0.6f, 1f, 0.4f)
        };

        void Awake()
        {
            // 429R: one at a time: Gfx, Sfx and BallPhysics are shared statics.
            if (I && I != this)
            {
                Debug.LogError("[BallValley] Another Ball Valley is still running; this one removes itself.", this);
                Destroy(gameObject);
                return;
            }
            I = this;
            owner = true;
            gate = GetComponent<BallValleyGate>();
            CheckPlace();
            int ground = LayerMask.NameToLayer("Ground"), teleport = LayerMask.NameToLayer("Teleport");
            IgnoreLayers = (ground >= 0 ? 1 << ground : 0) | (teleport >= 0 ? 1 << teleport : 0);
            // 429R (gate 3): the player's own colliders too (the XR Origin's body on Player, the body parts): balls meet
            // the player only through the VR body (Ball Valley Player), never the rig's capsule.
            foreach (var layer in new[] { "Player", "Character Collider", "Character Controller" })
            {
                int l = LayerMask.NameToLayer(layer);
                if (l >= 0) IgnoreLayers |= 1 << l;
            }
            QueryMask = 1 << 0;   // Default: everything the valley makes
            if (vertexColorShader) Gfx.VertexColorShader = vertexColorShader;
            Gfx.Init(transform);
            if (KitOn && nightPalette) Gfx.PaletteMaterial().SetTexture("_BaseMap", nightPalette);   // 429R: kit: BALLS' leftovers at night
            BallPhysics.Hook(tune.ballBounce, tune.rimBounce);
        }

        void Start()
        {
            if (!owner) return;
            SetupLook();
            gameObject.AddComponent<Fireworks>();
            Sfx.Init(transform, FindSound);
            BuildWorld(Random.Range(1, int.MaxValue));
            StartRound(1, false);
        }

        /// <summary>429R: the valley is built in world space round (0, 0, 0): the stage has to sit there, unturned and unscaled.</summary>
        void CheckPlace()
        {
            var t = transform;
            bool moved = t.position.sqrMagnitude > 0.0001f;
            bool turned = Quaternion.Angle(t.rotation, Quaternion.identity) > 0.01f;
            bool scaled = (t.lossyScale - Vector3.one).sqrMagnitude > 0.0001f;
            if (moved || turned || scaled)
                Debug.LogError("[BallValley] The stage must sit at the world origin, unturned and unscaled (gate stages spawn at " +
                               "[LevelManager], (0, 0, 0)); it is at " + t.position + ", turned " + t.eulerAngles + ", scale " + t.lossyScale +
                               ". The valley is built round the origin anyway, so it won't line up with this object.", this);
        }

        AudioClip FindSound(string clipName)
        {
            if (sounds == null) return null;
            foreach (var s in sounds)
                if (s.clip && s.name == clipName) return s.clip;
            return null;
        }

        void OnDestroy()
        {
            if (!owner) return;
            // 429R: everything given back and every shared static reset, so the next stage starts clean.
            RestoreLook();
            if (skyMesh) Destroy(skyMesh);
            BallPhysics.Unhook();
            Sfx.Shutdown();
            StunStars.Release();
            Gfx.Shutdown();
            ModelLib.Clear();
            if (I == this) I = null;
        }

        // ---------- world ----------

        /// <summary>
        /// Builds a fresh valley. Rocks and trees come from <paramref name="worldSeed"/>.
        /// 429R: no network client, no human ball; the bots come with the first round.
        /// </summary>
        public void BuildWorld(int worldSeed)
        {
            ClearWorld();
            seed = worldSeed;
            world = new GameObject("World").transform;
            world.SetParent(transform, false);   // 429R: under the stage (BALLS: the scene root)
            valley = new GameObject("Valley").AddComponent<Valley>();
            valley.transform.SetParent(world, false);
            valley.Build(tune);
            Physics.SyncTransforms();

            // Same seed + same order of Random calls = the same rocks and trees.
            var savedRandom = Random.state;
            Random.InitState(worldSeed);
            for (int i = 0; i < tune.rockCount; i++) SpawnRock(FindClearPoint(FieldM(10f), 4f, HoleM(22f)));   // 429R: scaled
            Physics.SyncTransforms();
            for (int i = 0; i < tune.treeCount; i++) SpawnTree(treesAnywhere ? FindClearPoint(FieldM(10f), 4f, HoleM(22f)) : EdgePoint());   // 429R gate 3: anywhere
            Physics.SyncTransforms();
            for (int i = 0; i < volcanoCount; i++) SpawnVolcano(FindClearPoint(FieldM(10f), 5f, HoleM(22f)));   // 429R gate 3
            Physics.SyncTransforms();
            Random.state = savedRandom;

            // 429R: a third of the candies at once (each is a whole candy prefab: a long first frame in the headset), the
            // spawner fills the rest within seconds.
            int firstCandies = candyPrefabs != null && candyPrefabs.Length > 0 ? Mathf.Min(PelletCap, Mathf.Max(6, PelletCap / 3)) : PelletCap;
            for (int i = 0; i < firstCandies; i++) SpawnPellet(FindClearPoint(3f, 0.6f, HoleM(8f)) + Vector3.up * 0.4f, Vector3.zero, RandomPelletColor());
            for (int i = 0; i < tune.maxGrass / 2; i++) SpawnGrass();
            for (int i = 0; i < tune.slimeCount / 2; i++) SpawnSlime();
            if (KitOn) PlaceKitDecor();   // 429R
        }

        /// <summary>429R: the kit's dressing round the valley (looks only), under the world so it goes with it.</summary>
        void PlaceKitDecor()
        {
            if (kitDecor == null) return;
            var holder = NewWorldObject("Decor").transform;
            foreach (var d in kitDecor)
            {
                if (!d.piece) continue;
                var go = Instantiate(d.piece, holder, false);
                go.name = d.piece.name;
                go.transform.SetLocalPositionAndRotation(new Vector3(d.at.x, 0f, d.at.y), Quaternion.Euler(0f, d.turn, 0f));
                go.transform.localScale = Vector3.one * (d.size > 0f ? d.size : 1f);
                foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
                Gfx.NoShadows(go.transform);
            }
        }

        /// <summary>429R: a kit piece placed under <paramref name="parent"/> with its own pivot (its colliders taken off).</summary>
        static Transform PlaceKit(GameObject piece, Transform parent)
        {
            var go = Instantiate(piece, parent, false);
            go.name = piece.name;
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            return go.transform;
        }

        /// <summary>
        /// The ball colours bots get (BALLS: also the 24 a player can pick). No white: a white ball is food (every loose
        /// ball is white, see <see cref="LooseColor"/>).
        /// </summary>
        public static readonly Color[] BallColors =
        {
            new Color(0.85f, 0.10f, 0.12f), new Color(0.48f, 0.05f, 0.12f), new Color(0.85f, 0.12f, 0.45f), new Color(1.00f, 0.42f, 0.35f),   // red, maroon, raspberry, coral
            new Color(1.00f, 0.50f, 0.08f), new Color(1.00f, 0.84f, 0.12f), new Color(0.48f, 0.52f, 0.10f), new Color(0.55f, 0.88f, 0.18f),   // orange, yellow, olive, lime
            new Color(0.10f, 0.60f, 0.28f), new Color(0.35f, 0.92f, 0.65f), new Color(0.02f, 0.48f, 0.48f), new Color(0.15f, 0.80f, 0.85f),   // green, mint, teal, cyan
            new Color(0.42f, 0.70f, 1.00f), new Color(0.12f, 0.35f, 0.90f), new Color(0.06f, 0.10f, 0.42f), new Color(0.28f, 0.16f, 0.62f),   // sky, blue, navy, indigo
            new Color(0.55f, 0.25f, 0.85f), new Color(0.75f, 0.62f, 1.00f), new Color(0.92f, 0.12f, 0.80f), new Color(1.00f, 0.50f, 0.70f),   // purple, lavender, magenta, pink
            new Color(0.80f, 0.62f, 0.40f), new Color(0.45f, 0.28f, 0.15f), new Color(0.38f, 0.42f, 0.48f), new Color(0.10f, 0.10f, 0.12f),   // tan, brown, slate, black
        };
        // 429R: no start screen (BALLS' name and colour pickers, PlayerPrefs, the name filter).

        static bool SameColor(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.02f;

        /// <summary>A colour for a new bot: the first one (in <see cref="BotColorOrder"/>) nobody has yet; with more balls than colours, they go round again.</summary>
        Color NextBotColor()
        {
            foreach (int i in BotColorOrder)
            {
                bool taken = false;
                foreach (var p in players)
                    if (SameColor(p.color, BallColors[i])) { taken = true; break; }
                if (!taken) return BallColors[i];
            }
            return BallColors[BotColorOrder[botCounter % BotColorOrder.Length]];
        }

        public void ClearWorld()
        {
            if (world)
            {
                // Deactivate first so the old colliders are gone before the new map is placed (Destroy waits for the frame end).
                world.gameObject.SetActive(false);
                Destroy(world.gameObject);
            }
            world = null;
            valley = null;
            human = null;
            king = null;
            players.Clear();
            pellets.Clear();
            looseBalls.Clear();
            destructibles.Clear();
            slimes.Clear();
            grass.Clear();
            fieldRocks.Clear();
            slimeExits.Clear();
            feed.Clear();
            respawnQueue.Clear();
            props.Clear();
            ResetRoundState();
            botCounter = 0;
            Physics.SyncTransforms();
        }

        GameObject NewWorldObject(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(world, false);
            return go;
        }

        // ---------- players ----------

        public BallPlayer CreatePlayer(string n, Color c, bool isHuman, bool isLocal, IBallBrain brain)
        {
            var go = NewWorldObject("Player " + n);
            var p = go.AddComponent<BallPlayer>();
            p.Init(n, c, isHuman, isLocal, brain);
            p.revives = GateMode ? 0 : tune.revives;   // 429R: gate 3: pocketed = gone
            players.Add(p);
            return p;
        }

        /// <summary>429R: a new bot, not dropped in yet (a round's start drops everyone in).</summary>
        public BallPlayer CreateBot()
        {
            int i = botCounter++;
            string name = BotNames[i % BotNames.Length];
            if (i >= BotNames.Length) name += " " + (i / BotNames.Length + 1);   // lots of bots: Rex, ..., Rex 2, ...
            return CreatePlayer(name, NextBotColor(), false, false, new BotBrain());
        }

        /// <summary>A new bot, dropped in at once.</summary>
        public BallPlayer AddBot()
        {
            var bot = CreateBot();
            SpawnPlayer(bot);
            return bot;
        }

        public int BotCount
        {
            get
            {
                int n = 0;
                foreach (var p in players) if (!p.isHuman) n++;
                return n;
            }
        }

        /// <summary>Drops a bot (prefers one that is out or small).</summary>
        public void RemoveOneBot()
        {
            BallPlayer pick = null;
            foreach (var p in players)
            {
                if (p.isHuman) continue;
                if (!pick || (!p.alive && pick.alive) || (p.alive == pick.alive && p.TotalBalls < pick.TotalBalls)) pick = p;
            }
            if (pick) RemovePlayer(pick);
        }

        /// <summary>Removes a player with no food drop or kill feed.</summary>
        public void RemovePlayer(BallPlayer p)
        {
            if (!p) return;
            players.Remove(p);
            respawnQueue.RemoveAll(k => k.Key == p);
            if (king == p) king = null;
            if (human == p) human = null;
            p.Despawn();
            Destroy(p.gameObject);
        }

        /// <summary>
        /// 429R seam (the VR player, a later step): one more ball, steered by <paramref name="brain"/>. It's a person's
        /// ball: never dropped back in by itself (bots are), and it counts for the last ball standing like any other.
        /// Not dropped in yet: use <see cref="SpawnAt"/> (or <see cref="TryRevive"/>, which uses a revive after the free
        /// first drop). <paramref name="neverGrows"/>: no tail, food only scores.
        /// </summary>
        public BallPlayer AddExternalBall(string displayName, Color c, IBallBrain brain, bool neverGrows = true)
        {
            var p = CreatePlayer(displayName, c, true, true, brain);
            p.neverGrows = neverGrows;
            p.freeDrop = true;
            if (!human) human = p;
            return p;
        }

        /// <summary>429R seam: <paramref name="p"/> appears at <paramref name="pos"/> (its centre; no revive used).</summary>
        public void SpawnAt(BallPlayer p, Vector3 pos)
        {
            if (!p || p.alive) return;
            p.freeDrop = false;
            p.Spawn(pos);
            respawnQueue.RemoveAll(k => k.Key == p);
        }

        public void SpawnPlayer(BallPlayer p) => p.Spawn(SpawnPoint(p) + Vector3.up * tune.spawnDropHeight);   // dropped in from the sky (429R: Spawn Drop Height)

        /// <summary>A drop-in this far from every pit's rim (m) is safe; closer, the nearer the worse (429R: times Hole Scale).</summary>
        const float SafeFromPits = 7f;

        /// <summary>
        /// Where a ball drops in from the sky: on the field and off its edge, never on a tree or boulder, as far as it can
        /// be from the holes' pits (and, once the border is gone, the edges) first and from the other balls second.
        /// </summary>
        Vector3 SpawnPoint(BallPlayer p)
        {
            float hx = valley.SafeHalfX, hz = valley.SafeHalfZ;
            float edge = Mathf.Min(8f, Mathf.Min(hx, hz) * 0.3f);   // off the edge (less room in a tiny valley)
            float safe = HoleM(SafeFromPits);   // 429R
            Vector3 best = Vector3.zero;
            float bestScore = float.MinValue;
            for (int i = 0; i < 48; i++)
            {
                var cand = new Vector3(Random.Range(-hx + edge, hx - edge), 0f, Random.Range(-hz + edge, hz - edge));
                float others = 6f;
                foreach (var o in players)
                    if (o != p && o.alive) others = Mathf.Min(others, Vector3.Distance(cand, Flat(o.Position)));
                float drop = valley.DistanceToNearestPit(cand);
                if (!valley.BordersOn) drop = Mathf.Min(drop, Mathf.Min(hx - Mathf.Abs(cand.x), hz - Mathf.Abs(cand.z)));   // the open edges too
                // Every metre from a drop (up to SafeFromPits) is worth 10, every metre from a ball (up to 6) 2.
                float score = Mathf.Min(drop, safe) * 10f + others * 2f;
                if (PropAt(cand)) score -= 1000f;
                if (score > bestScore) { bestScore = score; best = cand; }
            }
            return best;
        }

        /// <summary>A standing tree or boulder takes up <paramref name="pos"/> (a ball dropped there would land on it).</summary>
        bool PropAt(Vector3 pos)
        {
            foreach (var pr in props)
                if (pr && !pr.Broken && !pr.Waiting && Near(pr.transform.position, pos, pr.baseRadius + 1.5f)) return true;
            return false;
        }

        /// <summary>
        /// BALLS: camera shake for <paramref name="p"/>. 429R: nothing (never shake a VR view, and no controller buzz: the
        /// user's rule); kept so the callers stay as they are.
        /// </summary>
        public void Shake(BallPlayer p, float amount) { }

        // ---------- loop ----------

        void Update()
        {
            if (!world) return;
            float dt = Time.deltaTime;
            UpdateRound();
            cullTimer -= dt;
            if (cullTimer <= 0f)
            {
                cullTimer = shrinking ? 0.1f : 0.25f;   // keep up with the border while it moves in
                CullOutside();
            }

            pelletTimer -= dt;
            if (pelletTimer <= 0f)
            {
                pelletTimer = tune.pelletSpawnInterval / Mathf.Max(0.1f, tune.foodBoost);
                if (pellets.Count < BerryCap)
                    SpawnPellet(FindClearPoint(3f, 0.6f, HoleM(8f)) + Vector3.up * 0.4f, Vector3.zero, RandomPelletColor());
            }

            grassTimer -= dt;
            if (grassTimer <= 0f)
            {
                grassTimer = tune.grassSpawnInterval * Random.Range(0.6f, 1.4f);   // "sometimes": not on a strict beat
                if (grass.Count < tune.maxGrass) SpawnGrass();
            }

            slimeTimer -= dt;
            if (slimeTimer <= 0f)
            {
                slimeTimer = Mathf.Lerp(tune.slimeSpawnInterval, tune.slimeSpawnIntervalSmallest, Shrunk01) * Random.Range(0.7f, 1.3f);
                if (slimes.Count < SlimeCap) SpawnSlime();
            }

            for (int i = respawnQueue.Count - 1; i >= 0; i--)
            {
                if (i >= respawnQueue.Count || Time.time < respawnQueue[i].Value) continue;
                var bot = respawnQueue[i].Key;
                respawnQueue.RemoveAt(i);
                TryRevive(bot);
            }

            // The Moment of Truth: nobody waits outside for the others to knock each other out. Pocketed with a revive left
            // = straight back in after a second. 429R: bots only (the player's ball is a later step's to bring back).
            if (momentOfTruth && !roundOver)
                foreach (var p in players)
                    if (!p.alive && !p.isHuman && Time.time > p.deathTime + tune.momentOfTruthReviveSeconds)
                        TryRevive(p);

            var oldKing = king;
            UpdateKing();
            if (king != oldKing) kingSince = Time.time;   // a new reign starts
            // Holding the title pays: points keep ticking up for as long as you're King of the Valley.
            if (king) king.points += tune.kingPointsPerSecond * dt;
            if (king && king != oldKing && Time.time > nextKingNews)
            {
                nextKingNews = Time.time + 4f;
                AddFeed($"{king.displayName} is the King of the Valley!", new Color(1f, 0.82f, 0.25f));
            }
        }

        float nextKingNews;
        /// <summary>When the current King of the Valley took the title (their reign so far: now minus this).</summary>
        float kingSince;

        void UpdateKing()
        {
            king = null;
            foreach (var p in players)
            {
                if (!p.alive || p.BabyCount == 0) continue;
                if (!king || p.BabyCount > king.BabyCount || (p.BabyCount == king.BabyCount && p.kills > king.kills)) king = p;
            }
        }

        void LateUpdate()
        {
            KeepLook();     // 429R: nobody else's fog or far plane over the valley
            FollowHead();   // 429R: the sky stays round the head
        }

        public void OnPlayerDied(BallPlayer p, BallPlayer killer, Cave cave)
        {
            // Into a hole, or off the field's edge somewhere else?
            bool inHole = p.diedInHole;
            string text = killer ? $"{killer.displayName} potted {p.displayName}!"
                : inHole ? $"{p.displayName} rolled into a cave" : $"{p.displayName} fell off the edge";
            AddFeed(text, Color.Lerp(killer ? killer.color : p.color, Color.white, 0.35f));
            // Fell in a hole by themself (nobody pushed them): everyone else gets a revive back, spectators too.
            // 429R: not in gate 3 (every pocket counts down its balls).
            if (!killer && inHole && !roundOver && !GateMode && tune.selfPocketGift > 0 && mapScale > tune.selfPocketGiftMinScale) GiftRevives(p);
            if (!p.isHuman) QueueRevive(p, Random.Range(3f, 6f));
            else if (!CanRevive(p)) AddFeed($"{p.displayName} is out of revives", new Color(1f, 0.6f, 0.5f));
            if (cave != null) Gfx.Puff(cave.mouth + cave.outward * HoleM(3f), new Color(0.15f, 0.12f, 0.1f, 0.8f), 24, HoleM(4f), 6f);   // 429R: hole sized
            // A long reign ends in a hole: a Rainbow Slime jumps out of that same hole.
            if (p == king && inHole && Time.time - kingSince >= tune.rainbowKingSeconds)
                SpawnRainbowSlime(cave ?? valley.NearestCave(p.lastPosition));
            BallPocketed?.Invoke(p, killer);   // 429R
        }

        // ---------- rounds: shrinking valley, limited revives ----------

        /// <summary>How big the valley is right now (1 = full size).</summary>
        [System.NonSerialized] public float mapScale = 1f;
        /// <summary>Someone is the last ball standing (429R: the next round comes after Round Restart Delay in Watch Mode).</summary>
        [System.NonSerialized] public bool roundOver;
        [System.NonSerialized] public BallPlayer roundWinner;
        /// <summary>The winner's balls (main ball + tail) as the round ended, even if they'd just been pocketed.</summary>
        [System.NonSerialized] public int roundWinnerBalls;
        /// <summary>The valley is shrinking from one phase to the next right now.</summary>
        [System.NonSerialized] public bool shrinking;
        /// <summary>
        /// Round over: seconds until the next round (429R: Watch Mode). Otherwise: seconds until the valley shrinks to its
        /// next phase (0 while it shrinks, and at its smallest).
        /// </summary>
        [System.NonSerialized] public float roundSeconds;
        float roundStart, roundStartScale = 1f, roundRestartAt;
        /// <summary>The valley is shrinking to this size right now (-1 = it isn't).</summary>
        [System.NonSerialized] public float borderPending = -1f;
        float shrinkFrom = 1f, shrinkStart;
        /// <summary>The phase the valley is at: 0 = phase 1 (full size) up to PhaseCount - 1 (its smallest).</summary>
        [System.NonSerialized] public int phase;
        /// <summary>The layout of trees and boulders this round (a new round picks a new one, see ScatterProps; 0 = the one the world was built with).</summary>
        [System.NonSerialized] public int matchSeed;
        /// <summary>The valley is at its smallest: anyone pocketed drops straight back in until one ball is left (revives still count).</summary>
        [System.NonSerialized] public bool momentOfTruth;
        /// <summary>429R: when this round's Moment of Truth began (Time.time): bots get angrier from then (BotBrain).</summary>
        [System.NonSerialized] public float momentOfTruthAt;
        [Tooltip("429R: in the Moment of Truth every bot's fight grows from its own to all out over this many seconds, so a " +
                 "round can't stall (play test 2026-11-16: 4 bots circled for 40+ s without a single pocket).")]
        public float momentOfTruthRampSeconds = 15f;
        // 429R: no replay vote and no online clock (BALLS' votes, myVote, mapScaleTarget, hostMatchLeft).

        /// <summary>
        /// About how long the round has left on its shrink schedule (the valley is at its smallest just after Shrink Delay +
        /// Shrink Duration; after that it's the Moment of Truth until one ball is left): 0 in the Moment of Truth and once over.
        /// </summary>
        public float MatchSecondsLeft
        {
            get
            {
                if (roundOver || momentOfTruth) return 0f;
                return Mathf.Max(0f, ShrinkAt(PhaseCount - 1) + ShrinkSeconds - (Time.time - roundStart));
            }
        }

        // ---------- the phases ----------

        List<float> phaseSizes;
        int phasesFor = -1;

        /// <summary>The valley's sizes, phase 1 (full size) first and its smallest last (Valley.BorderPhases).</summary>
        public List<float> Phases
        {
            get
            {
                int sig = tune.phases * 100000 + Mathf.RoundToInt(tune.minMapScale * 10000f);
                if (phaseSizes == null || sig != phasesFor)
                {
                    phaseSizes = Valley.BorderPhases(tune);
                    phasesFor = sig;
                }
                return phaseSizes;
            }
        }

        public int PhaseCount => Phases.Count;

        /// <summary>
        /// Seconds into a round when the valley starts shrinking to phase <paramref name="k"/> (k = 1 .. PhaseCount - 1, 0-based):
        /// the first at Shrink Delay, the last at Shrink Delay + Shrink Duration, the others evenly between.
        /// </summary>
        float ShrinkAt(int k)
        {
            float start = Mathf.Max(tune.shrinkDelay, tune.regrowDuration);
            int n = PhaseCount;
            return n <= 2 ? start : start + Mathf.Max(1f, tune.shrinkDuration) * (k - 1) / (n - 2);
        }

        /// <summary>How long one shrink takes (Phase Shrink Seconds, but never longer than the time between two).</summary>
        float ShrinkSeconds
        {
            get
            {
                int n = PhaseCount;
                float between = n <= 2 ? float.MaxValue : Mathf.Max(1f, tune.shrinkDuration) / (n - 2);
                return Mathf.Clamp(tune.phaseShrinkSeconds, 0.2f, Mathf.Max(0.2f, between * 0.8f));
            }
        }

        /// <summary>Eases in and out (0..1 to 0..1): the valley starts moving gently and settles gently.</summary>
        static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        /// <summary>Trees and boulders, in the order they were built.</summary>
        public readonly List<FieldProp> props = new List<FieldProp>();

        void ResetRoundState()
        {
            mapScale = roundStartScale = shrinkFrom = 1f;
            borderPending = -1f;
            phase = 0;
            matchSeed = 0;
            momentOfTruth = false;
            roundStart = Time.time;
            roundOver = false;
            roundWinner = null;
            shrinking = false;
            roundSeconds = 0f;
        }

        /// <summary>
        /// The round clock. A new round first grows the valley back to phase 1; then, on the shrink schedule, it shrinks
        /// smoothly from one phase to the next (the lawn, the floor, the border and the six holes all move in together).
        /// Ends the round when one ball is left.
        /// </summary>
        void UpdateRound()
        {
            float t = Time.time - roundStart;
            // 429R gate 3 (2026-11-23): the gate drives the valley's size itself (one long shrink over its song).
            if (driveScale > 0f)
            {
                if (!Mathf.Approximately(mapScale, driveScale)) SetMapScale(driveScale);
                borderPending = -1f;
                shrinking = drivePending > 0f && drivePending < mapScale - 0.001f;
                if (valley)
                {
                    valley.SetPending(shrinking ? drivePending : -1f);
                    valley.SetBorders(true);
                }
                UpdatePropsShown();
                roundSeconds = 0f;
                return;
            }
            float regrow = Mathf.Max(0.01f, tune.regrowDuration);
            if (t < regrow) SetMapScale(Mathf.Lerp(roundStartScale, 1f, Smooth(t / regrow)));   // a new round: back from the last phase to the first
            else if (borderPending > 0f)
            {
                float k = Mathf.Clamp01((Time.time - shrinkStart) / (forcedShrinkSeconds > 0f ? forcedShrinkSeconds : ShrinkSeconds));   // 429R: Shrink To Last
                SetMapScale(Mathf.Lerp(shrinkFrom, borderPending, Smooth(k)));
                if (k >= 1f) ReachedPhase();
            }
            else
            {
                if (phase == 0 && mapScale != 1f) SetMapScale(1f);   // grown back all the way
                if (!roundOver && phase + 1 < PhaseCount && t >= ShrinkAt(phase + 1)) BeginShrink();
            }
            shrinking = borderPending > 0f;
            if (valley)
            {
                valley.SetPending(borderPending);   // bots, drop-ins and berries keep inside the size it's shrinking to
                valley.SetBorders(!(momentOfTruth && tune.momentOfTruthCrumble));   // (switched off: the border stays up to the end)
            }
            UpdatePropsShown();

            if (roundOver)
            {
                // 429R: no replay vote. Watch Mode: the next round after Round Restart Delay; otherwise NextRound() starts it.
                roundSeconds = Mathf.Max(0f, roundRestartAt - Time.time);
                if (watchMode && Time.time >= roundRestartAt) NextRound();
                return;
            }
            roundSeconds = !shrinking && phase + 1 < PhaseCount ? Mathf.Max(0f, roundStart + ShrinkAt(phase + 1) - Time.time) : 0f;

            // 429R: gate 3 has no last ball standing: its gate ends the round (its boss, then the seed).
            if (GateMode) return;
            // Last ball standing: everyone else is out of revives.
            if (players.Count < 2) return;
            int left = 0;
            BallPlayer last = null;
            foreach (var p in players)
                if (p.alive || CanRevive(p)) { left++; last = p; }
            if (left > 1) return;
            roundOver = true;
            roundWinner = last;
            roundWinnerBalls = last ? (last.alive ? last.TotalBalls : last.ballsAtDeath) : 0;
            roundRestartAt = Time.time + tune.roundRestartDelay;
            if (last)
            {
                last.wins++;
                last.points += tune.pointsPerRoundWin;
                AddFeed($"Round {Round}: last ball standing: {last.displayName}!", new Color(1f, 0.82f, 0.25f));
            }
            else AddFeed($"Round {Round}: nobody is left standing!", new Color(1f, 0.82f, 0.25f));
            RoundEnded?.Invoke(Round, last);   // 429R
        }

        /// <summary>429R: bots in round <paramref name="r"/> (1..).</summary>
        public int BotsFor(int r)
        {
            if (botsPerRound == null || botsPerRound.Length == 0) return 5;
            return Mathf.Max(1, botsPerRound[Mathf.Clamp(r - 1, 0, botsPerRound.Length - 1)]);
        }

        /// <summary>429R: the bots' temper range in round <paramref name="r"/> (1..): x least, y most.</summary>
        public Vector2 TemperFor(int r)
        {
            if (aggressionPerRound == null || aggressionPerRound.Length == 0) return new Vector2(0.75f, 1f);
            Vector2 a = aggressionPerRound[Mathf.Clamp(r - 1, 0, aggressionPerRound.Length - 1)];
            return new Vector2(Mathf.Clamp01(Mathf.Min(a.x, a.y)), Mathf.Clamp01(Mathf.Max(a.x, a.y)));
        }

        /// <summary>
        /// 429R: the next round (Watch Mode calls it by itself once a round is over; after the last round, round 1 again).
        /// Outside Watch Mode: false after the last round (nothing starts).
        /// </summary>
        public bool NextRound()
        {
            if (watchMode)
            {
                StartRound(Round % RoundCount + 1, true);
                return true;
            }
            if (Round >= RoundCount) return false;
            StartRound(Round + 1, true);
            return true;
        }

        /// <summary>429R: round <paramref name="r"/>: its number of bots (added or taken away), their temper, then a fresh round.</summary>
        void StartRound(int r, bool announce)
        {
            Round = Mathf.Max(1, r);
            int bots = GateMode ? Mathf.Max(0, startBalls) : startBalls >= 0 ? startBalls : BotsFor(Round);   // 429R: gate 3 drops its balls round by round
            Vector2 temper = GateMode ? new Vector2(tune.botAggressionMin, tune.botAggressionMax) : TemperFor(Round);   // (gate 3 sets them)
            tune.botAggressionMin = temper.x;   // (each bot keeps its own temper inside the range: BotBrain reads it live)
            tune.botAggressionMax = temper.y;
            while (BotCount > bots) RemoveOneBot();
            while (BotCount < bots) CreateBot();
            StartMatch(announce);
            AddFeed($"Round {Round}: {bots} balls, tempers {temper.x:0.##} - {temper.y:0.##}", new Color(0.6f, 1f, 0.6f));
            RoundStarted?.Invoke(Round);
        }

        /// <summary>
        /// A fresh round: the valley grows back from its last phase to the first, broken trees and boulders are whole again
        /// (on a new round they all stand somewhere new), and everyone starts over as one ball with full revives.
        /// </summary>
        void StartMatch(bool announce)
        {
            roundOver = false;
            roundWinner = null;
            roundStart = Time.time;
            roundStartScale = mapScale;
            borderPending = -1f;
            forcedShrinkSeconds = 0f;   // 429R
            phase = 0;
            momentOfTruth = false;
            respawnQueue.Clear();
            if (announce) ScatterProps(Random.Range(1, int.MaxValue));
            foreach (var pr in props)
                if (pr && pr.Broken) pr.Restore();   // (they pop up as the growing field reaches them: UpdatePropsShown)
            foreach (var p in players)
            {
                p.points = 0f;
                p.kills = p.deaths = p.assists = 0;   // K / D / A count the round, like the points
                p.revives = GateMode ? 0 : tune.revives;   // 429R: gate 3: pocketed = gone
                if (p.alive) p.Despawn();
                p.freeDrop = true;   // the first drop of a round doesn't use a revive
                if (!p.isHuman) QueueRevive(p, Random.Range(0.3f, 2f));
                // 429R: a person's ball (AddExternalBall) is dropped in by the step that added it
            }
            if (announce) AddFeed("The valley grows back", new Color(0.6f, 1f, 0.6f));
        }

        void SetMapScale(float s)
        {
            if (!valley) return;
            mapScale = s;
            valley.SetScale(s);
        }

        /// <summary>The valley starts shrinking to its next phase (UpdateRound moves it there over Phase Shrink Seconds).</summary>
        void BeginShrink()
        {
            shrinkFrom = mapScale;
            shrinkStart = Time.time;
            borderPending = Phases[phase + 1];
            bool last = phase + 2 >= PhaseCount;
            AddFeed(last ? "The valley shrinks one last time!" : $"The valley shrinks!  Phase {phase + 2} of {PhaseCount}", new Color(1f, 0.6f, 0.3f));
            Shrinks?.Invoke(phase + 1, last);
        }

        /// <summary>429R: the valley starts shrinking to phase <c>k</c> (0-based); true: its last.</summary>
        public event System.Action<int, bool> Shrinks;

        /// <summary>
        /// 429R (gate 3): straight to the last, smallest phase from wherever it is, over <paramref name="seconds"/> (the gate's
        /// balls are all gone early: its boss comes on the smallest field). Nothing if it's there or on its way already.
        /// </summary>
        public void ShrinkToLast(float seconds) => ShrinkToPhase(PhaseCount - 1, seconds);

        /// <summary>
        /// 429R gate 3: to phase <paramref name="k"/> (0-based: 1 is the first step smaller) from wherever it is, over
        /// <paramref name="seconds"/> (the gate shrinks it a step each round itself). Nothing if it's there, past it, or on
        /// its way already.
        /// </summary>
        public void ShrinkToPhase(int k, float seconds)
        {
            int last = PhaseCount - 1;
            k = Mathf.Clamp(k, 1, last);
            if (!valley || phase >= k || roundOver) return;
            if (borderPending > 0f && Mathf.Approximately(borderPending, Phases[k])) return;
            phase = k - 1;   // (from where it is now, mid-shrink too: Begin Shrink starts at the size it has)
            BeginShrink();
            forcedShrinkSeconds = Mathf.Max(0.2f, seconds);
        }

        /// <summary>
        /// 429R gate 3: <paramref name="n"/> new balls drop in from the sky over <paramref name="within"/> seconds (a round's
        /// balls); pocketed, each is gone for good.
        /// </summary>
        public void DropBalls(int n, float within)
        {
            for (int i = 0; i < n; i++)
            {
                var bot = CreateBot();
                bot.freeDrop = true;
                QueueRevive(bot, Random.Range(0.2f, Mathf.Max(0.3f, within)));
            }
        }

        /// <summary>
        /// 429R gate 3 (2026-11-23): <paramref name="p"/>, pocketed (not by the player), drops back in from the sky after
        /// <paramref name="delay"/> seconds: only the player's pockets count.
        /// </summary>
        public void DropBackIn(BallPlayer p, float delay)
        {
            if (!p || p.alive) return;
            p.freeDrop = true;
            QueueRevive(p, delay);
        }

        // 429R: Shrink To Last's own speed for the shrink going on (0: the usual Shrink Seconds).
        float forcedShrinkSeconds;

        /// <summary>429R: the valley is at its last, smallest phase.</summary>
        public bool AtLastPhase => valley && phase >= PhaseCount - 1 && borderPending <= 0f;

        /// <summary>
        /// The valley has shrunk to its next phase. At the last one it's the Moment of Truth: anyone pocketed drops straight
        /// back in (no waiting outside) until one ball is left, and whatever trees and boulders are left crumble.
        /// </summary>
        void ReachedPhase()
        {
            float to = borderPending;
            borderPending = -1f;
            forcedShrinkSeconds = 0f;   // 429R
            phase = Mathf.Min(phase + 1, PhaseCount - 1);
            SetMapScale(to);
            CullOutside();
            if (phase >= PhaseCount - 1) LastPhaseReached?.Invoke();   // 429R: gate 3's boss comes now at the latest
            if (phase < PhaseCount - 1 || !tune.momentOfTruth || momentOfTruth || roundOver) return;
            momentOfTruth = true;
            momentOfTruthAt = Time.time;   // 429R: bots get angrier from now
            foreach (var pr in props)
                if (pr && !pr.Broken && !pr.Waiting) SwallowProp(pr);
            AddFeed("MOMENT OF TRUTH!", new Color(1f, 0.4f, 0.3f));
        }

        /// <summary>
        /// Did <paramref name="p"/> go down a hole (rather than off the edge)? With the border up a pit is the only way
        /// down; without it (the Moment of Truth), it's a hole if the ball went down over the lawn or by a pit.
        /// </summary>
        public bool DiedInHole(BallPlayer p)
        {
            if (!p || !valley || valley.BordersOn) return true;
            Vector3 at = p.lastPosition;
            bool overLawn = Mathf.Abs(at.x) < valley.HalfX && Mathf.Abs(at.z) < valley.HalfZ;   // (there only a pit goes down)
            return overLawn || valley.DistanceToNearestPit(at) < 1.5f;
        }

        /// <summary>Can drop (back) in now: has a revive left (or a free drop). In the Moment of Truth too: revives still count.</summary>
        public bool CanRevive(BallPlayer p) => p && p.CanDropIn;

        /// <summary>Trees and boulders the border reaches as it moves in (or that a closing hole gets to) crumble, letting out slimes.</summary>
        void CrumbleSwallowedProps()
        {
            foreach (var pr in props)
                if (pr && !pr.Broken && !pr.Waiting && !PropOnField(pr, 0f)) SwallowProp(pr);
        }

        /// <summary>
        /// <paramref name="pr"/> stands on the field as it is now: inside the border (the border touching it counts as
        /// out) and clear of the holes, with <paramref name="margin"/> metres to spare.
        /// </summary>
        bool PropOnField(FieldProp pr, float margin)
        {
            Vector3 p = pr.transform.position;
            float r = pr.baseRadius;
            if (Mathf.Abs(p.x) + r * 0.8f + margin > valley.HalfX || Mathf.Abs(p.z) + r * 0.8f + margin > valley.HalfZ) return false;
            foreach (var c in valley.caves)
            {
                float dx = p.x - c.mouth.x, dz = p.z - c.mouth.z;
                float reach = c.width * 0.5f + r * 0.6f + margin;
                if (dx * dx + dz * dz < reach * reach) return false;
            }
            return true;
        }

        /// <summary>
        /// Trees and boulders waiting for the field (a new round: it grows back, and they all stand somewhere new) pop up
        /// once it reaches them, as soon as no ball, slime or loose ball is in the way.
        /// </summary>
        void UpdatePropsShown()
        {
            if (!valley) return;
            foreach (var pr in props)
            {
                if (!pr || !pr.Waiting || pr.Broken || !PropOnField(pr, 1f) || SomethingUnder(pr)) continue;
                ClearUnder(pr);
                pr.Arrive();
            }
        }

        /// <summary>A ball, a slime or a loose ball is where <paramref name="pr"/> would pop up (it would get stuck inside).</summary>
        bool SomethingUnder(FieldProp pr)
        {
            Vector3 c = pr.transform.position;
            float r = pr.baseRadius + 0.5f;
            foreach (var p in players)
            {
                if (!p || !p.alive) continue;
                if (Near(p.Position, c, r + p.radius)) return true;
                foreach (var b in p.babies)
                    if (b && Near(b.transform.position, c, r + b.curR)) return true;
            }
            foreach (var s in slimes)
                if (s && Near(s.transform.position, c, r + tune.slimeRadius * s.size)) return true;
            foreach (var l in looseBalls)
                if (l && Near(l.transform.position, c, r + 1f)) return true;
            return false;
        }

        static bool Near(Vector3 a, Vector3 b, float d)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return dx * dx + dz * dz < d * d;
        }

        /// <summary>Berries and grass where <paramref name="pr"/> pops up are gone (they'd be stuck inside it).</summary>
        void ClearUnder(FieldProp pr)
        {
            Vector3 c = pr.transform.position;
            float r = pr.baseRadius + 0.5f;
            for (int i = pellets.Count - 1; i >= 0; i--)
                if (i < pellets.Count && pellets[i] && !pellets[i].Flying && Near(pellets[i].transform.position, c, r)) pellets[i].Eat();
            for (int i = grass.Count - 1; i >= 0; i--)
                if (i < grass.Count && grass[i] && Near(grass[i].transform.position, c, r)) EatGrass(grass[i]);
        }

        // ---------- a new layout of trees and boulders ----------

        /// <summary>
        /// A new round: every tree and boulder moves to a new spot picked from <paramref name="seed"/> (boulders anywhere on
        /// the field, trees near its edges, all well clear of the holes and of each other). Whole-number maths (BALLS: so
        /// every computer got the same layout). Each one hides until the field reaches its new spot, then pops up.
        /// 429R: the distances (in decimetres) scaled to this valley.
        /// </summary>
        public void ScatterProps(int seed)
        {
            matchSeed = seed;
            uint rng = (uint)seed;
            if (rng == 0) rng = 0x9E3779B9u;
            int hx = Mathf.RoundToInt(tune.halfX * 10f), hz = Mathf.RoundToInt(tune.halfZ * 10f);   // all in decimetres
            // 429R: BALLS' 220 / 100 / 50 / 160 decimetres were for its 200 x 100 m valley and 9 m holes.
            int HoleClear = Mathf.RoundToInt(HoleM(220f)), Inset = Mathf.RoundToInt(FieldM(100f)),
                EdgeMin = Mathf.Max(5, Mathf.RoundToInt(FieldM(50f))), EdgeMax = Mathf.Max(EdgeMin + 1, Mathf.RoundToInt(FieldM(160f)));
            const int Gap = 30, Tries = 60;
            placed.Clear();
            foreach (var pr in props)
            {
                if (!pr) continue;
                int r = Mathf.RoundToInt(pr.baseRadius * 10f);
                int x = 0, z = 0;
                bool any = false;
                for (int tries = 0; tries < Tries; tries++)
                {
                    int cx, cz;
                    if (pr.tree)
                    {
                        int inset = Range(ref rng, EdgeMin, EdgeMax + 1);
                        switch (Range(ref rng, 0, 4))
                        {
                            case 0: cx = Range(ref rng, -hx + EdgeMin, hx - EdgeMin + 1); cz = hz - inset; break;
                            case 1: cx = Range(ref rng, -hx + EdgeMin, hx - EdgeMin + 1); cz = -hz + inset; break;
                            case 2: cx = hx - inset; cz = Range(ref rng, -hz + EdgeMin, hz - EdgeMin + 1); break;
                            default: cx = -hx + inset; cz = Range(ref rng, -hz + EdgeMin, hz - EdgeMin + 1); break;
                        }
                    }
                    else
                    {
                        cx = Range(ref rng, -hx + Inset, hx - Inset + 1);
                        cz = Range(ref rng, -hz + Inset, hz - Inset + 1);
                    }
                    if (NearMouth(cx, cz, hx, hz, HoleClear)) continue;
                    if (!any) { x = cx; z = cz; any = true; }   // if the field is too crowded: clear of the holes at least
                    if (Crowded(cx, cz, r, Gap * (Tries - tries) / Tries)) continue;   // 3 m apart, less as it gets hard
                    x = cx;
                    z = cz;
                    break;
                }
                pr.MoveTo(new Vector3(x * 0.1f, 0f, z * 0.1f), Range(ref rng, 0, 360));
                placed.Add(new Vector3Int(x, z, r));
            }
        }

        readonly List<Vector3Int> placed = new List<Vector3Int>();   // (x, z, radius) in decimetres

        bool Crowded(int x, int z, int r, int gap)
        {
            foreach (var o in placed)
            {
                int dx = x - o.x, dz = z - o.y, d = r + o.z + gap;
                if (dx * dx + dz * dz < d * d) return true;
            }
            return false;
        }

        /// <summary>Within <paramref name="clear"/> of a hole's mouth at full size (corners and the middles of the long sides).</summary>
        static bool NearMouth(int x, int z, int hx, int hz, int clear)
        {
            for (int sz = -1; sz <= 1; sz += 2)
                for (int sx = -1; sx <= 1; sx++)
                {
                    int dx = x - sx * hx, dz = z - sz * hz;
                    if (dx * dx + dz * dz < clear * clear) return true;
                }
            return false;
        }

        /// <summary>Xorshift: the same numbers from the same seed.</summary>
        static int Range(ref uint s, int min, int max)
        {
            s ^= s << 13;
            s ^= s >> 17;
            s ^= s << 5;
            return max <= min ? min : min + (int)(s % (uint)(max - min));
        }

        /// <summary>Makes <paramref name="root"/> a breakable tree / boulder.</summary>
        FieldProp AddProp(Transform root, Transform pivot, bool tree, float size)
        {
            var pr = root.gameObject.AddComponent<FieldProp>();
            pr.index = props.Count;
            pr.tree = tree;
            pr.maxHp = pr.hp = (tree ? tune.treeHealth : tune.rockHealth) * size;
            pr.shakeHp = (tree ? 8f : 12f) * size;   // the wobble a hit gives stays the same whatever the hit points
            pr.food = tree ? tune.treeFood : tune.rockFood;
            if (tree) pr.hitCandiesLeft = treeHitCandies;   // 429R gate 3: candy trees
            pr.chipColor = tree ? new Color(0.45f, 0.3f, 0.16f) : new Color(0.62f, 0.6f, 0.56f);
            pr.basePos = root.position;
            pr.baseScale = root.localScale;
            pr.jiggle = pivot.gameObject.AddComponent<PropJiggle>();
            if (tree)
            {
                pr.jiggle.squash = 0.1f;   // trees sway more than they squash
                pr.jiggle.tilt = 12f;
            }
            float reach = 0f;
            foreach (var rend in root.GetComponentsInChildren<Renderer>())
            {
                Bounds b = rend.bounds;
                reach = Mathf.Max(reach, Mathf.Abs(b.min.x - root.position.x), Mathf.Abs(b.max.x - root.position.x),
                    Mathf.Abs(b.min.z - root.position.z), Mathf.Abs(b.max.z - root.position.z));
            }
            pr.baseRadius = Mathf.Clamp(reach, 0.5f, 6f);
            foreach (var hz in root.GetComponentsInChildren<Hazard>(true)) hz.prop = pr;
            props.Add(pr);
            return pr;
        }

        /// <summary>A berry colour (for food bursting out of broken things).</summary>
        public Color RandomBerryColor() => RandomPelletColor();

        /// <summary>Drops a player back in if they have a revive left (uses it up). The first drop of a round is free.</summary>
        public bool TryRevive(BallPlayer p)
        {
            if (!p || p.alive || !CanRevive(p)) return false;
            if (p.freeDrop) p.freeDrop = false;
            else p.revives--;
            SpawnPlayer(p);
            for (int i = respawnQueue.Count - 1; i >= 0; i--)   // back in another way (Moment of Truth): its queued drop-in is spent
                if (respawnQueue[i].Key == p) respawnQueue.RemoveAt(i);
            return true;
        }

        void QueueRevive(BallPlayer bot, float delay)
        {
            if (bot.alive || !CanRevive(bot)) return;
            foreach (var kv in respawnQueue)
                if (kv.Key == bot) return;
            respawnQueue.Add(new KeyValuePair<BallPlayer, float>(bot, Time.time + delay));
        }

        void GiftRevives(BallPlayer faller)
        {
            int n = tune.selfPocketGift;
            foreach (var o in players)
            {
                if (o == faller && !tune.selfPocketGiftsFaller) continue;
                o.revives += n;
                if (o != faller && !o.isHuman && !o.alive) QueueRevive(o, Random.Range(1.5f, 3.5f));   // a bot that was out comes back
            }
            string who = tune.selfPocketGiftsFaller ? "everyone" : "everyone else";
            string plural = n > 1 ? "s" : "";
            AddFeed($"{faller.displayName} fell in by themself: {who} gets +{n} revive{plural}!", new Color(0.6f, 1f, 0.6f));
        }

        /// <summary>Berries and grass left outside the shrinking border are gone.</summary>
        void CullOutside()
        {
            CrumbleSwallowedProps();
            float hx = valley.HalfX - 0.6f, hz = valley.HalfZ - 0.6f;
            for (int i = pellets.Count - 1; i >= 0; i--)
            {
                if (i >= pellets.Count) continue;
                var pl = pellets[i];
                if (!pl) { pellets.RemoveAt(i); continue; }
                Vector3 q = pl.transform.position;
                if (!pl.Flying && (Mathf.Abs(q.x) > hx || Mathf.Abs(q.z) > hz)) pl.Eat();
            }
            for (int i = grass.Count - 1; i >= 0; i--)
            {
                if (i >= grass.Count) continue;
                var g = grass[i];
                if (!g) { grass.RemoveAt(i); continue; }
                Vector3 q = g.transform.position;
                if (Mathf.Abs(q.x) > hx || Mathf.Abs(q.z) > hz) EatGrass(g);
            }
        }

        /// <summary>A kill-feed line (429R: no HUD shows it yet; written to the console with Log Feed).</summary>
        public void AddFeed(string text, Color c)
        {
            var line = new FeedLine { text = text, color = c, time = Time.time };
            feed.Add(line);
            if (feed.Count > 6) feed.RemoveAt(0);
            if (logFeed && (Application.isEditor || Debug.isDebugBuild)) Debug.Log("[BallValley] " + text, this);   // 429R: not in release builds
        }

        // ---------- spawning helpers ----------

        public static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        public Vector3 RandomFloorPoint(float inset)
        {
            float hx = Mathf.Max(valley.SafeHalfX - inset, 1f), hz = Mathf.Max(valley.SafeHalfZ - inset, 1f);
            return new Vector3(Random.Range(-hx, hx), 0f, Random.Range(-hz, hz));
        }

        public Vector3 FindClearPoint(float inset, float clearance, float caveClear)
        {
            for (int tries = 0; tries < 40; tries++)
            {
                Vector3 p = RandomFloorPoint(inset);
                if (valley.DistanceToNearestCave(p) < caveClear) continue;
                if (Physics.CheckSphere(p + Vector3.up * (clearance + 0.3f), clearance, QueryMask, QueryTriggerInteraction.Ignore)) continue;   // 429R: mask
                return p;
            }
            return RandomFloorPoint(inset);
        }

        /// <summary>A spot near the field's edge for a tree (429R: BALLS' 5 - 16 m in from the edge and 22 m from the holes, scaled).</summary>
        Vector3 EdgePoint()
        {
            float hx = tune.halfX, hz = tune.halfZ;
            float end = FieldM(5f), caveClear = HoleM(22f);
            for (int tries = 0; tries < 40; tries++)
            {
                float inset = Random.Range(FieldM(5f), FieldM(16f));
                Vector3 p;
                switch (Random.Range(0, 4))
                {
                    case 0: p = new Vector3(Random.Range(-hx + end, hx - end), 0f, hz - inset); break;
                    case 1: p = new Vector3(Random.Range(-hx + end, hx - end), 0f, -hz + inset); break;
                    case 2: p = new Vector3(hx - inset, 0f, Random.Range(-hz + end, hz - end)); break;
                    default: p = new Vector3(-hx + inset, 0f, Random.Range(-hz + end, hz - end)); break;
                }
                if (valley.DistanceToNearestCave(p) < caveClear) continue;
                if (Physics.CheckSphere(p + Vector3.up * 2.3f, 2f, QueryMask, QueryTriggerInteraction.Ignore)) continue;   // 429R: mask
                return p;
            }
            return FindClearPoint(FieldM(10f), 2f, caveClear);
        }

        Color RandomPelletColor() => PelletColors[Random.Range(0, PelletColors.Length)];

        /// <summary>429R: a random one of Candy Prefabs (null: none set, a berry then).</summary>
        GameObject PickCandy()
        {
            if (candyPrefabs == null || candyPrefabs.Length == 0) return null;
            for (int tries = 0; tries < 4; tries++)
            {
                var c = candyPrefabs[Random.Range(0, candyPrefabs.Length)];
                if (c) return c;
            }
            foreach (var c in candyPrefabs) if (c) return c;
            return null;
        }

        int PelletCap => Mathf.RoundToInt(tune.maxPellets * Mathf.Max(0.1f, tune.foodBoost));

        /// <summary>Most berries on the field now: with Berries Follow Field, the same density at every size.</summary>
        int BerryCap => tune.berriesFollowField
            ? Mathf.Max(Mathf.Min(tune.minBerries, PelletCap), Mathf.RoundToInt(PelletCap * mapScale * mapScale))
            : PelletCap;

        /// <summary>How far the valley has shrunk: 0 at full size, 1 at its smallest.</summary>
        float Shrunk01 => Mathf.InverseLerp(1f, Mathf.Clamp(tune.minMapScale, 0.05f, 1f), mapScale);

        /// <summary>Most slimes at once now: more as the valley shrinks.</summary>
        int SlimeCap => Mathf.RoundToInt(Mathf.Lerp(tune.slimeCount, tune.slimeCountSmallest, Shrunk01));

        static readonly string[] BoulderModels = { "Map/Boulder_Round", "Map/Boulder_Tall", "Map/Boulder_Slab" };
        const string SlimeRockModel = "Map/Boulder_SlimeRock";

        /// <summary>A boulder from BALLS_Map.blend (some are slime rocks with a hole slimes come out of), or the code-built rock if the models are missing.</summary>
        void SpawnRock(Vector3 p)
        {
            if (KitOn && kitRocks != null && kitRocks.Length > 0) { SpawnKitRock(p); return; }   // 429R: the kit's rocks
            if (!ModelLib.HasPath(SlimeRockModel)) { SpawnCodeRock(p); return; }
            bool slimeRock = Random.value < tune.slimeRockShare;
            string model = slimeRock ? SlimeRockModel : BoulderModels[Random.Range(0, BoulderModels.Length)];
            float s = Random.Range(1.0f, 1.7f);
            float yaw = Random.Range(0f, 360f);
            var root = NewWorldObject(slimeRock ? "Slime Rock" : "Rock");
            root.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, yaw, 0f));
            root.transform.localScale = Vector3.one * s;
            // The model wobbles on a pivot; its colliders stay still on the root (a jiggle mustn't shove balls around).
            var pivot = new GameObject("Jiggle").transform;
            pivot.SetParent(root.transform, false);
            var inst = ModelLib.Place(model, pivot, Gfx.PaletteMaterial());
            foreach (var mf in inst.GetComponentsInChildren<MeshFilter>())
            {
                var col = new GameObject("Collider").transform;
                col.SetParent(root.transform, false);
                Matrix4x4 m = root.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                col.localPosition = m.GetColumn(3);
                col.localRotation = m.rotation;
                col.localScale = m.lossyScale;
                var mc = col.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                mc.sharedMaterial = Gfx.RockPhys;
                col.gameObject.AddComponent<Hazard>();
                fieldRocks.Add(mc);
            }
            if (slimeRock)
                foreach (var t in inst.GetComponentsInChildren<Transform>())
                    if (t.name.StartsWith("SlimeExit")) { slimeExits.Add(t); break; }
            AddProp(root.transform, pivot, false, s);
        }

        /// <summary>429R: a kit rock (a mossy boulder or a gravestone): its own mesh is its collider, it wobbles on a pivot.</summary>
        void SpawnKitRock(Vector3 p)
        {
            var piece = kitRocks[Random.Range(0, kitRocks.Length)];
            if (!piece) { SpawnCodeRock(p); return; }
            float s = Random.Range(0.85f, 1.25f);
            var root = NewWorldObject("Rock");
            root.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            root.transform.localScale = Vector3.one * s;
            var pivot = new GameObject("Jiggle").transform;
            pivot.SetParent(root.transform, false);
            var inst = PlaceKit(piece, pivot);
            foreach (var mf in inst.GetComponentsInChildren<MeshFilter>())
            {
                if (!mf.sharedMesh) continue;
                var col = new GameObject("Collider").transform;
                col.SetParent(root.transform, false);
                Matrix4x4 m = root.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                col.localPosition = m.GetColumn(3);
                col.localRotation = m.rotation;
                col.localScale = m.lossyScale;
                var mc = col.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                mc.sharedMaterial = Gfx.RockPhys;
                col.gameObject.AddComponent<Hazard>();
                fieldRocks.Add(mc);
            }
            AddProp(root.transform, pivot, false, s);
        }

        /// <summary>
        /// 429R gate 3 (2026-11-23): a jello volcano (a Kit Volcanoes piece, else a code-built jelly cone): it stands like a
        /// boulder (its own mesh is its collider, it wobbles on a pivot), never breaks and spits candy (Jello Volcano).
        /// </summary>
        void SpawnVolcano(Vector3 p)
        {
            var piece = kitVolcanoes != null && kitVolcanoes.Length > 0 ? kitVolcanoes[Random.Range(0, kitVolcanoes.Length)] : null;
            float s = Random.Range(0.9f, 1.15f);
            var root = NewWorldObject("Jello Volcano");
            root.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            root.transform.localScale = Vector3.one * s;
            var pivot = new GameObject("Jiggle").transform;
            pivot.SetParent(root.transform, false);
            var inst = piece ? PlaceKit(piece, pivot)
                : Gfx.Visual("Volcano", Gfx.Cone, Gfx.Lit(new Color(0.8f, 0.35f, 0.85f), 0.85f), pivot,
                    new Vector3(0f, 0.9f, 0f), new Vector3(4f, 1.8f, 4f), Quaternion.identity);
            foreach (var mf in inst.GetComponentsInChildren<MeshFilter>())
            {
                if (!mf.sharedMesh) continue;
                var col = new GameObject("Collider").transform;
                col.SetParent(root.transform, false);
                Matrix4x4 m = root.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                col.localPosition = m.GetColumn(3);
                col.localRotation = m.rotation;
                col.localScale = m.lossyScale;
                var mc = col.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                mc.sharedMaterial = Gfx.RockPhys;
                col.gameObject.AddComponent<Hazard>();
                fieldRocks.Add(mc);
            }
            var pr = AddProp(root.transform, pivot, false, s);
            pr.maxHp = pr.hp = 1e9f;   // never breaks
            pr.food = 0;
            pr.chipColor = new Color(0.9f, 0.45f, 0.85f);
            pr.jiggle.squash = 0.22f;   // jelly
            root.AddComponent<JelloVolcano>().Init(this, pr);
        }

        void SpawnCodeRock(Vector3 p)
        {
            var go = NewWorldObject("Rock");
            go.transform.position = p;
            float s = Random.Range(2.2f, 6f);
            go.transform.localScale = new Vector3(s * Random.Range(0.8f, 1.3f), s * Random.Range(0.45f, 0.8f), s * Random.Range(0.8f, 1.3f));
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            var mesh = Gfx.RockMesh(Random.Range(0, 4));
            float tone = Random.Range(0.42f, 0.58f);
            var pivot = Gfx.Visual("Jiggle", mesh, Gfx.Lit(new Color(tone, tone * 0.95f, tone * 0.9f), 0.15f), go.transform,
                Vector3.zero, Vector3.one, Quaternion.identity);
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
            mc.sharedMaterial = Gfx.RockPhys;
            go.AddComponent<Hazard>();
            fieldRocks.Add(mc);
            AddProp(go.transform, pivot, false, s / 3f);
        }

        void SpawnTree(Vector3 p)
        {
            var root = NewWorldObject("Tree");
            root.transform.position = p;
            float s = Random.Range(0.8f, 1.25f);
            var cap = root.AddComponent<CapsuleCollider>();
            cap.center = new Vector3(0f, 2f * s, 0f);
            cap.radius = 0.6f * s;
            cap.height = 4f * s;
            cap.sharedMaterial = Gfx.RockPhys;
            root.AddComponent<Hazard>().wood = true;   // trees sound like wood
            var pivot = new GameObject("Jiggle").transform;   // the tree sways on this; its trunk collider stays put
            pivot.SetParent(root.transform, false);
            // 429R: the kit's tree (a jelly-bubble tree on a licorice trunk), one picked per tree.
            if (KitOn && kitTrees != null && kitTrees.Length > 0)
            {
                var piece = kitTrees[Random.Range(0, kitTrees.Length)];
                if (piece)
                {
                    var t = PlaceKit(piece, pivot);
                    t.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                    AddProp(root.transform, pivot, true, s);
                    return;
                }
            }
            // The pine from BALLS_Map.blend.
            string pineModel = Random.value < 0.6f ? "Map/Pine_Tall" : "Map/Pine_Short";
            float yaw = Random.Range(0f, 360f);
            if (ModelLib.HasPath(pineModel))
            {
                var pine = new GameObject("Pine").transform;
                pine.SetParent(pivot, false);
                pine.localRotation = Quaternion.Euler(0f, yaw, 0f);
                pine.localScale = Vector3.one * (s * 1.15f);
                ModelLib.Place(pineModel, pine, Gfx.PaletteMaterial());
                AddProp(root.transform, pivot, true, s);
                return;
            }
            Gfx.Visual("Trunk", Gfx.Cylinder, Gfx.Lit(new Color(0.35f, 0.22f, 0.12f), 0.1f), pivot,
                new Vector3(0f, 1.2f * s, 0f), new Vector3(0.7f * s, 1.2f * s, 0.7f * s), Quaternion.identity);
            var leaf = Gfx.Lit(new Color(0.10f, Random.Range(0.3f, 0.4f), 0.16f), 0.1f);
            for (int k = 0; k < 3; k++)
            {
                float w = (3.6f - k * 0.9f) * s;
                Gfx.Visual("Leaves", Gfx.Cone, leaf, pivot, new Vector3(0f, (2f + k * 1.4f) * s, 0f),
                    new Vector3(w, 2.6f * s, w), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            }
            AddProp(root.transform, pivot, true, s);
        }

        // ---------- grass ----------

        void SpawnGrass()
        {
            Vector3 p = FindClearPoint(FieldM(8f), 1f, HoleM(15f));   // 429R: scaled
            var g = BuildGrass(p, (byte)Random.Range(0, GrassTuft.Models.Length), Random.Range(0f, 360f), Random.Range(0.8f, 1.25f));
            grass.Add(g);
        }

        /// <summary>A grass tuft (no collider: balls roll through it).</summary>
        public GrassTuft BuildGrass(Vector3 pos, byte variant, float yaw, float scale)
        {
            var go = NewWorldObject("Grass");
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = Vector3.one * scale;
            var g = go.AddComponent<GrassTuft>();
            g.variant = variant;
            g.vis = GrassTuft.BuildVisual(go.transform, variant, tune.grassHeight);
            Gfx.NoShadows(go.transform);   // 429R: always (BALLS: phones)
            return g;
        }

        /// <summary>A slime ate this tuft.</summary>
        public void EatGrass(GrassTuft g)
        {
            if (!g) return;
            grass.Remove(g);
            Gfx.Puff(g.transform.position + Vector3.up * 0.4f, new Color(0.4f, 0.75f, 0.3f, 0.8f), 8, 0.8f, 3f);
            Destroy(g.gameObject);
        }

        public Pellet SpawnPellet(Vector3 pos, Vector3 vel, Color c)
        {
            var p = BuildPellet(pos, vel, c);
            p.value = tune.pelletValue;
            pellets.Add(p);
            return p;
        }

        /// <summary>A berry (429R: a random candy, see Candy Prefabs).</summary>
        public Pellet BuildPellet(Vector3 pos, Vector3 vel, Color c)
        {
            var candy = PickCandy();
            if (candy)
            {
                // 429R: the candy itself is the field food (Pellet.SetupCandy): hovering where the berry would be.
                var made = Instantiate(candy, new Vector3(pos.x, Mathf.Max(pos.y, Pellet.CandyHover), pos.z),
                    Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), world);
                made.name = candy.name;
                var cp = made.AddComponent<Pellet>();
                cp.color = c;
                cp.SetupCandy(vel);
                return cp;
            }
            var go = NewWorldObject("Pellet");
            go.transform.position = pos;
            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.45f;
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            SetupBody(rb);   // 429R: no trigger touches with 429R's Ground box
            var vis = Gfx.Visual("Berry", Gfx.TinySphere, Gfx.Lit(c, 0.7f, c * 0.9f), go.transform, Vector3.zero, Vector3.one * 0.55f, Quaternion.identity);
            var p = go.AddComponent<Pellet>();
            p.color = c;
            p.Setup(vis, vel);
            Gfx.NoShadows(go.transform);   // 429R: always (BALLS: phones)
            return p;
        }

        /// <summary>
        /// Every loose ball (shed from a tail, or left behind when its owner got pocketed) is white, like a cue ball:
        /// white means food, anyone's to eat.
        /// </summary>
        public static readonly Color LooseColor = new Color(0.97f, 0.97f, 0.95f);

        static Material LooseMaterial(Color c) => Gfx.Lit(c, 0.85f, c * 0.12f);

        /// <summary>A ball comes off <paramref name="from"/>'s tail (or is left when they're pocketed): a white loose ball, food for anyone.</summary>
        public void SpawnLooseBall(Vector3 pos, Vector3 vel, float r, BallPlayer from)
        {
            Color c = LooseColor;
            looseBalls.RemoveAll(l => !l);   // 429R: one taken away by something else (the purple hole, a clean-up) leaves a gap
            if (looseBalls.Count > tune.maxLooseBalls) looseBalls[0].Expire();   // 429R: Max Loose Balls (BALLS: 400, phones 150)
            var go = NewWorldObject("Loose Ball");
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * (r * 2f);
            go.AddComponent<MeshFilter>().sharedMesh = Gfx.BallSphere;
            go.AddComponent<MeshRenderer>().sharedMaterial = LooseMaterial(c);
            var col = go.AddComponent<SphereCollider>();
            col.radius = 0.5f;
            col.sharedMaterial = Gfx.BallPhys;
            float sizeMul = r / tune.headRadius;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = tune.babyMass * sizeMul * sizeMul;
            rb.linearDamping = 0.7f;
            rb.angularDamping = 0.5f;
            rb.maxDepenetrationVelocity = 4f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;   // 429R: Quest, as BALLS' phones
            rb.linearVelocity = vel;
            SetupBody(rb);   // 429R
            Gfx.NoShadows(go.transform);   // 429R: always (BALLS: phones)
            var lb = go.AddComponent<LooseBall>();
            lb.rb = rb;
            lb.color = c;
            lb.physId = BallPhysics.Register(col);
            lb.value = Mathf.Max(1, Mathf.RoundToInt(tune.looseBallValue * sizeMul));
            lb.from = from;
            lb.eatableAt = Time.time + 0.4f;
            lb.ownerEatableAt = Time.time + 1.5f;
            lb.dieAt = Time.time + Random.Range(60f, 80f);
            looseBalls.Add(lb);
        }

        // ---------- slimes ----------

        /// <summary>
        /// A new slime: out of a slime rock (or the side of a rock, if the map has no slime rocks); with none left to come
        /// out of (the border swallowed them, or the field is too small to clear the holes), it climbs up over the edge.
        /// </summary>
        void SpawnSlime()
        {
            bool done = slimeExits.Count > 0 ? SpawnSlimeFromHole() : SpawnSlimeFromRock();
            if (!done) SpawnSlimeLate();
        }

        /// <summary>No rock to come out of: over the edge, or (a tiny field ringed by holes) up out of the grass.</summary>
        void SpawnSlimeLate()
        {
            if (!SpawnSlimeFromEdge()) SpawnSlimeFromGround();
        }

        /// <summary>A slime squeezes out of the side of a random rock on the field and hops away from it.</summary>
        bool SpawnSlimeFromRock()
        {
            if (fieldRocks.Count == 0) return false;
            float r = tune.slimeRadius;
            for (int tries = 0; tries < 12; tries++)
            {
                var rock = fieldRocks[Random.Range(0, fieldRocks.Count)];
                if (!rock || !rock.gameObject.activeInHierarchy) continue;   // broken
                Bounds b = rock.bounds;
                float a = Random.value * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 p = new Vector3(b.center.x, r, b.center.z) + dir * (Mathf.Max(b.extents.x, b.extents.z) * 0.85f + r + 0.2f);
                if (Mathf.Abs(p.x) > valley.HalfX - FieldM(6f) || Mathf.Abs(p.z) > valley.HalfZ - FieldM(6f)) continue;   // 429R: scaled
                if (valley.DistanceToNearestCave(p) < HoleM(20f)) continue;
                if (Physics.CheckSphere(p + Vector3.up * 0.1f, r * 0.9f, QueryMask, QueryTriggerInteraction.Ignore)) continue;   // 429R: mask
                AddSlime(p, dir, 5f);   // pops out with a hop
                var tone = rock.GetComponent<MeshRenderer>();
                Gfx.Puff(p, tone ? tone.sharedMaterial.GetColor("_BaseColor") : Color.grey, 12, 1.2f, 3f);
                return true;
            }
            return false;
        }

        /// <summary>A slime squeezes out of the hole in a random slime rock, facing away from the rock.</summary>
        bool SpawnSlimeFromHole()
        {
            float r = tune.slimeRadius;
            for (int tries = 0; tries < 12; tries++)
            {
                var exit = slimeExits[Random.Range(0, slimeExits.Count)];
                if (!exit || !exit.gameObject.activeInHierarchy) continue;   // its rock is broken
                Transform rock = exit;
                while (rock.parent && rock.parent != world) rock = rock.parent;   // the rock's root (holds its yaw and scale)
                Vector3 dir = Flat(exit.position - rock.position);
                dir = dir.sqrMagnitude > 0.01f ? dir.normalized : rock.forward;
                Vector3 p = Flat(exit.position) + dir * (r + 0.3f) + Vector3.up * r;
                if (Mathf.Abs(p.x) > valley.HalfX - FieldM(5f) || Mathf.Abs(p.z) > valley.HalfZ - FieldM(5f)) continue;   // 429R: scaled
                if (valley.DistanceToNearestCave(p) < HoleM(18f)) continue;
                // (no clearance test: the rubble in front of the hole would always fail it; physics nudges it free)
                AddSlime(p, dir, 4f);   // squeezes out and hops away
                Gfx.Puff(Flat(exit.position) + Vector3.up * 0.4f, new Color(0.62f, 0.59f, 0.55f), 12, 1.0f, 3f);
                return true;
            }
            return false;
        }

        /// <summary>A slime climbs up out of the chasm over the field's edge (inside the size it's shrinking to, clear of the holes) and hops in.</summary>
        bool SpawnSlimeFromEdge()
        {
            float hx = valley.SafeHalfX, hz = valley.SafeHalfZ, r = tune.slimeRadius;
            for (int tries = 0; tries < 16; tries++)
            {
                bool longSide = Random.value < hx / (hx + hz);   // the long sides more often: there's more of them
                float side = Random.value < 0.5f ? -1f : 1f;
                Vector3 edge = longSide ? new Vector3(Random.Range(-hx, hx), 0f, side * hz) : new Vector3(side * hx, 0f, Random.Range(-hz, hz));
                Vector3 inward = longSide ? new Vector3(0f, 0f, -side) : new Vector3(-side, 0f, 0f);
                Vector3 p = edge + inward * 1.5f + Vector3.up * r;
                if (!SlimeSpotClear(p)) continue;
                AddSlime(p, inward, 5.5f);
                Gfx.Puff(edge + Vector3.up * 0.2f, new Color(0.45f, 0.4f, 0.35f, 0.8f), 10, 1f, 3f);
                return true;
            }
            return false;
        }

        /// <summary>
        /// A tree or boulder the border or a hole swallowed crumbles and lets its slimes out (instead of berries): they hop
        /// off it onto what's left of the field.
        /// </summary>
        void SwallowProp(FieldProp pr)
        {
            pr.Break(false);
            Vector3 c = Flat(pr.transform.position);
            float hx = valley.SafeHalfX - 1.5f, hz = valley.SafeHalfZ - 1.5f, r = tune.slimeRadius;
            for (int i = 0; i < tune.swallowedPropSlimes && slimes.Count < SlimeCap; i++)
            {
                bool done = false;
                for (int tries = 0; tries < 10 && !done; tries++)
                {
                    Vector2 jitter = Random.insideUnitCircle * (pr.baseRadius + 1f);
                    Vector3 p = new Vector3(Mathf.Clamp(c.x + jitter.x, -hx, hx), r, Mathf.Clamp(c.z + jitter.y, -hz, hz));
                    if (!SlimeSpotClear(p)) continue;
                    Vector3 dir = Flat(p - c);
                    dir = dir.sqrMagnitude > 0.01f ? dir.normalized : -Flat(p).normalized;
                    AddSlime(p, dir, 5f);
                    done = true;
                }
                if (!done) SpawnSlimeLate();
            }
        }

        /// <summary>A slime pops up out of the grass somewhere clear (when the holes leave no edge to climb over).</summary>
        bool SpawnSlimeFromGround()
        {
            float r = tune.slimeRadius;
            for (int tries = 0; tries < 24; tries++)
            {
                Vector3 p = RandomFloorPoint(1.5f) + Vector3.up * r;
                if (!SlimeSpotClear(p)) continue;
                float a = Random.value * Mathf.PI * 2f;
                AddSlime(p, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)), 6f);
                Gfx.Puff(Flat(p) + Vector3.up * 0.2f, new Color(0.4f, 0.75f, 0.3f, 0.8f), 10, 1f, 3f);
                return true;
            }
            return false;
        }

        /// <summary>
        /// On the field (inside the size it's shrinking to), not right in front of a hole and clear of anything solid:
        /// somewhere a slime can appear. A hole is a gap in the border, so only the ground near the edge in front of its gap is risky.
        /// </summary>
        bool SlimeSpotClear(Vector3 p)
        {
            float hx = valley.SafeHalfX, hz = valley.SafeHalfZ;
            if (Mathf.Abs(p.x) > hx - 1f || Mathf.Abs(p.z) > hz - 1f) return false;
            const float near = 3.5f;
            foreach (var c in valley.caves)
            {
                float gap = c.EdgeGap + 1.5f;
                if (hz - Mathf.Abs(p.z) < near && Mathf.Sign(p.z) == c.side.z && Mathf.Abs(p.x - c.mouth.x) < gap) return false;
                if (c.corner && hx - Mathf.Abs(p.x) < near && Mathf.Sign(p.x) == c.side.x && Mathf.Abs(p.z - c.mouth.z) < gap) return false;
            }
            return !Physics.CheckSphere(p + Vector3.up * 0.1f, tune.slimeRadius * 0.9f, QueryMask, QueryTriggerInteraction.Ignore);   // 429R: mask
        }

        Slime AddSlime(Vector3 p, Vector3 dir, float hopUp)
        {
            var s = BuildSlime(p, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, (byte)Random.Range(0, Slime.LookCount));
            s.rb.linearVelocity = dir * 4f + Vector3.up * hopUp;
            s.Trigger(SlimeAnim.Clip.Wake);   // shakes itself off and cackles, like Fat Pumpkin waking
            slimes.Add(s);
            return s;
        }

        /// <summary>A slime (429R: always with its physics; BALLS' network copies had none).</summary>
        public Slime BuildSlime(Vector3 pos, float yaw, byte look)
        {
            var go = NewWorldObject("Slime");
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var s = go.AddComponent<Slime>();
            s.look = look;
            s.vis = Slime.BuildVisual(go.transform, look, out s.anim);
            s.SetupLook();
            Gfx.NoShadows(go.transform);   // 429R: always (BALLS: phones)
            s.anim.velocity = () => s.Velocity;
            s.col = go.AddComponent<SphereCollider>();
            s.col.sharedMaterial = Gfx.BallPhys;
            s.rb = go.AddComponent<Rigidbody>();
            s.rb.useGravity = true;
            s.rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            s.rb.interpolation = RigidbodyInterpolation.Interpolate;
            s.rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            SetupBody(s.rb);   // 429R
            s.SetSize(s.rainbow ? Slime.RainbowSize : 1f);
            return s;
        }

        /// <summary>The Rainbow Slime, if one is out (there's never more than one).</summary>
        public Slime RainbowSlime
        {
            get
            {
                foreach (var s in slimes) if (s && s.rainbow) return s;
                return null;
            }
        }

        /// <summary>A long reign ended in <paramref name="cave"/>, and a Rainbow Slime jumps out of it, up over the lip and onto the lawn. One at a time.</summary>
        void SpawnRainbowSlime(Cave cave)
        {
            if (cave == null || RainbowSlime) return;
            Vector3 pit = cave.mouth + cave.openingOffset;
            pit.y = 0f;
            Vector3 inward = -cave.outward;
            inward.y = 0f;
            inward = inward.sqrMagnitude > 0.01f ? inward.normalized : Flat(-pit).normalized;
            float r = tune.slimeRadius * Slime.RainbowSize;
            Vector3 p = pit + Vector3.up * (r + 0.6f);
            var s = BuildSlime(p, Mathf.Atan2(inward.x, inward.z) * Mathf.Rad2Deg, Slime.RainbowLook);
            // One jump that lands it a few metres past the lip, whatever the pit's size.
            float g = -Physics.gravity.y * (1f + tune.extraGravity), up = 9f;
            float reach = (cave.openingRadius > 0f ? cave.openingRadius : cave.width * 0.5f) + 4f;
            s.rb.linearVelocity = inward * (reach / (2f * up / g)) + Vector3.up * up;
            s.Trigger(SlimeAnim.Clip.Wake);
            slimes.Add(s);
            for (int i = 0; i < 6; i++) Gfx.Sparks(p, Slime.Rainbow(i / 6f), 6, 7f);
            Sfx.Play(Sfx.Sound.SlimeLaugh, p);
            AddFeed("A Rainbow Slime jumped out of the cave! Catch it to be invincible!", new Color(1f, 0.6f, 0.92f));
        }

        /// <summary><paramref name="p"/> caught the Rainbow Slime (its food is already in).</summary>
        public void CaughtRainbow(BallPlayer p)
        {
            p.MakeInvincible(tune.rainbowInvincibleSeconds);
            AddFeed($"{p.displayName} caught the Rainbow Slime: invincible for {tune.rainbowInvincibleSeconds:0} seconds!", new Color(1f, 0.6f, 0.92f));
            Sfx.Play(Sfx.Sound.FireworkBang, p.Position, 0.6f);
        }

        // ---------- queries for bots ----------

        public Component NearestFood(BallPlayer me, Vector3 pos, float maxDist)
        {
            Component best = null;
            float bestD = maxDist * maxDist;
            foreach (var p in pellets)
            {
                if (!p) continue;   // 429R: a candy the game took away this frame
                Vector3 d = Flat(p.transform.position - pos);
                float s = d.sqrMagnitude;
                if (s < bestD) { bestD = s; best = p; }
            }
            foreach (var l in looseBalls)
            {
                if (!l || !l.CanEat(me)) continue;   // 429R: (one taken away by something else)
                Vector3 d = Flat(l.transform.position - pos);
                float s = d.sqrMagnitude * 0.6f;   // loose balls are worth more
                if (s < bestD) { bestD = s; best = l; }
            }
            foreach (var sl in slimes)
            {
                if (!sl) continue;
                // A whole burst of berries, more the fatter it is: worth the chase. The Rainbow Slime: worth a long one.
                float s = Flat(sl.transform.position - pos).sqrMagnitude * (sl.rainbow ? 0.08f : 0.6f / sl.size);
                if (s < bestD) { bestD = s; best = sl; }
            }
            return best;
        }

        public Destructible NearestDestructible(Vector3 pos, float maxDist)
        {
            Destructible best = null;
            float bestD = maxDist * maxDist;
            foreach (var d in destructibles)
            {
                float s = Flat(d.transform.position - pos).sqrMagnitude;
                if (s < bestD) { bestD = s; best = d; }
            }
            return best;
        }

        // ---------- 429R: the look (sun, ambient light, no fog, the sky) and giving it back ----------

        Light sun;
        Transform sky;
        Mesh skyMesh;
        Material skyStandIn;
        Camera head, farCam;
        float farWas = -1f;
        bool lookSet, fogWas;
        Light sunWas;
        AmbientMode ambientModeWas;
        Color ambientWas;
        SphericalHarmonicsL2 probeWas;

        /// <summary>
        /// 429R: daylight for the valley while the stage runs (BALLS' SetupRendering without its camera and render-pipeline
        /// changes): its own sun, BALLS' flat ambient light, the fog off (429R's house fog would swallow the valley) and the
        /// sky sphere. <see cref="RestoreLook"/> gives it all back.
        /// </summary>
        void SetupLook()
        {
            fogWas = RenderSettings.fog;
            sunWas = RenderSettings.sun;
            ambientModeWas = RenderSettings.ambientMode;
            ambientWas = RenderSettings.ambientLight;
            probeWas = RenderSettings.ambientProbe;
            lookSet = true;

            sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(transform, false);
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(sunAngles);
            sun.color = sunColour;
            sun.intensity = sunIntensity;
            sun.shadows = sunShadows;
            sun.shadowStrength = 0.75f;
            RenderSettings.sun = sun;

            SetAmbient();
            RenderSettings.fog = false;

            // The head sees as far as the valley needs (429R's camera: 40 m), as the opening cutscene does.
            farCam = Camera.main;
            if (farCam)
            {
                farWas = farCam.farClipPlane;
                farCam.farClipPlane = Mathf.Max(farWas, seeingDistance);
            }
            BuildSky();
        }

        /// <summary>
        /// 429R: something else changed the look while the valley runs (play test 2026-11-16: the opening cutscene ending
        /// put the house fog, its ambient light and the 40 m far plane back over the valley: black sky, fog). What it set is
        /// what's given back when the valley goes, and the valley's look goes on again. Changing Ambient while it runs
        /// shows at once.
        /// </summary>
        void KeepLook()
        {
            if (!lookSet) return;
            if (RenderSettings.fog)
            {
                fogWas = true;
                RenderSettings.fog = false;
            }
            if (RenderSettings.ambientLight != ambientSet || RenderSettings.ambientMode != AmbientMode.Flat)
            {
                if (RenderSettings.ambientLight != ambientSet)
                {
                    ambientModeWas = RenderSettings.ambientMode;
                    ambientWas = RenderSettings.ambientLight;
                    probeWas = RenderSettings.ambientProbe;
                }
                SetAmbient();
            }
            else if (ambient != ambientSet) SetAmbient();
            if (sun && RenderSettings.sun != sun)
            {
                sunWas = RenderSettings.sun;
                RenderSettings.sun = sun;
            }
            var cam = Camera.main;
            if (cam && cam != farCam)
            {
                // A new head camera: give the old one back its far plane and take this one's.
                if (farCam && farWas > 0f) farCam.farClipPlane = farWas;
                farCam = cam;
                farWas = cam.farClipPlane;
            }
            if (farCam && farCam.farClipPlane < seeingDistance)
            {
                farWas = farCam.farClipPlane;
                farCam.farClipPlane = seeingDistance;
            }
        }

        Color ambientSet;

        // The flat ambient light (and its probe), remembered so a change by anyone else can be told apart.
        void SetAmbient()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambient;
            var sh = new SphericalHarmonicsL2();
            sh.AddAmbientLight(ambient);
            RenderSettings.ambientProbe = sh;
            ambientSet = RenderSettings.ambientLight;
        }

        void RestoreLook()
        {
            if (!lookSet) return;
            lookSet = false;
            RenderSettings.fog = fogWas;
            RenderSettings.sun = sunWas;
            RenderSettings.ambientMode = ambientModeWas;
            RenderSettings.ambientLight = ambientWas;
            RenderSettings.ambientProbe = probeWas;
            if (farCam && farWas > 0f) farCam.farClipPlane = farWas;
            farCam = null;
            if (skyStandIn) Destroy(skyStandIn);
        }

        /// <summary>
        /// 429R: the sky: an inside-out sphere round the head with the sky picture on it (unlit, drawn after the valley so
        /// what's in front of it hides it; no per-pixel work beyond a texture read). A plain blue stands in without the material.
        /// </summary>
        void BuildSky()
        {
            var mat = skyMaterial;
            if (!mat)
            {
                Debug.LogWarning("[BallValley] No sky material (Tools > 429 Game > Ball Valley > Set Up Ball Valley makes it): a plain blue sky stands in.", this);
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (!shader) return;
                mat = skyStandIn = new Material(shader);
                mat.SetColor("_BaseColor", new Color(0.45f, 0.66f, 0.93f));
                mat.renderQueue = BallValleySky.Queue;
            }
            else if (mat.renderQueue != BallValleySky.Queue)
            {
                // URP puts a saved Unlit material back at queue 2000 (play test 2026-11-16). The sky ball sits on the head,
                // so at 2000 it's drawn FIRST, under everything (the whole screen twice on Quest): a copy at the
                // after-the-valley queue instead, made here and gone with the valley (RestoreLook).
                mat = skyStandIn = new Material(mat) { name = mat.name + " (after the valley)", renderQueue = BallValleySky.Queue };
            }
            float far = farCam ? farCam.farClipPlane : seeingDistance;
            float radius = Mathf.Clamp(skyRadius, 10f, far * 0.9f);   // inside the far plane, or it's cut away
            if (radius < skyRadius) Debug.LogWarning($"[BallValley] Sky Radius {skyRadius} m is past the head's far plane ({far} m): it's {radius:0} m instead.", this);
            var go = new GameObject("Sky");
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * radius;
            skyMesh = BallValleySky.SphereMesh();
            go.AddComponent<MeshFilter>().sharedMesh = skyMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            sky = go.transform;
            FollowHead();
        }

        void FollowHead()
        {
            if (!sky) return;
            if (!head || !head.isActiveAndEnabled) head = Camera.main;
            if (head) sky.position = head.transform.position;
        }
    }
}

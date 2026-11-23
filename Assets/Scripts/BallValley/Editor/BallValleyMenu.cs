using System.Collections.Generic;
using System.Linq;
using HutongGames.PlayMaker;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Balls.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Ball Valley (Gate 3, step 1 of 6, 2026-11-16):
    /// - Set Up Ball Valley: sets the sky picture's import settings (Assets/BallValley/Sky/BallValley_Sky.png, a painted
    ///   Halloween dusk), makes its unlit material if it's missing, and the prefab Assets/Prefabs/BallValley/Ball_Valley.prefab with the Ball Valley Stage (BALLS'
    ///   valley, bots only) and its sounds (429R's Audio/SFX files). Safe to run again: an existing prefab keeps its file
    ///   and every number tuned on it; only its sounds (and a missing sky material) are filled in. Never touches the scene.
    /// - Watch Ball Valley (Play mode): as GAMESTAGES' "Path 2 Holder" a fresh CurrentlySpawnedStuffs under -LevelManger
    ///   (the old one and everything in it go), the valley spawned at (0, 0, 0) in it, and the player put on an invisible
    ///   platform 4 m up behind its near long edge, looking at the middle, with moving and teleport off while watching.
    /// - Play Ball Valley (Play mode): the gate as GAMESTAGES' Path 3 ON spawns it, with its gate and the player (no rooftop
    ///   after it: GAMESTAGES isn't moved). GAMESTAGES' gate paths are set up by Tools > 429 Game > Gates.
    /// </summary>
    public static class BallValleyMenu
    {
        private const string Root = "Tools/429 Game/Ball Valley/";
        private const string Log = "[BallValley]";
        public const string PrefabFolder = "Assets/Prefabs/BallValley";
        public const string PrefabPath = PrefabFolder + "/Ball_Valley.prefab";
        private const string SkyFolder = "Assets/BallValley/Sky";
        public const string SkyPicturePath = SkyFolder + "/BallValley_Sky.png";
        public const string SkyMaterialPath = SkyFolder + "/BallValley_Sky_Mat.mat";
        private const string UnlitShader = "Universal Render Pipeline/Unlit";
        private const string Sfx429 = "Assets/Audio/SFX/";
        private const string OwnSfx = "Assets/BallValley/Sfx/";

        // What the valley loads from Resources (copied from BALLS): it can't be built without them.
        private static readonly string[] Needed =
        {
            "Assets/BallValley/Resources/BALLS/Map/Map_Static.fbx",
            "Assets/BallValley/Resources/BALLS/Map/Rock_Border.fbx",
            "Assets/BallValley/Resources/BALLS/BALLS_Palette.png",
            "Assets/BallValley/Resources/BALLS/Slime_Mat.mat",
            "Assets/BallValley/Resources/BALLS/Models/Giant_Slime.fbx",
            SkyPicturePath,
        };

        // BALLS' sound names whose 429R file is called something else.
        private static readonly Dictionary<string, string> SoundFiles = new Dictionary<string, string>
        {
            { "Bell_1", Sfx429 + "Clear,_resonant_bell_1.wav" },
            { "Bell_2", Sfx429 + "Clear,_resonant_bell_2.wav" },
            { "giant_ball_rolling_shot", OwnSfx + "giant_ball_rolling_shot.wav" },   // BALLS' cut of the Bowling Pumpkin's roll
            // 2026-11-23 (gate 3): a pocket no longer whistles like the player's own fall; a bot growing no longer plays
            // 429R's level-up; the special move's charge and the player's own pocket have their own sounds.
            { "lowDown", Kenney + "kenney_digital-audio/Audio/lowDown.ogg" },
            { "highUp", Kenney + "kenney_digital-audio/Audio/highUp.ogg" },
            { "pluck_001", Kenney + "kenney_interface-sounds/Audio/pluck_001.ogg" },
            { "pluck_002", Kenney + "kenney_interface-sounds/Audio/pluck_002.ogg" },
        };
        private const string Kenney = "Assets/Audio/Kenney Audio/";

        // The viewing platform: this far behind the near long edge and this high (m).
        private const float WatchBack = 6f, WatchHeight = 4f;

        // ---------------------------------------------------------------- Set Up Ball Valley

        [MenuItem(Root + "Set Up Ball Valley")]
        public static void SetUp() => Run();

        [MenuItem(Root + "Set Up Ball Valley", true)]
        private static bool CanSetUp() => !EditorApplication.isPlaying;

        /// <summary>Makes what's missing (sky picture, sky material, prefab) and refreshes the prefab's sounds. False if it couldn't.</summary>
        public static bool Run()
        {
            if (EditorApplication.isPlaying) { Debug.LogError($"{Log} Stop play mode first; nothing changed."); return false; }
            var missing = new List<string>();
            foreach (var path in Needed)
                if (AssetDatabase.LoadMainAssetAtPath(path) == null) missing.Add(path);
            var unlit = Shader.Find(UnlitShader);
            if (unlit == null) missing.Add("shader " + UnlitShader);
            if (missing.Count > 0)
            {
                Debug.LogError($"{Log} Set Up Ball Valley: nothing made, missing:\n" + string.Join("\n", missing));
                return false;
            }

            Folder("Assets", "BallValley");
            Folder("Assets/BallValley", "Sky");
            Folder("Assets", "Prefabs");
            Folder("Assets/Prefabs", "BallValley");

            var notes = new List<string>();
            var picture = SkyPicture(notes);
            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
            if (sky == null)
            {
                sky = new Material(unlit) { name = "BallValley_Sky_Mat" };
                sky.SetTexture("_BaseMap", picture);
                sky.mainTexture = picture;
                sky.SetColor("_BaseColor", Color.white);
                if (sky.HasProperty("_QueueControl")) sky.SetFloat("_QueueControl", 1f);   // URP: keep our queue (not reset from the surface type)
                sky.renderQueue = BallValleySky.Queue;
                AssetDatabase.CreateAsset(sky, SkyMaterialPath);
                notes.Add("sky material made");
            }

            var sounds = FindSounds(out var noSound);
            if (noSound.Count > 0) notes.Add("NO FILE for sounds " + string.Join(", ", noSound) + " (they stay silent)");

            // 429R gate 3 (2026-11-18): the gate's pieces: the candies (the field's food), the boss, the seed.
            var candies = Game.Inventory.CandyKinds.Paths.Select(AssetDatabase.LoadAssetAtPath<GameObject>).Where(c => c != null).ToArray();
            var boss = AssetDatabase.LoadAssetAtPath<GameObject>(Game.Gates.EditorTools.PumpkinBallBossMenu.PrefabPath);
            if (boss == null) boss = Game.Gates.EditorTools.PumpkinBallBossMenu.Run();
            var seed = AssetDatabase.LoadAssetAtPath<GameObject>(SeedPath);
            if (boss == null) notes.Add("NO BOSS (" + Game.Gates.EditorTools.PumpkinBallBossMenu.PrefabPath + ")");
            if (seed == null) notes.Add("NO SEED (" + SeedPath + "): The_Seed is set without one");

            bool saved;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                // There already: keep the file (GAMESTAGES will point at it) and everything tuned on it.
                var root = PrefabUtility.LoadPrefabContents(PrefabPath);
                try
                {
                    var stage = root.GetComponent<BallValleyStage>();
                    if (stage == null) stage = root.AddComponent<BallValleyStage>();
                    if (stage.skyMaterial == null) stage.skyMaterial = sky;
                    stage.sounds = sounds;
                    GatePieces(root, stage, candies, boss, seed, notes);
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out saved);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                notes.Add("prefab refreshed (its tuning kept)");
            }
            else
            {
                // Built in a preview scene: the open scene is never touched.
                var build = EditorSceneManager.NewPreviewScene();
                try
                {
                    var root = new GameObject("Ball_Valley");
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, build);
                    var stage = root.AddComponent<BallValleyStage>();
                    stage.skyMaterial = sky;
                    stage.sounds = sounds;
                    GatePieces(root, stage, candies, boss, seed, notes);
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out saved);
                }
                finally
                {
                    EditorSceneManager.ClosePreviewScene(build);
                }
                notes.Add("prefab made");
            }
            if (!saved)
            {
                Debug.LogError($"{Log} Could not save {PrefabPath}.");
                return false;
            }
            AssetDatabase.SaveAssets();
            var t = new Tuning();
            Debug.Log($"{Log} Set Up Ball Valley: {string.Join(", ", notes)}; {PrefabPath} ({t.halfX * 2f:0} x {t.halfZ * 2f:0} m valley, " +
                      $"{sounds.Length} sounds, sky {SkyMaterialPath}). Try it: Play, then {Root}Watch Ball Valley (Play mode).",
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            return true;
        }

        public const string SeedPath = "Assets/Prefabs/Items/Chest/The_Seed.prefab";
        /// <summary>The main game's chest with The Seed inside (Whack 7's barrel pumpkin drops it): gate 3's end.</summary>
        public const string SeedChestPath = "Assets/Prefabs/Items/Chest/Treasure_Chest_and_Seed.prefab";
        // Gate 3's song (2026-11-23: "a 5 minutes song that slow at first ... then keep getting faster and faster"): no song
        // is 5 minutes yet, so a chill one (2.5 min) then a fast one (3 min), both sped up as the field closes.
        private static readonly string[] SongParts = { "Pumpkin_Playroom", "Spider_Queen_Rampage" };

        // 429R gate 3 (2026-11-18, 2026-11-23): the Ball Valley Gate and Ball Valley Player on the stage, the shared Gate Run
        // (card, chests, rounds, HUD line, finale), the field's candies, the boss and the seed (numbers already tuned on
        // the prefab are kept); no Watch Mode (the gate runs its rounds).
        private static void GatePieces(GameObject root, BallValleyStage stage, GameObject[] candies, GameObject boss, GameObject seed, List<string> notes)
        {
            var gate = root.GetComponent<BallValleyGate>();
            if (gate == null)
            {
                gate = root.AddComponent<BallValleyGate>();
                notes.Add("Ball Valley Gate added");
            }
            // 2026-11-23 (user's gate 3: 15 balls, a 5-minute song, the Black Pumpkin Ball, the main game's seed chest; "no
            // need for other seeds in this gate game"): no Gate Run (its start chest, rounds and gear chests). The gate does
            // its own card and HUD line; Gate Exit (shared) sets the gear bools on the way out.
            var run = root.GetComponent<Game.Gates.GateRun>();
            if (run != null)
            {
                Object.DestroyImmediate(run, true);
                notes.Add("Gate Run removed (gate 3 runs its own flow)");
            }
            GameObject Load(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (gate.seedChest == null) gate.seedChest = Load(SeedChestPath);
            var ownBoss = Load(BallValleyKitMenu.BossVariantPath);
            var voice = ownBoss != null ? ownBoss : boss;
            if (gate.voiceFrom == null && voice != null) gate.voiceFrom = voice.GetComponent<Game.Gates.PumpkinBallBoss>();
            if (gate.blackPumpkinLook == null) gate.blackPumpkinLook = Load(BallValleyKitMenu.PieceFolder + "/BallValley_Kit_Giant_Jack_O_Lantern.prefab");
            if (gate.song == null || gate.song.Length == 0 || gate.song.Any(c => c == null))
                gate.song = SongParts.Select(n => AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx429 + n + ".wav")).Where(c => c != null).ToArray();
            notes.Add($"seed chest {(gate.seedChest != null ? gate.seedChest.name : "MISSING")}, song {gate.song.Length} parts " +
                      $"({string.Join(" + ", gate.song.Select(c => c.name))}), black pumpkin look {(gate.blackPumpkinLook != null ? "kit" : "NONE: a black ball")}");
            var player = root.GetComponent<BallValleyPlayer>();
            if (player == null)
            {
                player = root.AddComponent<BallValleyPlayer>();
                notes.Add("Ball Valley Player added");
            }
            // "We open our eyes in the middle of a field" (the old start spot was 4 m south of it).
            if (player.startSpot == new Vector3(0f, 0f, -4f)) player.startSpot = Vector3.zero;
            // A fall into a hole whistles like any fall (Fall Out Of World's Cartoon_fall_1/2).
            player.fallSounds = new[] { "Cartoon_fall_1", "Cartoon_fall_2" }
                .Select(n => AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx429 + n + ".wav")).Where(c => c != null).ToArray();
            stage.watchMode = false;
            stage.candyPrefabs = candies;
            notes.Add($"{candies.Length} candies for berries");
        }

        // Every clip BALLS' Sfx table names: 429R's Audio/SFX file of that name (or the one in SoundFiles).
        private static BallValleyStage.NamedSound[] FindSounds(out List<string> missing)
        {
            var list = new List<BallValleyStage.NamedSound>();
            missing = new List<string>();
            var seen = new HashSet<string>();
            foreach (var name in Sfx.ClipNames)
            {
                if (!seen.Add(name)) continue;
                string path = SoundFiles.TryGetValue(name, out var p) ? p : Sfx429 + name + ".wav";
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null) { missing.Add(name); continue; }
                list.Add(new BallValleyStage.NamedSound { name = name, clip = clip });
            }
            return list.ToArray();
        }

        // ---------------------------------------------------------------- the sky picture

        // The sky is a painted picture (user, 2026-11-16: "more 429R style, halloween vibe"): a Halloween dusk with a
        // harvest moon, bats and a few orange-lit clouds, drawn outside Unity by
        // K:/WORK_2026/08_04/_claude/ballvalley_2026-11-16/tools/halloween_sky.py (equirectangular, see BallValleySky: the
        // left edge looks along +Z, the top row straight up). It can be painted over or swapped for any such picture.
        // Set Up only puts its import settings right.
        private const int SkyMaxSize = 2048;

        /// <summary>The sky picture, its import settings put right: round the sky without a seam, no bleeding between the
        /// top and the bottom, smooth gradients on Quest. Null if it's missing.</summary>
        private static Texture2D SkyPicture(List<string> notes)
        {
            if (AssetImporter.GetAtPath(SkyPicturePath) is not TextureImporter importer) return null;
            bool wrong = importer.textureType != TextureImporterType.Default || !importer.sRGBTexture
                || importer.alphaSource != TextureImporterAlphaSource.None
                || importer.wrapModeU != TextureWrapMode.Repeat || importer.wrapModeV != TextureWrapMode.Clamp
                || importer.mipmapEnabled || importer.maxTextureSize != SkyMaxSize
                || importer.textureCompression != TextureImporterCompression.CompressedHQ;
            if (wrong)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.alphaSource = TextureImporterAlphaSource.None;
                importer.wrapModeU = TextureWrapMode.Repeat;   // round the sky: no seam
                importer.wrapModeV = TextureWrapMode.Clamp;    // the top doesn't bleed into the bottom
                // No mipmaps: the sky is always shown bigger than its pixels (about 6 per degree against the headset's ~18),
                // so they'd never be used; without them the main game's skybox (Skybox/Panoramic) has no seam line where
                // the picture wraps round, and it's a third smaller.
                importer.mipmapEnabled = false;
                importer.maxTextureSize = SkyMaxSize;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;   // smooth gradients (less banding on Quest)
                importer.SaveAndReimport();
                notes.Add("sky picture import settings set");
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(SkyPicturePath);
        }

        // ---------------------------------------------------------------- Watch Ball Valley (Play mode)

        [MenuItem(Root + "Watch Ball Valley (Play mode)")]
        public static void Watch()
        {
            if (!EditorApplication.isPlaying) { Debug.LogError($"{Log} Watch Ball Valley works in Play mode: press Play first."); return; }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null || prefab.GetComponent<BallValleyStage>() == null)
            {
                Debug.LogError($"{Log} No {PrefabPath} with a Ball Valley Stage: run {Root}Set Up Ball Valley first (out of Play mode).");
                return;
            }

            // As GAMESTAGES' "Path 2 Holder": the old CurrentlySpawnedStuffs (and all in it) goes, a fresh one under -LevelManger.
            var global = FsmVariables.GlobalVariables.FindFsmGameObject("CurrentlySpawnedStuffs");
            if (global != null && global.Value != null) Object.Destroy(global.Value);
            var manager = GameObject.Find("-LevelManger");
            if (manager == null) Debug.LogWarning($"{Log} No -LevelManger in the scene: the holder goes at the scene root.");
            var holder = new GameObject("CurrentlySpawnedStuffs");
            if (manager != null) holder.transform.SetParent(manager.transform);
            holder.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            if (global != null) global.Value = holder;
            else Debug.LogWarning($"{Log} No global CurrentlySpawnedStuffs variable: the valley still goes in the new holder.");

            // A frame later (the old holder, and any old valley in it, are gone by then): the valley and the platform.
            int frame = Time.frameCount;
            EditorApplication.CallbackFunction spawn = null;
            spawn = () =>
            {
                if (!EditorApplication.isPlaying) { EditorApplication.update -= spawn; return; }
                if (Time.frameCount <= frame + 1) return;
                EditorApplication.update -= spawn;
                if (holder == null) { Debug.LogError($"{Log} The new CurrentlySpawnedStuffs went away before the valley came."); return; }
                var go = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity, holder.transform);
                go.name = prefab.name;
                var stage = go.GetComponent<BallValleyStage>();
                // Watching: BALLS' rounds with bots only (no gate, no player in the valley).
                var gateOn = go.GetComponent<BallValleyGate>();
                if (gateOn != null) gateOn.enabled = false;
                var runOn = go.GetComponent<Game.Gates.GateRun>();
                if (runOn != null) runOn.enabled = false;   // (before its Start: no card, chests or rounds while watching)
                // (the gate's Awake has already put its own clock and sizes on the stage: BALLS' numbers back, 2026-11-23)
                var asMade = prefab.GetComponent<BallValleyStage>();
                if (gateOn != null && asMade != null) stage.tune = JsonUtility.FromJson<Tuning>(JsonUtility.ToJson(asMade.tune));
                // (and its other settings: BALLS' phases, trees by the edges, no volcanoes; 2026-11-23)
                stage.driveScale = stage.drivePending = -1f;
                stage.botsHuntBots = true;
                stage.startBalls = -1;
                stage.treesAnywhere = false;
                stage.volcanoCount = 0;
                stage.treeHitCandies = 0;
                var vrOn = go.GetComponent<BallValleyPlayer>();
                if (vrOn != null) vrOn.enabled = false;
                stage.watchMode = true;
                var spot = new GameObject("Watch Spot");
                spot.transform.SetParent(go.transform, false);
                float back = stage.tune.halfZ + WatchBack;
                spot.transform.position = new Vector3(0f, WatchHeight, -back);
                var watch = spot.AddComponent<BallValleyWatchSpot>();
                watch.yaw = 0f;   // looking +z: down the middle of the valley
                Debug.Log($"{Log} Watching: Ball_Valley at (0, 0, 0) in a fresh CurrentlySpawnedStuffs, the player on a platform " +
                          $"{WatchHeight:0} m up, {back:0.#} m behind the middle (moving and teleport off while it's there). " +
                          "GAMESTAGES isn't told: stop Play mode when done.", go);
            };
            EditorApplication.update += spawn;
        }

        [MenuItem(Root + "Watch Ball Valley (Play mode)", true)]
        private static bool CanWatch() => EditorApplication.isPlaying;

        /// <summary>
        /// 429R gate 3 (2026-11-18), Play mode: the valley as GAMESTAGES' Path 3 ON spawns it (a fresh
        /// CurrentlySpawnedStuffs, the valley at (0, 0, 0)), with its gate and the player in it. GAMESTAGES isn't moved:
        /// after the boss the seed is out, but no rooftop comes (Gate Exit waits, then gives the view back).
        /// </summary>
        [MenuItem(Root + "Play Ball Valley (Play mode)")]
        public static void PlayGate()
        {
            if (!EditorApplication.isPlaying) { Debug.LogError($"{Log} Play Ball Valley works in Play mode: press Play first."); return; }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null || prefab.GetComponent<BallValleyGate>() == null)
            {
                Debug.LogError($"{Log} No {PrefabPath} with a Ball Valley Gate: run {Root}Set Up Ball Valley first (out of Play mode).");
                return;
            }
            var global = FsmVariables.GlobalVariables.FindFsmGameObject("CurrentlySpawnedStuffs");
            if (global != null && global.Value != null) Object.Destroy(global.Value);
            var manager = GameObject.Find("-LevelManger");
            var holder = new GameObject("CurrentlySpawnedStuffs");
            if (manager != null) holder.transform.SetParent(manager.transform);
            holder.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            if (global != null) global.Value = holder;
            int frame = Time.frameCount;
            EditorApplication.CallbackFunction spawn = null;
            spawn = () =>
            {
                if (!EditorApplication.isPlaying) { EditorApplication.update -= spawn; return; }
                if (Time.frameCount <= frame + 1) return;
                EditorApplication.update -= spawn;
                if (holder == null) return;
                var go = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity, holder.transform);
                go.name = prefab.name;
                Debug.Log($"{Log} Playing gate 3: Ball_Valley at (0, 0, 0) with its gate and the player. Stop Play mode when done.", go);
            };
            EditorApplication.update += spawn;
        }

        [MenuItem(Root + "Play Ball Valley (Play mode)", true)]
        private static bool CanPlayGate() => EditorApplication.isPlaying;

        private static void Folder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}

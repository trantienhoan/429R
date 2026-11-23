using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Gates;
using UnityEditor;
using UnityEngine;

namespace Game.PlayGround.Editor
{
    /// <summary>
    /// Tools > 429 Game > PlayGround > Use Gate Run (3 Rounds) (2026-11-23, the gates' one flow): Play_Ground_Z.prefab gets
    /// a Gate Run (the card, the start chest = the cauldron chest, the 3 round chests, the rounds' time limits, the HUD
    /// line, the finale with gate 1's stew pumpkin) and a Play Ground Game (cook and defend in 3 rounds: their recipes, monsters and mini
    /// bosses), and its old start chest spawner (Cauldron_Pos, whose seed made the cauldron) is switched off. Numbers
    /// already there are kept (tune them in the Inspector); only empty slots are filled. A copy of the prefab from before
    /// the first run goes to Documents/429R_claude_backups/2026-11-23/gate1/. The broken pot's chaos boss
    /// (pumpkin_boss_Z_Doom) sends no Win and sets no BOSS: killing it only ends the gate (user, 2026-11-23).
    /// "Back To One Dish (No Gate Run)" switches both off and the old start chest back on.
    /// Never touches the scene (GAMESTAGES' Path 1 ON spawns Play_Ground_Z).
    /// </summary>
    public static class PlayGroundGateMenu
    {
        private const string Root = "Tools/429 Game/PlayGround/";
        private const string Log = "[PlayGround Gate]";
        private const string PlayGroundPath = "Assets/Prefabs/Bases/Play_Ground_Z.prefab";
        private const string CauldronPath = "Assets/Prefabs/PlayGround/Fire_Woods_n_Cauldron.prefab";
        private const string Bugs = "Assets/Prefabs/Enemies/Enemies_Smarter/";
        private const string Slimes = "Assets/Prefabs/Enemies/PLAYGROUND/";
        // The cauldron chest: its seed (Bracer) pops the cauldron out, as it always did in gate 1.
        private const string StartChestPath = "Assets/Prefabs/Items/Chest/Treasure_Chest_and_Seed_Bracer_Cauldron.prefab";
        private const string SharedStartChestPath = "Assets/Prefabs/Items/Chest/Treasure_Chest_and_Seed_Bracer.prefab";
        private const string ChestFolder = "Assets/Prefabs/PlayGround/PG_Wood/";
        private const string SeedPath = "Assets/Prefabs/Items/Chest/The_Seed.prefab";
        private const string PuffPath = "Assets/Prefabs/Effects/Smoke/Smoke_Flash_Small_Puff.prefab";
        private const string Spawner = "Cauldron_Pos";
        private const string HatPath = "Assets/Prefabs/PlayGround/Recipe_Hat.prefab";

        // Each round's monsters while it cooks, and its mini bosses (the user's 11-16 "a 4th big slime, more = fun" in round 3).
        private static readonly string[][] RoundBugs =
        {
            new[] { "Spider_Small" },
            new[] { "Spider_Small", "Spider_Normal" },
            new[] { "Spider_Normal", "Cockroach_Small", "Cockroach_Normal" },
        };
        private static readonly string[][] RoundBosses =
        {
            new[] { "Slime_Big_Blue" },
            new[] { "Slime_Big_Red" },
            new[] { "Slime_Big_Yellow", "Slime_Big" },
        };
        private static readonly string[] RoundChests =
            { "PG_Treasure_Chest_and_Seed_Chest", "PG_Treasure_Chest_and_Seed_Head", "PG_Treasure_Chest_and_Seed_Shield" };
        // Each round's time limit (it ends anyway, its chest still drops): about 2-2.5 minutes a round.
        private static readonly float[] RoundSeconds = { 150f, 165f, 180f };

        [MenuItem(Root + "Use Gate Run (3 Rounds)")]
        public static void Use()
        {
            if (EditorApplication.isPlaying) { Debug.LogError($"{Log} Stop play mode first; nothing changed."); return; }
            var notes = new List<string>();
            Backup(notes);
            var root = PrefabUtility.LoadPrefabContents(PlayGroundPath);
            try
            {
                var run = root.GetComponent<GateRun>();
                if (run == null) { run = root.AddComponent<GateRun>(); notes.Add("Gate Run added"); }
                run.enabled = true;
                FillRun(run, notes);
                var game = root.GetComponent<PlayGroundGame>();
                if (game == null) { game = root.AddComponent<PlayGroundGame>(); notes.Add("Play Ground Game added"); }
                game.enabled = true;
                FillGame(game, notes);
                Spawners(root, false, notes);
                RemoveDeadCopies(root, notes);
                EditorUtility.SetDirty(run);
                EditorUtility.SetDirty(game);
                PrefabUtility.SaveAsPrefabAsset(root, PlayGroundPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            CauldronDefenceMenu.DoomBossEndsGateOnly(notes);
            AssetDatabase.SaveAssets();
            Debug.Log($"{Log} Gate 1 = the gates' one flow, 3 rounds: {string.Join("; ", notes)}.");
        }

        [MenuItem(Root + "Use Gate Run (3 Rounds)", true)]
        private static bool CanUse() => !EditorApplication.isPlaying;

        [MenuItem(Root + "Back To One Dish (No Gate Run)")]
        public static void Back()
        {
            if (EditorApplication.isPlaying) { Debug.LogError($"{Log} Stop play mode first; nothing changed."); return; }
            var notes = new List<string>();
            Backup(notes);
            var root = PrefabUtility.LoadPrefabContents(PlayGroundPath);
            try
            {
                if (root.TryGetComponent(out GateRun run) && run.enabled) { run.enabled = false; notes.Add("Gate Run off (kept)"); }
                if (root.TryGetComponent(out PlayGroundGame game) && game.enabled) { game.enabled = false; notes.Add("Play Ground Game off (kept)"); }
                Spawners(root, true, notes);
                PrefabUtility.SaveAsPrefabAsset(root, PlayGroundPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"{Log} Gate 1 = the old single dish (its own start chest): {string.Join("; ", notes)}.");
        }

        [MenuItem(Root + "Back To One Dish (No Gate Run)", true)]
        private static bool CanGoBack() => !EditorApplication.isPlaying;

        private static void FillRun(GateRun run, List<string> notes)
        {
            // Gate 1 starts with the cauldron chest (Gate Run's own default is the shared Bracer chest).
            if (run.startChest == null || run.startChest == AssetDatabase.LoadAssetAtPath<GameObject>(SharedStartChestPath))
            {
                run.startChest = Load<GameObject>(StartChestPath, notes);
                notes.Add("start chest: the cauldron chest");
            }
            if (run.roundChests == null || run.roundChests.Length != 3) run.roundChests = new GameObject[3];
            for (int i = 0; i < 3; i++)
                if (run.roundChests[i] == null) run.roundChests[i] = Load<GameObject>(ChestFolder + RoundChests[i] + ".prefab", notes);
            // Gate 1's own giant pumpkin (Build PlayGround Kit makes it), else the shared one.
            var stew = AssetDatabase.LoadAssetAtPath<GameObject>(PlayGroundKitMenu.BossVariantPath);
            var shared = AssetDatabase.LoadAssetAtPath<GameObject>(Game.Gates.EditorTools.PumpkinBallBossMenu.PrefabPath);
            if (run.finaleBoss == null || (run.finaleBoss == shared && stew != null))
            {
                run.finaleBoss = stew != null ? stew : shared;
                notes.Add("finale: " + (run.finaleBoss != null ? run.finaleBoss.name : "NONE"));
            }
            if (run.finaleSeed == null) run.finaleSeed = Load<GameObject>(SeedPath, notes);
            if (run.chestPuff == null) run.chestPuff = Load<GameObject>(PuffPath, notes);
            if (run.roundSeconds == null || run.roundSeconds.Length != 3 || run.roundSeconds.All(s => Mathf.Approximately(s, 150f)))
            {
                run.roundSeconds = (float[])RoundSeconds.Clone();
                notes.Add("round limits " + string.Join("/", RoundSeconds) + " s");
            }
        }

        private static void FillGame(PlayGroundGame game, List<string> notes)
        {
            if (game.cauldronPrefab == null) game.cauldronPrefab = Load<GameObject>(CauldronPath, notes);
            if (game.rounds == null || game.rounds.Length != 3) game.rounds = new CauldronDefence.Round[3];
            for (int i = 0; i < 3; i++)
            {
                game.rounds[i] ??= new CauldronDefence.Round { label = (i + 1).ToString() };
                var r = game.rounds[i];
                if (r.enemies == null || r.enemies.Length == 0 || r.enemies.All(e => e == null))
                {
                    r.enemies = RoundBugs[i].Select(b => Load<GameObject>(Bugs + b + ".prefab", notes)).Where(x => x != null).ToArray();
                    notes.Add($"round {i + 1} monsters: {string.Join(", ", RoundBugs[i])}");
                }
                if (r.miniBosses == null || r.miniBosses.Length == 0 || r.miniBosses.All(e => e == null))
                {
                    r.miniBosses = RoundBosses[i].Select(b => Load<GameObject>(Slimes + b + ".prefab", notes)).Where(x => x != null).ToArray();
                    notes.Add($"round {i + 1} mini bosses: {string.Join(", ", RoundBosses[i])}");
                }
            }
        }

        // The PlayGround's own start chest spawner on (the old single dish) or off (Gate Run drops the start chest).
        private static void Spawners(GameObject root, bool on, List<string> notes)
        {
            int n = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != Spawner) continue;
                foreach (var fsm in t.GetComponents<PlayMakerFSM>())
                {
                    if (fsm.enabled == on) continue;
                    fsm.enabled = on;
                    n++;
                }
            }
            notes.Add(n > 0 ? $"old start chest spawner ({Spawner}) {(on ? "on" : "off")}" : $"old start chest spawner already {(on ? "on" : "off")}");
        }

        // The inactive copies of the cauldron and the hat the PlayGround kept inside it (never switched on: the game makes
        // its own from the prefabs): removed, so the PlayGround loads lighter (the gate 1 brief, 2026-11-22).
        private static void RemoveDeadCopies(GameObject root, List<string> notes)
        {
            var dead = new List<GameObject>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var go = t.gameObject;
                if (go == root || go.activeSelf || !PrefabUtility.IsOutermostPrefabInstanceRoot(go)) continue;
                string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
                if (path == CauldronPath || path == HatPath) dead.Add(go);
            }
            foreach (var go in dead) Object.DestroyImmediate(go);
            if (dead.Count > 0) notes.Add(dead.Count + " inactive copies of the cauldron and the hat removed");
        }

        // A copy of the prefab as it was before this menu first changed it (a later run keeps that first copy).
        private static void Backup(List<string> notes)
        {
            string folder = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
                "429R_claude_backups", "2026-11-23", "gate1");
            Directory.CreateDirectory(folder);
            string to = Path.Combine(folder, "Play_Ground_Z_before_gate_run.prefab");
            if (File.Exists(to)) return;
            File.Copy(PlayGroundPath, to);
            notes.Add("backup " + to);
        }

        private static T Load<T>(string path, List<string> notes) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                Debug.LogWarning($"{Log} Missing '{path}' (left empty).");
                notes.Add("MISSING " + path);
            }
            return asset;
        }
    }
}

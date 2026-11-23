using System.Collections.Generic;
using System.Linq;
using Game.Gates;
using UnityEditor;
using UnityEngine;

namespace Game.Bowling.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Bowling > Gate 2: Use Gate Run (2026-11-23, the gates' one flow): puts Gate Run on
    /// Prefabs/Bowling/Bowling_Arena (its Bowling Stage plays the rounds through IGateGame): gate 2's card ("BOWL AND
    /// FIGHT", user 2026-11-23: "bowling and fighting"), the shared start and round chests (Bracer; Chest, Head, Shield),
    /// 120 s rounds, the giant pumpkin (gate 2's own variant when there is one, else the one the arena already had) and
    /// The_Seed. It also moves the Bowling Stage's start to the new pace (the slime comes 1 s after the start seed, a 5 s
    /// descent, no laps) where it still has the old values, and removes the old Rules Sign (the card says it now).
    /// Re-runnable: an existing Gate Run keeps its card, sounds and numbers (only missing prefabs are filled in). Never
    /// touches the scene. Set Up Bowling Arena calls the same.
    /// </summary>
    public static class Gate2RunMenu
    {
        private const string Log = "[Bowling]";
        private const string MenuPath = "Tools/429 Game/Bowling/Gate 2: Use Gate Run";
        private const string ArenaPath = "Assets/Prefabs/Bowling/Bowling_Arena.prefab";
        /// <summary>Gate 2's own giant pumpkin (a variant of the shared one), once it's made.</summary>
        public const string BossVariantPath = "Assets/Prefabs/Bowling/Pumpkin_Ball_Boss_Bowling.prefab";
        private const string BossPath = "Assets/Prefabs/Gates/Pumpkin_Ball_Boss.prefab";
        private const string StartChestPath = "Assets/Prefabs/Items/Chest/Treasure_Chest_and_Seed_Bracer.prefab";
        private static readonly string[] RoundChestPaths =
        {
            "Assets/Prefabs/PlayGround/PG_Wood/PG_Treasure_Chest_and_Seed_Chest.prefab",
            "Assets/Prefabs/PlayGround/PG_Wood/PG_Treasure_Chest_and_Seed_Head.prefab",
            "Assets/Prefabs/PlayGround/PG_Wood/PG_Treasure_Chest_and_Seed_Shield.prefab",
        };
        private const string SeedPath = "Assets/Prefabs/Items/Chest/The_Seed.prefab";
        private const string PuffPath = "Assets/Prefabs/Effects/Smoke/Smoke_Flash_Small_Puff.prefab";

        [MenuItem(MenuPath)]
        public static void Menu()
        {
            if (EditorApplication.isPlaying) { Debug.LogError($"{Log} Stop play mode first; nothing changed."); return; }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ArenaPath) == null)
            {
                Debug.LogError($"{Log} No {ArenaPath} (run Set Up Bowling Arena first); nothing changed.");
                return;
            }
            var notes = new List<string>();
            var root = PrefabUtility.LoadPrefabContents(ArenaPath);
            try
            {
                if (root.GetComponent<BowlingStage>() == null)
                {
                    Debug.LogError($"{Log} {ArenaPath} has no Bowling Stage (run Set Up Bowling Arena); nothing changed.");
                    return;
                }
                Configure(root, notes);
                PrefabUtility.SaveAsPrefabAsset(root, ArenaPath, out bool saved);
                if (!saved) notes.Add("COULD NOT SAVE " + ArenaPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"{Log} Gate 2: Use Gate Run: {string.Join("; ", notes)}.");
        }

        [MenuItem(MenuPath, true)]
        private static bool CanRun() => !EditorApplication.isPlaying;

        /// <summary>The arena root's Gate Run, made if missing and set up for gate 2 (see the class summary).</summary>
        public static GateRun Configure(GameObject root, List<string> notes)
        {
            var run = root.GetComponent<GateRun>();
            if (run == null)
            {
                run = root.AddComponent<GateRun>();
                run.cardTitle = "BOWL AND FIGHT";
                run.cardLine = "Strike the slime pins. Fight what hatches.";
                run.roundSeconds = new[] { 120f, 120f, 120f };
                notes.Add("Gate Run added (card, chests, 120 s rounds, finale)");
            }
            if (run.startChest == null) run.startChest = Load(StartChestPath);
            if (run.roundChests == null || run.roundChests.Length < 3 || run.roundChests.Any(c => c == null))
                run.roundChests = RoundChestPaths.Select(Load).ToArray();
            if (run.chestPuff == null) run.chestPuff = Load(PuffPath);
            if (run.finaleSeed == null) run.finaleSeed = Load(SeedPath);

            // The giant pumpkin: gate 2's own variant when there is one; else what the Gate Run or the stage (Gate Finales
            // wrote it there before) already had; else the shared one.
            var stage = root.GetComponent<BowlingStage>();
            var own = Load(BossVariantPath);
            if (own != null) run.finaleBoss = own;
            else if (run.finaleBoss == null)
            {
                var old = stage != null ? new SerializedObject(stage).FindProperty("finaleBoss")?.objectReferenceValue as GameObject : null;
                run.finaleBoss = old != null ? old : Load(BossPath);
            }
            EditorUtility.SetDirty(run);
            if (run.startChest == null || run.roundChests.Any(c => c == null) || run.finaleSeed == null) notes.Add("SOME CHEST OR SEED PREFABS MISSING");
            notes.Add("giant pumpkin: " + (run.finaleBoss != null ? run.finaleBoss.name : "NONE"));

            // The stage's start at the new pace, only where it still has the old values (the user may have tuned them).
            if (stage != null)
            {
                var so = new SerializedObject(stage);
                int moved = 0;
                moved += Move(so, "entranceLaps", 2, 0);
                moved += Move(so, "entranceSeconds", 6f, 5f);
                moved += Move(so, "slimeDelay", 4f, 1f);
                moved += Move(so, "poopEvery", new[] { 12f, 14f, 18f }, new[] { 8f, 12f, 16f });
                so.ApplyModifiedPropertiesWithoutUndo();
                if (moved > 0) notes.Add($"{moved} values moved to the new pace (no laps, 5 s descent, 1 s delay, racks every 8/12/16 s)");
            }

            // The old rules sign over lane 0: the card says it now.
            var sign = root.transform.Find("X/Rules Sign");
            if (sign != null)
            {
                Object.DestroyImmediate(sign.gameObject);
                notes.Add("old Rules Sign removed (the card says it)");
            }
            return run;
        }

        private static int Move(SerializedObject so, string name, int from, int to)
        {
            var p = so.FindProperty(name);
            if (p == null || p.intValue != from) return 0;
            p.intValue = to;
            return 1;
        }

        private static int Move(SerializedObject so, string name, float from, float to)
        {
            var p = so.FindProperty(name);
            if (p == null || !Mathf.Approximately(p.floatValue, from)) return 0;
            p.floatValue = to;
            return 1;
        }

        private static int Move(SerializedObject so, string name, float[] from, float[] to)
        {
            var p = so.FindProperty(name);
            if (p == null || !p.isArray || p.arraySize != from.Length) return 0;
            for (int i = 0; i < from.Length; i++)
                if (!Mathf.Approximately(p.GetArrayElementAtIndex(i).floatValue, from[i])) return 0;
            p.arraySize = to.Length;
            for (int i = 0; i < to.Length; i++) p.GetArrayElementAtIndex(i).floatValue = to[i];
            return 1;
        }

        private static GameObject Load(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }
}

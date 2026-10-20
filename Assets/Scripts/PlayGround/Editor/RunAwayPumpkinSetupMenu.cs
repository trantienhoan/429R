using System.Collections.Generic;
using Game.Combat;
using Game.Leveling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.PlayGround.Editor
{
    /// <summary>
    /// Tools > 429 Game > Items > Make Stage Pumpkins Run Away.
    /// The main game's breakable pumpkins run from the player like Pumpkin_9, for a short while each (Run Away Pumpkin):
    /// Pumpkin_0 5 stamina, Pumpkin_1 9, Pumpkin_2 15, Pumpkin_4 17, Pumpkin_6 21 (10 used per second of running).
    /// Their FSMs are not changed. Safe to run again (it sets the stamina again).
    /// Tools > 429 Game > Items > Give EXP For Tree Pumpkins: Pumpkin_On_Tree gives 0.25 EXP when it breaks (Exp When
    /// Broken).
    /// </summary>
    public static class RunAwayPumpkinSetupMenu
    {
        private const string Folder = "Assets/Prefabs/Items/Breakable/";
        private const string HitSettingsPath = "Assets/Data/Combat/Hit Settings.asset";
        private const string TreePumpkinPath = Folder + "Pumpkin_On_Tree.prefab";

        private static readonly (string name, float stamina)[] Runners =
        {
            ("Pumpkin_0", 5f), ("Pumpkin_1", 9f), ("Pumpkin_2", 15f), ("Pumpkin_4", 17f), ("Pumpkin_6", 21f),
        };

        [MenuItem("Tools/429 Game/Items/Make Stage Pumpkins Run Away")]
        public static void Run()
        {
            var hitSettings = AssetDatabase.LoadAssetAtPath<HitSettings>(HitSettingsPath);
            var log = new List<string>();
            foreach (var (name, stamina) in Runners)
            {
                string path = Folder + name + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { log.Add(name + " missing"); continue; }
                if (IsOpen(path)) { log.Add(name + " is open in Prefab Mode: skipped (close it and run again)"); continue; }
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var runner = root.GetComponent<RunAwayPumpkin>();
                    if (runner == null) runner = root.AddComponent<RunAwayPumpkin>();
                    var so = new SerializedObject(runner);
                    so.FindProperty("stamina").floatValue = stamina;
                    if (hitSettings != null) so.FindProperty("hitSettings").objectReferenceValue = hitSettings;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    log.Add($"{name} {stamina}");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Run Away Pumpkins] " + string.Join("; ", log));
        }

        [MenuItem("Tools/429 Game/Items/Give EXP For Tree Pumpkins")]
        public static void TreePumpkinExp()
        {
            if (IsOpen(TreePumpkinPath)) { Debug.LogWarning("[Tree Pumpkin EXP] Pumpkin_On_Tree is open in Prefab Mode: close it and run again."); return; }
            var root = PrefabUtility.LoadPrefabContents(TreePumpkinPath);
            try
            {
                var exp = root.GetComponent<ExpWhenBroken>();
                if (exp == null) exp = root.AddComponent<ExpWhenBroken>();
                var so = new SerializedObject(exp);
                so.FindProperty("exp").floatValue = 0.25f;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, TreePumpkinPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            Debug.Log("[Tree Pumpkin EXP] Pumpkin_On_Tree gives 0.25 EXP when it breaks.");
        }

        private static bool IsOpen(string path)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            return stage != null && stage.assetPath == path;
        }
    }
}

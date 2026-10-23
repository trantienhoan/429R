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
    /// Pumpkin_0 150 stamina, Pumpkin_1 170, Pumpkin_2 190, Pumpkin_4 200, Pumpkin_6 210 (10 used per second of running);
    /// Pumpkin_1, 2, 4 and 6 turn their back to the player after a run (Turn Away After Run).
    /// Each also goes off with a big bang when it breaks (Pumpkin Boom: flash, burst, smoke and a pumpkin explosion sound).
    /// Their FSMs are not changed. Safe to run again (it sets the stamina again).
    /// Tools > 429 Game > Weapons > Smaller Flip-Flop Blasts: the flip-flops' bang at 70% of its old size (Grenade Effect
    /// Size 0.7).
    /// Tools > 429 Game > Items > Give EXP For Tree Pumpkins: Pumpkin_On_Tree gives 0.25 EXP when it breaks (Exp When
    /// Broken). Tools > 429 Game > Items > Tree Pumpkins Break On Enemies: it bursts when it's thrown into an enemy
    /// (Break On Thrown Hit).
    /// </summary>
    public static class RunAwayPumpkinSetupMenu
    {
        private const string Folder = "Assets/Prefabs/Items/Breakable/";
        private const string HitSettingsPath = "Assets/Data/Combat/Hit Settings.asset";
        private const string TreePumpkinPath = Folder + "Pumpkin_On_Tree.prefab";

        // Name, stamina, and whether it turns its back to the player after a run (Turn Away After Run).
        private static readonly (string name, float stamina, bool turnAway)[] Runners =
        {
            ("Pumpkin_0", 150f, false), ("Pumpkin_1", 170f, true), ("Pumpkin_2", 190f, true), ("Pumpkin_4", 200f, true), ("Pumpkin_6", 210f, true),
        };

        private static readonly string[] BoomEffects =
        {
            "Assets/Lana Studio/Hyper Casual FX/Prefabs/Flash/Flash_round_yellow.prefab",
            "Assets/Lana Studio/Hyper Casual FX/Prefabs/Confetti/Hit_Blast.prefab",
            "Assets/Lana Studio/Hyper Casual FX/Prefabs/Confetti/Smoke_Blast.prefab",
        };
        private static readonly string[] BoomSounds = { "Assets/Audio/SFX/pumpkin_explode_1.wav", "Assets/Audio/SFX/pumpkin_explode_2.wav" };
        private static readonly string[] FlipFlops = { "Flip_Flop_L", "Flip_Flop_R", "Flip_Flop2_L", "Flip_Flop2_R" };
        private const float FlipFlopEffectSize = 0.7f;
        // The bang for a pumpkin 30 cm across (the stage pumpkins): nearly twice the flip-flops' 0.7.
        private const float BoomSize = 1.3f;

        [MenuItem("Tools/429 Game/Items/Make Stage Pumpkins Run Away")]
        public static void Run()
        {
            var hitSettings = AssetDatabase.LoadAssetAtPath<HitSettings>(HitSettingsPath);
            var log = new List<string>();
            foreach (var (name, stamina, turnAway) in Runners)
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
                    so.FindProperty("turnAwayAfterRun").boolValue = turnAway;
                    if (hitSettings != null) so.FindProperty("hitSettings").objectReferenceValue = hitSettings;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    SetUpBoom(root);
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

        // The big bang when it breaks.
        private static void SetUpBoom(GameObject root)
        {
            var boom = root.GetComponent<PumpkinBoom>();
            if (boom == null) boom = root.AddComponent<PumpkinBoom>();
            var so = new SerializedObject(boom);
            var effects = so.FindProperty("effects");
            effects.arraySize = BoomEffects.Length;
            for (int i = 0; i < BoomEffects.Length; i++)
                effects.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(BoomEffects[i]);
            var sounds = so.FindProperty("sounds");
            sounds.arraySize = BoomSounds.Length;
            for (int i = 0; i < BoomSounds.Length; i++)
                sounds.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(BoomSounds[i]);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("Tools/429 Game/Weapons/Smaller Flip-Flop Blasts")]
        public static void SmallerFlipFlopBlasts()
        {
            var log = new List<string>();
            foreach (var name in FlipFlops)
            {
                string path = "Assets/Prefabs/Weapon/" + name + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { log.Add(name + " missing"); continue; }
                if (IsOpen(path)) { log.Add(name + " is open in Prefab Mode: skipped"); continue; }
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var grenade = root.GetComponent<Game.Weapons.Grenade>();
                    if (grenade == null) { log.Add(name + " has no Grenade"); continue; }
                    var so = new SerializedObject(grenade);
                    so.FindProperty("effectSize").floatValue = FlipFlopEffectSize;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    log.Add(name + " " + FlipFlopEffectSize);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Flip-Flop Blasts] " + string.Join("; ", log));
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

        [MenuItem("Tools/429 Game/Items/Tree Pumpkins Break On Enemies")]
        public static void TreePumpkinBreaks()
        {
            if (IsOpen(TreePumpkinPath)) { Debug.LogWarning("[Tree Pumpkin] Pumpkin_On_Tree is open in Prefab Mode: close it and run again."); return; }
            var root = PrefabUtility.LoadPrefabContents(TreePumpkinPath);
            try
            {
                if (root.GetComponent<Game.Weapons.BreakOnThrownHit>() == null) root.AddComponent<Game.Weapons.BreakOnThrownHit>();
                PrefabUtility.SaveAsPrefabAsset(root, TreePumpkinPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            Debug.Log("[Tree Pumpkin] Pumpkin_On_Tree breaks when it's thrown into an enemy.");
        }

        private static bool IsOpen(string path)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            return stage != null && stage.assetPath == path;
        }
    }
}

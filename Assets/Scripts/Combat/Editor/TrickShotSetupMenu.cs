using Game.Combat;
using Game.Weapons;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Combat > Trick Shots (Headshot And Splat):
    /// - Headshot: the shared Hit Settings get their headshot bonk (bone_bonk_1/2). Any thrown thing that hurts an enemy
    ///   on the head knocks it out for a moment, with a bonk and +1 EXP (Hit Settings > Headshot).
    /// - Splat: Pumpkin_On_Tree bursting on an enemy (Break On Thrown Hit) splashes the enemies around it, with a pumpkin
    ///   burst (Hit_Blast + Smoke_Blast) and its sound (pumpkin_explode_1).
    /// Safe to run again.
    /// </summary>
    public static class TrickShotSetupMenu
    {
        private const string HitSettingsPath = "Assets/Data/Combat/Hit Settings.asset";
        private const string TreePumpkinPath = "Assets/Prefabs/Items/Breakable/Pumpkin_On_Tree.prefab";
        private static readonly string[] HeadshotSounds = { "Assets/Audio/SFX/bone_bonk_1.wav", "Assets/Audio/SFX/bone_bonk_2.wav" };
        private static readonly string[] SplatEffects =
        {
            "Assets/Lana Studio/Hyper Casual FX/Prefabs/Confetti/Hit_Blast.prefab",
            "Assets/Lana Studio/Hyper Casual FX/Prefabs/Confetti/Smoke_Blast.prefab",
        };
        private const string SplatSound = "Assets/Audio/SFX/pumpkin_explode_1.wav";

        [MenuItem("Tools/429 Game/Combat/Trick Shots (Headshot And Splat)")]
        public static void Run()
        {
            var settings = AssetDatabase.LoadAssetAtPath<HitSettings>(HitSettingsPath);
            if (settings == null)
            {
                Debug.LogError("[Trick Shots] No Hit Settings at " + HitSettingsPath + ".");
                return;
            }
            var so = new SerializedObject(settings);
            SetList(so.FindProperty("headshotSounds"), HeadshotSounds);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);

            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == TreePumpkinPath)
            {
                Debug.LogWarning("[Trick Shots] Pumpkin_On_Tree is open in Prefab Mode: close it and run again for the splat.");
                return;
            }
            var root = PrefabUtility.LoadPrefabContents(TreePumpkinPath);
            try
            {
                var breaks = root.GetComponent<BreakOnThrownHit>();
                if (breaks == null) breaks = root.AddComponent<BreakOnThrownHit>();
                var splat = new SerializedObject(breaks);
                SetList(splat.FindProperty("splatEffects"), SplatEffects);
                splat.FindProperty("splatSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(SplatSound);
                splat.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, TreePumpkinPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Trick Shots] Headshots (Hit Settings) and the tree pumpkin's splat are set up.");
        }

        private static void SetList(SerializedProperty list, string[] paths)
        {
            list.arraySize = paths.Length;
            for (int i = 0; i < paths.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Object>(paths[i]);
        }
    }
}

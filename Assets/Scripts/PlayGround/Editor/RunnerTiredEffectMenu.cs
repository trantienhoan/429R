using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.PlayGround.Editor
{
    /// <summary>
    /// Tools > 429 Game > Items > Add Tired Stars To Runner Pumpkins (user, 2026-10-29: "use that stun particle as
    /// exhausted particle for runner pumpkins"). Gives every pumpkin with Run Away Pumpkin (the Chaos ones too) the mini
    /// bosses' stun stars (Assets/Prefabs/Enemies/Hit_FX/Stun_Stars.prefab) as its Tired Stars, shown over its top while
    /// it's out of breath. Also takes off the sweat-and-panting Tired_Effect the pumpkins had before and puts its prefab
    /// and materials (Assets/Prefabs/Effects/Pumpkin_Tired.prefab, Assets/Data/Effects/Pumpkin Tired *.mat) in the
    /// recycle bin. Running it again changes nothing more.
    /// </summary>
    public static class RunnerTiredEffectMenu
    {
        private const string StarsPath = "Assets/Prefabs/Enemies/Hit_FX/Stun_Stars.prefab";
        private const string OldEffectName = "Tired_Effect";
        private static readonly string[] OldAssets =
        {
            "Assets/Prefabs/Effects/Pumpkin_Tired.prefab",
            "Assets/Data/Effects/Pumpkin Tired Sweat.mat",
            "Assets/Data/Effects/Pumpkin Tired Breath.mat",
        };

        [MenuItem("Tools/429 Game/Items/Add Tired Stars To Runner Pumpkins")]
        public static void Run()
        {
            var stars = AssetDatabase.LoadAssetAtPath<GameObject>(StarsPath);
            if (stars == null)
            {
                Debug.LogError("[Tired Pumpkins] No " + StarsPath + ": run Tools > 429 Game > Enemies > Build Stun Stars FX first.");
                return;
            }
            var log = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null || asset.GetComponent<RunAwayPumpkin>() == null) continue;
                if (PrefabUtility.IsPartOfVariantPrefab(asset)) { log.Add(asset.name + " is a variant: left to its base"); continue; }
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                if (stage != null && stage.assetPath == path) { log.Add(asset.name + " is open in Prefab Mode: skipped (close it and run again)"); continue; }
                log.Add(AddTo(path, stars));
            }
            AssetDatabase.SaveAssets();
            foreach (var old in OldAssets)
            {
                if (AssetDatabase.LoadMainAssetAtPath(old) != null && AssetDatabase.MoveAssetToTrash(old)) log.Add(old + " to the recycle bin");
            }
            Debug.Log("[Tired Pumpkins] " + string.Join("; ", log));
        }

        private static string AddTo(string path, GameObject stars)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                string note = "";
                var old = root.transform.Find(OldEffectName);
                if (old != null)
                {
                    Object.DestroyImmediate(old.gameObject);
                    note = ", old sweat effect off";
                }
                var runner = new SerializedObject(root.GetComponent<RunAwayPumpkin>());
                runner.FindProperty("tiredStars").objectReferenceValue = stars;
                runner.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                return root.name + note;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}

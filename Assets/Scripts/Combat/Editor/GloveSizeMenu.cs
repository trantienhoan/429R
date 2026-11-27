using UnityEditor;
using UnityEngine;

namespace Game.Combat.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Combat > Gloves: 20% Bigger (re-runnable; "Gloves: Old Size" undoes it). User 2026-11-27:
    /// "increase base scale of gloves, make them bigger 20% to now". The glove on the hand takes its size from its prefab's
    /// own scale (Fist: Glove Scale, then grown by Fist Size and Hot Hands), and its punch shape is made from its colliders
    /// at that size, so both grow. The six prefabs (Boxing, Throwing, Soldier; left and right) were scale 1; their pivot is
    /// the knuckles, so they grow back towards the wrist. Prefabs only. (The gloves already hanging on the Tip Knight were
    /// copied at the old size when Set Up Gloves built them.)
    /// </summary>
    internal static class GloveSizeMenu
    {
        private const float Bigger = 1.2f;
        private static readonly string[] Prefabs =
        {
            "Assets/Prefabs/Player/Gloves/Glove_Boxing_L.prefab", "Assets/Prefabs/Player/Gloves/Glove_Boxing_R.prefab",
            "Assets/Prefabs/Player/Gloves/Glove_Throwing_L.prefab", "Assets/Prefabs/Player/Gloves/Glove_Throwing_R.prefab",
            "Assets/Prefabs/Player/Gloves/Glove_Soldier_L.prefab", "Assets/Prefabs/Player/Gloves/Glove_Soldier_R.prefab",
        };

        [MenuItem("Tools/429 Game/Combat/Gloves: 20% Bigger")]
        private static void Big() => SetSize(Bigger);

        [MenuItem("Tools/429 Game/Combat/Gloves: Old Size")]
        private static void Old() => SetSize(1f);

        private static void SetSize(float size)
        {
            int done = 0;
            foreach (var path in Prefabs)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    root.transform.localScale = Vector3.one * size;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    done++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            Debug.Log($"[Gloves] {done} glove prefabs at scale {size}.");
        }
    }
}

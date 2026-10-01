using System.Collections.Generic;
using Game.Enemies;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Game.EditorTools
{
    /// <summary>Menu command that makes enemies land on the NavMesh when they spawn (Tools > 429 Game > Enemies).</summary>
    public static class LandOnNavMeshSetupMenu
    {
        private const string EnemyFolder = "Assets/Prefabs/Enemies";

        /// <summary>Adds Land On NavMesh to every enemy prefab whose root has a NavMesh Agent.</summary>
        [MenuItem("Tools/429 Game/Enemies/Keep Enemies On NavMesh")]
        public static void AddLandOnNavMesh()
        {
            var added = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { EnemyFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (root.GetComponent<NavMeshAgent>() == null || root.GetComponent<LandOnNavMesh>() != null) continue;

                    root.AddComponent<LandOnNavMesh>();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    added.Add(root.name);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            Debug.Log(added.Count > 0
                ? $"[Enemies] These now land on the NavMesh when they appear: {string.Join(", ", added)}."
                : "[Enemies] Every enemy with a NavMesh Agent already has Land On NavMesh.");
        }
    }
}

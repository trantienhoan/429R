using System.Collections.Generic;
using Game.Enemies;
using Game.VFX;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Fixes from the user's 2026-11-26 night run (record 22-25-49 and its Player.log). Re-runnable, prefabs/assets only:
    /// - Tools > 429 Game > Enemies > Cockroach Mini Bosses: Leap When Stuck (user: "about the mini_boss cockroach,
    ///   sometimes they still stucks at the edge of the map, add that when the cockroach feel stucked, make them fly a
    ///   bit and jump on Player"): Stuck Leap on Cockroach_Boss and Cockroach_Boss_PumpkinButt (Spider Brain).
    /// - Tools > 429 Game > Fixes > Readable Meshes For Jiggling Colliders: a Mesh Collider under a Jiggle is scaled
    ///   unevenly while it wobbles, which a build can only cook from a readable mesh (the log: "This Mesh Collider is
    ///   attached to ... Pumpkin_5(Clone)/Jiggle/pumpkin_5/witch_hat ... doesn't have Read/Write enabled", 89 times):
    ///   Read/Write on those meshes' models.
    /// </summary>
    public static class StuckLeapMenu
    {
        private static readonly string[] Roaches =
        {
            "Assets/Prefabs/Enemies/Enemies_Smarter/Cockroach_Boss.prefab",
            "Assets/Prefabs/Enemies/Enemies_Smarter/Cockroach_Boss_PumpkinButt.prefab",
        };

        [MenuItem("Tools/429 Game/Enemies/Cockroach Mini Bosses: Leap When Stuck")]
        public static void CockroachesLeap()
        {
            var notes = new List<string>();
            foreach (var path in Roaches)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { notes.Add(path + " missing"); continue; }
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var brain = root.GetComponent<SpiderBrain>();
                    if (brain == null) { notes.Add(root.name + ": no Spider Brain"); continue; }
                    var so = new SerializedObject(brain);
                    var leap = so.FindProperty("stuckLeap");
                    if (leap.boolValue) { notes.Add(root.name + " already"); continue; }
                    leap.boolValue = true;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    notes.Add(root.name + " leaps when stuck");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            Debug.Log("[Spider Brain] " + string.Join("; ", notes) + ". Prefabs only.");
        }

        [MenuItem("Tools/429 Game/Fixes/Readable Meshes For Jiggling Colliders")]
        public static void ReadableJiggleMeshes()
        {
            var models = new HashSet<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (prefab == null) continue;
                foreach (var jiggle in prefab.GetComponentsInChildren<Jiggle>(true))
                foreach (var collider in jiggle.GetComponentsInChildren<MeshCollider>(true))
                {
                    if (collider.sharedMesh == null || collider.sharedMesh.isReadable) continue;
                    var model = AssetDatabase.GetAssetPath(collider.sharedMesh);
                    if (AssetImporter.GetAtPath(model) is ModelImporter) models.Add(model);
                }
            }
            var done = new List<string>();
            foreach (var model in models)
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(model);
                if (importer.isReadable) continue;
                importer.isReadable = true;
                importer.SaveAndReimport();
                done.Add(System.IO.Path.GetFileName(model));
            }
            Debug.Log(done.Count > 0
                ? $"[Fixes] Read/Write on {done.Count} models with jiggling colliders: {string.Join(", ", done)}."
                : "[Fixes] Every jiggling collider's mesh is readable already.");
        }

        [MenuItem("Tools/429 Game/Enemies/Cockroach Mini Bosses: Leap When Stuck", true)]
        [MenuItem("Tools/429 Game/Fixes/Readable Meshes For Jiggling Colliders", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}

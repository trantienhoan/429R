using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// User 2026-11-26: "help me re-import Twigs back in unity" (after editing the nest in Exports/Lair/Lair_Nest.blend).
    /// Tools > 429 Game > Lair > Use Blender Nest Twigs (re-runnable): Assets/Models/Lair/Lair_Nest_Twigs.fbx (exported by
    /// Exports/Lair/export_nest_twigs.py: -Z forward, Y up, Apply Transform, in the nest prefab's root layout) becomes the
    /// twig bowl of Prefabs/Lair/Lair_Nest. Its Twigs object gets the FBX mesh and a scale that undoes the Nest's x1.35, so
    /// the bowl sits exactly as in Blender. The big bedroom nest (Lair_Nest_Big) and the roach nests (Lair_Roach_Nest) hold
    /// Lair_Nest inside them, so they follow. A new export of the FBX updates all of them with no menu run; Set Up Lair
    /// Story Props keeps the FBX too (LairDecor uses it when it is there).
    /// </summary>
    public static class LairNestTwigsMenu
    {
        private const string Tag = "[Lair Nest Twigs]";
        internal const string FbxPath = "Assets/Models/Lair/Lair_Nest_Twigs.fbx";
        private const string PrefabPath = "Assets/Prefabs/Lair/Lair_Nest.prefab";

        [MenuItem("Tools/429 Game/Lair/Use Blender Nest Twigs")]
        public static void Run()
        {
            if (!PrepareFbx(out var mesh, out string why))
            {
                Debug.LogError($"{Tag} {why}; nothing changed.");
                return;
            }
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var twigs = root.transform.Find("Nest/Twigs");
                if (twigs == null || !twigs.TryGetComponent(out MeshFilter filter))
                {
                    Debug.LogError($"{Tag} {PrefabPath} has no Nest/Twigs with a Mesh Filter; nothing changed.");
                    return;
                }
                string before = filter.sharedMesh != null ? filter.sharedMesh.name : "none";
                Use(twigs.gameObject, mesh);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"{Tag} Lair_Nest's Twigs: {before} -> {mesh.name} from {FbxPath} ({mesh.vertexCount} verts, " +
                          $"{mesh.triangles.Length / 3} tris), scale {twigs.localScale.x:F4} under the Nest's " +
                          $"{twigs.parent.localScale.x:F2}. Lair_Nest_Big and Lair_Roach_Nest follow (they hold Lair_Nest).");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>The Twigs object shows the FBX mesh at the size it has in Blender (its parent Nest is scaled up).</summary>
        internal static void Use(GameObject twigs, Mesh mesh)
        {
            twigs.GetComponent<MeshFilter>().sharedMesh = mesh;
            var parent = twigs.transform.parent;
            float up = parent != null ? parent.localScale.x : 1f;
            twigs.transform.localPosition = Vector3.zero;
            twigs.transform.localRotation = Quaternion.identity;
            twigs.transform.localScale = Vector3.one / Mathf.Max(0.0001f, up);
        }

        /// <summary>The FBX's mesh, imported as a plain model (no materials, rig or animation: the prefab keeps its own material).</summary>
        internal static bool PrepareFbx(out Mesh mesh, out string why)
        {
            mesh = null;
            if (!(AssetImporter.GetAtPath(FbxPath) is ModelImporter importer))
            {
                why = FbxPath + " is missing (export it with Exports/Lair/export_nest_twigs.py)";
                return false;
            }
            bool changed = false;
            void Set<T>(T now, T want, System.Action<T> apply)
            {
                if (Equals(now, want)) return;
                apply(want);
                changed = true;
            }
            Set(importer.materialImportMode, ModelImporterMaterialImportMode.None, v => importer.materialImportMode = v);
            Set(importer.animationType, ModelImporterAnimationType.None, v => importer.animationType = v);
            Set(importer.importAnimation, false, v => importer.importAnimation = v);
            Set(importer.importBlendShapes, false, v => importer.importBlendShapes = v);
            Set(importer.importCameras, false, v => importer.importCameras = v);
            Set(importer.importLights, false, v => importer.importLights = v);
            // Smooth twigs like the built ones (Blender's hard edges would make each 3-sided twig look faceted).
            Set(importer.importNormals, ModelImporterNormals.Calculate, v => importer.importNormals = v);
            Set(importer.normalSmoothingAngle, 180f, v => importer.normalSmoothingAngle = v);
            Set(importer.importTangents, ModelImporterTangents.CalculateMikk, v => importer.importTangents = v);
            Set(importer.globalScale, 1f, v => importer.globalScale = v);
            Set(importer.useFileScale, true, v => importer.useFileScale = v);
            if (changed) importer.SaveAndReimport();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(FbxPath))
                if (asset is Mesh m) { mesh = m; break; }
            why = mesh == null ? FbxPath + " has no mesh" : null;
            return mesh != null;
        }

        [MenuItem("Tools/429 Game/Lair/Use Blender Nest Twigs", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}

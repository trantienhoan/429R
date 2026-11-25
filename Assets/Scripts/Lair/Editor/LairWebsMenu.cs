using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Lair > Set Up Lair Webs (user, 2026-11-25: the storage room as the first pumpkin lair,
    /// "still need more web stuffs", "remodel them instead of creating new"). The webs and egg sacs were remodelled in
    /// Blender from our own Spider_Web and Web_Ball (Exports/Lair/make_lair_webs.py -> Assets/Models/Lair/*.fbx). This
    /// menu (re-runnable) sets their import and makes the prefabs in Prefabs/Lair/ in the user's style (2026-11-25: "I
    /// want your new props follow my style and texture"): the webs wear Spider_Web's own material (Bedroom_Set_1_Mat,
    /// the furniture atlas); the user then put every piece, the egg sacs too, on Spider_Web's grey patch by hand in
    /// Exports/Lair/Lair_Props.blend (re-export it with Exports/Lair/export_lair_props.py). The prefabs:
    /// the 6 pieces plus Lair_Web_Strand_Sac (a sac on a 0.6 m thread). Only looks: no colliders, no shadows.
    /// </summary>
    public static class LairWebsMenu
    {
        private const string Tag = "[Lair Webs]";
        private const string ModelFolder = "Assets/Models/Lair/";
        private const string TextureFolder = "Assets/M_Textures/Lair/";
        private const string MaterialFolder = "Assets/Materials/Lair/";
        private const string PrefabFolder = "Assets/Prefabs/Lair/";
        private static readonly string[] Pieces =
            { "Lair_Web_Round", "Lair_Web_Corner", "Lair_Web_Sheet", "Lair_Web_Strand", "Lair_Egg_Sac", "Lair_Egg_Sac_Cluster" };
        // A sac hanging on a thread this long (metres).
        private const float ThreadLength = 0.6f;

        [MenuItem("Tools/429 Game/Lair/Set Up Lair Webs")]
        public static void SetUp()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError(Tag + " Stop play mode first; nothing changed.");
                return;
            }
            var done = new List<string>();
            foreach (var piece in Pieces) Import(ModelFolder + piece + ".fbx", done);
            var web = UserWebMaterial();
            if (web == null)
            {
                Debug.LogError(Tag + " Spider_Web's material (Bedroom_Set_1_Mat) wasn't found; nothing changed.");
                return;
            }
            // The earlier materials and the orange sac textures are no longer used (the user put every piece on
            // Spider_Web's grey patch, 2026-11-25).
            foreach (var old in new[] { MaterialFolder + "Lair_Web_Mat.mat", MaterialFolder + "Lair_Egg_Sac_Mat.mat",
                                        TextureFolder + "Lair_Egg_Sac.png", TextureFolder + "Lair_Egg_Sac_Glow.png" })
            {
                if (AssetDatabase.LoadMainAssetAtPath(old) == null) continue;
                AssetDatabase.DeleteAsset(old);
                done.Add("removed " + Path.GetFileName(old));
            }
            Directory.CreateDirectory(PrefabFolder);
            var made = new List<string>();
            foreach (var piece in Pieces)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelFolder + piece + ".fbx");
                if (model == null)
                {
                    Debug.LogError($"{Tag} {ModelFolder}{piece}.fbx is missing (run Exports/Lair/make_lair_webs.py).");
                    continue;
                }
                var root = new GameObject(piece);
                try
                {
                    AddLook(root.transform, model, new[] { web });
                    Save(root, piece, made);
                }
                finally
                {
                    Object.DestroyImmediate(root);
                }
            }
            // A sac on a thread: the thread from the root down, the sac's tip at its end.
            var thread = AssetDatabase.LoadAssetAtPath<GameObject>(ModelFolder + "Lair_Web_Strand.fbx");
            var oneSac = AssetDatabase.LoadAssetAtPath<GameObject>(ModelFolder + "Lair_Egg_Sac.fbx");
            if (thread != null && oneSac != null)
            {
                var root = new GameObject("Lair_Web_Strand_Sac");
                try
                {
                    var t = AddLook(root.transform, thread, new[] { web });
                    t.localScale = new Vector3(1f, ThreadLength, 1f);
                    var s = AddLook(root.transform, oneSac, new[] { web });
                    s.localPosition = new Vector3(0f, -ThreadLength, 0f);
                    Save(root, "Lair_Web_Strand_Sac", made);
                }
                finally
                {
                    Object.DestroyImmediate(root);
                }
            }
            if (made.Count > 0) done.Add("prefabs " + string.Join(", ", made));
            AssetDatabase.SaveAssets();
            Debug.Log(Tag + " " + string.Join("; ", done) + ". No scene change.");
        }

        [MenuItem("Tools/429 Game/Lair/Set Up Lair Webs", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

        // The model's meshes only (no animation, no materials of its own: ours are given in the prefab).
        private static void Import(string path, List<string> done)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) return;
            bool changed = importer.materialImportMode != ModelImporterMaterialImportMode.None || importer.importAnimation ||
                           importer.importCameras || importer.importLights || importer.addCollider;
            if (!changed) return;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.addCollider = false;
            importer.SaveAndReimport();
            done.Add("import " + Path.GetFileNameWithoutExtension(path));
        }

        private static Transform AddLook(Transform parent, GameObject model, Material[] materials)
        {
            var look = (GameObject)PrefabUtility.InstantiatePrefab(model);
            look.name = model.name;
            look.transform.SetParent(parent, false);
            foreach (var r in look.GetComponentsInChildren<Renderer>(true))
            {
                var slots = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < slots.Length; i++) slots[i] = materials[Mathf.Min(i, materials.Length - 1)];
                r.sharedMaterials = slots;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = true;
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            return look.transform;
        }

        private static void Save(GameObject root, string name, List<string> made)
        {
            string path = PrefabFolder + name + ".prefab";
            bool had = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
            PrefabUtility.SaveAsPrefabAsset(root, path);
            if (!had) made.Add(name);
        }

        // The material the user's own Spider_Web wears (the furniture atlas).
        private static Material UserWebMaterial()
        {
            var web = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/BedRoom/Spider_Web.prefab");
            var look = web != null ? web.GetComponentInChildren<MeshRenderer>(true) : null;
            return look != null ? look.sharedMaterial : null;
        }
    }
}

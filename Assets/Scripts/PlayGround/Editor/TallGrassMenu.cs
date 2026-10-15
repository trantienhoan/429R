using Game.VFX;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Game.PlayGround.Editor
{
    /// <summary>
    /// Tools > 429 Game > PlayGround > Make Tall Grass: Prefabs/Foliage/Set_1/Tall_Grass, a clump of the
    /// Foliage_Set_1_Tall_Grass blades (each on a pivot at its foot) that sways lightly and ducks shyly when the player
    /// comes close (Shy Grass), and hides a laughing pumpkin while it's standing (Hideout). Running it again remakes it.
    /// </summary>
    public static class TallGrassMenu
    {
        private const string Model = "Assets/Models/Foliage_Set_1/Foliage_Set_1_Tall_Grass.fbx";
        private const string PrefabPath = "Assets/Prefabs/Foliage/Set_1/Tall_Grass.prefab";
        private const int InnerBlades = 4;
        private const int OuterBlades = 8;

        [MenuItem("Tools/429 Game/PlayGround/Make Tall Grass")]
        public static void Make()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            var filter = source != null ? source.GetComponentInChildren<MeshFilter>() : null;
            var look = source != null ? source.GetComponentInChildren<MeshRenderer>() : null;
            if (filter == null || look == null)
            {
                Debug.LogError("[Tall Grass] " + Model + " has no mesh.");
                return;
            }

            // Built in a preview scene, so the open scene isn't touched.
            var preview = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Tall_Grass");
            SceneManager.MoveGameObjectToScene(root, preview);
            try
            {
                // The same clump every time it's remade.
                var random = new System.Random(429);
                float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);

                int n = 0;
                for (int ring = 0; ring < 2; ring++)
                {
                    int count = ring == 0 ? InnerBlades : OuterBlades;
                    for (int i = 0; i < count; i++)
                    {
                        float angle = (i + Range(-0.3f, 0.3f)) * 360f / count + ring * 22f;
                        float radius = ring == 0 ? Range(0.08f, 0.16f) : Range(0.26f, 0.38f);
                        var outward = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;

                        var pivot = new GameObject("Blade_" + (++n).ToString("00")).transform;
                        pivot.SetParent(root.transform, false);
                        pivot.localPosition = outward * radius;
                        // Turned any way, leaning a little out from the middle, some taller than others.
                        var lean = Quaternion.AngleAxis(Range(3f, 10f) * (ring == 0 ? 0.5f : 1f), Vector3.Cross(Vector3.up, outward));
                        pivot.localRotation = lean * Quaternion.Euler(0f, Range(0f, 360f), 0f);
                        pivot.localScale = Vector3.one * Range(0.65f, 0.95f);

                        var blade = new GameObject("Foliage_Set_1_Tall_Grass");
                        blade.transform.SetParent(pivot, false);
                        blade.transform.localRotation = filter.transform.localRotation;
                        blade.transform.localScale = filter.transform.localScale;
                        blade.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                        var shown = blade.AddComponent<MeshRenderer>();
                        shown.sharedMaterials = look.sharedMaterials;
                        shown.shadowCastingMode = ShadowCastingMode.Off;
                    }
                }

                // The hiding space for a laughing pumpkin (walked through by everyone; rays pass through it).
                root.layer = LayerMask.NameToLayer("Ignore Raycast");
                var space = root.AddComponent<BoxCollider>();
                space.isTrigger = true;
                space.center = new Vector3(0f, 0.6f, 0f);
                space.size = new Vector3(1.1f, 1.2f, 1.1f);
                root.AddComponent<Hideout>();
                root.AddComponent<ShyGrass>();

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[Tall Grass] made " + PrefabPath + " (" + n + " blades).", prefab);
                EditorGUIUtility.PingObject(prefab);
            }
            finally
            {
                Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        [MenuItem("Tools/429 Game/PlayGround/Make Tall Grass", true)]
        private static bool NotPlaying() => !EditorApplication.isPlaying;
    }
}

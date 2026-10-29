using Game.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Player > Build Fist Punch Impact (user, 2026-10-29: the fist bump's hit where a closed punch
    /// lands, "infront of the fist, so it should be blocked by the fist"). Builds Fist_Punch_Impact (Assets/Prefabs/Player)
    /// from Fist_Bump_Impact (make that first: Punchier Fist Bump) with the same parts and materials, but made to lie on
    /// the surface that was hit: the flash, the blast and the shockwave lie flat on it (facing its Z axis, which the Fist
    /// turns out of the surface), so the fist in front of them hides their middle and the wall never cuts them off; the
    /// rays, embers, stars and smoke burst out of the surface instead of every way. Then it puts it on both Fists as
    /// their Impact Effect (the scene needs saving after). Running it again rebuilds it from the fist bump's hit.
    /// </summary>
    public static class FistPunchImpactMenu
    {
        private const string BumpPath = "Assets/Prefabs/Player/Fist_Bump_Impact.prefab";
        private const string PunchPath = "Assets/Prefabs/Player/Fist_Punch_Impact.prefab";

        [MenuItem("Tools/429 Game/Player/Build Fist Punch Impact")]
        public static void Run()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BumpPath) == null)
            {
                Debug.LogError("[Fist Punch Impact] No " + BumpPath + ": run Tools > 429 Game > Player > Punchier Fist Bump first.");
                return;
            }
            var prefab = Build();
            string result = SetOnFists(prefab);
            AssetDatabase.SaveAssets();
            Debug.Log("[Fist Punch Impact] Built " + PunchPath + "; " + result);
        }

        private static GameObject Build()
        {
            var root = PrefabUtility.LoadPrefabContents(BumpPath);
            try
            {
                root.name = "Fist_Punch_Impact";
                // Flat on the surface.
                foreach (var part in new[] { "Flash", "Burst", "Shockwave" }) Flat(root, part);
                // Out of the surface: a cone along Z this wide (degrees).
                Out(root, "Rays", 75f, 0.02f);
                Out(root, "Embers", 65f, 0.02f);
                Out(root, "Stars", 60f, 0.03f);
                Out(root, "Puff", 50f, 0.03f);
                return PrefabUtility.SaveAsPrefabAsset(root, PunchPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // Lined up with its own transform, a particle shows its face to -Z: turned round, so it faces out of the surface
        // (the root's Z) like the rest.
        private static void Flat(GameObject root, string part)
        {
            var renderer = Part(root, part)?.GetComponent<ParticleSystemRenderer>();
            if (renderer == null) return;
            renderer.alignment = ParticleSystemRenderSpace.Local;
            renderer.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        }

        private static void Out(GameObject root, string part, float angle, float radius)
        {
            var ps = Part(root, part)?.GetComponent<ParticleSystem>();
            if (ps == null) return;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = angle;
            shape.radius = radius;
            shape.rotation = Vector3.zero;
            shape.position = Vector3.zero;
        }

        private static Transform Part(GameObject root, string name)
        {
            var part = root.transform.Find(name);
            if (part == null) Debug.LogWarning("[Fist Punch Impact] Fist_Bump_Impact has no " + name + " any more: left out.");
            return part;
        }

        private static string SetOnFists(GameObject prefab)
        {
            var fists = Object.FindObjectsByType<Fist>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (fists.Length == 0) return "no Fists in the open scene: set Impact Effect by hand";
            foreach (var fist in fists)
            {
                var so = new SerializedObject(fist);
                so.FindProperty("impactEffect").objectReferenceValue = prefab;
                so.ApplyModifiedProperties();
                EditorSceneManager.MarkSceneDirty(fist.gameObject.scene);
            }
            return "set on " + fists.Length + " Fists (save the scene)";
        }
    }
}

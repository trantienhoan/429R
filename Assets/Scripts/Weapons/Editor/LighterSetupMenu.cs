using System.Collections.Generic;
using Game.Lighting;
using Game.Weapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    /// <summary>Menu commands that make the candles lighters (Tools > 429 Game > Items).</summary>
    public static class LighterSetupMenu
    {
        private static readonly string[] CandlePaths =
        {
            "Assets/Prefabs/Light/Candle_Fat.prefab",
            "Assets/Prefabs/Light/Candle_Land.prefab",
            "Assets/Prefabs/Light/Candle_Land_2.prefab",
            "Assets/Prefabs/Light/Pumpkin_Candle.prefab",
        };

        private const string SparkMaterialPath = "Assets/Data/Effects/Lighter Spark.mat";
        private const string SparkTexturePath = "Assets/Lana Studio/Hyper Casual FX/Textures/Circle01.png";
        private const string RelightSound = "Assets/Audio/Kenney Audio/kenney_interface-sounds/Audio/scratch_004.ogg";
        private const string OutSound = "Assets/Audio/SFX/whoosh.wav";

        /// <summary>
        /// Makes Candle_Fat, Candle_Land, Candle_Land_2 and Pumpkin_Candle lighters: their Light Off When Grabbed is
        /// swapped for a Lighter (still goes out when picked up; shake it to light it again; its flame lights boongs),
        /// with sparks at the wick and the relight and blow-out sounds. Run again, it only adds what's missing (tuned
        /// values are kept).
        /// </summary>
        [MenuItem("Tools/429 Game/Items/Make Candles Lighters")]
        public static void MakeCandlesLighters()
        {
            Convert(CandlePaths);
        }

        /// <summary>The same for the candle prefabs selected in the Project window (e.g. Pumpkin_Candle_Purple).</summary>
        [MenuItem("Tools/429 Game/Items/Make Selected Candles Lighters")]
        public static void MakeSelectedCandlesLighters()
        {
            var paths = new List<string>();
            foreach (var go in Selection.GetFiltered<GameObject>(SelectionMode.Assets))
            {
                string path = AssetDatabase.GetAssetPath(go);
                if (path.EndsWith(".prefab")) paths.Add(path);
            }
            if (paths.Count == 0)
            {
                Debug.LogError("[Lighter] Select the candle prefabs in the Project window first.");
                return;
            }
            Convert(paths);
        }

        private static void Convert(IEnumerable<string> paths)
        {
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                Debug.LogError("[Lighter] Close the prefab you're editing (Prefab Mode) first, then run it again.");
                return;
            }

            var notes = new List<string>();
            foreach (string path in paths)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    notes.Add($"WARNING: '{path}' is missing");
                    continue;
                }
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    string done = MakeLighter(root);
                    if (done == null) continue;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    notes.Add($"{root.name}: {done}");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Lighter] Candles:\n- " + (notes.Count > 0 ? string.Join("\n- ", notes) : "nothing to change"));
        }

        // Swaps Light Off When Grabbed for a Lighter on the candle's root. Returns what changed (null: nothing).
        private static string MakeLighter(GameObject root)
        {
            var changes = new List<string>();
            GameObject flame = null;

            var lightOff = root.GetComponent<LightOffWhenGrabbed>();
            if (lightOff != null)
            {
                var flames = new SerializedObject(lightOff).FindProperty("flames");
                if (flames != null && flames.arraySize > 0) flame = flames.GetArrayElementAtIndex(0).objectReferenceValue as GameObject;
                Object.DestroyImmediate(lightOff);
                changes.Add("Light Off When Grabbed removed");
            }

            var lighter = root.GetComponent<Lighter>();
            if (lighter == null)
            {
                lighter = root.AddComponent<Lighter>();
                changes.Add("Lighter added");
            }
            var settings = new SerializedObject(lighter);
            var flameField = settings.FindProperty("flame");
            if (flameField.objectReferenceValue == null)
            {
                if (flame == null)
                {
                    var found = FindChild(root.transform, "CandleFlame");
                    if (found == null)
                    {
                        foreach (var child in root.GetComponentsInChildren<Transform>(true))
                        {
                            if (child != root.transform && child.name.Contains("Flame")) { found = child; break; }
                        }
                    }
                    if (found != null) flame = found.gameObject;
                }
                if (flame == null) changes.Add("WARNING: no flame found, set the Lighter's Flame by hand");
                else
                {
                    flameField.objectReferenceValue = flame;
                    changes.Add($"flame = {flame.name}");
                }
            }
            else flame = flameField.objectReferenceValue as GameObject;

            var sparksField = settings.FindProperty("sparks");
            if (sparksField.objectReferenceValue == null)
            {
                var sparks = FindChild(root.transform, "Sparks");
                if (sparks == null)
                {
                    sparks = MakeSparks(root.transform, flame != null ? flame.transform : null).transform;
                    changes.Add("sparks added at the wick");
                }
                sparksField.objectReferenceValue = sparks.GetComponent<ParticleSystem>();
            }
            SetIfEmpty(settings, "relightSound", AssetDatabase.LoadAssetAtPath<AudioClip>(RelightSound));
            SetIfEmpty(settings, "outSound", AssetDatabase.LoadAssetAtPath<AudioClip>(OutSound));
            settings.ApplyModifiedPropertiesWithoutUndo();

            return changes.Count > 0 ? string.Join(", ", changes) : null;
        }

        // A few tiny hot sparks at the wick (emitted by the Lighter only), falling and fading fast. On the root, not under
        // the flame: they show while it's out.
        private static GameObject MakeSparks(Transform root, Transform flame)
        {
            var go = new GameObject("Sparks");
            go.transform.SetParent(root, false);
            go.transform.localPosition = flame != null ? root.InverseTransformPoint(flame.position) : Vector3.zero;
            // The cone points up.
            go.transform.localRotation = Quaternion.Inverse(root.rotation) * Quaternion.LookRotation(Vector3.up, Vector3.forward);

            var sparks = go.AddComponent<ParticleSystem>();
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = sparks.main;
            main.loop = true;
            main.duration = 1f;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.006f, 0.013f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.45f), new Color(1f, 0.5f, 0.12f));
            main.gravityModifier = 0.6f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 30;
            var emission = sparks.emission;
            emission.enabled = false;
            emission.rateOverTime = 0f;
            var shape = sparks.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.003f;
            var fade = sparks.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.6f, 0.3f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var shrink = sparks.sizeOverLifetime;
            shrink.enabled = true;
            shrink.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = SparkMaterial(root);
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.maxParticleSize = 0.05f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }

        // A soft round dot that adds light (the FX pack's additive shader, as the bottles' glow uses); if that shader is
        // missing, the flame's own material.
        private static Material SparkMaterial(Transform root)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(SparkMaterialPath);
            if (material != null) return material;
            var shader = Shader.Find("Mobile/Particles/Additive");
            if (shader == null)
            {
                var flame = root.GetComponentInChildren<ParticleSystemRenderer>(true);
                return flame != null ? flame.sharedMaterial : null;
            }
            material = new Material(shader) { mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(SparkTexturePath) };
            SetupUtility.EnsureFolder(System.IO.Path.GetDirectoryName(SparkMaterialPath)?.Replace('\\', '/'));
            AssetDatabase.CreateAsset(material, SparkMaterialPath);
            return material;
        }

        private static void SetIfEmpty(SerializedObject settings, string field, Object value)
        {
            var property = settings.FindProperty(field);
            if (property == null || value == null) return;
            if (property.objectReferenceValue == null) property.objectReferenceValue = value;
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child != root && child.name == name) return child;
            }
            return null;
        }
    }
}

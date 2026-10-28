using Game.VFX;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > VFX > Add Running Light Round Lid (user, 2026-10-28: "run a purple light around the lid"): puts
    /// Lid_Running_Light under Rooftop_Base_Z_Lid in Rooftop_Base_Z.prefab. It's a thin ring fitted to the lid's oval
    /// edge, drawn with "429/Effects/Running Glow Ring": a band of glow on the floor just outside the lid, a thinner one
    /// on the lid's outer rim and a low sheet of light rising from the seam. A steady, dim glow (user, 2026-10-28: "we do
    /// not need it to run, just shines alone is okay. dimmer the light is good, because that lid only break in near end
    /// game"). With Running = true it is brighter and a purple head with a fading tail runs round it (RunningGlow), with a
    /// small purple point light (no shadows) riding along. Being under the lid, it goes when the lid breaks (the lid
    /// destroys itself).
    /// Placed in the lid's own space: in the prefab's resting pose the lid lies upside down and the rooftop's arrival
    /// animation turns it over, so in the game its +y is up and its top is flush with the floor. Running it again rebuilds
    /// the ring mesh, the material (Assets/Data/Effects/Lid Running Light.mat) and the object.
    /// </summary>
    public static class LidRunningLightMenu
    {
        private const string PrefabPath = "Assets/Prefabs/Bases/Rooftop_Base_Z.prefab";
        private const string LidName = "Rooftop_Base_Z_Lid";
        private const string RingName = "Lid_Running_Light";
        private const string MaterialPath = "Assets/Data/Effects/Lid Running Light.mat";
        private const string MeshPath = "Assets/Data/Effects/Lid Running Light Ring.asset";
        private const string ShaderName = "429/Effects/Running Glow Ring";
        private const int Segments = 128;
        private const float FloorBand = 0.4f;     // metres of glow on the floor outside the lid
        private const float RimBand = 0.15f;      // metres of glow on the lid's own rim
        private const float SheetHeight = 0.22f;  // metres of the sheet of light rising from the seam
        private static readonly Color Purple = new(0.62f, 0.24f, 1f);
        private static readonly bool Running = false;   // true = the running head and its light come back (and it's brighter)

        [MenuItem("Tools/429 Game/VFX/Add Running Light Round Lid")]
        public static void Run()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == PrefabPath)
            {
                Debug.LogWarning("[Lid Running Light] Rooftop_Base_Z is open in Prefab Mode: close it and run again");
                return;
            }
            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError("[Lid Running Light] shader " + ShaderName + " not found");
                return;
            }
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Transform lid = null;
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == LidName) lid = t;
                if (lid == null)
                {
                    Debug.LogError("[Lid Running Light] no " + LidName + " in " + PrefabPath);
                    return;
                }
                var old = lid.Find(RingName);
                if (old != null) Object.DestroyImmediate(old.gameObject);

                // The lid's disc in its own space: the top's centre and its two radii in metres (it is slightly oval).
                var bounds = lid.GetComponent<MeshFilter>().sharedMesh.bounds;
                var scale = lid.lossyScale;
                float radiusX = bounds.extents.x * Mathf.Abs(scale.x);
                float radiusZ = bounds.extents.z * Mathf.Abs(scale.z);

                var ring = new GameObject(RingName);
                ring.layer = lid.gameObject.layer;
                ring.transform.SetParent(lid, false);
                ring.transform.localPosition = new Vector3(bounds.center.x, bounds.max.y + 0.006f / Mathf.Abs(scale.y), bounds.center.z);
                ring.transform.localRotation = Quaternion.identity;
                ring.transform.localScale = new Vector3(1f / Mathf.Abs(scale.x), 1f / Mathf.Abs(scale.y), 1f / Mathf.Abs(scale.z));

                ring.AddComponent<MeshFilter>().sharedMesh = BuildMesh(radiusX, radiusZ);
                var renderer = ring.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = BuildMaterial(shader);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

                if (Running) AddRunner(ring, (radiusX + radiusZ) * 0.5f + 0.15f);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[Lid Running Light] added under " + LidName + ": lid radii " + radiusX.ToString("F3") + " x " + radiusZ.ToString("F3") + " m");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
        }

        // The running head: RunningGlow moves it round and carries a small purple point light along with it.
        private static void AddRunner(GameObject ring, float lightRadius)
        {
            var lightObject = new GameObject("Runner Light");
            lightObject.transform.SetParent(ring.transform, false);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = Purple;
            light.intensity = 2.5f;
            light.range = 2.5f;
            light.shadows = LightShadows.None;

            var glow = ring.AddComponent<RunningGlow>();
            var so = new SerializedObject(glow);
            so.FindProperty("runner").objectReferenceValue = light;
            so.FindProperty("lightRadius").floatValue = lightRadius;
            so.FindProperty("lightHeight").floatValue = 0.3f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Three strips round the oval edge, each from the seam (UV y = 0, brightest) to where its glow ends (UV y = 1):
        // out over the floor, in over the lid's rim, and up from the seam. UV x = how far round (0-1, from +x towards
        // +z); vertex alpha = the strip's strength.
        private static Mesh BuildMesh(float radiusX, float radiusZ)
        {
            // (step out from the edge at the start, at the end, height at the end, strength)
            var strips = new[]
            {
                new Vector4(0f, FloorBand, 0f, 1f),
                new Vector4(0f, -RimBand, 0f, 0.8f),
                new Vector4(0.01f, 0.01f, SheetHeight, 0.5f),
            };
            int ringVerts = Segments + 1;
            var vertices = new Vector3[ringVerts * 2 * strips.Length];
            var uvs = new Vector2[vertices.Length];
            var colours = new Color[vertices.Length];
            var triangles = new int[Segments * 6 * strips.Length];
            int k = 0;
            for (int s = 0; s < strips.Length; s++)
            {
                int a0 = s * 2 * ringVerts, b0 = a0 + ringVerts;
                var strength = new Color(1f, 1f, 1f, strips[s].w);
                for (int i = 0; i < ringVerts; i++)
                {
                    float u = (float)i / Segments;
                    float angle = u * Mathf.PI * 2f;
                    var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    // The oval's radius in this direction.
                    float edge = radiusX * radiusZ / Mathf.Sqrt(Mathf.Pow(radiusZ * dir.x, 2f) + Mathf.Pow(radiusX * dir.z, 2f));
                    vertices[a0 + i] = dir * (edge + strips[s].x);
                    vertices[b0 + i] = dir * (edge + strips[s].y) + Vector3.up * strips[s].z;
                    uvs[a0 + i] = new Vector2(u, 0f);
                    uvs[b0 + i] = new Vector2(u, 1f);
                    colours[a0 + i] = colours[b0 + i] = strength;
                }
                for (int i = 0; i < Segments; i++)
                {
                    triangles[k++] = a0 + i; triangles[k++] = b0 + i; triangles[k++] = a0 + i + 1;
                    triangles[k++] = a0 + i + 1; triangles[k++] = b0 + i; triangles[k++] = b0 + i + 1;
                }
            }
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (mesh == null)
            {
                mesh = new Mesh { name = "Lid Running Light Ring" };
                AssetDatabase.CreateAsset(mesh, MeshPath);
            }
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.colors = colours;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static Material BuildMaterial(Shader shader)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.shader = shader;
            // Steady: dim, under the bloom threshold, so it doesn't call attention long before the lid can break.
            // Running: HDR so the head blooms, with the glow all round low enough that the head stands out.
            float brightness = Running ? 2.6f : 1f;
            material.SetColor("_Color", new Color(Purple.r * brightness, Purple.g * brightness, Purple.b * brightness, 1f));
            material.SetFloat("_BaseGlow", Running ? 0.12f : 0.22f);
            material.SetFloat("_Comet", Running ? 1f : 0f);
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}

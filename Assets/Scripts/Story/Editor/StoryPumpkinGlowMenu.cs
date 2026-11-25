using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Story;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Story > Set Up Laughing Pumpkin Glow (user, 2026-11-25: the laughing pumpkin holds a piece of
    /// the Green Seed and takes it in until it turns giant). Re-runnable, prefabs and assets only (no scene change):
    /// - the face: the candle-lit (yellow) parts of the runners' painted face, found in their texture, become a mask
    ///   (M_Textures/Story/Runner_Face_Glow_Mask.png) and a mesh of only the face's triangles, lifted 1.5 mm off the
    ///   surface (Models/Story/Runner_Face_Glow_Mesh.asset), drawn additive (Materials/Story/Runner_Face_Glow_Mat);
    /// - Resources/Story Pumpkin Glow Settings (tuned values stay);
    /// - a Story Pumpkin Glow on the 9 runner prefabs (Pumpkin_0/1/2/4/6 and their _Chaos copies).
    /// Their Warn Before Transform action feeds it the countdown to the giant.
    /// </summary>
    public static class StoryPumpkinGlowMenu
    {
        private const string Tag = "[Story Pumpkin Glow]";
        private const string Folder = "Assets/Prefabs/Items/Breakable/";
        private static readonly string[] Runners =
        {
            "Pumpkin_0", "Pumpkin_1", "Pumpkin_2", "Pumpkin_4", "Pumpkin_6",
            "Pumpkin_1_Chaos", "Pumpkin_2_Chaos", "Pumpkin_4_Chaos", "Pumpkin_6_Chaos",
        };
        private const string MaskPath = "Assets/M_Textures/Story/Runner_Face_Glow_Mask.png";
        private const string MeshPath = "Assets/Models/Story/Runner_Face_Glow_Mesh.asset";
        private const string MaterialPath = "Assets/Materials/Story/Runner_Face_Glow_Mat.mat";
        private const string SettingsPath = "Assets/Resources/Story Pumpkin Glow Settings.asset";
        private const string SeedPath = "Assets/Prefabs/Items/Chest/Seed_Bracer.prefab";
        // The face sits this far off the pumpkin's surface, in metres.
        private const float Lift = 0.0015f;
        private const int Pad = 6;

        [MenuItem("Tools/429 Game/Story/Set Up Laughing Pumpkin Glow")]
        public static void SetUp()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError(Tag + " Stop play mode first; nothing changed.");
                return;
            }
            var done = new List<string>();
            var sample = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Pumpkin_1.prefab");
            var body = sample != null ? Body(sample) : null;
            var filter = body != null ? body.GetComponent<MeshFilter>() : null;
            if (filter == null || filter.sharedMesh == null || body.sharedMaterial == null)
            {
                Debug.LogError(Tag + " Pumpkin_1 has no pumpkin body mesh; nothing changed.");
                return;
            }
            var atlas = body.sharedMaterial.GetTexture("_BaseMap") as Texture2D;
            var atlasPath = atlas != null ? AssetDatabase.GetAssetPath(atlas) : null;
            if (string.IsNullOrEmpty(atlasPath) || !File.Exists(atlasPath))
            {
                Debug.LogError(Tag + " The pumpkin's texture file wasn't found; nothing changed.");
                return;
            }

            var (mesh, mask, faces) = MakeFace(filter.sharedMesh, atlasPath, body.transform.lossyScale.x, done);
            if (mesh == null) return;
            var material = MakeMaterial(mask, done);
            var settings = AssetDatabase.LoadAssetAtPath<StoryPumpkinGlowSettings>(SettingsPath);
            if (settings == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
                settings = ScriptableObject.CreateInstance<StoryPumpkinGlowSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
                done.Add("made " + SettingsPath);
            }
            settings.Fill(mesh, material, AssetDatabase.LoadAssetAtPath<GameObject>(SeedPath));
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            var added = new List<string>();
            foreach (var name in Runners)
            {
                string path = Folder + name + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    Debug.LogWarning(Tag + " " + path + " is missing (skipped).");
                    continue;
                }
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (root.GetComponent<StoryPumpkinGlow>() != null) continue;
                    root.AddComponent<StoryPumpkinGlow>();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    added.Add(name);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            if (added.Count > 0) done.Add("Story Pumpkin Glow on " + string.Join(", ", added));
            Debug.Log($"{Tag} The face: {faces} triangles. " + string.Join("; ", done) + ". No scene change.");
        }

        [MenuItem("Tools/429 Game/Story/Set Up Laughing Pumpkin Glow", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

        private static MeshRenderer Body(GameObject prefab)
        {
            foreach (var look in prefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (look.gameObject.name.StartsWith("pumpkin", System.StringComparison.OrdinalIgnoreCase)) return look;
            }
            return null;
        }

        // How much a painted colour is the face's candle light: bright yellow (not the orange skin, the dark carving or
        // the olive stem). 0 to 1.
        private static float Candle(Color32 c)
        {
            float r = c.r / 255f, g = c.g / 255f, b = c.b / 255f;
            float yellow = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.62f, 0.78f, g / Mathf.Max(r, 0.01f)));
            float bright = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.65f, g));
            float red = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 0.75f, r));
            float notBlue = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.55f, b));
            return yellow * bright * red * notBlue;
        }

        private static (Mesh, Texture2D, int) MakeFace(Mesh source, string atlasPath, float bodyScale, List<string> done)
        {
            var atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!atlas.LoadImage(File.ReadAllBytes(atlasPath)))
            {
                Debug.LogError(Tag + " Couldn't read " + atlasPath + "; nothing changed.");
                return (null, null, 0);
            }
            int width = atlas.width, height = atlas.height;
            var pixels = atlas.GetPixels32();
            Object.DestroyImmediate(atlas);

            var vertices = source.vertices;
            var normals = source.normals;
            var uvs = source.uv;
            var triangles = source.triangles;
            var keep = new List<int>();
            int minX = width, minY = height, maxX = 0, maxY = 0;
            for (int t = 0; t < triangles.Length; t += 3)
            {
                var a = Pixel(uvs[triangles[t]], width, height);
                var b = Pixel(uvs[triangles[t + 1]], width, height);
                var c = Pixel(uvs[triangles[t + 2]], width, height);
                if (MostCandle(a, b, c, pixels, width, height) < 0.25f) continue;
                keep.Add(t);
                foreach (var p in new[] { a, b, c })
                {
                    minX = Mathf.Min(minX, Mathf.FloorToInt(p.x));
                    minY = Mathf.Min(minY, Mathf.FloorToInt(p.y));
                    maxX = Mathf.Max(maxX, Mathf.CeilToInt(p.x));
                    maxY = Mathf.Max(maxY, Mathf.CeilToInt(p.y));
                }
            }
            if (keep.Count == 0)
            {
                Debug.LogError(Tag + " No candle-lit face found in the pumpkin's texture; nothing changed.");
                return (null, null, 0);
            }
            minX = Mathf.Max(0, minX - Pad);
            minY = Mathf.Max(0, minY - Pad);
            maxX = Mathf.Min(width - 1, maxX + Pad);
            maxY = Mathf.Min(height - 1, maxY + Pad);
            int cropW = maxX - minX + 1, cropH = maxY - minY + 1;

            // The mask: the candle light, softened a little at its edges.
            var raw = new float[cropW * cropH];
            for (int y = 0; y < cropH; y++)
                for (int x = 0; x < cropW; x++)
                    raw[y * cropW + x] = Candle(pixels[(minY + y) * width + minX + x]);
            var maskPixels = new Color32[cropW * cropH];
            for (int y = 0; y < cropH; y++)
            {
                for (int x = 0; x < cropW; x++)
                {
                    float sum = 0f;
                    int n = 0;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int sx = x + dx, sy = y + dy;
                            if (sx < 0 || sy < 0 || sx >= cropW || sy >= cropH) continue;
                            sum += raw[sy * cropW + sx];
                            n++;
                        }
                    byte v = (byte)Mathf.RoundToInt(255f * Mathf.Max(raw[y * cropW + x], sum / n));
                    maskPixels[y * cropW + x] = new Color32(v, v, v, 255);
                }
            }
            var mask = new Texture2D(cropW, cropH, TextureFormat.RGBA32, false);
            mask.SetPixels32(maskPixels);
            mask.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(MaskPath));
            bool hadMask = File.Exists(MaskPath);
            File.WriteAllBytes(MaskPath, mask.EncodeToPNG());
            Object.DestroyImmediate(mask);
            AssetDatabase.ImportAsset(MaskPath, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(MaskPath) is TextureImporter importer)
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.alphaSource = TextureImporterAlphaSource.None;
                importer.maxTextureSize = 512;
                importer.SaveAndReimport();
            }
            if (!hadMask) done.Add("made " + MaskPath + $" ({cropW}x{cropH})");

            // The face mesh: the kept triangles, lifted off the surface, their UVs on the mask.
            var map = new Dictionary<int, int>();
            var outVerts = new List<Vector3>();
            var outNormals = new List<Vector3>();
            var outUvs = new List<Vector2>();
            var outTris = new List<int>();
            float lift = Lift / Mathf.Max(0.0001f, bodyScale);
            foreach (int t in keep)
            {
                for (int k = 0; k < 3; k++)
                {
                    int i = triangles[t + k];
                    if (!map.TryGetValue(i, out int j))
                    {
                        j = outVerts.Count;
                        map[i] = j;
                        var normal = normals.Length > i ? normals[i] : Vector3.zero;
                        outVerts.Add(vertices[i] + normal * lift);
                        outNormals.Add(normal);
                        outUvs.Add(new Vector2((uvs[i].x * width - minX) / cropW, (uvs[i].y * height - minY) / cropH));
                    }
                    outTris.Add(j);
                }
            }
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            bool newMesh = mesh == null;
            if (newMesh) mesh = new Mesh();
            mesh.Clear();
            mesh.name = "Runner_Face_Glow_Mesh";
            mesh.SetVertices(outVerts);
            mesh.SetNormals(outNormals);
            mesh.SetUVs(0, outUvs);
            mesh.SetTriangles(outTris, 0);
            mesh.RecalculateBounds();
            if (newMesh)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(MeshPath));
                AssetDatabase.CreateAsset(mesh, MeshPath);
                done.Add("made " + MeshPath);
            }
            else EditorUtility.SetDirty(mesh);
            return (mesh, AssetDatabase.LoadAssetAtPath<Texture2D>(MaskPath), keep.Count);
        }

        private static Vector2 Pixel(Vector2 uv, int width, int height) =>
            new(Mathf.Repeat(uv.x, 1f) * width, Mathf.Repeat(uv.y, 1f) * height);

        // The strongest candle light inside a triangle of the texture (its pixels, or its corners if it's tiny).
        private static float MostCandle(Vector2 a, Vector2 b, Vector2 c, Color32[] pixels, int width, int height)
        {
            float most = 0f;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))));
            int x1 = Mathf.Min(width - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))));
            int y1 = Mathf.Min(height - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))));
            float area = Cross(b - a, c - a);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float w0 = Cross(b - p, c - p), w1 = Cross(c - p, a - p), w2 = Cross(a - p, b - p);
                    bool inside = area >= 0f ? w0 >= 0f && w1 >= 0f && w2 >= 0f : w0 <= 0f && w1 <= 0f && w2 <= 0f;
                    if (!inside) continue;
                    most = Mathf.Max(most, Candle(pixels[y * width + x]));
                }
            }
            foreach (var p in new[] { a, b, c })
            {
                int x = Mathf.Clamp((int)p.x, 0, width - 1), y = Mathf.Clamp((int)p.y, 0, height - 1);
                most = Mathf.Max(most, Candle(pixels[y * width + x]));
            }
            return most;
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        // Unlit, transparent, additive (black adds nothing): the face's colour comes from the glow (a property block).
        private static Material MakeMaterial(Texture2D mask, List<string> done)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            bool made = material == null;
            if (made) material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.name = "Runner_Face_Glow_Mat";
            material.SetTexture("_BaseMap", mask);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.One);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)CullMode.Back);
            material.SetFloat("_AlphaClip", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            if (made)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));
                AssetDatabase.CreateAsset(material, MaterialPath);
                done.Add("made " + MaterialPath);
            }
            else EditorUtility.SetDirty(material);
            return material;
        }
    }
}

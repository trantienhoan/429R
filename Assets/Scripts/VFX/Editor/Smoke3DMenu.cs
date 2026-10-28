using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.EditorTools
{
    /// <summary>
    /// 3D smoke (user, 2026-10-28: "start with the 3D smoke on Smoke_Puff"): the shared Smoke_Puff draws its particles as
    /// small lumpy balls (Smoke Puff Ball, made here) with the Smoke Puff 3D shader (lit from above, light rim, fades by
    /// alpha to coverage) instead of flat cards, turning slowly. Every linked copy keeps its own colours, sizes and timing;
    /// only its leftover mesh, material, pivot and vertex streams are put back to the shared one's, so the look follows it.
    /// Tools > 429 Game > Effects > Smoke > 4. Make Smoke_Puff 3D (run again to rebuild the ball and redo the copies).
    /// </summary>
    public static class Smoke3DMenu
    {
        private const string PuffPath = "Assets/Prefabs/Effects/Smoke/Smoke_Puff.prefab";
        private const string BallPath = "Assets/Data/Effects/Smoke Puff Ball.asset";
        private const string MaterialPath = "Assets/Data/Effects/Smoke Puff 3D.mat";
        private const string ShaderName = "429/Particles/Smoke Puff 3D";

        // The ball's size against the flat card it replaces: the soft cloud drawn on the card fills about this much of it
        // (the particles are big: Size over Lifetime multiplies them by 4-5).
        private const float BallDiameter = 0.45f;
        // How lumpy the ball is (0 = a smooth sphere).
        private const float Lumps = 0.14f;
        // How fast the puffs turn, at most, in radians a second on each axis (slow: with Scatter, a turn swings the puff
        // round its particle, a gentle swirl).
        private const float Spin = 0.25f;
        // How far each puff sits from its particle, in shares of its size, the way the old flat cards' pivot (-0.95)
        // spread a burst out (user, 2026-10-28: "scatter the puffs outward like the old spread"). Each puff is turned at
        // random, so each is pushed a different way and the burst spreads in every direction. 0 = no spread.
        // (Kept at 0.95 when the puffs grew 35%: user, 2026-10-28 "keep the spread part. only bigger smoke".)
        private const float Scatter = 0.95f;

        // The copy's own renderer settings put back to the shared smoke's (the look); its colours, sizes and timing stay.
        private static readonly string[] LookProperties =
        {
            "m_RenderMode", "m_Mesh", "m_Mesh1", "m_Mesh2", "m_Mesh3", "m_MeshDistribution", "m_MeshWeighting",
            "m_MeshWeighting1", "m_MeshWeighting2", "m_MeshWeighting3", "m_Materials", "m_Pivot", "m_RenderAlignment",
            "m_VertexStreams", "m_UseCustomVertexStreams", "m_EnableGPUInstancing",
        };

        // ---------- other shapes (user, 2026-10-28: "can we do other shape? less roundy?") ----------

        /// <summary>
        /// A puff shape. Round: the lumpy ball. Cloud: three to five bumps grown together, a 3D cartoon cloud. Chunky: a
        /// low-poly lump with flat faces. Wisp: a stretched, lumpy, slightly bent blob. Each but Round has three meshes,
        /// and every particle picks one at random, so no two puffs look the same.
        /// </summary>
        public enum Shape { Round, Cloud, Chunky, Wisp }

        private const string ShapeFolder = "Assets/Data/Effects/Smoke Shapes";
        // Every shape is made about as big as the round ball (its mean radius), so a copy's size still fits.
        // ...then 30% bigger (user, 2026-10-28: the see-through edges made them look smaller than the old flat clouds), then
        // 35% bigger again (user, 2026-10-28: "make the smoke size bigger in each one by 35%").
        private const float MeanRadius = BallDiameter * 0.5f * 0.92f * 1.3f * 1.35f;

        // ---------- black smokes (user, 2026-10-28: "darker material for black smokes") ----------

        private const string DarkMaterialPath = "Assets/Data/Effects/Smoke Puff 3D Dark.mat";
        private const string DarkMaterialName = "Smoke Puff 3D Dark";
        // A puff this dark or darker (its colour's brightness, 0-1), or named black, is a black smoke.
        private const float BlackBrightness = 0.25f;

        [MenuItem("Tools/429 Game/Effects/Smoke/9. Scatter The 3D Puffs (Old Spread)")]
        public static void ScatterMenu() => Debug.Log(ScatterPuffs());

        /// <summary>
        /// Every shared smoke's 3D puffs spread out like the old flat cards did (Scatter), turning slower (Spin); every
        /// linked copy follows (its own pivot or spin settings put back).
        /// </summary>
        public static string ScatterPuffs()
        {
            if (EditorApplication.isPlaying) return "[Smoke 3D] Stop play mode first.";
            if (PrefabStageUtility.GetCurrentPrefabStage() != null) return "[Smoke 3D] Close the prefab you're editing (Prefab Mode) first.";
            var masters = new HashSet<GameObject>();
            int puffs = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Effects/Smoke" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    bool any = false;
                    foreach (var renderer in root.GetComponentsInChildren<ParticleSystemRenderer>(true))
                    {
                        if (renderer.renderMode != ParticleSystemRenderMode.Mesh) continue;
                        renderer.pivot = new Vector3(Scatter, 0f, 0f);
                        var turn = renderer.GetComponent<ParticleSystem>().rotationOverLifetime;
                        turn.enabled = true;
                        turn.separateAxes = true;
                        turn.x = new ParticleSystem.MinMaxCurve(-Spin, Spin);
                        turn.y = new ParticleSystem.MinMaxCurve(-Spin, Spin);
                        turn.z = new ParticleSystem.MinMaxCurve(-Spin, Spin);
                        puffs++;
                        any = true;
                    }
                    if (any) PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                masters.Add(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            }
            return $"[Smoke 3D] {puffs} shared 3D puff(s) now spread out (pivot {Scatter}) and turn at most {Spin} a second. " +
                   FollowShared(masters) + " " + FollowSpin(masters);
        }

        // Copies with their own turning (Rotation over Lifetime) on a 3D puff: put back to the shared one's. As a sentence.
        private static string FollowSpin(HashSet<GameObject> masters)
        {
            int copies = 0;
            foreach (var path in PrefabPaths())
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null || !asset.GetComponentsInChildren<Transform>(true).Any(t => IsCopyOf(t, masters))) continue;
                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    bool any = false;
                    foreach (var t in contents.GetComponentsInChildren<Transform>(true))
                    {
                        if (!IsCopyOf(t, masters)) continue;
                        foreach (var system in t.GetComponentsInChildren<ParticleSystem>(true))
                        {
                            var shared = PrefabUtility.GetCorrespondingObjectFromOriginalSource(system);
                            if (shared == null || shared.GetComponent<ParticleSystemRenderer>().renderMode != ParticleSystemRenderMode.Mesh) continue;
                            var p = new SerializedObject(system).FindProperty("RotationModule");
                            if (p == null || !p.prefabOverride) continue;
                            PrefabUtility.RevertPropertyOverride(p, InteractionMode.AutomatedAction);
                            any = true;
                            copies++;
                        }
                    }
                    if (any) PrefabUtility.SaveAsPrefabAsset(contents, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }
            return copies > 0 ? $"{copies} copies' own turning put back." : "No copy had its own turning.";
        }

        [MenuItem("Tools/429 Game/Effects/Smoke/8. Black Smokes Use The Dark Material")]
        public static void DarkMenu() => Debug.Log(UseDarkForBlack());

        /// <summary>
        /// Black smokes (a colour this dark or darker, or named black: the bugs' death smoke, the rooftop's, the door's)
        /// get Smoke Puff 3D Dark: more solid (Opacity 0.85), thinner edge fade and a fainter rim, so they read black
        /// again; every other puff keeps the see-through Smoke Puff 3D. Decided puff by puff (the colour is each copy's
        /// own), on the shared smokes and every linked copy, in every prefab and the open scene.
        /// </summary>
        public static string UseDarkForBlack()
        {
            if (EditorApplication.isPlaying) return "[Smoke 3D] Stop play mode first.";
            if (PrefabStageUtility.GetCurrentPrefabStage() != null) return "[Smoke 3D] Close the prefab you're editing (Prefab Mode) first.";
            var normal = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (normal == null) return "[Smoke 3D] No Smoke Puff 3D material yet: run 4. Make Smoke_Puff 3D first.";
            var dark = AssetDatabase.LoadAssetAtPath<Material>(DarkMaterialPath);
            if (dark == null)
            {
                dark = new Material(normal) { name = DarkMaterialName };
                dark.SetFloat("_Opacity", 0.85f);
                dark.SetFloat("_EdgeSoftness", 0.25f);
                dark.SetFloat("_RimStrength", 0.15f);
                AssetDatabase.CreateAsset(dark, DarkMaterialPath);
            }

            // The shared smokes themselves.
            var masters = new HashSet<GameObject>();
            int sharedDark = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Effects/Smoke" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    bool changed = false;
                    foreach (var renderer in root.GetComponentsInChildren<ParticleSystemRenderer>(true))
                    {
                        if (renderer.renderMode != ParticleSystemRenderMode.Mesh) continue;
                        var wanted = IsBlack(renderer.GetComponent<ParticleSystem>()) ? dark : normal;
                        if (wanted == dark) sharedDark++;
                        if (renderer.sharedMaterial == wanted) continue;
                        renderer.sharedMaterial = wanted;
                        changed = true;
                    }
                    if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                masters.Add(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            }

            // Every linked copy, puff by puff: its own colour decides.
            int copies = 0, files = 0;
            foreach (var path in PrefabPaths())
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null || !asset.GetComponentsInChildren<Transform>(true).Any(t => IsCopyOf(t, masters))) continue;
                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int set = DarkenCopies(contents, masters, dark, normal);
                    if (set == 0) continue;
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                    copies += set;
                    files++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }
            var scene = SceneManager.GetActiveScene();
            string sceneNote;
            int needScene = scene.GetRootGameObjects().Sum(r => DarkenCopies(r, masters, dark, normal, true));
            if (needScene == 0) sceneNote = "none in the scene needed it.";
            else if (scene.isDirty) sceneNote = $"{needScene} in the scene need it, but the scene has unsaved changes: save it and run this again.";
            else
            {
                scene.GetRootGameObjects().Sum(r => DarkenCopies(r, masters, dark, normal));
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                sceneNote = $"{needScene} in the scene (saved).";
            }
            AssetDatabase.SaveAssets();
            return $"[Smoke 3D] Black smokes now use {DarkMaterialName}: {sharedDark} shared puff(s), and {copies} copies in {files} prefabs " +
                   $"changed (to the dark one, or back to the see-through one); {sceneNote}";
        }

        // Gives each copy's 3D puffs the dark material if they're black, else the shared smoke's own. How many copies
        // changed (Only Count: count, change nothing).
        private static int DarkenCopies(GameObject root, HashSet<GameObject> masters, Material dark, Material normal, bool onlyCount = false)
        {
            int changed = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!IsCopyOf(t, masters)) continue;
                bool any = false;
                foreach (var renderer in t.GetComponentsInChildren<ParticleSystemRenderer>(true))
                {
                    var shared = PrefabUtility.GetCorrespondingObjectFromOriginalSource(renderer);
                    if (shared == null || shared.renderMode != ParticleSystemRenderMode.Mesh) continue;
                    var wanted = IsBlack(renderer.GetComponent<ParticleSystem>()) ? dark : normal;
                    if (renderer.sharedMaterial == wanted) continue;
                    any = true;
                    if (onlyCount) continue;
                    if (shared.sharedMaterial == wanted)
                    {
                        var p = new SerializedObject(renderer).FindProperty("m_Materials");
                        PrefabUtility.RevertPropertyOverride(p, InteractionMode.AutomatedAction);
                    }
                    else renderer.sharedMaterial = wanted;
                }
                if (any) changed++;
            }
            return changed;
        }

        // A black smoke: its colour (start colour x colour over lifetime) this dark or darker, or named black.
        private static bool IsBlack(ParticleSystem system)
        {
            if (system.name.IndexOf("black", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            for (var t = system.transform.parent; t != null; t = t.parent)
                if (t.name.IndexOf("black", System.StringComparison.OrdinalIgnoreCase) >= 0 && t.GetComponentInParent<ParticleSystem>() != null) return true;
            return Brightness(system) <= BlackBrightness;
        }

        private static float Brightness(ParticleSystem system)
        {
            var colours = new List<Color>();
            var start = system.main.startColor;
            switch (start.mode)
            {
                case ParticleSystemGradientMode.Color: colours.Add(start.color); break;
                case ParticleSystemGradientMode.TwoColors: colours.Add(start.colorMin); colours.Add(start.colorMax); break;
                case ParticleSystemGradientMode.TwoGradients:
                    colours.AddRange(start.gradientMin.colorKeys.Select(k => k.color));
                    colours.AddRange(start.gradientMax.colorKeys.Select(k => k.color));
                    break;
                default:
                    if (start.gradient != null) colours.AddRange(start.gradient.colorKeys.Select(k => k.color));
                    break;
            }
            float Light(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
            float light = colours.Count > 0 ? colours.Average(Light) : 1f;
            var overLife = system.colorOverLifetime;
            if (overLife.enabled && overLife.color.gradient != null) light *= overLife.color.gradient.colorKeys.Average(k => Light(k.color));
            return light;
        }

        [MenuItem("Tools/429 Game/Effects/Smoke/5. Make Smoke Shapes")]
        public static void MakeShapesMenu() => Debug.Log(MakeShapes());

        [MenuItem("Tools/429 Game/Effects/Smoke/6. Smoke_Puff Shape: Round")]
        public static void UseRound() => Debug.Log(UseShape(Shape.Round));

        [MenuItem("Tools/429 Game/Effects/Smoke/6. Smoke_Puff Shape: Cloud")]
        public static void UseCloud() => Debug.Log(UseShape(Shape.Cloud));

        [MenuItem("Tools/429 Game/Effects/Smoke/6. Smoke_Puff Shape: Chunky")]
        public static void UseChunky() => Debug.Log(UseShape(Shape.Chunky));

        [MenuItem("Tools/429 Game/Effects/Smoke/6. Smoke_Puff Shape: Wisp")]
        public static void UseWisp() => Debug.Log(UseShape(Shape.Wisp));

        /// <summary>Makes (or remakes) the Cloud, Chunky and Wisp meshes, three of each.</summary>
        public static string MakeShapes()
        {
            if (!AssetDatabase.IsValidFolder(ShapeFolder)) AssetDatabase.CreateFolder("Assets/Data/Effects", "Smoke Shapes");
            var log = new StringBuilder("[Smoke 3D] Shapes:");
            foreach (var shape in new[] { Shape.Cloud, Shape.Chunky, Shape.Wisp })
            {
                for (int i = 0; i < 3; i++)
                {
                    var made = shape == Shape.Cloud ? MakeCloud(i) : shape == Shape.Chunky ? MakeChunk(i) : MakeWisp(i);
                    made.name = $"Smoke Puff {shape} {i + 1}";
                    var saved = SaveMesh(made, $"{ShapeFolder}/{made.name}.asset");
                    if (i == 0) log.Append($" {shape} {saved.vertexCount} vertices / {saved.triangles.Length / 3} triangles;");
                }
            }
            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        /// <summary>The meshes of a shape (made by Make Smoke Shapes; Round is the ball).</summary>
        public static Mesh[] ShapeMeshes(Shape shape)
        {
            if (shape == Shape.Round) return new[] { AssetDatabase.LoadAssetAtPath<Mesh>(BallPath) };
            return Enumerable.Range(1, 3).Select(i => AssetDatabase.LoadAssetAtPath<Mesh>($"{ShapeFolder}/Smoke Puff {shape} {i}.asset"))
                .Where(m => m != null).ToArray();
        }

        /// <summary>Gives the shared Smoke_Puff this shape (its meshes mixed at random), and every copy follows.</summary>
        public static string UseShape(Shape shape)
        {
            if (EditorApplication.isPlaying) return "[Smoke 3D] Stop play mode first.";
            if (PrefabStageUtility.GetCurrentPrefabStage() != null) return "[Smoke 3D] Close the prefab you're editing (Prefab Mode) first.";
            var meshes = ShapeMeshes(shape);
            if (meshes.Length == 0 || meshes.Any(m => m == null)) return $"[Smoke 3D] No {shape} meshes yet: run 5. Make Smoke Shapes first.";
            var root = PrefabUtility.LoadPrefabContents(PuffPath);
            try
            {
                var renderer = root.GetComponent<ParticleSystemRenderer>();
                renderer.SetMeshes(meshes);
                renderer.meshDistribution = ParticleSystemMeshDistribution.UniformRandom;
                PrefabUtility.SaveAsPrefabAsset(root, PuffPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            var puff = AssetDatabase.LoadAssetAtPath<GameObject>(PuffPath);
            return $"[Smoke 3D] Smoke_Puff is now {shape} ({meshes.Length} mesh{(meshes.Length > 1 ? "es, mixed at random" : "")}). " +
                   FollowShared(new HashSet<GameObject> { puff });
        }

        // A 3D cartoon cloud: a middle bump and three or four smaller ones grown into it (one surface: for each direction
        // from the middle, how far the bumps reach, softly the farthest), its size made like the ball's.
        private static Mesh MakeCloud(int seed)
        {
            var random = new System.Random(4290 + seed * 17);
            var lobes = new List<(Vector3 centre, float radius)> { (Vector3.zero, 0.75f) };
            int extra = 4 + seed % 2;
            for (int i = 0; i < extra; i++)
            {
                var dir = RandomDirection(random);
                // Spread round the middle rather than piled on one side, sticking well out (so they show as bumps).
                dir = Vector3.Slerp(dir, Quaternion.Euler(0f, 360f * i / extra, 0f) * Vector3.forward, 0.55f).normalized;
                lobes.Add((dir * Range(random, 0.55f, 0.75f), Range(random, 0.38f, 0.55f)));
            }
            var (points, triangles) = Icosphere(2);
            var vertices = points.Select(d => d * SoftFarthest(d, lobes, 12f)).ToArray();
            return Finish(vertices, triangles, false);
        }

        // A low-poly lump: twenty-ish flat faces, each corner pushed in or out, a little squashed.
        private static Mesh MakeChunk(int seed)
        {
            var random = new System.Random(5290 + seed * 31);
            var (points, triangles) = Icosphere(1);
            var squash = new Vector3(Range(random, 0.95f, 1.15f), Range(random, 0.75f, 0.9f), Range(random, 0.9f, 1.05f));
            var vertices = points.Select(d => Vector3.Scale(d * Range(random, 0.8f, 1.2f), squash)).ToArray();
            return Finish(vertices, triangles, true);
        }

        // A wisp: stretched along one way, lumpy, bent a little like a curl of smoke.
        private static Mesh MakeWisp(int seed)
        {
            var random = new System.Random(6290 + seed * 43);
            var (points, triangles) = Icosphere(2);
            var stretch = new Vector3(Range(random, 1.6f, 1.9f), Range(random, 0.65f, 0.8f), Range(random, 0.8f, 0.95f));
            float bend = Range(random, 0.18f, 0.3f);
            var phase = new Vector3(Range(random, 0f, 6f), Range(random, 0f, 6f), Range(random, 0f, 6f));
            var vertices = points.Select(d =>
            {
                float lump = 1f + 0.2f * Mathf.Sin(3.3f * d.x + phase.x) * Mathf.Sin(2.9f * d.y + phase.y) * Mathf.Sin(3.1f * d.z + phase.z);
                var v = Vector3.Scale(d * lump, stretch);
                v.y += bend * (v.x * v.x - 1f);
                return v;
            }).ToArray();
            return Finish(vertices, triangles, false);
        }

        // How far the bumps reach in this direction (each the far side of its sphere; the farthest, rounded off by
        // Softness: lower = softer joins).
        private static float SoftFarthest(Vector3 d, List<(Vector3 centre, float radius)> lobes, float softness)
        {
            float sum = 0f;
            foreach (var (c, r) in lobes)
            {
                // Where this way leaves the bump's sphere (a bump it misses adds nothing).
                float along = Vector3.Dot(d, c);
                float room = along * along - (c.sqrMagnitude - r * r);
                if (room < 0f) continue;
                float t = along + Mathf.Sqrt(room);
                if (t > 0f) sum += Mathf.Exp(softness * t);
            }
            return Mathf.Log(Mathf.Max(sum, 1f)) / softness;
        }

        // Centred on its middle, made as big as the ball (mean radius), facing outwards; Faceted: flat faces.
        private static Mesh Finish(Vector3[] vertices, List<int> triangles, bool faceted)
        {
            var middle = vertices.Aggregate(Vector3.zero, (a, v) => a + v) / vertices.Length;
            float mean = vertices.Average(v => (v - middle).magnitude);
            vertices = vertices.Select(v => (v - middle) * (MeanRadius / Mathf.Max(0.0001f, mean))).ToArray();
            var tris = triangles.ToArray();
            var n = Vector3.Cross(vertices[tris[1]] - vertices[tris[0]], vertices[tris[2]] - vertices[tris[0]]);
            if (Vector3.Dot(n, vertices[tris[0]] + vertices[tris[1]] + vertices[tris[2]]) < 0f)
                for (int i = 0; i < tris.Length; i += 3) (tris[i + 1], tris[i + 2]) = (tris[i + 2], tris[i + 1]);
            if (faceted)
            {
                vertices = tris.Select(i => vertices[i]).ToArray();
                tris = Enumerable.Range(0, tris.Length).ToArray();
            }
            var mesh = new Mesh { vertices = vertices, triangles = tris };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 RandomDirection(System.Random random)
        {
            var v = new Vector3(Range(random, -1f, 1f), Range(random, -1f, 1f), Range(random, -1f, 1f));
            return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.up;
        }

        private static float Range(System.Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);

        // A unit icosphere: its points (each a direction) and triangles, Subdivisions times finer than the icosahedron.
        private static (List<Vector3> points, List<int> triangles) Icosphere(int subdivisions)
        {
            float g = (1f + Mathf.Sqrt(5f)) / 2f;
            var points = new List<Vector3>
            {
                new(-1, g, 0), new(1, g, 0), new(-1, -g, 0), new(1, -g, 0), new(0, -1, g), new(0, 1, g),
                new(0, -1, -g), new(0, 1, -g), new(g, 0, -1), new(g, 0, 1), new(-g, 0, -1), new(-g, 0, 1),
            };
            for (int i = 0; i < points.Count; i++) points[i] = points[i].normalized;
            var triangles = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            for (int s = 0; s < subdivisions; s++)
            {
                var middles = new Dictionary<long, int>();
                int Middle(int a, int b)
                {
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (middles.TryGetValue(key, out int index)) return index;
                    points.Add(((points[a] + points[b]) * 0.5f).normalized);
                    middles[key] = points.Count - 1;
                    return points.Count - 1;
                }
                var finer = new List<int>(triangles.Count * 4);
                for (int i = 0; i < triangles.Count; i += 3)
                {
                    int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                    int ab = Middle(a, b), bc = Middle(b, c), ca = Middle(c, a);
                    finer.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                triangles = finer;
            }
            return (points, triangles);
        }

        // Saves a mesh, keeping the asset (and what uses it) if it's already there.
        private static Mesh SaveMesh(Mesh made, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(made, path);
                return made;
            }
            string name = made.name;
            EditorUtility.CopySerialized(made, existing);
            existing.name = name;
            EditorUtility.SetDirty(existing);
            return existing;
        }

        [MenuItem("Tools/429 Game/Effects/Smoke/4. Make Smoke_Puff 3D")]
        public static void MakeMenu() => Debug.Log(MakePuff3D());

        public static string MakePuff3D()
        {
            if (EditorApplication.isPlaying) return "[Smoke 3D] Stop play mode first.";
            if (PrefabStageUtility.GetCurrentPrefabStage() != null) return "[Smoke 3D] Close the prefab you're editing (Prefab Mode) first.";
            var shader = Shader.Find(ShaderName);
            if (shader == null) return $"[Smoke 3D] The shader '{ShaderName}' isn't there (Assets/Shader/SmokePuff3D.shader).";
            var puff = AssetDatabase.LoadAssetAtPath<GameObject>(PuffPath);
            if (puff == null) return $"[Smoke 3D] {PuffPath} isn't there: run 2. Build Shared Smoke Prefabs first.";

            var ball = SaveBall(MakeBall(BallDiameter * 0.5f, 2, Lumps));
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "Smoke Puff 3D", enableInstancing = true };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            // The shared smoke: mesh particles, the ball and the material, turning slowly.
            var root = PrefabUtility.LoadPrefabContents(PuffPath);
            try
            {
                Make3D(root.GetComponent<ParticleSystem>(), new[] { ball }, material);
                PrefabUtility.SaveAsPrefabAsset(root, PuffPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            return "[Smoke 3D] Smoke_Puff now draws 3D puffs " +
                   $"({ball.vertexCount} vertices, {ball.triangles.Length / 3} triangles each). " + FollowShared(new HashSet<GameObject> { puff });
        }

        // ---------- the other shared smokes (user, 2026-10-28: "do the other 7 smokes with chunky too") ----------

        [MenuItem("Tools/429 Game/Effects/Smoke/7. Other Shared Smokes 3D (Chunky)")]
        public static void OthersMenu() => Debug.Log(MakeOthers3D(Shape.Chunky));

        /// <summary>
        /// Every other shared smoke in Assets/Prefabs/Effects/Smoke gets the 3D puffs too (this shape, the same material):
        /// each of its smoke puffs (the cloud sheet, 2x2). A flash (the 4x4 sheet) and its ring stay flat cards: they're a
        /// flash, not smoke. Then every linked copy's own look settings on those puffs are put back, so they follow.
        /// </summary>
        public static string MakeOthers3D(Shape shape)
        {
            if (EditorApplication.isPlaying) return "[Smoke 3D] Stop play mode first.";
            if (PrefabStageUtility.GetCurrentPrefabStage() != null) return "[Smoke 3D] Close the prefab you're editing (Prefab Mode) first.";
            var meshes = ShapeMeshes(shape);
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (meshes.Length == 0 || meshes.Any(m => m == null)) return $"[Smoke 3D] No {shape} meshes yet: run 5. Make Smoke Shapes first.";
            if (material == null) return "[Smoke 3D] No Smoke Puff 3D material yet: run 4. Make Smoke_Puff 3D first.";

            var masters = new HashSet<GameObject>();
            var log = new StringBuilder();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Effects/Smoke" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path == PuffPath) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var puffs = root.GetComponentsInChildren<ParticleSystem>(true).Where(IsCloudSheet).ToList();
                    if (puffs.Count == 0) continue;
                    foreach (var system in puffs) Make3D(system, meshes, material);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    int flat = root.GetComponentsInChildren<ParticleSystem>(true).Length - puffs.Count;
                    log.Append($" {root.name} ({puffs.Count} puff{(puffs.Count > 1 ? "s" : "")}{(flat > 0 ? $", {flat} flash part{(flat > 1 ? "s" : "")} kept flat" : "")});");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                masters.Add(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            }
            return $"[Smoke 3D] Now 3D {shape}:{log}\n" + FollowShared(masters);
        }

        // A smoke puff: the cloud sheet (2x2 tiles). The flash (4x4) and its ring (4x2) aren't.
        private static bool IsCloudSheet(ParticleSystem system)
        {
            var sheet = system.textureSheetAnimation;
            return sheet.enabled && sheet.numTilesX == 2 && sheet.numTilesY == 2;
        }

        // One particle system drawn as 3D puffs: these meshes (at random), the 3D material, spread out (Scatter), turning
        // slowly.
        private static void Make3D(ParticleSystem system, Mesh[] meshes, Material material)
        {
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.SetMeshes(meshes);
            renderer.meshDistribution = ParticleSystemMeshDistribution.UniformRandom;
            renderer.sharedMaterial = material;
            renderer.pivot = new Vector3(Scatter, 0f, 0f);
            renderer.alignment = ParticleSystemRenderSpace.World;
            renderer.enableGPUInstancing = false;
            renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
            {
                ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Normal, ParticleSystemVertexStream.Color,
            });
            var main = system.main;
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var turn = system.rotationOverLifetime;
            turn.enabled = true;
            turn.separateAxes = true;
            turn.x = new ParticleSystem.MinMaxCurve(-Spin, Spin);
            turn.y = new ParticleSystem.MinMaxCurve(-Spin, Spin);
            turn.z = new ParticleSystem.MinMaxCurve(-Spin, Spin);
        }

        // Every linked copy of these shared smokes: its leftover look settings on the 3D puffs put back, in every prefab
        // (bases first) and the open scene (saved if it had no other unsaved changes). What was done, as a sentence.
        private static string FollowShared(HashSet<GameObject> masters)
        {
            int copies = 0, files = 0;
            foreach (var path in PrefabPaths())
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null || !asset.GetComponentsInChildren<Transform>(true).Any(t => IsCopyOf(t, masters))) continue;
                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int reset = ResetLooks(contents, masters);
                    if (reset == 0) continue;
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                    copies += reset;
                    files++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }
            var scene = SceneManager.GetActiveScene();
            string sceneNote;
            if (scene.isDirty) sceneNote = "The scene had unsaved changes, so its copies weren't touched: save it and run this again.";
            else
            {
                int inScene = scene.GetRootGameObjects().Sum(r => ResetLooks(r, masters));
                if (inScene > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                sceneNote = inScene > 0 ? $"{inScene} in the scene (saved)." : "none in the scene needed it.";
            }
            AssetDatabase.SaveAssets();
            return $"{copies} copies in {files} prefabs had their own mesh/material/pivot put back to the shared one's; {sceneNote}";
        }

        // A linked copy of one of these shared smokes (made from it, not a part of it).
        private static bool IsCopyOf(Transform t, HashSet<GameObject> masters) =>
            PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) && masters.Contains(PrefabUtility.GetCorrespondingObjectFromOriginalSource(t.gameObject));

        // Puts each copy's own look settings back to the shared smoke's, on every part that's a 3D puff there. How many
        // copies changed.
        private static int ResetLooks(GameObject root, HashSet<GameObject> masters)
        {
            int changed = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!IsCopyOf(t, masters)) continue;
                bool any = false;
                foreach (var renderer in t.GetComponentsInChildren<ParticleSystemRenderer>(true))
                {
                    var shared = PrefabUtility.GetCorrespondingObjectFromOriginalSource(renderer);
                    if (shared == null || shared.renderMode != ParticleSystemRenderMode.Mesh) continue;
                    var so = new SerializedObject(renderer);
                    // Its 3D material (the dark one for a black smoke, the see-through one for a light copy of a dark shared
                    // smoke) is its own on purpose (8. Black Smokes Use The Dark Material).
                    bool keepMaterial = renderer.sharedMaterial != null &&
                                        (renderer.sharedMaterial.name == DarkMaterialName || renderer.sharedMaterial.name == "Smoke Puff 3D");
                    foreach (var name in LookProperties)
                    {
                        if (keepMaterial && name == "m_Materials") continue;
                        var p = so.FindProperty(name);
                        if (p == null || !p.prefabOverride) continue;
                        PrefabUtility.RevertPropertyOverride(p, InteractionMode.AutomatedAction);
                        any = true;
                    }
                }
                if (any) changed++;
            }
            return changed;
        }

        // ---------- the ball ----------

        // A lumpy ball: an icosphere (Subdivisions 2: 320 triangles) of this radius, its surface pushed in and out by a few
        // soft bumps (Lumps: how much), smooth normals, centred on its middle.
        private static Mesh MakeBall(float radius, int subdivisions, float lumps)
        {
            float g = (1f + Mathf.Sqrt(5f)) / 2f;
            var points = new List<Vector3>
            {
                new(-1, g, 0), new(1, g, 0), new(-1, -g, 0), new(1, -g, 0), new(0, -1, g), new(0, 1, g),
                new(0, -1, -g), new(0, 1, -g), new(g, 0, -1), new(g, 0, 1), new(-g, 0, -1), new(-g, 0, 1),
            };
            for (int i = 0; i < points.Count; i++) points[i] = points[i].normalized;
            var triangles = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            for (int s = 0; s < subdivisions; s++)
            {
                var middles = new Dictionary<long, int>();
                int Middle(int a, int b)
                {
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (middles.TryGetValue(key, out int index)) return index;
                    points.Add(((points[a] + points[b]) * 0.5f).normalized);
                    middles[key] = points.Count - 1;
                    return points.Count - 1;
                }
                var finer = new List<int>(triangles.Count * 4);
                for (int i = 0; i < triangles.Count; i += 3)
                {
                    int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                    int ab = Middle(a, b), bc = Middle(b, c), ca = Middle(c, a);
                    finer.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                triangles = finer;
            }

            var vertices = points.Select(d => d * radius * (1f + lumps * Bumps(d))).ToArray();
            // Faces outwards in Unity's winding (clockwise seen from outside, so Cross(b - a, c - a) points out of the
            // front): flip them all if the first one faces in.
            var n = Vector3.Cross(vertices[triangles[1]] - vertices[triangles[0]], vertices[triangles[2]] - vertices[triangles[0]]);
            if (Vector3.Dot(n, vertices[triangles[0]] + vertices[triangles[1]] + vertices[triangles[2]]) < 0f)
            {
                for (int i = 0; i < triangles.Count; i += 3) (triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]);
            }
            var mesh = new Mesh { name = "Smoke Puff Ball", vertices = vertices, triangles = triangles.ToArray() };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // Soft bumps over a direction, about -1..1.
        private static float Bumps(Vector3 d) =>
            0.65f * Mathf.Sin(3.1f * d.x + 0.7f) * Mathf.Sin(2.7f * d.y + 1.9f) * Mathf.Sin(3.3f * d.z + 0.4f) +
            0.35f * Mathf.Sin(5.3f * d.x + 2.2f) * Mathf.Sin(4.9f * d.y + 0.3f) * Mathf.Sin(5.7f * d.z + 1.1f);

        // Saves the ball, keeping the asset (and what uses it) if it's already there.
        private static Mesh SaveBall(Mesh made)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(BallPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(made, BallPath);
                return made;
            }
            EditorUtility.CopySerialized(made, existing);
            existing.name = "Smoke Puff Ball";
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static List<string> PrefabPaths()
        {
            var paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" })
                .Select(AssetDatabase.GUIDToAssetPath).Where(p => !p.StartsWith("Assets/Prefabs/Effects/Smoke/")).ToList();
            int Depth(string path)
            {
                int depth = 0;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                while (go != null && PrefabUtility.GetPrefabAssetType(go) == PrefabAssetType.Variant && depth < 10)
                {
                    go = PrefabUtility.GetCorrespondingObjectFromSource(go);
                    depth++;
                }
                return depth;
            }
            var depths = paths.ToDictionary(p => p, Depth);
            return paths.OrderBy(p => depths[p]).ThenBy(p => p).ToList();
        }
    }
}

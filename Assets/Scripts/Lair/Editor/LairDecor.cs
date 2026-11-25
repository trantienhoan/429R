using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// The storage-room lair's small built pieces (user, 2026-11-25, the lair plan; part of Tools > 429 Game > Lair > Set
    /// Up Lair Story Props). No new textures or materials: they wear the game's own ones, the furniture atlas
    /// (Bedroom_Set_1_Mat) through flat spots of it, the Slime Bowling vine and the pumpkin leaves:
    /// - Lair_Wall_Vine: a curly pumpkin vine climbing a wall from the floor (Bowling_Vine_Mat stem, leaf cards on
    ///   Pumpkin_Leaves_Mat: mostly its yellow-green leaf, some orange), two branches and tendril curls; built flat
    ///   against the wall plane (local XY, the room on +Z).
    /// - Lair_Bug_Hole: a hole gnawed into the wood, black inside, splintered light wood round it (facing +Z).
    /// - Lair_Claw_Scratches: four gouges of fresh light wood with a dark line in each (facing +Z).
    /// - Lair_Nest: a bowl of twigs and straw with the user's egg sacs in it and pumpkin seeds, some soaked green and
    ///   glowing (the glow jar's material), a few spilt out; beside it a chewed box (Paper_Box_2 with a bite and a hole
    ///   cut by Exports/Lair/make_lair_box.py) trailing seeds.
    /// Looks only: no colliders, no shadows. Meshes in Models/Lair/*.asset, prefabs in Prefabs/Lair.
    /// </summary>
    internal static class LairDecor
    {
        private const string Tag = "[Lair Story Props]";
        private const string PrefabFolder = "Assets/Prefabs/Lair/";
        private const string ModelFolder = "Assets/Models/Lair/";
        private const string AtlasMaterial = "Assets/Materials/Bedroom_Set_1_Mat.mat";
        private const string VineMaterial = "Assets/Materials/Bowling/Bowling_Vine_Mat.mat";
        private const string LeavesMaterial = "Assets/Materials/Pumpkin_Leaves_Mat.mat";

        // Flat spots of the furniture atlas (16 px blocks with almost no variation; found in its PNG).
        private static readonly Vector2 Black = new Vector2(0.8164f, 0.0742f);
        private static readonly Vector2 Straw = new Vector2(0.1836f, 0.6445f);
        private static readonly Vector2 FreshWood = new Vector2(0.9492f, 0.7930f);
        private static readonly Vector2 Cream = new Vector2(0.1055f, 0.8789f);
        private static readonly Vector2 DarkBrown = new Vector2(0.0586f, 0.7617f);

        public static void Build(List<string> done)
        {
            var atlas = AssetDatabase.LoadAssetAtPath<Material>(AtlasMaterial);
            if (atlas == null)
            {
                Debug.LogError($"{Tag} {AtlasMaterial} is missing; no vine, holes, scratches or nest.");
                return;
            }
            WallVine(done);
            BugHole(atlas, done);
            ClawScratches(atlas, done);
            Nest(atlas, done);
        }

        // ---- The wall vine ----

        private static void WallVine(List<string> done)
        {
            var stemMat = AssetDatabase.LoadAssetAtPath<Material>(VineMaterial);
            var leafMat = AssetDatabase.LoadAssetAtPath<Material>(LeavesMaterial);
            if (stemMat == null || leafMat == null)
            {
                Debug.LogError($"{Tag} The Bowling vine material, leaf card or pumpkin leaves are missing; no wall vine.");
                return;
            }
            var b = new MeshBuild();
            var rng = new System.Random(429);
            // The main stem: up the wall, swaying side to side, a little off the wall.
            var main = new List<Vector3>();
            const int Steps = 40;
            for (int i = 0; i <= Steps; i++)
            {
                float t = (float)i / Steps;
                main.Add(new Vector3(0.17f * Mathf.Sin(t * 8.2f) + 0.08f * Mathf.Sin(t * 19.5f + 1f),
                                     -0.02f + 2.15f * t,
                                     0.028f + 0.01f * Mathf.Sin(t * 23f)));
            }
            b.Tube(main, 0.024f, 0.007f, 7, Vector2.zero, Vector2.zero);
            // Two side branches.
            var branchTips = new List<Vector3>();
            foreach (var (at, side, length) in new[] { (0.32f, 1f, 0.75f), (0.6f, -1f, 0.55f) })
            {
                var from = main[Mathf.RoundToInt(at * Steps)];
                var branch = new List<Vector3>();
                for (int i = 0; i <= 16; i++)
                {
                    float t = i / 16f;
                    branch.Add(from + new Vector3(side * length * 0.7f * t + 0.05f * Mathf.Sin(t * 11f),
                                                  length * 0.75f * t, 0.004f * Mathf.Sin(t * 9f)));
                }
                b.Tube(branch, 0.015f, 0.006f, 6, Vector2.zero, Vector2.zero);
                branchTips.Add(branch[branch.Count - 1]);
            }
            // Tendril curls along the stem: a shrinking spiral beside it.
            foreach (float at in new[] { 0.18f, 0.45f, 0.72f, 0.95f })
            {
                var from = main[Mathf.RoundToInt(at * Steps)];
                float side = rng.Next(2) == 0 ? -1f : 1f;
                var curl = new List<Vector3>();
                for (int i = 0; i <= 20; i++)
                {
                    float t = i / 20f;
                    float r = 0.055f * (1f - 0.7f * t);
                    float a = t * Mathf.PI * 3.2f;
                    curl.Add(from + new Vector3(side * (0.03f + r * (1f - Mathf.Cos(a))), r * Mathf.Sin(a) + 0.03f * t, 0.012f * t));
                }
                b.Tube(curl, 0.0055f, 0.0025f, 5, Vector2.zero, Vector2.zero);
            }
            var stemMesh = SaveMesh("Lair_Wall_Vine_Stem", b, done);
            // The pumpkin leaf sheet (Images/pumpkin_leaves.png) has four leaves: the yellow-green one (bottom right) on
            // most, an orange one (top left) here and there. Cards like the Bowling vines' (stem at the pivot, the leaf
            // along +Z, facing +Y).
            var greenLeaf = SaveMesh("Lair_Leaf_Green", LeafCard(new Vector2(0.5f, 0f)), done);
            var orangeLeaf = SaveMesh("Lair_Leaf_Orange", LeafCard(new Vector2(0f, 0.5f)), done);

            var root = new GameObject("Lair_Wall_Vine");
            try
            {
                Look(root, "Stem", stemMesh, stemMat);
                // Leaves on alternate sides, tilted out of the wall.
                int n = 0;
                foreach (float at in new[] { 0.08f, 0.2f, 0.31f, 0.43f, 0.55f, 0.66f, 0.78f, 0.9f, 1f })
                {
                    var p = main[Mathf.RoundToInt(at * Steps)];
                    float side = n % 2 == 0 ? 1f : -1f;
                    var along = new Vector3(side * 0.85f, 0.45f + 0.1f * (float)rng.NextDouble(), 0.25f).normalized;
                    var go = Look(root, $"Leaf {n}", n % 3 == 2 ? orangeLeaf : greenLeaf, leafMat);
                    go.transform.localPosition = p + new Vector3(0f, 0f, 0.01f);
                    go.transform.localRotation = Quaternion.LookRotation(along, new Vector3(0f, 0.25f, 1f));
                    go.transform.localScale = Vector3.one * (0.38f + 0.12f * (float)rng.NextDouble()) * (1.1f - 0.35f * at);
                    n++;
                }
                foreach (var tip in branchTips)
                {
                    var go = Look(root, $"Leaf {n++}", greenLeaf, leafMat);
                    go.transform.localPosition = tip;
                    go.transform.localRotation = Quaternion.LookRotation(new Vector3(0f, 1f, 0.3f), Vector3.forward);
                    go.transform.localScale = Vector3.one * 0.3f;
                }
                SavePrefab(root, done);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static MeshBuild LeafCard(Vector2 corner)
        {
            var b = new MeshBuild();
            b.Card(corner, 0.5f);
            return b;
        }

        // ---- A gnawed bug hole ----

        private static void BugHole(Material atlas, List<string> done)
        {
            var b = new MeshBuild();
            var rng = new System.Random(4291);
            const int Points = 16;
            var hole = new Vector2[Points];
            for (int i = 0; i < Points; i++)
            {
                float a = i * Mathf.PI * 2f / Points;
                float r = 0.042f * (0.82f + 0.3f * (float)rng.NextDouble());
                hole[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.85f);
            }
            // A dark gnawed rim just round the hole, and a few light splinters of uneven length sticking out of it.
            var rim = new Vector2[Points];
            for (int i = 0; i < Points; i++) rim[i] = hole[i] * (1.18f + 0.12f * (float)rng.NextDouble());
            var splinters = new List<Vector2>();
            for (int i = 0; i < Points; i++)
            {
                splinters.Add(rim[i] * 0.98f);
                if (rng.NextDouble() < 0.45)
                {
                    // A splinter: a thin point out between this rim point and the next.
                    var mid = (rim[i] + rim[(i + 1) % Points]) * 0.5f;
                    splinters.Add(mid * (1.25f + 0.45f * (float)rng.NextDouble()));
                }
            }
            b.Fan(Vector3.forward * 0.002f, Vector3.right, Vector3.up, Vector3.forward, splinters, FreshWood);
            b.Fan(Vector3.forward * 0.0035f, Vector3.right, Vector3.up, Vector3.forward, rim, DarkBrown);
            b.Fan(Vector3.forward * 0.005f, Vector3.right, Vector3.up, Vector3.forward, hole, Black);
            var mesh = SaveMesh("Lair_Bug_Hole", b, done);
            var root = new GameObject("Lair_Bug_Hole");
            try
            {
                var look = root.AddComponent<MeshRenderer>();
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                look.sharedMaterial = atlas;
                Quiet(look);
                SavePrefab(root, done);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ---- Claw scratches ----

        private static void ClawScratches(Material atlas, List<string> done)
        {
            var b = new MeshBuild();
            for (int k = 0; k < 4; k++)
            {
                // Four claws pulled down and across, the middle ones longer.
                float offset = (k - 1.5f) * 0.05f;
                float length = k == 1 || k == 2 ? 0.42f : 0.32f;
                var line = new List<Vector2>();
                for (int i = 0; i <= 12; i++)
                {
                    float t = i / 12f;
                    line.Add(new Vector2(offset + t * length * 0.45f + 0.015f * Mathf.Sin(t * 3.1f + k),
                                         length * 0.5f - t * length));
                }
                b.Strip(line, 0.016f, 0.003f, Vector3.forward, FreshWood);
                b.Strip(line, 0.005f, 0.005f, Vector3.forward, DarkBrown);
            }
            var mesh = SaveMesh("Lair_Claw_Scratches", b, done);
            var root = new GameObject("Lair_Claw_Scratches");
            try
            {
                var look = root.AddComponent<MeshRenderer>();
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                look.sharedMaterial = atlas;
                Quiet(look);
                SavePrefab(root, done);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ---- The nest ----

        private static void Nest(Material atlas, List<string> done)
        {
            var rng = new System.Random(4292);
            var twigs = new MeshBuild();
            // The straw bed.
            var bed = new Vector2[18];
            for (int i = 0; i < bed.Length; i++)
            {
                float a = i * Mathf.PI * 2f / bed.Length;
                float r = 0.25f * (0.9f + 0.15f * (float)rng.NextDouble());
                bed[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            }
            twigs.Fan(new Vector3(0f, 0.012f, 0f), Vector3.right, Vector3.forward, Vector3.up, bed, Straw);
            // Twigs laid round and round in a bowl: lower rings wider, the top ring the rim.
            Vector2[] colours = { Straw, Straw, FreshWood, DarkBrown };
            for (int i = 0; i < 84; i++)
            {
                float layer = (i % 6) / 5f;
                float a = (float)(rng.NextDouble() * Mathf.PI * 2f);
                float radius = Mathf.Lerp(0.2f, 0.29f, layer) + 0.02f * (float)rng.NextDouble();
                float height = 0.015f + layer * 0.1f + 0.015f * (float)rng.NextDouble();
                float length = 0.18f + 0.12f * (float)rng.NextDouble();
                var path = new List<Vector3>();
                for (int s = 0; s <= 3; s++)
                {
                    float t = s / 3f;
                    float aa = a + (t - 0.5f) * length / radius;
                    float r = radius + 0.02f * Mathf.Sin(t * 3.1f + i);
                    path.Add(new Vector3(Mathf.Cos(aa) * r, height + 0.012f * Mathf.Sin(t * 5f + i), Mathf.Sin(aa) * r));
                }
                float thick = 0.009f + 0.006f * (float)rng.NextDouble();
                twigs.Tube(path, thick, thick * 0.7f, 3, colours[i % colours.Length], colours[i % colours.Length]);
            }
            // Pumpkin seeds: in the bed, and a few spilt out in front.
            for (int i = 0; i < 16; i++)
            {
                bool spilt = i >= 12;
                float a = spilt ? -0.6f + 0.5f * (float)rng.NextDouble() : (float)(rng.NextDouble() * Mathf.PI * 2f);
                float r = spilt ? 0.33f + 0.15f * (float)rng.NextDouble() : 0.13f * (float)rng.NextDouble();
                var at = new Vector3(Mathf.Cos(a) * r, spilt ? 0.004f : 0.02f, Mathf.Sin(a) * r);
                twigs.Seed(at, (float)(rng.NextDouble() * 360f), 0.036f, Cream);
            }
            var nestMesh = SaveMesh("Lair_Nest", twigs, done);

            // Seeds soaked in the Green: the glow jar's material, its UVs on a glowing spot of the jar's texture.
            var jar = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Lair/Lair_Glow_Jar_Mat.mat");
            Mesh glowMesh = null;
            if (jar != null)
            {
                var scale = jar.GetTextureScale("_BaseMap");
                var offset = jar.GetTextureOffset("_BaseMap");
                var glowSpot = new Vector2((0.5f - offset.x) / scale.x, (0.45f - offset.y) / scale.y);
                var green = new MeshBuild();
                for (int i = 0; i < 7; i++)
                {
                    float a = (float)(rng.NextDouble() * Mathf.PI * 2f);
                    float r = 0.11f * (float)rng.NextDouble();
                    green.Seed(new Vector3(Mathf.Cos(a) * r, 0.024f, Mathf.Sin(a) * r), (float)(rng.NextDouble() * 360f), 0.038f, glowSpot);
                }
                glowMesh = SaveMesh("Lair_Nest_Green_Seeds", green, done);
            }

            var root = new GameObject("Lair_Nest");
            try
            {
                // Built at 0.6 m across; a little bigger reads better from standing height (Play pictures 2026-11-25).
                var nest = new GameObject("Nest");
                nest.transform.SetParent(root.transform, false);
                nest.transform.localScale = Vector3.one * 1.35f;
                Look(nest, "Twigs", nestMesh, atlas);
                if (glowMesh != null) Look(nest, "Green_Seeds", glowMesh, jar);
                var sac = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "Lair_Egg_Sac.prefab");
                if (sac != null)
                {
                    foreach (var (x, z, size, turn) in new[] { (0.05f, 0.05f, 1f, 20f), (-0.09f, -0.02f, 0.85f, 140f), (0.03f, -0.1f, 0.75f, 260f) })
                    {
                        var s = (GameObject)PrefabUtility.InstantiatePrefab(sac, nest.transform);
                        s.transform.localPosition = new Vector3(x, 0.02f, z);
                        s.transform.localRotation = Quaternion.Euler(0f, turn, 0f);
                        s.transform.localScale = Vector3.one * size;
                        // The sac's pivot is where its thread would be: sit its bottom on the straw.
                        var bounds = new Bounds(s.transform.position, Vector3.zero);
                        foreach (var r in s.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
                        s.transform.position += Vector3.up * (root.transform.position.y + 0.018f - bounds.min.y);
                    }
                }
                ChewedBox(root, atlas, done);
                SavePrefab(root, done);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // The chewed box beside the nest (west of it as placed in the storage room): Paper_Box_2 with a bite out of its top
        // edge and a ragged hole in one side (Exports/Lair/make_lair_box.py), the hole turned out to the room, seeds
        // trailing out of it. Part of the nest (it stays put; a collider so things bump into it), not a breakable box
        // (so never a box bomb either).
        private static void ChewedBox(GameObject nest, Material atlas, List<string> done)
        {
            var mesh = LairStoryPropsMenu.ModelMesh("Assets/Models/Lair/Lair_Chewed_Box.fbx", done);
            if (mesh == null) return;
            // The hole's side: where the triangles showing the black spot are.
            var uvs = mesh.uv;
            var vertices = mesh.vertices;
            var tris = mesh.triangles;
            var sum = Vector3.zero;
            int count = 0;
            for (int i = 0; i < tris.Length; i += 3)
            {
                if ((uvs[tris[i]] - Black).sqrMagnitude > 1e-5f || (uvs[tris[i + 1]] - Black).sqrMagnitude > 1e-5f) continue;
                sum += (vertices[tris[i]] + vertices[tris[i + 1]] + vertices[tris[i + 2]]) / 3f;
                count++;
            }
            var hole = count > 0 ? sum / count - mesh.bounds.center : Vector3.forward;
            hole.y = 0f;
            hole = hole.sqrMagnitude > 1e-6f ? hole.normalized : Vector3.forward;

            // Beside the nest, its hole (and the bite above it) turned out to the room: as placed in the storage room (the
            // nest turned 200 degrees at 2.75, -2.45), this spot is west of the nest and this way is toward the room's middle.
            var at = new Vector3(0.48f, 0f, 0.28f);
            var toward = new Vector3(0.87f, 0f, -0.49f).normalized;
            var box = new GameObject("Chewed_Box");
            box.transform.SetParent(nest.transform, false);
            box.transform.localPosition = at;
            box.transform.localRotation = Quaternion.FromToRotation(hole, toward);
            box.transform.localScale = Vector3.one * 0.8f;             // Paper_Box_2's own size
            box.AddComponent<MeshFilter>().sharedMesh = mesh;
            var look = box.AddComponent<MeshRenderer>();
            look.sharedMaterial = atlas;
            look.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            var solid = box.AddComponent<BoxCollider>();
            solid.center = new Vector3(0f, 0.142f, 0f);                  // Paper_Box_2's collider
            solid.size = new Vector3(0.322f, 0.266f, 0.312f);

            // Seeds trailing out of the hole toward the nest.
            var rng = new System.Random(4293);
            var seeds = new MeshBuild();
            var mouth = at + toward * 0.14f;
            for (int i = 0; i < 9; i++)
            {
                float t = i / 8f;
                var side = new Vector3(-toward.z, 0f, toward.x) * (0.07f * ((float)rng.NextDouble() - 0.5f) * (1f + t * 2f));
                seeds.Seed(mouth + toward * (0.03f + 0.22f * t) + side + Vector3.up * 0.002f, (float)(rng.NextDouble() * 360f), 0.036f, Cream);
            }
            Look(nest, "Box_Seeds", SaveMesh("Lair_Box_Seeds", seeds, done), atlas);
        }

        // ---- Helpers ----

        private static GameObject Look(GameObject parent, string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var look = go.AddComponent<MeshRenderer>();
            look.sharedMaterial = material;
            Quiet(look);
            return go;
        }

        private static void Quiet(Renderer look)
        {
            look.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            look.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        }

        private static Mesh SaveMesh(string name, MeshBuild build, List<string> done)
        {
            Directory.CreateDirectory(ModelFolder);
            string path = ModelFolder + name + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool fresh = mesh == null;
            if (fresh) mesh = new Mesh { name = name };
            build.Fill(mesh);
            if (fresh)
            {
                AssetDatabase.CreateAsset(mesh, path);
                done.Add($"made {path} ({build.Triangles} tris)");
            }
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static void SavePrefab(GameObject root, List<string> done)
        {
            Directory.CreateDirectory(PrefabFolder);
            string path = PrefabFolder + root.name + ".prefab";
            bool had = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
            PrefabUtility.SaveAsPrefabAsset(root, path);
            if (!had) done.Add("made " + path);
        }

        /// <summary>Builds one mesh from tubes, flat fans, strips and seeds (each face one UV spot or a run between two).</summary>
        private sealed class MeshBuild
        {
            private readonly List<Vector3> v = new List<Vector3>();
            private readonly List<Vector3> n = new List<Vector3>();
            private readonly List<Vector2> uv = new List<Vector2>();
            private readonly List<int> t = new List<int>();

            public int Triangles => t.Count / 3;

            public void Fill(Mesh mesh)
            {
                mesh.Clear();
                mesh.indexFormat = v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
                mesh.SetVertices(v);
                mesh.SetNormals(n);
                mesh.SetUVs(0, uv);
                mesh.SetTriangles(t, 0);
                mesh.RecalculateBounds();
            }

            private int Add(Vector3 p, Vector3 normal, Vector2 c)
            {
                v.Add(p);
                n.Add(normal);
                uv.Add(c);
                return v.Count - 1;
            }

            // A triangle whose front faces the given way.
            private void Tri(int a, int b, int c, Vector3 facing)
            {
                if (Vector3.Dot(Vector3.Cross(v[b] - v[a], v[c] - v[a]), facing) < 0f) (b, c) = (c, b);
                t.Add(a);
                t.Add(b);
                t.Add(c);
            }

            // A tube along a path, tapering from r0 to r1, capped at the start and pointed at the end.
            public void Tube(IList<Vector3> path, float r0, float r1, int sides, Vector2 uvA, Vector2 uvB)
            {
                int start = v.Count;
                var normal = Vector3.zero;
                for (int i = 0; i < path.Count; i++)
                {
                    var tangent = (path[Mathf.Min(i + 1, path.Count - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
                    normal = i == 0
                        ? Vector3.Cross(tangent, Mathf.Abs(tangent.y) < 0.9f ? Vector3.up : Vector3.right).normalized
                        : (normal - Vector3.Dot(normal, tangent) * tangent).normalized;
                    var binormal = Vector3.Cross(tangent, normal);
                    float s = path.Count > 1 ? (float)i / (path.Count - 1) : 0f;
                    float r = Mathf.Lerp(r0, r1, s);
                    var c = Vector2.Lerp(uvA, uvB, s);
                    for (int k = 0; k < sides; k++)
                    {
                        float a = k * Mathf.PI * 2f / sides;
                        var dir = normal * Mathf.Cos(a) + binormal * Mathf.Sin(a);
                        Add(path[i] + dir * r, dir, c);
                    }
                }
                for (int i = 0; i < path.Count - 1; i++)
                {
                    for (int k = 0; k < sides; k++)
                    {
                        int a = start + i * sides + k, b = start + i * sides + (k + 1) % sides;
                        var outward = n[a] + n[b];
                        Tri(a, b, a + sides, outward);
                        Tri(b, b + sides, a + sides, outward);
                    }
                }
                var first = (path[1] - path[0]).normalized;
                var last = (path[path.Count - 1] - path[path.Count - 2]).normalized;
                int cap = Add(path[0], -first, uvA);
                int tip = Add(path[path.Count - 1] + last * r1 * 1.5f, last, uvB);
                int end = start + (path.Count - 1) * sides;
                for (int k = 0; k < sides; k++)
                {
                    Tri(cap, start + k, start + (k + 1) % sides, -first);
                    Tri(tip, end + k, end + (k + 1) % sides, last);
                }
            }

            // A flat fan from a centre over a closed outline (in the plane of right and up), facing the given way.
            public void Fan(Vector3 centre, Vector3 right, Vector3 up, Vector3 facing, IList<Vector2> outline, Vector2 c)
            {
                int mid = Add(centre, facing, c);
                int first = v.Count;
                foreach (var p in outline) Add(centre + right * p.x + up * p.y, facing, c);
                for (int i = 0; i < outline.Count; i++) Tri(mid, first + i, first + (i + 1) % outline.Count, facing);
            }

            // A flat strip along a line in the XY plane, widest in the middle and pointed at both ends, at height z.
            public void Strip(IList<Vector2> line, float width, float z, Vector3 facing, Vector2 c)
            {
                int start = v.Count;
                for (int i = 0; i < line.Count; i++)
                {
                    var along = (line[Mathf.Min(i + 1, line.Count - 1)] - line[Mathf.Max(i - 1, 0)]).normalized;
                    var across = new Vector2(-along.y, along.x);
                    float s = (float)i / (line.Count - 1);
                    float w = width * 0.5f * Mathf.Sin(s * Mathf.PI);
                    Add(new Vector3(line[i].x + across.x * w, line[i].y + across.y * w, z), facing, c);
                    Add(new Vector3(line[i].x - across.x * w, line[i].y - across.y * w, z), facing, c);
                }
                for (int i = 0; i < line.Count - 1; i++)
                {
                    int a = start + i * 2;
                    Tri(a, a + 1, a + 2, facing);
                    Tri(a + 1, a + 3, a + 2, facing);
                }
            }

            // A leaf card: a 1 x 1 card lying flat (facing +Y), its stem end at the pivot and the leaf along +Z, showing the
            // square of the texture from corner to corner + size.
            public void Card(Vector2 corner, float size)
            {
                int a = Add(new Vector3(-0.5f, 0f, 0f), Vector3.up, corner);
                int b = Add(new Vector3(0.5f, 0f, 0f), Vector3.up, corner + new Vector2(size, 0f));
                int c = Add(new Vector3(-0.5f, 0f, 1f), Vector3.up, corner + new Vector2(0f, size));
                int d = Add(new Vector3(0.5f, 0f, 1f), Vector3.up, corner + new Vector2(size, size));
                Tri(a, c, b, Vector3.up);
                Tri(b, c, d, Vector3.up);
            }

            // A pumpkin seed lying flat: a pointed oval, puffed in the middle.
            public void Seed(Vector3 at, float turn, float length, Vector2 c)
            {
                var rot = Quaternion.Euler(0f, turn, 0f);
                const int Points = 8;
                int top = Add(at + Vector3.up * length * 0.16f, Vector3.up, c);
                int bottom = Add(at, Vector3.down, c);
                int first = v.Count;
                for (int i = 0; i < Points; i++)
                {
                    float a = i * Mathf.PI * 2f / Points;
                    float x = Mathf.Cos(a) * length * 0.5f;
                    float z = Mathf.Sin(a) * length * 0.3f * (1f - 0.35f * Mathf.Cos(a));
                    var p = rot * new Vector3(x, 0f, z);
                    Add(at + p + Vector3.up * length * 0.04f, (p.normalized + Vector3.up * 0.3f).normalized, c);
                }
                for (int i = 0; i < Points; i++)
                {
                    int a = first + i, b = first + (i + 1) % Points;
                    Tri(top, a, b, Vector3.up);
                    Tri(bottom, b, a, Vector3.down);
                }
            }
        }
    }
}

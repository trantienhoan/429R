using System.Collections.Generic;
using Game.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using static UnityEngine.ParticleSystem;

namespace Game.Bowling.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Bowling > Set Up Bowling Look (Gate 2, "Strike the Slimes", 2026-11-08). Builds the look pieces
    /// the arena menu puts together, all from code with a fixed random seed (a rebuild looks the same):
    /// - Models/Bowling: Bowling_Dirt (the 5 x 5 m rounded dirt island with an earth skirt), Fire_Plinth (a small round
    ///   one), Vine_0..7 (one vine from the middle to each ball sprout) and Vine_Leaf (a leaf card).
    /// - Materials/Bowling: Bowling_Dirt_Mat (dirt_ground_new, capped at 1024 on Android), Bowling_Vine_Mat, and the
    ///   no-fog fire materials Bowling_Fire_Flame_Mat / Bowling_Fire_Ember_Mat.
    /// - Prefabs/Bowling: Bowling_Vines (Vine Grow, hidden until the seeds), Fire_Column (a 4 m flame on logs on a dirt
    ///   plinth, no light, ignites when switched on) and Lane_Sparkles (blinking points along a lane's rails and over its
    ///   pin deck).
    /// Built in a preview scene: the open scene is never touched. Run it before Set Up Bowling Arena.
    /// </summary>
    public static class BowlingLookSetupMenu
    {
        private const string MenuPath = "Tools/429 Game/Bowling/Set Up Bowling Look";
        private const string Log = "[Bowling]";
        private const string PrefabFolder = "Assets/Prefabs/Bowling";
        private const string MatFolder = "Assets/Materials/Bowling";
        private const string ModelFolder = "Assets/Models/Bowling";

        private const string DirtMeshPath = ModelFolder + "/Bowling_Dirt.asset";
        private const string PlinthMeshPath = ModelFolder + "/Fire_Plinth.asset";
        private const string LeafMeshPath = ModelFolder + "/Vine_Leaf.asset";
        private const string DirtMatPath = MatFolder + "/Bowling_Dirt_Mat.mat";
        private const string VineMatPath = MatFolder + "/Bowling_Vine_Mat.mat";
        private const string FlameMatPath = MatFolder + "/Bowling_Fire_Flame_Mat.mat";
        private const string EmberMatPath = MatFolder + "/Bowling_Fire_Ember_Mat.mat";
        private const string VinesPath = PrefabFolder + "/Bowling_Vines.prefab";
        private const string FirePath = PrefabFolder + "/Fire_Column.prefab";
        // The fire columns' flame: the Bonus Area's Danger_Flame, this big (it scales with its transform).
        private const string DangerFlamePath = "Assets/Prefabs/Light/Danger_Flame.prefab";
        private const float DangerFlameScale = 1.5f;
        private const string SparklesPath = PrefabFolder + "/Lane_Sparkles.prefab";

        private const string DirtTexture = "Assets/Images/dirt_ground_new.png";
        private const string FlameTexture = "Assets/XRI_Examples/Props/Textures/CandleFlame.png";
        private const string GlowTexture = "Assets/Lana Studio/Hyper Casual FX/Textures/Circle01.png";
        private const string TwinkleMatPath = "Assets/Data/Effects/Firework Twinkle.mat";
        private const string LeavesMatPath = "Assets/Materials/Pumpkin_Leaves_Mat.mat";
        private const string LeafParticleMatPath = "Assets/Materials/Pumpkin_Leaf_Particle_Mat.mat";
        private const string LogsPath = "Assets/Models/PlayGround/Fire_Woods.fbx";
        private const string PuffPath = "Assets/Prefabs/Effects/Smoke/Smoke_Flash_Small_Puff.prefab";
        private const string GrowSoundPath = "Assets/Audio/SFX/A_magical_glowing.wav";
        private const string LitShader = "Universal Render Pipeline/Lit";
        private const string NoFogShader = "429/Particles/Unlit No Fog";

        private const int Seed = 429;
        private const int AndroidSize = 1024;

        // Dirt and plinth (arena numbers live in BowlingLayout).
        private const int DirtCornerSteps = 16;          // 4 x 16 = 64 rim verts
        private const float PlinthHalf = 0.45f, PlinthSkirt = 0.3f;
        private const int PlinthCornerSteps = 8;

        // Vines.
        private const float VineRootOut = 0.2f;           // starts 0.2 m out from the middle, toward its sprout
        private const float RingStep = 0.08f;
        private const int Sides = 8;
        private const float RootRadius = 0.035f, TipRadius = 0.012f;

        // Fire column.
        private const float LogsScale = 0.55f;

        [MenuItem(MenuPath)]
        public static void Menu() => Run();

        [MenuItem(MenuPath, true)]
        private static bool CanRun() => !EditorApplication.isPlaying;

        /// <summary>Builds the meshes, materials and the 3 prefabs. False (and nothing built) when something it needs is missing.</summary>
        public static bool Run()
        {
            if (EditorApplication.isPlaying) { Debug.LogError($"{Log} Stop play mode first."); return false; }

            var dirtTexture = Load<Texture2D>(DirtTexture);
            var flameTexture = Load<Texture2D>(FlameTexture);
            var glowTexture = Load<Texture2D>(GlowTexture);
            var twinkleMat = Load<Material>(TwinkleMatPath);
            var leavesMat = Load<Material>(LeavesMatPath);
            var leafParticleMat = Load<Material>(LeafParticleMatPath);
            var logs = Load<GameObject>(LogsPath);
            var puff = Load<GameObject>(PuffPath);
            var growSound = Load<AudioClip>(GrowSoundPath);

            var missing = new List<string>();
            if (dirtTexture == null) missing.Add(DirtTexture);
            if (flameTexture == null) missing.Add(FlameTexture);
            if (glowTexture == null) missing.Add(GlowTexture);
            if (twinkleMat == null) missing.Add(TwinkleMatPath);
            if (leavesMat == null) missing.Add(LeavesMatPath);
            if (leafParticleMat == null) missing.Add(LeafParticleMatPath);
            if (logs == null) missing.Add(LogsPath);
            if (puff == null) missing.Add(PuffPath);
            if (growSound == null) missing.Add(GrowSoundPath);
            if (Shader.Find(LitShader) == null) missing.Add("shader " + LitShader);
            if (Shader.Find(NoFogShader) == null) missing.Add("shader " + NoFogShader);
            if (missing.Count > 0)
            {
                Debug.LogError($"{Log} Set Up Bowling Look: nothing built, missing:\n" + string.Join("\n", missing));
                return false;
            }

            Folder("Assets/Prefabs", "Bowling");
            Folder("Assets/Materials", "Bowling");
            Folder("Assets/Models", "Bowling");
            var notes = new List<string>();

            // Materials.
            notes.Add(CapAndroid(DirtTexture));
            var dirtMat = LitMaterial(DirtMatPath, dirtTexture, new Color(0.9f, 0.9f, 0.9f), 0.05f);
            var vineMat = LitMaterial(VineMatPath, null, new Color(0.22f, 0.42f, 0.12f), 0.25f);
            var flameMat = NoFogMaterial(FlameMatPath, flameTexture);
            var emberMat = NoFogMaterial(EmberMatPath, glowTexture);

            // Meshes.
            var dirtMesh = SaveMesh(DirtMeshPath, "Bowling_Dirt", m => Slab(m, BowlingLayout.DirtHalf, BowlingLayout.DirtCorner,
                DirtCornerSteps, BowlingLayout.DirtTop, BowlingLayout.DirtSkirt));
            var plinthMesh = SaveMesh(PlinthMeshPath, "Fire_Plinth", m => Slab(m, PlinthHalf, PlinthHalf, PlinthCornerSteps,
                BowlingLayout.DirtTop, PlinthSkirt));
            var leafMesh = SaveMesh(LeafMeshPath, "Vine_Leaf", LeafCard);
            var rng = new System.Random(Seed);
            int count = BowlingLayout.SproutYaws.Length;
            var vines = new VineData[count];
            for (int i = 0; i < count; i++)
            {
                var data = new VineData();
                data.mesh = SaveMesh($"{ModelFolder}/Vine_{i}.asset", $"Vine_{i}", m => VineMesh(m, i, rng, data));
                vines[i] = data;
            }
            notes.Add($"meshes: Bowling_Dirt ({dirtMesh.vertexCount} verts), Fire_Plinth, Vine_Leaf, Vine_0..{count - 1} " +
                      $"(lengths {string.Join(", ", System.Array.ConvertAll(vines, v => v.length.ToString("0.00")))} m)");

            // Prefabs.
            int vineParticles = BuildVines(vines, vineMat, leavesMat, leafMesh, leafParticleMat, puff, growSound);
            if (vineParticles < 0) return false;
            notes.Add($"Bowling_Vines: {count} vines, particle max {vineParticles}");
            int fireParticles = BuildFireColumn(plinthMesh, dirtMat, logs, flameMat, emberMat, out string logsNote);
            if (fireParticles < 0) return false;
            notes.Add($"Fire_Column: particle max {fireParticles} (x8 = {fireParticles * 8}), {logsNote}");
            int sparkleParticles = BuildSparkles(twinkleMat);
            if (sparkleParticles < 0) return false;
            notes.Add($"Lane_Sparkles: particle max {sparkleParticles} (x4 = {sparkleParticles * 4})");
            notes.Add($"arena look particle max {fireParticles * 8 + sparkleParticles * 4 + vineParticles} (budget 650)");

            AssetDatabase.SaveAssets();
            Debug.Log($"{Log} Set Up Bowling Look: " + string.Join("; ", notes) + ". Next: Set Up Slime Pins, Set Up Giant " +
                      "Slime And Showdown, Set Up Bowling Arena, Use Bowling For Gate 2.");
            return true;
        }

        // ------------------------------------------------------------------ dirt and plinth

        // A rounded-square slab: a flat top (UV = xz / 2), an earth skirt down the side (u = rim length / 2, v = depth / 2)
        // and a bottom, so it reads as a floating island from any side.
        private static void Slab(Mesh mesh, float half, float corner, int cornerSteps, float top, float skirt)
        {
            corner = Mathf.Clamp(corner, 0.01f, half);
            var rim = new List<Vector3>();
            var outward = new List<Vector3>();
            float inner = half - corner;
            var centres = new[] { new Vector2(inner, inner), new Vector2(-inner, inner), new Vector2(-inner, -inner), new Vector2(inner, -inner) };
            for (int c = 0; c < 4; c++)
            {
                for (int s = 0; s < cornerSteps; s++)
                {
                    float angle = (c * 90f + 90f * s / (cornerSteps - 1)) * Mathf.Deg2Rad;
                    var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    var point = new Vector3(centres[c].x, 0f, centres[c].y) + dir * corner;
                    // Round (corner = half): neighbouring arcs meet in one point; keep it once.
                    if (rim.Count > 0 && (rim[^1] - point).sqrMagnitude < 1e-8f) continue;
                    if (rim.Count > 0 && c == 3 && s == cornerSteps - 1 && (rim[0] - point).sqrMagnitude < 1e-8f) continue;
                    rim.Add(point);
                    outward.Add(dir);
                }
            }
            int n = rim.Count;
            float bottom = top - skirt;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            // Top.
            int topCentre = vertices.Count;
            vertices.Add(new Vector3(0f, top, 0f)); normals.Add(Vector3.up); uvs.Add(Vector2.zero);
            for (int i = 0; i < n; i++)
            {
                vertices.Add(rim[i] + Vector3.up * top); normals.Add(Vector3.up); uvs.Add(new Vector2(rim[i].x, rim[i].z) * 0.5f);
            }
            for (int i = 0; i < n; i++) Tri(triangles, vertices, topCentre, topCentre + 1 + i, topCentre + 1 + (i + 1) % n, Vector3.up);

            // Skirt (the first rim point again at the end, so the texture wraps without a jump).
            int skirtStart = vertices.Count;
            float along = 0f;
            for (int i = 0; i <= n; i++)
            {
                int r = i % n;
                if (i > 0) along += (rim[r] - rim[i - 1]).magnitude;
                vertices.Add(rim[r] + Vector3.up * top); normals.Add(outward[r]); uvs.Add(new Vector2(along * 0.5f, 0f));
                vertices.Add(rim[r] + Vector3.up * bottom); normals.Add(outward[r]); uvs.Add(new Vector2(along * 0.5f, skirt * 0.5f));
            }
            for (int i = 0; i < n; i++)
            {
                int a = skirtStart + i * 2, b = a + 1, c = a + 2, d = a + 3;
                var outwards = outward[i] + outward[(i + 1) % n];
                Tri(triangles, vertices, a, b, c, outwards);
                Tri(triangles, vertices, c, b, d, outwards);
            }

            // Bottom.
            int bottomCentre = vertices.Count;
            vertices.Add(new Vector3(0f, bottom, 0f)); normals.Add(Vector3.down); uvs.Add(Vector2.zero);
            for (int i = 0; i < n; i++)
            {
                vertices.Add(rim[i] + Vector3.up * bottom); normals.Add(Vector3.down); uvs.Add(new Vector2(rim[i].x, rim[i].z) * 0.5f);
            }
            for (int i = 0; i < n; i++) Tri(triangles, vertices, bottomCentre, bottomCentre + 1 + i, bottomCentre + 1 + (i + 1) % n, Vector3.down);

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
        }

        // ------------------------------------------------------------------ vines

        private sealed class VineData
        {
            public Mesh mesh;
            public int[] ringIndexEnds;
            public float length;
            public readonly List<(Vector3 at, Quaternion turn, float size, float share)> leaves = new();
        }

        // Vine i: an S-curve over the dirt from 0.2 m out to its ball sprout, then a little curl rising at the tip. A tube
        // (8 sides, a ring every 0.08 m, 0.035 -> 0.012 m thick) whose vertices and triangles run ring by ring from the
        // root, so Vine Grow shows it growing by drawing more of it. Also picks its 3-4 leaf spots.
        private static void VineMesh(Mesh mesh, int index, System.Random rng, VineData data)
        {
            var sprout = BowlingLayout.SproutPoint(index);
            sprout.y = 0f;
            var dir = sprout.normalized;
            var side = Vector3.Cross(Vector3.up, dir).normalized;
            var from = dir * VineRootOut;
            float amp = Range(rng, 0.15f, 0.25f) * (rng.Next(2) == 0 ? -1f : 1f);
            float waves = Range(rng, 1f, 1.8f);
            float arch = Range(rng, 0.03f, 0.1f);
            float top = BowlingLayout.DirtTop;

            // The S-curve, finely sampled (the root starts half in the dirt).
            var dense = new List<Vector3>();
            const int SSteps = 120;
            for (int s = 0; s <= SSteps; s++)
            {
                float t = (float)s / SSteps;
                var p = Vector3.Lerp(from, sprout, t) + side * (amp * Mathf.Sin(2f * Mathf.PI * waves * t) * Mathf.Sin(Mathf.PI * t));
                p.y = top - 0.02f * (1f - t) + arch * Mathf.Sin(Mathf.PI * t) + 0.005f * t;
                dense.Add(p);
            }
            float sLength = Length(dense);

            // The curl: from the tip on along its heading, round to one side, shrinking and rising.
            var heading = dense[^1] - dense[^2];
            heading.y = 0f;
            heading.Normalize();
            var curlSide = Vector3.Cross(Vector3.up, heading).normalized * (rng.Next(2) == 0 ? -1f : 1f);
            float curlRadius = Range(rng, 0.05f, 0.07f);
            float turns = Range(rng, 1.1f, 1.4f);
            float rise = Range(rng, 0.1f, 0.15f);
            var tip = dense[^1];
            var curlCentre = tip + curlSide * curlRadius;
            const int CurlSteps = 60;
            for (int s = 1; s <= CurlSteps; s++)
            {
                float u = (float)s / CurlSteps;
                float phi = u * turns * 2f * Mathf.PI;
                float radius = curlRadius * (1f - 0.6f * u);
                dense.Add(curlCentre + radius * (-curlSide * Mathf.Cos(phi) + heading * Mathf.Sin(phi)) + Vector3.up * (rise * Mathf.Pow(u, 1.5f)));
            }
            float length = Length(dense);
            data.length = length;

            // Rings every 0.08 m along it (the last one at the very tip).
            int rings = Mathf.Max(2, Mathf.CeilToInt(length / RingStep) + 1);
            var centres = new Vector3[rings];
            var tangents = new Vector3[rings];
            for (int r = 0; r < rings; r++)
            {
                float d = Mathf.Min(r * RingStep, length);
                if (r == rings - 1) d = length;
                PointAt(dense, d, out centres[r], out tangents[r]);
            }

            // Frames carried along the curve (no sudden twists).
            var normal = Vector3.ProjectOnPlane(Vector3.up, tangents[0]).normalized;
            if (normal.sqrMagnitude < 0.5f) normal = Vector3.ProjectOnPlane(side, tangents[0]).normalized;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            var ends = new int[rings];
            for (int r = 0; r < rings; r++)
            {
                if (r > 0)
                {
                    var turn = Quaternion.FromToRotation(tangents[r - 1], tangents[r]);
                    normal = Vector3.ProjectOnPlane(turn * normal, tangents[r]).normalized;
                }
                var binormal = Vector3.Cross(tangents[r], normal);
                float share = (float)r / (rings - 1);
                float radius = Mathf.Lerp(RootRadius, TipRadius, share);
                for (int s = 0; s < Sides; s++)
                {
                    float angle = 2f * Mathf.PI * s / Sides;
                    var outward = Mathf.Cos(angle) * normal + Mathf.Sin(angle) * binormal;
                    vertices.Add(centres[r] + outward * radius);
                    normals.Add(outward);
                    uvs.Add(new Vector2((float)s / Sides, r * RingStep));
                }
                if (r == 0) continue;
                int a0 = (r - 1) * Sides, b0 = r * Sides;
                for (int s = 0; s < Sides; s++)
                {
                    int a = a0 + s, b = a0 + (s + 1) % Sides, c = b0 + s, d = b0 + (s + 1) % Sides;
                    var outwards = normals[a] + normals[b];
                    Tri(triangles, vertices, a, c, b, outwards);
                    Tri(triangles, vertices, b, c, d, outwards);
                }
                ends[r] = triangles.Count;
            }
            // A point closing the tip.
            int last = (rings - 1) * Sides;
            int point = vertices.Count;
            vertices.Add(centres[rings - 1] + tangents[rings - 1] * TipRadius);
            normals.Add(tangents[rings - 1]);
            uvs.Add(new Vector2(0.5f, length));
            for (int s = 0; s < Sides; s++)
            {
                int a = last + s, b = last + (s + 1) % Sides;
                Tri(triangles, vertices, a, point, b, normals[a] + tangents[rings - 1]);
            }
            ends[rings - 1] = triangles.Count;
            data.ringIndexEnds = ends;

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

            // 3-4 leaves along the S-curve, on alternate sides, tilted up a little.
            int leafCount = rng.Next(3, 5);
            float leafSide = rng.Next(2) == 0 ? -1f : 1f;
            for (int j = 0; j < leafCount; j++)
            {
                float u = 0.22f + 0.6f * j / (leafCount - 1) + Range(rng, -0.04f, 0.04f);
                float d = u * sLength;
                PointAt(dense, d, out var at, out var along);
                var flat = Vector3.ProjectOnPlane(along, Vector3.up).normalized;
                var outFlat = Quaternion.AngleAxis(leafSide * Range(rng, 55f, 80f), Vector3.up) * flat;
                float pitch = Range(rng, 15f, 30f) * Mathf.Deg2Rad;
                var leafDir = (outFlat * Mathf.Cos(pitch) + Vector3.up * Mathf.Sin(pitch)).normalized;
                float radius = Mathf.Lerp(RootRadius, TipRadius, d / length);
                data.leaves.Add((at + outFlat * radius * 0.8f + Vector3.up * 0.005f, Quaternion.LookRotation(leafDir, Vector3.up),
                                 Range(rng, 0.25f, 0.35f), d / length));
                leafSide = -leafSide;
            }
        }

        // A 1 x 1 card lying flat, its stem end at the pivot and the leaf along +Z; the top-left (green) leaf of the
        // pumpkin leaf sheet.
        private static void LeafCard(Mesh mesh)
        {
            var vertices = new List<Vector3> { new(-0.5f, 0f, 0f), new(0.5f, 0f, 0f), new(-0.5f, 0f, 1f), new(0.5f, 0f, 1f) };
            var uvs = new List<Vector2> { new(0f, 0.5f), new(0.5f, 0.5f), new(0f, 1f), new(0.5f, 1f) };
            var triangles = new List<int>();
            Tri(triangles, vertices, 0, 2, 1, Vector3.up);
            Tri(triangles, vertices, 1, 2, 3, Vector3.up);
            mesh.SetVertices(vertices);
            mesh.SetNormals(new List<Vector3> { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
        }

        // Bowling_Vines: Vine Grow on the root (placed at the arena's middle), one child per vine with its leaves, and a
        // small leaf burst. No colliders, no shadows. Returns its particle max (-1 = not saved).
        private static int BuildVines(VineData[] vines, Material vineMat, Material leavesMat, Mesh leafMesh, Material leafParticleMat,
                                      GameObject puff, AudioClip growSound)
        {
            var build = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Bowling_Vines");
            SceneManager.MoveGameObjectToScene(root, build);
            try
            {
                var groups = BowlingLayout.SproutGroups;
                var vineObjects = new MeshFilter[vines.Length];
                var vineGroups = new int[vines.Length];
                var leafObjects = new GameObject[vines.Length][];
                int group = 0, inGroup = 0;
                for (int i = 0; i < vines.Length; i++)
                {
                    while (group < groups.Length - 1 && inGroup >= groups[group]) { group++; inGroup = 0; }
                    vineGroups[i] = group;
                    inGroup++;

                    var vine = new GameObject($"Vine {i}");
                    vine.transform.SetParent(root.transform, false);
                    vineObjects[i] = vine.AddComponent<MeshFilter>();
                    vineObjects[i].sharedMesh = vines[i].mesh;
                    Quiet(vine.AddComponent<MeshRenderer>(), vineMat);

                    var leaves = vines[i].leaves;
                    leafObjects[i] = new GameObject[leaves.Count];
                    for (int j = 0; j < leaves.Count; j++)
                    {
                        var leaf = new GameObject($"Leaf {j}");
                        leaf.transform.SetParent(vine.transform, false);
                        leaf.transform.SetLocalPositionAndRotation(leaves[j].at, leaves[j].turn);
                        leaf.transform.localScale = Vector3.one * leaves[j].size;
                        leaf.AddComponent<MeshFilter>().sharedMesh = leafMesh;
                        Quiet(leaf.AddComponent<MeshRenderer>(), leavesMat);
                        var grow = new SerializedObject(leaf.AddComponent<Game.GrowIn>());
                        grow.FindProperty("growParentCollider").boolValue = false;
                        grow.FindProperty("startAtParentSize").boolValue = false;
                        grow.FindProperty("startShare").floatValue = 0.05f;
                        grow.FindProperty("growSeconds").floatValue = 0.35f;
                        grow.ApplyModifiedPropertiesWithoutUndo();
                        leaf.SetActive(false);
                        leafObjects[i][j] = leaf;
                    }
                }

                var burst = LeafBurst(root, leafParticleMat);

                var vineGrow = root.AddComponent<VineGrow>();
                var so = new SerializedObject(vineGrow);
                var list = so.FindProperty("vines");
                list.arraySize = vines.Length;
                for (int i = 0; i < vines.Length; i++)
                {
                    var element = list.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("filter").objectReferenceValue = vineObjects[i];
                    element.FindPropertyRelative("group").intValue = vineGroups[i];
                    var ends = element.FindPropertyRelative("ringIndexEnds");
                    ends.arraySize = vines[i].ringIndexEnds.Length;
                    for (int r = 0; r < vines[i].ringIndexEnds.Length; r++) ends.GetArrayElementAtIndex(r).intValue = vines[i].ringIndexEnds[r];
                    var leaves = element.FindPropertyRelative("leaves");
                    var shares = element.FindPropertyRelative("leafShares");
                    leaves.arraySize = leafObjects[i].Length;
                    shares.arraySize = leafObjects[i].Length;
                    for (int j = 0; j < leafObjects[i].Length; j++)
                    {
                        leaves.GetArrayElementAtIndex(j).objectReferenceValue = leafObjects[i][j];
                        shares.GetArrayElementAtIndex(j).floatValue = vines[i].leaves[j].share;
                    }
                }
                so.FindProperty("growSeconds").floatValue = 0.9f;
                so.FindProperty("puff").objectReferenceValue = puff;
                so.FindProperty("puffScale").floatValue = 0.5f;
                so.FindProperty("growSound").objectReferenceValue = growSound;
                so.FindProperty("volume").floatValue = 0.7f;
                so.FindProperty("leafBurst").objectReferenceValue = burst;
                so.FindProperty("burstLeaves").intValue = 6;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, VinesPath, out bool saved);
                if (!saved)
                {
                    Debug.LogError($"{Log} Could not save {VinesPath} (read-only or locked?).");
                    return -1;
                }
                return ParticleMax(root);
            }
            finally
            {
                Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(build);
            }
        }

        // 6 pumpkin leaves thrown up from the middle as a group of vines pops out (Vine Grow emits them; none on its own).
        private static ParticleSystem LeafBurst(GameObject root, Material material)
        {
            var go = DangerFlameSetupMenu.Child(root, "Leaf Burst");
            go.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 2f;
            main.loop = true;
            main.prewarm = false;
            main.playOnAwake = true;                 // playing, but drops nothing until Vine Grow says so
            main.startLifetime = new MinMaxCurve(1.5f, 2.2f);
            main.startSpeed = new MinMaxCurve(1.2f, 2.2f);
            main.startSize = new MinMaxCurve(0.15f, 0.25f);
            main.startRotation = new MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = Color.white;
            main.gravityModifier = 0.4f;
            main.maxParticles = 6;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 55f;
            shape.radius = 0.25f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            shape.position = Vector3.zero;
            var spin = ps.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new MinMaxCurve(-2f, 2f);
            DangerFlameSetupMenu.Wobble(ps, 0.6f, 0.5f);
            var sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.numTilesX = 2;
            sheet.numTilesY = 2;
            sheet.startFrame = new MinMaxCurve(0f, 3.999f);
            sheet.frameOverTime = new MinMaxCurve(0f);
            DangerFlameSetupMenu.Fade(ps, new[] { DangerFlameSetupMenu.Key(1f, 1f, 1f, 0f), DangerFlameSetupMenu.Key(1f, 1f, 1f, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        // ------------------------------------------------------------------ fire column

        // Fire_Column: a dirt plinth with logs on it and the Bonus Area's Danger_Flame (x1.5, its light off). No colliders. Returns its particle max (-1 = not saved).
        private static int BuildFireColumn(Mesh plinthMesh, Material dirtMat, GameObject logsModel, Material flameMat, Material emberMat,
                                           out string logsNote)
        {
            var build = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Fire_Column");
            SceneManager.MoveGameObjectToScene(root, build);
            try
            {
                var plinth = DangerFlameSetupMenu.Child(root, "Plinth");
                plinth.AddComponent<MeshFilter>().sharedMesh = plinthMesh;
                Quiet(plinth.AddComponent<MeshRenderer>(), dirtMat);

                // The logs, centred on the plinth and standing on it.
                var logs = (GameObject)Object.Instantiate(logsModel, root.transform, false);
                logs.name = "Logs";
                logs.transform.localScale = Vector3.one * LogsScale;
                foreach (var collider in logs.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                foreach (var animator in logs.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(animator);
                float logsTop = BowlingLayout.DirtTop + 0.1f;
                var renderers = logs.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length > 0)
                {
                    var bounds = renderers[0].bounds;
                    foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                    logs.transform.localPosition = new Vector3(-bounds.center.x, BowlingLayout.DirtTop - bounds.min.y, -bounds.center.z);
                    logsTop = BowlingLayout.DirtTop + bounds.size.y;
                    logsNote = $"logs {bounds.size.x:0.00} x {bounds.size.y:0.00} x {bounds.size.z:0.00} m";
                }
                else logsNote = "logs have no renderer";
                foreach (var r in renderers)
                {
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    r.receiveShadows = false;
                }

                // The flames start in the logs.
                float flameAt = BowlingLayout.DirtTop + (logsTop - BowlingLayout.DirtTop) * 0.4f;
                // The flame is the Bonus Area's Danger_Flame (user, 2026-11-08), linked to its prefab, so changes to it
                // show here too. Its own point light is off: 8 more lights would be too many on Quest.
                var dangerFlame = AssetDatabase.LoadAssetAtPath<GameObject>(DangerFlamePath);
                if (dangerFlame != null)
                {
                    var flame = (GameObject)PrefabUtility.InstantiatePrefab(dangerFlame, build);
                    flame.transform.SetParent(root.transform, false);
                    flame.transform.localPosition = new Vector3(0f, flameAt, 0f);
                    flame.transform.localScale = Vector3.one * DangerFlameScale;
                    foreach (var light in flame.GetComponentsInChildren<Light>(true)) light.gameObject.SetActive(false);
                }
                else
                {
                    Debug.LogWarning($"{Log} No {DangerFlamePath}: the fire column gets its own flame instead.");
                    FireCore(Flame(root, "Flame Core", flameAt), flameMat);
                    FireOuter(Flame(root, "Flame Outer", flameAt), flameMat);
                    FireEmbers(Flame(root, "Embers", flameAt), emberMat);
                }

                PrefabUtility.SaveAsPrefabAsset(root, FirePath, out bool saved);
                if (!saved)
                {
                    Debug.LogError($"{Log} Could not save {FirePath} (read-only or locked?).");
                    return -1;
                }
                return ParticleMax(root);
            }
            finally
            {
                Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(build);
            }
        }

        private static GameObject Flame(GameObject root, string name, float y)
        {
            var go = DangerFlameSetupMenu.Child(root, name);
            go.transform.localPosition = new Vector3(0f, y, 0f);
            return go;
        }

        // The bright heart: about 4 m tall and 0.8 m wide (Danger_Flame's core, taller and narrower).
        private static void FireCore(GameObject go, Material material)
        {
            var ps = DangerFlameSetupMenu.NewSystem(go, material, ParticleSystemRenderMode.VerticalBillboard, 32);
            var main = ps.main;
            main.prewarm = false;
            main.startLifetime = new MinMaxCurve(1.0f, 1.3f);
            main.startSize = new MinMaxCurve(0.6f, 0.85f);
            main.startRotation = new MinMaxCurve(-0.25f, 0.25f);
            var emission = ps.emission;
            emission.rateOverTime = 20f;
            Disc(ps, 0.14f, 0f);
            Rise(ps, 3.0f, 3.8f, 0.05f);
            DangerFlameSetupMenu.Fade(ps,
                new[] { DangerFlameSetupMenu.Key(1f, 0.95f, 0.65f, 0f), DangerFlameSetupMenu.Key(1f, 0.62f, 0.16f, 0.25f),
                        DangerFlameSetupMenu.Key(0.95f, 0.22f, 0.03f, 0.6f), DangerFlameSetupMenu.Key(0.45f, 0.03f, 0f, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.85f, 0.6f), new GradientAlphaKey(0f, 1f) });
            DangerFlameSetupMenu.Shrink(ps, 0.7f, 1f, 0.2f);
            DangerFlameSetupMenu.Wobble(ps, 0.2f, 1.5f);
        }

        // A wider, darker red flame round the core.
        private static void FireOuter(GameObject go, Material material)
        {
            var ps = DangerFlameSetupMenu.NewSystem(go, material, ParticleSystemRenderMode.VerticalBillboard, 14);
            var main = ps.main;
            main.prewarm = false;
            main.startLifetime = new MinMaxCurve(0.8f, 1.1f);
            main.startSize = new MinMaxCurve(0.9f, 1.2f);
            main.startRotation = new MinMaxCurve(-0.35f, 0.35f);
            var emission = ps.emission;
            emission.rateOverTime = 8f;
            Disc(ps, 0.22f, 0f);
            Rise(ps, 2.4f, 3.0f, 0.06f);
            DangerFlameSetupMenu.Fade(ps,
                new[] { DangerFlameSetupMenu.Key(1f, 0.45f, 0.1f, 0f), DangerFlameSetupMenu.Key(0.85f, 0.12f, 0.02f, 0.5f),
                        DangerFlameSetupMenu.Key(0.3f, 0.01f, 0f, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.6f, 0.15f), new GradientAlphaKey(0.4f, 0.6f), new GradientAlphaKey(0f, 1f) });
            DangerFlameSetupMenu.Shrink(ps, 0.8f, 1f, 0.4f);
            DangerFlameSetupMenu.Wobble(ps, 0.25f, 1.1f);
            ps.GetComponent<ParticleSystemRenderer>().sortingFudge = 5f;
        }

        // Sparks spitting up and swirling away.
        private static void FireEmbers(GameObject go, Material material)
        {
            var ps = DangerFlameSetupMenu.NewSystem(go, material, ParticleSystemRenderMode.Billboard, 12);
            var main = ps.main;
            main.prewarm = false;
            main.startLifetime = new MinMaxCurve(1.0f, 2.0f);
            main.startSize = new MinMaxCurve(0.05f, 0.09f);
            var emission = ps.emission;
            emission.rateOverTime = 5f;
            Disc(ps, 0.25f, 0.2f);
            Rise(ps, 2f, 4f, 0.35f);
            DangerFlameSetupMenu.Fade(ps,
                new[] { DangerFlameSetupMenu.Key(1f, 0.85f, 0.4f, 0f), DangerFlameSetupMenu.Key(1f, 0.4f, 0.08f, 0.5f),
                        DangerFlameSetupMenu.Key(0.8f, 0.1f, 0f, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            DangerFlameSetupMenu.Wobble(ps, 0.8f, 2f);
        }

        // Particles start on a flat disc (radius, at height y). Copied from Danger Flame (private there).
        private static void Disc(ParticleSystem ps, float radius, float y)
        {
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.rotation = new Vector3(90f, 0f, 0f);
            shape.position = new Vector3(0f, y, 0f);
        }

        // Upward speed (min..max m/s) with a little sideways drift. Copied from Danger Flame (private there).
        private static void Rise(ParticleSystem ps, float min, float max, float drift)
        {
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new MinMaxCurve(-drift, drift);
            velocity.y = new MinMaxCurve(min, max);
            velocity.z = new MinMaxCurve(-drift, drift);
        }

        // ------------------------------------------------------------------ sparkles

        // Lane_Sparkles, in the lane's frame (origin at the arena's middle, +Z along the lane): blinking points tracing the
        // rails, the mouth and the tip, and more twinkling over the pin deck. Prewarmed, so a lit lane is full at once.
        private static int BuildSparkles(Material twinkleMat)
        {
            var build = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Lane_Sparkles");
            SceneManager.MoveGameObjectToScene(root, build);
            try
            {
                float start = BowlingLayout.FoulLine + 0.1f, end = BowlingLayout.LaneEnd - 0.2f;   // 2.6 -> 22.4 (19.8 m)
                var outline = DangerFlameSetupMenu.Child(root, "Outline Twinkle");
                outline.transform.localPosition = new Vector3(0f, 0.22f, (start + end) * 0.5f);
                var ps = Twinkle(outline, twinkleMat, 18, 10f, 0.8f, 1.4f, 0.04f, 0.07f);
                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.BoxEdge;
                shape.scale = new Vector3(BowlingLayout.RailSide * 2f, 0f, end - start);
                var velocity = ps.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.Local;
                velocity.x = new MinMaxCurve(0f, 0f);
                velocity.y = new MinMaxCurve(0.05f, 0.15f);
                velocity.z = new MinMaxCurve(0f, 0f);

                var deck = DangerFlameSetupMenu.Child(root, "Deck Twinkle");
                deck.transform.localPosition = new Vector3(0f, 1.1f, BowlingLayout.DeckAt);
                ps = Twinkle(deck, twinkleMat, 16, 8f, 0.6f, 1.2f, 0.05f, 0.1f);
                shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(1.8f, 1.6f, 2.2f);

                PrefabUtility.SaveAsPrefabAsset(root, SparklesPath, out bool saved);
                if (!saved)
                {
                    Debug.LogError($"{Log} Could not save {SparklesPath} (read-only or locked?).");
                    return -1;
                }
                return ParticleMax(root);
            }
            finally
            {
                Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(build);
            }
        }

        // A warm gold blink: each point grows from nothing and back (0 -> 1 -> 0) over its short life.
        private static ParticleSystem Twinkle(GameObject go, Material material, int max, float rate, float lifeMin, float lifeMax,
                                              float sizeMin, float sizeMax)
        {
            var ps = DangerFlameSetupMenu.NewSystem(go, material, ParticleSystemRenderMode.Billboard, max);
            var main = ps.main;
            main.prewarm = true;
            main.startLifetime = new MinMaxCurve(lifeMin, lifeMax);
            main.startSize = new MinMaxCurve(sizeMin, sizeMax);
            main.startColor = new Color(1f, 0.8f, 0.4f);
            var emission = ps.emission;
            emission.rateOverTime = rate;
            var shape = ps.shape;
            shape.enabled = true;
            shape.position = Vector3.zero;
            shape.rotation = Vector3.zero;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0f)));
            return ps;
        }

        // ------------------------------------------------------------------ materials

        // A URP Lit material (made once, then kept up to date): base map, colour, smoothness, no emission.
        private static Material LitMaterial(string path, Texture2D texture, Color colour, float smoothness)
        {
            var shader = Shader.Find(LitShader);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader) material.shader = shader;
            material.SetTexture("_BaseMap", texture);
            material.SetTexture("_MainTex", texture);
            material.SetColor("_BaseColor", colour);
            material.SetColor("_Color", colour);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);
            material.DisableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            EditorUtility.SetDirty(material);
            return material;
        }

        // An additive particle material that fog never dims (as Firework Glow.mat): Danger Flame's set-up on the no-fog shader.
        private static Material NoFogMaterial(string path, Texture2D texture)
        {
            var shader = Shader.Find(NoFogShader);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader) material.shader = shader;
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.One);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_ALPHAMODULATE_ON");
            material.SetShaderPassEnabled("DepthOnly", false);
            material.SetShaderPassEnabled("SHADOWCASTER", false);
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        // The dirt texture is 1254 px: at most 1024 on the Quest.
        private static string CapAndroid(string texturePath)
        {
            if (AssetImporter.GetAtPath(texturePath) is not TextureImporter importer) return "dirt texture: no importer";
            var android = importer.GetPlatformTextureSettings("Android");
            if (android.overridden && android.maxTextureSize == AndroidSize) return "dirt texture: Android 1024 already";
            android.overridden = true;
            android.maxTextureSize = AndroidSize;
            importer.SetPlatformTextureSettings(android);
            importer.SaveAndReimport();
            return "dirt texture: Android max size 1024";
        }

        // ------------------------------------------------------------------ helpers

        // A mesh asset filled anew (an existing file keeps its GUID, so prefabs keep pointing at it).
        private static Mesh SaveMesh(string path, string name, System.Action<Mesh> fill)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool fresh = mesh == null;
            if (fresh) mesh = new Mesh();
            mesh.Clear();
            mesh.name = name;
            fill(mesh);
            if (fresh) AssetDatabase.CreateAsset(mesh, path);
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // A triangle facing the given way (its corners swapped if they wind the other way).
        private static void Tri(List<int> triangles, List<Vector3> vertices, int a, int b, int c, Vector3 facing)
        {
            var normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            if (Vector3.Dot(normal, facing) < 0f) (b, c) = (c, b);
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }

        private static float Length(List<Vector3> points)
        {
            float length = 0f;
            for (int i = 1; i < points.Count; i++) length += (points[i] - points[i - 1]).magnitude;
            return length;
        }

        // The point and heading this far along a polyline.
        private static void PointAt(List<Vector3> points, float distance, out Vector3 point, out Vector3 tangent)
        {
            for (int i = 1; i < points.Count; i++)
            {
                var step = points[i] - points[i - 1];
                float length = step.magnitude;
                if (length < 1e-6f) continue;
                if (distance <= length || i == points.Count - 1)
                {
                    point = points[i - 1] + step * Mathf.Clamp01(distance / length);
                    tangent = step / length;
                    return;
                }
                distance -= length;
            }
            point = points[^1];
            tangent = Vector3.forward;
        }

        private static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

        private static void Quiet(Renderer renderer, Material material)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static int ParticleMax(GameObject root)
        {
            int sum = 0;
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true)) sum += ps.main.maxParticles;
            return sum;
        }

        private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

        private static void Folder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}

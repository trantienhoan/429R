using System.Collections.Generic;
using System.Linq;
using Game.Gates;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Bowling.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Bowling > Build SlimeBowl Kit (gate 2's look, 2026-11-23; concept
    /// Exports/Bowling_Concept/concept_v1.png). Brings the SlimeBowl kit (Exports/Gates/gate2/SlimeBowl_Kit.blend, exported
    /// by tools/export_slimebowl_kit.py to Assets/Models/Gates/SlimeBowl/SlimeBowl_Kit.fbx) into the game:
    /// - its import settings (no materials from the FBX), the palette textures' (no mip maps: one colour per face) and one
    ///   material, Materials/Bowling/SlimeBowl_Palette (URP Lit: the palette, gloss from its alpha, the glow map);
    /// - one prefab per piece in Prefabs/Bowling/Kit (looks only, no colliders); the flames get Kit Flame;
    /// - gate 2's own giant pumpkin, Prefabs/Bowling/Pumpkin_Ball_Boss_Bowling (a variant of the shared one with the kit's
    ///   bowling-ball pumpkin), set as the arena's Gate Run Finale Boss;
    /// - Prefabs/Bowling/Bowling_Arena dressed at the game's real sizes: the island, each lane's wood, aim arrows, rails,
    ///   trestles, deck, braziers, pumpkin arch, chains; rocks, lanterns and clouds round it; a lit lane's rune ring and two
    ///   flames come on with its Lit part; the ball vines pop up as their spots open and hold the balls at hand height in
    ///   their cups (the spots are raised, a 3-sphere cradle holds each ball). The placeholder looks are hidden (lane
    ///   cubes, the dirt mesh, the deck ring quad, the Danger_Flame fire columns: about 1,450 particles fewer, the code
    ///   vines). Every collider, the teleport area and the gameplay points stay as they were.
    /// Re-runnable (it rebuilds only its own parts). "Remove SlimeBowl Kit" puts the placeholder look back. Never touches
    /// the scene.
    /// </summary>
    public static class SlimeBowlKitMenu
    {
        private const string Log = "[Bowling]";
        private const string Root = "Tools/429 Game/Bowling/";
        private const string FbxPath = "Assets/Models/Gates/SlimeBowl/SlimeBowl_Kit.fbx";
        private const string ColourPath = "Assets/M_Textures/Gates/SlimeBowl/SlimeBowl_Palette.png";
        private const string GlowPath = "Assets/M_Textures/Gates/SlimeBowl/SlimeBowl_Palette_Emission.png";
        private const string MatPath = "Assets/Materials/Bowling/SlimeBowl_Palette.mat";
        private const string PieceFolder = "Assets/Prefabs/Bowling/Kit";
        private const string ArenaPath = "Assets/Prefabs/Bowling/Bowling_Arena.prefab";
        private const string BallPath = "Assets/Prefabs/Bowling/Bowling_Ball.prefab";
        private const string BossBasePath = "Assets/Prefabs/Gates/Pumpkin_Ball_Boss.prefab";
        private const string Kit = "SlimeBowl Kit";
        private const string LitKit = "SlimeBowl Kit (lit)";
        private const string Prefix = "SlimeBowl_Kit_";

        // The kit's numbers (Exports/Gates/gate2/tools/slimebowl_kit.py; the game's from BowlingLayout).
        private const float EndLen = 1.2f, SegLen = 4f, ArchAt = 23.1f, FlameUp = 1.1f, ApproachAt = 1.25f, LaneUnder = 0.3f;
        private const float CupUp = 0.80f, CradleUp = 0.03f, CradleRing = 0.085f, CradleBall = 0.035f;
        private static readonly float[] TrestlesAt = { 5.6f, 11.6f, 17.6f };
        private static readonly string[] Rings = { "Rune_Ring_Green", "Rune_Ring_Red", "Rune_Ring_Blue", "Rune_Ring_Yellow" };

        [MenuItem(Root + "Build SlimeBowl Kit")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) { Debug.LogError($"{Log} Stop play mode first; nothing changed."); return; }
            if (BowlingHandSetUpMenu.Refuse("Build SlimeBowl Kit")) return;
            var notes = new List<string>();
            if (!Imports(notes)) return;
            var mat = Material(notes);
            var pieces = Pieces(mat, notes);
            if (pieces == null) return;
            var boss = BossVariant(pieces, notes);
            if (!Dress(pieces, boss, notes)) return;
            AssetDatabase.SaveAssets();
            Debug.Log($"{Log} Build SlimeBowl Kit:\n" + string.Join("\n", notes));
        }

        [MenuItem(Root + "Remove SlimeBowl Kit (placeholder look)")]
        public static void Remove()
        {
            if (EditorApplication.isPlaying) { Debug.LogError($"{Log} Stop play mode first; nothing changed."); return; }
            if (BowlingHandSetUpMenu.Refuse("Remove SlimeBowl Kit")) return;
            var root = PrefabUtility.LoadPrefabContents(ArenaPath);
            int n;
            try
            {
                n = Clear(root.transform);
                Placeholders(root.transform, true, null, null);
                PrefabUtility.SaveAsPrefabAsset(root, ArenaPath, out bool saved);
                if (!saved) Debug.LogError($"{Log} Could not save {ArenaPath}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            Debug.Log($"{Log} Remove SlimeBowl Kit: {n} kit parts removed, the placeholder look is back (Gate Run's giant pumpkin unchanged).");
        }

        [MenuItem(Root + "Build SlimeBowl Kit", true)]
        [MenuItem(Root + "Remove SlimeBowl Kit (placeholder look)", true)]
        private static bool CanRun() => !EditorApplication.isPlaying;

        // ---------------------------------------------------------------- imports, material, pieces

        internal static bool Imports(List<string> notes)
        {
            if (AssetImporter.GetAtPath(FbxPath) is not ModelImporter model)
            {
                Debug.LogError($"{Log} No {FbxPath}: export the kit first (blender -b Exports/Gates/gate2/SlimeBowl_Kit.blend " +
                               "--python Exports/Gates/gate2/tools/export_slimebowl_kit.py); nothing changed.");
                return false;
            }
            bool change = model.materialImportMode != ModelImporterMaterialImportMode.None || model.importAnimation ||
                          model.importCameras || model.importLights || model.importNormals != ModelImporterNormals.Import ||
                          model.isReadable || model.addCollider || model.importBlendShapes ||
                          model.animationType != ModelImporterAnimationType.None;
            if (change)
            {
                model.materialImportMode = ModelImporterMaterialImportMode.None;
                model.importAnimation = false;
                model.importCameras = false;
                model.importLights = false;
                model.importNormals = ModelImporterNormals.Import;
                model.isReadable = false;
                model.addCollider = false;
                model.importBlendShapes = false;
                model.animationType = ModelImporterAnimationType.None;
                model.SaveAndReimport();
                notes.Add("FBX import settings set (no materials, normals imported)");
            }
            foreach (var (path, alpha) in new[] { (ColourPath, true), (GlowPath, false) })
            {
                if (AssetImporter.GetAtPath(path) is not TextureImporter tex)
                {
                    Debug.LogError($"{Log} No {path}: export the kit first; nothing changed.");
                    return false;
                }
                var want = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
                if (tex.mipmapEnabled || tex.alphaIsTransparency || tex.wrapMode != TextureWrapMode.Clamp || !tex.sRGBTexture ||
                    tex.alphaSource != want || tex.textureCompression != TextureImporterCompression.CompressedHQ)
                {
                    tex.mipmapEnabled = false;
                    tex.alphaIsTransparency = false;
                    tex.wrapMode = TextureWrapMode.Clamp;
                    tex.filterMode = FilterMode.Bilinear;
                    tex.sRGBTexture = true;
                    tex.alphaSource = want;
                    tex.textureCompression = TextureImporterCompression.CompressedHQ;
                    tex.SaveAndReimport();
                    notes.Add(System.IO.Path.GetFileName(path) + " import settings set (no mip maps, clamp)");
                }
            }
            return true;
        }

        internal static Material Material(List<string> notes)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "SlimeBowl_Palette" };
                AssetDatabase.CreateAsset(mat, MatPath);
                notes.Add("material made: " + MatPath);
            }
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ColourPath));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", 1f);
            mat.SetFloat("_SmoothnessTextureChannel", 1f);
            mat.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            mat.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(GlowPath));
            mat.SetColor("_EmissionColor", Color.white * 1.4f);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // One prefab per mesh of the FBX (looks only), keyed by the piece's short name (Lane_4m...).
        private static Dictionary<string, GameObject> Pieces(Material mat, List<string> notes)
        {
            var meshes = AssetDatabase.LoadAllAssetsAtPath(FbxPath).OfType<Mesh>().Where(m => m.name.StartsWith(Prefix)).ToList();
            if (meshes.Count == 0)
            {
                Debug.LogError($"{Log} {FbxPath} has no {Prefix}* meshes; nothing changed.");
                return null;
            }
            if (!AssetDatabase.IsValidFolder(PieceFolder)) AssetDatabase.CreateFolder("Assets/Prefabs/Bowling", "Kit");
            var made = new Dictionary<string, GameObject>();
            foreach (var mesh in meshes)
            {
                var go = new GameObject(mesh.name);
                try
                {
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var r = go.AddComponent<MeshRenderer>();
                    r.sharedMaterial = mat;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    r.receiveShadows = false;
                    if (mesh.name.EndsWith("_Flame")) go.AddComponent<KitFlame>();
                    made[mesh.name.Substring(Prefix.Length)] = PrefabUtility.SaveAsPrefabAsset(go, PieceFolder + "/" + mesh.name + ".prefab");
                }
                finally
                {
                    Object.DestroyImmediate(go);
                }
            }
            notes.Add($"{made.Count} piece prefabs in {PieceFolder}, {meshes.Sum(m => m.triangles.Length / 3)} triangles in all");
            return made;
        }

        // Gate 2's giant pumpkin: a variant of the shared one whose model is the kit's Boss_Pumpkin (radius 1 m there).
        private static GameObject BossVariant(Dictionary<string, GameObject> pieces, List<string> notes)
        {
            if (!pieces.TryGetValue("Boss_Pumpkin", out var model)) return null;
            var baseBoss = AssetDatabase.LoadAssetAtPath<GameObject>(BossBasePath);
            if (baseBoss == null)
            {
                notes.Add("NO " + BossBasePath + ": no gate 2 giant pumpkin");
                return null;
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Gate2RunMenu.BossVariantPath) == null)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(baseBoss);
                try
                {
                    PrefabUtility.SaveAsPrefabAsset(inst, Gate2RunMenu.BossVariantPath);
                }
                finally
                {
                    Object.DestroyImmediate(inst);
                }
                notes.Add("giant pumpkin variant made: " + Gate2RunMenu.BossVariantPath);
            }
            var root = PrefabUtility.LoadPrefabContents(Gate2RunMenu.BossVariantPath);
            try
            {
                var boss = root.GetComponent<PumpkinBallBoss>();
                if (boss != null)
                {
                    boss.model = model;
                    boss.modelRadius = 1f;
                    boss.modelOffset = Vector3.zero;
                    boss.modelEuler = Vector3.zero;
                    EditorUtility.SetDirty(boss);
                }
                PrefabUtility.SaveAsPrefabAsset(root, Gate2RunMenu.BossVariantPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            notes.Add("gate 2's giant pumpkin = the bowling-ball pumpkin (kit Boss_Pumpkin)");
            return AssetDatabase.LoadAssetAtPath<GameObject>(Gate2RunMenu.BossVariantPath);
        }

        // ---------------------------------------------------------------- the arena

        private static bool Dress(Dictionary<string, GameObject> pieces, GameObject boss, List<string> notes)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ArenaPath) == null)
            {
                Debug.LogError($"{Log} No {ArenaPath} (run Set Up Bowling Arena first).");
                return false;
            }
            float ballRadius = BallRadius();
            var root = PrefabUtility.LoadPrefabContents(ArenaPath);
            try
            {
                var x = root.transform.Find("X");
                if (x == null)
                {
                    Debug.LogError($"{Log} {ArenaPath} has no X child (run Set Up Bowling Arena).");
                    return false;
                }
                int cleared = Clear(root.transform);
                var kit = Child(x, Kit);
                int placed = 0;
                GameObject Put(string piece, Transform parent, float side, float up, float along, float yaw = 0f, float scale = 1f)
                {
                    if (!pieces.TryGetValue(piece, out var prefab)) return null;
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                    // The kit's frame (Blender: +Y along, +X right, Z up) comes in with +Y as -Z and +X as -X: half a turn.
                    go.transform.localPosition = new Vector3(side, up, along);
                    go.transform.localRotation = Quaternion.Euler(0f, 180f + yaw, 0f);
                    go.transform.localScale = Vector3.one * scale;
                    placed++;
                    return go;
                }

                // The island and what's on and under it (the X frame: lane 0 along +Z).
                Put("Island", kit, 0f, 0f, 0f);
                foreach (var (sx, sz) in new[] { (2.02f, 2.02f), (-2.02f, -2.02f) })
                {
                    float yaw = Mathf.Atan2(sz, -sx) * Mathf.Rad2Deg;   // the arm towards the middle
                    var post = Put("Lantern_Post", kit, sx, BowlingLayout.DirtTop, sz, yaw);
                    if (post != null)
                    {
                        var lantern = (GameObject)PrefabUtility.InstantiatePrefab(pieces["Lantern"], post.transform);
                        lantern.transform.localPosition = new Vector3(-0.44f, 1.84f, 0f);
                        placed++;
                    }
                }
                foreach (var (sx, sz, yaw) in new[] { (1.6f, -1.7f, 20f), (-1.75f, 1.5f, 75f), (-1.9f, -1.2f, 140f) })
                    Put("Pumpkin_Small", kit, sx, BowlingLayout.DirtTop, sz, yaw);
                foreach (var (sx, sy, sz) in new[] { (1.1f, -1.2f, -0.9f), (-1.2f, -1.4f, 1.1f), (0.3f, -1.6f, 1.3f) })
                    Put("Chain_Rock", kit, sx, sy, sz);
                var rng = new System.Random(7);
                float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);
                var rocks = new[] { (10f, 3f, 1f), (13f, 1f, 1.3f), (9f, -1f, 0.9f), (14f, 4.5f, 1.1f) };
                for (int q = 0; q < rocks.Length; q++)
                {
                    var (r, up, s) = rocks[q];
                    float a = (45f + 90f * q + Rand(-12f, 12f)) * Mathf.Deg2Rad;
                    var rock = Put("Floating_Rock", kit, r * Mathf.Cos(a), up, r * Mathf.Sin(a), Rand(0f, 360f), s);
                    if (rock != null && q % 2 == 0)
                    {
                        var lantern = (GameObject)PrefabUtility.InstantiatePrefab(pieces["Lantern"], rock.transform);
                        lantern.transform.localPosition = new Vector3(0.15f, -0.5f, 0f);
                        placed++;
                    }
                }
                for (int i = 0; i < 8; i++)
                {
                    float a = Rand(0f, Mathf.PI * 2f), r = Rand(9f, 30f);
                    Put("Cloud_Puff", kit, r * Mathf.Cos(a), Rand(-24f, -15f), r * Mathf.Sin(a), Rand(0f, 360f), Rand(2f, 3.4f));
                }

                // Each lane: its kit (always on) and its lit kit (on with the lane's Lit part).
                for (int k = 0; k < 4; k++)
                {
                    var lane = x.Find("Lane " + k);
                    if (lane == null) { notes.Add("NO Lane " + k); continue; }
                    var lk = Child(lane, Kit);
                    Put("Lane_End", lk, 0f, 0f, BowlingLayout.LaneStart);
                    for (int i = 0; i < 4; i++) Put("Lane_4m", lk, 0f, 0f, BowlingLayout.LaneStart + EndLen + SegLen * i);
                    Put("Lane_Arrows", lk, 0f, 0f, 7f);
                    for (int i = 0; i < 5; i++)
                        foreach (float side in new[] { -BowlingLayout.RailSide, BowlingLayout.RailSide })
                            Put("Rail_4m", lk, side, 0f, BowlingLayout.FoulLine + SegLen * i);
                    foreach (float along in TrestlesAt) Put("Trestle", lk, 0f, -LaneUnder, along);
                    Put("Deck", lk, 0f, 0f, BowlingLayout.DeckAt);
                    foreach (float side in new[] { -BowlingLayout.FireSide, BowlingLayout.FireSide })
                        Put("Brazier", lk, side, 0f, BowlingLayout.DeckAt, side < 0f ? 0f : 180f);
                    Put("Pumpkin_Arch", lk, 0f, 0f, ArchAt);
                    float chain = k % 2 == 0 ? 1f : -1f;
                    Put("Chain_Rock", lk, chain, -0.28f, 9.6f);
                    Put("Chain_Rock", lk, -chain, -0.28f, 13.6f);
                    Put("Pumpkin_Small", lk, chain, 0f, BowlingLayout.DeckAt + 1.25f, 30f * k);
                    Put("Approach_Stone", lk, 0f, BowlingLayout.DirtTop, ApproachAt);

                    var lit = lane.Find("Lit");
                    var flames = new List<Object>();
                    if (lit != null)
                    {
                        var lkit = Child(lit, LitKit);
                        Put(Rings[k], lkit, 0f, 0f, BowlingLayout.DeckAt);
                        foreach (float side in new[] { -BowlingLayout.FireSide, BowlingLayout.FireSide })
                        {
                            var flame = Put("Flame", lkit, side, FlameUp, BowlingLayout.DeckAt, 47f * k + (side < 0f ? 0f : 90f));
                            if (flame != null) flames.Add(flame.transform);
                        }
                    }
                    Placeholders(lane, false, flames.ToArray(), notes);
                }

                // The ball vines: one per sprout spot, popping up as the spot opens; its cup's cradle holds the ball.
                var sprouts = x.Find("Ball Sprouts");
                var supply = sprouts != null ? sprouts.GetComponent<BowlingBallSupply>() : null;
                if (sprouts != null && supply != null && pieces.ContainsKey("Vine"))
                {
                    float cradle = CupUp + CradleUp;
                    float centre = cradle + Mathf.Sqrt(Mathf.Max(0f, (ballRadius + CradleBall) * (ballRadius + CradleBall) - CradleRing * CradleRing));
                    for (int i = 0; i < BowlingLayout.SproutYaws.Length; i++)
                    {
                        var spot = sprouts.Find("Spot " + i);
                        if (spot == null) continue;
                        var at = BowlingLayout.SproutPoint(i);
                        var holder = Child(kit, "Kit Sprout " + i);
                        holder.localPosition = new Vector3(at.x, BowlingLayout.DirtTop, at.z);
                        holder.localRotation = Quaternion.Euler(0f, BowlingLayout.SproutYaws[i] + 180f, 0f);
                        var look = (GameObject)PrefabUtility.InstantiatePrefab(pieces["Vine"], holder);
                        look.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                        placed++;
                        var cradleGo = new GameObject("Cradle");
                        cradleGo.transform.SetParent(holder, false);
                        cradleGo.transform.localPosition = new Vector3(0f, cradle, 0f);
                        for (int c = 0; c < 3; c++)
                        {
                            var sphere = cradleGo.AddComponent<SphereCollider>();
                            float a = c * Mathf.PI * 2f / 3f;
                            sphere.center = new Vector3(Mathf.Cos(a) * CradleRing, 0f, Mathf.Sin(a) * CradleRing);
                            sphere.radius = CradleBall;
                        }
                        holder.gameObject.AddComponent<KitSprout>().Setup(supply, i, new[] { look, cradleGo });
                        // The ball grows and waits in the cup (its bottom here), not on the floor.
                        spot.localPosition = new Vector3(spot.localPosition.x, BowlingLayout.DirtTop + centre - ballRadius, spot.localPosition.z);
                    }
                    notes.Add($"8 ball vines: balls wait at {BowlingLayout.DirtTop + centre:0.00} m (their middle), in the cups");
                }
                var vines = x.Find("Vines");
                if (vines != null)
                    foreach (var r in vines.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                var dirt = x.Find("Dirt");
                if (dirt != null && dirt.TryGetComponent(out MeshRenderer dirtLook)) dirtLook.enabled = false;

                // Gate 2's own giant pumpkin.
                var run = root.GetComponent<GateRun>();
                if (run != null && boss != null)
                {
                    run.finaleBoss = boss;
                    EditorUtility.SetDirty(run);
                }

                int particles = 0;
                foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
                    if (ps.gameObject.activeInHierarchy || ps.transform.IsChildOf(root.transform)) particles += Active(ps, root.transform) ? ps.main.maxParticles : 0;
                PrefabUtility.SaveAsPrefabAsset(root, ArenaPath, out bool saved);
                if (!saved) { Debug.LogError($"{Log} Could not save {ArenaPath}."); return false; }
                notes.Add($"arena dressed: {placed} kit parts ({cleared} old ones removed); placeholder looks hidden, colliders kept");
                notes.Add($"particle max sum left: {particles} (was about 1,450 with the Danger_Flame columns)");
                notes.Add("giant pumpkin: " + (run != null && run.finaleBoss != null ? run.finaleBoss.name : "NONE"));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            return true;
        }

        // Whether the particle system would play when its lane lights (not under an object switched off by the kit).
        private static bool Active(ParticleSystem ps, Transform root)
        {
            for (var t = ps.transform; t != null && t != root; t = t.parent)
                if (!t.gameObject.activeSelf && t.name != "Lit") return false;
            return true;
        }

        // The lane's placeholder looks off (or on again), colliders untouched; Bowling Lane's fire points = the kit flames
        // (or the fire columns again).
        private static void Placeholders(Transform laneOrRoot, bool on, Object[] flames, List<string> notes)
        {
            var lanes = laneOrRoot.GetComponent<BowlingLane>() != null ? new[] { laneOrRoot } :
                laneOrRoot.GetComponentsInChildren<BowlingLane>(true).Select(l => l.transform).ToArray();
            foreach (var lane in lanes)
            {
                foreach (var part in new[] { "Floor", "Rail L", "Rail R" })
                {
                    var t = lane.Find(part);
                    if (t != null && t.TryGetComponent(out MeshRenderer r)) r.enabled = on;
                }
                var fires = new List<Object>();
                foreach (var part in new[] { "Lit/Deck Ring", "Lit/Fire Column L", "Lit/Fire Column R" })
                {
                    var t = lane.Find(part);
                    if (t == null) continue;
                    t.gameObject.SetActive(on);
                    if (part.StartsWith("Lit/Fire")) fires.Add(t);
                }
                var so = new SerializedObject(lane.GetComponent<BowlingLane>());
                var p = so.FindProperty("fires");
                var use = on ? fires.ToArray() : flames ?? System.Array.Empty<Object>();
                if (p != null)
                {
                    p.arraySize = use.Length;
                    for (int i = 0; i < use.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = use[i];
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            if (!on) return;
            // The whole arena: the dirt mesh and the code vines back, the ball spots back on the floor.
            var x = laneOrRoot.Find("X");
            if (x == null) return;
            var dirt = x.Find("Dirt");
            if (dirt != null && dirt.TryGetComponent(out MeshRenderer dirtLook)) dirtLook.enabled = true;
            var vines = x.Find("Vines");
            if (vines != null)
                foreach (var r in vines.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
            var sprouts = x.Find("Ball Sprouts");
            if (sprouts != null)
                for (int i = 0; i < BowlingLayout.SproutYaws.Length; i++)
                {
                    var spot = sprouts.Find("Spot " + i);
                    if (spot != null) spot.localPosition = new Vector3(spot.localPosition.x, 0f, spot.localPosition.z);
                }
        }

        // Every kit part the menu made (by name), anywhere in the arena.
        private static int Clear(Transform root)
        {
            var gone = root.GetComponentsInChildren<Transform>(true)
                .Where(t => t != root && (t.name == Kit || t.name == LitKit))
                .ToList();
            foreach (var t in gone) if (t != null) Object.DestroyImmediate(t.gameObject);
            return gone.Count;
        }

        private static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        // The bowling ball's radius (its sphere collider, scaled), for where it sits in a cup.
        private static float BallRadius()
        {
            var ball = AssetDatabase.LoadAssetAtPath<GameObject>(BallPath);
            if (ball != null && ball.TryGetComponent(out SphereCollider sphere))
            {
                var s = ball.transform.lossyScale;
                return sphere.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
            }
            return 0.16f;
        }
    }
}

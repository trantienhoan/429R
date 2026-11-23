using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Balls.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Ball Valley > Build Ball Valley Kit (gate 3, 2026-11-23: "for each gate create a kit for easy
    /// change and fix"): the kit made in Exports/Gates/gate3/BallValley_Kit.blend, exported by
    /// Exports/Gates/gate3/tools/export_ballvalley_kit.py to Assets/Models/Gates/BallValley/ (and its palette to
    /// Assets/M_Textures/Gates/BallValley/), into Unity:
    /// - the palette's import settings and one material, BallValley_Kit_Mat (URP Lit: the palette's colours, the smoothness
    ///   in its alpha, the glowing swatches from the emission picture);
    /// - one prefab per piece in Assets/Prefabs/BallValley/Kit/ (looks only: the valley makes its own colliders);
    /// - gate 3's own giant pumpkin, Assets/Prefabs/BallValley/Pumpkin_Ball_Boss_BallValley.prefab: a variant of the shared
    ///   Prefabs/Gates/Pumpkin_Ball_Boss wearing the kit's jack-o'-lantern (its face turned to the way it rolls).
    /// Re-runnable: pieces are refreshed from the FBX files (export again after changing the .blend, then run this).
    /// Never touches the scene.
    /// </summary>
    public static class BallValleyKitMenu
    {
        private const string Root = "Tools/429 Game/Ball Valley/";
        private const string Log = "[BallValley Kit]";
        public const string Models = "Assets/Models/Gates/BallValley";
        public const string Textures = "Assets/M_Textures/Gates/BallValley";
        public const string PaletteColour = Textures + "/BallValley_Palette.png";
        public const string PaletteGlow = Textures + "/BallValley_Palette_Emission.png";
        public const string MaterialPath = Textures + "/BallValley_Kit_Mat.mat";
        public const string PieceFolder = "Assets/Prefabs/BallValley/Kit";
        public const string BossVariantPath = "Assets/Prefabs/BallValley/Pumpkin_Ball_Boss_BallValley.prefab";
        private const string Prefix = "BallValley_Kit_";
        private const string GiantPiece = Prefix + "Giant_Jack_O_Lantern";
        private const float GiantRadius = 2.6f;          // as modelled (the game's radius: the boss grows it to its own)
        private const int Swatches = 32, GlowFaceSwatch = 21;   // the palette's columns; the pumpkin's glowing face

        [MenuItem(Root + "Build Ball Valley Kit")]
        public static void Build() => Run();

        [MenuItem(Root + "Build Ball Valley Kit", true)]
        private static bool CanBuild() => !EditorApplication.isPlaying;

        /// <summary>Builds (or refreshes) the material, the piece prefabs and the boss variant. False if it couldn't.</summary>
        public static bool Run()
        {
            if (EditorApplication.isPlaying) { Debug.LogError($"{Log} Stop play mode first; nothing changed."); return false; }
            var fbx = AssetDatabase.FindAssets("t:Model", new[] { Models })
                .Select(AssetDatabase.GUIDToAssetPath).Where(p => Path.GetFileNameWithoutExtension(p).StartsWith(Prefix)).ToList();
            if (fbx.Count == 0 || AssetDatabase.LoadAssetAtPath<Texture2D>(PaletteColour) == null)
            {
                Debug.LogError($"{Log} No kit in {Models} (or no palette in {Textures}): export it first with " +
                               "Exports/Gates/gate3/tools/export_ballvalley_kit.py. Nothing changed.");
                return false;
            }
            var notes = new List<string>();
            SetUpTexture(PaletteColour, true, notes);
            SetUpTexture(PaletteGlow, false, notes);
            var mat = KitMaterial(notes);
            foreach (var path in fbx) SetUpModel(path, notes);

            Folder("Assets/Prefabs", "BallValley");
            Folder("Assets/Prefabs/BallValley", "Kit");
            int pieces = 0;
            GameObject giant = null;
            foreach (var path in fbx)
            {
                var piece = PiecePrefab(path, mat);
                if (piece == null) continue;
                pieces++;
                if (piece.name == GiantPiece) giant = piece;
            }
            notes.Add($"{pieces} piece prefabs in {PieceFolder}");
            if (giant != null) BossVariant(giant, notes);
            else notes.Add("NO " + GiantPiece + ": no boss variant");
            SetUpTexture(NightPalette, true, notes, point: true);
            DressValley(mat, true, notes);
            AssetDatabase.SaveAssets();
            Debug.Log($"{Log} Build Ball Valley Kit: {string.Join(", ", notes)}.", AssetDatabase.LoadAssetAtPath<Material>(MaterialPath));
            return true;
        }

        // The palette: point sampling across its 32-px swatch columns isn't needed (bilinear stays inside a column: the UVs
        // keep 4 px from its sides); no compression artefacts across the columns' gradients; the colour one is sRGB with the
        // smoothness in its alpha, the glow one plain. (BALLS' night palette: point sampled like BALLS' own.)
        private static void SetUpTexture(string path, bool colour, List<string> notes, bool point = false)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) return;
            var filter = point ? FilterMode.Point : FilterMode.Bilinear;
            bool wrong = ti.textureType != TextureImporterType.Default || !ti.sRGBTexture || ti.wrapMode != TextureWrapMode.Clamp
                         || ti.textureCompression != TextureImporterCompression.CompressedHQ || ti.alphaIsTransparency
                         || ti.filterMode != filter || ti.mipmapEnabled == point
                         || ti.alphaSource != (colour ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None);
            if (!wrong) return;
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true;
            ti.alphaSource = colour ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            ti.alphaIsTransparency = false;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = filter;
            ti.mipmapEnabled = !point;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.SaveAndReimport();
            notes.Add(Path.GetFileName(path) + " import settings set");
        }

        public const string NightPalette = "Assets/BallValley/Kit/BALLS_Palette_Night.png";
        private const string ValleyPrefab = "Assets/Prefabs/BallValley/Ball_Valley.prefab";

        [MenuItem(Root + "Ball Valley: BALLS Look (No Kit)")]
        public static void NoKit()
        {
            var notes = new List<string>();
            DressValley(null, false, notes);
            AssetDatabase.SaveAssets();
            Debug.Log($"{Log} Ball Valley: BALLS look (no kit): {string.Join(", ", notes)}. Build Ball Valley Kit puts the kit back.");
        }

        [MenuItem(Root + "Ball Valley: BALLS Look (No Kit)", true)]
        private static bool CanNoKit() => !EditorApplication.isPlaying;

        // The Ball_Valley prefab's kit fields: on (the material, the rim, the holes, trees, rocks, the dressing round it and
        // the night palette for BALLS' leftovers) or off (BALLS' models). A backup of the prefab first, each time.
        private static void DressValley(Material mat, bool on, List<string> notes)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ValleyPrefab) == null) { notes.Add("NO " + ValleyPrefab); return; }
            Backup(ValleyPrefab);
            var root = PrefabUtility.LoadPrefabContents(ValleyPrefab);
            try
            {
                var stage = root.GetComponent<BallValleyStage>();
                if (stage == null) { notes.Add("no Ball Valley Stage on " + ValleyPrefab); return; }
                if (!on)
                {
                    stage.kitMaterial = null;
                    notes.Add("Ball_Valley wears BALLS' models");
                }
                else
                {
                    GameObject P(string piece) => AssetDatabase.LoadAssetAtPath<GameObject>(PieceFolder + "/" + Prefix + piece + ".prefab");
                    stage.kitMaterial = mat;
                    stage.kitRim = P("Rim_Straight_4m");
                    stage.kitHoleCorner = P("Hole_Corner");
                    stage.kitHoleSide = P("Hole_Side");
                    stage.kitTrees = new[] { P("Tree_Jelly") }.Where(g => g != null).ToArray();
                    stage.kitRocks = new[] { P("Boulder_Mossy"), P("Boulder_Mossy"), P("Gravestone") }.Where(g => g != null).ToArray();
                    stage.nightPalette = AssetDatabase.LoadAssetAtPath<Texture2D>(NightPalette);
                    stage.kitDecor = Decor(P);
                    notes.Add($"Ball_Valley wears the kit ({stage.kitDecor.Length} pieces round it" +
                              (stage.kitRim && stage.kitHoleCorner && stage.kitHoleSide ? "" : "; SOME PIECES MISSING") + ")");
                    // Its giant pumpkin: gate 3's own (the kit's jack-o'-lantern).
                    var run = root.GetComponent<Game.Gates.GateRun>();
                    var own = AssetDatabase.LoadAssetAtPath<GameObject>(BossVariantPath);
                    if (run != null && own != null)
                    {
                        run.finaleBoss = own;
                        EditorUtility.SetDirty(run);
                    }
                }
                EditorUtility.SetDirty(stage);
                PrefabUtility.SaveAsPrefabAsset(root, ValleyPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // The dressing round the full-size valley (the lawn is 50 x 25 m; the ground round it reaches 35 x 22.5 m from the
        // middle): lantern posts past the corners, twisted trees, the ruined gazebo, jack-o'-lanterns on the ground.
        private static BallValleyStage.KitPlace[] Decor(System.Func<string, GameObject> piece)
        {
            var list = new List<BallValleyStage.KitPlace>();
            void Add(string name, float x, float z, float turn, float size = 1f)
            {
                var p = piece(name);
                if (p != null) list.Add(new BallValleyStage.KitPlace { piece = p, at = new Vector2(x, z), turn = turn, size = size });
            }
            foreach (var (x, z) in new[] { (31f, 18f), (-31f, 18f), (31f, -18f), (-31f, -18f), (12f, 17.5f), (-12f, -17.5f) })
                Add("Lantern_Post", x, z, Mathf.Atan2(-x, -z) * Mathf.Rad2Deg);
            Add("Tree_Twisted", -33f, 3f, 40f);
            Add("Tree_Twisted", 33.5f, -6f, 200f);
            Add("Tree_Twisted", -19f, 20.5f, 120f);
            Add("Tree_Twisted", 24f, 20f, 300f);
            Add("Gazebo_Ruined", 22f, -19f, 15f);
            foreach (var (x, z, t) in new[] { (29f, 6f, 250f), (-29.5f, -7f, 80f), (8f, 19.5f, 170f), (-8f, 20f, 190f),
                                              (17f, -20f, 340f), (-18f, -20.5f, 10f), (0f, -20.5f, 0f), (33f, 12f, 230f),
                                              (-33f, -13f, 60f), (-3f, 21f, 175f) })
                Add("Pumpkin_Lantern", x, z, t, 1.4f);
            return list.ToArray();
        }

        private static void Backup(string assetPath)
        {
            var dir = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
                "429R_claude_backups", System.DateTime.Now.ToString("yyyy-MM-dd"), "gate3", "prefab_backups");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, Path.GetFileNameWithoutExtension(assetPath) + "_" + System.DateTime.Now.ToString("HHmmss") + ".prefab");
            File.Copy(assetPath, file, true);
        }

        private static Material KitMaterial(List<string> notes)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null)
            {
                var lit = Shader.Find("Universal Render Pipeline/Lit");
                if (lit == null) { Debug.LogError($"{Log} No URP Lit shader."); return null; }
                mat = new Material(lit) { name = "BallValley_Kit_Mat" };
                AssetDatabase.CreateAsset(mat, MaterialPath);
                notes.Add("material made");
            }
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(PaletteColour));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", 1f);                 // times the palette's alpha
            mat.SetFloat("_SmoothnessTextureChannel", 1f);   // Albedo Alpha
            mat.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            mat.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(PaletteGlow));
            mat.SetColor("_EmissionColor", Color.white * 1.4f);
            mat.EnableKeyword("_EMISSION");
            // Keep RealtimeEmissive (None made URP switch the glow off after a save: the pumpkin tree, 2026-10-20).
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // Read/Write on (the valley builds colliders from some meshes and reads the holes' shapes), no imported materials.
        private static void SetUpModel(string path, List<string> notes)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter mi)) return;
            bool wrong = !mi.isReadable || mi.materialImportMode != ModelImporterMaterialImportMode.None || mi.importAnimation
                         || mi.importCameras || mi.importLights || !Mathf.Approximately(mi.globalScale, 1f);
            if (!wrong) return;
            mi.isReadable = true;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importAnimation = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.globalScale = 1f;
            mi.SaveAndReimport();
        }

        // A prefab of the piece: its mesh with the kit material, looks only.
        private static GameObject PiecePrefab(string fbxPath, Material mat)
        {
            var name = Path.GetFileNameWithoutExtension(fbxPath);
            var mesh = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<Mesh>().FirstOrDefault();
            if (mesh == null) return null;
            var path = PieceFolder + "/" + name + ".prefab";
            var go = new GameObject(name);
            try
            {
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                return PrefabUtility.SaveAsPrefabAsset(go, path);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // Gate 3's giant pumpkin: the shared one wearing the kit's jack-o'-lantern, its face towards its front (+Z).
        private static void BossVariant(GameObject giant, List<string> notes)
        {
            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Game.Gates.EditorTools.PumpkinBallBossMenu.PrefabPath);
            if (basePrefab == null)
            {
                notes.Add("NO " + Game.Gates.EditorTools.PumpkinBallBossMenu.PrefabPath + ": no boss variant");
                return;
            }
            GameObject root;
            bool fresh = AssetDatabase.LoadAssetAtPath<GameObject>(BossVariantPath) == null;
            var build = fresh ? UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene() : default;
            if (fresh) root = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab, build);   // (never in the open scene)
            else root = PrefabUtility.LoadPrefabContents(BossVariantPath);
            try
            {
                var boss = root.GetComponent<Game.Gates.PumpkinBallBoss>();
                boss.model = giant;
                boss.modelRadius = GiantRadius;
                boss.modelOffset = Vector3.zero;
                boss.modelEuler = new Vector3(0f, FaceYaw(giant), 0f);
                EditorUtility.SetDirty(boss);
                root.name = Path.GetFileNameWithoutExtension(BossVariantPath);
                PrefabUtility.SaveAsPrefabAsset(root, BossVariantPath);
                notes.Add((fresh ? "boss variant made" : "boss variant refreshed") + $" (face turned {boss.modelEuler.y:0} degrees)");
            }
            finally
            {
                if (fresh) UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(build);
                else PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // The turn that brings the pumpkin's glowing face (its vertices on the face swatch) to +Z, whatever way the FBX
        // import turned it.
        private static float FaceYaw(GameObject piece)
        {
            var filter = piece.GetComponent<MeshFilter>();
            var mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null || !mesh.isReadable) return 0f;
            var verts = mesh.vertices;
            var uvs = mesh.uv;
            float u0 = GlowFaceSwatch / (float)Swatches, u1 = (GlowFaceSwatch + 1) / (float)Swatches;
            var sum = Vector3.zero;
            int n = 0;
            for (int i = 0; i < verts.Length && i < uvs.Length; i++)
            {
                if (uvs[i].x < u0 || uvs[i].x > u1) continue;
                sum += verts[i];
                n++;
            }
            if (n == 0) return 0f;
            var face = sum / n;
            face.y = 0f;
            if (face.sqrMagnitude < 0.0001f) return 0f;
            return Mathf.Round(-Vector3.SignedAngle(Vector3.forward, face.normalized, Vector3.up));
        }

        private static void Folder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}

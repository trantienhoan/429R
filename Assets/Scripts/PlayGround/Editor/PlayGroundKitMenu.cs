using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.PlayGround.Editor
{
    /// <summary>
    /// Tools > 429 Game > PlayGround > Build PlayGround Kit (gate 1, 2026-11-23: "for each gate create a kit for easy
    /// change and fix"): the kit made in Exports/Gates/gate1/PlayGround_Kit.blend, exported by
    /// Exports/Gates/gate1/tools/export_playground_kit.py to Assets/Models/Gates/PlayGround/ (and its palette to
    /// Assets/M_Textures/Gates/PlayGround/), into Unity (the same way as gate 3's Build Ball Valley Kit):
    /// - the palette's import settings and one material, PlayGround_Kit_Mat (URP Lit: the palette's colours, the
    ///   smoothness in its alpha, the glowing swatches from the emission picture);
    /// - one prefab per piece in Assets/Prefabs/PlayGround/Kit/ (looks only, for now: the stage dressing adds what each
    ///   piece does); a breakable's broken set is one prefab with its chunks as children;
    /// - gate 1's own giant pumpkin, Assets/Prefabs/PlayGround/Pumpkin_Ball_Boss_Stew.prefab: a variant of the shared
    ///   Prefabs/Gates/Pumpkin_Ball_Boss wearing the kit's stew pumpkin (its face turned to its front), and the
    ///   cauldron's Finale Boss pointed at it (only when it still names the shared one or nothing).
    /// Re-runnable: pieces are refreshed from the FBX files (export again after changing the .blend, then run this).
    /// Never touches the scene.
    /// </summary>
    public static class PlayGroundKitMenu
    {
        private const string Root = "Tools/429 Game/PlayGround/";
        private const string Log = "[PlayGround Kit]";
        public const string Models = "Assets/Models/Gates/PlayGround";
        public const string Textures = "Assets/M_Textures/Gates/PlayGround";
        public const string PaletteColour = Textures + "/PlayGround_Palette.png";
        public const string PaletteGlow = Textures + "/PlayGround_Palette_Emission.png";
        public const string MaterialPath = Textures + "/PlayGround_Kit_Mat.mat";
        public const string PieceFolder = "Assets/Prefabs/PlayGround/Kit";
        public const string BossVariantPath = "Assets/Prefabs/PlayGround/Pumpkin_Ball_Boss_Stew.prefab";
        private const string CauldronPath = "Assets/Prefabs/PlayGround/Fire_Woods_n_Cauldron.prefab";
        private const string Prefix = "PlayGround_Kit_";
        private const string GiantPiece = Prefix + "Giant_Stew_Pumpkin";
        private const float GiantRadius = 1.5f;              // as modelled (gate 1's Finale Radius)
        private const float GateOneChargeSpeed = 10f;        // m/s, for the PlayGround's small field
        private const int Swatches = 32, FaceSwatch = 31;    // the palette's columns; the stew pumpkin's eyes (eye_glow)

        [MenuItem(Root + "Build PlayGround Kit")]
        public static void Build() => Run();

        [MenuItem(Root + "Build PlayGround Kit", true)]
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
                               "Exports/Gates/gate1/tools/export_playground_kit.py. Nothing changed.");
                return false;
            }
            var notes = new List<string>();
            SetUpTexture(PaletteColour, true, notes);
            SetUpTexture(PaletteGlow, false, notes);
            var mat = KitMaterial(notes);
            if (mat == null) return false;
            foreach (var path in fbx) SetUpModel(path, notes);

            if (!AssetDatabase.IsValidFolder(PieceFolder)) AssetDatabase.CreateFolder("Assets/Prefabs/PlayGround", "Kit");
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
            if (giant != null)
            {
                var variant = BossVariant(giant, notes);
                if (variant != null) PointCauldron(variant, notes);
            }
            else notes.Add("NO " + GiantPiece + ": no boss variant");
            AssetDatabase.SaveAssets();
            Debug.Log($"{Log} Build PlayGround Kit: {string.Join(", ", notes)}.", AssetDatabase.LoadAssetAtPath<Material>(MaterialPath));
            return true;
        }

        // The palette: sRGB, the colour one with the smoothness in its alpha (never transparency), clamped, no harsh
        // compression across the columns' gradients (the UVs keep 5 px inside each 32-px column).
        private static void SetUpTexture(string path, bool colour, List<string> notes)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) return;
            bool wrong = ti.textureType != TextureImporterType.Default || !ti.sRGBTexture || ti.wrapMode != TextureWrapMode.Clamp
                         || ti.textureCompression != TextureImporterCompression.CompressedHQ || ti.alphaIsTransparency
                         || ti.alphaSource != (colour ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None);
            if (!wrong) return;
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true;
            ti.alphaSource = colour ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            ti.alphaIsTransparency = false;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.mipmapEnabled = true;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.SaveAndReimport();
            notes.Add(Path.GetFileName(path) + " import settings set");
        }

        private static Material KitMaterial(List<string> notes)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null)
            {
                var lit = Shader.Find("Universal Render Pipeline/Lit");
                if (lit == null) { Debug.LogError($"{Log} No URP Lit shader; nothing changed."); return null; }
                mat = new Material(lit) { name = "PlayGround_Kit_Mat" };
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

        // Readable meshes (the stage may build colliders from them), no imported materials, animations, cameras, lights.
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
            notes.Add(Path.GetFileName(path) + " import settings set");
        }

        // A prefab of the piece with the kit material, looks only. One mesh: on the prefab's root; several (a broken
        // set): one child each, placed as in the FBX. A piece prefab that is already there is only REFRESHED (user
        // 2026-11-23: "I may edit, move them and set them"): its meshes come anew from the FBX, everything the user added
        // or changed on it (colliders, scripts, other materials, its own place) is kept.
        private static GameObject PiecePrefab(string fbxPath, Material mat)
        {
            var name = Path.GetFileNameWithoutExtension(fbxPath);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (model == null) return null;
            var filters = model.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null).ToArray();
            if (filters.Length == 0) return null;
            var path = PieceFolder + "/" + name + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return RefreshPiece(path, model, filters, mat);
            var go = new GameObject(name);
            try
            {
                var toRoot = model.transform.worldToLocalMatrix;
                foreach (var filter in filters)
                {
                    var rel = toRoot * filter.transform.localToWorldMatrix;
                    bool atRoot = filters.Length == 1 && rel.isIdentity;
                    var holder = atRoot ? go : new GameObject(filter.name);
                    if (!atRoot)
                    {
                        holder.transform.SetParent(go.transform, false);
                        holder.transform.localPosition = rel.GetColumn(3);
                        holder.transform.localRotation = rel.rotation;
                        holder.transform.localScale = rel.lossyScale;
                    }
                    holder.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                    var r = holder.AddComponent<MeshRenderer>();
                    r.sharedMaterial = mat;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }
                return PrefabUtility.SaveAsPrefabAsset(go, path);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // The meshes of a piece prefab that's already there, new from the FBX; the rest is the user's and stays. One mesh:
        // the root's mesh. A broken set: each chunk child by its name (its place inside the set from the FBX too, as the
        // set is laid out in Blender); a new chunk is added; a chunk gone from the FBX (its mesh missing now) is removed.
        // A renderer keeps its material unless it has none.
        private static GameObject RefreshPiece(string path, GameObject model, MeshFilter[] filters, Material mat)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var toRoot = model.transform.worldToLocalMatrix;
                if (filters.Length == 1 && (toRoot * filters[0].transform.localToWorldMatrix).isIdentity
                    && root.TryGetComponent(out MeshFilter own))
                {
                    own.sharedMesh = filters[0].sharedMesh;
                    Wear(root, mat);
                }
                else
                {
                    foreach (var filter in filters)
                    {
                        var rel = toRoot * filter.transform.localToWorldMatrix;
                        var child = root.transform.Find(filter.name);
                        if (child == null)
                        {
                            child = new GameObject(filter.name).transform;
                            child.SetParent(root.transform, false);
                        }
                        child.localPosition = rel.GetColumn(3);
                        child.localRotation = rel.rotation;
                        child.localScale = rel.lossyScale;
                        if (!child.TryGetComponent(out MeshFilter mf)) mf = child.gameObject.AddComponent<MeshFilter>();
                        mf.sharedMesh = filter.sharedMesh;
                        Wear(child.gameObject, mat);
                    }
                    var names = new HashSet<string>(filters.Select(f => f.name));
                    for (int i = root.transform.childCount - 1; i >= 0; i--)
                    {
                        var c = root.transform.GetChild(i);
                        if (!names.Contains(c.name) && c.TryGetComponent(out MeshFilter gone) && gone.sharedMesh == null)
                            Object.DestroyImmediate(c.gameObject);
                    }
                }
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void Wear(GameObject go, Material mat)
        {
            var r = go.GetComponent<MeshRenderer>();
            if (r == null)
            {
                r = go.AddComponent<MeshRenderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            if (r.sharedMaterial == null) r.sharedMaterial = mat;
        }

        // Gate 1's giant pumpkin: the shared one wearing the kit's stew pumpkin, its face towards its front (+Z).
        private static GameObject BossVariant(GameObject giant, List<string> notes)
        {
            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Game.Gates.EditorTools.PumpkinBallBossMenu.PrefabPath);
            if (basePrefab == null)
            {
                notes.Add("NO " + Game.Gates.EditorTools.PumpkinBallBossMenu.PrefabPath + ": no boss variant");
                return null;
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
                // The PlayGround's field is small (about 11 x 12 m): a slower charge (the shared one does 13 m/s), so it
                // can be seen coming and side-stepped. Only lowered, never raised (tune it on the variant).
                if (boss.chargeSpeed > GateOneChargeSpeed) boss.chargeSpeed = GateOneChargeSpeed;
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
            return AssetDatabase.LoadAssetAtPath<GameObject>(BossVariantPath);
        }

        // The turn that brings the pumpkin's glowing eyes (its vertices on the eye_glow column) to +Z, whatever way the
        // FBX import turned it.
        private static float FaceYaw(GameObject piece)
        {
            var filter = piece.GetComponent<MeshFilter>();
            var mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null || !mesh.isReadable) return 0f;
            var verts = mesh.vertices;
            var uvs = mesh.uv;
            float u0 = FaceSwatch / (float)Swatches, u1 = (FaceSwatch + 1) / (float)Swatches;
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

        // The cauldron's finale brings the stew pumpkin, unless someone already chose another giant pumpkin for it.
        private static void PointCauldron(GameObject variant, List<string> notes)
        {
            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Game.Gates.EditorTools.PumpkinBallBossMenu.PrefabPath);
            var root = PrefabUtility.LoadPrefabContents(CauldronPath);
            try
            {
                var defence = root.GetComponent<CauldronDefence>();
                if (defence == null) { notes.Add("no Cauldron Defence on the cauldron: its finale boss not set"); return; }
                var so = new SerializedObject(defence);
                var boss = so.FindProperty("finaleBoss");
                var now = boss.objectReferenceValue as GameObject;
                if (now == variant) { notes.Add("the cauldron already brings the stew pumpkin"); return; }
                if (now != null && now != basePrefab) { notes.Add($"the cauldron brings {now.name}: left as it is"); return; }
                boss.objectReferenceValue = variant;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, CauldronPath);
                notes.Add("the cauldron's finale now brings the stew pumpkin");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ------------------------------------------------------------------ the stage dressing

        private const string PlayGroundPath = "Assets/Prefabs/Bases/Play_Ground_Z.prefab";
        private const string DressingName = "Kit_Dressing";
        // The field's middle in the PlayGround's own space, in metres (measured in Play, 2026-11-23: the field inside the
        // walls is x -5.43..5.35, z -3.93..8.08 with the PlayGround at the world's middle).
        private static readonly Vector3 FieldMiddle = new(-0.04f, 0f, 2.08f);

        // Where each piece goes, in metres in the PlayGround's own space (the concept's layout at the game's size): the
        // monster gates on the 4 corners where the waves come in, the playground toys just outside the walls, the trees
        // further out, the slime hills round the edge. Turn: degrees; Face: turned to the field's middle first.
        private static readonly (string piece, float x, float z, float turn, bool face)[] Dressing =
        {
            ("Monster_Gate", -4.8f, -3.3f, 0f, true), ("Monster_Gate", 4.7f, -3.3f, 0f, true),
            ("Monster_Gate", 4.7f, 7.5f, 0f, true), ("Monster_Gate", -4.8f, 7.5f, 0f, true),
            ("Puddle_Slime", -2.6f, -0.8f, 0f, false), ("Puddle_Slime", 3.0f, 0.2f, 40f, false),
            ("Puddle_Slime", -1.8f, 5.6f, 80f, false),
            ("Swing_Set", -4.5f, 11.8f, 0f, true), ("Slide", 0.6f, 12.8f, 20f, true), ("Dome_CandyCane", 6.2f, 11.6f, 10f, false),
            ("Merry_Go_Round", 10.2f, 4.2f, 0f, false), ("Sandbox_Candy", 9.8f, -2.6f, 15f, false),
            ("Rocking_Horse", -9.8f, 5.2f, 70f, true), ("Jack_In_The_Box", -9.9f, 0.6f, 0f, true),
            ("Tin_Robot", -10.3f, -2.2f, 0f, true), ("Toy_Block", 3.0f, 13.8f, 20f, false), ("Toy_Block", 4.0f, 14.3f, -15f, false),
            ("Beach_Ball", 3.6f, 10.4f, 0f, false), ("Statue_Knight", -3.6f, -7.8f, 0f, true), ("Totem_Pumpkin", 4.4f, -7.8f, 0f, true),
            ("Gravestone", 10.6f, 8.6f, 0f, true), ("Gravestone", 11.9f, 6.2f, 15f, true), ("Gravestone", -11.6f, 9.6f, -10f, true),
            ("Geode_Candy", 11.9f, 1.0f, 0f, false), ("Geode_Candy", -10.3f, -6.6f, 40f, false), ("Geode_Candy", 7.1f, -8.2f, 0f, false),
            ("Pumpkin_Stack", -11.1f, 7.6f, 0f, true), ("Pumpkin_Stack", 8.1f, -6.1f, 0f, true), ("Barrel", 11.1f, -5.1f, 0f, false),
            ("Barrel", 12.1f, 10.6f, 0f, false), ("Crate", -7.6f, -8.1f, 15f, false), ("Candy_Basket", -6.6f, -8.7f, 0f, false),
            ("Lollipop_Sign", -1.6f, 13.6f, 0f, true), ("Lollipop_Sign", 8.6f, 13.1f, 0f, true), ("Tree_Mushroom", -12.6f, -4.1f, 0f, false),
            ("Bush_Jelly", -13.1f, -8.6f, 0f, false), ("Bush_Jelly", 12.6f, -9.1f, 0f, false), ("Bush_Jelly", -3.6f, 15.6f, 0f, false),
            ("Stump_Slime", 14.1f, -2.1f, 0f, false), ("Stump_Slime", -15.1f, 6.1f, 0f, false),
            ("Tree_Jelly_A", -14.1f, 11.6f, 0f, false), ("Tree_Jelly_A", -8.1f, 18.1f, 40f, false), ("Tree_Jelly_A", 13.6f, 15.6f, 80f, false),
            ("Tree_Jelly_B", -16.1f, 0.1f, 20f, false), ("Tree_Jelly_B", 2.6f, 19.1f, 0f, false), ("Tree_Jelly_B", 16.6f, 6.1f, 60f, false),
            ("Tree_Lantern", -11.6f, 15.9f, 0f, true), ("Tree_Lantern", 9.1f, 17.3f, 0f, true), ("Tree_Lantern", 16.9f, -5.5f, 0f, true),
            ("Tree_Dead", -19.1f, 14.1f, 0f, false), ("Tree_Dead", 19.6f, 15.1f, 90f, false), ("Tree_Dead", -20.1f, -7.1f, 30f, false),
            ("Tree_Dead", 21.1f, 0.1f, 0f, false),
            ("Hill_Slime", 0f, 26f, 0f, true), ("Hill_Slime", -17f, 23f, 0f, true), ("Hill_Slime", 17f, 23f, 0f, true),
            ("Hill_Slime", -25f, 5f, 0f, true), ("Hill_Slime", 25f, 5f, 0f, true), ("Hill_Slime", -22f, -12f, 0f, true),
            ("Hill_Slime", 22f, -12f, 0f, true),
        };

        /// <summary>
        /// Tools > 429 Game > PlayGround > Dress PlayGround With Kit: Play_Ground_Z.prefab gets a child Kit_Dressing with the
        /// kit's pieces round its field, laid out from the list above. ONCE: when Kit_Dressing is already there it is left
        /// alone (user 2026-11-23: "I may edit, move them and set them"), so moving, adding (drag a prefab from
        /// Prefabs/PlayGround/Kit under Kit_Dressing) or deleting pieces by hand in Prefab Mode is kept. Start over from the
        /// list: Reset Kit Dressing. Looks only (no colliders): the field, its walls, its props and their gameplay are
        /// untouched. Its own scale cancels the PlayGround's (x1.5), so the pieces are their real size. A copy of the
        /// prefab from before the first run goes to Documents/429R_claude_backups/2026-11-23/gate1/. Never touches the scene.
        /// </summary>
        [MenuItem(Root + "Dress PlayGround With Kit")]
        public static void Dress() => DressFromList(false);

        /// <summary>
        /// Tools > 429 Game > PlayGround > Reset Kit Dressing (Loses Hand Changes): Kit_Dressing made again from the list,
        /// after a yes in a dialog; what was moved, added or deleted by hand is lost (a copy of the prefab goes to the
        /// backup folder first).
        /// </summary>
        [MenuItem(Root + "Reset Kit Dressing (Loses Hand Changes)")]
        public static void ResetDressing()
        {
            if (!EditorUtility.DisplayDialog("Reset Kit Dressing",
                    "Kit_Dressing in Play_Ground_Z is made again from the list. Every piece you moved, added or deleted by hand " +
                    "goes back to the list's layout. A copy of the prefab goes to the backup folder first.", "Reset", "Cancel"))
                return;
            DressFromList(true);
        }

        [MenuItem(Root + "Reset Kit Dressing (Loses Hand Changes)", true)]
        private static bool CanResetDressing() => !EditorApplication.isPlaying;

        private static void DressFromList(bool reset)
        {
            if (EditorApplication.isPlaying) { Debug.LogError($"{Log} Stop play mode first; nothing changed."); return; }
            if (!AssetDatabase.IsValidFolder(PieceFolder)) { Debug.LogError($"{Log} No kit prefabs: run Build PlayGround Kit first; nothing changed."); return; }
            BackupPlayGround("Play_Ground_Z_before_dressing.prefab");
            if (reset) BackupPlayGround($"Play_Ground_Z_before_reset_{System.DateTime.Now:yyyyMMdd_HHmmss}.prefab");
            var root = PrefabUtility.LoadPrefabContents(PlayGroundPath);
            int placed = 0;
            var missing = new List<string>();
            try
            {
                var old = root.transform.Find(DressingName);
                if (old != null && !reset)
                {
                    Debug.Log($"{Log} Dress PlayGround With Kit: {DressingName} is already there ({old.childCount} pieces): left as it " +
                              "is, with your hand changes. To lay it out from the list again: Reset Kit Dressing (Loses Hand Changes).");
                    return;
                }
                if (old != null) Object.DestroyImmediate(old.gameObject);
                var holder = new GameObject(DressingName).transform;
                holder.SetParent(root.transform, false);
                var s = root.transform.localScale;
                holder.localScale = new Vector3(1f / s.x, 1f / s.y, 1f / s.z);
                foreach (var (piece, x, z, turn, face) in Dressing)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PieceFolder + "/" + Prefix + piece + ".prefab");
                    if (prefab == null)
                    {
                        if (!missing.Contains(piece)) missing.Add(piece);
                        continue;
                    }
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder);
                    var at = new Vector3(x, 0f, z);
                    var look = FieldMiddle - at;
                    look.y = 0f;
                    float yaw = (face && look.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(look).eulerAngles.y : 0f) + turn;
                    go.transform.localPosition = at;
                    go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                    go.name = piece + "_" + (++placed);
                }
                PrefabUtility.SaveAsPrefabAsset(root, PlayGroundPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            Debug.Log($"{Log} {(reset ? "Reset Kit Dressing" : "Dress PlayGround With Kit")}: {placed} pieces round the field in {PlayGroundPath} ({DressingName})" +
                      (missing.Count > 0 ? "; MISSING " + string.Join(", ", missing) : "") + ".");
        }

        [MenuItem(Root + "Dress PlayGround With Kit", true)]
        private static bool CanDress() => !EditorApplication.isPlaying;

        /// <summary>Tools > 429 Game > PlayGround > Remove Kit Dressing: Play_Ground_Z as before the dressing.</summary>
        [MenuItem(Root + "Remove Kit Dressing")]
        public static void Undress()
        {
            if (EditorApplication.isPlaying) { Debug.LogError($"{Log} Stop play mode first; nothing changed."); return; }
            if (!EditorUtility.DisplayDialog("Remove Kit Dressing",
                    "Kit_Dressing (every kit piece round the PlayGround, with your hand changes) is removed from Play_Ground_Z. " +
                    "A copy of the prefab goes to the backup folder first.", "Remove", "Cancel"))
                return;
            BackupPlayGround($"Play_Ground_Z_before_remove_{System.DateTime.Now:yyyyMMdd_HHmmss}.prefab");
            var root = PrefabUtility.LoadPrefabContents(PlayGroundPath);
            bool had = false;
            try
            {
                var old = root.transform.Find(DressingName);
                if (old != null)
                {
                    Object.DestroyImmediate(old.gameObject);
                    had = true;
                    PrefabUtility.SaveAsPrefabAsset(root, PlayGroundPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            Debug.Log($"{Log} Remove Kit Dressing: " + (had ? "removed." : "there was none."));
        }

        [MenuItem(Root + "Remove Kit Dressing", true)]
        private static bool CanUndress() => !EditorApplication.isPlaying;

        private static void BackupPlayGround(string name)
        {
            string folder = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
                "429R_claude_backups", "2026-11-23", "gate1");
            Directory.CreateDirectory(folder);
            string to = Path.Combine(folder, name);
            if (!File.Exists(to)) File.Copy(PlayGroundPath, to);
        }
    }
}

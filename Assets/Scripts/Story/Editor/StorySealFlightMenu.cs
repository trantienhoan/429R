using System.Collections.Generic;
using Game.Inventory;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// User 2026-11-26: "I want to re-model those pieces of the Seed now. All the Yellow Seed now is a piece of the Seed,
    /// still has Green aura, but not as strong as the Seed, when the seal on the rooftop is broken, all the collected pieces
    /// fly out, circling and join in as whole seed, dig to the ground and grow up as the Pumpkin Tree. We can remove yellow
    /// seed currency from the store. Only the Boss Seed is kept and be able to be spent on gates."
    /// Two re-runnable menus (prefabs and assets; never the scene):
    /// - Tools > 429 Game > Story > Seed Pieces: New Model And The Seal Flight:
    ///   * Models/Story/Seed_Piece.fbx (a fifth of the Boss Seed's body without its stem, its cut sides the atlas' straw:
    ///     made from The_Boss_Seed.fbx, source Exports/Story/Seed_Piece.blend) becomes the look of the 6 pieces
    ///     (Items/Chest/Seed_Bracer, _Bracer_Cauldron, _Chest, _Head, _Shield, _Boot) instead of the dark Magical_Seed_Bad,
    ///     with the Boss Seed's material (their green aura stays, from Seeds Into Seed Pieces);
    ///   * The_Seed wears the Boss Seed's model (user: "use the model of the Boss Seed for The Seed"): the Seed grows the
    ///     Pumpkin Tree, the Boss Seed is the boss's prize; they look alike;
    ///   * Story Seed Settings: Piece Item = Yellow_Seed, Whole Seed Look = The_Seed, stand-in sounds where empty
    ///     (whoosh.wav, holy_seed.wav / holy_seed_2.wav) until the user's seed_* sounds are in;
    ///   * the rooftop (Bases/Rooftop_Base_Z, Pumpkin_Tree_Pos): Seeds Into Soil takes Flight Seconds (6) and the tree waits
    ///     Tree Wait (6.3) for it: pop out, fly, circle, join into the Seed, dig, then the tree.
    /// - Tools > 429 Game > Shop > Yellow Seed Out Of The Store (Boss Seed For Gates): the Candy Shop's special line is no
    ///   longer locked behind Yellow Seeds, and the 3 gate locks (Items/Stone_Cube_Dice 1-3) take 1 Boss Seed only.
    /// </summary>
    public static class StorySealFlightMenu
    {
        private const string Tag = "[Seed Pieces]";
        private const string PieceFbx = "Assets/Models/Story/Seed_Piece.fbx";
        private const string OldPieceFbx = "Assets/Models/Bedroom_Stuffs/Magical_Seed_Bad.fbx";
        // The Seed's look is the Boss Seed's (user 2026-11-26: "use the model of the Boss Seed for The Seed. The only
        // different is the Seed is now used to grow the Pumpkin Tree, the Boss Seed is the one we can obtain after
        // beating the Boss"): the pieces are cut from it and The_Seed wears it.
        private const string SeedFbx = "Assets/Models/Bedroom_Stuffs/The_Boss_Seed.fbx";
        private const string OldSeedFbx = "Assets/Models/Bedroom_Stuffs/Magical_Seed.fbx";
        private const string BossSeedPrefab = "Assets/Prefabs/Items/Chest/The_Boss_Seed.prefab";
        private const string ChestFolder = "Assets/Prefabs/Items/Chest/";
        private static readonly string[] Pieces = { "Seed_Bracer", "Seed_Bracer_Cauldron", "Seed_Chest", "Seed_Head", "Seed_Shield", "Seed_Boot" };
        private const string WholeSeed = ChestFolder + "The_Seed.prefab";
        private const string SettingsPath = "Assets/Resources/Story Seed Settings.asset";
        private const string PieceItemPath = "Assets/Data/Inventory/Items/Yellow_Seed.asset";
        private const string BossSeedPath = "Assets/Data/Inventory/Items/Boss_Seed.asset";
        private const string RooftopPath = "Assets/Prefabs/Bases/Rooftop_Base_Z.prefab";
        private const string ShopPath = "Assets/Data/Shop/Candy Shop.asset";
        private const string LockPath = "Assets/Prefabs/Items/Stone_Cube_Dice {0}.prefab";
        private const string Sfx = "Assets/Audio/SFX/";
        // The dig's brown dirt (user 2026-11-26: "Yes, brown dirt"): a copy of the boss's scrape dust, its dust browner.
        private const string ScrapeDust = "Assets/Prefabs/Enemies/Boss_FX/Boss_Scrape_Dust.prefab";
        private const string DirtPuffPath = "Assets/Prefabs/Story/Seed_Dig_Dirt.prefab";
        private static readonly Color DirtDark = new Color(0.33f, 0.22f, 0.13f, 1f);
        private static readonly Color DirtLight = new Color(0.5f, 0.36f, 0.22f, 1f);

        private const string DirtMatPath = "Assets/Materials/Story/Seed_Dig_Dirt_Mat.mat";

        // Prefabs/Story/Seed_Dig_Dirt: a copy of Boss_Scrape_Dust (made once), set every run: its 3D dust brown, solid and
        // small, spurting up and falling back (the shared smoke look made it pale grey-blue under the night fog, and its
        // 4 m/s dust flew 2.5 m wide at the camera); its brown-orange dirt flecks slower.
        private static GameObject DirtPuff(List<string> notes)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(DirtPuffPath) == null)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(ScrapeDust) == null) { notes.Add("no " + ScrapeDust + " (no dirt puff)"); return null; }
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(DirtPuffPath));
                AssetDatabase.CopyAsset(ScrapeDust, DirtPuffPath);
                notes.Add("made Seed_Dig_Dirt");
            }
            var root = PrefabUtility.LoadPrefabContents(DirtPuffPath);
            try
            {
                foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = ps.main;
                    var look = ps.GetComponent<ParticleSystemRenderer>();
                    if (ps.name == "Dust")
                    {
                        main.startColor = new ParticleSystem.MinMaxGradient(DirtDark, DirtLight);
                        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
                        main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
                        main.gravityModifier = 0.35f;
                        if (look != null) look.sharedMaterial = DirtMaterial(look.sharedMaterial);
                        // Its colour over life only fades it out (no lighter tint).
                        var fade = ps.colorOverLifetime;
                        if (fade.enabled)
                        {
                            var g = new Gradient();
                            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.6f), new GradientAlphaKey(0f, 1f) });
                            fade.color = new ParticleSystem.MinMaxGradient(g);
                        }
                    }
                    else if (ps.name == "Dirt")
                    {
                        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 3.5f);
                        main.gravityModifier = 1f;
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(root, DirtPuffPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            notes.Add("Seed_Dig_Dirt: brown, small, falls back");
            return AssetDatabase.LoadAssetAtPath<GameObject>(DirtPuffPath);
        }

        // Its own copy of the 3D smoke look: more solid, hardly any light rim (the shared one stays as it is).
        private static Material DirtMaterial(Material from)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(DirtMatPath);
            if (mat == null)
            {
                if (from == null) return null;
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(DirtMatPath));
                mat = new Material(from) { name = "Seed_Dig_Dirt_Mat" };
                AssetDatabase.CreateAsset(mat, DirtMatPath);
            }
            if (mat.HasProperty("_Opacity")) mat.SetFloat("_Opacity", 0.85f);
            if (mat.HasProperty("_RimStrength")) mat.SetFloat("_RimStrength", 0.08f);
            if (mat.HasProperty("_RimColor")) mat.SetColor("_RimColor", DirtLight);
            if (mat.HasProperty("_EdgeSoftness")) mat.SetFloat("_EdgeSoftness", 0.3f);
            if (mat.HasProperty("_Brightness")) mat.SetFloat("_Brightness", 0.95f);
            EditorUtility.SetDirty(mat);
            return mat;
        }
        // A piece is a fifth of the Seed; Seed_Piece.fbx is exported 1.35 times its true size so it still reads.
        private const float PieceScale = 1f;   // (the 1.35 is in the FBX now: the mesh is on the prefab root, whose scale would grow its aura too)
        private const float FlightSeconds = 6f;
        private const float TreeWait = 6.3f;

        [MenuItem("Tools/429 Game/Story/Seed Pieces: New Model And The Seal Flight")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) { Debug.LogError($"{Tag} Stop play mode first; nothing changed."); return; }
            var notes = new List<string>();
            var seedMaterials = SeedMaterials();
            var pieceMesh = seedMaterials != null ? PrepareFbx(seedMaterials, notes) : null;
            // The piece's own slot order (its FBX lists the leaf first): the FBX's renderer, its names mapped to the Seed's.
            var pieceMaterials = PieceMaterials();
            if (pieceMesh == null || pieceMaterials == null)
            {
                Debug.LogError($"{Tag} {PieceFbx} (its mesh and materials) or The_Seed's materials are missing; nothing changed.");
                return;
            }
            foreach (var name in Pieces) NewLook(ChestFolder + name + ".prefab", pieceMesh, pieceMaterials, notes);
            TheSeedLook(seedMaterials, notes);
            Settings(notes);
            Rooftop(notes);
            AssetDatabase.SaveAssets();
            Debug.Log($"{Tag} {string.Join("; ", notes)}.");
        }

        [MenuItem("Tools/429 Game/Shop/Yellow Seed Out Of The Store (Boss Seed For Gates)")]
        public static void Store()
        {
            if (EditorApplication.isPlaying) { Debug.LogError($"{Tag} Stop play mode first; nothing changed."); return; }
            var notes = new List<string>();
            var shop = AssetDatabase.LoadAssetAtPath<ScriptableObject>(ShopPath);
            if (shop != null)
            {
                var so = new SerializedObject(shop);
                var unlock = so.FindProperty("specialUnlockCurrency");
                if (unlock != null && unlock.objectReferenceValue != null)
                {
                    notes.Add("Candy Shop: the special line no longer costs " + unlock.objectReferenceValue.name);
                    unlock.objectReferenceValue = null;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(shop);
                }
                else notes.Add("Candy Shop: no seed unlock (already)");
            }
            var boss = AssetDatabase.LoadAssetAtPath<ItemDefinition>(BossSeedPath);
            for (int n = 1; n <= 3 && boss != null; n++)
            {
                string path = string.Format(LockPath, n);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { notes.Add("no " + path); continue; }
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var seedLock = root.GetComponent<Game.Paths.SeedLock>();
                    if (seedLock == null) { notes.Add(root.name + ": no Seed Lock"); continue; }
                    var so = new SerializedObject(seedLock);
                    var prices = so.FindProperty("prices");
                    prices.arraySize = 1;
                    var price = prices.GetArrayElementAtIndex(0);
                    price.FindPropertyRelative("item").objectReferenceValue = boss;
                    price.FindPropertyRelative("amount").intValue = 1;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    notes.Add(root.name + ": 1 Boss Seed");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"{Tag} Store: {string.Join("; ", notes)}.");
        }

        [MenuItem("Tools/429 Game/Story/Seed Pieces: New Model And The Seal Flight", true)]
        [MenuItem("Tools/429 Game/Shop/Yellow Seed Out Of The Store (Boss Seed For Gates)", true)]
        private static bool CanRun() => !EditorApplication.isPlaying;

        // The piece's FBX: its two materials (Magical_Tree, Magical_Leaf) mapped to the Seed's own, no animation; its mesh.
        private static Mesh PrepareFbx(Material[] seedMaterials, List<string> notes)
        {
            if (!(AssetImporter.GetAtPath(PieceFbx) is ModelImporter importer)) return null;
            bool changed = false;
            if (importer.materialImportMode == ModelImporterMaterialImportMode.None) { importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard; changed = true; }
            if (importer.animationType != ModelImporterAnimationType.None) { importer.animationType = ModelImporterAnimationType.None; changed = true; }
            if (importer.importAnimation) { importer.importAnimation = false; changed = true; }
            if (changed) importer.SaveAndReimport();
            // Its materials are the Boss Seed FBX's (it was cut from it, so the names match): the same remaps as that FBX's
            // (Bedroom_Furniture_Set1 -> Bedroom_Set_1_Mat). Remaps the piece no longer needs (the first piece's
            // Magical_Tree / Magical_Leaf) go.
            var wanted = new Dictionary<string, Object>();
            if (AssetImporter.GetAtPath(SeedFbx) is ModelImporter source)
                foreach (var kv in source.GetExternalObjectMap())
                    if (kv.Key.type == typeof(Material) && kv.Value != null) wanted[kv.Key.name] = kv.Value;
            foreach (var kv in wanted)
            {
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key);
                importer.GetExternalObjectMap().TryGetValue(id, out var had);
                if (had == kv.Value) continue;
                importer.AddRemap(id, kv.Value);
                changed = true;
            }
            foreach (var kv in new List<KeyValuePair<AssetImporter.SourceAssetIdentifier, Object>>(importer.GetExternalObjectMap()))
                if (kv.Key.type == typeof(Material) && !wanted.ContainsKey(kv.Key.name)) { importer.RemoveRemap(kv.Key); changed = true; }
            if (changed) { importer.SaveAndReimport(); notes.Add("Seed_Piece.fbx imported (the Seed's materials, no animation)"); }
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(PieceFbx))
                if (asset is Mesh m) return m;
            return null;
        }

        // The piece FBX's own renderer materials, in its submesh order.
        private static Material[] PieceMaterials()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(PieceFbx);
            var look = model != null ? model.GetComponentInChildren<MeshRenderer>() : null;
            if (look == null) return null;
            foreach (var m in look.sharedMaterials) if (m == null || m.name == "Lit" || m.name.StartsWith("Default")) return null;
            return look.sharedMaterials;
        }

        // The Boss Seed's own materials (its renderer that shows The_Boss_Seed), in its submesh order.
        private static Material[] SeedMaterials()
        {
            var seed = AssetDatabase.LoadAssetAtPath<GameObject>(BossSeedPrefab);
            if (seed == null) return null;
            foreach (var filter in seed.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || AssetDatabase.GetAssetPath(filter.sharedMesh) != SeedFbx) continue;
                var look = filter.GetComponent<MeshRenderer>();
                if (look != null) return look.sharedMaterials;
            }
            return null;
        }

        private static void NewLook(string path, Mesh pieceMesh, Material[] materials, List<string> notes)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { notes.Add("no " + path); return; }
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int swapped = 0;
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null) continue;
                    string from = AssetDatabase.GetAssetPath(filter.sharedMesh);
                    bool old = from == OldPieceFbx;   // (a piece already showing Seed_Piece keeps it: the mesh updates in place)
                    if (!old && filter.sharedMesh != pieceMesh) continue;
                    if (old)
                    {
                        filter.sharedMesh = pieceMesh;
                        filter.transform.localScale *= PieceScale;
                        swapped++;
                    }
                    var look = filter.GetComponent<MeshRenderer>();
                    if (look != null) look.sharedMaterials = materials;
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
                notes.Add(root.name + (swapped > 0 ? ": the new piece" : ": the piece already"));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // The_Seed (what the pieces join into, what grows the Pumpkin Tree, the gates' last seed) wears the Boss Seed's
        // model and materials; its own aura, FSM and the rest stay.
        private static void TheSeedLook(Material[] materials, List<string> notes)
        {
            Mesh bossMesh = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(SeedFbx)) if (asset is Mesh m) bossMesh = m;
            if (bossMesh == null) { notes.Add("no Boss Seed mesh"); return; }
            var root = PrefabUtility.LoadPrefabContents(WholeSeed);
            try
            {
                int swapped = 0;
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null) continue;
                    string from = AssetDatabase.GetAssetPath(filter.sharedMesh);
                    if (from != OldSeedFbx && filter.sharedMesh != bossMesh) continue;
                    if (filter.sharedMesh != bossMesh) { filter.sharedMesh = bossMesh; swapped++; }
                    var look = filter.GetComponent<MeshRenderer>();
                    if (look != null) look.sharedMaterials = materials;
                }
                PrefabUtility.SaveAsPrefabAsset(root, WholeSeed);
                notes.Add("The_Seed: " + (swapped > 0 ? "now the Boss Seed's model" : "the Boss Seed's model already"));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void Settings(List<string> notes)
        {
            var settings = AssetDatabase.LoadAssetAtPath<ScriptableObject>(SettingsPath);
            if (settings == null) { notes.Add("no Story Seed Settings"); return; }
            var so = new SerializedObject(settings);
            void Fill(string field, Object value)
            {
                var p = so.FindProperty(field);
                if (p != null && p.objectReferenceValue == null && value != null) p.objectReferenceValue = value;
            }
            Fill("pieceItem", AssetDatabase.LoadAssetAtPath<ItemDefinition>(PieceItemPath));
            Fill("wholeSeedLook", AssetDatabase.LoadAssetAtPath<GameObject>(WholeSeed));
            Fill("pieceChime", AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + "holy_seed_2.wav"));
            Fill("flightWhoosh", AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + "whoosh.wav"));
            Fill("mergeSound", AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + "holy_seed.wav"));
            Fill("digPuff", DirtPuff(notes));
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            notes.Add("Story Seed Settings: pieces = " + Name(so, "pieceItem") + ", whole seed = " + Name(so, "wholeSeedLook"));
        }

        private static string Name(SerializedObject so, string field)
        {
            var p = so.FindProperty(field);
            return p != null && p.objectReferenceValue != null ? p.objectReferenceValue.name : "none";
        }

        // The rooftop's tree waits for the longer flight.
        private static void Rooftop(List<string> notes)
        {
            var root = PrefabUtility.LoadPrefabContents(RooftopPath);
            try
            {
                PlayMakerFSM fsm = null;
                foreach (var f in root.GetComponentsInChildren<PlayMakerFSM>(true))
                    if (f.gameObject.name == "Pumpkin_Tree_Pos") fsm = f;
                if (fsm == null) { notes.Add("rooftop: no Pumpkin_Tree_Pos FSM"); return; }
                fsm.Fsm.Init(fsm);
                bool changed = false;
                foreach (var state in fsm.Fsm.States)
                {
                    state.LoadActions();
                    bool here = false;
                    foreach (var action in state.Actions)
                    {
                        if (action is HutongGames.PlayMaker.Actions.SeedsIntoSoil soil)
                        {
                            soil.seconds = FlightSeconds;
                            here = true;
                        }
                    }
                    if (!here) continue;
                    foreach (var action in state.Actions)
                        if (action is HutongGames.PlayMaker.Actions.Wait wait) wait.time = TreeWait;
                    state.SaveActions();
                    changed = true;
                }
                if (changed)
                {
                    EditorUtility.SetDirty(fsm);
                    PrefabUtility.SaveAsPrefabAsset(root, RooftopPath);
                    notes.Add($"rooftop: the flight {FlightSeconds} s, the tree after {TreeWait} s");
                }
                else notes.Add("rooftop: no Seeds Into Soil found");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}

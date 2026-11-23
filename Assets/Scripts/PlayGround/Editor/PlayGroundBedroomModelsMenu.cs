using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.PlayGround.Editor
{
    /// <summary>
    /// Gate 1, 2026-11-23. The user: "check Bedroom_Walls.blend. I want to reuse my model in there for the Playground
    /// Kit", then "Use your suggestion" (21 of their own house models, numbered on
    /// Exports/Gates/gate1/kit_renders/bedroom_walls_models_2026-11-23.jpg).
    ///
    /// Tools > 429 Game > PlayGround > Place Bedroom Models puts them in Play_Ground_Z under Bedroom_Models:
    /// - the house prefabs as they are (Wood_Barrel, Wood_Box, the candy basket, pumpkin_bag, Skull_2, Pumpkin_Tree,
    ///   Spider_Web, Bat_Candle_Holder, Rooftop_Pole_Light, the clock tower): they break when hit, like in the house
    ///   (their own pieces and smoke only; nothing in them points at a house room);
    /// - looks-only models (the house FBX, or the gate 2 session's House_Pieces prefabs for Sleigh, Tank and Dino_Rex,
    ///   which weren't in Unity before): "solid" ones get a box collider per mesh and the Wall layer on the placed copy
    ///   (LiveNavMesh cuts them out, so monsters walk round the toys in the field). The source prefabs/FBX are untouched.
    /// A kit piece a model takes the place of (Kit_Dressing) is switched off and renamed "..._off" (not deleted).
    /// ONCE: when Bedroom_Models is already there it's left alone (the user moves and sets them by hand). Reset Bedroom
    /// Models (Loses Hand Changes) lays it out again; Remove Bedroom Models takes it out and switches the kit pieces back
    /// on. Both ask first and back the prefab up. Never touches the scene.
    /// </summary>
    public static class PlayGroundBedroomModelsMenu
    {
        private const string Root = "Tools/429 Game/PlayGround/";
        private const string Log = "[PlayGround Bedroom Models]";
        private const string PlayGroundPath = "Assets/Prefabs/Bases/Play_Ground_Z.prefab";
        private const string HolderName = "Bedroom_Models";
        private const string KitDressingName = "Kit_Dressing";
        private const string OffTag = "_off";

        private const string Breakable = "Assets/Prefabs/Items/Breakable/";
        private const string BedRoom = "Assets/Prefabs/BedRoom/";
        private const string Lights = "Assets/Prefabs/Light/";
        private const string HousePieces = "Assets/Prefabs/PlayGround/House_Pieces/";
        private const string BedroomFbx = "Assets/Models/Bedroom_Stuffs/";

        // Where each goes, in metres in the PlayGround's own space (like Kit_Dressing: the field inside the walls is
        // x -5.43..5.35, z -3.93..8.08). y: the model's bottom (0 = the ground). Turn: degrees. Size: x its own size.
        // Solid: box colliders + the Wall layer on the placed copy. Replaces: the Kit_Dressing piece at that spot
        // (switched off), or null. The comment = the model's number on the picture sheet.
        // The PlayGround's only floor (Teleport Area) is x -7.6..7.6, z -7.8..8.9: the breakables (free physics bodies)
        // stand on it, in the strip between the field's walls and its edge (theirs after GO, when the walls go down);
        // looks-only models stand further out, on the painted ground with nothing under it.
        private static readonly (string asset, float x, float y, float z, float turn, float size, bool solid, string replaces)[] Layout =
        {
            // the toys inside the field, x2 (75 Tank, 76 Dino, 72 Sleigh)
            (HousePieces + "House_Tank_03.prefab", -3.6f, 0f, 3.4f, 60f, 2f, true, null),
            (HousePieces + "House_Dino_Rex.prefab", 3.4f, 0f, 4.6f, -120f, 2f, true, null),
            (HousePieces + "House_Sleigh.prefab", 2.9f, 0f, 1.9f, 200f, 2f, true, null),
            // breakables on the strip round the field
            (BedRoom + "Wood_Barrel.prefab", 6.5f, 0f, -1.0f, 0f, 1f, false, null),                         // 50
            (BedRoom + "Wood_Barrel.prefab", -6.6f, 0f, 5.2f, 30f, 1f, false, null),
            (BedRoom + "Wood_Box.prefab", 6.6f, 0f, 4.0f, 15f, 1f, false, null),                            // 52
            (BedRoom + "Bedroom_Candy_Basket.prefab", -3.0f, 0f, -6.4f, 0f, 1f, false, null),               // 53
            (Breakable + "pumpkin_bag.prefab", 6.4f, 0f, -6.2f, 40f, 1f, false, null),                      // 20
            (Breakable + "pumpkin_bag.prefab", -6.6f, 0f, 0.4f, 0f, 1f, false, null),
            (Breakable + "Skull_2.prefab", 2.6f, 0f, -5.6f, 160f, 1f, false, null),                         // 33
            (BedRoom + "Livingroom_Clock_Tower.prefab", 0.0f, 0f, -7.1f, 0f, 1f, false, null),              // 66
            (Lights + "Rooftop_Pole_Light.prefab", -6.6f, 0f, 8.3f, 43f, 1f, false, null),                  // 65
            (Lights + "Rooftop_Pole_Light.prefab", 6.5f, 0f, 8.3f, 136f, 1f, false, null),
            (BedroomFbx + "pumpkin_bucket.fbx", -1.9f, 0f, -6.8f, 0f, 1f, true, null),                      // 22
            // the stone block in the south-west corner of the floor, the bat candles on it and the web on its side
            (BedroomFbx + "Stone_Cube2.fbx", -6.4f, 0f, -6.5f, 0f, 1f, true, null),                         // 19
            (Lights + "Bat_Candle_Holder.prefab", -6.4f, 2.22f, -6.5f, 90f, 1f, false, null),               // 43
            (BedRoom + "Spider_Web.prefab", -5.1f, 0.5f, -6.4f, 0f, 1f, false, null),                       // 40
            // looks only, further out, in place of the kit pieces they match
            (BedroomFbx + "Stone_Pumpkin_2.fbx", 4.4f, 0f, -7.8f, 0f, 1f, true, "Totem_Pumpkin"),          // 18
            (BedroomFbx + "Armour_Stand_Sign1.fbx", -1.6f, 0f, 13.6f, 180f, 1f, true, "Lollipop_Sign"),     // 47
            (BedroomFbx + "Armour_Stand_Sign1.fbx", 8.6f, 0f, 13.1f, 200f, 1f, true, "Lollipop_Sign"),
            (BedroomFbx + "Jenga_Block.fbx", 3.0f, 0f, 13.8f, 20f, 1f, true, "Toy_Block"),                  // 63
            (BedroomFbx + "Jenga_Block.fbx", 4.0f, 0f, 14.3f, -15f, 1f, true, "Toy_Block"),
            (BedroomFbx + "The_Cage.fbx", -12.8f, 0f, 3.0f, 0f, 1f, true, null),                            // 38
            (Breakable + "Pumpkin_Tree.prefab", -12.6f, 0f, -4.1f, 0f, 1f, false, "Tree_Mushroom"),         // 5 (kinematic)
            (HousePieces + "House_Magical_Tree_Big.prefab", -16.1f, 0f, 0.1f, 20f, 1f, false, "Tree_Jelly_B"), // 7
            (TexturedTree, -19.1f, 0f, 14.1f, 0f, 1f, false, "Tree_Dead"),                                  // 2
            (TexturedTree, 21.1f, 0f, 0.1f, 90f, 1f, false, "Tree_Dead"),
        };

        // Picture sheet #2 (Big_Tree_01_01.001), exported by Exports/Gates/gate1/tools/export_house_tree.py; it wears the
        // house furniture material.
        private const string TexturedTree = "Assets/Models/Gates/PlayGround/PG_Big_Tree_Textured.fbx";
        private const string HouseMaterial = "Assets/Materials/Bedroom_Set_1_Mat.mat";

        private static void TreeMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(HouseMaterial);
            if (mat == null || !(AssetImporter.GetAtPath(TexturedTree) is ModelImporter importer)) return;
            var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), "Bedroom_Furniture_Set1");
            if (importer.GetExternalObjectMap().TryGetValue(id, out var now) && now == mat) return;
            importer.AddRemap(id, mat);
            importer.SaveAndReimport();
        }

        /// <summary>Tools > 429 Game > PlayGround > Place Bedroom Models (see the class).</summary>
        [MenuItem(Root + "Place Bedroom Models")]
        public static void Place() => PlaceFromList(false);

        [MenuItem(Root + "Place Bedroom Models", true)]
        private static bool CanPlace() => !EditorApplication.isPlaying;

        /// <summary>Tools > 429 Game > PlayGround > Reset Bedroom Models (Loses Hand Changes): laid out again from the list.</summary>
        [MenuItem(Root + "Reset Bedroom Models (Loses Hand Changes)")]
        public static void ResetModels()
        {
            if (!EditorUtility.DisplayDialog("Reset Bedroom Models",
                    "Bedroom_Models in Play_Ground_Z is made again from the list. Every model you moved, added or deleted by " +
                    "hand goes back to the list's layout. A copy of the prefab goes to the backup folder first.", "Reset", "Cancel"))
                return;
            PlaceFromList(true);
        }

        [MenuItem(Root + "Reset Bedroom Models (Loses Hand Changes)", true)]
        private static bool CanReset() => !EditorApplication.isPlaying;

        /// <summary>Tools > 429 Game > PlayGround > Remove Bedroom Models: out again, the kit pieces back on.</summary>
        [MenuItem(Root + "Remove Bedroom Models")]
        public static void Remove()
        {
            if (!EditorUtility.DisplayDialog("Remove Bedroom Models",
                    "Bedroom_Models (your house models in the PlayGround, with your hand changes) is removed from Play_Ground_Z, " +
                    "and the kit pieces they stood in for are switched back on. A copy of the prefab goes to the backup folder first.",
                    "Remove", "Cancel"))
                return;
            Backup($"Play_Ground_Z_before_remove_bedroom_{System.DateTime.Now:yyyyMMdd_HHmmss}.prefab");
            var root = PrefabUtility.LoadPrefabContents(PlayGroundPath);
            int back = 0;
            bool had = false;
            try
            {
                var old = root.transform.Find(HolderName);
                if (old != null)
                {
                    Object.DestroyImmediate(old.gameObject);
                    had = true;
                }
                back = KitPiecesBackOn(root.transform);
                if (had || back > 0) PrefabUtility.SaveAsPrefabAsset(root, PlayGroundPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            Debug.Log($"{Log} Remove Bedroom Models: " + (had ? "removed" : "there were none") + $"; {back} kit piece(s) back on.");
        }

        [MenuItem(Root + "Remove Bedroom Models", true)]
        private static bool CanRemove() => !EditorApplication.isPlaying;

        private static void PlaceFromList(bool reset)
        {
            if (EditorApplication.isPlaying) { Debug.LogError($"{Log} Stop play mode first; nothing changed."); return; }
            TreeMaterial();
            Backup("Play_Ground_Z_before_bedroom_models.prefab");
            if (reset) Backup($"Play_Ground_Z_before_bedroom_reset_{System.DateTime.Now:yyyyMMdd_HHmmss}.prefab");
            var root = PrefabUtility.LoadPrefabContents(PlayGroundPath);
            int placed = 0, off = 0, spots = 0;
            var missing = new List<string>();
            try
            {
                var old = root.transform.Find(HolderName);
                if (old != null && !reset)
                {
                    Debug.Log($"{Log} Place Bedroom Models: {HolderName} is already there ({old.childCount} models): left as it is, " +
                              "with your hand changes. To lay it out from the list again: Reset Bedroom Models (Loses Hand Changes).");
                    return;
                }
                if (old != null) Object.DestroyImmediate(old.gameObject);
                if (reset) KitPiecesBackOn(root.transform);
                var holder = new GameObject(HolderName).transform;
                holder.SetParent(root.transform, false);
                var s = root.transform.localScale;
                holder.localScale = new Vector3(1f / s.x, 1f / s.y, 1f / s.z);
                var kit = root.transform.Find(KitDressingName);
                int wall = LayerMask.NameToLayer("Wall");
                foreach (var (path, x, y, z, turn, size, solid, replaces) in Layout)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null)
                    {
                        if (!missing.Contains(path)) missing.Add(path);
                        continue;
                    }
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder);
                    go.transform.localRotation = Quaternion.Euler(0f, turn, 0f) * prefab.transform.localRotation;
                    go.transform.localScale = prefab.transform.localScale * size;
                    go.transform.localPosition = new Vector3(x, 0f, z);
                    // its bottom on y (house prefabs keep their pivots where they were made)
                    if (TryBounds(go, holder, out var b)) go.transform.localPosition += new Vector3(0f, y - b.min.y + 0.005f, 0f);
                    go.name = Path.GetFileNameWithoutExtension(path) + "_" + (++placed);
                    if (solid) MakeSolid(go, wall);
                    // a free physics body can't ride the PlayGround: a spot that puts it there as the PlayGround comes out
                    if (go.GetComponentsInChildren<Rigidbody>(true).Length > 0) { SpotFor(go, prefab, holder, size); spots++; }
                    if (replaces != null && kit != null && SwitchOff(kit, replaces, new Vector3(x, 0f, z))) off++;
                }
                PrefabUtility.SaveAsPrefabAsset(root, PlayGroundPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            Debug.Log($"{Log} {(reset ? "Reset" : "Place")} Bedroom Models: {placed} models in {PlayGroundPath} ({HolderName}; " +
                      $"{spots} of them breakables put out by a Spawn Prop Here spot), {off} kit piece(s) switched off" +
                      (missing.Count > 0 ? "; MISSING " + string.Join(", ", missing) : "") + ".");
        }

        // The placed copy of a breakable becomes a spot (SpawnPropHere) at its place, turn and size, with a looks-only
        // copy under it for Prefab Mode (tagged EditorOnly, switched off as the game starts); the copy itself goes.
        private static void SpotFor(GameObject go, GameObject prefab, Transform holder, float size)
        {
            var spot = new GameObject(go.name).transform;
            spot.SetParent(holder, false);
            spot.localPosition = go.transform.localPosition;
            spot.localRotation = go.transform.localRotation;
            spot.localScale = Vector3.one * size;
            spot.gameObject.AddComponent<SpawnPropHere>().prefab = prefab;
            var look = new GameObject(SpawnPropHere.LookName).transform;
            look.SetParent(spot, false);
            look.gameObject.tag = "EditorOnly";
            foreach (var r in go.GetComponentsInChildren<Renderer>(false))
            {
                Mesh mesh = r is SkinnedMeshRenderer sk ? sk.sharedMesh : r.TryGetComponent(out MeshFilter mf) ? mf.sharedMesh : null;
                if (mesh == null || !(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
                var part = new GameObject(r.name).transform;
                part.SetParent(look, false);
                part.SetPositionAndRotation(r.transform.position, r.transform.rotation);
                var ls = look.lossyScale;
                var rs = r.transform.lossyScale;
                part.localScale = new Vector3(rs.x / ls.x, rs.y / ls.y, rs.z / ls.z);
                part.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                part.gameObject.AddComponent<MeshRenderer>().sharedMaterials = r.sharedMaterials;
                part.gameObject.tag = "EditorOnly";
            }
            spot.SetSiblingIndex(go.transform.GetSiblingIndex());
            Object.DestroyImmediate(go);
        }

        // The kit piece called piece_N nearest the spot (within 0.6 m: one the user moved is left alone).
        private static bool SwitchOff(Transform kit, string piece, Vector3 at)
        {
            Transform best = null;
            float bestDist = 0.6f;
            foreach (Transform t in kit)
            {
                if (!t.gameObject.activeSelf || !t.name.StartsWith(piece + "_") || t.name.EndsWith(OffTag)) continue;
                float d = Vector2.Distance(new Vector2(t.localPosition.x, t.localPosition.z), new Vector2(at.x, at.z));
                if (d < bestDist) { bestDist = d; best = t; }
            }
            if (best == null) return false;
            best.gameObject.SetActive(false);
            best.name += OffTag;
            return true;
        }

        private static int KitPiecesBackOn(Transform root)
        {
            var kit = root.Find(KitDressingName);
            if (kit == null) return 0;
            int n = 0;
            foreach (Transform t in kit)
            {
                if (!t.name.EndsWith(OffTag)) continue;
                t.name = t.name.Substring(0, t.name.Length - OffTag.Length);
                t.gameObject.SetActive(true);
                n++;
            }
            return n;
        }

        // The model's renderers' box in the holder's space.
        private static bool TryBounds(GameObject go, Transform space, out Bounds b)
        {
            b = default;
            bool any = false;
            // what shows: active renderers only (a breakable's broken pieces wait switched off)
            foreach (var r in go.GetComponentsInChildren<Renderer>(false))
            {
                Mesh mesh = r is SkinnedMeshRenderer sk ? sk.sharedMesh : r.TryGetComponent(out MeshFilter mf) ? mf.sharedMesh : null;
                if (mesh == null || !r.enabled) continue;
                var mb = mesh.bounds;
                var m = space.worldToLocalMatrix * r.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var c = m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    if (!any) { b = new Bounds(c, Vector3.zero); any = true; }
                    else b.Encapsulate(c);
                }
            }
            return any;
        }

        // A box collider on each mesh that has none, and the Wall layer, on the placed copy only.
        private static void MakeSolid(GameObject go, int wall)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                if (wall >= 0) t.gameObject.layer = wall;
                if (t.TryGetComponent(out MeshFilter mf) && mf.sharedMesh != null && !t.TryGetComponent<Collider>(out _))
                    t.gameObject.AddComponent<BoxCollider>();
            }
        }

        private static void Backup(string name)
        {
            string folder = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
                "429R_claude_backups", "2026-11-23", "gate1");
            Directory.CreateDirectory(folder);
            string to = Path.Combine(folder, name);
            if (!File.Exists(to)) File.Copy(PlayGroundPath, to);
        }
    }
}

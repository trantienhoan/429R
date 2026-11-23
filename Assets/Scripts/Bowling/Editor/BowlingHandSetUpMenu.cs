using System.Collections.Generic;
using System.Linq;
using Game.Locomotion;
using UnityEditor;
using UnityEngine;

namespace Game.Bowling.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Bowling > Hand Set Up (gate 2, 2026-11-23; user: "ready it for me to go into unity and set up
    /// things by hand"). Re-runnable menus, never the scene, out of Play only:
    /// - Make One Lane Prefab: the 4 lanes of Prefabs/Bowling/Bowling_Arena become instances of one prefab,
    ///   Prefabs/Bowling/Bowling_Lane (made from lane 0). Open it, change it (floor, rails, pin slots, the kit pieces) and
    ///   all 4 lanes follow; each keeps its turn, its index and its rune ring colour (overrides). Turns Bowling Stage's
    ///   Hand Set Up on (the old builder menus then leave the arena alone) and gives the balls the break sound at fall edges.
    /// - Back To 4 Separate Lanes: undoes it (unpacks the 4 lanes; Hand Set Up off).
    /// - Fit Fall Edges To Lanes: after a lane is made wider or longer, the 12 red Fall Edge boxes are fitted round the
    ///   lanes' floors and the dirt again (a ball reaching one breaks, the player falling in is put back).
    /// - Import SlimeBowl Kit Pieces: after a new export from Blender, the FBX's import settings and the palette material
    ///   again, and a prefab for every NEW piece in Prefabs/Bowling/Kit. Nothing is placed or moved: the pieces already in
    ///   the arena are those prefabs, so they take the new shapes by themselves.
    /// A backup of the arena goes to Exports/Gates/gate2/backups/ before each change.
    /// </summary>
    public static class BowlingHandSetUpMenu
    {
        private const string Log = "[Bowling]";
        private const string Root = "Tools/429 Game/Bowling/Hand Set Up/";
        private const string ArenaPath = "Assets/Prefabs/Bowling/Bowling_Arena.prefab";
        private const string LanePath = "Assets/Prefabs/Bowling/Bowling_Lane.prefab";
        private const string FbxPath = "Assets/Models/Gates/SlimeBowl/SlimeBowl_Kit.fbx";
        private const string PieceFolder = "Assets/Prefabs/Bowling/Kit";
        private const string Prefix = "SlimeBowl_Kit_";
        private const string RingPrefix = "SlimeBowl_Kit_Rune_Ring_";
        private const string BreakSoundPath = "Assets/Audio/SFX/pumpkin_explode_1.wav";
        private const float FarOut = 42.6f;      // the Fall Edges' outer side (X frame), as Set Up Bowling Arena
        private const float TipLength = 20f;

        // ---------------------------------------------------------------- the guard for the old builder menus

        /// <summary>True (and an error in the Console) when the arena is set up by hand: the menu named What must not touch
        /// it.</summary>
        public static bool Refuse(string what)
        {
            var arena = AssetDatabase.LoadAssetAtPath<GameObject>(ArenaPath);
            var stage = arena != null ? arena.GetComponent<BowlingStage>() : null;
            if (stage == null || !stage.HandSetUp) return false;
            Debug.LogError($"{Log} {what}: nothing changed. The arena is set up by hand (Bowling Stage > Hand Set Up is on), " +
                           "and this menu would rebuild it and throw that work away. To change the kit's shapes use Hand Set Up > " +
                           "Import SlimeBowl Kit Pieces. If you really want the old builder, untick Hand Set Up on Bowling_Arena first.");
            return true;
        }

        // ---------------------------------------------------------------- one lane prefab

        [MenuItem(Root + "Make One Lane Prefab (edit 1 lane, all 4 follow)")]
        public static void MakeLanePrefab()
        {
            if (EditorApplication.isPlaying) { Debug.LogError($"{Log} Stop Play mode first; nothing changed."); return; }
            var notes = new List<string>();
            string backup = Backup("before_lane_prefab");
            if (backup != null) notes.Add("backup: " + backup);
            var root = PrefabUtility.LoadPrefabContents(ArenaPath);
            try
            {
                var stage = root.GetComponent<BowlingStage>();
                var so = stage != null ? new SerializedObject(stage) : null;
                var lanesProp = so?.FindProperty("lanes");
                if (lanesProp == null || lanesProp.arraySize == 0)
                {
                    Debug.LogError($"{Log} {ArenaPath} has no Bowling Stage with lanes; nothing changed.");
                    return;
                }
                var old = new BowlingLane[lanesProp.arraySize];
                for (int i = 0; i < old.Length; i++) old[i] = lanesProp.GetArrayElementAtIndex(i).objectReferenceValue as BowlingLane;
                if (old.Any(l => l == null))
                {
                    Debug.LogError($"{Log} A lane is missing on the Bowling Stage; nothing changed.");
                    return;
                }
                if (IsLaneInstance(old[0].gameObject))
                {
                    Debug.Log($"{Log} Make One Lane Prefab: the lanes are already {LanePath} (nothing to do). Open it to change all 4.");
                    return;
                }

                // Lane 0 (straight, at the centre) becomes the Lane prefab, kit pieces as nested prefabs.
                var first = old[0].transform;
                var keepPos = first.localPosition;
                var keepRot = first.localRotation;
                first.localPosition = Vector3.zero;
                first.localRotation = Quaternion.identity;
                var lanePrefab = PrefabUtility.SaveAsPrefabAsset(first.gameObject, LanePath, out bool laneSaved);
                first.localPosition = keepPos;
                first.localRotation = keepRot;
                if (!laneSaved || lanePrefab == null)
                {
                    Debug.LogError($"{Log} Could not save {LanePath}; nothing changed.");
                    return;
                }
                notes.Add("Lane prefab made from lane 0: " + LanePath);

                // An instance of it in each lane's place; each keeps its turn, index and ring colour.
                var made = new BowlingLane[old.Length];
                var map = new Dictionary<Object, Object>();
                for (int k = 0; k < old.Length; k++)
                {
                    var was = old[k].transform;
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(lanePrefab, was.parent);
                    go.name = was.name;
                    go.transform.SetSiblingIndex(was.GetSiblingIndex());
                    go.transform.localPosition = was.localPosition;
                    go.transform.localRotation = was.localRotation;
                    go.transform.localScale = was.localScale;
                    made[k] = go.GetComponent<BowlingLane>();
                    var lso = new SerializedObject(made[k]);
                    lso.FindProperty("index").intValue = old[k].Index;
                    lso.ApplyModifiedPropertiesWithoutUndo();
                    string ring = Ring(was, go.transform);
                    if (ring != null) notes.Add($"lane {k}: ring {ring}");
                    MapInto(was, go.transform, map);
                }

                // Everything else in the arena that pointed into an old lane points into its new one.
                int remapped = 0, lost = 0;
                var oldRoots = old.Select(l => l.transform).ToArray();
                foreach (var c in root.GetComponentsInChildren<Component>(true))
                {
                    // Transforms keep their own child lists (the hierarchy itself): never re-pointed by hand.
                    if (c == null || c is Transform || oldRoots.Any(r => c.transform.IsChildOf(r)) || made.Any(l => c.transform.IsChildOf(l.transform))) continue;
                    var cso = new SerializedObject(c);
                    var it = cso.GetIterator();
                    bool changed = false;
                    while (it.Next(true))
                    {
                        if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue == null) continue;
                        var target = it.objectReferenceValue;
                        var t = target is GameObject g ? g.transform : target is Component tc ? tc.transform : null;
                        if (t == null || !oldRoots.Any(r => t.IsChildOf(r))) continue;
                        if (map.TryGetValue(target, out var now) && now != null) { it.objectReferenceValue = now; remapped++; }
                        else { it.objectReferenceValue = null; lost++; Debug.LogWarning($"{Log} {c.GetType().Name}.{it.propertyPath} on {c.name} pointed at {target.name} in an old lane: not found in the new one (emptied).", c); }
                        changed = true;
                    }
                    if (changed) cso.ApplyModifiedPropertiesWithoutUndo();
                }
                notes.Add($"{remapped} references re-pointed to the new lanes" + (lost > 0 ? $", {lost} LOST (see warnings)" : ""));
                foreach (var l in old) Object.DestroyImmediate(l.gameObject);

                // Hand set up on; the balls break at fall edges with the pumpkin break sound.
                so.Update();
                so.FindProperty("handSetUp").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                var supply = root.GetComponentInChildren<BowlingBallSupply>(true);
                if (supply != null)
                {
                    var sso = new SerializedObject(supply);
                    var p = sso.FindProperty("breakSound");
                    if (p != null && p.objectReferenceValue == null) p.objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(BreakSoundPath);
                    sso.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(root, ArenaPath, out bool saved);
                if (!saved) { Debug.LogError($"{Log} Could not save {ArenaPath}."); return; }
                notes.Add("Bowling Stage > Hand Set Up on: Set Up Bowling Arena and Build / Remove SlimeBowl Kit now refuse to run");
                notes.Add(Sizes(made));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"{Log} Make One Lane Prefab:\n" + string.Join("\n", notes) +
                      "\nTo change all 4 lanes: double-click Prefabs/Bowling/Bowling_Lane (the lane points along +Z from the " +
                      "arena's centre). After a width or length change: Hand Set Up > Fit Fall Edges To Lanes.");
        }

        [MenuItem(Root + "Back To 4 Separate Lanes (undo the lane prefab)")]
        public static void Unpack()
        {
            if (EditorApplication.isPlaying) { Debug.LogError($"{Log} Stop Play mode first; nothing changed."); return; }
            string backup = Backup("before_unpack_lanes");
            var root = PrefabUtility.LoadPrefabContents(ArenaPath);
            int n = 0;
            try
            {
                foreach (var lane in root.GetComponentsInChildren<BowlingLane>(true))
                {
                    if (!IsLaneInstance(lane.gameObject)) continue;
                    PrefabUtility.UnpackPrefabInstance(lane.gameObject, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
                    n++;
                }
                var stage = root.GetComponent<BowlingStage>();
                if (stage != null)
                {
                    var so = new SerializedObject(stage);
                    so.FindProperty("handSetUp").boolValue = false;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(root, ArenaPath, out bool saved);
                if (!saved) { Debug.LogError($"{Log} Could not save {ArenaPath}."); return; }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            Debug.Log($"{Log} Back To 4 Separate Lanes: {n} lanes unpacked (each its own again), Hand Set Up off. " +
                      $"{LanePath} is kept (unused). Backup: {backup}");
        }

        // ---------------------------------------------------------------- fall edges

        [MenuItem(Root + "Fit Fall Edges To Lanes")]
        public static void FitFallEdges()
        {
            if (EditorApplication.isPlaying) { Debug.LogError($"{Log} Stop Play mode first; nothing changed."); return; }
            string backup = Backup("before_fit_fall_edges");
            var root = PrefabUtility.LoadPrefabContents(ArenaPath);
            var notes = new List<string>();
            try
            {
                var x = root.transform.Find("X");
                var stage = root.GetComponent<BowlingStage>();
                if (x == null || stage == null) { Debug.LogError($"{Log} {ArenaPath} has no X child or Bowling Stage; nothing changed."); return; }
                var edges = x.Find("Fall Edges");
                if (edges == null)
                {
                    edges = new GameObject("Fall Edges").transform;
                    edges.SetParent(x, false);
                }
                var lanes = stage.Lanes.Where(l => l != null).ToArray();
                // Sizes in the X frame (lane k along LaneDir(k)): the widest lane, each lane's end, the dirt's half side.
                float w = lanes.Length > 0 ? lanes.Max(l => l.HalfWidth) : BowlingLayout.LaneWidth * 0.5f;
                float d = DirtHalf(x, stage);
                var ends = new float[4];
                for (int k = 0; k < 4; k++)
                {
                    var lane = lanes.FirstOrDefault(l => l.Index == k);
                    ends[k] = lane != null ? lane.EndAlong : BowlingLayout.LaneEnd;
                }
                float far = Mathf.Max(FarOut, ends.Max() + TipLength);
                for (int q = 0; q < 4; q++)
                {
                    var turn = Quaternion.Euler(0f, -90f * q, 0f);
                    bool swap = q % 2 == 1;
                    // A: beside lane q's +X side, from the dirt's edge out; B: beside lane q-1 (along +X in quadrant 0),
                    // between the lane's side and the dirt's edge.
                    var sizeA = new Vector3(far - w, 5f, far - d);
                    var sizeB = new Vector3(far - d, 5f, Mathf.Max(0.01f, d - w));
                    Fit(edges, "Fall Edge A" + q, turn * new Vector3((w + far) * 0.5f, 1.5f, (d + far) * 0.5f), swap ? Swap(sizeA) : sizeA);
                    var b = Fit(edges, "Fall Edge B" + q, turn * new Vector3((d + far) * 0.5f, 1.5f, (w + d) * 0.5f), swap ? Swap(sizeB) : sizeB);
                    b.gameObject.SetActive(d - w > 0.02f);
                }
                for (int k = 0; k < 4; k++)
                {
                    var dir = BowlingLayout.LaneDir(k);
                    var size = Mathf.Abs(dir.z) > 0.5f ? new Vector3(w * 2f, 5f, TipLength) : new Vector3(TipLength, 5f, w * 2f);
                    Fit(edges, "Fall Edge Tip " + k, BowlingLayout.LanePoint(k, ends[k] + TipLength * 0.5f, 0f, 1.5f), size);
                }
                PrefabUtility.SaveAsPrefabAsset(root, ArenaPath, out bool saved);
                if (!saved) { Debug.LogError($"{Log} Could not save {ArenaPath}."); return; }
                notes.Add($"lanes {w * 2f:0.00} m wide (the widest floor), dirt {d * 2f:0.00} m, lane ends {string.Join(" / ", ends.Select(e => e.ToString("0.00")))} m");
                notes.Add("12 Fall Edges fitted: 8 beside the lanes, 4 past their ends" + (d - w <= 0.02f ? " (the B boxes are off: the lanes are as wide as the dirt)" : ""));
                notes.Add("backup: " + backup);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            Debug.Log($"{Log} Fit Fall Edges To Lanes:\n" + string.Join("\n", notes));
        }

        // ---------------------------------------------------------------- kit pieces

        [MenuItem(Root + "Import SlimeBowl Kit Pieces (no placing)")]
        public static void ImportPieces()
        {
            if (EditorApplication.isPlaying) { Debug.LogError($"{Log} Stop Play mode first; nothing changed."); return; }
            var notes = new List<string>();
            AssetDatabase.ImportAsset(FbxPath, ImportAssetOptions.ForceUpdate);
            if (!SlimeBowlKitMenu.Imports(notes)) return;
            var mat = SlimeBowlKitMenu.Material(notes);
            var meshes = AssetDatabase.LoadAllAssetsAtPath(FbxPath).OfType<Mesh>().Where(m => m.name.StartsWith(Prefix)).ToList();
            if (meshes.Count == 0) { Debug.LogError($"{Log} {FbxPath} has no {Prefix}* meshes; nothing changed."); return; }
            if (!AssetDatabase.IsValidFolder(PieceFolder)) AssetDatabase.CreateFolder("Assets/Prefabs/Bowling", "Kit");
            var fresh = new List<string>();
            var fixedUp = new List<string>();
            foreach (var mesh in meshes)
            {
                string path = PieceFolder + "/" + mesh.name + ".prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    var go = new GameObject(mesh.name);
                    try
                    {
                        go.AddComponent<MeshFilter>().sharedMesh = mesh;
                        var r = go.AddComponent<MeshRenderer>();
                        r.sharedMaterial = mat;
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        r.receiveShadows = false;
                        if (mesh.name.EndsWith("_Flame")) go.AddComponent<KitFlame>();
                        PrefabUtility.SaveAsPrefabAsset(go, path);
                        fresh.Add(mesh.name.Substring(Prefix.Length));
                    }
                    finally
                    {
                        Object.DestroyImmediate(go);
                    }
                    continue;
                }
                // An existing piece: its mesh is the FBX's (it updates by itself); fixed only if it lost it.
                var filter = prefab.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh == mesh) continue;
                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var f = contents.GetComponent<MeshFilter>();
                    if (f == null) f = contents.AddComponent<MeshFilter>();
                    f.sharedMesh = mesh;
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                    fixedUp.Add(mesh.name.Substring(Prefix.Length));
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }
            var names = new HashSet<string>(meshes.Select(m => m.name));
            var gone = AssetDatabase.FindAssets("t:Prefab", new[] { PieceFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(p => System.IO.Path.GetFileNameWithoutExtension(p))
                .Where(n => n.StartsWith(Prefix) && !names.Contains(n))
                .Select(n => n.Substring(Prefix.Length)).ToList();
            AssetDatabase.SaveAssets();
            notes.Add($"{meshes.Count} pieces in the FBX ({meshes.Sum(m => m.triangles.Length / 3)} triangles); the ones in the arena show the new shapes");
            notes.Add(fresh.Count > 0 ? "NEW piece prefabs (drag them in where you want): " + string.Join(", ", fresh) : "no new pieces");
            if (fixedUp.Count > 0) notes.Add("piece prefabs given their mesh again: " + string.Join(", ", fixedUp));
            if (gone.Count > 0) notes.Add("no longer in the FBX (prefabs kept, they show nothing now; delete them by hand if unused): " + string.Join(", ", gone));
            Debug.Log($"{Log} Import SlimeBowl Kit Pieces:\n" + string.Join("\n", notes));
        }

        [MenuItem(Root + "Make One Lane Prefab (edit 1 lane, all 4 follow)", true)]
        [MenuItem(Root + "Back To 4 Separate Lanes (undo the lane prefab)", true)]
        [MenuItem(Root + "Fit Fall Edges To Lanes", true)]
        [MenuItem(Root + "Import SlimeBowl Kit Pieces (no placing)", true)]
        private static bool CanRun() => !EditorApplication.isPlaying;

        // ---------------------------------------------------------------- helpers

        private static bool IsLaneInstance(GameObject go)
        {
            if (!PrefabUtility.IsAnyPrefabInstanceRoot(go)) return false;
            return PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go) == LanePath;
        }

        // The new lane's rune ring takes the old lane's (its mesh and name): the Lane prefab has lane 0's.
        private static string Ring(Transform was, Transform now)
        {
            var oldRing = was.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.name.StartsWith(RingPrefix));
            var newRing = now.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.name.StartsWith(RingPrefix));
            if (oldRing == null || newRing == null || oldRing.sharedMesh == newRing.sharedMesh) return null;
            newRing.sharedMesh = oldRing.sharedMesh;
            newRing.gameObject.name = oldRing.gameObject.name;
            PrefabUtility.RecordPrefabInstancePropertyModifications(newRing);
            PrefabUtility.RecordPrefabInstancePropertyModifications(newRing.gameObject);
            return oldRing.name.Substring(RingPrefix.Length);
        }

        // Old lane object -> the new lane's object at the same place in the hierarchy (child indexes), and their components.
        private static void MapInto(Transform was, Transform now, Dictionary<Object, Object> map)
        {
            map[was.gameObject] = now.gameObject;
            map[was] = now;
            var oldParts = was.GetComponents<Component>();
            var newParts = now.GetComponents<Component>();
            foreach (var part in oldParts)
            {
                if (part == null) continue;
                var type = part.GetType();
                int nth = oldParts.Where(p => p != null && p.GetType() == type).ToList().IndexOf(part);
                var match = newParts.Where(p => p != null && p.GetType() == type).ElementAtOrDefault(nth);
                if (match != null) map[part] = match;
            }
            for (int i = 0; i < was.childCount && i < now.childCount; i++)
                MapInto(was.GetChild(i), now.GetChild(i), map);
        }

        private static string Sizes(BowlingLane[] lanes)
        {
            var parts = lanes.Where(l => l != null).Select(l =>
                $"lane {l.Index}: {l.HalfWidth * 2f:0.00} m wide, {l.StartAlong:0.00}-{l.EndAlong:0.00} m, head pin {l.HeadPinAlong:0.00} m, {l.SlotCount} slots");
            return string.Join("\n", parts);
        }

        private static float DirtHalf(Transform x, BowlingStage stage)
        {
            var dirt = x.Find("Dirt");
            if (dirt != null && dirt.TryGetComponent(out BoxCollider box))
                return Mathf.Abs(box.size.x * dirt.lossyScale.x) * 0.5f;
            var so = new SerializedObject(stage);
            var p = so.FindProperty("padHalfSize");
            return p != null ? p.floatValue : BowlingLayout.DirtHalf;
        }

        private static Transform Fit(Transform parent, string name, Vector3 centre, Vector3 size)
        {
            var t = parent.Find(name);
            if (t == null)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.AddComponent<FallEdge>();   // before sizing: its Reset sizes a fresh box
                go.layer = 2;
                t = go.transform;
            }
            t.localPosition = centre;
            t.localRotation = Quaternion.identity;
            var b = t.GetComponent<BoxCollider>();
            b.center = Vector3.zero;
            b.size = size;
            b.isTrigger = true;
            return t;
        }

        private static Vector3 Swap(Vector3 size) => new(size.z, size.y, size.x);

        // A copy of the arena prefab in Exports/Gates/gate2/backups (outside Assets), named with the time.
        private static string Backup(string why)
        {
            try
            {
                string dir = System.IO.Path.GetFullPath("Exports/Gates/gate2/backups");
                System.IO.Directory.CreateDirectory(dir);
                string to = System.IO.Path.Combine(dir, $"Bowling_Arena_{System.DateTime.Now:yyyy-MM-dd_HHmmss}_{why}.prefab");
                System.IO.File.Copy(System.IO.Path.GetFullPath(ArenaPath), to, true);
                if (System.IO.File.Exists(System.IO.Path.GetFullPath(LanePath)))
                    System.IO.File.Copy(System.IO.Path.GetFullPath(LanePath), to.Replace("Bowling_Arena_", "Bowling_Lane_"), true);
                return to;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"{Log} No backup made ({e.Message}).");
                return null;
            }
        }
    }
}

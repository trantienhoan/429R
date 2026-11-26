using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Sit = Game.EditorTools.LairStoryPlaceMenu.Sit;
using Spot = Game.EditorTools.LairPlaceStorageMenu.Spot;
using Surface = Game.EditorTools.LairPlaceStorageMenu.Surface;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Lair > Set Up Bedroom Lair (user 2026-11-26: "Yes, go" on Exports/Bedroom_Concept/LAIR_PLAN.md).
    /// The bedroom (room 2: laughing pumpkin Pumpkin_2, mini boss the pumpkin-butted spider) as the second pumpkin lair,
    /// the spider's den, with the storage room's lair pieces (our own models and textures). Re-runnable; three parts:
    /// 1. Prefabs (Prefabs/Lair): aged-wood copies of the bedroom's furniture (Lair_Work_Table, Lair_Work_Chair,
    ///    Lair_Bedroom_Shelf_01/_03, Lair_Shelf_Hang/_2, Lair_Bed, Lair_Coffer, Lair_Bed_Stool, + their aged broken
    ///    pieces); the bed the spider took over (a web over the headboard, egg sacs on the pillow, in Lair_Bed);
    ///    Lair_Nest_Big (the room-1 nest half as big again, more eggs).
    /// 2. The room kit's "Big" set (the bedroom's walls, in Prefabs/Bases/Bedroom_Kit_Base): webs in the 4 top corners,
    ///    a web sheet low across the nest corner (north west), egg sacs on threads over the nest and the bed, sac clusters
    ///    and round webs on the walls; the pumpkin vine up the west wall from a bug hole by the nest, more bug holes low
    ///    on the walls, claw scratches on the chained north door and the locked west door.
    /// 3. The scene's bedroom sets (--Bedroom--, --Bedroom_Chaos-- under -GAMESTAGES): their furniture points make the
    ///    aged copies, the west clover banner is the torn one, the broom moves out of the nest corner, and new points make
    ///    the big nest (north west), the jar huddle (by the coffer), and in the plain set three glow jars (on top of the west
    ///    wardrobe and the south-east shelf, and on the coffer) and the green candle on the work table (those four wait 1.5 s and spawn last, so what
    ///    they stand on is there first). The sets and their points stay OFF; new points join the set's Staggered Spawn.
    /// Back As Before puts the scene and the walls back (the prefabs are kept). SAVE 2 Game Scene after either.
    /// </summary>
    public static class LairBedroomMenu
    {
        private const string Tag = "[Lair Bedroom]";
        private const string LairFolder = "Assets/Prefabs/Lair/";
        private const string BedroomFolder = "Assets/Prefabs/BedRoom/";
        private const string PlainSet = "--Bedroom--";
        private const string MessySet = "--Bedroom_Chaos--";
        private const string KitSet = "Big";
        private const string PointSuffix = "_Pos";
        private const float Inner = LairPlaceStorageMenu.Inner;
        private const float Top = LairPlaceStorageMenu.Top;

        private static readonly (string source, string copy)[] Aged =
        {
            ("Bedroom_Work_Table", "Lair_Work_Table"),
            ("Bedroom_Work_Table_Chair", "Lair_Work_Chair"),
            ("Bedroom_Shelf_01", "Lair_Bedroom_Shelf_01"),
            ("Bedroom_Shelf_03", "Lair_Bedroom_Shelf_03"),
            ("Shelf_Hang_Wood", "Lair_Shelf_Hang"),
            ("Shelf_Hang_Wood_2", "Lair_Shelf_Hang_2"),
            ("Bedroom_Bed", "Lair_Bed"),
            ("Bedroom_Coffer", "Lair_Coffer"),
            ("Bedroom_Bed_Stool", "Lair_Bed_Stool"),
        };

        private static readonly Vector2 AnyWhere = new Vector2(float.NaN, float.NaN);

        // Points that make something else now (both sets unless a set is named; the first that fits a point wins).
        private static IEnumerable<(string set, string from, string to, Vector2 at)> Swaps()
        {
            foreach (var (source, copy) in Aged)
            {
                yield return (PlainSet, source, copy, AnyWhere);
                yield return (MessySet, source, copy, AnyWhere);
            }
            // One clover banner torn (the west one).
            yield return (PlainSet, "Banner_Hang", "Lair_Torn_Banner", new Vector2(-3.7f, 0.3f));
            yield return (MessySet, "Banner_Hang", "Lair_Torn_Banner", new Vector2(-3.7f, 0.5f));
        }

        // Points moved: the broom out of the nest corner (plain set).
        private static readonly (string set, string made, Vector3 was, Vector3 now)[] Moves =
        {
            (PlainSet, "Hammer_Broom", new Vector3(-3.3f, 0f, 3.2f), new Vector3(-3.3f, 0f, 1.75f)),
        };

        // New points: prefab, also in the messy set?, where (x, z and the floor / top height), turn, how it sits,
        // waits for what it stands on (and spawns last).
        private static readonly (string prefab, bool messyToo, Vector3 at, float turn, Sit sit, bool waits)[] Adds =
        {
            ("Lair_Nest_Big", true, new Vector3(-2.85f, 0f, 3.05f), 135f, Sit.Floor, false),
            ("Lair_Jar_Huddle", true, new Vector3(-2.5f, 0f, -0.15f), 90f, Sit.Floor, false),
            // (on the hanging shelves they fell off: Play test 2026-11-26) on top of the west wardrobe (Bedroom_Shelf_01)
            // and of the south-east shelf beside its bottles
            ("Lair_Glow_Jar", false, new Vector3(-3.2f, 2.28f, -1.4f), 20f, Sit.OnTop, true),
            ("Lair_Glow_Jar", false, new Vector3(3.4f, 1.8f, -2.74f), 60f, Sit.OnTop, true),
            ("Lair_Glow_Jar", false, new Vector3(-3.42f, 0.6f, 0.22f), 0f, Sit.OnTop, true),
            ("Lair_Candle_Green", false, new Vector3(1.3f, 0.71f, -3.05f), 0f, Sit.OnTop, true),
        };

        // The bedroom's walls (kit set Big: inner faces +-3.70; north door x -3..0, west door z -3.75..-0.75).
        private static IEnumerable<Spot> Spots()
        {
            yield return LairPlaceStorageMenu.Corner(-Inner, Inner, 1.3f);     // the nest corner: the biggest
            yield return LairPlaceStorageMenu.Corner(Inner, Inner, 1.15f);     // over the bed
            yield return LairPlaceStorageMenu.Corner(Inner, -Inner, 1.05f);
            yield return LairPlaceStorageMenu.Corner(-Inner, -Inner, 0.95f);
            var se = new Vector3(1f, 0f, -1f).normalized;
            yield return new Spot("Lair_Web_Sheet", "BR_Kit_Corner", new Vector3(-Inner, 1.95f, Inner) + se * 0.8f, se, 1.25f);
            yield return new Spot("Lair_Web_Strand_Sac", "Roof", new Vector3(-3.15f, Top, 2.95f), Vector3.forward, 1.2f);
            yield return new Spot("Lair_Web_Strand_Sac", "Roof", new Vector3(-2.65f, Top, 3.3f), Vector3.right, 1.0f);
            yield return new Spot("Lair_Web_Strand_Sac", "Roof", new Vector3(-3.35f, Top, 2.45f), Vector3.left, 0.9f);
            yield return new Spot("Lair_Web_Strand_Sac", "Roof", new Vector3(2.15f, Top, 3.1f), Vector3.back, 1.1f);
            yield return new Spot("Lair_Web_Strand_Sac", "Roof", new Vector3(2.9f, Top, 2.45f), Vector3.forward, 0.85f);
            yield return new Spot("Lair_Egg_Sac_Cluster", "BR_Kit_Wall_0p75m", new Vector3(-3.35f, 2.4f, Inner - 0.03f), Vector3.back, 1.2f);
            yield return new Spot("Lair_Egg_Sac_Cluster", "BR_Kit_Wall_3m", new Vector3(-Inner + 0.03f, 2.35f, 2.45f), Vector3.right, 1.1f);
            yield return new Spot("Lair_Web_Round", "BR_Kit_Wall_3m", new Vector3(2.35f, 2.1f, Inner - 0.03f), Vector3.back, 1.0f);
            yield return new Spot("Lair_Web_Round", "BR_Kit_Wall_3m", new Vector3(-2.6f, 2.2f, -Inner + 0.03f), Vector3.forward, 0.85f);
        }

        private static IEnumerable<Surface> Surfaces()
        {
            // The vine climbs the west wall from a bug hole by the nest.
            yield return new Surface("Lair_Bug_Hole", new Vector3(-3f, 0.12f, 2.17f), Vector3.left, 1.1f, 20f);
            // Bug holes low on the walls, where nothing stands.
            yield return new Surface("Lair_Bug_Hole", new Vector3(0.6f, 0.3f, 3f), Vector3.forward, 0.9f, 200f);
            yield return new Surface("Lair_Bug_Hole", new Vector3(-1.8f, 0.32f, -3f), Vector3.back, 0.85f, 300f);
            yield return new Surface("Lair_Bug_Hole", new Vector3(-3f, 0.4f, 0.6f), Vector3.left, 0.75f, 75f);
            // The growth chart pinned beside the chained north door (its right, on the 3 m wall).
            yield return new Surface("Lair_Growth_Chart", new Vector3(0.35f, 0.95f, 3f), Vector3.forward, 1f);
            // Claw scratches on the chained north door and the locked west door.
            yield return new Surface("Lair_Claw_Scratches", new Vector3(-1.2f, 1.15f, 3f), Vector3.forward, 1f, 15f);
            yield return new Surface("Lair_Claw_Scratches", new Vector3(-1.85f, 0.7f, 3f), Vector3.forward, 0.75f, -10f);
            yield return new Surface("Lair_Claw_Scratches", new Vector3(-3f, 1.0f, -2.0f), Vector3.left, 0.9f, 5f);
        }

        [MenuItem("Tools/429 Game/Lair/Set Up Bedroom Lair")]
        public static void SetUp()
        {
            var done = new List<string>();
            foreach (var (source, copy) in Aged)
                if (LairStoryPropsMenu.AgedCopy(BedroomFolder + source + ".prefab", copy, done) != null) done.Add(copy);
            TakenBed(done);
            BigNest(done);
            GrowthChart(done);
            AssetDatabase.SaveAssets();
            Debug.Log($"{Tag} Prefabs: {string.Join(", ", done)}.");
            LairPlaceStorageMenu.PlaceInSet(KitSet, Spots(), Surfaces(), Tag, "the bedroom");
            PlaceInScene(false);
        }

        [MenuItem("Tools/429 Game/Lair/Set Up Bedroom Lair: Back As Before")]
        public static void BackAsBefore()
        {
            LairPlaceStorageMenu.PlaceInSet(KitSet, new Spot[0], new Surface[0], Tag, "the bedroom");
            PlaceInScene(true);
        }

        [MenuItem("Tools/429 Game/Lair/Set Up Bedroom Lair", true)]
        [MenuItem("Tools/429 Game/Lair/Set Up Bedroom Lair: Back As Before", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

        // The bed the spider took over: a web over the headboard, egg sacs on the pillow (the bed's head is +Z, its
        // headboard 1.22 m high at z 0.88, the pillow's top about 0.77 m at z 0.6).
        private static void TakenBed(List<string> done)
        {
            string path = LairFolder + "Lair_Bed.prefab";
            var web = AssetDatabase.LoadAssetAtPath<GameObject>(LairFolder + "Lair_Web_Sheet.prefab");
            var sac = AssetDatabase.LoadAssetAtPath<GameObject>(LairFolder + "Lair_Egg_Sac.prefab");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null || web == null || sac == null)
            {
                Debug.LogError($"{Tag} Lair_Bed, Lair_Web_Sheet or Lair_Egg_Sac is missing; the bed has no web.");
                return;
            }
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Add(root.transform, web, new Vector3(0f, 1.0f, 0.74f), Quaternion.LookRotation(new Vector3(0f, -0.55f, -1f)), 1.0f);
                // The sac hangs from its top (0.21 m long): its top sits just above the pillow.
                Add(root.transform, sac, new Vector3(-0.27f, 0.95f, 0.6f), Quaternion.Euler(0f, 30f, 8f), 0.9f);
                Add(root.transform, sac, new Vector3(0.04f, 1.0f, 0.66f), Quaternion.Euler(0f, 0f, -6f), 1.15f);
                Add(root.transform, sac, new Vector3(0.3f, 0.93f, 0.55f), Quaternion.Euler(0f, -40f, 10f), 0.8f);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                done.Add("Lair_Bed (web + 3 eggs)");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // The spider's nest: room 1's nest half as big again, with four more eggs in it.
        private static void BigNest(List<string> done)
        {
            var nest = AssetDatabase.LoadAssetAtPath<GameObject>(LairFolder + "Lair_Nest.prefab");
            var sac = AssetDatabase.LoadAssetAtPath<GameObject>(LairFolder + "Lair_Egg_Sac.prefab");
            if (nest == null || sac == null)
            {
                Debug.LogError($"{Tag} Lair_Nest or Lair_Egg_Sac is missing (run Set Up Lair Story Props); no big nest.");
                return;
            }
            var root = new GameObject("Lair_Nest_Big");
            try
            {
                var inner = (GameObject)PrefabUtility.InstantiatePrefab(nest, root.transform);
                inner.transform.localScale = Vector3.one * 1.5f;
                Add(root.transform, sac, new Vector3(-0.35f, 0.33f, 0.18f), Quaternion.Euler(0f, 10f, 4f), 1.3f);
                Add(root.transform, sac, new Vector3(0.0f, 0.3f, -0.2f), Quaternion.Euler(0f, 70f, -5f), 1.15f);
                Add(root.transform, sac, new Vector3(-0.12f, 0.36f, 0.32f), Quaternion.Euler(0f, 150f, 6f), 1.4f);
                Add(root.transform, sac, new Vector3(-0.5f, 0.28f, -0.12f), Quaternion.Euler(0f, 220f, -8f), 1.0f);
                root.AddComponent<Game.Rooms.PurpleHoleFood>();   // looks only: the purple hole eats it when marked
                PrefabUtility.SaveAsPrefabAsset(root, LairFolder + "Lair_Nest_Big.prefab");
                done.Add("Lair_Nest_Big");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // The growth chart (Exports/Lair/paint_growth_chart.py): a 0.2 x 1.6 m strip of old paper, its middle at the
        // prefab's pivot, its front on +Z (what the wall placement turns to face the room), 4 mm off the wall.
        private const string ChartTexture = "Assets/M_Textures/Lair/Lair_Growth_Chart.png";
        private const string ChartMaterial = "Assets/Materials/Lair/Lair_Growth_Chart_Mat.mat";

        private static void GrowthChart(List<string> done)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ChartTexture);
            if (texture == null)
            {
                Debug.LogError($"{Tag} {ChartTexture} is missing (run Exports/Lair/paint_growth_chart.py); no growth chart.");
                return;
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(ChartMaterial);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Lair_Growth_Chart_Mat" };
                AssetDatabase.CreateAsset(material, ChartMaterial);
            }
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0.08f);
            material.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(material);

            var root = new GameObject("Lair_Growth_Chart");
            try
            {
                var paper = GameObject.CreatePrimitive(PrimitiveType.Quad);
                paper.name = "Paper";
                Object.DestroyImmediate(paper.GetComponent<Collider>());
                paper.transform.SetParent(root.transform, false);
                paper.transform.localPosition = new Vector3(0f, 0f, 0.004f);
                paper.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // the quad shows its -Z side
                paper.transform.localScale = new Vector3(0.2f, 1.6f, 1f);
                var look = paper.GetComponent<MeshRenderer>();
                look.sharedMaterial = material;
                look.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                PrefabUtility.SaveAsPrefabAsset(root, LairFolder + "Lair_Growth_Chart.prefab");
                done.Add("Lair_Growth_Chart");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void Add(Transform parent, GameObject prefab, Vector3 at, Quaternion turn, float size)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.localPosition = at;
            go.transform.localRotation = turn;
            go.transform.localScale = Vector3.one * size;
        }

        private static bool IsOurs(GameObject go) => go.name.StartsWith("Lair_") && go.name.EndsWith(PointSuffix);

        // Another point in the list (before this one) makes the same prefab within 5 cm of it.
        private static bool IsDuplicate(GameObject point, List<GameObject> list, Transform set)
        {
            var pm = point.GetComponent<PlayMakerFSM>();
            if (pm == null) return false;
            string made = LairStoryPlaceMenu.Made(pm);
            foreach (var other in list)
            {
                if (other == null) continue;
                if (other == point) return false;   // only ones before it count
                var opm = other.GetComponent<PlayMakerFSM>();
                if (opm == null || LairStoryPlaceMenu.Made(opm) != made) continue;
                if (Vector3.Distance(other.transform.position, point.transform.position) < 0.05f) return true;
            }
            return false;
        }

        private static void PlaceInScene(bool back)
        {
            var changes = new List<string>();
            foreach (var setName in new[] { PlainSet, MessySet })
            {
                var set = LairStoryPlaceMenu.FindSceneObject(setName);
                var stagger = set != null ? set.GetComponent<Game.Rooms.StaggeredSpawn>() : null;
                if (set == null || stagger == null)
                {
                    Debug.LogWarning($"{Tag} No '{setName}' with a Staggered Spawn in the open scene (skipped).");
                    continue;
                }
                var so = new SerializedObject(stagger);
                var points = so.FindProperty("points");
                var keep = new List<GameObject>();
                for (int i = 0; i < points.arraySize; i++)
                {
                    var p = points.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                    if (p != null && !IsOurs(p)) keep.Add(p);
                }

                var old = new List<GameObject>();
                foreach (Transform child in set.transform)
                    if (IsOurs(child.gameObject)) old.Add(child.gameObject);
                foreach (var o in old) Undo.DestroyObjectImmediate(o);
                if (old.Count > 0) changes.Add($"{setName}: {old.Count} earlier points removed");

                foreach (var (swapSet, from, to, at) in Swaps())
                {
                    if (swapSet != setName) continue;
                    string had = back ? to : from;
                    var make = back ? LairStoryPlaceMenu.FindPrefab(from) : AssetDatabase.LoadAssetAtPath<GameObject>(LairFolder + to + ".prefab");
                    if (make == null)
                    {
                        Debug.LogError($"{Tag} No prefab {(back ? from : to)}; {setName} {had} left.");
                        continue;
                    }
                    foreach (var fsm in set.GetComponentsInChildren<PlayMakerFSM>(true))
                    {
                        if (fsm.gameObject == set || IsOurs(fsm.gameObject) || LairStoryPlaceMenu.Made(fsm) != had) continue;
                        var spot = set.transform.InverseTransformPoint(fsm.transform.position);
                        if (!float.IsNaN(at.x) && Vector2.Distance(new Vector2(spot.x, spot.z), at) > 0.1f) continue;
                        LairStoryPlaceMenu.SetMade(fsm, make, null);
                        changes.Add($"{setName}/{fsm.name}: {had} -> {make.name}");
                    }
                }

                foreach (var (moveSet, made, was, now) in Moves)
                {
                    if (moveSet != setName) continue;
                    var from = back ? now : was;
                    var to = back ? was : now;
                    foreach (var fsm in set.GetComponentsInChildren<PlayMakerFSM>(true))
                    {
                        if (fsm.gameObject == set || LairStoryPlaceMenu.Made(fsm) != made) continue;
                        var spot = set.transform.InverseTransformPoint(fsm.transform.position);
                        if (Vector2.Distance(new Vector2(spot.x, spot.z), new Vector2(from.x, from.z)) > 0.15f) continue;
                        Undo.RecordObject(fsm.transform, "Set Up Bedroom Lair");
                        fsm.transform.position = set.transform.TransformPoint(new Vector3(to.x, spot.y, to.z));
                        changes.Add($"{setName}/{fsm.name} ({made}) moved to ({to.x}, {to.z})");
                    }
                }

                var last = new List<GameObject>();
                if (!back)
                {
                    PlayMakerFSM template = null;
                    foreach (var fsm in set.GetComponentsInChildren<PlayMakerFSM>(true))
                    {
                        if (fsm.gameObject == set || IsOurs(fsm.gameObject)) continue;
                        var made = LairStoryPlaceMenu.Made(fsm);
                        if (made == "Lair_Torn_Banner" || made == "Banner_Hang") { template = fsm; break; }
                    }
                    if (template == null) Debug.LogError($"{Tag} {setName} has no banner point to copy; no new points there.");
                    else
                    {
                        foreach (var (prefabName, messyToo, at, turn, sit, waits) in Adds)
                        {
                            if (setName == MessySet && !messyToo) continue;
                            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LairFolder + prefabName + ".prefab");
                            if (prefab == null)
                            {
                                Debug.LogError($"{Tag} {LairFolder}{prefabName}.prefab is missing.");
                                continue;
                            }
                            var rotation = Quaternion.Euler(0f, turn, 0f);
                            var point = Object.Instantiate(template.gameObject, set.transform);
                            point.name = prefabName + PointSuffix;
                            point.SetActive(false);
                            point.transform.SetPositionAndRotation(set.transform.TransformPoint(LairStoryPlaceMenu.Seat(prefab, at, rotation, sit)),
                                set.transform.rotation * rotation);
                            Undo.RegisterCreatedObjectUndo(point, "Set Up Bedroom Lair");
                            var pm = point.GetComponent<PlayMakerFSM>();
                            LairStoryPlaceMenu.SetMade(pm, prefab, point);
                            if (waits) LairStoryPlaceMenu.AddWait(pm);
                            (waits ? last : keep).Add(point);
                            changes.Add($"{setName}/{point.name} at {at}{(waits ? " (waits, last)" : "")}");
                        }
                    }
                }

                if (set.activeSelf)
                {
                    Undo.RecordObject(set, "Set Up Bedroom Lair");
                    set.SetActive(false);
                    changes.Add($"{setName} switched off");
                }
                // A point left off the list that makes the same thing at the same spot as one on it (the plain set has a
                // second hanging shelf "Shelf_Hang_2_Pos (1)" on top of the first) stays off: it would make it twice.
                var twice = keep.FindAll(p => p != null && IsDuplicate(p, keep, set.transform));
                foreach (var p in twice)
                {
                    keep.Remove(p);
                    changes.Add($"{setName}/{p.name} taken off the Staggered Spawn list (same thing at the same spot)");
                }
                int pointsOff = 0;
                foreach (Transform child in set.transform)
                {
                    var pm = child.GetComponent<PlayMakerFSM>();
                    if (pm == null) continue;
                    if (child.gameObject.activeSelf)
                    {
                        Undo.RecordObject(child.gameObject, "Set Up Bedroom Lair");
                        child.gameObject.SetActive(false);
                        pointsOff++;
                    }
                    if (!keep.Contains(child.gameObject) && !last.Contains(child.gameObject) && LairStoryPlaceMenu.Made(pm) != null
                        && !IsDuplicate(child.gameObject, keep, set.transform))
                    {
                        keep.Add(child.gameObject);
                        changes.Add($"{setName}/{child.name} added to the Staggered Spawn list");
                    }
                }
                if (pointsOff > 0) changes.Add($"{setName}: {pointsOff} points switched off");
                keep.AddRange(last);

                points.arraySize = keep.Count;
                for (int i = 0; i < keep.Count; i++) points.GetArrayElementAtIndex(i).objectReferenceValue = keep[i];
                so.ApplyModifiedProperties();
                EditorSceneManager.MarkSceneDirty(set.scene);
            }
            Debug.Log(changes.Count == 0
                ? $"{Tag} Nothing changed in the scene."
                : $"{Tag} {(back ? "Back as before" : "Bedroom lair placed")}: {string.Join("; ", changes)}. SAVE 2 Game Scene.");
        }
    }
}

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
    /// Tools > 429 Game > Lair > Set Up Living Room Lair (user 2026-11-26: "Yes, living room", after the bedroom lair).
    /// The living room (room 3: laughing pumpkin Pumpkin_4, mini boss the big green cockroach, EnemyManager 2) as the
    /// third pumpkin lair, the cockroaches' den, with the lair pieces of rooms 1-2 (our own models and textures).
    /// Re-runnable; three parts, like Set Up Bedroom Lair:
    /// 1. Prefabs (Prefabs/Lair): aged-wood copies of the living room's furniture (Lair_LR_Table_2, Lair_LR_Table_3,
    ///    Lair_Book_Shelf, Lair_LR_Stool, Lair_LR_Hat_Table; the chairs and the hanging shelf reuse Lair_Work_Chair and
    ///    Lair_Shelf_Hang_2); roach egg cases (Lair_Roach_Egg_Mat: the egg sacs' atlas material tinted glossy brown):
    ///    Lair_Roach_Nest (room 1's nest with brown eggs and more of them lying about) and Lair_Roach_Eggs (a small pile).
    /// 2. The room kit's "Living" set (Prefabs/Bases/Livingroom_Kit_Base; inner faces +-4.5, roof 3.0; doors: north
    ///    x 1.5..4.5, east "Door_Big" z 0..3, south x -4.5..-1.5): dusty webs in the 4 top corners and a few bare
    ///    threads, two pumpkin vines (the pumpkin is stronger here: north wall, and east wall behind the candy baskets),
    ///    four bug holes low on the walls, claw scratches on the north and east doors.
    /// 3. The scene's living room sets (--Livingroom--, --Livingroom_Chaos--, and --Livingroom--MagicTable-- for the hat
    ///    table): furniture points make the aged copies, the west table's candle burns green, and new points make the
    ///    roach nest among the north-west barrels, the jar huddle by the west wall, and in the plain set a pile of roach
    ///    eggs by the south door and a glow jar on top of the bookshelf (it waits 1.5 s and spawns last). The sets and
    ///    their points stay OFF; new points join the set's Staggered Spawn; exact duplicates stay off the list.
    /// Back As Before puts the scene and the walls back (the prefabs are kept). SAVE 2 Game Scene after either.
    /// </summary>
    public static class LairLivingroomMenu
    {
        private const string Tag = "[Lair Living Room]";
        private const string LairFolder = "Assets/Prefabs/Lair/";
        private const string BedroomFolder = "Assets/Prefabs/BedRoom/";
        private const string MaterialFolder = "Assets/Materials/Lair/";
        private const string PlainSet = "--Livingroom--";
        private const string MessySet = "--Livingroom_Chaos--";
        private const string MagicSet = "--Livingroom--MagicTable--";
        private const string KitPath = "Assets/Prefabs/Bases/Livingroom_Kit_Base.prefab";
        private const string KitSet = "Living";
        private const string PointSuffix = "_Pos";
        private const float Inner = 4.5f;
        private const float Top = 2.95f;

        private static readonly (string source, string copy)[] Aged =
        {
            ("Livingroom_Table_2", "Lair_LR_Table_2"),
            ("Livingroom_Table_3", "Lair_LR_Table_3"),
            ("Livingroom_Book_Shelf", "Lair_Book_Shelf"),
            ("LivingRoom_Stool", "Lair_LR_Stool"),
            ("Bedroom_Work_Table_Chair", "Lair_Work_Chair"),
            ("Shelf_Hang_Wood_2", "Lair_Shelf_Hang_2"),
            ("Livingroom_Table_2+Magic_Hat", "Lair_LR_Hat_Table"),
        };

        private static readonly Vector2 AnyWhere = new Vector2(float.NaN, float.NaN);

        private static IEnumerable<(string set, string from, string to, Vector2 at)> Swaps()
        {
            // The hanging wall candles burn green, on the wall and once fallen (Candle Items Menu makes the copy).
            yield return (PlainSet, "Candle_Hang", "Lair_Candle_Hang_Green", AnyWhere);
            yield return (MessySet, "Candle_Hang", "Lair_Candle_Hang_Green", AnyWhere);
            foreach (var (source, copy) in Aged)
            {
                if (source.Contains("Magic_Hat")) { yield return (MagicSet, source, copy, AnyWhere); continue; }
                yield return (PlainSet, source, copy, AnyWhere);
                yield return (MessySet, source, copy, AnyWhere);
            }
            yield return (PlainSet, "Candle_Land", "Lair_Candle_Green", AnyWhere);
        }

        private static readonly (string prefab, bool messyToo, Vector3 at, float turn, Sit sit, bool waits)[] Adds =
        {
            ("Lair_Roach_Nest", true, new Vector3(-2.0f, 0f, 3.5f), 200f, Sit.Floor, false),
            ("Lair_Jar_Huddle", true, new Vector3(-3.1f, 0f, 1.15f), 90f, Sit.Floor, false),
            ("Lair_Roach_Eggs", false, new Vector3(-1.6f, 0f, -4.0f), 30f, Sit.Floor, false),
            ("Lair_Glow_Jar", false, new Vector3(3.25f, 2.2f, -3.8f), 20f, Sit.OnTop, true),
        };

        // A web across a corner of this bigger room, on the wall piece nearest that corner.
        private static Spot Corner(float x, float z, float size, string part)
        {
            var into = new Vector3(-Mathf.Sign(x), 0f, -Mathf.Sign(z)).normalized;
            float out_ = 0.884f * size * 0.5f;
            return new Spot("Lair_Web_Corner", part, new Vector3(x, Top, z) + into * out_, into, size);
        }

        private static IEnumerable<Spot> Spots()
        {
            yield return Corner(-Inner, Inner, 1.0f, "LR_Kit_Wall_3m");
            yield return Corner(Inner, Inner, 0.9f, "LR_Kit_Wall_1p5m");
            yield return Corner(Inner, -Inner, 1.1f, "LR_Kit_Wall_1p5m");
            yield return Corner(-Inner, -Inner, 0.85f, "LR_Kit_Wall_3m");
            yield return new Spot("Lair_Web_Strand", "Roof", new Vector3(-3.6f, Top, 3.3f), Vector3.forward, 1.0f);
            yield return new Spot("Lair_Web_Strand", "Roof", new Vector3(3.7f, Top, -3.4f), Vector3.right, 0.9f);
            yield return new Spot("Lair_Web_Strand", "Roof", new Vector3(-3.9f, Top, -3.5f), Vector3.left, 0.8f);
        }

        private static IEnumerable<Surface> Surfaces()
        {
            // Two vines: the pumpkin's power is stronger here.
            yield return new Surface("Lair_Bug_Hole", new Vector3(-0.3f, 0.12f, 4f), Vector3.forward, 1.1f, 20f);
            yield return new Surface("Lair_Bug_Hole", new Vector3(4f, 0.12f, -0.15f), Vector3.right, 1f, 140f);
            // More bug holes low on the walls.
            yield return new Surface("Lair_Bug_Hole", new Vector3(-4f, 0.3f, -3.2f), Vector3.left, 0.9f, 75f);
            yield return new Surface("Lair_Bug_Hole", new Vector3(0.4f, 0.25f, -4f), Vector3.back, 0.85f, 300f);
            // Claw scratches on the north door and the big east door.
            yield return new Surface("Lair_Claw_Scratches", new Vector3(2.6f, 1.1f, 4f), Vector3.forward, 1f, 12f);
            yield return new Surface("Lair_Claw_Scratches", new Vector3(4f, 0.9f, 1.4f), Vector3.right, 1.1f, -8f);
        }

        [MenuItem("Tools/429 Game/Lair/Set Up Living Room Lair")]
        public static void SetUp()
        {
            var done = new List<string>();
            foreach (var (source, copy) in Aged)
            {
                var path = BedroomFolder + source + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    var found = LairStoryPlaceMenu.FindPrefab(source);
                    path = found != null ? AssetDatabase.GetAssetPath(found) : path;
                }
                if (LairStoryPropsMenu.AgedCopy(path, copy, done) != null) done.Add(copy);
            }
            var eggMat = RoachEggMaterial(done);
            RoachNest(eggMat, done);
            RoachEggs(eggMat, done);
            AssetDatabase.SaveAssets();
            Debug.Log($"{Tag} Prefabs: {string.Join(", ", done)}.");
            LairPlaceStorageMenu.PlaceInSet(KitSet, Spots(), Surfaces(), Tag, "the living room", KitPath);
            PlaceInScene(false);
        }

        [MenuItem("Tools/429 Game/Lair/Set Up Living Room Lair: Back As Before")]
        public static void BackAsBefore()
        {
            LairPlaceStorageMenu.PlaceInSet(KitSet, new Spot[0], new Surface[0], Tag, "the living room", KitPath);
            PlaceInScene(true);
        }

        [MenuItem("Tools/429 Game/Lair/Set Up Living Room Lair", true)]
        [MenuItem("Tools/429 Game/Lair/Set Up Living Room Lair: Back As Before", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

        // Roach egg cases: the egg sacs' own material (the furniture atlas' grey patch) tinted a glossy dark brown.
        private static Material RoachEggMaterial(List<string> done)
        {
            var sac = AssetDatabase.LoadAssetAtPath<GameObject>(LairFolder + "Lair_Egg_Sac.prefab");
            var look = sac != null ? sac.GetComponentInChildren<MeshRenderer>() : null;
            if (look == null || look.sharedMaterial == null) return null;
            string path = MaterialFolder + "Lair_Roach_Egg_Mat.mat";
            var egg = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (egg == null)
            {
                egg = new Material(look.sharedMaterial) { name = "Lair_Roach_Egg_Mat" };
                AssetDatabase.CreateAsset(egg, path);
                done.Add("made " + path);
            }
            else egg.CopyPropertiesFromMaterial(look.sharedMaterial);
            egg.SetColor("_BaseColor", new Color(0.55f, 0.34f, 0.18f, 1f));
            if (egg.HasProperty("_Smoothness")) egg.SetFloat("_Smoothness", 0.6f);
            if (egg.HasProperty("_EmissionColor")) egg.SetColor("_EmissionColor", Color.black);
            EditorUtility.SetDirty(egg);
            return egg;
        }

        private static void Brown(GameObject root, Material egg)
        {
            if (egg == null) return;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!r.name.Contains("Egg") && !r.name.Contains("Sac") && !(r.transform.parent != null && r.transform.parent.name.Contains("Egg_Sac"))) continue;
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = egg;
                r.sharedMaterials = slots;
            }
        }

        // The roaches' nest: room 1's nest, its eggs brown, and more egg cases lying about.
        private static void RoachNest(Material egg, List<string> done)
        {
            var nest = AssetDatabase.LoadAssetAtPath<GameObject>(LairFolder + "Lair_Nest.prefab");
            var sac = AssetDatabase.LoadAssetAtPath<GameObject>(LairFolder + "Lair_Egg_Sac.prefab");
            if (nest == null || sac == null) { Debug.LogError($"{Tag} Lair_Nest or Lair_Egg_Sac is missing; no roach nest."); return; }
            var root = new GameObject("Lair_Roach_Nest");
            try
            {
                var inner = (GameObject)PrefabUtility.InstantiatePrefab(nest, root.transform);
                inner.transform.localScale = Vector3.one * 1.15f;
                // Lying egg cases (a sac on its side: its top, the pivot, at one end).
                Add(root.transform, sac, new Vector3(0.45f, 0.06f, 0.3f), Quaternion.Euler(0f, 30f, 90f), 1.0f);
                Add(root.transform, sac, new Vector3(0.6f, 0.06f, 0.05f), Quaternion.Euler(0f, 110f, 90f), 0.9f);
                Add(root.transform, sac, new Vector3(-0.55f, 0.06f, 0.35f), Quaternion.Euler(0f, 200f, 90f), 1.1f);
                Add(root.transform, sac, new Vector3(-0.2f, 0.06f, 0.55f), Quaternion.Euler(0f, 300f, 90f), 0.85f);
                Add(root.transform, sac, new Vector3(0.15f, 0.06f, -0.45f), Quaternion.Euler(0f, 75f, 90f), 1.0f);
                Brown(root, egg);
                root.AddComponent<Game.Rooms.PurpleHoleFood>();   // looks only: the purple hole eats it when marked
                PrefabUtility.SaveAsPrefabAsset(root, LairFolder + "Lair_Roach_Nest.prefab");
                done.Add("Lair_Roach_Nest");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // A small pile of roach egg cases, for a corner or behind furniture.
        private static void RoachEggs(Material egg, List<string> done)
        {
            var sac = AssetDatabase.LoadAssetAtPath<GameObject>(LairFolder + "Lair_Egg_Sac.prefab");
            if (sac == null) return;
            var root = new GameObject("Lair_Roach_Eggs");
            try
            {
                Add(root.transform, sac, new Vector3(0f, 0.06f, 0f), Quaternion.Euler(0f, 0f, 90f), 1.0f);
                Add(root.transform, sac, new Vector3(0.14f, 0.06f, 0.1f), Quaternion.Euler(0f, 60f, 90f), 0.9f);
                Add(root.transform, sac, new Vector3(-0.1f, 0.06f, 0.15f), Quaternion.Euler(0f, 140f, 90f), 0.95f);
                Add(root.transform, sac, new Vector3(0.05f, 0.15f, 0.06f), Quaternion.Euler(20f, 250f, 90f), 0.8f);
                Add(root.transform, sac, new Vector3(-0.16f, 0.06f, -0.08f), Quaternion.Euler(0f, 300f, 90f), 0.85f);
                Brown(root, egg);
                root.AddComponent<Game.Rooms.PurpleHoleFood>();   // looks only: the purple hole eats it when marked
                PrefabUtility.SaveAsPrefabAsset(root, LairFolder + "Lair_Roach_Eggs.prefab");
                done.Add("Lair_Roach_Eggs");
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

        private static bool IsDuplicate(GameObject point, List<GameObject> list)
        {
            var pm = point.GetComponent<PlayMakerFSM>();
            if (pm == null) return false;
            string made = LairStoryPlaceMenu.Made(pm);
            foreach (var other in list)
            {
                if (other == null) continue;
                if (other == point) return false;
                var opm = other.GetComponent<PlayMakerFSM>();
                if (opm == null || LairStoryPlaceMenu.Made(opm) != made) continue;
                if (Vector3.Distance(other.transform.position, point.transform.position) < 0.05f) return true;
            }
            return false;
        }

        private static void SwapIn(GameObject set, string setName, bool back, List<string> changes)
        {
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
        }

        private static void PlaceInScene(bool back)
        {
            var changes = new List<string>();
            var magic = LairStoryPlaceMenu.FindSceneObject(MagicSet);
            if (magic != null)
            {
                SwapIn(magic, MagicSet, back, changes);
                EditorSceneManager.MarkSceneDirty(magic.scene);
            }
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

                SwapIn(set, setName, back, changes);

                var last = new List<GameObject>();
                if (!back)
                {
                    PlayMakerFSM template = null;
                    foreach (var fsm in set.GetComponentsInChildren<PlayMakerFSM>(true))
                    {
                        if (fsm.gameObject == set || IsOurs(fsm.gameObject)) continue;
                        if (LairStoryPlaceMenu.Made(fsm) == "Bedroom_Matt" || LairStoryPlaceMenu.Made(fsm) == "LivingRoom_Matt") { template = fsm; break; }
                    }
                    if (template == null) Debug.LogError($"{Tag} {setName} has no rug point to copy; no new points there.");
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
                            point.transform.localScale = Vector3.one;
                            Undo.RegisterCreatedObjectUndo(point, "Set Up Living Room Lair");
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
                    Undo.RecordObject(set, "Set Up Living Room Lair");
                    set.SetActive(false);
                    changes.Add($"{setName} switched off");
                }
                var twice = keep.FindAll(p => p != null && IsDuplicate(p, keep));
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
                        Undo.RecordObject(child.gameObject, "Set Up Living Room Lair");
                        child.gameObject.SetActive(false);
                        pointsOff++;
                    }
                    if (!keep.Contains(child.gameObject) && !last.Contains(child.gameObject) && LairStoryPlaceMenu.Made(pm) != null
                        && !IsDuplicate(child.gameObject, keep))
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
                : $"{Tag} {(back ? "Back as before" : "Living room lair placed")}: {string.Join("; ", changes)}. SAVE 2 Game Scene.");
        }
    }
}

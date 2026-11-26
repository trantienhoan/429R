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
    /// Tools > 429 Game > Lair > Set Up Playroom Lair (user 2026-11-26: "Yes, playroom", after the living room lair).
    /// The playroom (room 4, the Jenga room: laughing pumpkin Pumpkin_6, mini boss the pumpkin-butted cockroach,
    /// EnemyManager 3) as the last and strongest pumpkin lair, with the pieces of rooms 1-3 (no new models):
    /// 1. Prefabs: the aged copies it needs already exist (Lair_LR_Table_2, Lair_Stuffs_Shelf); remade here so the menu
    ///    works on its own. Lair_Roach_Nest / Lair_Roach_Eggs come from Set Up Living Room Lair.
    /// 2. Livingroom_Kit_Base set "Play" (the cross room; the kit is scaled 1.5 in this room, so these are kit-local
    ///    numbers: centre square inner faces +-3.73 (5.6 m), arms +-0.73 wide out to 7.07 (10.6 m) with breakable brick
    ///    ends that are left alone): webs in the 4 corners of the centre square, threads, round webs, a pumpkin vine up
    ///    the middle of every side (the pumpkin is strongest here), each from a bug hole, two more bug holes, claw
    ///    scratches.
    /// 3. The scene's playroom sets (--Livingroom_Jenga--, --Livingroom_Jenga Chaos--): the north-west table and the east
    ///    arm's shelves are the aged copies; new points (world metres): two roach nests (north and south walls) and an egg
    ///    pile by the sofa in both sets; in the plain set the jar huddle (south east), and on the north-west table a green
    ///    candle and a glow jar (they wait 1.5 s and spawn last). Sets and points stay OFF; new points join the Staggered
    ///    Spawn; exact duplicates stay off the list.
    /// Back As Before puts the scene and the walls back. SAVE 2 Game Scene after either.
    /// </summary>
    public static class LairPlayroomMenu
    {
        private const string Tag = "[Lair Playroom]";
        private const string LairFolder = "Assets/Prefabs/Lair/";
        private const string BedroomFolder = "Assets/Prefabs/BedRoom/";
        private const string PlainSet = "--Livingroom_Jenga--";
        private const string MessySet = "--Livingroom_Jenga Chaos--";
        private const string MagicSet = "--none--";
        private const string KitPath = "Assets/Prefabs/Bases/Livingroom_Kit_Base.prefab";
        private const string KitSet = "Play";
        private const string PointSuffix = "_Pos";
        private const float Inner = 3.73f;
        private const float Top = 2.88f;   // the playroom roof (Roof_Sub) hangs lower: its underside is at 2.92 kit-local

        private static readonly (string source, string copy)[] Aged =
        {
            ("Livingroom_Table_2", "Lair_LR_Table_2"),
            ("Bedroom_Stuffs_Shelf", "Lair_Stuffs_Shelf"),
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
        }

        private static readonly (string prefab, bool messyToo, Vector3 at, float turn, Sit sit, bool waits)[] Adds =
        {
            ("Lair_Roach_Nest", true, new Vector3(2.6f, 0f, 4.95f), 160f, Sit.Floor, false),
            ("Lair_Roach_Nest", true, new Vector3(-3.0f, 0f, -4.95f), 340f, Sit.Floor, false),
            ("Lair_Roach_Eggs", true, new Vector3(-4.4f, 0f, 1.9f), 60f, Sit.Floor, false),
            ("Lair_Jar_Huddle", false, new Vector3(3.4f, 0f, -4.0f), 200f, Sit.Floor, false),
            ("Lair_Candle_Green", false, new Vector3(-5.25f, 0.6f, 5.0f), 0f, Sit.OnTop, true),   // the table's middle (at 4.75 it tipped off once)
            ("Lair_Glow_Jar", false, new Vector3(-5.25f, 0.6f, 5.4f), 30f, Sit.OnTop, true),
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
            yield return Corner(-Inner, Inner, 1.1f, "LR_Kit_Wall_3m");
            yield return Corner(Inner, Inner, 1.0f, "LR_Kit_Wall_3m");
            yield return Corner(Inner, -Inner, 1.15f, "LR_Kit_Wall_3m");
            yield return Corner(-Inner, -Inner, 0.95f, "LR_Kit_Wall_3m");
            yield return new Spot("Lair_Web_Strand", "Livingroom_Base_Z_Roof_Sub", new Vector3(-3.3f, Top, 3.2f), Vector3.forward, 1.0f);
            yield return new Spot("Lair_Web_Strand", "Livingroom_Base_Z_Roof_Sub", new Vector3(3.3f, Top, -3.3f), Vector3.right, 1.0f);
            yield return new Spot("Lair_Web_Strand", "Livingroom_Base_Z_Roof_Sub", new Vector3(3.2f, Top, 3.4f), Vector3.left, 0.9f);
            yield return new Spot("Lair_Web_Round", "LR_Kit_Wall_3m", new Vector3(-Inner + 0.03f, 2.2f, 2.6f), Vector3.right, 0.9f);
            yield return new Spot("Lair_Web_Round", "LR_Kit_Wall_3m", new Vector3(Inner - 0.03f, 2.2f, -2.6f), Vector3.left, 0.9f);
        }

        // Kit-local (x 1.5 in the room). A vine up the middle of every side of the centre square, each from a bug hole.
        private static IEnumerable<Surface> Surfaces()
        {
            yield return new Surface("Lair_Bug_Hole", new Vector3(-1.88f, 0.08f, 3.2f), Vector3.forward, 1f, 20f);
            yield return new Surface("Lair_Bug_Hole", new Vector3(1.88f, 0.08f, -3.2f), Vector3.back, 1f, 200f);
            yield return new Surface("Lair_Bug_Hole", new Vector3(3.2f, 0.08f, 2.08f), Vector3.right, 1f, 140f);
            yield return new Surface("Lair_Bug_Hole", new Vector3(-3.2f, 0.08f, -2.08f), Vector3.left, 1f, 300f);
            yield return new Surface("Lair_Bug_Hole", new Vector3(-3.2f, 0.2f, 1.6f), Vector3.left, 0.8f, 75f);
            yield return new Surface("Lair_Bug_Hole", new Vector3(3.2f, 0.18f, -1.7f), Vector3.right, 0.8f, 250f);
            yield return new Surface("Lair_Claw_Scratches", new Vector3(2.6f, 1.0f, 3.2f), Vector3.forward, 1f, 12f);
            yield return new Surface("Lair_Claw_Scratches", new Vector3(-2.6f, 0.8f, -3.2f), Vector3.back, 1f, -8f);
        }

        [MenuItem("Tools/429 Game/Lair/Set Up Playroom Lair")]
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
            AssetDatabase.SaveAssets();
            Debug.Log($"{Tag} Prefabs: {string.Join(", ", done)}.");
            LairPlaceStorageMenu.PlaceInSet(KitSet, Spots(), Surfaces(), Tag, "the playroom", KitPath);
            PlaceInScene(false);
        }

        [MenuItem("Tools/429 Game/Lair/Set Up Playroom Lair: Back As Before")]
        public static void BackAsBefore()
        {
            LairPlaceStorageMenu.PlaceInSet(KitSet, new Spot[0], new Surface[0], Tag, "the playroom", KitPath);
            PlaceInScene(true);
        }

        [MenuItem("Tools/429 Game/Lair/Set Up Playroom Lair", true)]
        [MenuItem("Tools/429 Game/Lair/Set Up Playroom Lair: Back As Before", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

        private static void Add(Transform parent, GameObject prefab, Vector3 at, Quaternion turn, float size)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.localPosition = at;
            go.transform.localRotation = turn;
            go.transform.localScale = Vector3.one * size;
        }

        private static bool IsOurs(GameObject go) => go.name.StartsWith("Lair_") && go.name.EndsWith(PointSuffix);

        private static bool ComesWithTheSet(GameObject go) => go.name.StartsWith("Candle_Hand_Pos") || go.name.StartsWith("Jenga_Tower_Pos");

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
                        if (LairStoryPlaceMenu.Made(fsm) == "Wood_Box_Weapon_Main") { template = fsm; break; }
                    }
                    if (template == null) Debug.LogError($"{Tag} {setName} has no weapon crate point to copy; no new points there.");
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
                            Undo.RegisterCreatedObjectUndo(point, "Set Up Playroom Lair");
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
                    Undo.RecordObject(set, "Set Up Playroom Lair");
                    set.SetActive(false);
                    changes.Add($"{setName} switched off");
                }
                var twice = keep.FindAll(p => p != null && IsDuplicate(p, keep));
                foreach (var p in twice)
                {
                    keep.Remove(p);
                    changes.Add($"{setName}/{p.name} taken off the Staggered Spawn list (same thing at the same spot)");
                }
                // Unlike the other rooms, these sets have points that are left switched ON and are not on the Staggered
                // Spawn list: the 4 wall candles (Candle_Hand_Pos*) and the Jenga tower (Jenga_Tower_Pos) come at once
                // with the set. They stay exactly like that; only the points on the list are switched off.
                int pointsOff = 0, restored = 0;
                foreach (Transform child in set.transform)
                {
                    var pm = child.GetComponent<PlayMakerFSM>();
                    if (pm == null) continue;
                    if (ComesWithTheSet(child.gameObject))
                    {
                        if (keep.Remove(child.gameObject)) changes.Add($"{setName}/{child.name} kept off the Staggered Spawn list (it comes with the set)");
                        if (!child.gameObject.activeSelf)
                        {
                            Undo.RecordObject(child.gameObject, "Set Up Playroom Lair");
                            child.gameObject.SetActive(true);
                            restored++;
                        }
                        continue;
                    }
                    if (child.gameObject.activeSelf && (keep.Contains(child.gameObject) || last.Contains(child.gameObject)))
                    {
                        Undo.RecordObject(child.gameObject, "Set Up Playroom Lair");
                        child.gameObject.SetActive(false);
                        pointsOff++;
                    }
                }
                if (pointsOff > 0) changes.Add($"{setName}: {pointsOff} listed points switched off");
                if (restored > 0) changes.Add($"{setName}: {restored} points that come with the set switched back on");
                keep.AddRange(last);

                points.arraySize = keep.Count;
                for (int i = 0; i < keep.Count; i++) points.GetArrayElementAtIndex(i).objectReferenceValue = keep[i];
                so.ApplyModifiedProperties();
                EditorSceneManager.MarkSceneDirty(set.scene);
            }
            Debug.Log(changes.Count == 0
                ? $"{Tag} Nothing changed in the scene."
                : $"{Tag} {(back ? "Back as before" : "Playroom lair placed")}: {string.Join("; ", changes)}. SAVE 2 Game Scene.");
        }
    }
}

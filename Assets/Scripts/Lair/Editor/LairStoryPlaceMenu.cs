using System.Collections.Generic;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Lair > Place Storage Room Story Props (user, 2026-11-25, the storage-room lair plan
    /// Exports/Lair_Concept/storage_plan_now_vs_concept.png, "Yes, go"). Puts the story props of Set Up Lair Story Props
    /// into the storage room's furniture sets (--MiniBedroom--, --MiniBedroom_Chaos-- under -GAMESTAGES), as spawn points
    /// like the room's other furniture:
    /// - the wardrobes (MiniBedroom_Shelf_01) are the aged lair ones with iron; in the plain set the one at -1.78 stands
    ///   open with eyes inside (Lair_Wardrobe_Ajar); the table, the shelves, the chairs and the hat table (its root
    ///   --Bedroom--MagicTable-- is the bedroom's too) are their aged-wood copies (Lair_Table_Small, Lair_Stuffs_Shelf,
    ///   Lair_Chair, Lair_Hat_Table);
    /// - both sets and all their points are kept switched OFF in the scene (Staggered Spawn switches them on), and a
    ///   point added by hand joins the set's Staggered Spawn list;
    /// - the clover banner's point makes the torn banner (both sets), the candle on the table burns green (the plain set;
    ///   the messy set's candle is another model and stays); its point now sits just above the table top, in from the
    ///   edge, spawns last and first waits 1.5 s in a new "Wait For Table" state (the candle fell through the table
    ///   while it was still settling, or tipped off its edge, and ended on the floor);
    /// - new points (copies of the banner's point: one Create Object at itself), added to the set's Staggered Spawn:
    ///   the small pumpkins round a glowing jar (north east, open floor), the nest (south east, before the box tower),
    ///   the specimen board (east wall above the north shelf), and in the plain set three glowing jars on the east
    ///   shelves' tops (the messy set has no shelves there).
    /// Re-runnable (earlier "Lair_..._Pos" points are replaced). "Back As Before" removes the new points and gives the
    /// old banner and candle back. Changes the scene: save it after.
    /// </summary>
    public static class LairStoryPlaceMenu
    {
        private const string Tag = "[Lair Storage Room]";
        private const string PlainSet = "--MiniBedroom--";
        private const string MessySet = "--MiniBedroom_Chaos--";
        private const string MagicSet = "--Bedroom--MagicTable--";    // the hat table; the bedroom shows the same one
        private const string LairFolder = "Assets/Prefabs/Lair/";

        private static readonly Vector2 AnyWhere = new Vector2(float.NaN, float.NaN);

        // Points that make something else now: in which set, what they made, what they make, and only the point at
        // this x, z (AnyWhere = every such point). The first that fits a point wins.
        private static readonly (string set, string from, string to, string toFolder, Vector2 at)[] Swaps =
        {
            (PlainSet, "Banner_Hang", "Lair_Torn_Banner", LairFolder, AnyWhere),
            (MessySet, "Banner_Hang", "Lair_Torn_Banner", LairFolder, AnyWhere),
            (PlainSet, "Candle_Land", "Lair_Candle_Green", LairFolder, AnyWhere),
            // The wardrobes (user 2026-11-25 "Aged wood + lair remodel"): the one nearer the room's middle stands open
            // with eyes inside, the others are the aged ones with iron.
            (PlainSet, "MiniBedroom_Shelf_01", "Lair_Wardrobe_Ajar", LairFolder, new Vector2(-1.78f, -3.43f)),
            (PlainSet, "MiniBedroom_Shelf_01", "Lair_Wardrobe", LairFolder, AnyWhere),
            (MessySet, "MiniBedroom_Shelf_01", "Lair_Wardrobe", LairFolder, AnyWhere),
            // The table and the shelves in the same aged wood (user 2026-11-25).
            (PlainSet, "Livingroom_Table_2_small", "Lair_Table_Small", LairFolder, AnyWhere),
            (MessySet, "Livingroom_Table_2_small", "Lair_Table_Small", LairFolder, AnyWhere),
            (PlainSet, "Bedroom_Stuffs_Shelf", "Lair_Stuffs_Shelf", LairFolder, AnyWhere),
            (MessySet, "Bedroom_Stuffs_Shelf", "Lair_Stuffs_Shelf", LairFolder, AnyWhere),
            // The chairs and the hat table too (user 2026-11-25, "help the chair with new material also. this table also").
            (PlainSet, "Livingroom_Chair", "Lair_Chair", LairFolder, AnyWhere),
            (MessySet, "Livingroom_Chair", "Lair_Chair", LairFolder, AnyWhere),
            (MagicSet, "Bedroom_Coffer+Magic_Hats", "Lair_Hat_Table", LairFolder, AnyWhere),
        };

        private const string CandlePoint = "light_pos";
        // The candle's point: where the user had it, and where it is now (lower, and in from the table's edge).
        private static readonly Vector3 CandleWas = new Vector3(1.45f, 0.98f, 3.53f), CandleNow = new Vector3(1.3f, 0.66f, 3.5f);
        private const string CandleWait = "Wait For Table";
        private const float CandleWaitSeconds = 1.5f;

        internal enum Sit { Floor, OnTop, WallMiddle }

        // New points: the prefab, also in the messy set?, where (x, z and the height of the floor, shelf top or the
        // board's middle), its turn, and how it sits there.
        private static readonly (string prefab, bool messyToo, Vector3 at, float turn, Sit sit)[] Adds =
        {
            ("Lair_Jar_Huddle", true, new Vector3(1.55f, 0f, 2.4f), 30f, Sit.Floor),
            // (The nest is out: user 2026-11-26, "remove claw mark and nest and pumpkin ruler".)
            ("Lair_Specimen_Board", true, new Vector3(3.74f, 1.95f, 0.95f), 270f, Sit.WallMiddle),
            ("Lair_Glow_Jar", false, new Vector3(3.47f, 1.15f, 0.72f), 0f, Sit.OnTop),
            ("Lair_Glow_Jar", false, new Vector3(3.5f, 1.15f, 1.18f), 40f, Sit.OnTop),
            ("Lair_Glow_Jar", false, new Vector3(3.48f, 1.15f, -0.55f), 80f, Sit.OnTop),
        };

        [MenuItem("Tools/429 Game/Lair/Place Storage Room Story Props")]
        public static void Place() => Run(false);

        [MenuItem("Tools/429 Game/Lair/Place Storage Room Story Props: Back As Before")]
        public static void BackAsBefore() => Run(true);

        [MenuItem("Tools/429 Game/Lair/Place Storage Room Story Props", true)]
        [MenuItem("Tools/429 Game/Lair/Place Storage Room Story Props: Back As Before", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

        private static void Run(bool back)
        {
            var changes = new List<string>();
            SwapOnly(MagicSet, back, changes);
            foreach (var setName in new[] { PlainSet, MessySet })
            {
                var set = FindSceneObject(setName);
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

                // Earlier new points go.
                var old = new List<GameObject>();
                foreach (Transform child in set.transform)
                    if (IsOurs(child.gameObject)) old.Add(child.gameObject);
                foreach (var o in old) Undo.DestroyObjectImmediate(o);
                if (old.Count > 0) changes.Add($"{setName}: {old.Count} earlier points removed");

                // The swapped points.
                foreach (var (swapSet, from, to, toFolder, at) in Swaps)
                {
                    if (swapSet != setName) continue;
                    string had = back ? to : from;
                    var make = back ? FindPrefab(from) : AssetDatabase.LoadAssetAtPath<GameObject>(toFolder + to + ".prefab");
                    if (make == null)
                    {
                        Debug.LogError($"{Tag} No prefab {(back ? from : toFolder + to)}; {setName} {had} left.");
                        continue;
                    }
                    foreach (var fsm in set.GetComponentsInChildren<PlayMakerFSM>(true))
                    {
                        if (fsm.gameObject == set || Made(fsm) != had) continue;
                        var spot = set.transform.InverseTransformPoint(fsm.transform.position);
                        if (!float.IsNaN(at.x) && Vector2.Distance(new Vector2(spot.x, spot.z), at) > 0.05f) continue;
                        SetMade(fsm, make, null);
                        changes.Add($"{setName}/{fsm.name}: {had} -> {make.name}");
                    }
                }

                // The candle's point sat 0.39 m above the table top (0.58 m) right at its edge: the candle dropped, tipped
                // and often ended on the floor (Play pictures 2026-11-25). Now just above the top and 15 cm in from the
                // edge; back as before puts it where it was.
                if (setName == PlainSet)
                {
                    foreach (Transform child in set.transform)
                    {
                        if (child.name != CandlePoint) continue;
                        // It waits a moment before it makes the candle: the table under it is only solid once it has
                        // settled (Play test: a candle made with the table fell through it to the floor; one put on the
                        // table 5 s later stayed at 0.59-0.61 m).
                        var candleFsm = child.GetComponent<PlayMakerFSM>();
                        if (candleFsm != null && (back ? RemoveWait(candleFsm) : AddWait(candleFsm)))
                            changes.Add($"{setName}/{child.name}: '{CandleWait}' state {(back ? "removed" : $"({CandleWaitSeconds} s) added first")}");
                        var from = set.transform.TransformPoint(back ? CandleNow : CandleWas);
                        var to = set.transform.TransformPoint(back ? CandleWas : CandleNow);
                        if (Vector3.Distance(child.position, from) > 0.02f) continue;
                        Undo.RecordObject(child, "Place Storage Room Story Props");
                        child.position = to;
                        changes.Add($"{setName}/{child.name} moved to {(back ? CandleWas : CandleNow)}");
                    }
                }

                if (!back)
                {
                    // The template: the banner's point (one Create Object at itself).
                    PlayMakerFSM template = null;
                    foreach (var fsm in set.GetComponentsInChildren<PlayMakerFSM>(true))
                    {
                        if (fsm.gameObject == set || IsOurs(fsm.gameObject)) continue;
                        var made = Made(fsm);
                        if (made == "Lair_Torn_Banner" || made == "Banner_Hang") { template = fsm; break; }
                    }
                    if (template == null)
                    {
                        Debug.LogError($"{Tag} {setName} has no banner point to copy; no new points there.");
                    }
                    else
                    {
                        foreach (var (prefabName, messyToo, at, turn, sit) in Adds)
                        {
                            if (setName == MessySet && !messyToo) continue;
                            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LairFolder + prefabName + ".prefab");
                            if (prefab == null)
                            {
                                Debug.LogError($"{Tag} {LairFolder}{prefabName}.prefab is missing (run Tools > 429 Game > Lair > Set Up Lair Story Props).");
                                continue;
                            }
                            var rotation = Quaternion.Euler(0f, turn, 0f);
                            var point = Object.Instantiate(template.gameObject, set.transform);
                            point.name = prefabName + "_Pos";
                            point.SetActive(false);
                            point.transform.SetPositionAndRotation(set.transform.TransformPoint(Seat(prefab, at, rotation, sit)),
                                set.transform.rotation * rotation);
                            Undo.RegisterCreatedObjectUndo(point, "Place Storage Room Story Props");
                            SetMade(point.GetComponent<PlayMakerFSM>(), prefab, point);
                            keep.Add(point);
                            changes.Add($"{setName}/{point.name} at {at}");
                        }
                    }
                }

                // The set and its points stay switched OFF in the scene: GAMESTAGES switches the set on and its Staggered
                // Spawn switches the points on a couple a frame. Switched on in the scene (found 2026-11-25, a preview
                // setup), the room was made twice: at scene load and again at the room (user: "yes, fix it"). A point
                // added by hand (a copy of another) joins the list.
                if (set.activeSelf)
                {
                    Undo.RecordObject(set, "Place Storage Room Story Props");
                    set.SetActive(false);
                    changes.Add($"{setName} switched off");
                }
                int pointsOff = 0;
                foreach (Transform child in set.transform)
                {
                    var pm = child.GetComponent<PlayMakerFSM>();
                    if (pm == null) continue;
                    if (child.gameObject.activeSelf)
                    {
                        Undo.RecordObject(child.gameObject, "Place Storage Room Story Props");
                        child.gameObject.SetActive(false);
                        pointsOff++;
                    }
                    if (!keep.Contains(child.gameObject) && Made(pm) != null)
                    {
                        keep.Add(child.gameObject);
                        changes.Add($"{setName}/{child.name} added to the Staggered Spawn list");
                    }
                }
                if (pointsOff > 0) changes.Add($"{setName}: {pointsOff} points switched off");

                // The candle's point makes its candle after the table is there (Staggered Spawn goes down the list a couple
                // a frame; the candle came before the table and fell to the floor).
                if (!back && setName == PlainSet)
                {
                    int candle = keep.FindIndex(p => p != null && p.name == CandlePoint);
                    if (candle >= 0 && candle != keep.Count - 1)
                    {
                        var point = keep[candle];
                        keep.RemoveAt(candle);
                        keep.Add(point);
                        changes.Add($"{setName}/{CandlePoint} spawns last");
                    }
                }

                points.arraySize = keep.Count;
                for (int i = 0; i < keep.Count; i++) points.GetArrayElementAtIndex(i).objectReferenceValue = keep[i];
                so.ApplyModifiedProperties();
                EditorSceneManager.MarkSceneDirty(set.scene);
            }
            Debug.Log(changes.Count == 0
                ? $"{Tag} Nothing changed."
                : $"{Tag} {(back ? "Back as before" : "Story props placed")}: {string.Join("; ", changes)}. SAVE 2 Game Scene.");
        }

        // Swaps in a root that is not a furniture set with its own Staggered Spawn (the hat table's).
        private static void SwapOnly(string rootName, bool back, List<string> changes)
        {
            var root = FindSceneObject(rootName);
            if (root == null)
            {
                Debug.LogWarning($"{Tag} No '{rootName}' in the open scene (skipped).");
                return;
            }
            foreach (var (swapSet, from, to, toFolder, at) in Swaps)
            {
                if (swapSet != rootName) continue;
                string had = back ? to : from;
                var make = back ? FindPrefab(from) : AssetDatabase.LoadAssetAtPath<GameObject>(toFolder + to + ".prefab");
                if (make == null)
                {
                    Debug.LogError($"{Tag} No prefab {(back ? from : toFolder + to)}; {rootName} {had} left.");
                    continue;
                }
                foreach (var fsm in root.GetComponentsInChildren<PlayMakerFSM>(true))
                {
                    if (fsm.gameObject == root || Made(fsm) != had) continue;
                    var spot = root.transform.InverseTransformPoint(fsm.transform.position);
                    if (!float.IsNaN(at.x) && Vector2.Distance(new Vector2(spot.x, spot.z), at) > 0.05f) continue;
                    SetMade(fsm, make, null);
                    changes.Add($"{rootName}/{fsm.name}: {had} -> {make.name}");
                }
            }
            EditorSceneManager.MarkSceneDirty(root.scene);
        }

        private static bool IsOurs(GameObject go) => go.name.StartsWith("Lair_") && go.name.EndsWith("_Pos");

        // Where the point goes so the prefab sits right: on the floor as built, its bottom on a shelf top, or its middle
        // at the given height (a board on the wall).
        internal static Vector3 Seat(GameObject prefab, Vector3 at, Quaternion rotation, Sit sit)
        {
            if (sit == Sit.Floor) return at;
            var probe = Object.Instantiate(prefab, Vector3.zero, rotation);
            probe.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                bool any = false;
                var bounds = new Bounds();
                foreach (var r in probe.GetComponentsInChildren<Renderer>())
                {
                    if (r is ParticleSystemRenderer) continue;
                    if (!any) { bounds = r.bounds; any = true; }
                    else bounds.Encapsulate(r.bounds);
                }
                if (!any) return at;
                float lift = sit == Sit.OnTop ? -bounds.min.y + 0.004f : -bounds.center.y;
                return new Vector3(at.x, at.y + lift, at.z);
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }
        }

        // The prefab a point's FSM makes (its first Create Object), or null.
        internal static string Made(PlayMakerFSM fsm)
        {
            if (!fsm.Fsm.Initialized) fsm.Fsm.Init(fsm);
            foreach (var state in fsm.Fsm.States)
            {
                state.LoadActions();
                foreach (var action in state.Actions)
                    if (action is CreateObject create && create.gameObject != null && create.gameObject.Value != null)
                        return create.gameObject.Value.name;
            }
            return null;
        }

        // Every Create Object of the point makes this prefab (at the point itself, when given).
        internal static void SetMade(PlayMakerFSM fsm, GameObject prefab, GameObject at)
        {
            if (fsm == null) return;
            Undo.RecordObject(fsm, "Place Storage Room Story Props");
            if (!fsm.Fsm.Initialized) fsm.Fsm.Init(fsm);
            foreach (var state in fsm.Fsm.States)
            {
                state.LoadActions();
                bool changed = false;
                foreach (var action in state.Actions)
                {
                    if (!(action is CreateObject create)) continue;
                    create.gameObject.Value = prefab;
                    if (at != null) create.spawnPoint.Value = at;
                    changed = true;
                }
                if (changed) state.SaveActions();
            }
            EditorUtility.SetDirty(fsm);
        }

        // A first state that waits, then goes on (FINISHED) to the point's old first state.
        internal static bool AddWait(PlayMakerFSM pm)
        {
            var f = pm.Fsm;
            if (!f.Initialized) f.Init(pm);
            if (f.GetState(CandleWait) != null) return false;
            var first = f.GetState(f.StartState);
            if (first == null) return false;
            foreach (var st in f.States) st.LoadActions();
            Undo.RecordObject(pm, "Place Storage Room Story Props");
            var wait = new Wait { time = CandleWaitSeconds, finishEvent = HutongGames.PlayMaker.FsmEvent.Finished, realTime = false };
            var state = new HutongGames.PlayMaker.FsmState(f)
            {
                Name = CandleWait,
                Position = new Rect(first.Position.x, first.Position.y - 100f, first.Position.width, first.Position.height),
                Actions = new HutongGames.PlayMaker.FsmStateAction[] { wait },
                Transitions = new[] { new HutongGames.PlayMaker.FsmTransition { FsmEvent = HutongGames.PlayMaker.FsmEvent.Finished, ToState = first.Name } },
            };
            var states = new List<HutongGames.PlayMaker.FsmState>(f.States) { state };
            f.States = states.ToArray();
            f.StartState = CandleWait;
            foreach (var st in f.States) st.SaveActions();
            EditorUtility.SetDirty(pm);
            return true;
        }

        private static bool RemoveWait(PlayMakerFSM pm)
        {
            var f = pm.Fsm;
            if (!f.Initialized) f.Init(pm);
            var state = f.GetState(CandleWait);
            if (state == null) return false;
            string next = state.Transitions.Length > 0 ? state.Transitions[0].ToState : null;
            foreach (var st in f.States) st.LoadActions();
            Undo.RecordObject(pm, "Place Storage Room Story Props: Back As Before");
            var states = new List<HutongGames.PlayMaker.FsmState>(f.States);
            states.Remove(state);
            f.States = states.ToArray();
            if (f.StartState == CandleWait && next != null) f.StartState = next;
            foreach (var st in f.States) st.SaveActions();
            EditorUtility.SetDirty(pm);
            return true;
        }

        internal static GameObject FindPrefab(string name)
        {
            foreach (var guid in AssetDatabase.FindAssets(name + " t:Prefab"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == name) return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            return null;
        }

        internal static GameObject FindSceneObject(string name)
        {
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                if (go.name == name && go.scene.IsValid() && go.scene.isLoaded) return go;
            return null;
        }
    }
}

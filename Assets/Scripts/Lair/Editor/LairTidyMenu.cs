using System.Collections.Generic;
using Game.Rooms;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// User 2026-11-26: "remove this tree thing, I don't like it much, maybe I can improve it later, but not now. And the
    /// nest seems to not be removed with the purple hole." Two re-runnable menus (prefabs only, no scene change):
    /// - Tools > 429 Game > Lair > Remove Wall Vines: takes every Lair_Wall_Vine out of the room kits (Bedroom_Kit_Base:
    ///   storage room + bedroom; Livingroom_Kit_Base: living room + playroom). The bug holes they grew from stay. The
    ///   vine prefab (Prefabs/Lair/Lair_Wall_Vine) is kept for later; the lair menus don't place it any more.
    /// - Tools > 429 Game > Effects > Purple Hole Eats Lair Nests: Purple Hole Food on Lair_Nest, Lair_Nest_Big,
    ///   Lair_Roach_Nest and Lair_Roach_Eggs (looks only, no body, so the hole never found them). The lair menus add it
    ///   when they make these again.
    /// - Tools > 429 Game > Lair > Remove Claw Marks, Nests And Growth Chart (user 2026-11-26: "remove claw mark and nest
    ///   and pumpkin ruler or meter from the game, they look all bad"): every Lair_Claw_Scratches and Lair_Growth_Chart
    ///   out of the room kits and the open wardrobe (prefabs), and the nests' spawn points (Lair_Nest_Pos,
    ///   Lair_Nest_Big_Pos, Lair_Roach_Nest_Pos) out of the open scene's room sets and their Staggered Spawn lists (save
    ///   the scene after). The egg piles (Lair_Roach_Eggs) stay. The prefabs are kept for later; the lair menus don't
    ///   place them any more.
    /// </summary>
    public static class LairTidyMenu
    {
        private const string Tag = "[Lair]";
        private static readonly string[] Kits =
        {
            "Assets/Prefabs/Bases/Bedroom_Kit_Base.prefab",
            "Assets/Prefabs/Bases/Livingroom_Kit_Base.prefab",
        };
        private static readonly string[] Food =
        {
            "Assets/Prefabs/Lair/Lair_Nest.prefab",
            "Assets/Prefabs/Lair/Lair_Nest_Big.prefab",
            "Assets/Prefabs/Lair/Lair_Roach_Nest.prefab",
            "Assets/Prefabs/Lair/Lair_Roach_Eggs.prefab",
        };

        [MenuItem("Tools/429 Game/Lair/Remove Wall Vines")]
        public static void RemoveVines()
        {
            var notes = new List<string>();
            foreach (var path in Kits)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { notes.Add(path + " missing"); continue; }
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int removed = 0;
                    var vines = new List<GameObject>();
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                        if (t.name.StartsWith("Lair_Wall_Vine")) vines.Add(t.gameObject);
                    foreach (var v in vines)
                    {
                        if (v == null) continue;
                        // Only the top of a vine (a vine's own parts are named otherwise).
                        if (v.transform.parent != null && v.transform.parent.name.StartsWith("Lair_Wall_Vine")) continue;
                        Object.DestroyImmediate(v);
                        removed++;
                    }
                    if (removed > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
                    notes.Add($"{root.name}: {removed} vines removed");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            Debug.Log($"{Tag} {string.Join("; ", notes)}. The bug holes stay; Prefabs/Lair/Lair_Wall_Vine is kept for later.");
        }

        [MenuItem("Tools/429 Game/Effects/Purple Hole Eats Lair Nests")]
        public static void NestsAreFood()
        {
            var notes = new List<string>();
            foreach (var path in Food)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { notes.Add(path + " missing"); continue; }
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (root.GetComponent<PurpleHoleFood>() != null) { notes.Add(root.name + " already"); continue; }
                    root.AddComponent<PurpleHoleFood>();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    notes.Add(root.name + " marked");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            Debug.Log($"{Tag} Purple hole food: {string.Join(", ", notes)}.");
        }

        // The marks on the walls and doors: in the room kits and the open wardrobe.
        private static readonly string[] Marks = { "Lair_Claw_Scratches", "Lair_Growth_Chart" };
        private static readonly string[] MarkPrefabs =
        {
            "Assets/Prefabs/Bases/Bedroom_Kit_Base.prefab",
            "Assets/Prefabs/Bases/Livingroom_Kit_Base.prefab",
            "Assets/Prefabs/Lair/Lair_Wardrobe_Ajar.prefab",
        };
        // The nests: spawn points in the scene's room sets.
        private static readonly string[] NestPoints = { "Lair_Nest_Pos", "Lair_Nest_Big_Pos", "Lair_Roach_Nest_Pos" };

        [MenuItem("Tools/429 Game/Lair/Remove Claw Marks, Nests And Growth Chart")]
        public static void RemoveMarksAndNests()
        {
            var notes = new List<string>();
            foreach (var path in MarkPrefabs)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { notes.Add(path + " missing"); continue; }
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var found = new List<GameObject>();
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    {
                        if (!IsMark(t.name)) continue;
                        // Only the top of one (not its own parts).
                        if (t.parent != null && IsMark(t.parent.name)) continue;
                        found.Add(t.gameObject);
                    }
                    int removed = 0;
                    foreach (var mark in found)
                    {
                        if (mark == null) continue;
                        Object.DestroyImmediate(mark);
                        removed++;
                    }
                    if (removed > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
                    notes.Add($"{root.name}: {removed} removed");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            // The nests' points, out of their sets' Staggered Spawn lists first, then gone.
            var points = new List<GameObject>();
            var staggers = new List<StaggeredSpawn>();
            for (int s = 0; s < UnityEngine.SceneManagement.SceneManager.sceneCount; s++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                foreach (var top in scene.GetRootGameObjects())
                {
                    foreach (var t in top.GetComponentsInChildren<Transform>(true))
                        if (System.Array.IndexOf(NestPoints, t.name) >= 0) points.Add(t.gameObject);
                    staggers.AddRange(top.GetComponentsInChildren<StaggeredSpawn>(true));
                }
            }
            int unlisted = 0;
            foreach (var stagger in staggers)
            {
                var so = new SerializedObject(stagger);
                var list = so.FindProperty("points");
                var keep = new List<Object>();
                for (int i = 0; i < list.arraySize; i++)
                {
                    var p = list.GetArrayElementAtIndex(i).objectReferenceValue;
                    if (p is GameObject go && points.Contains(go)) { unlisted++; continue; }
                    keep.Add(p);
                }
                if (keep.Count == list.arraySize) continue;
                list.arraySize = keep.Count;
                for (int i = 0; i < keep.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = keep[i];
                so.ApplyModifiedProperties();
            }
            var scenes = new HashSet<UnityEngine.SceneManagement.Scene>();
            foreach (var point in points)
            {
                if (point == null) continue;
                scenes.Add(point.scene);
                Undo.DestroyObjectImmediate(point);
            }
            foreach (var scene in scenes) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            notes.Add($"{points.Count} nest points removed from the scene ({unlisted} Staggered Spawn entries)");
            Debug.Log($"{Tag} Claw marks, nests and growth chart: {string.Join("; ", notes)}." +
                      (points.Count > 0 ? " Save the scene." : "") + " The prefabs are kept for later; the egg piles stay.");
        }

        // User 2026-11-26: "remove the decor pumpkin that surround the green bottle from the game, they are not doing
        // anything but distraction". The four Lair_Sipping_Pumpkin round Lair_Jar_Huddle's glowing jar go; the jar stays
        // (Set Up Lair Story Props no longer adds them).
        private const string Huddle = "Assets/Prefabs/Lair/Lair_Jar_Huddle.prefab";

        [MenuItem("Tools/429 Game/Lair/Remove The Pumpkins Round The Glow Jar")]
        public static void RemoveHuddlePumpkins()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Huddle) == null) { Debug.LogError($"{Tag} {Huddle} missing."); return; }
            var root = PrefabUtility.LoadPrefabContents(Huddle);
            try
            {
                var pumpkins = new List<GameObject>();
                foreach (Transform child in root.transform)
                    if (child.name.StartsWith("Lair_Sipping_Pumpkin")) pumpkins.Add(child.gameObject);
                foreach (var p in pumpkins) Object.DestroyImmediate(p);
                if (pumpkins.Count > 0) PrefabUtility.SaveAsPrefabAsset(root, Huddle);
                Debug.Log($"{Tag} Glow jar: {pumpkins.Count} pumpkins removed round it (every room's huddle). Prefab only.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("Tools/429 Game/Lair/Remove The Pumpkins Round The Glow Jar", true)]
        private static bool CanRemoveHuddlePumpkins() => !EditorApplication.isPlayingOrWillChangePlaymode;

        private static bool IsMark(string name)
        {
            foreach (var mark in Marks)
                if (name.StartsWith(mark)) return true;
            return false;
        }

        [MenuItem("Tools/429 Game/Lair/Remove Wall Vines", true)]
        [MenuItem("Tools/429 Game/Effects/Purple Hole Eats Lair Nests", true)]
        [MenuItem("Tools/429 Game/Lair/Remove Claw Marks, Nests And Growth Chart", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}

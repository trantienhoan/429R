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

        [MenuItem("Tools/429 Game/Lair/Remove Wall Vines", true)]
        [MenuItem("Tools/429 Game/Effects/Purple Hole Eats Lair Nests", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}

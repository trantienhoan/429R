using System.Collections.Generic;
using System.Linq;
using Game.Rooms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Rooms > Add Bug Hordes To Furniture: puts Bug Horde on the big furniture bugs could hide in
    /// (beds, shelves and the bookshelf, tables and the work table, sofas, the coffer), so when one breaks, sometimes
    /// (12%) 2-7 small bugs burst out of it. Chairs, stools, the clock, baskets, barrels, boxes, the Jenga blocks, the
    /// wall shelves and the magic tables don't get it. An empty bug list, puff or pop is filled in (Spider_Small,
    /// Cockroach_Small, Bee_Small; the dust puff; Bug_Pop); the chance and the numbers already set aren't touched, so
    /// running it again keeps changes made in the Inspector.
    /// </summary>
    public static class BugHordeSetupMenu
    {
        private const string Folder = "Assets/Prefabs/BedRoom/";
        private const string BugFolder = "Assets/Prefabs/Enemies/Enemies_Smarter/";
        private const string PuffPath = "Assets/Prefabs/Enemies/Boss_FX/Boss_Scrape_Dust.prefab";
        private const string PopPath = "Assets/Audio/SFX/Bug_Pop.wav";

        private static readonly string[] Furniture =
        {
            // Bedroom
            "Bedroom_Bed",
            "Bedroom_Coffer",
            "Bedroom_Shelf_01",
            "MiniBedroom_Shelf_01",
            "Bedroom_Shelf_03",
            "Bedroom_Stuffs_Shelf",
            "Bedroom_Work_Table",
            "Livingroom_Table_2_small",
            // Livingroom
            "Livingroom_Book_Shelf",
            "Livingroom_Sofa_Couple",
            "Livingroom_Sofa_Small",
            "Livingroom_Table_2",
            "Livingroom_Table_3",
        };

        private static readonly string[] Bugs = { "Spider_Small", "Cockroach_Small", "Bee_Small" };

        [MenuItem("Tools/429 Game/Rooms/Add Bug Hordes To Furniture")]
        public static void AddToFurniture()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && Furniture.Any(n => stage.assetPath == Folder + n + ".prefab"))
            {
                Debug.LogError($"[Bug Horde] {stage.assetPath} is open in Prefab Mode: close it first (it would save its " +
                               "old look over the change). Nothing changed.");
                return;
            }

            var bugs = Bugs.Select(n => AssetDatabase.LoadAssetAtPath<GameObject>(BugFolder + n + ".prefab")).Where(b => b != null).ToArray();
            var puff = AssetDatabase.LoadAssetAtPath<GameObject>(PuffPath);
            var pop = AssetDatabase.LoadAssetAtPath<AudioClip>(PopPath);
            var notes = new List<string>();
            if (bugs.Length < Bugs.Length) notes.Add($"only {bugs.Length} of the {Bugs.Length} small bugs found in {BugFolder}");
            if (puff == null) notes.Add("no puff at " + PuffPath);
            if (pop == null) notes.Add("no pop sound at " + PopPath);

            var done = new List<string>();
            foreach (var piece in Furniture)
            {
                string path = Folder + piece + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    notes.Add(piece + ": no prefab at " + path);
                    continue;
                }
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    bool added = !root.TryGetComponent(out BugHorde horde);
                    if (added) horde = root.AddComponent<BugHorde>();
                    var so = new SerializedObject(horde);
                    bool changed = added;
                    var list = so.FindProperty("bugs");
                    if (list.arraySize == 0 && bugs.Length > 0)
                    {
                        list.arraySize = bugs.Length;
                        for (int i = 0; i < bugs.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = bugs[i];
                        changed = true;
                    }
                    changed |= FillIfEmpty(so.FindProperty("puff"), puff);
                    changed |= FillIfEmpty(so.FindProperty("popSound"), pop);
                    if (!changed) continue;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    done.Add(piece + (added ? " (added)" : " (filled in)"));
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            Debug.Log($"[Bug Horde] {done.Count} of {Furniture.Length} furniture prefabs changed" +
                      (done.Count > 0 ? ":\n" + string.Join("\n", done) : " (they all have it already).") +
                      (notes.Count > 0 ? "\nNOTES: " + string.Join("; ", notes) : ""));
        }

        [MenuItem("Tools/429 Game/Rooms/Add Bug Hordes To Furniture", true)]
        private static bool NotPlaying() => !EditorApplication.isPlaying;

        private static bool FillIfEmpty(SerializedProperty property, Object value)
        {
            if (property == null || value == null || property.objectReferenceValue != null) return false;
            property.objectReferenceValue = value;
            return true;
        }
    }
}

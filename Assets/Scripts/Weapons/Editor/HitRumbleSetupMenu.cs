using System.Collections.Generic;
using Game.Weapons;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game.EditorTools
{
    /// <summary>Menu command that adds hit rumble to weapon prefabs (Tools > 429 Game > Weapons).</summary>
    public static class HitRumbleSetupMenu
    {
        private const string WeaponFolder = "Assets/Prefabs/Weapon";

        /// <summary>
        /// Adds Hit Rumble to the selected prefabs, or with none selected, to every grabbable prefab in the weapon
        /// folder. Prefabs that already have it keep their settings.
        /// </summary>
        [MenuItem("Tools/429 Game/Weapons/Add Hit Rumble To Weapons")]
        public static void AddHitRumble()
        {
            var paths = SelectedPrefabPaths();
            bool fromSelection = paths.Count > 0;
            if (!fromSelection)
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { WeaponFolder }))
                    paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            }

            var added = new List<string>();
            int skipped = 0;
            foreach (var path in paths)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    // Only things you hold can buzz your hand; bullets and the like are skipped.
                    if (root.GetComponent<XRGrabInteractable>() == null || root.GetComponent<HitRumble>() != null)
                    {
                        skipped++;
                        continue;
                    }

                    root.AddComponent<HitRumble>();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    added.Add(root.name);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            string where = fromSelection ? "the selected prefabs" : WeaponFolder;
            Debug.Log(added.Count > 0
                ? $"[Hit Rumble] Added to {added.Count} weapon(s) in {where}: {string.Join(", ", added)}. {skipped} skipped (not grabbable, or already set up)."
                : $"[Hit Rumble] Nothing to add in {where}: {skipped} prefab(s) are not grabbable or already have it.");
        }

        private static List<string> SelectedPrefabPaths()
        {
            var paths = new List<string>();
            foreach (var go in Selection.gameObjects)
            {
                if (!EditorUtility.IsPersistent(go)) continue;
                var type = PrefabUtility.GetPrefabAssetType(go);
                if (type != PrefabAssetType.Regular && type != PrefabAssetType.Variant) continue;

                var path = AssetDatabase.GetAssetPath(go);
                if (!paths.Contains(path)) paths.Add(path);
            }
            return paths;
        }
    }
}

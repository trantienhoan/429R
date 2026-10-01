using System.Collections.Generic;
using Game.Lighting;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game.EditorTools
{
    /// <summary>Menu command that makes grabbable lights go out when picked up (Tools > 429 Game > Lighting).</summary>
    public static class LightOffWhenGrabbedMenu
    {
        private const string LightFolder = "Assets/Prefabs/Light";

        /// <summary>
        /// Adds Light Off When Grabbed to the selected prefabs, or with none selected, to every grabbable prefab with a
        /// light in Assets/Prefabs/Light. Prefabs that already have it keep their settings.
        /// </summary>
        [MenuItem("Tools/429 Game/Lighting/Lights Off When Grabbed")]
        public static void AddToGrabbableLights()
        {
            var paths = HitRumbleSetupMenu.SelectedPrefabPaths();
            bool fromSelection = paths.Count > 0;
            if (!fromSelection)
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { LightFolder }))
                    paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            }

            var added = new List<string>();
            foreach (var path in paths)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    // Only things the player can pick up that have a light; ones already set up keep their settings.
                    var grab = root.GetComponentInChildren<XRGrabInteractable>(true);
                    if (grab == null || grab.GetComponent<LightOffWhenGrabbed>() != null) continue;
                    if (grab.GetComponentInChildren<Light>(true) == null) continue;

                    grab.gameObject.AddComponent<LightOffWhenGrabbed>();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    added.Add(root.name);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            string where = fromSelection ? "the selected prefabs" : LightFolder;
            Debug.Log(added.Count > 0
                ? $"[Lights] These now go out (light and flame) when picked up: {string.Join(", ", added)}."
                : $"[Lights] Nothing to change in {where}: no grabbable prefab with a light that isn't set up already.");
        }
    }
}

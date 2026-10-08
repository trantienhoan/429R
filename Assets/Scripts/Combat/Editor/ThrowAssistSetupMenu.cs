using System.Collections.Generic;
using Game.Combat;
using Game.Enemies;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Gaze;

namespace Game.EditorTools
{
    /// <summary>Menu command that sets up the Throw Assist (Tools > 429 Game > Combat).</summary>
    public static class ThrowAssistSetupMenu
    {
        private const string GhostFolder = "Assets/Prefabs/Enemies/PLAYGROUND";

        /// <summary>
        /// Adds the Throw Assist to the XR Origin in the open scene, and a Throw Assist Target to every ghost balloon
        /// prefab (they have no Enemy tag, so it wouldn't aim at them otherwise). Run it in each scene with a player;
        /// whatever is already set up keeps its settings.
        /// </summary>
        [MenuItem("Tools/429 Game/Combat/Add Throw Assist")]
        public static void AddThrowAssist()
        {
            var origin = Object.FindAnyObjectByType<XROrigin>(FindObjectsInactive.Include);
            if (origin == null)
            {
                Debug.LogError("[Throw Assist] No XR Origin in the open scene. Nothing changed.");
                return;
            }

            var notes = new List<string>();
            if (!origin.TryGetComponent(out ThrowAssist assist))
            {
                assist = Undo.AddComponent<ThrowAssist>(origin.gameObject);
                EditorSceneManager.MarkSceneDirty(origin.gameObject.scene);
                notes.Add($"added to '{origin.name}'");
            }

            // XRI uses the aim assist nearest the hand, so another one under the XR Origin would win.
            foreach (var behaviour in origin.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour is IXRAimAssist && behaviour != assist)
                    notes.Add($"WARNING: {behaviour.GetType().Name} on '{behaviour.name}' is also an aim assist and takes over " +
                              "for the hands under it; remove it to use this one");
            }

            var targets = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { GhostFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab.GetComponent<GhostBalloon>() == null || prefab.GetComponent<ThrowAssistTarget>() != null) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    root.AddComponent<ThrowAssistTarget>();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    targets.Add(root.name);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            if (targets.Count > 0) notes.Add("throws now aim at " + string.Join(", ", targets));

            Selection.activeObject = assist;
            Debug.Log("[Throw Assist] " + (notes.Count > 0 ? string.Join("; ", notes) : "already set up, nothing to change") +
                      ". Strength, cone and targets are on the Throw Assist (XR Origin). Save the scene.");
        }
    }
}

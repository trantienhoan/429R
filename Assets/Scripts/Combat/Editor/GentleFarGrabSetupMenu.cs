using Game.Combat;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>Menu command that makes grabbing from a distance gentle (Tools > 429 Game > Combat).</summary>
    public static class GentleFarGrabSetupMenu
    {
        /// <summary>
        /// Adds Gentle Far Grab to the XR Origin in the open scene, so far-grabbed things fly to the hand calmly and
        /// pass through everything on the way. Run it in each scene with a player; an existing one keeps its settings.
        /// </summary>
        [MenuItem("Tools/429 Game/Combat/Add Gentle Far Grab")]
        public static void AddGentleFarGrab()
        {
            var origin = Object.FindAnyObjectByType<XROrigin>(FindObjectsInactive.Include);
            if (origin == null)
            {
                Debug.LogError("[Gentle Far Grab] No XR Origin in the open scene. Nothing changed.");
                return;
            }

            if (origin.TryGetComponent(out GentleFarGrab existing))
            {
                Selection.activeObject = existing;
                Debug.Log("[Gentle Far Grab] The XR Origin already has it; it's selected.");
                return;
            }

            Selection.activeObject = Undo.AddComponent<GentleFarGrab>(origin.gameObject);
            EditorSceneManager.MarkSceneDirty(origin.gameObject.scene);
            Debug.Log($"[Gentle Far Grab] Added to '{origin.name}'. Pull speed and the far distance are on it. Save the scene.");
        }
    }
}

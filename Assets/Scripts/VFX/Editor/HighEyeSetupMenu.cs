using System.Collections.Generic;
using Game.Menus;
using Game.VFX;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Player > Add High Eye: puts a "High Eye" object (High Eye, the gentle look while high on a
    /// boong) in the scene, hidden while the death look (Post Processing_Dead) is on, and adds the High Eye on/off option
    /// to the pause menu. Safe to run again. Save the scene afterwards.
    /// </summary>
    public static class HighEyeSetupMenu
    {
        private const string ObjectName = "High Eye";
        private const string DeathLookName = "Post Processing_Dead";

        [MenuItem("Tools/429 Game/Player/Add High Eye")]
        public static void Run()
        {
            var notes = new List<string>();
            var eye = Object.FindAnyObjectByType<HighEye>(FindObjectsInactive.Include);
            if (eye == null)
            {
                var go = new GameObject(ObjectName);
                Undo.RegisterCreatedObjectUndo(go, "Add High Eye");
                eye = go.AddComponent<HighEye>();
                notes.Add("made '" + ObjectName + "'");
            }

            var serialized = new SerializedObject(eye);
            var hide = serialized.FindProperty("hideWhileOn");
            if (hide.objectReferenceValue == null)
            {
                foreach (var volume in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (volume.name != DeathLookName) continue;
                    hide.objectReferenceValue = volume.gameObject;
                    serialized.ApplyModifiedProperties();
                    notes.Add("it steps aside for " + DeathLookName);
                    break;
                }
            }

            var menu = Object.FindAnyObjectByType<PauseMenu>(FindObjectsInactive.Include);
            if (menu == null) notes.Add("no Pause Menu in the scene, so there's no High Eye option (it's on)");
            else if (PauseMenuSetup.AddHighEyeOption(menu, SetupUtility.ResolveFont())) notes.Add("added High Eye: on/off to the pause menu");

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = eye.gameObject;
            Debug.Log("[High Eye] " + (notes.Count > 0 ? string.Join("; ", notes) : "already set up") + ". Save the scene.");
        }
    }
}

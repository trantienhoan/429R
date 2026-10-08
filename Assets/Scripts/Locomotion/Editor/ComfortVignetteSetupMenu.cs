using System.Collections.Generic;
using Game.Locomotion;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;

namespace Game.EditorTools
{
    /// <summary>Menu command that adds XRI's comfort vignette to the player's view (Tools > 429 Game > Locomotion).</summary>
    public static class ComfortVignetteSetupMenu
    {
        private const string VignettePrefab = "Assets/Samples/XR Interaction Toolkit/3.1.1/Starter Assets/TunnelingVignette/TunnelingVignette.prefab";

        /// <summary>
        /// Puts XRI's Tunneling Vignette on the headset camera: the edges of the view darken softly while the player
        /// moves (thumbstick or arm swing), turns or teleports, which cuts motion sickness a lot. Every locomotion
        /// provider on the XR Origin drives it. Run it again after adding a new way to move.
        /// </summary>
        [MenuItem("Tools/429 Game/Locomotion/Add Comfort Vignette")]
        public static void AddComfortVignette()
        {
            var origin = Object.FindAnyObjectByType<XROrigin>(FindObjectsInactive.Include);
            if (origin == null || origin.Camera == null)
            {
                Debug.LogError("[Comfort Vignette] No XR Origin with a camera in the open scene. Nothing changed.");
                return;
            }

            var notes = new List<string>();
            var camera = origin.Camera.transform;
            var vignette = camera.GetComponentInChildren<TunnelingVignetteController>(true);
            if (vignette == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VignettePrefab);
                if (prefab == null)
                {
                    Debug.LogError($"[Comfort Vignette] Missing {VignettePrefab} (XRI Starter Assets sample). Nothing changed.");
                    return;
                }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, camera);
                Undo.RegisterCreatedObjectUndo(go, "Add Comfort Vignette");
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                vignette = go.GetComponent<TunnelingVignetteController>();
                notes.Add($"added the vignette under '{camera.name}'");
            }

            if (!vignette.TryGetComponent(out HideVignetteWhenOpen _))
            {
                Undo.AddComponent<HideVignetteWhenOpen>(vignette.gameObject);
                notes.Add("it's hidden whenever it's fully open, so it costs nothing while standing still");
            }

            if (!vignette.TryGetComponent(out ComfortVignetteLevel _))
            {
                Undo.AddComponent<ComfortVignetteLevel>(vignette.gameObject);
                notes.Add("its strength follows the player's choice in the pause menu (Comfort Vignette Level; new players get Low)");
            }

            // Every way to move or turn on the rig (thumbstick move, arm swing, smooth and snap turn, teleport).
            Undo.RecordObject(vignette, "Add Comfort Vignette");
            var providers = vignette.locomotionVignetteProviders;
            var added = new List<string>();
            foreach (var provider in origin.GetComponentsInChildren<LocomotionProvider>(true))
            {
                if (providers.Exists(p => p.locomotionProvider == provider)) continue;
                providers.Add(new LocomotionVignetteProvider { locomotionProvider = provider, enabled = true });
                added.Add($"{provider.GetType().Name} ('{provider.name}')");
            }
            if (added.Count > 0) notes.Add("driven by " + string.Join(", ", added));
            EditorUtility.SetDirty(vignette);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = vignette.gameObject;
            Debug.Log("[Comfort Vignette] " + (notes.Count > 0 ? string.Join("; ", notes) : "already set up, nothing to change") +
                      ". The strength of each pause-menu level is on Comfort Vignette Level, fade times on the Tunneling Vignette Controller. Save the scene.");
        }
    }
}

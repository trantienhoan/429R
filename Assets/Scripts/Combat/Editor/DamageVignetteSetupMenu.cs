using Game.Combat;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.EditorTools
{
    /// <summary>Menu command that adds the red hit lines to the player's view (Tools > 429 Game > Combat).</summary>
    public static class DamageVignetteSetupMenu
    {
        private const string ShaderName = "429/Damage Vignette";
        private const string MaterialPath = "Assets/Materials/DamageVignette.mat";

        /// <summary>
        /// Puts a Damage Vignette on the XR Origin in the open scene: whenever the PlayerHealth FSM's health drops, red
        /// lines burst in from the edges of the view and fade. Makes its material the first time. Safe to run again.
        /// </summary>
        [MenuItem("Tools/429 Game/Combat/Add Damage Vignette")]
        public static void AddDamageVignette()
        {
            var origin = Object.FindAnyObjectByType<XROrigin>(FindObjectsInactive.Include);
            if (origin == null)
            {
                Debug.LogError("[Damage Vignette] No XR Origin in the open scene. Nothing changed.");
                return;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                var shader = Shader.Find(ShaderName);
                if (shader == null)
                {
                    Debug.LogError($"[Damage Vignette] The '{ShaderName}' shader is missing (Assets/Shader/DamageVignette.shader). Nothing changed.");
                    return;
                }
                material = new Material(shader) { name = "DamageVignette" };
                AssetDatabase.CreateAsset(material, MaterialPath);
                AssetDatabase.SaveAssets();
            }

            var vignette = origin.GetComponent<DamageVignette>();
            bool added = vignette == null;
            if (added) vignette = Undo.AddComponent<DamageVignette>(origin.gameObject);
            var so = new SerializedObject(vignette);
            so.FindProperty("material").objectReferenceValue = material;
            so.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = origin.gameObject;
            Debug.Log($"[Damage Vignette] {(added ? "Added to" : "Already on")} '{origin.name}', using {MaterialPath}. " +
                      "Colour and lines are on the material; strength and fade times on the component. Save the scene.");
        }
    }
}

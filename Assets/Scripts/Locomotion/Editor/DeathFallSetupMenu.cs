using System.Collections.Generic;
using Game.Locomotion;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    /// <summary>Menu command that makes the death view fall to the floor and fade to black (Tools > 429 Game > Player).</summary>
    public static class DeathFallSetupMenu
    {
        private const string DeadCamName = "DeadCam";
        private const string FadeName = "Headset Fade";
        private const string GameOverName = "GAME_OVER";
        private const string EffectsFolder = "Assets/Data/Effects";
        private const string FadeMaterialPath = EffectsFolder + "/Headset Fade.mat";
        private const string OverFadeMaterialPath = EffectsFolder + "/UI Over Fade.mat";
        // A see-through Unlit material that already works in the headset; the fade material starts as a copy of it.
        private const string FadeMaterialSource = "Assets/Materials/Fade Screen Material.mat";
        // Just outside the camera's near clip at every angle, and close enough to cover nearly everything.
        private const float FadeRadius = 0.15f;
        // The fade draws after everything else, and "UI Over Fade" pictures after the fade.
        private const int FadeQueue = (int)RenderQueue.Overlay;

        /// <summary>
        /// Adds Death Camera Fall to the DeadCam object under the XR rig's Camera Offset (making DeadCam if there's none),
        /// a Headset Fade sphere to the Main Camera, and puts the GAME_OVER picture over the fade. Run again, it only adds
        /// what's missing. PlayerHealth's DEAD and Restart states stay as they are.
        /// </summary>
        [MenuItem("Tools/429 Game/Player/Set Up Death Fall")]
        public static void SetUpDeathFall()
        {
            var origin = Object.FindAnyObjectByType<XROrigin>(FindObjectsInactive.Include);
            if (origin == null || origin.CameraFloorOffsetObject == null || origin.Camera == null)
            {
                Debug.LogError("[Death Fall] There's no XR Origin with a Camera Offset and a camera in the scene.");
                return;
            }

            var notes = new List<string>();
            var offset = origin.CameraFloorOffsetObject.transform;
            var head = origin.Camera.transform;

            var deadCam = offset.Find(DeadCamName);
            if (deadCam == null)
            {
                var go = new GameObject(DeadCamName);
                Undo.RegisterCreatedObjectUndo(go, "Create DeadCam");
                deadCam = go.transform;
                deadCam.SetParent(offset, false);
                notes.Add($"made '{DeadCamName}' under '{offset.name}' (point PlayerHealth's DEAD Set Parent at it)");
            }

            if (!deadCam.TryGetComponent<DeathCameraFall>(out var fall))
            {
                fall = Undo.AddComponent<DeathCameraFall>(deadCam.gameObject);
                notes.Add("added Death Camera Fall to DeadCam");
            }
            SetupUtility.SetReferenceIfEmpty(fall, "head", head);

            var fade = head.GetComponentInChildren<HeadsetFade>(true);
            if (fade == null)
            {
                fade = CreateFade(head);
                notes.Add($"added '{FadeName}' to the camera");
            }
            SetupUtility.SetReferenceIfEmpty(fall, "fade", fade);

            int onTop = PutGameOverOverFade(head, notes);
            if (onTop > 0) notes.Add($"the {GameOverName} picture now shows over the fade");

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = deadCam.gameObject;
            Debug.Log("[Death Fall] Done: " + (notes.Count > 0 ? string.Join("; ", notes) : "everything was already set up") +
                      ". Tune it on the DeadCam object, then save the scene.", deadCam);
        }

        private static HeadsetFade CreateFade(Transform head)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = FadeName;
            go.layer = head.gameObject.layer;
            Undo.RegisterCreatedObjectUndo(go, "Create Headset Fade");
            go.transform.SetParent(head, false);
            go.transform.localScale = Vector3.one * (FadeRadius * 2f);

            var sphere = go.GetComponent<MeshRenderer>();
            sphere.sharedMaterial = FadeMaterial();
            sphere.shadowCastingMode = ShadowCastingMode.Off;
            sphere.receiveShadows = false;
            sphere.lightProbeUsage = LightProbeUsage.Off;
            sphere.reflectionProbeUsage = ReflectionProbeUsage.Off;
            sphere.enabled = false;

            return go.AddComponent<HeadsetFade>();
        }

        private static Material FadeMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(FadeMaterialPath);
            if (material != null) return material;

            SetupUtility.EnsureFolder(EffectsFolder);
            if (AssetDatabase.LoadAssetAtPath<Material>(FadeMaterialSource) != null && AssetDatabase.CopyAsset(FadeMaterialSource, FadeMaterialPath))
            {
                material = AssetDatabase.LoadAssetAtPath<Material>(FadeMaterialPath);
            }
            else
            {
                // Unlit, see-through, seen from inside the sphere.
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_Cull", (float)CullMode.Off);
                material.SetFloat("_ZWrite", 0f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                AssetDatabase.CreateAsset(material, FadeMaterialPath);
            }

            material.SetColor("_BaseColor", new Color(0f, 0f, 0f, 0f));
            material.renderQueue = FadeQueue;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material OverFadeMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(OverFadeMaterialPath);
            if (material != null) return material;

            SetupUtility.EnsureFolder(EffectsFolder);
            material = new Material(Shader.Find("UI/Default")) { renderQueue = FadeQueue + 1 };
            AssetDatabase.CreateAsset(material, OverFadeMaterialPath);
            return material;
        }

        // The death picture is a canvas on the camera: its images use "UI Over Fade", drawn after the fade.
        private static int PutGameOverOverFade(Transform head, List<string> notes)
        {
            Transform gameOver = null;
            foreach (var child in head.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == GameOverName) gameOver = child;
            }
            if (gameOver == null) return 0;

            var material = OverFadeMaterial();
            int changed = 0;
            foreach (var graphic in gameOver.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic is TMP_Text)
                {
                    notes.Add($"'{graphic.name}' is TextMeshPro text, which keeps its font material and may be hidden by the fade");
                    continue;
                }
                if (graphic.material == material) continue;

                Undo.RecordObject(graphic, "Show Over Fade");
                graphic.material = material;
                EditorUtility.SetDirty(graphic);
                changed++;
            }
            return changed;
        }
    }
}

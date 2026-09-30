using Game.Locomotion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    /// <summary>Menu command that adds arm-swing walking to the XR rig (Tools > 429 Game > Locomotion).</summary>
    public static class ArmSwingSetupMenu
    {
        // A hard swing walks a little faster than the thumbstick (1 m/s), without outrunning the bugs.
        private const float DefaultSpeed = 1.5f;

        [MenuItem("Tools/429 Game/Locomotion/Set Up Arm Swing")]
        public static void SetUpArmSwing()
        {
            var existing = Object.FindAnyObjectByType<ArmSwingMoveProvider>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                Debug.Log("[Arm Swing] The rig already has it; it's selected.", existing);
                return;
            }

            var mediator = Object.FindAnyObjectByType<LocomotionMediator>(FindObjectsInactive.Include);
            if (mediator == null)
            {
                Debug.LogError("[Arm Swing] There's no Locomotion Mediator in the scene. It lives on the XR rig's Locomotion object.");
                return;
            }

            var go = new GameObject("Arm Swing Move");
            Undo.RegisterCreatedObjectUndo(go, "Create Arm Swing Move");
            go.transform.SetParent(mediator.transform, false);
            var provider = go.AddComponent<ArmSwingMoveProvider>();
            provider.mediator = mediator;
            provider.moveSpeed = DefaultSpeed;
            provider.enableStrafe = false;

            var settings = new SerializedObject(provider);
            // Fall and steer in the air the same way the thumbstick mover does.
            var stick = FindStickMover(provider);
            if (stick != null)
            {
                var stickSettings = new SerializedObject(stick);
                CopyBool(stickSettings, settings, "m_UseGravity");
                CopyFloat(stickSettings, settings, "m_InAirControlModifier");
            }
            var leftHand = XRRig.FindController(left: true);
            var rightHand = XRRig.FindController(left: false);
            if (leftHand != null) settings.FindProperty("leftHand").objectReferenceValue = leftHand;
            if (rightHand != null) settings.FindProperty("rightHand").objectReferenceValue = rightHand;
            settings.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = go;
            Debug.Log($"[Arm Swing] Added 'Arm Swing Move' under '{mediator.name}'. Hold X on the left controller and swing your arms " +
                      $"to walk (up to {DefaultSpeed} m/s). Save the scene to keep it.", go);
        }

        private static ContinuousMoveProvider FindStickMover(ContinuousMoveProvider except)
        {
            foreach (var mover in Object.FindObjectsByType<ContinuousMoveProvider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mover != except) return mover;
            }
            return null;
        }

        private static void CopyBool(SerializedObject from, SerializedObject to, string field)
        {
            var source = from.FindProperty(field);
            var target = to.FindProperty(field);
            if (source != null && target != null) target.boolValue = source.boolValue;
        }

        private static void CopyFloat(SerializedObject from, SerializedObject to, string field)
        {
            var source = from.FindProperty(field);
            var target = to.FindProperty(field);
            if (source != null && target != null) target.floatValue = source.floatValue;
        }
    }
}

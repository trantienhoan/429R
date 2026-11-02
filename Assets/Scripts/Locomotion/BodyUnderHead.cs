using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Locomotion
{
    /// <summary>
    /// Keeps the player's body under their head. Walking in real life moves the head and not the XR Origin, and XRI only moves
    /// the Character Controller while the player moves by stick, so what hit the body hit the middle of the play area, beside
    /// the player. Each frame this puts the Character Controller and the rig's capsules (the Player trigger, Bug Slayer) under
    /// the head; their heights stay as they are. Added to the XR Origin by itself as a scene loads: nothing to set up.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XROrigin))]
    public class BodyUnderHead : MonoBehaviour
    {
        // Moved only once it's this far off, in metres.
        private const float Tolerance = 0.01f;

        private XROrigin origin;
        private CharacterController controller;
        private CapsuleCollider[] capsules;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void WatchScenes()
        {
            SceneManager.sceneLoaded -= AddToPlayer;
            SceneManager.sceneLoaded += AddToPlayer;
        }

        private static void AddToPlayer(Scene scene, LoadSceneMode mode)
        {
            var rig = FindAnyObjectByType<XROrigin>(FindObjectsInactive.Include);
            if (rig != null && !rig.TryGetComponent<BodyUnderHead>(out _)) rig.gameObject.AddComponent<BodyUnderHead>();
        }

        private void Awake()
        {
            origin = GetComponent<XROrigin>();
            controller = GetComponent<CharacterController>();
            capsules = GetComponentsInChildren<CapsuleCollider>(true);
        }

        private void LateUpdate()
        {
            if (origin.Camera == null) return;
            var head = origin.Camera.transform.position;
            if (controller != null)
            {
                var under = transform.InverseTransformPoint(head);
                under.y = controller.center.y;
                if (IsOff(controller.center, under)) controller.center = under;
            }
            foreach (var capsule in capsules)
            {
                // Those on the hands and the head go with them.
                if (capsule == null || capsule.transform.IsChildOf(origin.CameraFloorOffsetObject.transform)) continue;
                var part = capsule.transform;
                var middle = part.TransformPoint(capsule.center);
                var under = part.InverseTransformPoint(new Vector3(head.x, middle.y, head.z));
                if (IsOff(capsule.center, under)) capsule.center = under;
            }
        }

        private static bool IsOff(Vector3 now, Vector3 wanted)
        {
            float x = now.x - wanted.x;
            float z = now.z - wanted.z;
            return x * x + z * z > Tolerance * Tolerance;
        }
    }
}

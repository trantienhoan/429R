using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace Balls
{
    /// <summary>
    /// 429R (Ball Valley step 1, made by Tools > 429 Game > Ball Valley > Watch Ball Valley): an invisible platform for
    /// watching the bots. The player is put on it, facing <see cref="yaw"/>; while it's there, thumbstick and arm-swing
    /// moving and teleport are off (checked again every half second: the pause menu or a fall may switch them back on).
    /// It's a child of the stage, so it goes with it, and everything is given back then.
    /// </summary>
    [DisallowMultipleComponent]
    public class BallValleyWatchSpot : MonoBehaviour
    {
        [Tooltip("The platform's size (m); its top is this object's position.")]
        public Vector3 size = new Vector3(4f, 0.2f, 4f);
        [Tooltip("The way the player faces on it (world degrees).")]
        public float yaw;

        XROrigin origin;
        GameObject teleportRay;
        bool rayWas;
        float nextCheck;
        readonly Dictionary<Behaviour, bool> was = new Dictionary<Behaviour, bool>();

        void Start()
        {
            var box = gameObject.AddComponent<BoxCollider>();
            box.size = size;
            box.center = new Vector3(0f, -size.y * 0.5f, 0f);
            Physics.SyncTransforms();
            origin = FindAnyObjectByType<XROrigin>();
            MovingOff();
            PutPlayer();
        }

        void Update()
        {
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + 0.5f;
            MovingOff();
        }

        void OnDestroy() => MovingBack();

        void MovingOff()
        {
            foreach (var mover in FindObjectsByType<ContinuousMoveProvider>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Hold(mover);
            foreach (var teleport in FindObjectsByType<TeleportationProvider>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Hold(teleport);
            if (!teleportRay && origin)
            {
                var ray = Find(origin.transform, "Teleport Interactor");
                if (ray)
                {
                    teleportRay = ray.gameObject;
                    rayWas = teleportRay.activeSelf;
                }
            }
            if (teleportRay && teleportRay.activeSelf) teleportRay.SetActive(false);
        }

        // Off, its own state remembered once (given back by MovingBack).
        void Hold(Behaviour b)
        {
            if (!b) return;
            if (!was.ContainsKey(b)) was[b] = b.enabled;
            if (b.enabled) b.enabled = false;
        }

        void MovingBack()
        {
            foreach (var pair in was)
                if (pair.Key) pair.Key.enabled = pair.Value;
            was.Clear();
            if (teleportRay) teleportRay.SetActive(rayWas);
            teleportRay = null;
        }

        // The head over the platform's middle, the feet on it, facing Yaw (as the bowling stage moves the player).
        void PutPlayer()
        {
            if (!origin) return;
            var cam = origin.Camera ? origin.Camera.transform : null;
            var body = origin.GetComponent<CharacterController>();
            if (body) body.enabled = false;
            var launch = origin.GetComponent<Game.Combat.PlayerLaunch>();
            if (launch) launch.Stop();
            if (cam)
            {
                var look = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
                float headYaw = look.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(look).eulerAngles.y : 0f;
                origin.RotateAroundCameraUsingOriginUp(Mathf.DeltaAngle(headYaw, yaw));
            }
            Vector3 spot = transform.position;
            Vector3 feet = cam ? new Vector3(cam.position.x, origin.transform.position.y, cam.position.z) : origin.transform.position;
            var to = origin.transform.position + new Vector3(spot.x - feet.x, 0f, spot.z - feet.z);
            to.y = spot.y;
            origin.transform.position = to;
            Physics.SyncTransforms();
            if (body) body.enabled = true;
        }

        static Transform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }
}

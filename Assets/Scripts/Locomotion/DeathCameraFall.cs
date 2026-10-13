using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using TrackedPoseDriver = UnityEngine.InputSystem.XR.TrackedPoseDriver;

namespace Game.Locomotion
{
    /// <summary>
    /// The death view. Goes on the "DeadCam" object under the XR rig's Camera Offset. When the player dies and
    /// PlayerHealth's DEAD state puts the Main Camera under it, it first lines up with where the view already is, so
    /// nothing jumps, then plays a fall: the head drops to the floor and tips onto its side, and the hands fall with it,
    /// while the headset view fades to black. The headset keeps tracking the whole time, so looking around still works.
    /// When the Restart state takes the camera back out, everything goes back to normal and the view fades back in.
    /// A revive (Player Revive) first stops the fall part way (Stop Part Way: the fall and the fade hold there), then
    /// calls Rise: the fall plays backwards, the view fades back in, and the camera goes back to the rig.
    /// Tools > 429 Game > Player > Set Up Death Fall adds it.
    /// </summary>
    [DisallowMultipleComponent]
    public class DeathCameraFall : MonoBehaviour
    {
        [Tooltip("The headset camera. Left empty, Camera.main is used.")]
        [SerializeField] private Transform head;
        [Tooltip("How long the fall takes, in seconds.")]
        [Min(0.1f)]
        [SerializeField] private float fallTime = 1.57f;
        [Tooltip("Eye height above the floor once lying down, in metres.")]
        [Min(0f)]
        [SerializeField] private float lyingHeight = 0.2f;
        [Tooltip("How far the view tips onto its side, in degrees, to the left or right at random. 0 = straight down.")]
        [Range(0f, 90f)]
        [SerializeField] private float tipOver = 75f;
        [Tooltip("The hands fall with the head, instead of staying up in the air.")]
        [SerializeField] private bool handsFallToo = true;

        [Header("Fade")]
        [Tooltip("Fades the headset view to black during the fall. Left empty, the one under the camera is used.")]
        [SerializeField] private HeadsetFade fade;
        [Tooltip("Seconds after dying before the view starts to go dark.")]
        [Min(0f)]
        [SerializeField] private float fadeDelay = 0.6f;
        [Tooltip("Seconds to go fully dark. Keep delay + this under the death's 4.3 s, so it's dark before the restart.")]
        [Min(0f)]
        [SerializeField] private float fadeOutTime = 2f;
        [Tooltip("Seconds for the view to come back when the game restarts.")]
        [Min(0f)]
        [SerializeField] private float fadeInTime = 1f;

        private readonly List<Transform> hands = new();
        private Transform offset;
        private XROrigin origin;
        private Vector3 homePosition;
        private Quaternion homeRotation;
        private Vector3 headPosition;
        private Quaternion headRotation = Quaternion.identity;
        private bool fallen;
        private float elapsed;
        private Vector3 pivot;
        private Vector3 tipAxis;
        private float tipAngle;
        private float drop;
        private bool rising;
        private float riseSpeed;
        // How much of the fall this death plays (1: all of it; a revive stops it part way), and whether the fade has
        // been held there.
        private float stopAt = 1f;
        private bool holding;

        /// <summary>Whether the player is down (the camera is on this, falling or fallen).</summary>
        public bool IsDown => fallen;

        /// <summary>Whether the fall has got to where Stop Part Way stops it, and is holding there.</summary>
        public bool IsHolding => fallen && !rising && holding;

        /// <summary>
        /// This death's fall stops part way (0.5 = half way) and holds there, the view's fade too, until Rise: a revive.
        /// Call it as the player dies (before or just after the fall starts). The next death falls all the way again.
        /// </summary>
        public void StopPartWay(float share)
        {
            stopAt = Mathf.Clamp01(share);
        }

        /// <summary>
        /// Gets the player back up (a revive): the fall plays backwards over this many seconds while the view fades back
        /// in, then the camera goes back to the rig. Nothing happens if the player isn't down.
        /// </summary>
        public void Rise(float seconds)
        {
            if (!fallen) return;
            rising = true;
            riseSpeed = elapsed / Mathf.Max(0.05f, seconds);
            if (fade != null) fade.FadeIn(Mathf.Max(0.05f, seconds));
        }

        private void Awake()
        {
            // Everything happens in the Camera Offset's space: the head's usual parent and this object's parent.
            offset = transform.parent;
            origin = GetComponentInParent<XROrigin>();
            homePosition = transform.localPosition;
            homeRotation = transform.localRotation;
            if (head == null && Camera.main != null) head = Camera.main.transform;
            if (fade == null && head != null) fade = head.GetComponentInChildren<HeadsetFade>(true);
        }

        private void OnDisable()
        {
            if (fallen) GetUp(true);
        }

        private void LateUpdate()
        {
            if (head == null || offset == null) return;

            bool headHere = head.parent == transform;
            if (!fallen)
            {
                if (headHere)
                {
                    StartFall();
                }
                else
                {
                    // The DEAD state resets the head's pose when it moves it here, so the last tracked pose is kept.
                    if (head.parent == offset) head.GetLocalPositionAndRotation(out headPosition, out headRotation);
                    return;
                }
            }
            else if (!headHere)
            {
                GetUp(true);
                return;
            }

            if (rising) RiseStep();
            else Fall();
        }

        private void StartFall()
        {
            fallen = true;
            elapsed = 0f;
            holding = false;

            // On top of the Camera Offset, with the head back where it was: the view doesn't move yet.
            transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            head.SetLocalPositionAndRotation(headPosition, headRotation);

            hands.Clear();
            if (handsFallToo)
            {
                foreach (Transform child in offset)
                {
                    if (child != head && child != transform && child.GetComponent<TrackedPoseDriver>() != null) hands.Add(child);
                }
                // Same place as the Camera Offset, so their tracked local poses carry over unchanged.
                foreach (var hand in hands) hand.SetParent(transform, false);
            }

            // Drop from the head's height to lying height above the floor, turning about the head, tipping sideways.
            pivot = headPosition;
            float floor = origin != null ? offset.InverseTransformPoint(origin.Origin.transform.position).y : 0f;
            drop = Mathf.Max(0f, pivot.y - floor - lyingHeight);

            var forward = headRotation * Vector3.forward;
            forward.y = 0f;
            tipAxis = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            tipAngle = Random.value < 0.5f ? tipOver : -tipOver;

            if (fade != null) fade.FadeOut(fadeOutTime, fadeDelay);
        }

        private void Fall()
        {
            float end = fallTime * stopAt;
            if (elapsed >= end)
            {
                // Stopped part way: it holds there, and the view stays as dark as it got.
                if (stopAt < 1f && !holding)
                {
                    holding = true;
                    if (fade != null) fade.FadeTo(fade.Alpha, 0f);
                }
                return;
            }

            elapsed = Mathf.Min(elapsed + Time.deltaTime, end);
            Pose(elapsed / fallTime);
        }

        // The fall backwards; once all the way up, the camera goes back to the rig, right where it is.
        private void RiseStep()
        {
            elapsed = Mathf.Max(0f, elapsed - Time.deltaTime * riseSpeed);
            Pose(elapsed / fallTime);
            if (elapsed > 0f) return;

            rising = false;
            // Back up, this lines up with the Camera Offset, so the head keeps its tracked local pose and doesn't move.
            head.SetParent(offset, false);
            GetUp(false);
        }

        // Where the view is, t of the way through the fall.
        private void Pose(float t)
        {
            // Falls faster and faster, like a body; tips over smoothly.
            float down = t * t;
            float tip = t * t * (3f - 2f * t);

            var rotation = Quaternion.AngleAxis(tipAngle * tip, tipAxis);
            transform.SetLocalPositionAndRotation(pivot - rotation * pivot + Vector3.down * (drop * down), rotation);
        }

        private void GetUp(bool fadeIn)
        {
            fallen = false;
            rising = false;
            stopAt = 1f;
            holding = false;
            foreach (var hand in hands)
            {
                if (hand != null) hand.SetParent(offset, false);
            }
            hands.Clear();
            transform.SetLocalPositionAndRotation(homePosition, homeRotation);
            if (fadeIn && fade != null) fade.FadeIn(fadeInTime);
        }
    }
}

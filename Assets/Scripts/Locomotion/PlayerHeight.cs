using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;

namespace Game.Locomotion
{
    /// <summary>
    /// Raises or lowers the player's view and hands by Offset, on top of the height the headset measures: for playing
    /// seated, or for anyone who wants to stand taller or shorter. With the XR Origin tracking from the floor, 0 means
    /// the game's floor is the real floor. The pause menu's Height buttons change it, and it's remembered on this device.
    /// Bonus adds to it for a while without being remembered (the Green Boong buff makes the player 0.5 m taller).
    /// Goes on the XR Origin; Tools > 429 Game > Menu > Set Up Pause Menu adds it.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XROrigin))]
    // After everything else in LateUpdate, so a height set by an FSM that frame (the Camera Offset's "Info" FSM puts it
    // at 0.7 after every shake) is corrected before the frame is drawn.
    [DefaultExecutionOrder(10000)]
    public class PlayerHeight : MonoBehaviour
    {
        // Where the player's choice is kept (PlayerPrefs, so per device: each headset measures its own floor).
        private const string SaveKey = "429 Game.PlayerHeight";

        [Tooltip("How much each press of a Height button changes it, in metres.")]
        [Min(0.01f)]
        [SerializeField] private float step = 0.05f;
        [Tooltip("The lowest it can go, in metres (negative = shorter than the player really is).")]
        [SerializeField] private float lowest = -1f;
        [Tooltip("The highest it can go, in metres.")]
        [SerializeField] private float highest = 1f;

        private XROrigin origin;
        private float offset;

        /// <summary>Metres added to the player's height; negative makes them shorter. Remembered when changed.</summary>
        public float Offset
        {
            get => offset;
            set
            {
                offset = Mathf.Clamp(value, lowest, highest);
                PlayerPrefs.SetFloat(SaveKey, offset);
                PlayerPrefs.Save();
                Apply();
            }
        }

        /// <summary>
        /// Metres added on top of Offset while the game runs, never remembered: e.g. the Green Boong buff (Player Buffs)
        /// eases it up to 0.5 and back. 0 = none.
        /// </summary>
        public static float Bonus { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Bonus = 0f;
        }

        /// <summary>Raises the player by this many Height steps (negative lowers them).</summary>
        public void Step(int steps)
        {
            Offset = Mathf.Round(offset / step + steps) * step;
        }

        private void Awake()
        {
            origin = GetComponent<XROrigin>();
            offset = Mathf.Clamp(PlayerPrefs.GetFloat(SaveKey, 0f), lowest, highest);
        }

        // The XR Origin sets the view's height again whenever tracking starts or the player recenters, so the offset
        // is put back on top every frame; nothing moves unless the height is off.
        private void LateUpdate()
        {
            Apply();
        }

        private void Apply()
        {
            if (origin == null || origin.CameraFloorOffsetObject == null) return;

            // The height the XR Origin itself gives the view: none when tracking from the floor (the headset measures
            // it), otherwise its fixed Camera Y Offset.
            float baseHeight;
            switch (origin.CurrentTrackingOriginMode)
            {
                case TrackingOriginModeFlags.Floor:
                    baseHeight = 0f;
                    break;
                case TrackingOriginModeFlags.Device:
                case TrackingOriginModeFlags.Unbounded:
                    baseHeight = origin.CameraYOffset;
                    break;
                default:
                    // Tracking hasn't started yet.
                    return;
            }

            var offsetTransform = origin.CameraFloorOffsetObject.transform;
            var position = offsetTransform.localPosition;
            float height = baseHeight + offset + Bonus;
            if (Mathf.Approximately(position.y, height)) return;

            position.y = height;
            offsetTransform.localPosition = position;
        }
    }
}

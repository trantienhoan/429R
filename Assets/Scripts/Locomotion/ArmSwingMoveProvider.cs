using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

namespace Game.Locomotion
{
    /// <summary>
    /// Walk by swinging your arms, like jogging on the spot: the faster you swing, the faster you go, up to Move Speed.
    /// Speed builds up over the first few swings like a real jog: the first swing gets you ready, then each swing adds
    /// speed until you reach Move Speed (on the 4th swing by default).
    /// By default you hold X on the left controller while swinging, so fighting never walks you anywhere. Without the
    /// button, only alternating swings count (one arm forward while the other goes back), which a weapon swing isn't.
    /// Players pick hold X, no button or off in the pause menu; the choice is remembered on their device.
    /// It's a Continuous Move Provider, so walls, the character controller, gravity and pause work just like the
    /// thumbstick; its Left/Right Hand Move Input fields aren't used. Tools > 429 Game > Locomotion > Set Up Arm Swing adds it.
    /// </summary>
    public class ArmSwingMoveProvider : ContinuousMoveProvider
    {
        public enum WalkDirection
        {
            Head,
            Hands,
        }

        public enum SwingMode
        {
            /// <summary>Only swings made while holding the button walk you.</summary>
            HoldButton,
            /// <summary>No button; only alternating swings walk you.</summary>
            NoButton,
            /// <summary>Arm swinging never walks you.</summary>
            Off,
        }

        private enum HandSwing
        {
            Still,
            Forward,
            Back,
        }

        // Letting go of the button stops you this quickly, in seconds.
        private const float ReleaseStopTime = 0.1f;
        // Where the player's choice from the pause menu is kept (PlayerPrefs, so per device).
        private const string ModeKey = "429 Game.ArmSwingMode";

        [Header("Arm Swing")]
        [Tooltip("How it starts for a new player. On: only swings made while holding the button move you. Off: no button, " +
                 "but only alternating swings count. Players can change it in the pause menu.")]
        [SerializeField] private bool requireButton = true;
        [Tooltip("The button to hold while swinging. Defaults to X on the left controller.")]
        [SerializeField] private InputActionProperty swingButton = new(new InputAction("Arm Swing", InputActionType.Button, "<XRController>{LeftHand}/primaryButton"));
        [Tooltip("Head: walk where you look. Hands: walk where your hands point, so you can look around while you walk.")]
        [SerializeField] private WalkDirection direction = WalkDirection.Head;
        [Tooltip("Swings slower than this, in metres per second, don't move you.")]
        [Min(0f)]
        [SerializeField] private float minSwingSpeed = 0.4f;
        [Tooltip("Swinging this fast or faster walks at full Move Speed, once the walk has built up.")]
        [Min(0.1f)]
        [SerializeField] private float fullSwingSpeed = 2.5f;
        [Tooltip("Swings that only get you ready, without moving you yet. A swing is one arm swinging forward.")]
        [Min(0)]
        [SerializeField] private int readySwings = 1;
        [Tooltip("The swing that reaches full Move Speed. Every swing after the ready ones adds speed until then.")]
        [Min(1)]
        [SerializeField] private int swingsToFullSpeed = 4;
        [Tooltip("Stop swinging for this long, in seconds (or let go of the button), and the next walk builds up from the start again.")]
        [Min(0.1f)]
        [SerializeField] private float swingGap = 1f;
        [Tooltip("Seconds to pick up speed when you start swinging.")]
        [Min(0.01f)]
        [SerializeField] private float speedUpTime = 0.15f;
        [Tooltip("Seconds to slow down when you stop swinging; long enough to carry you smoothly from one swing to the next.")]
        [Min(0.01f)]
        [SerializeField] private float slowDownTime = 0.35f;
        [Tooltip("The hands to watch. Left empty, the Left and Right Controllers are found automatically.")]
        [SerializeField] private Transform leftHand;
        [SerializeField] private Transform rightHand;

        private Transform head;
        private Transform handsForward;
        private Vector3 lastLeft;
        private Vector3 lastRight;
        private bool tracking;
        private float amount;
        private float nextSearch;
        private HandSwing leftSwing;
        private HandSwing rightSwing;
        private int swingCount;
        private float lastSwingTime;
        private SwingMode mode;

        /// <summary>How fast you're walking right now, from 0 to 1 (1 = full Move Speed).</summary>
        public float SwingAmount => amount;

        /// <summary>Hold the button, no button, or off. Remembered on this device when changed.</summary>
        public SwingMode Mode
        {
            get => mode;
            set
            {
                if (mode == value) return;

                mode = value;
                PlayerPrefs.SetInt(ModeKey, (int)value);
                PlayerPrefs.Save();
                ResetBuildUp();
            }
        }

        /// <summary>How far the walk has built up, from 0 (just started) to 1 (full speed allowed).</summary>
        public float BuildUp
        {
            get
            {
                if (swingCount <= readySwings) return 0f;
                if (swingsToFullSpeed <= readySwings) return 1f;
                return Mathf.Clamp01((float)(swingCount - readySwings) / (swingsToFullSpeed - readySwings));
            }
        }

        protected override void Awake()
        {
            base.Awake();
            // The arm swing drives this mover instead of a thumbstick.
            leftHandMoveInput.inputSourceMode = XRInputValueReader.InputSourceMode.ManualValue;
            leftHandMoveInput.manualValue = Vector2.zero;
            rightHandMoveInput.inputSourceMode = XRInputValueReader.InputSourceMode.Unused;

            int saved = PlayerPrefs.GetInt(ModeKey, (int)(requireButton ? SwingMode.HoldButton : SwingMode.NoButton));
            mode = saved >= (int)SwingMode.HoldButton && saved <= (int)SwingMode.Off ? (SwingMode)saved : SwingMode.HoldButton;
        }

        protected new void OnEnable()
        {
            base.OnEnable();
            swingButton.action?.Enable();
            tracking = false;
            amount = 0f;
            ResetBuildUp();
        }

        protected new void OnDisable()
        {
            base.OnDisable();
            // Only switch off our own button; a referenced action may be used elsewhere.
            if (swingButton.reference == null) swingButton.action?.Disable();
        }

        protected new void Update()
        {
            leftHandMoveInput.manualValue = new Vector2(0f, ReadSwing());
            base.Update();
        }

        private float ReadSwing()
        {
            var origin = mediator != null ? mediator.xrOrigin : null;
            float deltaTime = Time.deltaTime;
            if (mode == SwingMode.Off || deltaTime <= 0f || origin == null || !FindParts(origin))
            {
                // Off, paused or no rig: measure afresh afterwards, so hands moved in the meantime don't count as a swing.
                tracking = false;
                if (deltaTime > 0f) amount = 0f;
                return amount;
            }

            // Hands relative to the head, in the rig's own space: turning, teleporting or walking around the room
            // doesn't count as swinging, only the arms moving.
            var originTransform = origin.Origin.transform;
            var headPosition = originTransform.InverseTransformPoint(head.position);
            var left = originTransform.InverseTransformPoint(leftHand.position) - headPosition;
            var right = originTransform.InverseTransformPoint(rightHand.position) - headPosition;

            bool held = mode != SwingMode.HoldButton || (swingButton.action != null && swingButton.action.IsPressed());
            if (!held || Time.time - lastSwingTime > swingGap) ResetBuildUp();

            float target = 0f;
            if (tracking && held)
            {
                var forward = Vector3.ProjectOnPlane(originTransform.InverseTransformDirection(head.forward), Vector3.up);
                forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
                var leftVelocity = (left - lastLeft) / deltaTime;
                var rightVelocity = (right - lastRight) / deltaTime;
                target = Mathf.InverseLerp(minSwingSpeed, fullSwingSpeed, SwingSpeed(leftVelocity, rightVelocity, forward));
                if (target > 0f)
                {
                    CountSwing(ref leftSwing, ForwardSpeed(leftVelocity, forward));
                    CountSwing(ref rightSwing, ForwardSpeed(rightVelocity, forward));
                }
                // The first swings only get the walk going; full speed comes after a few.
                target *= BuildUp;
            }
            lastLeft = left;
            lastRight = right;
            tracking = true;

            float settleTime = !held ? ReleaseStopTime : (target > amount ? speedUpTime : slowDownTime);
            amount = Mathf.Lerp(amount, target, 1f - Mathf.Exp(-deltaTime / settleTime));
            if (amount < 0.01f) amount = 0f;

            if (direction == WalkDirection.Hands) AimAlongHands(originTransform);
            else if (handsForward != null && forwardSource == handsForward) forwardSource = null;
            return amount;
        }

        // Walking swings go forward and back, and up and down; sideways hand movement doesn't count.
        private float SwingSpeed(Vector3 leftVelocity, Vector3 rightVelocity, Vector3 forward)
        {
            if (mode == SwingMode.HoldButton)
            {
                float leftSpeed = new Vector2(Vector3.Dot(leftVelocity, forward), leftVelocity.y).magnitude;
                float rightSpeed = new Vector2(Vector3.Dot(rightVelocity, forward), rightVelocity.y).magnitude;
                return (leftSpeed + rightSpeed) * 0.5f;
            }

            // No button: only alternating swings count, one arm going forward and up while the other goes back and
            // down. A weapon swing moves one arm, or both the same way, so it doesn't walk you.
            float leftSpeedForward = ForwardSpeed(leftVelocity, forward);
            float rightSpeedForward = ForwardSpeed(rightVelocity, forward);
            return leftSpeedForward * rightSpeedForward < 0f ? Mathf.Min(Mathf.Abs(leftSpeedForward), Mathf.Abs(rightSpeedForward)) : 0f;
        }

        // Positive while the hand swings forward and up, negative while it swings back and down.
        private static float ForwardSpeed(Vector3 velocity, Vector3 forward)
        {
            return Vector3.Dot(velocity, forward) + velocity.y;
        }

        // A swing counts when a hand starts swinging forward fast enough; it has to swing back before it can count again.
        private void CountSwing(ref HandSwing state, float forwardSpeed)
        {
            if (forwardSpeed > minSwingSpeed)
            {
                if (state == HandSwing.Forward) return;

                state = HandSwing.Forward;
                swingCount = Mathf.Min(swingCount + 1, Mathf.Max(swingsToFullSpeed, readySwings + 1));
                lastSwingTime = Time.time;
            }
            else if (forwardSpeed < -minSwingSpeed)
            {
                state = HandSwing.Back;
            }
        }

        private void ResetBuildUp()
        {
            swingCount = 0;
            leftSwing = HandSwing.Still;
            rightSwing = HandSwing.Still;
            lastSwingTime = float.NegativeInfinity;
        }

        private void AimAlongHands(Transform originTransform)
        {
            if (handsForward == null)
            {
                handsForward = new GameObject("Arm Swing Direction").transform;
                handsForward.SetParent(transform, false);
            }

            var up = originTransform.up;
            var forward = Vector3.ProjectOnPlane(leftHand.forward + rightHand.forward, up);
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.ProjectOnPlane(head.forward, up);
            if (forward.sqrMagnitude < 0.0001f) return;

            handsForward.rotation = Quaternion.LookRotation(forward, up);
            forwardSource = handsForward;
        }

        private bool FindParts(XROrigin origin)
        {
            if (head == null && origin.Camera != null) head = origin.Camera.transform;
            if ((leftHand == null || rightHand == null) && Time.unscaledTime >= nextSearch)
            {
                nextSearch = Time.unscaledTime + 1f;
                if (leftHand == null) leftHand = XRRig.FindController(left: true);
                if (rightHand == null) rightHand = XRRig.FindController(left: false);
            }
            return head != null && leftHand != null && rightHand != null;
        }
    }
}

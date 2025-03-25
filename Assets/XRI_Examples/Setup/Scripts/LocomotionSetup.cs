using UnityEngine;
using UnityEngine.Events;

namespace XRI_Examples
{
    /// <summary>
    /// Provider for continuous movement in an XR environment
    /// </summary>
    public class ContinuousMoveProvider : MonoBehaviour
    {
        public bool enableStrafe = true;
        public float moveSpeed = 1.0f;
        public Transform forwardSource;
    }

    /// <summary>
    /// Provider for continuous turning in an XR environment
    /// </summary>
    public class ContinuousTurnProvider : MonoBehaviour
    {
        public float turnSpeed = 60.0f;
    }

    /// <summary>
    /// Provider for snap turning in an XR environment
    /// </summary>
    public class SnapTurnProvider : MonoBehaviour
    {
        public float turnAmount = 45.0f;
    }

    /// <summary>
    /// Enum defining the locomotion types available in the application
    /// </summary>
    public enum LocomotionType
    {
        MoveAndStrafe,
        TeleportAndTurn
    }

    /// <summary>
    /// Enum defining the turn styles available in the application
    /// </summary>
    public enum TurnStyle
    {
        Continuous,
        Snap
    }

    /// <summary>
    /// Manager class that controls the various locomotion providers
    /// </summary>
    public class LocomotionManager : MonoBehaviour
    {
        // Movement providers
        public ContinuousMoveProvider continuousMoveProvider;
        public ContinuousTurnProvider continuousTurnProvider;
        public SnapTurnProvider snapTurnProvider;
        public MonoBehaviour twoHandedGrabMoveProvider;

        // Locomotion settings
        public LocomotionType leftHandLocomotionType;
        public LocomotionType rightHandLocomotionType;
        public TurnStyle leftHandTurnStyle;
        public TurnStyle rightHandTurnStyle;
        
        // Movement options
        public bool enableComfortMode;
        public bool useGravity;
        public bool enableFly;
        public bool enableGrabMovement;

        public void SetMoveScheme(LocomotionType scheme, bool leftHand)
        {
            if (leftHand)
                leftHandLocomotionType = scheme;
            else
                rightHandLocomotionType = scheme;
                
            // Apply the changes to the appropriate providers
        }

        public void SetTurnStyle(TurnStyle style, bool leftHand)
        {
            if (leftHand)
                leftHandTurnStyle = style;
            else
                rightHandTurnStyle = style;
                
            // Apply the changes to the appropriate providers
        }
    }

    /// <summary>
    /// Interface for grab move providers to expose necessary properties
    /// </summary>
    public interface IGrabMoveProvider
    {
        float moveFactor { get; set; }
        bool enableScaling { get; set; }
    }

    /// <summary>
    /// XR UI component that represents a lever with on/off states
    /// </summary>
    public class XRLever : MonoBehaviour
    {
        // Events triggered when the lever is activated or deactivated
        public UnityEvent onLeverActivate = new UnityEvent();
        public UnityEvent onLeverDeactivate = new UnityEvent();
        
        // Current state of the lever
        private bool mValue;
        
        // Set the value of the lever and update visuals
        public void SetValue(bool value)
        {
            mValue = value;
            UpdateVisuals();
        }
        
        // Toggle the lever state
        public void Toggle()
        {
            mValue = !mValue;
            if (mValue)
                onLeverActivate.Invoke();
            else
                onLeverDeactivate.Invoke();
                
            UpdateVisuals();
        }
        
        private void UpdateVisuals()
        {
            // Update the visual representation of the lever based on mValue
        }
    }

    /// <summary>
    /// XR UI component that represents a slider with a continuous range of values
    /// </summary>
    public class XRSlider : MonoBehaviour
    {
        // Event triggered when the slider value changes
        [System.Serializable]
        public class ValueChangeEvent : UnityEvent<float> { }
        
        public ValueChangeEvent onValueChange = new ValueChangeEvent();
        
        // Current value of the slider
        private float mValue;
        
        // Set the value of the slider and update visuals
        public void SetValue(float value)
        {
            mValue = Mathf.Clamp01(value);  // Ensure value is between 0 and 1
            UpdateVisuals();
        }
        
        private void UpdateVisuals()
        {
            // Update the visual representation of the slider based on mValue
        }
    }

    /// <summary>
    /// XR UI component that represents a knob that can be rotated
    /// </summary>
    public class XRKnob : MonoBehaviour
    {
        // Event triggered when the knob value changes
        [System.Serializable]
        public class ValueChangeEvent : UnityEvent<float> { }
        
        public ValueChangeEvent onValueChange = new ValueChangeEvent();
        
        // Current rotation value of the knob (0-1 represents 0-360 degrees)
        private float mValue;
        
        // Set the value of the knob and update visuals
        public void SetValue(float value)
        {
            mValue = Mathf.Clamp01(value);
            UpdateVisuals();
        }
        
        private void UpdateVisuals()
        {
            // Update the visual representation of the knob based on mValue
        }
    }

    /// <summary>
    /// Controls the locomotion settings in the scene, connecting UI elements to the LocomotionManager
    /// </summary>
    public class LocomotionSetup : MonoBehaviour
    {
        [SerializeField] private LocomotionManager mManager = null;

        // UI component references
        [SerializeField] private XRLever mLeftHandMoveAndStrafeEnabler = null;
        [SerializeField] private XRLever mRightHandMoveAndStrafeEnabler = null;
        [SerializeField] private XRLever mLeftHandTeleportAndTurnEnabler = null;
        [SerializeField] private XRLever mRightHandTeleportAndTurnEnabler = null;
        [SerializeField] private XRLever mLeftHandSnapTurnEnabler = null;
        [SerializeField] private XRLever mRightHandSnapTurnEnabler = null;
        [SerializeField] private XRLever mLeftHandContinuousTurnEnabler = null;
        [SerializeField] private XRLever mRightHandContinuousTurnEnabler = null;
        
        [SerializeField] private XRLever mLeftHeadRelativeLever = null;
        [SerializeField] private XRLever mLeftHandRelativeLever = null;
        [SerializeField] private XRLever mRightHeadRelativeLever = null;
        [SerializeField] private XRLever mRightHandRelativeLever = null;
        
        [SerializeField] private XRSlider mMoveSpeedSlider = null;
        [SerializeField] private XRLever mStrafeEnabler = null;
        [SerializeField] private XRLever mComfortModeEnabler = null;
        [SerializeField] private XRLever mGravityEnabler = null;
        [SerializeField] private XRLever mFlyEnabler = null;
        
        [SerializeField] private XRKnob mTurnSpeedKnob = null;
        [SerializeField] private XRLever mTurnAroundEnabler = null;
        [SerializeField] private XRSlider mSnapTurnAmountSlider = null;
        
        [SerializeField] private XRLever mGrabMoveEnabler = null;
        [SerializeField] private XRSlider mGrabMoveRatioSlider = null;
        [SerializeField] private XRLever mScalingEnabler = null;

        private void OnEnable()
        {
            if (!ValidateManager())
                return;

            ConnectControlEvents();
            InitializeControls();
        }

        private void OnDisable()
        {
            if (!ValidateManager())
                return;

            DisconnectControlEvents();
        }

        private bool ValidateManager()
        {
            if (mManager == null)
            {
                Debug.LogWarning("LocomotionManager not assigned to LocomotionSetup.");
                return false;
            }
            return true;
        }

        private void ConnectControlEvents()
        {
            // Connect locomotion type controls
            if (mLeftHandMoveAndStrafeEnabler != null)
                mLeftHandMoveAndStrafeEnabler.onLeverActivate.AddListener(EnableLeftHandMoveAndStrafe);

            if (mRightHandMoveAndStrafeEnabler != null)
                mRightHandMoveAndStrafeEnabler.onLeverActivate.AddListener(EnableRightHandMoveAndStrafe);

            if (mLeftHandTeleportAndTurnEnabler != null)
                mLeftHandTeleportAndTurnEnabler.onLeverActivate.AddListener(EnableLeftHandTeleportAndTurn);

            if (mRightHandTeleportAndTurnEnabler != null)
                mRightHandTeleportAndTurnEnabler.onLeverActivate.AddListener(EnableRightHandTeleportAndTurn);

            // Connect turn style controls
            if (mLeftHandContinuousTurnEnabler != null)
                mLeftHandContinuousTurnEnabler.onLeverActivate.AddListener(EnableLeftHandContinuousTurn);

            if (mRightHandContinuousTurnEnabler != null)
                mRightHandContinuousTurnEnabler.onLeverActivate.AddListener(EnableRightHandContinuousTurn);

            if (mLeftHandSnapTurnEnabler != null)
                mLeftHandSnapTurnEnabler.onLeverActivate.AddListener(EnableLeftHandSnapTurn);

            if (mRightHandSnapTurnEnabler != null)
                mRightHandSnapTurnEnabler.onLeverActivate.AddListener(EnableRightHandSnapTurn);

            // Connect movement direction controls
            if (mLeftHeadRelativeLever != null)
                mLeftHeadRelativeLever.onLeverActivate.AddListener(SetLeftMovementDirectionHeadRelative);

            if (mLeftHandRelativeLever != null)
                mLeftHandRelativeLever.onLeverActivate.AddListener(SetLeftMovementDirectionHandRelative);

            if (mRightHeadRelativeLever != null)
                mRightHeadRelativeLever.onLeverActivate.AddListener(SetRightMovementDirectionHeadRelative);

            if (mRightHandRelativeLever != null)
                mRightHandRelativeLever.onLeverActivate.AddListener(SetRightMovementDirectionHandRelative);

            // Connect continuous movement controls
            if (mMoveSpeedSlider != null)
                mMoveSpeedSlider.onValueChange.AddListener(SetMoveSpeed);

            if (mStrafeEnabler != null)
            {
                mStrafeEnabler.onLeverActivate.AddListener(EnableStrafe);
                mStrafeEnabler.onLeverDeactivate.AddListener(DisableStrafe);
            }

            if (mComfortModeEnabler != null)
            {
                mComfortModeEnabler.onLeverActivate.AddListener(EnableComfort);
                mComfortModeEnabler.onLeverDeactivate.AddListener(DisableComfort);
            }

            if (mGravityEnabler != null)
            {
                mGravityEnabler.onLeverActivate.AddListener(EnableGravity);
                mGravityEnabler.onLeverDeactivate.AddListener(DisableGravity);
            }

            if (mFlyEnabler != null)
            {
                mFlyEnabler.onLeverActivate.AddListener(EnableFly);
                mFlyEnabler.onLeverDeactivate.AddListener(DisableFly);
            }

            // Connect turn controls
            if (mTurnSpeedKnob != null)
                mTurnSpeedKnob.onValueChange.AddListener(SetTurnSpeed);

            if (mTurnAroundEnabler != null)
            {
                mTurnAroundEnabler.onLeverActivate.AddListener(EnableTurnAround);
                mTurnAroundEnabler.onLeverDeactivate.AddListener(DisableTurnAround);
            }

            if (mSnapTurnAmountSlider != null)
                mSnapTurnAmountSlider.onValueChange.AddListener(SetSnapTurnAmount);

            // Connect grab move controls
            if (mGrabMoveEnabler != null)
            {
                mGrabMoveEnabler.onLeverActivate.AddListener(EnableGrabMove);
                mGrabMoveEnabler.onLeverDeactivate.AddListener(DisableGrabMove);
            }

            if (mGrabMoveRatioSlider != null)
                mGrabMoveRatioSlider.onValueChange.AddListener(SetGrabMoveRatio);

            if (mScalingEnabler != null)
            {
                mScalingEnabler.onLeverActivate.AddListener(EnableScaling);
                mScalingEnabler.onLeverDeactivate.AddListener(DisableScaling);
            }
        }

        private void DisconnectControlEvents()
        {
            // Implementation removed for brevity, follows the same pattern as ConnectControlEvents
            // but removing listeners instead of adding them
        }

        private void InitializeControls()
        {
            // Set initial UI states based on the current locomotion settings
            if (mLeftHandMoveAndStrafeEnabler != null)
                mLeftHandMoveAndStrafeEnabler.SetValue(mManager.leftHandLocomotionType == LocomotionType.MoveAndStrafe);

            if (mRightHandMoveAndStrafeEnabler != null)
                mRightHandMoveAndStrafeEnabler.SetValue(mManager.rightHandLocomotionType == LocomotionType.MoveAndStrafe);

            if (mLeftHandTeleportAndTurnEnabler != null)
                mLeftHandTeleportAndTurnEnabler.SetValue(mManager.leftHandLocomotionType == LocomotionType.TeleportAndTurn);

            if (mRightHandTeleportAndTurnEnabler != null)
                mRightHandTeleportAndTurnEnabler.SetValue(mManager.rightHandLocomotionType == LocomotionType.TeleportAndTurn);

            if (mLeftHandContinuousTurnEnabler != null)
                mLeftHandContinuousTurnEnabler.SetValue(mManager.leftHandTurnStyle == TurnStyle.Continuous);

            if (mRightHandContinuousTurnEnabler != null)
                mRightHandContinuousTurnEnabler.SetValue(mManager.rightHandTurnStyle == TurnStyle.Continuous);

            if (mLeftHandSnapTurnEnabler != null)
                mLeftHandSnapTurnEnabler.SetValue(mManager.leftHandTurnStyle == TurnStyle.Snap);

            if (mRightHandSnapTurnEnabler != null)
                mRightHandSnapTurnEnabler.SetValue(mManager.rightHandTurnStyle == TurnStyle.Snap);

            if (mManager.continuousMoveProvider != null)
            {
                if (mStrafeEnabler != null)
                    mStrafeEnabler.SetValue(mManager.continuousMoveProvider.enableStrafe);

                if (mMoveSpeedSlider != null)
                    mMoveSpeedSlider.SetValue(mManager.continuousMoveProvider.moveSpeed);
            }

            // Set initial comfort, gravity, fly state
            if (mComfortModeEnabler != null)
                mComfortModeEnabler.SetValue(mManager.enableComfortMode);

            if (mGravityEnabler != null)
                mGravityEnabler.SetValue(mManager.useGravity);

            if (mFlyEnabler != null)
                mFlyEnabler.SetValue(mManager.enableFly);

            // Set initial turn settings
            if (mManager.continuousTurnProvider != null)
            {
                if (mTurnSpeedKnob != null)
                    mTurnSpeedKnob.SetValue(mManager.continuousTurnProvider.turnSpeed / 180.0f);
            }

            if (mManager.snapTurnProvider != null)
            {
                if (mSnapTurnAmountSlider != null)
                    mSnapTurnAmountSlider.SetValue(mManager.snapTurnProvider.turnAmount / 90.0f);
            }

            // Set grab move settings
            if (mGrabMoveEnabler != null)
                mGrabMoveEnabler.SetValue(mManager.enableGrabMovement);

            if (mManager.twoHandedGrabMoveProvider != null && mManager.twoHandedGrabMoveProvider is IGrabMoveProvider grabMoveProvider)
            {
                if (mGrabMoveRatioSlider != null)
                    mGrabMoveRatioSlider.SetValue(grabMoveProvider.moveFactor);

                if (mScalingEnabler != null)
                    mScalingEnabler.SetValue(grabMoveProvider.enableScaling);
            }
        }

        public void EnableLeftHandMoveAndStrafe()
        {
            mManager.SetMoveScheme(LocomotionType.MoveAndStrafe, true);
        }

        public void EnableRightHandMoveAndStrafe()
        {
            mManager.SetMoveScheme(LocomotionType.MoveAndStrafe, false);
        }

        public void EnableLeftHandTeleportAndTurn()
        {
            mManager.SetMoveScheme(LocomotionType.TeleportAndTurn, true);
        }

        public void EnableRightHandTeleportAndTurn()
        {
            mManager.SetMoveScheme(LocomotionType.TeleportAndTurn, false);
        }

        public void EnableLeftHandContinuousTurn()
        {
            mManager.SetTurnStyle(TurnStyle.Continuous, true);
        }

        public void EnableRightHandContinuousTurn()
        {
            mManager.SetTurnStyle(TurnStyle.Continuous, false);
        }

        public void EnableLeftHandSnapTurn()
        {
            mManager.SetTurnStyle(TurnStyle.Snap, true);
        }

        public void EnableRightHandSnapTurn()
        {
            mManager.SetTurnStyle(TurnStyle.Snap, false);
        }

        public void SetLeftMovementDirectionHeadRelative()
        {
            if (mManager.continuousMoveProvider != null)
            {
                mManager.continuousMoveProvider.forwardSource = null; // Head-relative
            }
        }

        public void SetLeftMovementDirectionHandRelative()
        {
            if (mManager.continuousMoveProvider != null && GetLeftHandTransform() != null)
            {
                mManager.continuousMoveProvider.forwardSource = GetLeftHandTransform();
            }
        }

        public void SetRightMovementDirectionHeadRelative()
        {
            if (mManager.continuousMoveProvider != null)
            {
                mManager.continuousMoveProvider.forwardSource = null; // Head-relative
            }
        }

        public void SetRightMovementDirectionHandRelative()
        {
            if (mManager.continuousMoveProvider != null && GetRightHandTransform() != null)
            {
                mManager.continuousMoveProvider.forwardSource = GetRightHandTransform();
            }
        }

        private Transform GetLeftHandTransform()
        {
            // Implementation depends on your XR setup
            return null; // Replace with actual hand transform retrieval
        }

        private Transform GetRightHandTransform()
        {
            // Implementation depends on your XR setup
            return null; // Replace with actual hand transform retrieval
        }

        public void SetMoveSpeed(float sliderValue)
        {
            if (mManager.continuousMoveProvider != null)
            {
                mManager.continuousMoveProvider.moveSpeed = sliderValue;
            }
        }

        public void EnableStrafe()
        {
            if (mManager.continuousMoveProvider != null)
            {
                mManager.continuousMoveProvider.enableStrafe = true;
            }
        }

        public void DisableStrafe()
        {
            if (mManager.continuousMoveProvider != null)
            {
                mManager.continuousMoveProvider.enableStrafe = false;
            }
        }

        public void EnableComfort()
        {
            mManager.enableComfortMode = true;
        }

        public void DisableComfort()
        {
            mManager.enableComfortMode = false;
        }

        public void EnableGravity()
        {
            mManager.useGravity = true;
        }

        public void DisableGravity()
        {
            mManager.useGravity = false;
        }

        public void EnableFly()
        {
            mManager.enableFly = true;
        }

        public void DisableFly()
        {
            mManager.enableFly = false;
        }

        public void SetTurnSpeed(float knobValue)
        {
            if (mManager.continuousTurnProvider != null)
            {
                // Map knob value (0-1) to turn speed range (0-180)
                mManager.continuousTurnProvider.turnSpeed = knobValue * 180.0f;
            }
        }

        public void EnableTurnAround()
        {
            // Implement turn-around functionality here, if supported by your turn provider
        }

        public void DisableTurnAround()
        {
            // Implement turn-around functionality here, if supported by your turn provider
        }

        public void SetSnapTurnAmount(float newAmount)
        {
            if (mManager.snapTurnProvider != null)
            {
                // Map slider value (0-1) to turn amount range (0-90)
                mManager.snapTurnProvider.turnAmount = newAmount * 90.0f;
            }
        }

        public void EnableGrabMove()
        {
            mManager.enableGrabMovement = true;
        }

        public void DisableGrabMove()
        {
            mManager.enableGrabMovement = false;
        }

        public void SetGrabMoveRatio(float sliderValue)
        {
            if (mManager.twoHandedGrabMoveProvider != null && mManager.twoHandedGrabMoveProvider is IGrabMoveProvider grabMoveProvider)
            {
                grabMoveProvider.moveFactor = sliderValue;
            }
        }

        public void EnableScaling()
        {
            if (mManager.twoHandedGrabMoveProvider != null && mManager.twoHandedGrabMoveProvider is IGrabMoveProvider grabMoveProvider)
            {
                grabMoveProvider.enableScaling = true;
            }
        }

        public void DisableScaling()
        {
            if (mManager.twoHandedGrabMoveProvider != null && mManager.twoHandedGrabMoveProvider is IGrabMoveProvider grabMoveProvider)
            {
                grabMoveProvider.enableScaling = false;
            }
        }
    }
}
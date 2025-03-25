using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity;
//using Unity.XR.CoreUtils;
//using UnityEngine.XR.Interaction.Toolkit.Locomotion;
//using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
// Add any missing namespace for TwoHandedGrabMoveProvider
// using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

namespace XRI_Examples.Global.Scripts
{
    public class LocomotionManager : MonoBehaviour
    {
        [SerializeField]
        private ContinuousMoveProvider mContinuousMoveProvider;
        
        [SerializeField]
        private ContinuousTurnProvider mContinuousTurnProvider;
        
        [SerializeField]
        private SnapTurnProvider mSnapTurnProvider;
        
        [SerializeField]
        private MonoBehaviour mTwoHandedGrabMoveProvider; // Use MonoBehaviour if type isn't available
        
        [SerializeField]
        private LocomotionType mLeftHandLocomotionType = LocomotionType.MoveAndStrafe;
        
        [SerializeField]
        private LocomotionType mRightHandLocomotionType = LocomotionType.MoveAndStrafe;
        
        [SerializeField]
        private TurnStyle mLeftHandTurnStyle = TurnStyle.Smooth;
        
        [SerializeField]
        private TurnStyle mRightHandTurnStyle = TurnStyle.Smooth;
        
        [SerializeField] 
        private bool mEnableComfortMode;
        
        [SerializeField]
        private GravityProvider mGravityProvider;
        
        [SerializeField]
        private bool mUseGravity = true;
        
        [SerializeField]
        private bool mEnableFly;
        
        [SerializeField]
        private bool mEnableGrabMovement;

        public ContinuousMoveProvider continuousMoveProvider => mContinuousMoveProvider;
        public ContinuousTurnProvider continuousTurnProvider => mContinuousTurnProvider;
        public SnapTurnProvider snapTurnProvider => mSnapTurnProvider;
        public MonoBehaviour twoHandedGrabMoveProvider => mTwoHandedGrabMoveProvider;
        
        public LocomotionType leftHandLocomotionType
        {
            get => mLeftHandLocomotionType;
            set
            {
                mLeftHandLocomotionType = value;
                SetMoveScheme(value, true);
            }
        }
        
        public LocomotionType rightHandLocomotionType
        {
            get => mRightHandLocomotionType;
            set
            {
                mRightHandLocomotionType = value;
                SetMoveScheme(value, false);
            }
        }
        
        public TurnStyle leftHandTurnStyle
        {
            get => mLeftHandTurnStyle;
            set
            {
                mLeftHandTurnStyle = value;
                SetTurnStyle(value, true);
            }
        }
        
        public TurnStyle rightHandTurnStyle
        {
            get => mRightHandTurnStyle;
            set
            {
                mRightHandTurnStyle = value;
                SetTurnStyle(value, false);
            }
        }
        
        public bool enableComfortMode
        {
            get => mEnableComfortMode;
            set => mEnableComfortMode = value;
        }
        
        public bool useGravity
        {
            get => mUseGravity;
            set
            {
                mUseGravity = value;
                
                if (mGravityProvider != null)
                {
                    mGravityProvider.enabled = value;
                    
                    if (!value)
                    {
                        mEnableFly = true;
                    }
                }
            }
        }
        
        public bool enableFly
        {
            get => mEnableFly;
            set
            {
                mEnableFly = value;
                
                if (mContinuousMoveProvider != null)
                {
                    var propertyInfo = mContinuousMoveProvider.GetType().GetProperty("enableFly");
                    if (propertyInfo != null)
                    {
                        propertyInfo.SetValue(mContinuousMoveProvider, value);
                    }
                    
                    if (value)
                        mUseGravity = false;
                }
            }
        }
        
        public bool enableGrabMovement
        {
            get => mEnableGrabMovement;
            set
            {
                mEnableGrabMovement = value;
                if (mTwoHandedGrabMoveProvider != null)
                    mTwoHandedGrabMoveProvider.enabled = value;
            }
        }

        protected void OnEnable()
        {
            if (mGravityProvider != null)
            {
                mGravityProvider.enabled = mUseGravity;
                
                var propertyInfo = mContinuousMoveProvider.GetType().GetProperty("enableFly");
                if (propertyInfo != null)
                {
                    propertyInfo.SetValue(mContinuousMoveProvider, mEnableFly);
                }
            }

            if (mTwoHandedGrabMoveProvider != null)
                mTwoHandedGrabMoveProvider.enabled = mEnableGrabMovement;

            SetMoveScheme(mLeftHandLocomotionType, true);
            SetMoveScheme(mRightHandLocomotionType, false);
            SetTurnStyle(mLeftHandTurnStyle, true);
            SetTurnStyle(mRightHandTurnStyle, false);
        }

        private void SetMoveScheme(LocomotionType scheme, bool leftHand)
        {
            if (mContinuousMoveProvider == null)
                return;
                
            // Different approach that works with the API
            if (scheme == LocomotionType.NoMovement)
            {
                // Disable movement input completely
                mContinuousMoveProvider.enabled = false;
                return;
            }
            
            // For other schemes, enable the provider
            mContinuousMoveProvider.enabled = true;
            
            // Try to set specific properties if available
            try
            {
                bool moveEnabled = scheme != LocomotionType.NoMovement;
                
                // Try setting hand-specific properties if they exist
                var leftHandProperty = mContinuousMoveProvider.GetType().GetProperty("leftHandEnabled");
                var rightHandProperty = mContinuousMoveProvider.GetType().GetProperty("rightHandEnabled");
                
                if (leftHandProperty != null && rightHandProperty != null)
                {
                    if (leftHand)
                        leftHandProperty.SetValue(mContinuousMoveProvider, moveEnabled);
                    else
                        rightHandProperty.SetValue(mContinuousMoveProvider, moveEnabled);
                }
            }
            catch (System.Exception)
            {
                // Fallback if properties don't exist
                Debug.LogWarning("Unable to set specific hand movement settings - using generic enable/disable");
            }
        }

        public void SetTurnStyle(TurnStyle style, bool leftHand)
        {
            if (mContinuousTurnProvider == null || mSnapTurnProvider == null)
                return;
                
            switch (style)
            {
                case TurnStyle.Smooth:
                    mContinuousTurnProvider.enabled = true;
                    mSnapTurnProvider.enabled = false;
                    break;
                    
                case TurnStyle.Snap:
                    mContinuousTurnProvider.enabled = false;
                    mSnapTurnProvider.enabled = true;
                    break;
                    
                case TurnStyle.None:
                    mContinuousTurnProvider.enabled = false;
                    mSnapTurnProvider.enabled = false;
                    break;
            }
        }
    }

    public enum LocomotionType
    {
        MoveAndStrafe = 0,
        MoveAndRotate = 1,
        MoveAndTurn = 2,
        NoMovement = 3,
    }

    public enum TurnStyle
    {
        Smooth = 0,
        Snap = 1,
        None = 2,
    }
}
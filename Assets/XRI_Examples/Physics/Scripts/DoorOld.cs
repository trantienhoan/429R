using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using System.Collections;

namespace UnityEngine.XR.Content.Interaction
{
    public class DoorOld : MonoBehaviour
    {
        [SerializeField]
        HingeJoint m_DoorJoint;

        [SerializeField]
        [Tooltip("Transform joint that pulls a door to follow an interactor")]
        TransformJoint m_DoorPuller;

        [SerializeField]
        GameObject m_KeyKnob;

        [SerializeField]
        float m_HandleOpenValue = 0.1f;

        [SerializeField]
        float m_HandleCloseValue = 0.5f;

        [SerializeField]
        float m_HingeCloseAngle = 5.0f;

        [SerializeField]
        float m_KeyPullDistance = 0.1f;

        [SerializeField]
        [Tooltip("Events to fire when the door is locked.")]
        UnityEvent m_OnLock = new UnityEvent();

        [SerializeField]
        [Tooltip("Events to fire when the door is unlocked.")]
        UnityEvent m_OnUnlock = new UnityEvent();

        JointLimits m_OpenDoorLimits;
        JointLimits m_ClosedDoorLimits;
        bool m_Closed = true;
        float m_LastHandleValue = 1.0f;
        bool m_Locked = true; // Start locked by default

        GameObject m_KeySocket;
        UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable m_Key;

        UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor m_KnobInteractor;
        Transform m_KnobInteractorAttachTransform;

        /// <summary>
        /// Events to fire when the door is locked.
        /// </summary>
        public UnityEvent onLock => m_OnLock;

        /// <summary>
        /// Events to fire when the door is unlocked.
        /// </summary>
        public UnityEvent onUnlock => m_OnUnlock;

        [Header("Item Drop")]
        [SerializeField] private GameObject itemToDrop;
        [SerializeField] private Transform dropPosition;

        void Start()
        {
            m_OpenDoorLimits = m_DoorJoint.limits;
            m_ClosedDoorLimits = m_OpenDoorLimits;
            m_ClosedDoorLimits.min = 0.0f;
            m_ClosedDoorLimits.max = 0.0f;
            m_DoorJoint.limits = m_ClosedDoorLimits;
            m_KeyKnob.SetActive(false);
            m_Closed = true;
            m_Locked = true; // Ensure door starts locked
        }

        void Update()
        {
            // If the door is open, keep track of the hinge joint and see if it enters a state where it should close again
            if (!m_Closed)
            {
                if (m_LastHandleValue < m_HandleCloseValue)
                    return;

                if (Mathf.Abs(m_DoorJoint.angle) < m_HingeCloseAngle)
                {
                    m_DoorJoint.limits = m_ClosedDoorLimits;
                    m_Closed = true;
                }
            }

            // Handle key removal logic
            // if (m_KnobInteractor != null && m_KnobInteractorAttachTransform != null)
            // {
            //     var distance = (m_KnobInteractorAttachTransform.position - m_KeyKnob.transform.position).magnitude;
            //
            //     // If over threshold, break and grant the key back to the interactor
            //     if (distance > m_KeyPullDistance)
            //     {
            //         var newKeyInteractor = m_KnobInteractor;
            //         m_KeySocket.SetActive(true);
            //         m_Key.transform.gameObject.SetActive(true);
            //         newKeyInteractor.interactionManager.SelectEnter(newKeyInteractor, m_Key);
            //         m_KeyKnob.SetActive(false);
            //         
            //         // Lock the door when key is removed
            //         if (!m_Locked)
            //         {
            //             m_Locked = true;
            //             // Only force the door closed if it's already in a near-closed position
            //             if (Mathf.Abs(m_DoorJoint.angle) < m_HingeCloseAngle)
            //             {
            //                 m_DoorJoint.limits = m_ClosedDoorLimits;
            //                 m_Closed = true;
            //             }
            //             m_OnLock.Invoke();
            //         }
            //         
            //         // Reset references after key is removed
            //         m_KnobInteractor = null;
            //         m_KnobInteractorAttachTransform = null;
            //     }
            // }
        }

        public void BeginDoorPulling(SelectEnterEventArgs args)
        {
            if (!m_Locked) // Only allow pulling if door is unlocked
            {
                m_DoorPuller.connectedBody = args.interactorObject.GetAttachTransform(args.interactableObject);
                m_DoorPuller.enabled = true;
            }
        }

        public void EndDoorPulling()
        {
            m_DoorPuller.enabled = false;
            m_DoorPuller.connectedBody = null;
        }

        public void DoorHandleUpdate(float handleValue)
        {
            m_LastHandleValue = handleValue;

            if (!m_Closed || m_Locked)
                return;

            if (handleValue < m_HandleOpenValue)
            {
                m_DoorJoint.limits = m_OpenDoorLimits;
                m_Closed = false;
                StartCoroutine(DropItemAfterDelay(0.5f)); // Drop the item after door is open
            }
        }

        public void KeyDropUpdate(SelectEnterEventArgs args)
        {
            m_KeySocket = args.interactorObject.transform.gameObject;
            m_Key = args.interactableObject;

            // Get the Rigidbody component from the key
            Rigidbody keyRigidbody = m_Key.transform.gameObject.GetComponent<Rigidbody>();
            if (keyRigidbody != null)
            {
                // Set the Rigidbody to kinematic so it's not grabbable
                keyRigidbody.isKinematic = true;
            }

            // Disable the original key and socket, and enable the key knob
            m_KeySocket.SetActive(false);
            m_Key.transform.gameObject.SetActive(false);
            m_KeyKnob.SetActive(true);

            // Unlock the door immediately when key is inserted
            if (m_Locked)
            {
                m_Locked = false;
                m_OnUnlock.Invoke();
                Debug.Log("Door unlocked by key insertion");
            }
            
            // Set up key removal detection
            m_KnobInteractor = args.interactorObject as UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor;
            m_KnobInteractorAttachTransform = args.interactorObject.GetAttachTransform(args.interactableObject);
        }

        // This method is kept but simplified since we don't need key rotation
        public void KeyUpdate(float keyValue)
        {
            // Method kept for compatibility but not used for key rotation
            return;
        }
        
        // These methods can be removed as they're redundant with the logic in KeyDropUpdate
        // Kept for backward compatibility if they're connected to events
        public void KeyLockSelect(SelectEnterEventArgs args)
        {
            m_KnobInteractor = args.interactorObject as UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor;
            m_KnobInteractorAttachTransform = args.interactorObject.GetAttachTransform(args.interactableObject);
        }

        public void KeyLockDeselect(SelectExitEventArgs args)
        {
            // No need to reset references here as it's handled in the Update method when the key is removed
        }

        private IEnumerator DropItemAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            DropItem();
        }

        private void DropItem()
        {
            if (itemToDrop != null && dropPosition != null)
            {
                Instantiate(itemToDrop, dropPosition.position, dropPosition.rotation);
            }
        }
    }
}

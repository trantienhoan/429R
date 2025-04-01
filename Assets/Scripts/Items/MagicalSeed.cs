using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Items
{
    public class MagicalSeed : MonoBehaviour
    {
        private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;
        private Rigidbody rb;
        
        // Removed the unused isBeingHeld field

        private void Awake()
        {
            // Set the layer to Default
            gameObject.layer = LayerMask.NameToLayer("Default");
            
            // Setup Rigidbody
            rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }
            rb.useGravity = true;
            rb.isKinematic = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            // Setup Grab Interactable
            grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (grabInteractable == null)
            {
                grabInteractable = gameObject.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            }
            
            // Configure the grab interactable
            grabInteractable.movementType = UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable.MovementType.VelocityTracking;
            grabInteractable.throwOnDetach = true;
            grabInteractable.interactionLayers = InteractionLayerMask.GetMask("Default");
            
            // Set up attach transform if it doesn't exist
            Transform attachTransform = transform.Find("Attach");
            if (attachTransform == null)
            {
                GameObject attachPoint = new GameObject("Attach");
                attachPoint.transform.SetParent(transform);
                attachPoint.transform.localPosition = new Vector3(0, 0, 0.0256f);
                attachPoint.transform.localRotation = Quaternion.Euler(0, -180, 0);
                attachTransform = attachPoint.transform;
            }
            grabInteractable.attachTransform = attachTransform;

            // Add collider if it doesn't exist
            if (GetComponent<Collider>() == null)
            {
                // Fixed variable name to avoid hiding Component.collider property
                BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
                boxCollider.isTrigger = false;
                boxCollider.size = new Vector3(0.0068977354f, 0.051325835f, 0.13071556f);
                boxCollider.center = new Vector3(0.000000007683411f, 0, -0.03785309f);
            }

            // Subscribe to grab events - simplified since we're not tracking isBeingHeld anymore
            grabInteractable.selectEntered.AddListener(OnGrab);
            grabInteractable.selectExited.AddListener(OnRelease);
        }

        private void OnDestroy()
        {
            // Clean up event listeners
            if (grabInteractable != null)
            {
                grabInteractable.selectEntered.RemoveListener(OnGrab);
                grabInteractable.selectExited.RemoveListener(OnRelease);
            }
        }
        
        public void OnPlantedInPot()
        {
            // This method will be called by the TreeOfLightPot when the seed is placed
            Debug.Log("Seed has been planted in pot!");
            
            // Make sure the seed can't be grabbed again
            if (grabInteractable != null)
            {
                grabInteractable.enabled = false;
            }
            
            // The actual destruction will be handled by the TreeOfLightPot
            // as part of its growth sequence
        }

        private void OnGrab(SelectEnterEventArgs args)
        {
            // Visual feedback when grabbed could be added here if needed
            Debug.Log("Seed grabbed");
        }

        private void OnRelease(SelectExitEventArgs args)
        {
            // Visual feedback when released could be added here if needed
            Debug.Log("Seed released");
        }
    }
}
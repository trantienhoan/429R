using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Items
{
    public class MagicalSeed : MonoBehaviour
    {
        [Tooltip("The XRGrabInteractable component attached to this GameObject.")]
        [SerializeField] private XRGrabInteractable seedGrabInteractable;

        [Tooltip("The Rigidbody component attached to this GameObject.")]
        [SerializeField] private Rigidbody seedRigidbody;

        [Tooltip("The Collider component attached to this GameObject.")]
        [SerializeField] private Collider seedCollider;

        private void Awake()
        {
            // Ensure Rigidbody exists
            if (seedRigidbody == null)
            {
                seedRigidbody = GetComponent<Rigidbody>();
                if (seedRigidbody == null)
                {
                    Debug.LogError("Rigidbody is missing on MagicalSeed! Please add one manually.");
                    enabled = false; // Disable script if critical component is missing
                    return;
                }
            }
            seedRigidbody.useGravity = true;
            seedRigidbody.isKinematic = false;

            // Ensure Grab Interactable exists
            if (seedGrabInteractable == null)
            {
                seedGrabInteractable = GetComponent<XRGrabInteractable>();
                if (seedGrabInteractable == null)
                {
                    Debug.LogError("XRGrabInteractable is missing on MagicalSeed! Please add one manually.");
                    enabled = false; // Disable script if critical component is missing
                    return;
                }
            }

            // Ensure Collider exists
            if (seedCollider == null)
            {
                seedCollider = GetComponent<Collider>();
                if (seedCollider == null)
                {
                    Debug.LogError("Collider is missing on MagicalSeed! Please add one manually.");
                    enabled = false; // Disable script if critical component is missing
                    return;
                }
            }
        }

        private void OnEnable()
        {
            if (seedRigidbody != null)
            {
                seedRigidbody.isKinematic = false;
                seedRigidbody.useGravity = true;
            }
            if (seedGrabInteractable != null)
            {
                seedGrabInteractable.enabled = true;
            }
            if (seedCollider != null)
            {
                seedCollider.enabled = true;
            }
        }
    }
}
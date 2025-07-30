using UnityEngine;


namespace Items
{
    public class MagicalSeed : MonoBehaviour
    {
        [Tooltip("The XRGrabInteractable component attached to this GameObject.")]
        [SerializeField] private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable seedGrabInteractable;

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
                    enabled = false;
                    return;
                }
            }
            seedRigidbody.useGravity = true;
            seedRigidbody.isKinematic = false;
            seedRigidbody.interpolation = RigidbodyInterpolation.Interpolate;

            // Ensure Grab Interactable exists
            if (seedGrabInteractable == null)
            {
                seedGrabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
                if (seedGrabInteractable == null)
                {
                    Debug.LogError("XRGrabInteractable is missing on MagicalSeed! Please add one manually.");
                    enabled = false;
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
                    enabled = false;
                    //return;
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

        public void DisablePhysics()
        {
            if (seedRigidbody != null)
            {
                seedRigidbody.linearVelocity = Vector3.zero;
                seedRigidbody.angularVelocity = Vector3.zero;
                seedRigidbody.isKinematic = true;
                seedRigidbody.useGravity = false;
            }
            Collider[] colliders = GetComponents<Collider>();
            foreach (Collider col in colliders)
            {
                col.enabled = false;
            }
            if (seedGrabInteractable != null)
            {
                seedGrabInteractable.enabled = false;
            }
            Debug.Log($"[MagicalSeed {gameObject.name}] Physics disabled for absorption");
        }
    }
}
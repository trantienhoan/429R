using UnityEngine;

namespace VR
{

    public class PlayerPhysicsInteraction : MonoBehaviour
    {
        [Header("Physics Settings")] [SerializeField]
        private float pushForce = 5f;

        [SerializeField] private float pushRadius = 0.5f;
        [SerializeField] private LayerMask pushableLayer;
        [SerializeField] private LayerMask wallLayer;

        private CharacterController characterController;
        private Vector3 lastPosition;
        private Vector3 moveDirection;

        private void Start()
        {
            characterController = GetComponent<CharacterController>();
            lastPosition = transform.position;
        }

        private void Update()
        {
            // Calculate movement direction
            moveDirection = transform.position - lastPosition;
            lastPosition = transform.position;

            // Only process if we're actually moving
            if (moveDirection.magnitude > 0.01f)
            {
                PushObjects();
            }
        }

        private void PushObjects()
        {
            // Pre-allocate an array for colliders
            Collider[] hitColliders = new Collider[10]; // Adjust size based on expected maximum
    
            // Use non-allocating version
            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, pushRadius, hitColliders, pushableLayer);
    
            // Process only the valid hits
            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = hitColliders[i];
        
                // Skip if it's a wall
                if (((1 << hitCollider.gameObject.layer) & wallLayer) != 0)
                {
                    continue;
                }

                // Get the rigidbody if it exists
                Rigidbody rb = hitCollider.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    // Calculate push direction (away from player movement)
                    Vector3 pushDirection = moveDirection.normalized;
            
                    // Apply force to the object
                    rb.AddForce(pushDirection * pushForce, ForceMode.Impulse);
                }
            }
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            // Check if we hit a wall
            if (((1 << hit.gameObject.layer) & wallLayer) != 0)
            {
                // Prevent passing through walls
                Vector3 normal = hit.normal;
                Vector3 movement = moveDirection;
                Vector3 slideDirection = Vector3.ProjectOnPlane(movement, normal);

                // Move along the wall instead of through it
                characterController.Move(slideDirection * Time.deltaTime);
            }
        }
    }
}
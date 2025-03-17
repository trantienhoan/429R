using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
public class TestWeapon : MonoBehaviour
{
    [Header("Weapon Settings")]
    [SerializeField] private float weaponMass = 1f;
    [SerializeField] private float impactForceMultiplier = 1f;
    
    private Rigidbody rb;
    private Vector3 lastPosition;
    private float currentSpeed;
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;

    private void Awake()
    {
        // Get or add required components
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        // Get the grab interactable
        grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        if (grabInteractable == null)
        {
            grabInteractable = gameObject.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        }

        // Configure the grab interactable
        grabInteractable.movementType = UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable.MovementType.VelocityTracking;
        grabInteractable.throwOnDetach = false;
        grabInteractable.smoothPosition = true;
        grabInteractable.smoothRotation = true;
        grabInteractable.smoothPositionAmount = 10f;
        grabInteractable.smoothRotationAmount = 10f;
        grabInteractable.tightenPosition = 0.8f;
        grabInteractable.tightenRotation = 0.8f;
        grabInteractable.throwSmoothingDuration = 0.1f;
        grabInteractable.throwVelocityScale = 1f;
        grabInteractable.throwAngularVelocityScale = 1f;
        grabInteractable.retainTransformParent = true;

        // Configure rigidbody
        rb.mass = weaponMass;
        rb.useGravity = true;
        rb.isKinematic = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.maxAngularVelocity = 50f;
        rb.solverIterations = 6;
        rb.solverVelocityIterations = 2;

        // Subscribe to grab events
        grabInteractable.selectEntered.AddListener(OnGrab);
        grabInteractable.selectExited.AddListener(OnRelease);
        
        // Ensure weapon tag is set
        gameObject.tag = "Weapon";
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        // Make the weapon kinematic when grabbed
        rb.isKinematic = true;
        Debug.Log("Weapon grabbed - set to kinematic");
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        // Make the weapon non-kinematic when released
        rb.isKinematic = false;
        Debug.Log("Weapon released - set to non-kinematic");
    }

    private void FixedUpdate()
    {
        // Calculate speed for impact force
        currentSpeed = (transform.position - lastPosition).magnitude / Time.fixedDeltaTime;
        lastPosition = transform.position;
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Check if the hit object has a BreakableObject component
        if (collision.gameObject.TryGetComponent<Core.BreakableObject>(out var breakable))
        {
            float impactForce = currentSpeed * impactForceMultiplier;
            Debug.Log($"Weapon hit {collision.gameObject.name} with force: {impactForce}");
            breakable.TakeDamage(impactForce, collision.contacts[0].point, collision.contacts[0].normal);
        }
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
}

/* Prefab Setup Instructions:
1. Create a new empty GameObject named "TestHammer"
2. Create the hammer head:
   - Add a Cube, scale it to (0.15, 0.15, 0.25)
   - Position it at (0, 0, 0)
3. Create the handle:
   - Add a Cylinder, scale it to (0.05, 0.4, 0.05)
   - Rotate it 90 degrees on X axis
   - Position it at (0, 0, -0.3)
4. Add components to the parent object:
   - This TestWeapon script
   - XR Grab Interactable
   - Rigidbody (configured in Awake)
   - Box Collider (size to cover both parts)
5. Configure XR Grab Interactable:
   - Movement Type: Velocity Tracking
   - Throw On Detach: False
   - Smooth Position: True
   - Smooth Rotation: True
   - Smooth Position Amount: 10
   - Smooth Rotation Amount: 10
   - Tighten Position: 0.8
   - Tighten Rotation: 0.8
6. Set the tag to "Weapon"
7. Create prefab by dragging to Project window
*/ 
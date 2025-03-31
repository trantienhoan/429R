using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using Core;

[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
public class TestWeapon : MonoBehaviour
{
    [Header("Weapon Settings")]
    [SerializeField] private float weaponMass = 0.9f;
    [SerializeField] private float impactForceMultiplier = 1f;
    
    private Rigidbody rb;
    private Vector3 lastPosition;
    private float currentSpeed;
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;

    private void Awake()
    {
        Debug.Log($"TestWeapon: Awake called on {gameObject.name}");
        
        // Get required components
        rb = GetComponent<Rigidbody>();
        grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();

        // Subscribe to grab events
        grabInteractable.selectEntered.AddListener(OnGrab);
        grabInteractable.selectExited.AddListener(OnRelease);
        
        // Ensure weapon tag is set
        gameObject.tag = "Weapon";
        
        // Log final state
        Debug.Log($"TestWeapon: Setup complete on {gameObject.name}");
        Debug.Log($"TestWeapon: Has Rigidbody: {rb != null}");
        Debug.Log($"TestWeapon: Has XRGrabInteractable: {grabInteractable != null}");
        Debug.Log($"TestWeapon: Tag: {gameObject.tag}");

        if (rb != null)
        {
            rb.mass = weaponMass;
        }
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        Debug.Log($"TestWeapon: Grabbed by {args.interactorObject.transform.name}");
        rb.isKinematic = true;
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        Debug.Log($"TestWeapon: Released by {args.interactorObject.transform.name}");
        rb.isKinematic = false;
    }

    private void FixedUpdate()
    {
        // Calculate speed for impact force
        currentSpeed = (transform.position - lastPosition).magnitude / Time.fixedDeltaTime;
        lastPosition = transform.position;
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Calculate impact force based on collision impulse
        float impactForce = collision.impulse.magnitude * impactForceMultiplier;
        
        // Try to damage any type of breakable object
        var jiggleBreakable = collision.gameObject.GetComponent<JiggleBreakableBigObject>();
        if (jiggleBreakable != null)
        {
            jiggleBreakable.TakeDamage(impactForce, collision.contacts[0].point, collision.contacts[0].normal);
            return;
        }

        // Look for TreeOfLight in the collided object and its children
        var treeOfLight = collision.gameObject.GetComponentInChildren<TreeOfLight>();
        if (treeOfLight != null)
        {
            Debug.Log($"TestWeapon: Hit TreeOfLight with force {impactForce}");
            treeOfLight.TakeDamage(impactForce);
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
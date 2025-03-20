using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class GrabbableObject : MonoBehaviour
{
    [Header("Grab Settings")]
    public bool isGrabbable = true;
    public bool useGravity = true;
    public float mass = 1f;
    public float drag = 0.5f;
    public float angularDrag = 0.5f;
    
    [Header("Push Settings")]
    public bool canBePushed = true;
    public float pushMultiplier = 1f;

    [Header("Throw Settings")]
    public float throwForce = 10f;
    public float throwTorque = 5f;

    private Rigidbody rb;
    private bool isGrabbed = false;
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable interactable;
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private bool wasKinematic;
    private bool usedGravity;
    private Vector3 lastPosition;
    private Vector3 lastRotation;
    private float lastTime;

    private void Start()
    {
        // Ensure we have a Rigidbody
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        // Store initial state
        wasKinematic = rb.isKinematic;
        usedGravity = rb.useGravity;

        // Configure Rigidbody
        rb.mass = mass;
        rb.linearDamping = drag;
        rb.angularDamping = angularDrag;
        rb.useGravity = useGravity;

        // Add XR Interactable if it doesn't exist
        interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        if (interactable == null)
        {
            interactable = gameObject.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        }

        // Configure interactable
        interactable.interactionLayers = InteractionLayerMask.GetMask("Default");
        interactable.movementType = UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable.MovementType.VelocityTracking;
        interactable.throwOnDetach = true;
        interactable.smoothPosition = true;
        interactable.smoothRotation = true;
        interactable.smoothPositionAmount = 5f;
        interactable.smoothRotationAmount = 5f;
        interactable.tightenPosition = 0.5f;
        interactable.tightenRotation = 0.5f;

        // Subscribe to grab events
        interactable.selectEntered.AddListener(OnGrab);
        interactable.selectExited.AddListener(OnRelease);
    }

    private void OnDestroy()
    {
        if (interactable != null)
        {
            interactable.selectEntered.RemoveListener(OnGrab);
            interactable.selectExited.RemoveListener(OnRelease);
        }
    }

    private void Update()
    {
        if (isGrabbed)
        {
            // Calculate velocity and angular velocity
            float deltaTime = Time.time - lastTime;
            if (deltaTime > 0)
            {
                Vector3 velocity = (transform.position - lastPosition) / deltaTime;
                Vector3 angularVelocity = (transform.rotation.eulerAngles - lastRotation) / deltaTime;
                
                // Store for throw calculation
                lastPosition = transform.position;
                lastRotation = transform.rotation.eulerAngles;
                lastTime = Time.time;
            }
        }
    }

    public void OnGrab(SelectEnterEventArgs args)
    {
        if (!isGrabbable) return;
        
        isGrabbed = true;
        initialPosition = transform.position;
        initialRotation = transform.rotation;
        
        // Store and modify physics state
        wasKinematic = rb.isKinematic;
        usedGravity = rb.useGravity;
        rb.isKinematic = true;
        rb.useGravity = false;

        // Initialize tracking variables
        lastPosition = transform.position;
        lastRotation = transform.rotation.eulerAngles;
        lastTime = Time.time;
    }

    public void OnRelease(SelectExitEventArgs args)
    {
        if (!isGrabbed) return;
        
        isGrabbed = false;
        
        // Restore physics state
        rb.isKinematic = wasKinematic;
        rb.useGravity = usedGravity;
        
        // Calculate throw velocity
        float deltaTime = Time.time - lastTime;
        if (deltaTime > 0)
        {
            Vector3 throwVelocity = (transform.position - lastPosition) / deltaTime;
            Vector3 throwAngularVelocity = (transform.rotation.eulerAngles - lastRotation) / deltaTime;
            
            // Apply throw force
            rb.linearVelocity = throwVelocity * throwForce;
            rb.angularVelocity = throwAngularVelocity * throwTorque;
        }
    }

    public void OnPush(Vector3 force)
    {
        if (!canBePushed) return;
        
        rb.AddForce(force * pushMultiplier, ForceMode.Impulse);
    }

    private void OnValidate()
    {
        // Update Rigidbody properties in editor
        if (rb != null)
        {
            rb.mass = mass;
            rb.linearDamping = drag;
            rb.angularDamping = angularDrag;
            rb.useGravity = useGravity;
        }
    }
} 
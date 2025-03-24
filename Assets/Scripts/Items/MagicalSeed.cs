using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class MagicalSeed : MonoBehaviour
{
    [Header("Distance Settings")]
    [SerializeField] private float maxDistanceFromPlayer = 20f;
    [SerializeField] private float checkInterval = 1f;
    
    [Header("Respawn Settings")]
    [SerializeField] private GameObject seedPrefab;
    [SerializeField] private Transform respawnPoint;
    [SerializeField] private float respawnDelay = 2f;
    
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;
    private Rigidbody rb;
    private float checkTimer;
    private bool isBeingHeld;
    private bool isRespawning;

    private void Awake()
    {
        // Set the layer to Default (like the DoorKey)
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
        
        // Configure the grab interactable like DoorKey
        grabInteractable.movementType = UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable.MovementType.VelocityTracking;
        grabInteractable.throwOnDetach = true;
        grabInteractable.interactionLayers = InteractionLayerMask.GetMask("Default");
        
        // Set up attach transform if it doesn't exist
        Transform attachTransform = transform.Find("Attach");
        if (attachTransform == null)
        {
            GameObject attachPoint = new GameObject("Attach");
            attachPoint.transform.SetParent(transform);
            attachPoint.transform.localPosition = new Vector3(0, 0, 0.0256f); // Similar to DoorKey
            attachPoint.transform.localRotation = Quaternion.Euler(0, -180, 0); // Similar to DoorKey
            attachTransform = attachPoint.transform;
        }
        grabInteractable.attachTransform = attachTransform;

        // Add collider if it doesn't exist
        if (GetComponent<Collider>() == null)
        {
            BoxCollider collider = gameObject.AddComponent<BoxCollider>();
            collider.isTrigger = false;
            collider.size = new Vector3(0.0068977354f, 0.051325835f, 0.13071556f); // Similar to DoorKey
            collider.center = new Vector3(0.000000007683411f, 0, -0.03785309f); // Similar to DoorKey
        }

        // Subscribe to grab events
        grabInteractable.selectEntered.AddListener(OnGrab);
        grabInteractable.selectExited.AddListener(OnRelease);

        // Initialize check timer
        checkTimer = checkInterval;
    }

    private void Update()
    {
        if (!isBeingHeld && !isRespawning)
        {
            checkTimer -= Time.deltaTime;
            if (checkTimer <= 0)
            {
                CheckDistance();
                checkTimer = checkInterval;
            }
        }
    }

    private void CheckDistance()
    {
        // Find the player using tag
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        float distance = Vector3.Distance(transform.position, player.transform.position);
        
        if (distance > maxDistanceFromPlayer)
        {
            StartCoroutine(RespawnSeed());
        }
    }

    private System.Collections.IEnumerator RespawnSeed()
    {
        isRespawning = true;
        
        // Destroy current seed
        Destroy(gameObject);
        
        // Wait for respawn delay
        yield return new WaitForSeconds(respawnDelay);
        
        // Spawn new seed at respawn point
        if (seedPrefab != null && respawnPoint != null)
        {
            Instantiate(seedPrefab, respawnPoint.position, respawnPoint.rotation);
        }
        
        isRespawning = false;
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        isBeingHeld = true;
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        isBeingHeld = false;
    }
} 
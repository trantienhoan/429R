using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using System.Collections;
using UnityEngine.Events;

public class TreeOfLightPot : MonoBehaviour
{
    [Header("Tree Settings")]
    [SerializeField] private GameObject treePrefab;
    [SerializeField] private float growthDuration = 5f;
    [SerializeField] private float maxTreeScale = 1f;
    [SerializeField] private float growthDelay = 0.5f;
    
    [Header("Light Settings")]
    [SerializeField] private float initialLightIntensity = 0.1f;
    [SerializeField] private float growingLightIntensity = 0.3f;
    [SerializeField] private float finalLightIntensity = 2f;
    [SerializeField] private float finalLightPulseSpeed = 1f;
    [SerializeField] private float finalLightPulseAmount = 0.2f;
    
    [Header("Orientation Settings")]
    [SerializeField] private float uprightThreshold = 0.8f; // How close to vertical the pot needs to be (0-1)
    [SerializeField] private float checkInterval = 0.1f; // How often to check orientation
    
    [Header("Collider Settings")]
    [SerializeField] private float trunkHeight = 1.5f;    // Height of the trunk from base to top (measure in Unity)
    [SerializeField] private float trunkRadius = 0.15f;  // Half the width of the trunk at its widest point
    [SerializeField] private float topRadius = 0.4f;     // Half the width of the tree's top part
    [SerializeField] private float triggerColliderHeight = 0.5f;  // Height of the trigger collider
    [SerializeField] private float triggerColliderRadius = 0.3f;  // Radius of the trigger collider
    
    [Header("Physics Settings")]
    [SerializeField] private float potMass = 5f;
    [SerializeField] private float potDrag = 1f;
    [SerializeField] private float potAngularDrag = 0.5f;
    [SerializeField] private float potColliderHeight = 0.5f;
    [SerializeField] private float potColliderRadius = 0.3f;
    [SerializeField] private bool isPlanted = false;

    [Header("Jiggle Settings")]
    [SerializeField] private float jiggleAmount = 0.05f;
    [SerializeField] private float jiggleSpeed = 30f;
    [SerializeField] private float jiggleDamping = 8f;
    [SerializeField] private float uprightForce = 5f;  // Force to keep the pot upright
    private float jiggleTimer = 0f;
    private Vector3 jiggleVelocity = Vector3.zero;
    
    [Header("Events")]
    public UnityEvent onDamage;
    public UnityEvent onBreak;
    
    private GameObject currentTree;
    private bool isGrowing = false;
    private float growthTimer = 0f;
    private Transform socketPoint;
    private UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor socketInteractor;
    private bool wasUpright = false;
    private float lastCheckTime = 0f;
    private bool hasSeedBeenPlanted = false;
    private bool isBroken = false;
    private bool isFullyGrown = false;
    private bool isTreeBroken = false;
    private Animator animator;
    private Light treeLight;
    private ShadowMonsterSpawner monsterSpawner;
    [SerializeField] private CapsuleCollider trunkCollider;
    [SerializeField] private SphereCollider topCollider;
    private static readonly int GrowTrigger = Animator.StringToHash("Grow");
    private static readonly int GrowthSpeed = Animator.StringToHash("GrowthSpeed");
    private Vector3 originalTrunkPosition;
    private Vector3 originalTopPosition;

    [Header("Break Settings")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private GameObject explosionEffectPrefab;
    [SerializeField] private GameObject doorKeyPrefab;
    [SerializeField] private Transform keySpawnPoint;
    [SerializeField] private float breakDelay = 1f;
    [SerializeField] private float keyHoverHeight = 0.3f;  // Reduced from 1f to 0.3f for more subtle movement
    [SerializeField] private float keyHoverSpeed = 2f;     // Increased from 1f to 2f for faster movement
    [SerializeField] private float keyRotationSpeed = 60f; // Increased from 30f to 60f for faster rotation

    private float currentHealth;
    private GameObject spawnedKey;
    private Vector3 keyStartPosition; // Add this to store the initial position

    private void Awake()
    {
        // Find socket point
        socketPoint = transform.Find("TreeSpawnPoint");
        if (socketPoint == null)
        {
            socketPoint = transform;
        }

        // Get the socket interactor
        socketInteractor = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor>();
        if (socketInteractor != null)
        {
            socketInteractor.selectEntered.AddListener(OnSeedSocketed);
        }

        // Get the animator component
        animator = GetComponent<Animator>();
        // Get the light component
        treeLight = GetComponentInChildren<Light>();
        // Get the monster spawner
        monsterSpawner = FindObjectOfType<ShadowMonsterSpawner>();

        // Setup physics
        SetupRigidbody();
        // Setup colliders
        SetupColliders();
    }

    private void SetupRigidbody()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        rb.mass = potMass;
        rb.linearDamping = potDrag;
        rb.angularDamping = potAngularDrag;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        
        // Only freeze rotation X and Z, allow Y rotation for jiggle
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        // If planted, freeze position and make the tree kinematic
        if (isPlanted && currentTree != null)
        {
            rb.constraints |= RigidbodyConstraints.FreezePosition;
            Rigidbody treeRb = currentTree.GetComponent<Rigidbody>();
            if (treeRb != null)
            {
                treeRb.isKinematic = true;
            }
        }
    }

    private void SetupColliders()
    {
        // Find the child Collider GameObject
        Transform colliderTransform = transform.Find("TreeSpawnPoint/Collider");
        if (colliderTransform != null)
        {
            // Get the colliders from the child object
            trunkCollider = colliderTransform.GetComponent<CapsuleCollider>();
            topCollider = colliderTransform.GetComponent<SphereCollider>();

            // Ensure the child object is active
            colliderTransform.gameObject.SetActive(true);

            // Update collider properties
            UpdateColliderScale(0f); // Start with initial scale
        }
        else
        {
            Debug.LogWarning("Collider child object not found on Tree_Of_Light_Pot!");
        }

        // Find and preserve the trigger collider
        BoxCollider[] boxColliders = GetComponents<BoxCollider>();
        foreach (BoxCollider col in boxColliders)
        {
            if (col.isTrigger)
            {
                // Keep the trigger collider's original settings from the prefab
                col.enabled = true;
                break;
            }
        }
    }

    private void UpdateColliderScale(float growthProgress)
    {
        // Calculate current scale based on growth progress
        float currentScale = Mathf.Lerp(0.1f, 1f, growthProgress); // Start at 10% scale and grow to 100%

        // Update trunk collider
        if (trunkCollider != null)
        {
            // Scale the height and radius
            trunkCollider.height = 0.76f * currentScale;  // Final height is 0.76
            trunkCollider.radius = 0.12f * currentScale;  // Final radius is 0.12
            
            // Position the collider - scale from bottom up
            float currentHeight = 0.76f * currentScale;
            trunkCollider.center = new Vector3(0, currentHeight * 0.5f, 0);  // Center is half the height
            trunkCollider.isTrigger = false;
        }

        // Update top collider
        if (topCollider != null)
        {
            // Scale the radius
            topCollider.radius = 0.12f * currentScale;  // Final radius is 0.12
            
            // Position the collider - scale from bottom up
            float currentHeight = 0.76f * currentScale;  // Use trunk height for positioning
            topCollider.center = new Vector3(0, currentHeight + (0.86f - 0.76f) * currentScale, 0);  // Scale the offset from trunk
            topCollider.isTrigger = false;
        }
    }

    private void Update()
    {
        if (isGrowing && currentTree != null)
        {
            // Check orientation periodically
            if (Time.time >= lastCheckTime + checkInterval)
            {
                lastCheckTime = Time.time;
                bool isUpright = IsPotUpright();
                
                if (isUpright != wasUpright)
                {
                    wasUpright = isUpright;
                    if (isUpright)
                    {
                        // Resume growth
                        StartCoroutine(GrowTree());
                    }
                    else
                    {
                        // Pause growth
                        StopAllCoroutines();
                    }
                }
            }

            if (wasUpright)
            {
                growthTimer += Time.deltaTime;
                float growthProgress = Mathf.Clamp01(growthTimer / growthDuration);
                
                // Scale the tree
                float currentScale = Mathf.Lerp(0.1f, maxTreeScale, growthProgress);
                currentTree.transform.localScale = Vector3.one * currentScale;
                
                // Check if growth is complete
                if (growthProgress >= 1f)
                {
                    isGrowing = false;
                    // Ensure the tree stays at max scale
                    currentTree.transform.localScale = Vector3.one * maxTreeScale;
                }
            }
        }

        // Update jiggle effect and upright force
        if (jiggleTimer < jiggleDamping)
        {
            jiggleTimer += Time.deltaTime;
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                // Apply damping to jiggle velocity
                jiggleVelocity *= (1 - (jiggleTimer / jiggleDamping));
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, Time.deltaTime * jiggleDamping);
            }
        }

        // Apply upright force if planted
        if (isPlanted)
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                // Calculate the torque needed to keep the pot upright
                Vector3 currentUp = transform.up;
                Vector3 targetUp = Vector3.up;
                Vector3 rotationAxis = Vector3.Cross(currentUp, targetUp);
                float angle = Vector3.Angle(currentUp, targetUp);
                
                if (angle > 0.1f)
                {
                    rb.AddTorque(rotationAxis * uprightForce * angle, ForceMode.Force);
                }
            }
        }
    }

    private bool IsPotUpright()
    {
        // Check if the pot's up vector is close to world up
        float dotProduct = Vector3.Dot(transform.up, Vector3.up);
        return dotProduct >= uprightThreshold;
    }

    private void OnSeedSocketed(SelectEnterEventArgs args)
    {
        if (isGrowing || hasSeedBeenPlanted) return;

        // Get the seed component
        MagicalSeed seed = args.interactableObject.transform.GetComponent<MagicalSeed>();
        if (seed != null)
        {
            // Only start growing if the pot is upright
            if (IsPotUpright())
            {
                // Start growing
                isGrowing = true;
                wasUpright = true;
                hasSeedBeenPlanted = true;
                growthTimer = 0f; // Reset the growth timer

                // Disable the socket interactor to prevent other objects from being socketed
                if (socketInteractor != null)
                {
                    socketInteractor.enabled = false;
                }

                // Destroy the seed immediately
                Destroy(seed.gameObject);

                // Start growth sequence
                StartCoroutine(GrowTree());
            }
        }
    }

    private void OnDestroy()
    {
        // Clean up any existing seeds in the socket
        if (socketInteractor != null)
        {
            var interactables = socketInteractor.interactablesSelected;
            foreach (var interactable in interactables)
            {
                if (interactable != null)
                {
                    MagicalSeed seed = interactable.transform.GetComponent<MagicalSeed>();
                    if (seed != null)
                    {
                        Destroy(seed.gameObject);
                    }
                }
            }
        }
    }

    private void OnDisable()
    {
        // Clean up any existing seeds in the socket when disabled
        if (socketInteractor != null)
        {
            var interactables = socketInteractor.interactablesSelected;
            foreach (var interactable in interactables)
            {
                if (interactable != null)
                {
                    MagicalSeed seed = interactable.transform.GetComponent<MagicalSeed>();
                    if (seed != null)
                    {
                        Destroy(seed.gameObject);
                    }
                }
            }
        }
    }

    private IEnumerator GrowTree()
    {
        // Wait for initial delay
        yield return new WaitForSeconds(growthDelay);

        // Spawn the tree if it doesn't exist
        if (currentTree == null && treePrefab != null)
        {
            // Spawn at socket point and parent it
            currentTree = Instantiate(treePrefab);
            currentTree.transform.SetParent(socketPoint);
            currentTree.transform.localPosition = Vector3.zero;
            currentTree.transform.localRotation = Quaternion.identity;
            currentTree.transform.localScale = Vector3.zero;

            // Setup tree's Rigidbody
            Rigidbody treeRb = currentTree.GetComponent<Rigidbody>();
            if (treeRb != null)
            {
                treeRb.isKinematic = isPlanted; // Make kinematic if pot is planted
            }
        }

        // Start the growth animation
        if (animator != null)
        {
            animator.SetFloat(GrowthSpeed, 1f / growthDuration);
            animator.SetTrigger(GrowTrigger);
        }

        // Start spawning shadow monsters
        if (monsterSpawner != null)
        {
            monsterSpawner.StartSpawning();
        }
        
        // Grow the tree
        float elapsedTime = 0f;
        while (elapsedTime < growthDuration)
        {
            if (!wasUpright) yield break; // Stop if pot is knocked over
            
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / growthDuration;
            
            // Scale the tree
            float currentScale = Mathf.Lerp(0.1f, maxTreeScale, t);
            currentTree.transform.localScale = Vector3.one * currentScale;

            // Update light intensity
            if (treeLight != null)
            {
                // During the first 97% of growth, light stays dim
                if (t < 0.97f)
                {
                    treeLight.intensity = Mathf.Lerp(initialLightIntensity, growingLightIntensity, t / 0.97f);
                }
                // In the final 3%, light dramatically increases
                else
                {
                    float finalT = (t - 0.97f) / 0.03f;
                    treeLight.intensity = Mathf.Lerp(growingLightIntensity, finalLightIntensity, finalT);
                }
            }

            // Update collider scale
            UpdateColliderScale(t);
            
            yield return null;
        }

        // Final state
        isFullyGrown = true;
        currentTree.transform.localScale = Vector3.one * maxTreeScale;
        
        if (treeLight != null)
        {
            treeLight.intensity = finalLightIntensity;
        }

        // Stop spawning monsters when fully grown
        if (monsterSpawner != null)
        {
            monsterSpawner.StopSpawning();
        }

        // If the pot is already broken when the tree finishes growing, spawn the key
        if (isBroken && isTreeBroken)
        {
            SpawnKey();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.impulse.magnitude > 0.1f)
        {
            // Apply jiggle effect
            Vector3 hitDirection = collision.contacts[0].normal;
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                // If planted, apply a much smaller jiggle
                float currentJiggleAmount = isPlanted ? jiggleAmount * 0.2f : jiggleAmount;
                
                // Only apply force in the horizontal plane
                Vector3 horizontalForce = new Vector3(hitDirection.x, 0, hitDirection.z).normalized;
                rb.AddForce(horizontalForce * currentJiggleAmount * jiggleSpeed, ForceMode.Impulse);
                
                jiggleTimer = 0f;
                jiggleVelocity = horizontalForce * currentJiggleAmount * jiggleSpeed;
            }
        }
    }

    public void SetPlanted(bool planted)
    {
        isPlanted = planted;
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            if (planted)
            {
                rb.constraints |= RigidbodyConstraints.FreezePosition;
                // Make the tree kinematic when planted
                if (currentTree != null)
                {
                    Rigidbody treeRb = currentTree.GetComponent<Rigidbody>();
                    if (treeRb != null)
                    {
                        treeRb.isKinematic = true;
                    }
                }
            }
            else
            {
                rb.constraints &= ~RigidbodyConstraints.FreezePosition;
                // Make the tree non-kinematic when unplanted
                if (currentTree != null)
                {
                    Rigidbody treeRb = currentTree.GetComponent<Rigidbody>();
                    if (treeRb != null)
                    {
                        treeRb.isKinematic = false;
                    }
                }
            }
        }
    }

    public void TakeDamage(float damage)
    {
        if (isBroken) return;
        
        currentHealth = Mathf.Max(0, currentHealth - damage);
        
        if (currentHealth <= 0)
        {
            Break();
        }
    }

    public void OnTreeBreak()
    {
        isTreeBroken = true;
        // If we're already broken and the tree was fully grown, spawn the key
        if (isBroken && isFullyGrown)
        {
            SpawnKey();
        }
    }

    public void Break()
    {
        if (isBroken) return;
        
        isBroken = true;
        isGrowing = false;
        hasSeedBeenPlanted = false;
        growthTimer = 0f;
        wasUpright = false;
        
        // Play break effects
        if (explosionEffectPrefab != null)
        {
            Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);
        }

        // Break the tree if it exists and isn't already broken
        if (currentTree != null && !isTreeBroken)
        {
            TreeOfLight treeOfLight = currentTree.GetComponent<TreeOfLight>();
            if (treeOfLight != null)
            {
                treeOfLight.Break();
            }
        }

        // If the tree is already broken and we were fully grown, spawn the key
        if (isTreeBroken && isFullyGrown)
        {
            SpawnKey();
        }
        else
        {
            // If not fully grown or tree isn't broken yet, just disable the pot
            StartCoroutine(DisablePot());
        }
    }

    private IEnumerator DisablePot()
    {
        // Wait for effects to play
        yield return new WaitForSeconds(0.5f);
        
        // Disable the pot
        gameObject.SetActive(false);
    }

    private void SpawnKey()
    {
        if (doorKeyPrefab != null && keySpawnPoint != null)
        {
            // Spawn the key slightly above the spawn point to avoid physics interactions
            Vector3 spawnPosition = keySpawnPoint.position + Vector3.up * 0.5f;
            GameObject key = Instantiate(doorKeyPrefab, spawnPosition, keySpawnPoint.rotation);
            spawnedKey = key;
            keyStartPosition = spawnPosition; // Store the initial position

            // Make the key kinematic and disable its collider temporarily
            Rigidbody keyRb = key.GetComponent<Rigidbody>();
            if (keyRb != null)
            {
                keyRb.isKinematic = true;
                keyRb.useGravity = false;
            }

            // Disable any colliders temporarily
            Collider[] colliders = key.GetComponents<Collider>();
            foreach (Collider col in colliders)
            {
                col.enabled = false;
            }

            // Start the hover animation
            StartCoroutine(HoverAndRotateKey());
        }
        else
        {
            Debug.LogError("Door key prefab or spawn point not assigned!");
        }
    }

    private IEnumerator HoverAndRotateKey()
    {
        if (spawnedKey == null) yield break;

        float timeOffset = Random.Range(0f, 2f * Mathf.PI); // Random starting phase

        // Wait a short moment before enabling physics
        yield return new WaitForSeconds(0.5f);

        // Re-enable physics and colliders
        Rigidbody keyRb = spawnedKey.GetComponent<Rigidbody>();
        if (keyRb != null)
        {
            keyRb.isKinematic = false;
            keyRb.useGravity = true;
        }

        Collider[] colliders = spawnedKey.GetComponents<Collider>();
        foreach (Collider col in colliders)
        {
            col.enabled = true;
        }

        while (spawnedKey != null && spawnedKey.activeInHierarchy)
        {
            // Calculate hover offset using a smoother sine wave
            float hoverOffset = Mathf.Sin(Time.time * keyHoverSpeed + timeOffset) * keyHoverHeight;
            
            // Update position while maintaining the original X and Z coordinates
            Vector3 newPosition = keyStartPosition;
            newPosition.y += hoverOffset;
            spawnedKey.transform.position = newPosition;

            // Rotate around Y axis
            spawnedKey.transform.Rotate(Vector3.up, keyRotationSpeed * Time.deltaTime);

            yield return null;
        }
    }

    public void SetFullyGrown(bool value)
    {
        isFullyGrown = value;
    }

    private void OnGrowthComplete()
    {
        // Find the tree component
        TreeOfLight treeOfLight = FindObjectOfType<TreeOfLight>();
        if (treeOfLight != null)
        {
            treeOfLight.SetFullyGrown(true);
        }
    }
} 
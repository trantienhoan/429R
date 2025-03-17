using UnityEngine;
using Core;

[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(Rigidbody))]
public class SimpleBreakableBox : InstantBreakableObject
{
    // Inspector settings for visual feedback
    [Header("Visual Settings")]
    [SerializeField] private Material intactMaterial;
    [SerializeField] private Material breakingMaterial;
    
    private MeshRenderer meshRenderer;
    private BoxCollider boxCollider;
    private float initialHealth;

    [Header("Box Settings")]
    [SerializeField] private float boxMass = 1f;
    [SerializeField] private Vector3 boxSize = new Vector3(1f, 1f, 1f);
    [SerializeField] private Material boxMaterial;

    protected override void Awake()
    {
        Debug.Log($"SimpleBreakableBox: Awake called on {gameObject.name}");
        base.Awake();
        
        // Get components
        meshRenderer = GetComponent<MeshRenderer>();
        boxCollider = GetComponent<BoxCollider>();
        
        if (meshRenderer == null)
        {
            Debug.LogError("SimpleBreakableBox requires a MeshRenderer component!");
            return;
        }

        // Set initial material
        if (intactMaterial != null)
        {
            meshRenderer.material = intactMaterial;
        }

        initialHealth = health;

        // Ensure proper physics setup
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.useGravity = true;
        rb.isKinematic = false;
        rb.mass = boxMass;
        rb.linearDamping = 0.5f; // Add some drag to prevent excessive bouncing
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.maxAngularVelocity = 50f;
        rb.solverIterations = 6;
        rb.solverVelocityIterations = 2;

        // Ensure proper collision setup
        boxCollider.isTrigger = false;

        Debug.Log($"SimpleBreakableBox: Setup completed for {gameObject.name}");

        // Create the box mesh
        GameObject boxObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        boxObject.transform.SetParent(transform);
        boxObject.transform.localPosition = Vector3.zero;
        boxObject.transform.localScale = boxSize;
        
        // Set the material if provided
        if (boxMaterial != null)
        {
            boxObject.GetComponent<Renderer>().material = boxMaterial;
        }
        
        // Add the health bar
        GameObject healthBarPrefab = Resources.Load<GameObject>("Prefabs/BreakableHealthBar");
        if (healthBarPrefab != null)
        {
            GameObject healthBar = Instantiate(healthBarPrefab, transform);
            healthBar.GetComponent<BreakableHealthBar>().Initialize(this);
        }
    }

    protected override void OnBreakingStart()
    {
        // Visual feedback when breaking starts
        if (breakingMaterial != null && meshRenderer != null)
        {
            meshRenderer.material = breakingMaterial;
        }
    }

    protected override void OnBreak()
    {
        // Disable collider when broken
        if (boxCollider != null)
        {
            boxCollider.enabled = false;
        }

        // Optional: Add particle effects, sound, etc. here
        
        // Make the object invisible
        if (meshRenderer != null)
        {
            meshRenderer.enabled = false;
        }

        // Note: We're keeping the GameObject alive for debug purposes
        // In a real game, you might want to destroy it after effects finish playing
    }

    public override void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitDirection)
    {
        Debug.Log($"SimpleBreakableBox: Taking damage: {damage} on {gameObject.name}");
        base.TakeDamage(damage, hitPoint, hitDirection);

        // Visual feedback
        if (meshRenderer != null && breakingMaterial != null)
        {
            float healthPercentage = currentHealth / initialHealth;
            if (healthPercentage < 0.5f && meshRenderer.material != breakingMaterial)
            {
                meshRenderer.material = breakingMaterial;
            }
        }
    }

    // Helper method to setup the box in editor
    private void Reset()
    {
        Debug.Log("SimpleBreakableBox: Reset called in editor");
        // Default values for easy testing
        health = 100f;
        dropForce = 3f;
        destroyDelay = 1f;
        
        // Set break settings
        SetBreakSettings(5f, true, true);

        // Ensure components
        if (!TryGetComponent<BoxCollider>(out _))
        {
            Debug.Log("SimpleBreakableBox: Adding BoxCollider");
            gameObject.AddComponent<BoxCollider>();
        }
        
        if (!TryGetComponent<Rigidbody>(out _))
        {
            Debug.Log("SimpleBreakableBox: Adding Rigidbody");
            Rigidbody rb = gameObject.AddComponent<Rigidbody>();
            rb.useGravity = true;
            rb.isKinematic = false;
            rb.mass = 1f;
            rb.linearDamping = 0.5f;
        }

        if (!TryGetComponent<MeshRenderer>(out _))
        {
            Debug.Log("SimpleBreakableBox: Adding MeshFilter and MeshRenderer");
            MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
            meshFilter.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            gameObject.AddComponent<MeshRenderer>();
        }
    }

    private void OnEnable()
    {
        Debug.Log($"SimpleBreakableBox: OnEnable called on {gameObject.name}");
    }

    private void Start()
    {
        Debug.Log($"SimpleBreakableBox: Start called on {gameObject.name}");
    }
}

/* Prefab Setup Instructions:
1. Create a new Cube in your scene
2. Add this SimpleBreakableBox script
3. Ensure it has these components:
   - Box Collider
   - Mesh Filter (should be added with cube)
   - Mesh Renderer (should be added with cube)
4. Set up materials:
   - Create/assign an intact material (e.g., blue)
   - Create/assign a breaking material (e.g., red)
5. Configure the Rigidbody:
   - Add Rigidbody component
   - Mass: 1
   - Use Gravity: true
   - Is Kinematic: false
6. Set the tag to "Breakable"
7. Create prefab by dragging to Project window
*/ 
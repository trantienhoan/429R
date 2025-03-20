using UnityEngine;

public class PhysicsSetup : MonoBehaviour
{
    [Header("Layer Setup")]
    [SerializeField] private LayerMask pushableLayer;
    [SerializeField] private LayerMask wallLayer;
    
    [Header("Physics Material")]
    [SerializeField] private PhysicsMaterial pushableMaterial;
    [SerializeField] private PhysicsMaterial wallMaterial;

    private void Start()
    {
        SetupPhysicsLayers();
        SetupPhysicsMaterials();
    }

    private void SetupPhysicsLayers()
    {
        // Set up layer collision matrix
        for (int i = 0; i < 32; i++)
        {
            for (int j = 0; j < 32; j++)
            {
                // Walls should collide with everything
                if ((wallLayer & (1 << i)) != 0 || (wallLayer & (1 << j)) != 0)
                {
                    Physics.IgnoreLayerCollision(i, j, false);
                }
                // Pushable objects should collide with walls and other pushable objects
                else if ((pushableLayer & (1 << i)) != 0 && (pushableLayer & (1 << j)) != 0)
                {
                    Physics.IgnoreLayerCollision(i, j, false);
                }
                // Otherwise, ignore collisions between pushable objects and other layers
                else if ((pushableLayer & (1 << i)) != 0 || (pushableLayer & (1 << j)) != 0)
                {
                    Physics.IgnoreLayerCollision(i, j, true);
                }
            }
        }
    }

    private void SetupPhysicsMaterials()
    {
        // Create pushable material if it doesn't exist
        if (pushableMaterial == null)
        {
            pushableMaterial = new PhysicsMaterial("Pushable")
            {
                dynamicFriction = 0.6f,
                staticFriction = 0.6f,
                bounciness = 0.2f,
                frictionCombine = PhysicsMaterialCombine.Average,
                bounceCombine = PhysicsMaterialCombine.Average
            };
        }

        // Create wall material if it doesn't exist
        if (wallMaterial == null)
        {
            wallMaterial = new PhysicsMaterial("Wall")
            {
                dynamicFriction = 1f,
                staticFriction = 1f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Maximum,
                bounceCombine = PhysicsMaterialCombine.Maximum
            };
        }
    }

    // Helper method to set up a pushable object
    public void SetupPushableObject(GameObject obj)
    {
        // Add rigidbody if it doesn't exist
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = obj.AddComponent<Rigidbody>();
        }

        // Configure rigidbody
        rb.useGravity = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Add collider if it doesn't exist
        Collider collider = obj.GetComponent<Collider>();
        if (collider == null)
        {
            collider = obj.AddComponent<BoxCollider>();
        }

        // Set up collider
        collider.material = pushableMaterial;
        obj.layer = LayerMask.NameToLayer("Pushable");
    }

    // Helper method to set up a wall object
    public void SetupWallObject(GameObject obj)
    {
        // Add collider if it doesn't exist
        Collider collider = obj.GetComponent<Collider>();
        if (collider == null)
        {
            collider = obj.AddComponent<BoxCollider>();
        }

        // Set up collider
        collider.material = wallMaterial;
        collider.isTrigger = false;
        obj.layer = LayerMask.NameToLayer("Wall");
    }
} 
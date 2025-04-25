using UnityEngine;
using Core;

[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(Rigidbody))]
public class SimpleBreakableBox : InstantBreakableObject
{
    private BoxCollider boxCollider;

    [Header("Box Settings")]
    [SerializeField] private float boxMass = 1f;
    [SerializeField] private Vector3 boxSize = new Vector3(1f, 1f, 1f);
    //[SerializeField] private Material boxMaterial;

    protected override void Awake()
    {
        base.Awake();

        // Get components
        boxCollider = GetComponent<BoxCollider>();

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
    }

    public override void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitDirection)
    {
        //Debug.Log($"SimpleBreakableBox: Taking damage: {damage} on {gameObject.name}");
    }

    // Helper method to setup the box in editor
    private void Reset()
    {
        //Debug.Log("SimpleBreakableBox: Reset called in editor");

        // Ensure components
        if (!TryGetComponent<BoxCollider>(out _))
        {
            //Debug.Log("SimpleBreakableBox: Adding BoxCollider");
            gameObject.AddComponent<BoxCollider>();
        }

        if (!TryGetComponent<Rigidbody>(out _))
        {
            //Debug.Log("SimpleBreakableBox: Adding Rigidbody");
            Rigidbody rb = gameObject.AddComponent<Rigidbody>();
            rb.useGravity = true;
            rb.isKinematic = false;
            rb.mass = 1f;
            rb.linearDamping = 0.5f;
        }

        // Only add MeshFilter and MeshRenderer if they don't exist
        if (!TryGetComponent<MeshFilter>(out _))
        {
            //Debug.Log("SimpleBreakableBox: Adding MeshFilter and MeshRenderer");
            MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
            meshFilter.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            gameObject.AddComponent<MeshRenderer>();
        }
    }
}
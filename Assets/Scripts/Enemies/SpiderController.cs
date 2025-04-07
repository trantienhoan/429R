using UnityEngine;

namespace Enemies
{
[DefaultExecutionOrder(0)]
public class SpiderController : MonoBehaviour
{
    // Movement properties
    [SerializeField] private float maxSpeed = 5f;
    [SerializeField] private float acceleration = 40f;
    [SerializeField] private float friction = 5f;
    
    // Internal state
    private Vector2 velocityNoAdd = Vector2.zero;
    private Vector2 addVelocity = Vector2.zero;
    private Vector2 velocity = Vector2.zero;
    private float speed = 0;
    private float speedProgress = 0;
    
    // Surface detection/movement
    [SerializeField] private Transform arcTransformRotation;
    [SerializeField, Range(0, 360)] private float arcAngle = 270;
    [SerializeField] private int arcResolution = 6;
    [SerializeField] private LayerMask arcLayer;
    
    // VR considerations - Adjustable distance for comfortable VR experience
    [Tooltip("This is the minimum distance to the player in VR. The spider will not move closer than this distance to avoid VR discomfort.")]
    [SerializeField] private float minimumVRComfortDistance = 1.5f; // Adjust as needed

    // VR considerations - Added check to ensure proper collision detection and prevention of clipping with the player
    [Tooltip("This is the layer mask used to detect collision with the VR player. Ensure the VR player's GameObject is on a layer included in this mask.")]
    [SerializeField] private LayerMask vrPlayerCollisionLayer; // Set this to the VR player's layer


    // Public properties to replace Player3D's properties
    public Vector2 VelocityNoAdd { 
        get => velocityNoAdd;
        set {
            velocityNoAdd = value;
            UpdateVelocity();
        }
    }
    public Vector2 Velocity { get => velocity; }
    public Vector3 Velocity3 { get => new Vector3(velocity.x, 0, velocity.y); }
    public float Speed { get => speed; }
    public float SpeedProgress { get => speedProgress; }
    
    // Direction control
    private Vector2 movementDirection = Vector2.zero;

    private void Awake()
    {
        if (arcTransformRotation == null)
            arcTransformRotation = transform;
    }
    
    private void OnDisable()
    {
        velocityNoAdd = Vector2.zero;
        UpdateVelocity();
    }
    
    private void Update()
    {
        ApplyVelocity();
    }
    
    private void FixedUpdate()
    {
        ApplyMovementDirection();
        ApplyFriction();
        UpdateVelocity();
    }
    
    // Called by ShadowMonsterSpider to set the direction
    public void SetMovementDirection(Vector2 direction)
    {
        movementDirection = direction.normalized;
    }
    
    // Add temporary velocity (for knockback etc)
    public void AddVelocity(Vector2 additionalVelocity)
    {
        addVelocity += additionalVelocity;
        UpdateVelocity();
    }
    
    // Reset additional velocity
    public void ResetAddVelocity()
    {
        addVelocity = Vector2.zero;
        UpdateVelocity();
    }
    
    private void ApplyMovementDirection()
    {
        if (movementDirection != Vector2.zero)
        {
            // Get a reference to the VR player's transform - adjust tag and null check to be more flexible if needed
            GameObject vrPlayer = GameObject.FindGameObjectWithTag("Player");
            if (vrPlayer == null)
            {
                Debug.LogWarning("VRPlayer not found in scene. Make sure to tag your VR player GameObject with 'VRPlayer'.");
                velocityNoAdd += Time.fixedDeltaTime * acceleration * movementDirection; // Apply movement even if VR player is not found
            }
            else
            {
                // Check distance to the VR player
                float distanceToPlayer = Vector3.Distance(transform.position, vrPlayer.transform.position);
                if (distanceToPlayer > minimumVRComfortDistance)
                {
                    velocityNoAdd += Time.fixedDeltaTime * acceleration * movementDirection;
                }
                else
                {
                    // Optionally: Add some behaviour here, such as stopping movement or wandering around.
                    velocityNoAdd = Vector2.zero; // Stop moving closer
                }
            }
        }
    }
    
    private void ApplyFriction()
    {
        velocityNoAdd -= Time.fixedDeltaTime * friction * velocityNoAdd;
    }
    
    private void UpdateVelocity()
    {
        velocity = velocityNoAdd + addVelocity;
        UpdateSpeed();
    }
    
    private void UpdateSpeed()
    {
        speed = velocity.magnitude;
        speedProgress = Mathf.Clamp01(speed / maxSpeed);
    }
    
    private float ClampAngle(float angle, float min, float max)
    {
        angle = Mathf.Repeat(angle, 360);
        min = Mathf.Repeat(min, 360);
        max = Mathf.Repeat(max, 360);
        if (min > max)
        {
            float temp = min;
            min = max;
            max = temp;
        }
        return Mathf.Clamp(angle, min, max);
    }

    private void ApplyVelocity()
    {
        if (velocity == Vector2.zero)
            return;
        
        float arcRadius = speed * Time.deltaTime;
        Vector3 worldVelocity = arcTransformRotation.TransformVector(Velocity3);
        Debug.Log($"velocity: {velocity}, speed: {speed}, worldVelocity: {worldVelocity}");
        
        if (PhysicsExtension.ArcCast(transform.position, Quaternion.LookRotation(worldVelocity, arcTransformRotation.up), arcAngle, arcRadius, arcResolution, arcLayer, out RaycastHit hit))
        {
            Debug.Log("ArcCast hit something!");
            Debug.Log("Hit Point: " + hit.point);
            Debug.Log("Hit Normal: " + hit.normal);
            transform.position = hit.point;
            transform.MatchUp(hit.normal);
        }
        else
        {
            Debug.Log("ArcCast did not hit anything!");
        }
        Vector3 clampedRotation = transform.eulerAngles;
        //clampedRotation.x = ClampAngle(clampedRotation.x, -90, 90); // Adjust the angle limits as needed
        //clampedRotation.z = ClampAngle(clampedRotation.z, -90, 90); // Adjust the angle limits as needed
        transform.rotation = Quaternion.Euler(clampedRotation);
    }
}
}
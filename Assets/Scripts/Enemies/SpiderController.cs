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
        
        // Z-axis movement
        [SerializeField] private float zMoveSpeed = 2f; // Speed for Z-axis movement
        private float currentZVelocity = 0f;

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

        // Direction control
        private Vector2 movementDirection = Vector2.zero;
        private float zMovementInput = 0f; // Input for Z-axis movement
        
        public Vector2 Velocity { get => velocity; }
        public Vector3 Velocity3 { get => new Vector3(velocity.x, 0, velocity.y); }
        public float Speed { get => speed; }
        public float SpeedProgress { get => speedProgress; }

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
            ApplyZMovement();
        }

        // Called by ShadowMonsterSpider to set the direction
        public void SetMovementDirection(Vector2 direction)
        {
            direction.x = 0;

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
                velocityNoAdd += Time.fixedDeltaTime * acceleration * movementDirection;

                // Clamp velocity to maxSpeed
                velocityNoAdd = Vector2.ClampMagnitude(velocityNoAdd, maxSpeed);
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

        private void ApplyVelocity()
        {
            if (velocity == Vector2.zero)
                return;

            float arcRadius = speed * Time.deltaTime;
            Vector3 worldVelocity = arcTransformRotation.TransformVector(Velocity3);
            worldVelocity.x = 0;
            //Debug.Log($"velocity: {velocity}, speed: {speed}, worldVelocity: {worldVelocity}");

            if (PhysicsExtension.ArcCast(transform.position, Quaternion.LookRotation(worldVelocity, arcTransformRotation.up), arcAngle, arcRadius, arcResolution, arcLayer, out RaycastHit hit))
            {
                //Debug.Log("ArcCast hit something!");
                //Debug.Log("Hit Point: " + hit.point);
                //Debug.Log("Hit Normal: " + hit.normal);
                transform.position = hit.point;
                transform.MatchUp(hit.normal);
            }
            else
            {
                //Debug.Log("ArcCast did not hit anything!");
            }
            Vector3 clampedRotation = transform.eulerAngles;
            transform.rotation = Quaternion.Euler(clampedRotation);
        }
        private void ApplyZMovement()
        {
            // Calculate the target Z velocity based on input
            float targetZVelocity = zMovementInput * zMoveSpeed;

            // Smoothly interpolate the current Z velocity towards the target velocity
            currentZVelocity = Mathf.Lerp(currentZVelocity, targetZVelocity, Time.fixedDeltaTime * 10f); // You can adjust the smoothing factor (10f)

            // Apply the Z movement
            Vector3 zMovement = transform.forward * currentZVelocity * Time.fixedDeltaTime;
            transform.position += zMovement;
        }
    }
}
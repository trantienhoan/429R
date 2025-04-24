using UnityEngine;
using Core;

[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(HealthComponent))]
public class JiggleBreakableBigObject : BreakableObject
{
    [Header("Jiggle Settings")]
    [SerializeField] private float jiggleAmount = 0.1f;
    [SerializeField] private float jiggleDuration = 0.5f;
    [SerializeField] private AnimationCurve jiggleCurve;

    [Header("Physics Settings")]
    [SerializeField] private float objectMass = 50f; // Increased mass

    private BoxCollider boxCollider;
    private Rigidbody rigidBody;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private float jiggleTimer = 0f;
    private bool isJiggling = false;
    private Vector3 jiggleDirection;

    protected override void Awake()
    {
        base.Awake();

        // Cache components
        boxCollider = GetComponent<BoxCollider>();
        rigidBody = GetComponent<Rigidbody>();
		healthComponent = GetComponent<HealthComponent>();

        // Store initial values
        originalPosition = transform.position;
        originalRotation = transform.rotation;

        // Setup physics
        SetupPhysics();

        // Add the health bar if needed
        GameObject healthBarPrefab = Resources.Load<GameObject>("Prefabs/BreakableHealthBar");
        if (healthBarPrefab != null)
        {
            GameObject healthBar = Instantiate(healthBarPrefab, transform);
            healthBar.GetComponent<BreakableHealthBar>().Initialize(this);
        }

		// Subscribe to OnDeath event
        healthComponent.OnDeath += HandleHealthDeath; // SUBSCRIBE TO ONDEATH
    }

	private HealthComponent healthComponent;

    private void SetupPhysics()
    {
        // Configure rigidbody
        rigidBody.useGravity = true;
        rigidBody.isKinematic = false;
        rigidBody.mass = objectMass;
        rigidBody.linearDamping = 0.5f;
        rigidBody.interpolation = RigidbodyInterpolation.Interpolate;
        rigidBody.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rigidBody.maxAngularVelocity = 50f;
        rigidBody.solverIterations = 8;
        rigidBody.solverVelocityIterations = 3;

        // Configure collider
        boxCollider.isTrigger = false;
        // Collider size should be set manually in the editor
    }

    private void Update()
    {
        // Handle jiggle animation if active
        if (isJiggling)
        {
            jiggleTimer += Time.deltaTime;
            float progress = jiggleTimer / jiggleDuration;

            if (progress < 1.0f)
            {
                float curveValue = jiggleCurve != null ? jiggleCurve.Evaluate(progress) : Mathf.Sin(progress * Mathf.PI * 4);
                Vector3 offset = jiggleDirection * jiggleAmount * curveValue;
                transform.position = originalPosition + offset;
            }
            else
            {
                // Reset position when jiggle is complete
                transform.position = originalPosition;
                transform.rotation = originalRotation;
                isJiggling = false;
            }
        }
    }

    protected override void OnCollisionEnter(Collision collision)
    {
        // Skip if collision layer is not in our mask
        if ((collisionLayers.value & (1 << collision.gameObject.layer)) == 0)
            return;

        bool isWeapon = collision.gameObject.CompareTag("Weapon");
        float impactForce = collision.impulse.magnitude;

        // Check break conditions
        bool shouldBreak = false;

        if (breakOnlyFromWeapons)
        {
            shouldBreak = isWeapon && (!useImpactForce || impactForce >= minimumImpactForce);
        }
        else
        {
            shouldBreak = isWeapon || (useImpactForce && impactForce >= minimumImpactForce);
        }

        if (shouldBreak)
        {
            Vector3 hitPoint = collision.contacts[0].point;
            Vector3 hitDirection = collision.contacts[0].normal;

            // Trigger jiggle effect
            TriggerJiggleEffect(hitDirection);

            // Calculate damage based on impact force and weapon status
            float damage;
            if (useImpactForce)
            {
                // Scale damage based on impact force, with a minimum damage
                damage = Mathf.Max(impactForce * damageMultiplier, minimumImpactForce);
            }
            else
            {
                // If not using impact force, apply fixed damage
                damage = 100f * 0.2f; // Take 20% of health as damage
            }

            // Apply damage
            TakeDamage(damage, hitPoint, hitDirection);
        }
    }

    public override void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitDirection) // ADD hitPoint PARAMETER
    {
        healthComponent.TakeDamage(damage, hitPoint, this.gameObject);
    }

    private void TriggerJiggleEffect(Vector3 hitDirection)
    {
        // Reset jiggle timer and set direction
        jiggleTimer = 0f;
        jiggleDirection = -hitDirection.normalized; // Jiggle away from hit
        isJiggling = true;

        // Store original position and rotation
        originalPosition = transform.position;
        originalRotation = transform.rotation;
    }

    protected override void HandleDestruction()
    {
        // Disable collider immediately
        if (boxCollider != null)
        {
            boxCollider.enabled = false;
        }

        base.HandleDestruction();
    }
	private void HandleHealthDeath(HealthComponent health)
	{
		HandleBreaking();
	}

    protected override void HandleBreaking()
    {
        // Drop items - using the original method from the base class
        DropItems(transform.position);

        // Let the base class handle the rest
        Break();
    }

    protected override void Break()
    {
        base.Break();
    }

    // Helper method to setup the object in editor
    private void Reset()
    {
        dropForce = 5f;
        destroyDelay = 2f;

        // Set default jiggle settings
        jiggleAmount = 0.1f;
        jiggleDuration = 0.5f;

        // Create a default jiggle curve if none exists
        if (jiggleCurve == null || jiggleCurve.keys.Length == 0)
        {
            jiggleCurve = new AnimationCurve(
                new Keyframe(0, 0),
                new Keyframe(0.25f, 1),
                new Keyframe(0.5f, -0.8f),
                new Keyframe(0.75f, 0.6f),
                new Keyframe(1, 0)
            );
        }

        // Set the tag to "Breakable"
        gameObject.tag = "Breakable";

        // Ensure components
        if (!TryGetComponent<BoxCollider>(out _))
        {
            gameObject.AddComponent<BoxCollider>();
        }

        if (!TryGetComponent<Rigidbody>(out _))
        {
            Rigidbody rb = gameObject.AddComponent<Rigidbody>();
            rb.useGravity = true;
            rb.isKinematic = false;
            rb.mass = objectMass;
            rb.linearDamping = 0.5f;
        }
    }

    // Public method to set break settings
	private void OnDestroy()
	{
		if (healthComponent != null)
        {
            healthComponent.OnDeath -= HandleHealthDeath;
        }
	}
}
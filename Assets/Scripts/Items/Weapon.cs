using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using Core;

[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
public class Weapon : MonoBehaviour
{
    [Header("Weapon Base Settings")]
    [SerializeField] private WeaponType weaponType = WeaponType.Sword;
    [SerializeField] private float baseDamage = 10f;
    [SerializeField] private float weaponMass = 0.9f;
    [SerializeField] private float impactForceMultiplier = 1f;
    [SerializeField] private float treeOfLightDamageMultiplier = 0.5f;
    [SerializeField] private float shadowMonsterDamageMultiplier = 1.5f;

    [Header("Movement Detection")]
    [SerializeField] private float swingDetectionThreshold = 2.5f; // Min velocity for swing detection
    [SerializeField] private float verticalSwingThreshold = 0.8f;  // How vertical a swing must be (dot product)
    [SerializeField] private float horizontalSwingThreshold = 0.8f; // How horizontal a swing must be
    
    [Header("Effects")]
    [SerializeField] private GameObject groundImpactEffectPrefab;
    [SerializeField] private GameObject airSlashEffectPrefab;
    
    // Internal tracking
    private Rigidbody rb;
    private Vector3 lastPosition;
    private Vector3 movementDirection;
    private float currentSpeed;
    private Vector3 velocityHistory = Vector3.zero;
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;
    private bool isGrabbed = false;
    
    public enum WeaponType
    {
        Sword,
        Hammer,
        Axe,
        Spear
    }
    
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        
        grabInteractable.selectEntered.AddListener(OnGrab);
        grabInteractable.selectExited.AddListener(OnRelease);
        
        gameObject.tag = "Weapon";
        
        if (rb != null)
        {
            rb.mass = weaponMass;
        }
    }
    
    private void OnGrab(SelectEnterEventArgs args)
    {
        isGrabbed = true;
        rb.isKinematic = true;
    }
    
    private void OnRelease(SelectExitEventArgs args)
    {
        isGrabbed = false;
        rb.isKinematic = false;
    }
    
    private void FixedUpdate()
    {
        // Calculate current speed and direction
        Vector3 deltaPosition = transform.position - lastPosition;
        currentSpeed = deltaPosition.magnitude / Time.fixedDeltaTime;
        
        if (deltaPosition.magnitude > 0.001f)
        {
            movementDirection = deltaPosition.normalized;
        }
        
        // Smooth velocity tracking
        velocityHistory = Vector3.Lerp(velocityHistory, deltaPosition / Time.fixedDeltaTime, 0.5f);
        
        lastPosition = transform.position;
        
        // Detect weapon swing patterns if being held
        if (isGrabbed && currentSpeed > swingDetectionThreshold)
        {
            DetectWeaponSwingPattern();
        }
    }
    
    private void DetectWeaponSwingPattern()
    {
        // Get world up and right vectors for reference
        Vector3 worldUp = Vector3.up;
        Vector3 worldRight = Vector3.right;
        
        // Check type of swing
        float verticalDot = Vector3.Dot(movementDirection, -worldUp);
        float horizontalDot = Mathf.Abs(Vector3.Dot(movementDirection, worldRight));
        
        // Detect overhead swing (hammer special)
        if (weaponType == WeaponType.Hammer && verticalDot > verticalSwingThreshold)
        {
            // Will trigger special effect on collision
        }
        
        // Detect horizontal slash (sword special)
        if (weaponType == WeaponType.Sword && horizontalDot > horizontalSwingThreshold)
        {
            // Will trigger special effect on collision
        }
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        // Ignore collisions when not held
        if (!isGrabbed) return;
        
        // Calculate impact force and damage
        float impactForce = collision.impulse.magnitude * impactForceMultiplier;
        float damageAmount = baseDamage * (currentSpeed / 5f); // Scale damage with speed
        Vector3 collisionPoint = collision.contacts[0].point;
        Vector3 collisionNormal = collision.contacts[0].normal;
        
        // Check collision with ground for Hammer
        if (weaponType == WeaponType.Hammer && 
            Vector3.Dot(movementDirection, -Vector3.up) > verticalSwingThreshold &&
            collision.gameObject.CompareTag("Ground"))
        {
            // Spawn ground crack effect
            if (groundImpactEffectPrefab != null)
            {
                Instantiate(groundImpactEffectPrefab, collisionPoint, 
                    Quaternion.LookRotation(collisionNormal));
            }
            
            // Do AOE damage
            ApplyAreaDamage(collisionPoint, 3f, damageAmount * 1.5f);
        }
        
        // Check for horizontal sword swing
        if (weaponType == WeaponType.Sword && 
            Mathf.Abs(Vector3.Dot(movementDirection, Vector3.right)) > horizontalSwingThreshold)
        {
            // Create air slash effect
            if (airSlashEffectPrefab != null)
            {
                GameObject airSlash = Instantiate(airSlashEffectPrefab, transform.position, 
                    Quaternion.LookRotation(movementDirection));
                
                // Setup projectile behavior (if needed)
                Projectile projectile = airSlash.AddComponent<Projectile>();
                if (projectile != null)
                {
                    projectile.Initialize(movementDirection, 15f, damageAmount);
                }
            }
        }
        
        // Try to damage different types of objects
        
        // Breakable objects
        var breakableObject = collision.gameObject.GetComponent<JiggleBreakableObject>();
        if (breakableObject != null)
        {
            breakableObject.TakeDamage(damageAmount, collisionPoint, collisionNormal);
            return;
        }
        
        // TreeOfLight - takes less damage
        var treeOfLight = collision.gameObject.GetComponentInChildren<TreeOfLight>();
        if (treeOfLight != null)
        {
            treeOfLight.TakeDamage(damageAmount * treeOfLightDamageMultiplier);
            return;
        }
        
        // Shadow Monster - takes more damage
        var shadowMonster = collision.gameObject.GetComponentInChildren<ShadowMonster>();
        if (shadowMonster != null)
        {
            shadowMonster.TakeDamage(damageAmount * shadowMonsterDamageMultiplier);
            return;
        }
    }
    
    // For hammer AOE damage
    private void ApplyAreaDamage(Vector3 center, float radius, float damage)
    {
        Collider[] hitColliders = Physics.OverlapSphere(center, radius);
        foreach (var hitCollider in hitColliders)
        {
            // Check for breakable objects
            var breakable = hitCollider.GetComponent<JiggleBreakableObject>();
            if (breakable != null)
            {
                // Calculate distance-based damage falloff
                float distance = Vector3.Distance(center, hitCollider.transform.position);
                float damageWithFalloff = damage * (1f - (distance / radius));
                
                Vector3 direction = (hitCollider.transform.position - center).normalized;
                breakable.TakeDamage(damageWithFalloff, hitCollider.transform.position, direction);
            }
            
            // Similar code for other damageable types
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

// Simple projectile class for air slash effects
public class Projectile : MonoBehaviour
{
    private Vector3 direction;
    private float speed;
    private float damage;
    private float lifespan = 1.5f;
    
    public void Initialize(Vector3 dir, float spd, float dmg)
    {
        direction = dir;
        speed = spd;
        damage = dmg;
        Destroy(gameObject, lifespan);
    }
    
    private void Update()
    {
        transform.position += direction * speed * Time.deltaTime;
    }
    
    private void OnTriggerEnter(Collider other)
    {
        // Apply damage to hit objects
        var breakable = other.GetComponent<JiggleBreakableObject>();
        if (breakable != null)
        {
            breakable.TakeDamage(damage, transform.position, direction);
        }
        
        // Similar code for other damageable types
        
        // Optionally destroy projectile on impact
        Destroy(gameObject);
    }
}
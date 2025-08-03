using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using Core;

namespace Items
{
    [RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
    public class Weapon : MonoBehaviour
    {
        [Header("Weapon Base Settings")]
        [SerializeField] private WeaponType weaponType = WeaponType.Sword;
        [SerializeField] private float baseDamage = 5f;
        [SerializeField] private float minDamage = 5f; // Minimum damage to prevent zero
        [SerializeField] private float maxDamage = 30f;  // New: Cap to prevent overkill
        [SerializeField] private float weaponMass = 0.9f;
        [SerializeField] private float impactForceMultiplier = 1f;
        [SerializeField] private float treeOfLightDamageMultiplier = 0.5f;
        [SerializeField] private float shadowMonsterDamageMultiplier = 1.5f;

        [Header("Movement Detection")]
        [SerializeField] private float swingDetectionThreshold = 2.5f;
        [SerializeField] private float verticalSwingThreshold = 0.8f;
        [SerializeField] private float horizontalSwingThreshold = 0.8f;

        [Header("Effects")]
        [SerializeField] private GameObject groundImpactEffectPrefab;
        [SerializeField] private GameObject airSlashEffectPrefab;
        [SerializeField] private GameObject smokeHitVFXPrefab;  // New: Hit VFX for Smoke weapon

        private Rigidbody rb;
        private Vector3 lastPosition;
        private Vector3 movementDirection;
        private float currentSpeed;
        private Vector3 velocityHistory;
        private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;
        private bool isGrabbed;
        private float lastHitTime; 
        private const float k_HitCooldown = 0.2f;  // New: Prevent multi-hits per swing

        private enum WeaponType
        {
            Sword,
            Hammer,
            Smoke  // New: Smoke weapon type
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();

            grabInteractable.selectEntered.AddListener(OnGrab);
            grabInteractable.selectExited.AddListener(OnRelease);

            gameObject.tag = "Weapon";
            if (rb != null) rb.mass = weaponMass;
            Debug.Log($"Weapon {gameObject.name}: Initialized with baseDamage={baseDamage}, minDamage={minDamage}, maxDamage={maxDamage}, weaponType={weaponType}");
        }

        private void OnGrab(SelectEnterEventArgs args)
        {
            isGrabbed = true;
            rb.isKinematic = true;
            Debug.Log($"Weapon {gameObject.name}: Grabbed, isKinematic=true");
        }

        private void OnRelease(SelectExitEventArgs args)
        {
            isGrabbed = false;
            rb.isKinematic = false;
            Debug.Log($"Weapon {gameObject.name}: Released, isKinematic=false");
        }

        private void FixedUpdate()
        {
            Vector3 deltaPosition = transform.position - lastPosition;
            currentSpeed = deltaPosition.magnitude / Time.fixedDeltaTime;
            if (deltaPosition.magnitude > 0.001f) movementDirection = deltaPosition.normalized;
            velocityHistory = Vector3.Lerp(velocityHistory, deltaPosition / Time.fixedDeltaTime, 0.5f);
            lastPosition = transform.position;
            //Debug.Log($"Weapon {gameObject.name}: currentSpeed={currentSpeed:F2}, movementDirection={movementDirection}");

            if (isGrabbed && currentSpeed > swingDetectionThreshold)
            {
                DetectWeaponSwingPattern();
            }
        }

        private void DetectWeaponSwingPattern()
        {
            float verticalDot = Vector3.Dot(movementDirection, -Vector3.up);
            float horizontalDot = Mathf.Abs(Vector3.Dot(movementDirection, Vector3.right));

            if (weaponType == WeaponType.Hammer && verticalDot > verticalSwingThreshold)
            {
                Debug.Log($"Weapon {gameObject.name}: Hammer vertical swing detected, verticalDot={verticalDot:F2}");
            }
            if (weaponType == WeaponType.Sword && horizontalDot > horizontalSwingThreshold)
            {
                Debug.Log($"Weapon {gameObject.name}: Sword horizontal swing detected, horizontalDot={horizontalDot:F2}");
            }
            // Optional: Add smoke-specific swing detection if needed
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!isGrabbed || Time.time - lastHitTime < k_HitCooldown) return;  // New: Debounce

            lastHitTime = Time.time;  // New: Update timestamp

            // Use Rigidbody velocity for more accurate damage calculation
            float speed = collision.relativeVelocity.magnitude > currentSpeed ? collision.relativeVelocity.magnitude : currentSpeed;  // Changed: Use relativeVelocity for better accuracy
            float damageAmount = Mathf.Clamp(baseDamage + (speed * 0.5f), minDamage, maxDamage);  // Changed: Less aggressive scaling + clamp to max
            Vector3 collisionPoint = collision.contacts[0].point;

            Debug.Log($"Weapon {gameObject.name}: Collision with {collision.gameObject.name}, speed={speed:F2}, damageAmount={damageAmount:F2}");

            // New: For Smoke weapon, play hit VFX at collision point
            if (weaponType == WeaponType.Smoke && smokeHitVFXPrefab != null)
            {
                Instantiate(smokeHitVFXPrefab, collisionPoint, Quaternion.LookRotation(collision.contacts[0].normal));
                Debug.Log($"Weapon {gameObject.name}: Spawned smoke hit VFX at {collisionPoint}");
            }

            if (weaponType == WeaponType.Hammer &&
                Vector3.Dot(movementDirection, -Vector3.up) > verticalSwingThreshold &&
                collision.gameObject.CompareTag("Ground"))
            {
                if (groundImpactEffectPrefab != null)
                {
                    Instantiate(groundImpactEffectPrefab, collisionPoint, Quaternion.LookRotation(collision.contacts[0].normal));
                    Debug.Log($"Weapon {gameObject.name}: Spawned ground impact effect at {collisionPoint}");
                }
                ApplyAreaDamage(collisionPoint, 3f, damageAmount * 1.5f);
            }

            if (weaponType == WeaponType.Sword &&
                Mathf.Abs(Vector3.Dot(movementDirection, Vector3.right)) > horizontalSwingThreshold)
            {
                if (airSlashEffectPrefab != null)
                {
                    GameObject airSlash = Instantiate(airSlashEffectPrefab, transform.position,
                        Quaternion.LookRotation(movementDirection));
                    Projectile projectile = airSlash.AddComponent<Projectile>();
                    projectile.Initialize(movementDirection, 15f, damageAmount);
                    Debug.Log($"Weapon {gameObject.name}: Spawned air slash projectile with damage={damageAmount}");
                }
            }

            var breakableObject = collision.gameObject.GetComponent<JiggleBreakableBigObject>();
            if (breakableObject != null)
            {
                HealthComponent healthComponent = breakableObject.GetComponent<HealthComponent>();
                if (healthComponent != null && !healthComponent.IsDead())
                {
                    healthComponent.TakeDamage(damageAmount, collisionPoint, gameObject);
                    Debug.Log($"Weapon: Applied {damageAmount} damage to {collision.gameObject.name} (JiggleBreakable)");
                }
                return;
            }

            if (collision.gameObject.CompareTag("TreeOfLight"))
            {
                var healthComponent = collision.gameObject.GetComponent<HealthComponent>();
                if (healthComponent != null && !healthComponent.IsDead())
                {
                    float treeDamage = damageAmount * treeOfLightDamageMultiplier;
                    healthComponent.TakeDamage(treeDamage, collisionPoint, gameObject);
                    Debug.Log($"Weapon: Applied {treeDamage} damage to {collision.gameObject.name} (TreeOfLight)");
                }
                return;
            }

            var spiderHealth = collision.gameObject.GetComponent<HealthComponent>();
            if (spiderHealth != null && collision.gameObject.CompareTag("Enemy") && !spiderHealth.IsDead())
            {
                float spiderDamage = damageAmount * shadowMonsterDamageMultiplier;
                spiderHealth.TakeDamage(spiderDamage, collisionPoint, gameObject);
                Debug.Log($"Weapon: Applied {spiderDamage} damage to {collision.gameObject.name} (Enemy)");
                Rigidbody spiderRb = collision.gameObject.GetComponent<Rigidbody>();
                if (spiderRb != null && !spiderRb.isKinematic)
                {
                    Vector3 forceDir = (collision.transform.position - transform.position).normalized;
                    forceDir.y = 0.5f;
                    spiderRb.AddForce(forceDir * 15f * impactForceMultiplier, ForceMode.Impulse);
                    Debug.Log($"Weapon: Applied force to {collision.gameObject.name}, forceDir={forceDir}");
                }
            }
        }

        private void ApplyAreaDamage(Vector3 center, float radius, float damage)
        {
            Collider[] hitColliders = new Collider[20];
            int count = Physics.OverlapSphereNonAlloc(center, radius, hitColliders);
            for (int i = 0; i < count; i++)
            {
                var breakable = hitColliders[i].GetComponent<JiggleBreakableBigObject>();
                if (breakable != null)
                {
                    HealthComponent healthComponent = breakable.GetComponent<HealthComponent>();
                    if (healthComponent != null && !healthComponent.IsDead())
                    {
                        float distance = Vector3.Distance(center, hitColliders[i].transform.position);
                        float damageWithFalloff = damage * (1f - (distance / radius));
                        healthComponent.TakeDamage(damageWithFalloff, hitColliders[i].transform.position, gameObject);
                        Debug.Log($"Weapon: Applied {damageWithFalloff} area damage to {hitColliders[i].gameObject.name}");
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (grabInteractable != null)
            {
                grabInteractable.selectEntered.RemoveListener(OnGrab);
                grabInteractable.selectExited.RemoveListener(OnRelease);
            }
        }
    }

    public class Projectile : MonoBehaviour
    {
        private Vector3 direction;
        private float speed;
        private float damage;
        private readonly float lifespan = 1.5f;

        public void Initialize(Vector3 dir, float spd, float dmg)
        {
            direction = dir;
            speed = spd;
            damage = dmg;
            Destroy(gameObject, lifespan);
            Debug.Log($"Projectile: Initialized with damage={damage}, speed={speed}, direction={direction}");
        }

        private void Update()
        {
            transform.position += direction * (speed * Time.deltaTime);
        }

        private void OnTriggerEnter(Collider other)
        {
            var breakable = other.GetComponent<JiggleBreakableBigObject>();
            if (breakable != null)
            {
                HealthComponent healthComponent = breakable.GetComponent<HealthComponent>();
                if (healthComponent != null && !healthComponent.IsDead())
                {
                    healthComponent.TakeDamage(damage, transform.position, gameObject);
                    Debug.Log($"Projectile: Applied {damage} damage to {other.gameObject.name}");
                }
            }
            Destroy(gameObject);
        }
    }
}
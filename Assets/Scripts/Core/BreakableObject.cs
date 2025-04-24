using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

namespace Core
{
    public abstract class BreakableObject : MonoBehaviour
    {
        [Header("Breakable Object Settings")]
        //[SerializeField] protected float health = 100f;
        [SerializeField] protected bool isBreakable = true;

        [Header("Item Drop Settings")]
        [SerializeField] protected GameObject[] itemDropPrefabs;
        [SerializeField] protected float dropForce = 5f;
        [SerializeField] protected float itemSpawnHeightOffset = 0.5f;

        [Header("Destruction Settings")]
        [SerializeField] protected float destroyDelay = 2f;

        // [Header("Effects")]
        // [SerializeField] protected ParticleSystem breakEffect;
        // [SerializeField] protected AudioClip breakSound;

        [Header("Physics Settings")]
        [SerializeField] protected float mass = 1000f;
        [SerializeField] protected float drag = 1f;
        [SerializeField] protected float angularDrag = 1f;
        [SerializeField] protected bool useGravity = true;
        [SerializeField] protected float breakUpwardForce = 2f; // Force applied when breaking

        [Header("Collision Settings")]
        [SerializeField] protected LayerMask collisionLayers = -1; // Defaults to all layers
        [SerializeField] protected float minimumImpactForce = 5f;
        [SerializeField] protected bool breakOnlyFromWeapons = true;
        [SerializeField] protected bool useImpactForce = true;
        [SerializeField] protected float damageMultiplier = 1f;

        [Header("Events")]
        public UnityEvent onBreak;
        public UnityEvent onDamage;

        protected AudioSource audioSource;
        protected bool isBroken = false;
        //protected float currentHealth;
        protected Rigidbody rb;
        
        protected virtual void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            // Setup rigidbody
            SetupRigidbody();
        }

        protected virtual void SetupRigidbody()
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }

            // Configure rigidbody for stability
            rb.mass = mass;
            rb.linearDamping = drag;
            rb.angularDamping = angularDrag;
            rb.useGravity = useGravity;
            rb.isKinematic = true; // Start as kinematic until broken
            rb.interpolation = RigidbodyInterpolation.Interpolate; // Smoother physics
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous; // Better collision detection

            // Add these settings to prevent falling through
            rb.maxAngularVelocity = 50f; // Limit rotation speed
            rb.solverIterations = 6; // More physics solver iterations
            rb.solverVelocityIterations = 2; // More velocity solver iterations
        }

        public virtual void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitDirection)
        {
            
        }
        protected virtual void OnCollisionEnter(Collision collision)
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

                // Calculate damage based on impact force and weapon status
                float damage;
                if (useImpactForce)
                {
                    // Scale damage based on impact force, with a minimum damage
                    damage = Mathf.Max(impactForce * damageMultiplier, minimumImpactForce);
                }
                else
                {
                    damage = 100f * 0.2f; // Take 20% of health as damage
                }

                // Apply damage
                TakeDamage(damage, hitPoint, hitDirection);
            }
        }

        protected abstract void HandleBreaking();

        protected virtual void Break()
        {
            if (isBroken) return;

            isBroken = true;

            // Start coroutine to handle the breaking process
            StartCoroutine(BreakSequence());
        }

        protected virtual IEnumerator BreakSequence()
        {
            // Wait a short moment before making non-kinematic
            yield return new WaitForSeconds(0.1f);

            // Make the object non-kinematic when broken
            if (rb != null)
            {
                rb.isKinematic = false;

                // Apply a small upward force to prevent falling through
                rb.AddForce(Vector3.up * breakUpwardForce, ForceMode.VelocityChange);
            }

            // Handle destruction
            HandleDestruction();
        }

        protected virtual void DropItems(Vector3 originPoint)
        {
            if (itemDropPrefabs == null || itemDropPrefabs.Length == 0)
            {
                return; // No items to drop
            }

            foreach (var itemPrefab in itemDropPrefabs)
            {
                if (itemPrefab != null)
                {
                    Vector3 randomDirection = Random.insideUnitSphere;
                    randomDirection.y = Mathf.Abs(randomDirection.y); // Ensure items pop upward

                    Vector3 spawnPosition = originPoint + Vector3.up * itemSpawnHeightOffset;
                    GameObject droppedItem = Object.Instantiate(itemPrefab, spawnPosition, Random.rotation) as GameObject;

                    if (droppedItem == null) continue;

                    // Ensure dropped item has required components
                    Rigidbody itemRb = droppedItem.GetComponent<Rigidbody>();
                    if (itemRb == null)
                    {
                        itemRb = droppedItem.AddComponent<Rigidbody>();
                    }

                    // Configure the dropped item's rigidbody
                    itemRb.useGravity = true;
                    itemRb.isKinematic = false;
                    itemRb.interpolation = RigidbodyInterpolation.Interpolate;
                    itemRb.mass = 1f; // Set a reasonable mass for dropped items

                    Vector3 force = randomDirection * dropForce;
                    itemRb.AddForce(force, ForceMode.Impulse);

                    // Verify the dropped item is active and visible
                    if (!droppedItem.activeInHierarchy)
                    {
                        droppedItem.SetActive(true);
                    }
                }
            }
        }

        protected virtual void HandleDestruction()
        {
            // Drop items first
            DropItems(transform.position);

            onBreak?.Invoke();

            // Handle destruction
            StartCoroutine(DestroyAfterDelay());
        }

        protected IEnumerator DestroyAfterDelay()
        {
            yield return new WaitForSeconds(destroyDelay);
            Destroy(gameObject);
        }
        
    }
}
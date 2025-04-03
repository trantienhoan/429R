using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

namespace Core
{
    public abstract class BreakableObject : MonoBehaviour
    {
        [Header("Breakable Object Settings")]
        [SerializeField] protected float health = 100f;
        [SerializeField] protected bool isBreakable = true;
        [SerializeField] protected GameObject[] itemDropPrefabs;
        [SerializeField] protected float dropForce = 5f;
        [SerializeField] protected float destroyDelay = 2f;
        [SerializeField] protected ParticleSystem breakEffect;
        [SerializeField] protected AudioClip breakSound;

        [Header("Physics Settings")]
        [SerializeField] protected float mass = 1000f;
        [SerializeField] protected float drag = 1f;
        [SerializeField] protected float angularDrag = 1f;
        [SerializeField] protected bool useGravity = true;
        [SerializeField] protected float breakUpwardForce = 2f; // Force applied when breaking

        [Header("Events")]
        public UnityEvent onBreak;
        public UnityEvent onDamage;

        protected AudioSource audioSource;
        protected bool isBroken = false;
        protected float currentHealth;
        protected Rigidbody rb;

        public float GetCurrentHealth()
        {
            return currentHealth;
        }

        protected virtual void Awake()
        {
            Debug.Log($"BreakableObject: Awake called on {gameObject.name}");
            currentHealth = health;
            
            // Setup audio source
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null && breakSound != null)
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
            if (!isBreakable || isBroken)
            {
                Debug.Log($"BreakableObject: Damage ignored on {gameObject.name} (not breakable or already broken)");
                return;
            }

            currentHealth -= damage;
            Debug.Log($"BreakableObject: Taking {damage} damage on {gameObject.name}. Health: {currentHealth}/{health}");
            
            onDamage?.Invoke();
            Debug.Log($"BreakableObject: Damage event triggered on {gameObject.name}");

            if (currentHealth <= 0)
            {
                HandleBreaking();
            }
        }

        protected virtual void HandleBreaking()
        {
            // Base implementation just calls Break
            Break();
        }

        protected virtual void Break()
        {
            if (isBroken) return;
            
            isBroken = true;
            Debug.Log($"BreakableObject: Breaking {gameObject.name}");
            
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
                rb.AddForce(Vector3.up * breakUpwardForce, ForceMode.Impulse);
                //Debug.Log($"BreakableObject: Applied upward force {breakUpwardForce} to {gameObject.name}");
            }
            
            // Schedule destruction
            if (destroyDelay > 0)
            {
                Debug.Log($"BreakableObject: Scheduling destruction of {gameObject.name} in {destroyDelay} seconds");
                Invoke(nameof(HandleDestruction), destroyDelay);
            }
            else
            {
                HandleDestruction();
            }
        }

        protected virtual void DropItems(Vector3 originPoint)
        {
            Debug.Log($"BreakableObject: Attempting to drop items from {gameObject.name} at position {originPoint}");
            
            if (itemDropPrefabs == null || itemDropPrefabs.Length == 0)
            {
                Debug.LogWarning($"BreakableObject: No item drop prefabs assigned to {gameObject.name}");
                return;
            }

            Debug.Log($"BreakableObject: Found {itemDropPrefabs.Length} prefabs to drop");
            
            foreach (var itemPrefab in itemDropPrefabs)
            {
                if (itemPrefab != null)
                {
                    Vector3 randomDirection = Random.insideUnitSphere;
                    randomDirection.y = Mathf.Abs(randomDirection.y); // Ensure items pop upward

                    Vector3 spawnPosition = originPoint + Vector3.up * 0.5f;
                    Debug.Log($"BreakableObject: Spawning item {itemPrefab.name} at {spawnPosition}");

                    GameObject droppedItem = Instantiate(itemPrefab, spawnPosition, Random.rotation);
                    
                    // Ensure dropped item has required components
                    Rigidbody itemRb = droppedItem.GetComponent<Rigidbody>();
                    if (itemRb == null)
                    {
                        itemRb = droppedItem.AddComponent<Rigidbody>();
                        Debug.Log($"BreakableObject: Added missing Rigidbody to dropped item {droppedItem.name}");
                    }

                    // Configure the dropped item's rigidbody
                    itemRb.useGravity = true;
                    itemRb.isKinematic = false;
                    itemRb.interpolation = RigidbodyInterpolation.Interpolate;
                    itemRb.mass = 1f; // Set a reasonable mass for dropped items

                    Vector3 force = randomDirection * dropForce;
                    itemRb.AddForce(force, ForceMode.Impulse);
                    Debug.Log($"BreakableObject: Applied force {force} to dropped item {droppedItem.name}");

                    // Verify the dropped item is active and visible
                    if (!droppedItem.activeInHierarchy)
                    {
                        Debug.LogWarning($"BreakableObject: Dropped item {droppedItem.name} is not active!");
                        droppedItem.SetActive(true);
                    }
                }
                else
                {
                    Debug.LogWarning($"BreakableObject: Null item prefab found in {gameObject.name}");
                }
            }
        }

        protected virtual void HandleDestruction()
        {
            Debug.Log($"BreakableObject: HandleDestruction called on {gameObject.name}");
            
            // Drop items first
            DropItems(transform.position);
            
            // Spawn break effect
            if (breakEffect != null)
            {
                var effect = Instantiate(breakEffect, transform.position, Quaternion.identity);
                effect.Play();
                Destroy(effect.gameObject, effect.main.duration);
            }

            // Play break sound
            if (audioSource != null && breakSound != null)
            {
                audioSource.PlayOneShot(breakSound);
            }

            onBreak?.Invoke();
            
            // Handle destruction
            StartCoroutine(DestroyAfterDelay());
        }

        protected IEnumerator DestroyAfterDelay()
        {
            yield return new WaitForSeconds(destroyDelay);
            Destroy(gameObject);
        }

        public void SetHealth(float newHealth)
        {
            health = newHealth;
            currentHealth = newHealth;
            Debug.Log($"BreakableObject: Health set to {newHealth} on {gameObject.name}");
        }
    }
} 
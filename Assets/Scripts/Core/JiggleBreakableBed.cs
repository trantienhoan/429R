using UnityEngine;
using UnityEngine.Events;
using System.Collections;

namespace Core
{
    public class JiggleBreakableBed : MonoBehaviour
    {
        [Header("Health Settings")]
        [SerializeField] private float health = 100f;
        [SerializeField] private bool isBreakable = true;
        [SerializeField] private float currentHealth;

        [Header("Jiggle Effect Settings")]
        [SerializeField] private float jiggleAmount = 0.05f;
        [SerializeField] private float jiggleSpeed = 30f;
        [SerializeField] private float jiggleDamping = 8f;
        [SerializeField] private float jiggleRecoverySpeed = 2f;
        [SerializeField] private float hitScaleAmount = 0.8f;
        [SerializeField] private float hitScaleSpeed = 10f;

        [Header("Effects Settings")]
        [SerializeField] private GameObject hitParticlePrefab;
        [SerializeField] private GameObject breakParticlePrefab;
        [SerializeField] private AudioClip hitSound;
        [SerializeField] private AudioClip breakSound;
        [SerializeField] private float particleScale = 1f;
        [SerializeField] private float soundVolume = 1f;

        [Header("Item Drop Settings")]
        [SerializeField] private GameObject[] itemDropPrefabs;
        [SerializeField] private float dropForce = 5f;
        [SerializeField] private float dropChance = 0.3f;

        [Header("Events")]
        public UnityEvent onDamage;
        public UnityEvent onBreak;

        private Rigidbody rb;
        private Vector3 originalScale;
        private Vector3 targetScale;
        private bool isJiggling = false;
        private bool isBroken = false;
        private float jiggleTimer = 0f;
        private AudioSource audioSource;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            originalScale = transform.localScale;
            currentHealth = health;

            // Setup rigidbody constraints to keep the bed in place
            if (rb != null)
            {
                rb.constraints = RigidbodyConstraints.FreezePosition;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
            }

            // Initialize target scale to original scale
            targetScale = originalScale;

            // Add AudioSource component if not present
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 1f; // Make it fully 3D
                audioSource.rolloffMode = AudioRolloffMode.Linear;
                audioSource.minDistance = 1f;
                audioSource.maxDistance = 20f;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!isBreakable || isBroken) return;

            // Check if the colliding object is a weapon
            if (collision.gameObject.CompareTag("Weapon"))
            {
                float impactForce = collision.impulse.magnitude;
                Vector3 hitPoint = collision.contacts[0].point;
                Vector3 hitDirection = collision.contacts[0].normal;

                // Calculate damage based on impact force
                float damage = Mathf.Max(impactForce, 10f); // Minimum damage of 10

                TakeDamage(damage, hitPoint, hitDirection);
            }
        }

        public void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitDirection)
        {
            if (!isBreakable || isBroken) return;

            currentHealth -= damage;
            Debug.Log($"JiggleBreakableBed: Taking {damage} damage. Health: {currentHealth}/{health}");

            // Play hit particle effect
            if (hitParticlePrefab != null)
            {
                GameObject hitEffect = Instantiate(hitParticlePrefab, hitPoint, Quaternion.LookRotation(hitDirection));
                hitEffect.transform.localScale *= particleScale;
                Destroy(hitEffect, 2f);
            }

            // Play hit sound
            if (hitSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(hitSound, soundVolume);
            }

            // Trigger damage event
            onDamage?.Invoke();

            // Handle jiggle and scale effects
            HandleScaleEffect(hitDirection);
            TriggerJiggleEffect(hitDirection);

            // Chance to drop items on each hit
            if (Random.value <= dropChance)
            {
                DropRandomItem(false);
            }

            if (currentHealth <= 0)
            {
                HandleBreaking();
            }
        }

        private void HandleScaleEffect(Vector3 hitDirection)
        {
            // Scale down when hit from above
            if (hitDirection.y < -0.5f)
            {
                targetScale = new Vector3(originalScale.x, originalScale.y * hitScaleAmount, originalScale.z);
            }
            else
            {
                targetScale = originalScale;
            }
        }

        private void TriggerJiggleEffect(Vector3 hitDirection)
        {
            if (isJiggling) return;

            isJiggling = true;
            jiggleTimer = 0f;
            
            // Apply a small force in the hit direction
            if (rb != null)
            {
                rb.AddForce(hitDirection * jiggleAmount * jiggleSpeed, ForceMode.Impulse);
            }
        }

        private void Update()
        {
            // Smoothly interpolate scale
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * hitScaleSpeed);

            // Handle jiggle recovery
            if (isJiggling)
            {
                jiggleTimer += Time.deltaTime;
                if (jiggleTimer >= jiggleDamping)
                {
                    isJiggling = false;
                    if (rb != null)
                    {
                        rb.linearVelocity = Vector3.zero;
                    }
                }
            }
        }

        private void HandleBreaking()
        {
            isBroken = true;
            Debug.Log("JiggleBreakableBed: Breaking bed");
            
            // Play break particle effect
            if (breakParticlePrefab != null)
            {
                GameObject breakEffect = Instantiate(breakParticlePrefab, transform.position, Quaternion.identity);
                breakEffect.transform.localScale *= particleScale;
                Destroy(breakEffect, 3f);
            }

            // Play break sound
            if (breakSound != null && audioSource != null)
            {
                AudioSource.PlayClipAtPoint(breakSound, transform.position, soundVolume);
            }

            // Trigger break event
            onBreak?.Invoke();

            // Drop all items when breaking
            DropAllItems();

            // Destroy the bed after a short delay
            Destroy(gameObject, 0.5f);
        }

        private void DropAllItems()
        {
            if (itemDropPrefabs == null || itemDropPrefabs.Length == 0) return;

            // First try to drop magical seed if it hasn't been found yet
            bool hasMagicalSeed = false;
            foreach (GameObject prefab in itemDropPrefabs)
            {
                if (prefab.CompareTag("MagicalSeed"))
                {
                    hasMagicalSeed = true;
                    break;
                }
            }

            // If we have a magical seed prefab and no seed exists yet, drop it
            if (hasMagicalSeed && MagicalSeedManager.Instance != null && !MagicalSeedManager.Instance.HasSeedBeenFound())
            {
                GameObject seedPrefab = System.Array.Find(itemDropPrefabs, prefab => prefab.CompareTag("MagicalSeed"));
                if (seedPrefab != null)
                {
                    Vector3 dropPosition = GetRandomDropPosition();
                    GameObject droppedSeed = Instantiate(seedPrefab, dropPosition, Quaternion.identity);
                    Rigidbody seedRb = droppedSeed.GetComponent<Rigidbody>();
                    if (seedRb != null)
                    {
                        Vector3 randomDirection = GetRandomDropDirection();
                        seedRb.AddForce(randomDirection * dropForce, ForceMode.Impulse);
                    }
                }
            }

            // Then drop all other items with staggered timing
            StartCoroutine(DropAllItemsCoroutine());
        }

        private System.Collections.IEnumerator DropAllItemsCoroutine()
        {
            foreach (GameObject prefab in itemDropPrefabs)
            {
                if (!prefab.CompareTag("MagicalSeed"))
                {
                    Vector3 dropPosition = GetRandomDropPosition();
                    GameObject droppedItem = Instantiate(prefab, dropPosition, Quaternion.identity);
                    Rigidbody itemRb = droppedItem.GetComponent<Rigidbody>();
                    if (itemRb != null)
                    {
                        Vector3 randomDirection = GetRandomDropDirection();
                        itemRb.AddForce(randomDirection * dropForce, ForceMode.Impulse);
                    }
                    // Add a small delay between drops
                    yield return new WaitForSeconds(0.1f);
                }
            }
        }

        private void DropRandomItem(bool checkMagicalSeed = true)
        {
            if (itemDropPrefabs == null || itemDropPrefabs.Length == 0) return;

            // Check if we should drop a magical seed
            if (checkMagicalSeed && Random.value <= dropChance)
            {
                // Check if a magical seed already exists
                bool hasMagicalSeed = false;
                foreach (GameObject prefab in itemDropPrefabs)
                {
                    if (prefab.CompareTag("MagicalSeed"))
                    {
                        hasMagicalSeed = true;
                        break;
                    }
                }

                // If we have a magical seed prefab and no seed exists yet, drop it
                if (hasMagicalSeed && MagicalSeedManager.Instance != null && !MagicalSeedManager.Instance.HasSeedBeenFound())
                {
                    GameObject seedPrefab = System.Array.Find(itemDropPrefabs, prefab => prefab.CompareTag("MagicalSeed"));
                    if (seedPrefab != null)
                    {
                        Vector3 dropPosition = GetRandomDropPosition();
                        GameObject droppedSeed = Instantiate(seedPrefab, dropPosition, Quaternion.identity);
                        Rigidbody seedRb = droppedSeed.GetComponent<Rigidbody>();
                        if (seedRb != null)
                        {
                            Vector3 randomDirection = GetRandomDropDirection();
                            seedRb.AddForce(randomDirection * dropForce, ForceMode.Impulse);
                        }
                        return;
                    }
                }
            }

            // Drop other items if any
            foreach (GameObject prefab in itemDropPrefabs)
            {
                if (!prefab.CompareTag("MagicalSeed") && Random.value <= dropChance)
                {
                    Vector3 dropPosition = GetRandomDropPosition();
                    GameObject droppedItem = Instantiate(prefab, dropPosition, Quaternion.identity);
                    Rigidbody itemRb = droppedItem.GetComponent<Rigidbody>();
                    if (itemRb != null)
                    {
                        Vector3 randomDirection = GetRandomDropDirection();
                        itemRb.AddForce(randomDirection * dropForce, ForceMode.Impulse);
                    }
                }
            }
        }

        private Vector3 GetRandomDropPosition()
        {
            // Add some random offset to the drop position
            float randomX = Random.Range(-0.5f, 0.5f);
            float randomZ = Random.Range(-0.5f, 0.5f);
            return transform.position + new Vector3(randomX, 0.5f, randomZ);
        }

        private Vector3 GetRandomDropDirection()
        {
            // Create a more spread out random direction
            float randomX = Random.Range(-1f, 1f);
            float randomY = Random.Range(0.3f, 0.7f); // Reduced upward force to prevent items from going too high
            float randomZ = Random.Range(-1f, 1f);
            return new Vector3(randomX, randomY, randomZ).normalized;
        }

        public float GetCurrentHealth()
        {
            return currentHealth;
        }
    }
} 
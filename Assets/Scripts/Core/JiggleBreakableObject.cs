using UnityEngine;
using System.Collections;

namespace Core
{
    public class JiggleBreakableObject : BreakableObject
    {
        [Header("Jiggle Settings")]
        [SerializeField] private float jiggleAmount = 0.1f;
        [SerializeField] private float jiggleSpeed = 5f;
        [SerializeField] private float jiggleDamping = 0.5f;
        [SerializeField] private float jiggleRecoverySpeed = 2f;
        [SerializeField] private float hitScaleAmount = 0.8f;
        [SerializeField] private float hitScaleSpeed = 10f;
        [SerializeField] private float jiggleDuration = 1.5f;
        [SerializeField] private int jiggleCount = 3;
        [SerializeField] private float itemDropInterval = 0.5f;

        [Header("Collider Settings")]
        [SerializeField] private bool useChildColliders = true;
        [SerializeField] private string childColliderTag = "BreakablePart";

        [Header("Effects")]
        [SerializeField] private GameObject hitParticlePrefab;
        [SerializeField] private GameObject breakParticlePrefab;
        [SerializeField] private AudioClip hitSound;
        [SerializeField] private AudioClip breakSound;
        [SerializeField] private float particleScale = 1f;
        [SerializeField] private float soundVolume = 1f;

        private AudioSource audioSource;
        private Vector3 originalPosition;
        private Vector3 originalScale;
        private Vector3 targetPosition;
        private Vector3 currentVelocity;
        private float scaleProgress = 1f;
        private bool isJiggling = false;
        private Vector3 lastHitPoint;
        private Vector3 lastHitDirection;
        private Collider[] childColliders;

        protected override void Awake()
        {
            base.Awake();
            originalPosition = transform.localPosition;
            originalScale = transform.localScale;
            targetPosition = originalPosition;

            // Setup child colliders
            if (useChildColliders)
            {
                SetupChildColliders();
            }

            // Add AudioSource component if not present
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 1f;
                audioSource.rolloffMode = AudioRolloffMode.Linear;
                audioSource.minDistance = 1f;
                audioSource.maxDistance = 20f;
            }
        }

        private void SetupChildColliders()
        {
            // Get all colliders in children
            childColliders = GetComponentsInChildren<Collider>();
            
            // Tag child colliders if needed
            foreach (Collider col in childColliders)
            {
                if (col.gameObject != gameObject) // Skip the main object
                {
                    col.gameObject.tag = childColliderTag;
                }
            }
        }

        public override void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitDirection)
        {
            if (!isBreakable || isBroken) return;

            // Store hit information
            lastHitPoint = hitPoint;
            lastHitDirection = hitDirection;

            // Call base class TakeDamage to ensure events are triggered
            base.TakeDamage(damage, hitPoint, hitDirection);

            if (currentHealth <= 0 && !isJiggling)
            {
                StartCoroutine(JiggleAndBreak());
            }

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

            // Apply jiggle effect
            Vector3 randomOffset = Random.insideUnitSphere * jiggleAmount;
            targetPosition = originalPosition + randomOffset;
            scaleProgress = 0f;
        }

        private IEnumerator JiggleAndBreak()
        {
            isJiggling = true;
            float elapsedTime = 0f;
            int itemsDropped = 0;
            float nextItemDropTime = itemDropInterval;

            while (elapsedTime < jiggleDuration)
            {
                elapsedTime += Time.deltaTime;
                float jiggleProgress = elapsedTime / jiggleDuration;
                
                // Calculate jiggle offset
                float xOffset = Mathf.Sin(jiggleProgress * jiggleCount * 2 * Mathf.PI) * jiggleAmount;
                float zOffset = Mathf.Cos(jiggleProgress * jiggleCount * 2 * Mathf.PI) * jiggleAmount;
                
                // Apply jiggle to main object
                transform.position = originalPosition + new Vector3(xOffset, 0, zOffset);

                // Drop items periodically during the jiggle
                if (elapsedTime >= nextItemDropTime && itemsDropped < itemDropPrefabs.Length)
                {
                    Vector3 dropPoint = transform.position + Vector3.up;
                    DropSingleItem(dropPoint, itemsDropped);
                    itemsDropped++;
                    nextItemDropTime += itemDropInterval;
                }

                yield return null;
            }

            // Return to original position before breaking
            transform.position = originalPosition;
            
            // Trigger the break
            Break();
        }

        private void DropSingleItem(Vector3 dropPoint, int itemIndex)
        {
            if (itemDropPrefabs == null || itemIndex >= itemDropPrefabs.Length) return;

            GameObject itemPrefab = itemDropPrefabs[itemIndex];
            if (itemPrefab != null)
            {
                Vector3 randomDirection = Random.insideUnitSphere;
                randomDirection.y = Mathf.Abs(randomDirection.y);

                GameObject droppedItem = Instantiate(itemPrefab, dropPoint, Random.rotation);
                droppedItem.transform.localScale = Vector3.one;
                
                if (droppedItem.TryGetComponent<Rigidbody>(out Rigidbody rb))
                {
                    rb.AddForce(randomDirection * dropForce, ForceMode.Impulse);
                }
            }
        }

        protected override void Break()
        {
            if (isBroken) return;
            isBroken = true;

            // Spawn break particle effect
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

            onBreak?.Invoke();
            
            // Handle destruction
            HandleDestruction();
        }

        protected override void HandleDestruction()
        {
            // Disable all colliders (main and children)
            if (useChildColliders && childColliders != null)
            {
                foreach (Collider col in childColliders)
                {
                    col.enabled = false;
                }
            }
            else if (TryGetComponent<Collider>(out Collider mainCollider))
            {
                mainCollider.enabled = false;
            }

            // Disable mesh renderers (main and children)
            MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer renderer in renderers)
            {
                renderer.enabled = false;
            }

            base.HandleDestruction();
        }

        private void Update()
        {
            // Update position (jiggle effect)
            transform.localPosition = Vector3.SmoothDamp(
                transform.localPosition,
                targetPosition,
                ref currentVelocity,
                jiggleDamping,
                Mathf.Infinity,
                Time.deltaTime
            );

            // Gradually return to original position
            targetPosition = Vector3.Lerp(targetPosition, originalPosition, Time.deltaTime * jiggleRecoverySpeed);

            // Update scale (squash and stretch effect)
            scaleProgress = Mathf.MoveTowards(scaleProgress, 1f, Time.deltaTime * hitScaleSpeed);
            float currentScale = Mathf.Lerp(hitScaleAmount, 1f, scaleProgress);
            transform.localScale = originalScale * currentScale;
        }

        public void SetJiggleSettings(float amount, float damping, float speed)
        {
            this.jiggleAmount = amount;
            this.jiggleDamping = damping;
            this.jiggleSpeed = speed;
            Debug.Log($"JiggleBreakableObject: Jiggle settings updated on {gameObject.name}");
        }
    }
} 
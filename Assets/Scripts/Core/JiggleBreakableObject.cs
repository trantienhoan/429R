using UnityEngine;
using System.Collections;
using System.Collections.Generic; // Add this line

namespace Core
{
    public class JiggleBreakableObject : BreakableObject
    {
        [Header("Jiggle Settings")]
        [SerializeField] private float jiggleAmount = 0.05f;
        [SerializeField] private float jiggleSpeed = 30f;
        [SerializeField] private float jiggleDamping = 0.3f;
        [SerializeField] private float jiggleRecoverySpeed = 2f;
        [SerializeField] private float hitScaleAmount = 0.8f;
        [SerializeField] private float hitScaleSpeed = 10f;
        [SerializeField] private float jiggleDuration = 1.5f;
        [SerializeField] private int jiggleCount = 3;
        [SerializeField] private int maxItemsToDrop = 3; // Limit items dropped

        [Header("Collider Settings")]
        [SerializeField] private bool useChildColliders = true;
        [SerializeField] private string childColliderTag = "BreakablePart";

        [Header("Effects")]
        [SerializeField] private GameObject hitParticlePrefab;
        [SerializeField] private GameObject breakParticlePrefab;
        [SerializeField] private AudioClip hitSound;
        //[SerializeField] private AudioClip breakSound; // Overriding base class
        [SerializeField] private float particleScale = 1f;
        [SerializeField] private float soundVolume = 1f;

        private new AudioSource audioSource; // Shadowing base class

        private Vector3 originalPosition;
        private Vector3 originalScale;
        private Vector3 targetPosition;
        private Vector3 currentVelocity;
        private float scaleProgress = 1f;
        private bool isJiggling = false;
        private Vector3 lastHitPoint;
        private Vector3 lastHitDirection;
        private Collider[] childColliders;
        private float jiggleTimer = 0f;
        private Vector3 jiggleVelocity = Vector3.zero;
        private int itemsDropped = 0;

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
            Collider[] cols = GetComponentsInChildren<Collider>();
            List<Collider> validColliders = new List<Collider>();
            foreach (Collider col in cols)
            {
                if (col.gameObject != gameObject && col.enabled) // Skip the main object and disabled colliders
                {
                    col.gameObject.tag = childColliderTag;
                    validColliders.Add(col);
                }
            }
            childColliders = validColliders.ToArray();
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
                HandleBreaking(); // Start breaking sequence
            }
            else
            {
                PlayHitEffects(hitPoint, hitDirection);
            }
        }

        private void PlayHitEffects(Vector3 hitPoint, Vector3 hitDirection)
        {
            // Play hit particle effect
            if (hitParticlePrefab != null)
            {
                GameObject hitEffect = Instantiate(hitParticlePrefab, hitPoint, Quaternion.LookRotation(hitDirection));
                hitEffect.transform.localScale *= particleScale;
                Destroy(hitEffect, 2f);
            }

            // Play hit sound if not already playing
            if (hitSound != null && audioSource != null && !audioSource.isPlaying)
            {
                audioSource.PlayOneShot(hitSound, soundVolume);
            }

            // Apply jiggle effect
            Vector3 randomOffset = Random.insideUnitSphere * jiggleAmount;
            targetPosition = originalPosition + randomOffset;
            scaleProgress = 0f;
        }

        protected override void HandleBreaking()
        {
            StartCoroutine(JiggleAndBreak());
        }

        private IEnumerator JiggleAndBreak()
        {
            isJiggling = true;
            itemsDropped = 0;
            float elapsedTime = 0f;
            Vector3 originalPos = transform.position;

            while (elapsedTime < jiggleDuration)
            {
                elapsedTime += Time.deltaTime;
                float jiggleProgress = elapsedTime / jiggleDuration;

                // Calculate jiggle offset
                float xOffset = Mathf.Sin(jiggleProgress * jiggleCount * 2 * Mathf.PI) * jiggleAmount;
                float zOffset = Mathf.Cos(jiggleProgress * jiggleCount * 2 * Mathf.PI) * jiggleAmount;

                // Apply jiggle to main object
                transform.position = originalPos + new Vector3(xOffset, 0, zOffset);

                // Drop items periodically during the jiggle
                if (itemsDropped < maxItemsToDrop && itemsDropped < itemDropPrefabs.Length)
                {
                    DropSingleItem(originalPos + Vector3.up, itemsDropped);
                    itemsDropped++;
                }

                yield return null;
            }

            // Return to original position before breaking
            transform.position = originalPos;

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
                if(droppedItem != null)
                {
                    droppedItem.transform.localScale = Vector3.one;
                    if (droppedItem.TryGetComponent<Rigidbody>(out Rigidbody rb))
                    {
                        rb.AddForce(randomDirection * dropForce, ForceMode.Impulse);
                    }
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
                audioSource.PlayOneShot(breakSound, soundVolume);
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
                    if(col != null) col.enabled = false;
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
                if(renderer != null) renderer.enabled = false;
            }

            jiggleVelocity = Vector3.zero;
            base.HandleDestruction();
        }

        private void FixedUpdate()
        {
            // Update position (jiggle effect)
            transform.localPosition = Vector3.SmoothDamp(
                transform.localPosition,
                targetPosition,
                ref currentVelocity,
                jiggleDamping,
                Mathf.Infinity,
                Time.fixedDeltaTime // Use fixedDeltaTime
            );

            // Gradually return to original position
            targetPosition = Vector3.Lerp(targetPosition, originalPosition, Time.fixedDeltaTime * jiggleRecoverySpeed);
        }

        private void Update()
        {
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
        }
    }
}
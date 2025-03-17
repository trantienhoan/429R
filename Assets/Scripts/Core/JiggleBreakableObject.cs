using UnityEngine;
using System.Collections;

namespace Core
{
    public class JiggleBreakableObject : BreakableObject
    {
        [Header("Jiggle Settings")]
        [SerializeField] private float jiggleForce = 0.1f;
        [SerializeField] private float jiggleDuration = 1.5f;
        [SerializeField] private int jiggleCount = 3;
        [SerializeField] private float itemDropInterval = 0.5f;
        [SerializeField] private float jiggleDamping = 0.5f;
        [SerializeField] private float jiggleFrequency = 2f;
        
        private Vector3 originalPosition;
        private bool isJiggling = false;
        private Vector3 lastHitPoint;
        private Vector3 lastHitDirection;

        protected override void Awake()
        {
            base.Awake();
            originalPosition = transform.position;
        }

        public override void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitDirection)
        {
            if (!isBreakable || isBroken) return;

            currentHealth -= damage;
            onDamage?.Invoke();

            // Store hit information
            lastHitPoint = hitPoint;
            lastHitDirection = hitDirection;

            if (currentHealth <= 0 && !isJiggling)
            {
                StartCoroutine(JiggleAndBreak());
            }
        }

        private IEnumerator JiggleAndBreak()
        {
            isJiggling = true;
            float elapsedTime = 0f;
            int currentJiggle = 0;
            int itemsDropped = 0;
            float nextItemDropTime = itemDropInterval;

            while (elapsedTime < jiggleDuration)
            {
                elapsedTime += Time.deltaTime;
                float jiggleProgress = elapsedTime / jiggleDuration;
                
                // Calculate jiggle offset
                float xOffset = Mathf.Sin(jiggleProgress * jiggleCount * 2 * Mathf.PI) * jiggleForce;
                float zOffset = Mathf.Cos(jiggleProgress * jiggleCount * 2 * Mathf.PI) * jiggleForce;
                
                // Apply jiggle
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

            // Spawn break effect
            if (breakEffect != null)
            {
                var effect = Instantiate(breakEffect, lastHitPoint, Quaternion.LookRotation(lastHitDirection));
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
            HandleDestruction();
        }

        protected override void HandleDestruction()
        {
            // Disable collider immediately
            if (TryGetComponent<Collider>(out Collider col))
            {
                col.enabled = false;
            }

            // Disable mesh renderer immediately
            if (TryGetComponent<MeshRenderer>(out MeshRenderer renderer))
            {
                renderer.enabled = false;
            }

            base.HandleDestruction();
        }

        public void SetJiggleSettings(float jiggleForce, float jiggleDamping, float jiggleFrequency)
        {
            this.jiggleForce = jiggleForce;
            this.jiggleDamping = jiggleDamping;
            this.jiggleFrequency = jiggleFrequency;
            Debug.Log($"JiggleBreakableObject: Jiggle settings updated on {gameObject.name}");
        }
    }
} 
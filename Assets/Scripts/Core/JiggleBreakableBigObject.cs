using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Core;

public class JiggleBreakableBigObject : InstantBreakableObject
{
    [System.Serializable]
    public class DropItem
    {
        public GameObject itemPrefab;
        [Range(0, 100)]
        public float dropChance = 100f;
    }
    
    // Event for item drop notifications
    public delegate void ItemDroppedHandler(GameObject droppedItem, Vector3 position);
    public event ItemDroppedHandler OnItemDropped;
    
    [Header("Jiggle Effect Settings")]
    [SerializeField] private float jiggleAmount = 0.1f;
    [SerializeField] private float jiggleRecoverySpeed = 2f;
    [SerializeField] private float jiggleDuration = 0.5f;
    [SerializeField] private bool useScaleEffect = true;
    
    [Header("Item Drop Settings")]
    [SerializeField] private bool canDropItems = true;
    [SerializeField] private List<DropItem> possibleDropItems = new List<DropItem>();
    [SerializeField] private int minDropCount = 1;
    [SerializeField] private int maxDropCount = 3;
    [SerializeField] private float dropSpread = 0.5f;
    [SerializeField] private float jiggleItemForce = 2f; // Renamed from itemdropForce
    
    [Header("Magical Seed Settings")]
    [SerializeField] private float magicalSeedDropChance = 0.25f;
    [SerializeField] private GameObject magicalSeedPrefab;
    
    // Jiggle state tracking
    private bool isJiggling = false;
    private Vector3 originalScale;
    private float jiggleStartTime;
    
    public bool GetIsBroken()
    {
        // This accesses the parent class's isBroken field/property
        return base.isBroken;
    }


    protected override void Awake()
    {
        base.Awake();
        originalScale = transform.localScale;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isBroken) return;
        
        // Check collision force against breaking threshold
        float impactForce = collision.impulse.magnitude;
        if (impactForce >= minimumImpactForce)
        {
            // If force is enough to break, handle breaking
            Vector3 hitPoint = collision.contacts[0].point;
            Vector3 hitDirection = collision.contacts[0].normal;
            TakeDamage(impactForce, hitPoint, hitDirection);
        }
        // For smaller impacts, just jiggle
        else if (impactForce > 1.0f)
        {
            Vector3 hitDirection = collision.contacts[0].normal;
            TriggerJiggleEffect(hitDirection);
        }
    }

    public override void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitDirection)
    {
        if (isBroken) return;
        
        // Apply damage using base implementation
        base.TakeDamage(damage, hitPoint, hitDirection);
        
        // If the damage wasn't enough to break the object, trigger a jiggle
        if (health > 0)
        {
            TriggerJiggleEffect(hitDirection);
        }
    }

    private void TriggerJiggleEffect(Vector3 hitDirection)
    {
        if (useScaleEffect)
        {
            HandleScaleEffect(hitDirection);
        }
        
        // Could add other effect types here (rotation, position offset, etc.)
    }

    private void HandleScaleEffect(Vector3 hitDirection)
    {
        // Don't stack jiggle effects
        if (isJiggling) return;
        
        // Calculate scale change based on hit direction
        Vector3 scaleChange = Vector3.Scale(hitDirection.normalized, originalScale) * jiggleAmount;
        
        // Apply the squish effect opposite to hit direction
        transform.localScale = originalScale - scaleChange;
        
        // Start the recovery process
        isJiggling = true;
        jiggleStartTime = Time.time;
    }

    private void Update()
    {
        if (isJiggling)
        {
            // Calculate progress using jiggleRecoverySpeed
            float elapsedTime = (Time.time - jiggleStartTime) * jiggleRecoverySpeed;
            float normalizedTime = Mathf.Clamp01(elapsedTime / jiggleDuration);
            
            // Smoothly interpolate back to original scale
            transform.localScale = Vector3.Lerp(
                transform.localScale,
                originalScale,
                normalizedTime
            );
            
            // Check if jiggle effect is complete
            if (normalizedTime >= 1.0f)
            {
                transform.localScale = originalScale;
                isJiggling = false;
            }
        }
    }

    protected override void HandleBreaking()
    {
        if (isBroken) return;
        isBroken = true;
        
        // Stop any jiggle effects
        isJiggling = false;
        transform.localScale = originalScale;
        
        // Drop items if configured to do so
        if (canDropItems && (possibleDropItems.Count > 0 || magicalSeedPrefab != null))
        {
            StartCoroutine(DropAllItemsCoroutine());
        }
        
        // Call the base implementation to handle destruction effects
        base.HandleBreaking();
    }

    private IEnumerator DropAllItemsCoroutine()
    {
        // Determine how many items to drop (random between min and max)
        int itemCount = Random.Range(minDropCount, maxDropCount + 1);
        
        for (int i = 0; i < itemCount; i++)
        {
            DropRandomItem();
            
            // Add a small delay between drops for visual effect
            yield return new WaitForSeconds(0.1f);
        }
    }

    private void DropRandomItem()
    {
        // First try magical seed drop
        if (magicalSeedPrefab != null && Random.value <= magicalSeedDropChance)
        {
            SpawnItem(magicalSeedPrefab);
            return;
        }
        
        // Exit if no regular items to drop
        if (possibleDropItems.Count == 0) return;
        
        // Calculate total weight for weighted random selection
        float totalWeight = 0f;
        foreach (DropItem item in possibleDropItems)
        {
            if (item.itemPrefab != null)
                totalWeight += item.dropChance;
        }
        
        // Exit if no valid items (zero total weight)
        if (totalWeight <= 0) return;
        
        // Select an item using weighted probabilities
        float randomValue = Random.Range(0f, totalWeight);
        float currentWeight = 0f;
        
        foreach (DropItem item in possibleDropItems)
        {
            if (item.itemPrefab == null) continue;
            
            currentWeight += item.dropChance;
            if (randomValue <= currentWeight)
            {
                SpawnItem(item.itemPrefab);
                break;
            }
        }
    }
    
    private void SpawnItem(GameObject itemPrefab)
    {
        Vector3 spawnPos = GetRandomDropPosition();
        GameObject droppedItem = Instantiate(itemPrefab, spawnPos, Random.rotation);
        
        // Add force if the item has a rigidbody
        Rigidbody rb = droppedItem.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Use the renamed variable here
            rb.AddForce(GetRandomDropDirection() * jiggleItemForce, ForceMode.Impulse);
        }
        
        // Notify listeners about the dropped item
        OnItemDropped?.Invoke(droppedItem, spawnPos);
    }

    private Vector3 GetRandomDropPosition()
    {
        // Random position within the object's bounds plus some spread
        return transform.position + new Vector3(
            Random.Range(-dropSpread, dropSpread),
            0.1f, // Slight upward offset to prevent clipping
            Random.Range(-dropSpread, dropSpread)
        );
    }

    private Vector3 GetRandomDropDirection()
    {
        // Random horizontal direction with upward component
        return new Vector3(
            Random.Range(-1f, 1f),
            Random.Range(0.5f, 1.5f), // Upward force
            Random.Range(-1f, 1f)
        ).normalized;
    }

    public new float GetCurrentHealth()
    {
        return health;
    }
}
using UnityEngine;
using Core; // Assuming HealthComponent is in the Core namespace

namespace Items
{
    public class ItemDropHandler : MonoBehaviour
    {
        [Header("Item Prefabs")]
        [SerializeField] private GameObject keyItemPrefab;
        [SerializeField] private GameObject bombItemPrefab;

        private bool hasGrown = false; // A flag to indicate if growth was completed
        [SerializeField] private float dropForce = 200f;

        // Attach this script to both the TreeOfLight and TreeOfLightPot
        private void Start()
        {
            // Find the components
            HealthComponent health = GetComponent<HealthComponent>();

            // Make sure health exists on both the objects
            if (health != null)
            {
                // Assign the method to the death event on the object
                health.OnDeath += HandleDeath;
            }
            else
            {
                Debug.LogError("ItemDropHandler: No HealthComponent found on this GameObject.");
                enabled = false; // Disable the script if no HealthComponent is found
            }
        }
        public void SetHasGrown(bool grown)
        {
            hasGrown = grown;
        }

        private void HandleDeath()
        {
            if (hasGrown)
            {
                DropItem(keyItemPrefab);
            }
            else
            {
                DropItem(bombItemPrefab);
            }
        }

        public void DropItems()
        {
            if (hasGrown)
            {
                DropItem(keyItemPrefab);
            }
            else
            {
                DropItem(bombItemPrefab);
            }
        }

        private void DropItem(GameObject itemPrefab)
        {
            if (itemPrefab != null)
            {
                GameObject droppedItem = Instantiate(itemPrefab, transform.position, Quaternion.identity);
                Rigidbody rb = droppedItem.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    // Apply an impulse force to the instantiated object
                    rb.AddForce(Vector3.up * dropForce);
                }
            }
            else
            {
                Debug.LogWarning("ItemDropHandler: Item prefab is not assigned.");
            }
        }
    }
}
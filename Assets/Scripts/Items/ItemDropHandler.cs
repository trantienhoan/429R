using UnityEngine;
using Core; // Assuming HealthComponent is in the Core namespace

namespace Items
{
    [RequireComponent(typeof(HealthComponent))]
    public class ItemDropHandler : MonoBehaviour
    {
        [Header("Item Prefabs")]
        [SerializeField] private GameObject keyItemPrefab;
        [SerializeField] private GameObject bombItemPrefab;

        [SerializeField] private float dropForce = 200f;
        [SerializeField] private bool hasGrown = false; // A flag to indicate if growth was completed
        private HealthComponent health;

        private void OnEnable()
        {
            if (keyItemPrefab == null)
            {
                Debug.LogError("ItemDropHandler: keyItemPrefab is not assigned in the Inspector!");
            }
            if (bombItemPrefab == null)
            {
                Debug.LogError("ItemDropHandler: bombItemPrefab is not assigned in the Inspector!");
            }
        }

        // Attach this script to both the TreeOfLight and TreeOfLightPot
        private void Start()
        {
            // Find the components
            health = GetComponent<HealthComponent>();

            // Assign the method to the death event on the object
            health.OnDeath += HandleDeath;
        }

        public void SetHasGrown(bool grown)
        {
            hasGrown = grown;
        }

        private void HandleDeath(HealthComponent healthComponent)
        {
            Debug.Log("HandleDeath called on TreeOfLight!");

            DropItems();
        }

        public void DropItems()
        {
            Debug.Log("DropItem called! Item prefab: ");

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
                else
                {
                    Debug.LogWarning("ItemDropHandler: Dropped item prefab has no Rigidbody component.");
                }
            }
            else
            {
                Debug.LogWarning("ItemDropHandler: Item prefab is not assigned.");
            }
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.OnDeath -= HandleDeath;
            }
        }
    }
}
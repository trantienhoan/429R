using UnityEngine;
using Core;

namespace Items
{
    [RequireComponent(typeof(HealthComponent))]
    public class ItemDropHandler : MonoBehaviour
    {
        [Header("Item Prefabs")]
        [SerializeField] private GameObject keyItemPrefab;
        [SerializeField] private GameObject bombItemPrefab;

        [SerializeField] private float dropForce = 200f;
        [SerializeField] private bool hasGrown;
        private HealthComponent health;

        private void Awake()
        {
            health = GetComponent<HealthComponent>();
            if (health == null)
            {
                //Debug.LogError($"[ItemDropHandler {gameObject.name}] HealthComponent is missing!");
            }

            if (keyItemPrefab == null)
            {
                //Debug.LogError($"[ItemDropHandler {gameObject.name}] keyItemPrefab is not assigned!");
            }
            if (bombItemPrefab == null)
            {
                //Debug.LogError($"[ItemDropHandler {gameObject.name}] bombItemPrefab is not assigned!");
            }
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.OnDeath.AddListener(HandleDeath);
                //Debug.Log($"[ItemDropHandler {gameObject.name}] Subscribed to OnDeath");
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.OnDeath.RemoveListener(HandleDeath);
                //Debug.Log($"[ItemDropHandler {gameObject.name}] Unsubscribed from OnDeath in OnDisable");
            }
        }

        public void SetHasGrown(bool grown)
        {
            hasGrown = grown;
            //Debug.Log($"[ItemDropHandler {gameObject.name}] hasGrown set to {grown}");
        }

        public void DropItems()
        {
            if (!gameObject.activeInHierarchy)
            {
                //Debug.LogWarning($"[ItemDropHandler {gameObject.name}] DropItems called but GameObject is inactive, skipping");
                return;
            }

            //Debug.Log($"[ItemDropHandler {gameObject.name}] DropItems called, hasGrown = {hasGrown}");
            if (hasGrown)
            {
                DropItem(keyItemPrefab, "Key");
            }
            else
            {
                DropItem(bombItemPrefab, "Bomb");
            }
        }

        private void DropItem(GameObject itemPrefab, string itemName)
        {
            if (itemPrefab != null)
            {
                Vector3 dropPosition = transform.position + Vector3.up * 0.5f;
                GameObject droppedItem = Instantiate(itemPrefab, dropPosition, Quaternion.identity);
                Rigidbody rb = droppedItem.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.AddForce(Vector3.up * dropForce, ForceMode.Impulse);
                    //Debug.Log($"[ItemDropHandler {gameObject.name}] Dropped {itemName} at {dropPosition}");
                }
                else
                {
                    //Debug.LogWarning($"[ItemDropHandler {gameObject.name}] Dropped {itemName} has no Rigidbody component!");
                }
            }
            else
            {
                //Debug.LogError($"[ItemDropHandler {gameObject.name}] {itemName} prefab is not assigned!");
            }
        }

        private void HandleDeath(HealthComponent healthComponent)
        {
            //Debug.Log($"[ItemDropHandler {gameObject.name}] HandleDeath called, hasGrown = {hasGrown}");
            DropItems();
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.OnDeath.RemoveListener(HandleDeath);
                //Debug.Log($"[ItemDropHandler {gameObject.name}] Unsubscribed from OnDeath in OnDestroy");
            }
        }
    }
}
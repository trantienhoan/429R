using UnityEngine;
using Core; // Make sure this is the correct namespace

public class HealingItem : MonoBehaviour
{
    [SerializeField] private float healAmount = 20f;
    [SerializeField] private string playerTag = "Player";

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            HealthComponent playerHealth = other.GetComponent<HealthComponent>();
            if (playerHealth != null)
            {
                playerHealth.Heal(healAmount);
                Destroy(gameObject); // Destroy the item after use
            }
        }
    }
}
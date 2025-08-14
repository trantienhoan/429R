using System.Collections;
using UnityEngine;
using Core;  // For HealthComponent

namespace Enemies
{
    public class AttackHitboxCollider : MonoBehaviour
    {
        [SerializeField] private float damageAmount = 10f;  // Tune this (normal attack; override for dive/kamikaze)
        [SerializeField] private LayerMask targetLayerMask;  // Same as before, for players/furniture
        [SerializeField] private float cooldown = 1f;  // Time before next damage

        private bool canDamage = true;  // Flag to prevent spam

        private void OnCollisionEnter(Collision collision)
        {
            if (!canDamage) return;

            // Check layer
            if ((targetLayerMask.value & (1 << collision.gameObject.layer)) == 0) return;

            // Apply damage if has HealthComponent
            var health = collision.gameObject.GetComponent<HealthComponent>() ?? collision.gameObject.GetComponentInParent<HealthComponent>();
            if (health != null)
            {
                health.TakeDamage(damageAmount, collision.contacts[0].point);
                Debug.Log($"[AttackHitbox] Dealt {damageAmount} damage to {collision.gameObject.name}");
            }

            // Start cooldown
            StartCoroutine(CooldownRoutine());
        }

        private IEnumerator CooldownRoutine()
        {
            canDamage = false;
            yield return new WaitForSeconds(cooldown);
            canDamage = true;
        }

        // Public method to override damage (e.g., for dive)
        public void SetDamage(float newDamage)
        {
            damageAmount = newDamage;
        }
    }
}
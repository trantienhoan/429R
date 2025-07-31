using System;
using UnityEngine;
using UnityEngine.Events;

namespace Core
{
    public class HealthComponent : MonoBehaviour
    {
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float health = 100f;
        [SerializeField] private bool destroyOnDeath = true;
        [SerializeField] private bool scaleOnDeath = true; // Added for TreeOfLightPot
        [SerializeField] private bool isInvulnerableByDefault = false; // Added for TreeOfLightPot

        public UnityEvent<float> OnHealthChanged;
        public UnityEvent<HealthChangedEventArgs> OnTakeDamage;
        public UnityEvent<HealthComponent> OnDeath;

        private bool isDead;

        public class HealthChangedEventArgs : EventArgs
        {
            public float PreviousHealth { get; set; }
            public float CurrentHealth { get; set; }
            public float DamageAmount { get; set; }
            public Vector3 HitPoint { get; set; }
            public GameObject DamageSource { get; set; }
            public bool WasCritical { get; set; }
        }

        public bool IsInvulnerable { get; set; } // Added for isInvulnerableByDefault

        private void Awake()
        {
            health = maxHealth;
            isDead = false;
            IsInvulnerable = isInvulnerableByDefault;
            //Debug.Log($"[HealthComponent {gameObject.name}] Initialized with Health: {health}/{maxHealth}, Invulnerable: {IsInvulnerable}");
        }

        public void SetMaxHealth(float value)
        {
            float previousMaxHealth = maxHealth;
            maxHealth = Mathf.Max(0, value);
            health = Mathf.Clamp(health, 0, maxHealth);
            OnHealthChanged?.Invoke(health / maxHealth);
            //Debug.Log($"[HealthComponent {gameObject.name}] MaxHealth set to {maxHealth}, Health adjusted to {health}");
        }

        public float GetHealthPercentage()
        {
            return maxHealth > 0 ? health / maxHealth : 0;
        }

        public bool IsDead()
        {
            return isDead;
        }

        public void TakeDamage(float damage, Vector3 hitPoint = default, GameObject damageSource = null, bool wasCritical = false)
        {
            if (isDead || IsInvulnerable) return;

            float previousHealth = health;
            health = Mathf.Max(0, health - damage);
            bool justDied = health <= 0 && !isDead;

            var args = new HealthChangedEventArgs
            {
                PreviousHealth = previousHealth,
                CurrentHealth = health,
                DamageAmount = damage,
                HitPoint = hitPoint,
                DamageSource = damageSource,
                WasCritical = wasCritical
            };

            OnHealthChanged?.Invoke(health / maxHealth);
            OnTakeDamage?.Invoke(args);

            Debug.Log($"[HealthComponent {gameObject.name}] Took {damage} damage, Health: {health}/{maxHealth}");

            if (justDied)
            {
                isDead = true;
                Die(damageSource);
            }
        }

        public void Heal(float amount)
        {
            if (isDead) return;

            float previousHealth = health;
            health = Mathf.Min(maxHealth, health + amount);

            var args = new HealthChangedEventArgs
            {
                PreviousHealth = previousHealth,
                CurrentHealth = health,
                DamageAmount = -amount,
                HitPoint = transform.position,
                DamageSource = null,
                WasCritical = false
            };

            OnHealthChanged?.Invoke(health / maxHealth);
            OnTakeDamage?.Invoke(args);

            Debug.Log($"[HealthComponent {gameObject.name}] Healed for {amount}, Health: {health}/{maxHealth}");
        }

        public void ResetHealth()
        {
            health = maxHealth;
            isDead = false;
            IsInvulnerable = isInvulnerableByDefault;
            OnHealthChanged?.Invoke(health / maxHealth);
            Debug.Log($"[HealthComponent {gameObject.name}] Health reset to {health}/{maxHealth}, Invulnerable: {IsInvulnerable}");
        }

        public void Kill(GameObject damageSource = null)
        {
            if (isDead) return;
            isDead = true;
            Die(damageSource, health);
            Debug.Log($"[HealthComponent {gameObject.name}] Killed by {damageSource?.name ?? "unknown"}");
        }

        private void Die(GameObject damageSource, float finalHealth = 0)
        {
            health = finalHealth;
            OnHealthChanged?.Invoke(health / maxHealth);
            OnDeath?.Invoke(this);

            Debug.Log($"[HealthComponent {gameObject.name}] Died, invoking OnDeath");

            if (destroyOnDeath)
            {
                StartCoroutine(HandleDestroy());
            }
        }

        private System.Collections.IEnumerator HandleDestroy()
        {
            yield return new WaitForEndOfFrame();
            if (gameObject != null)
            {
                if (scaleOnDeath)
                {
                    float timer = 0f;
                    Vector3 startScale = transform.localScale;
                    float duration = 3f; // Match ShadowMonster's ScaleDownAndDisable
                    while (timer < duration)
                    {
                        timer += Time.deltaTime;
                        transform.localScale = Vector3.Lerp(startScale, Vector3.zero, timer / duration);
                        yield return null;
                    }
                }
                Debug.Log($"[HealthComponent {gameObject.name}] Destroying GameObject");
                UnityEngine.Object.Destroy(gameObject);
            }
        }
    }
}
using System;
using UnityEngine;
using UnityEngine.Events;

namespace Core
{
    public class HealthComponent : MonoBehaviour
    {
        [SerializeField] public float maxHealth = 100f;
        [SerializeField] public float health = 100f;
        [SerializeField] private bool destroyOnDeath = true;
        [SerializeField] private bool scaleOnDeath = true; // Toggle for scaling effect on death
        [SerializeField] private Vector3 deathScaleTarget = Vector3.zero; // Target scale (zero for down, larger for up)
        [SerializeField] private float deathScaleDuration = 3f; // Configurable duration for scaling
        [SerializeField] private bool isInvulnerableByDefault = false; // Added for TreeOfLightPot

        // New fields for continuous health-based scaling
        [SerializeField] private bool scaleWithHealth = false; // Toggle to enable scaling as health changes
        [SerializeField] private Vector3 minScaleOnLowHealth = Vector3.zero; // Minimum scale at zero health (customizable)

        public UnityEvent<float> OnHealthChanged;
        public UnityEvent<HealthChangedEventArgs> OnTakeDamage;
        public UnityEvent<HealthComponent> OnDeath;

        private bool isDead;
        private Vector3 originalScale; // Stores the initial scale for reference

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
            originalScale = transform.localScale; // Store original scale here
            //Debug.Log($"[HealthComponent {gameObject.name}] Initialized with Health: {health}/{maxHealth}, Invulnerable: {IsInvulnerable}");
        }

        public void SetMaxHealth(float value)
        {
            float previousMaxHealth = maxHealth;
            maxHealth = Mathf.Max(0, value);
            health = Mathf.Clamp(health, 0, maxHealth);
            OnHealthChanged?.Invoke(health / maxHealth);
            UpdateScale(); // New: Update scale after max health change
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

            if (!justDied)
            {
                UpdateScale(); // New: Update scale if still alive
            }

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

            UpdateScale(); // New: Update scale after healing

            Debug.Log($"[HealthComponent {gameObject.name}] Healed for {amount}, Health: {health}/{maxHealth}");
        }

        public void ResetHealth()
        {
            health = maxHealth;
            isDead = false;
            IsInvulnerable = isInvulnerableByDefault;
            OnHealthChanged?.Invoke(health / maxHealth);
            UpdateScale(); // New: Update scale after reset (back to original)
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
                if (scaleOnDeath && !scaleWithHealth) // Modified: Optionally skip death scaling if continuous scaling is enabled (to avoid conflict)
                {
                    float timer = 0f;
                    Vector3 startScale = transform.localScale;
                    while (timer < deathScaleDuration)
                    {
                        timer += Time.deltaTime;
                        transform.localScale = Vector3.Lerp(startScale, deathScaleTarget, timer / deathScaleDuration);
                        yield return null;
                    }
                }
                Debug.Log($"[HealthComponent {gameObject.name}] Destroying GameObject");
                UnityEngine.Object.Destroy(gameObject);
            }
        }

        // New method to handle continuous scaling based on health
        private void UpdateScale()
        {
            if (scaleWithHealth && !isDead)
            {
                float healthPercentage = GetHealthPercentage();
                // Linearly interpolate between original scale (at 100% health) and min scale (at 0% health)
                transform.localScale = Vector3.Lerp(minScaleOnLowHealth, originalScale, healthPercentage);
            }
        }
    }
}
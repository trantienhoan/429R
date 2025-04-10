using UnityEngine;
using System;

namespace Core
{
    public class HealthComponent : MonoBehaviour
    {
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float currentHealth;

        public class HealthChangedEventArgs : EventArgs
        {
            public float CurrentHealth { get; set; }
            public float MaxHealth { get; set; }
            public GameObject DamageSource { get; set; }
            public float DamageAmount { get; set; } // Add damage amount
        }

        public delegate void OnHealthChangedDelegate(object sender, HealthChangedEventArgs e);
        public event OnHealthChangedDelegate OnHealthChanged;

        public delegate void OnTakeDamageDelegate(object sender, HealthChangedEventArgs e); // New event
        public event OnTakeDamageDelegate OnTakeDamage;

        public delegate void OnDeathDelegate(HealthComponent health);
        public event OnDeathDelegate OnDeath;

        private bool isDead = false;

        private void Start()
        {
            currentHealth = maxHealth;
        }

        public void ResetHealth()
        {
            currentHealth = maxHealth;
        }

        public void TakeDamage(float damage, GameObject damageSource = null)
        {
            if (isDead) return;

            currentHealth -= damage;

            if (currentHealth <= 0)
            {
                currentHealth = 0;
                Die(damageSource, damage);
            }

            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

            HealthChangedEventArgs eventArgs = new HealthChangedEventArgs
            {
                CurrentHealth = currentHealth,
                MaxHealth = maxHealth,
                DamageSource = damageSource,
                DamageAmount = damage //Set damage amount
            };

            OnTakeDamage?.Invoke(this, eventArgs); // Invoke OnTakeDamage first

            // Trigger health changed event
            OnHealthChanged?.Invoke(this, eventArgs);
        }

        public void Heal(float amount)
        {
            if (isDead) return;

            currentHealth = Mathf.Min(currentHealth + amount, maxHealth);

            HealthChangedEventArgs eventArgs = new HealthChangedEventArgs
            {
                CurrentHealth = currentHealth,
                MaxHealth = maxHealth,
                DamageSource = null,
                DamageAmount = 0 // No damage when healing
            };

            // Trigger health changed event
            OnHealthChanged?.Invoke(this, eventArgs);
        }
        protected virtual void Die(GameObject damageSource, float damage)
        {
            if (isDead) return;

            HealthChangedEventArgs eventArgs = new HealthChangedEventArgs
            {
                CurrentHealth = currentHealth,
                MaxHealth = maxHealth,
                DamageSource = damageSource,
                DamageAmount = damage // Use the damage value passed to Die
            };

            OnDeath?.Invoke(this);
            isDead = true;

            Debug.Log($"{gameObject.name} has died!");
            Destroy(gameObject);
        }

        public float GetHealthPercentage()
        {
            return currentHealth / maxHealth;
        }

        public bool IsDead()
        {
            return isDead;
        }

        public float Health
        {
            get { return currentHealth; }
            set { currentHealth = value; }
        }
        public float MaxHealth => maxHealth;

        public void SetMaxHealth(float newMaxHealth)
        {
            maxHealth = newMaxHealth;
            if (currentHealth > maxHealth)
                currentHealth = maxHealth;

            OnHealthChanged?.Invoke(this, new HealthChangedEventArgs
            {
                CurrentHealth = currentHealth,
                MaxHealth = maxHealth,
                DamageSource = null,
                DamageAmount = 0
            });
        }
    }
}
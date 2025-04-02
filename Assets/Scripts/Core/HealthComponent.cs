using UnityEngine;
//using System.Linq;

namespace Core
{
    public class HealthComponent : MonoBehaviour
    {
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float currentHealth;
        
        public delegate void OnHealthChangedDelegate(float currentHealth, float maxHealth);
        public event OnHealthChangedDelegate OnHealthChanged;
        
        public delegate void OnDeathDelegate();
        public event OnDeathDelegate OnDeath;

        private bool isDead = false;
        
        private void Start()
        {
            currentHealth = maxHealth;
        }
        
        public void TakeDamage(float damage)
        {
            if (isDead) return;
            
            currentHealth -= damage;
            
            // Trigger health changed event
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            
            if (currentHealth <= 0)
            {
                currentHealth = 0;
                Die();
            }
        }
        
        public void Heal(float amount)
        {
            if (isDead) return;
            
            currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
            
            // Trigger health changed event
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }
        
        protected virtual void Die()
        {
            if (isDead) return;
            
            isDead = true;
            
            // Trigger death event
            OnDeath?.Invoke();
            
            Debug.Log($"{gameObject.name} has died!");
        }
        
        public float GetHealthPercentage()
        {
            return currentHealth / maxHealth;
        }
        
        public bool IsDead()
        {
            return isDead;
        }
        
        // New helper properties/methods
        public float Health => currentHealth;
        public float MaxHealth => maxHealth;
        
        public void SetMaxHealth(float newMaxHealth)
        {
            maxHealth = newMaxHealth;
            if (currentHealth > maxHealth)
                currentHealth = maxHealth;
                
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }
    }
}
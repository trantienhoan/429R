using UnityEngine;
using System;

namespace Core
{
    public class HealthComponent : MonoBehaviour
    {
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float currentHealth;
        [SerializeField] private ParticleSystem deathParticleSystem;
        [SerializeField] private ParticleSystem takeDamageParticleSystem;
        
        [Header("Breaking Effects")]
        [SerializeField] private ParticleSystem breakEffect;
        [SerializeField] private AudioClip breakSound;

        [Header("Damage Effects")]
        [SerializeField] private GameObject dustParticlePrefab;
        public class HealthChangedEventArgs : EventArgs
        {
            public float CurrentHealth { get; set; }
            public float MaxHealth { get; set; }
            public GameObject DamageSource { get; set; }
            public float DamageAmount { get; set; }
            public Vector3 HitPoint { get; set; }
        }

        public delegate void OnHealthChangedDelegate(object sender, HealthChangedEventArgs e);
        public event OnHealthChangedDelegate OnHealthChanged;

        public delegate void OnTakeDamageDelegate(object sender, HealthChangedEventArgs e);
        public event OnTakeDamageDelegate OnTakeDamage;

        public delegate void OnDeathDelegate(HealthComponent health);
        public event OnDeathDelegate OnDeath;

        private bool isDead = false;
        private AudioSource audioSource;
        private void Start()
        {
            currentHealth = maxHealth;
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null && breakSound != null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        public void ResetHealth()
        {
            currentHealth = maxHealth;
        }

        public void TakeDamage(float damage, Vector3 hitPoint, GameObject damageSource = null) // MODIFY THIS LINE
        {
            if (isDead) return;

            // Play take damage particle system if assigned
            if (takeDamageParticleSystem != null)
            {
                takeDamageParticleSystem.Play();
            }

            SpawnDustParticles(hitPoint);

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
                DamageAmount = damage,
                HitPoint = hitPoint // SET HitPoint
            };

            OnTakeDamage?.Invoke(this, eventArgs);

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
                DamageAmount = 0,
                HitPoint = transform.position
            };

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
                DamageAmount = damage,
                HitPoint = transform.position
            };

            OnDeath?.Invoke(this);
            isDead = true;

            Debug.Log($"{gameObject.name} has died!");
            
            if (breakEffect != null)
            {
                var effect = Instantiate(breakEffect, transform.position, Quaternion.identity);
                effect.Play();
                Destroy(effect.gameObject, effect.main.duration);
            }

            // Play break sound
            if (audioSource != null && breakSound != null)
            {
                audioSource.PlayOneShot(breakSound);
            }

            if (deathParticleSystem != null)
            {
                deathParticleSystem.Play();
                Destroy(gameObject, deathParticleSystem.main.duration);
            }
            else
            {
                Destroy(gameObject);
            }
        }
        private void SpawnDustParticles(Vector3 position) // ADD THIS METHOD
        {
            if (dustParticlePrefab != null)
            {
                GameObject dustEffect = Instantiate(dustParticlePrefab, position, Quaternion.identity);
                // Auto-destroy particles after they finish playing
                if (dustEffect.TryGetComponent<ParticleSystem>(out ParticleSystem ps))
                {
                    Destroy(dustEffect, ps.main.duration + 1f);
                }
                else
                {
                    Destroy(dustEffect, 3f); // Fallback destroy after 3 seconds
                }
            }
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
                DamageAmount = 0,
                HitPoint = transform.position
            });
        }
        
        public ParticleSystem BreakEffect => breakEffect;
        public AudioClip BreakSound => breakSound;
    }
}
using UnityEngine;
using System;
using System.Collections;

namespace Core
{
    public class HealthComponent : MonoBehaviour
    {
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float currentHealth;

        [Header("Light Integration")]
        [SerializeField] private Light healthLight; // Assign the light here

        [Header("Breaking Effects")]
        [SerializeField] private ParticleSystem breakEffect;
        [SerializeField] private AudioClip breakSound;

        [Header("Damage Effects")]
        [SerializeField] private ParticleSystem dustParticlePrefab;
        [SerializeField] private AudioClip hitSound;

        [Header("Scaling")]
        [SerializeField] private bool scaleOnDeath = true;
        [SerializeField] private float scaleDuration = 0.5f;
        [SerializeField] private Vector3 minScale = Vector3.zero;

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

            // Initialize light intensity
            UpdateLightIntensity();
        }

        public void ResetHealth()
        {
            currentHealth = maxHealth;
            UpdateLightIntensity();
        }

        public void TakeDamage(float damage, Vector3 hitPoint, GameObject damageSource = null) // MODIFY THIS LINE
        {
            if (isDead) return;

            SpawnDustParticles(hitPoint);

            float initialHealth = currentHealth;

            currentHealth -= damage;

            Debug.Log($"{gameObject.name} took {damage} damage from {damageSource?.name ?? "an unknown source"} at {hitPoint}. Initial health: {initialHealth}, New health: {currentHealth}");

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
                HitPoint = hitPoint
            };

            OnTakeDamage?.Invoke(this, eventArgs);

            OnHealthChanged?.Invoke(this, eventArgs);

            UpdateLightIntensity(); // Update light after taking damage
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

            UpdateLightIntensity(); // Update light after healing
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

            Debug.Log($"{gameObject.name} has died! Killed by {damageSource?.name ?? "an unknown source"} with {damage} damage.");

            UpdateLightIntensity(); // Ensure light is off when dead

            if (scaleOnDeath)
            {
                StartCoroutine(ScaleDownAndDestroy());
            }
            else
            {
                DestroyObject();
            }
        }

        private IEnumerator ScaleDownAndDestroy()
        {
            Vector3 startScale = transform.localScale;
            float timer = 0;

            while (timer < scaleDuration)
            {
                timer += Time.deltaTime;
                float progress = timer / scaleDuration;
                transform.localScale = Vector3.Lerp(startScale, minScale, progress);
                yield return null;
            }

            DestroyObject();
        }

        private void DestroyObject()
        {
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

            Destroy(gameObject);
        }

        private void SpawnDustParticles(Vector3 position)
        {
            if (dustParticlePrefab != null)
            {
                ParticleSystem dustEffect = Instantiate(dustParticlePrefab, position, Quaternion.identity); // Instantiate the ParticleSystem
                dustEffect.Play();

                // Play hit sound
                if (audioSource != null && hitSound != null)
                {
                    audioSource.PlayOneShot(hitSound);
                }
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

        private void UpdateLightIntensity()
        {
            if (healthLight != null)
            {
                healthLight.intensity = GetHealthPercentage();
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

            UpdateLightIntensity(); //Update light after setting max health
        }

        public ParticleSystem BreakEffect => breakEffect;
        public AudioClip BreakSound => breakSound;
    }
}

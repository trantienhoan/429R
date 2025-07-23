using UnityEngine;
using System;
using System.Collections;

namespace Core
{
    public class HealthComponent : MonoBehaviour
    {
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float currentHealth;

        [Header("Invulnerability")]
        [SerializeField] private float invulnerabilityDuration = 1f;
        private bool isInvulnerable;

        [Header("Light Integration")]
        [SerializeField] private Light healthLight;

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
            if (audioSource == null && (breakSound != null || hitSound != null))
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            UpdateLightIntensity();
        }

        public void ResetHealth()
        {
            isDead = false;
            isInvulnerable = false;
            currentHealth = maxHealth;
            StopAllCoroutines(); //Stops any existing invulnerability coroutine
            UpdateLightIntensity();
        }

        public void TakeDamage(float damage, Vector3 hitPoint, GameObject damageSource = null, bool bypassSelfDamage = false)
        {
            if (isDead || isInvulnerable) return;

            // Prevent self-damage unless explicitly allowed
            if (damageSource != null && damageSource == gameObject && !bypassSelfDamage)
            {
                Debug.Log($"{gameObject.name} attempted to self-damage but was prevented.");
                return;
            }

            SpawnDustParticles(hitPoint);

            float initialHealth = currentHealth;
            currentHealth -= damage;
            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

            Debug.Log($"{gameObject.name} took {damage} damage from {damageSource?.name ?? "Unknown"} at {hitPoint}. Health: {initialHealth} → {currentHealth}");

            var args = new HealthChangedEventArgs
            {
                CurrentHealth = currentHealth,
                MaxHealth = maxHealth,
                DamageSource = damageSource,
                DamageAmount = damage,
                HitPoint = hitPoint
            };

            OnTakeDamage?.Invoke(this, args);
            OnHealthChanged?.Invoke(this, args);

            UpdateLightIntensity();

            if (currentHealth <= 0 && !isDead)
            {
                Die(damageSource, damage);
            }
            else
            {
                StartCoroutine(InvulnerabilityRoutine());
            }
        }

        private IEnumerator InvulnerabilityRoutine()
        {
            isInvulnerable = true;
            yield return new WaitForSeconds(invulnerabilityDuration);
            isInvulnerable = false;
        }

        public void Heal(float amount)
        {
            if (isDead) return;

            currentHealth = Mathf.Min(currentHealth + amount, maxHealth);

            var args = new HealthChangedEventArgs
            {
                CurrentHealth = currentHealth,
                MaxHealth = maxHealth,
                DamageSource = null,
                DamageAmount = 0,
                HitPoint = transform.position
            };

            OnHealthChanged?.Invoke(this, args);
            UpdateLightIntensity();
        }

        protected virtual void Die(GameObject damageSource, float damage)
        {
            if (isDead) return;

            isDead = true;

            Debug.Log($"{gameObject.name} has died. Killed by {damageSource?.name ?? "Unknown"} with {damage} damage.");

            UpdateLightIntensity();

            OnDeath?.Invoke(this);

            if (scaleOnDeath)
            {
                StartCoroutine(ScaleDownAndHandleDestroy());
            }
            else
            {
                HandleDestroy();
            }
        }

        private IEnumerator ScaleDownAndHandleDestroy()
        {
            Vector3 startScale = transform.localScale;
            float timer = 0f;

            while (timer < scaleDuration)
            {
                timer += Time.deltaTime;
                transform.localScale = Vector3.Lerp(startScale, minScale, timer / scaleDuration);
                yield return null;
            }

            HandleDestroy();
        }

        private void HandleDestroy()
        {
            if (breakEffect != null)
            {
                var effect = Instantiate(breakEffect, transform.position, Quaternion.identity);
                effect.Play();
                Destroy(effect.gameObject, effect.main.duration);
            }

            if (audioSource != null && breakSound != null)
            {
                audioSource.PlayOneShot(breakSound);
            }

            // Pooling-friendly destroy logic
            if (Enemies.SpiderPool.Instance != null && gameObject.CompareTag("Enemy"))
            {
                Enemies.SpiderPool.Instance.ReturnSpider(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void SpawnDustParticles(Vector3 position)
        {
            if (dustParticlePrefab != null)
            {
                var dust = Instantiate(dustParticlePrefab, position, Quaternion.identity);
                dust.Play();

                if (audioSource != null && hitSound != null)
                {
                    audioSource.PlayOneShot(hitSound);
                }

                Destroy(dust.gameObject, dust.main.duration + 1f);
            }
        }

        private void UpdateLightIntensity()
        {
            if (healthLight != null)
            {
                healthLight.intensity = GetHealthPercentage();
            }
        }

        public float GetHealthPercentage() => currentHealth / maxHealth;
        public bool IsDead() => isDead;
        public float Health
        {
            get => currentHealth;
            set => currentHealth = value;
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

            UpdateLightIntensity();
        }
        public void Kill(GameObject damageSource = null)
        {
            if (isDead) return;

            Debug.Log($"{gameObject.name} is being forcefully killed by {damageSource?.name ?? "Unknown"}.");

            currentHealth = 0;
            isDead = true;

            var args = new HealthChangedEventArgs
            {
                CurrentHealth = 0,
                MaxHealth = maxHealth,
                DamageSource = damageSource,
                DamageAmount = maxHealth,
                HitPoint = transform.position
            };

            OnTakeDamage?.Invoke(this, args);
            OnHealthChanged?.Invoke(this, args);
            UpdateLightIntensity();

            Die(damageSource, maxHealth); // This is your internal method that handles death logic.
        }

        public ParticleSystem BreakEffect => breakEffect;
        public AudioClip BreakSound => breakSound;
    }
}
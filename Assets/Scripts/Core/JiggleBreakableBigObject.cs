using System.Collections;
using UnityEngine;

namespace Core
{
    [RequireComponent(typeof(BoxCollider))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(HealthComponent))]
    public class JiggleBreakableBigObject : BreakableObject
    {
        [Header("Jiggle Settings")]
        [SerializeField] private float jiggleAmount = 0.1f;
        [SerializeField] private float jiggleRotationAmount = 5f;
        [SerializeField] private float jiggleScaleAmount = 0.05f;
        [SerializeField] private float jiggleDuration = 0.5f;
        [SerializeField] private AnimationCurve jiggleCurve;
        [SerializeField] private AudioClip jiggleSfx;
        [SerializeField] private ParticleSystem hitParticlesPrefab;

        private BoxCollider boxCollider;
        private Vector3 originalPosition;
        private Quaternion originalRotation;
        private Vector3 originalScale;
        private HealthComponent healthComponent;

        protected override void Awake()
        {
            base.Awake();

            boxCollider = GetComponent<BoxCollider>();
            healthComponent = GetComponent<HealthComponent>();

            originalPosition = transform.position;
            originalRotation = transform.rotation;
            originalScale = transform.localScale;

            boxCollider.isTrigger = false;

            GameObject healthBarPrefab = Resources.Load<GameObject>("Prefabs/BreakableHealthBar");
            if (healthBarPrefab != null)
            {
                GameObject healthBar = Instantiate(healthBarPrefab, transform);
                if (healthBar.TryGetComponent(out BreakableHealthBar bar))
                {
                    bar.Initialize(this);
                }
            }
            else
            {
                Debug.LogWarning("Prefab 'Prefabs/BreakableHealthBar' not found.");
            }
        }

        private void Start()
        {
            if (healthComponent != null)
            {
                healthComponent.OnDeath.AddListener(HandleHealthDeath);
            }
        }

        protected override void OnCollisionEnter(Collision collision)
        {
            if ((collisionLayers.value & (1 << collision.gameObject.layer)) == 0) return;

            bool isWeapon = collision.gameObject.CompareTag("Weapon");
            float impactForce = collision.impulse.magnitude;

            bool shouldDamage = breakOnlyFromWeapons
                ? isWeapon && (!useImpactForce || impactForce >= minimumImpactForce)
                : isWeapon || (useImpactForce && impactForce >= minimumImpactForce);

            if (!shouldDamage) return;

            Vector3 hitPoint = collision.contacts[0].point;
            Vector3 hitDirection = collision.contacts[0].normal;

            StartCoroutine(JiggleCoroutine(hitDirection, hitPoint));

            float damage = useImpactForce
                ? Mathf.Max(impactForce * damageMultiplier, minimumImpactForce)
                : 20f;

            healthComponent.TakeDamage(damage, hitPoint, collision.gameObject);
        }

        private IEnumerator JiggleCoroutine(Vector3 hitDirection, Vector3 hitPoint)
        {
            if (audioSource != null && jiggleSfx != null) audioSource.PlayOneShot(jiggleSfx);

            if (hitParticlesPrefab != null)
            {
                ParticleSystem particles = Instantiate(hitParticlesPrefab, hitPoint, Quaternion.LookRotation(-hitDirection));
                particles.Play();
                Destroy(particles.gameObject, particles.main.duration);
            }

            float timer = 0f;
            Vector3 jiggleDir = -hitDirection.normalized;
            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;
            Vector3 startScale = transform.localScale;

            float randomFactor = Random.Range(0.8f, 1.2f);

            while (timer < jiggleDuration)
            {
                timer += Time.deltaTime;
                float progress = timer / jiggleDuration;
                float curveValue = jiggleCurve?.Evaluate(progress) ?? Mathf.Sin(progress * Mathf.PI * 4);

                transform.position = startPos + jiggleDir * (jiggleAmount * curveValue * randomFactor);

                Vector3 rotOffset = Vector3.Cross(Vector3.up, jiggleDir) * (jiggleRotationAmount * curveValue * randomFactor);
                transform.rotation = startRot * Quaternion.Euler(rotOffset);

                Vector3 scaleOffset = new Vector3(1 - jiggleScaleAmount * curveValue, 1 + jiggleScaleAmount * curveValue, 1 - jiggleScaleAmount * curveValue);
                transform.localScale = Vector3.Scale(startScale, scaleOffset);

                yield return null;
            }

            transform.position = startPos;
            transform.rotation = startRot;
            transform.localScale = startScale;
        }

        protected override void HandleDestruction()
        {
            if (boxCollider != null)
            {
                boxCollider.enabled = false;
            }

            StopAllCoroutines();

            transform.position = originalPosition;
            transform.rotation = originalRotation;
            transform.localScale = originalScale;

            base.HandleDestruction();
        }

        private void HandleHealthDeath(HealthComponent health)
        {
            HandleBreaking();
        }

        protected override void HandleBreaking()
        {
            if (rb != null)
            {
                rb.AddForce(Vector3.up * Random.Range(1f, 3f), ForceMode.Impulse);
            }

            if (TryGetComponent(out Lamp lamp) && !lamp.isBroken)
            {
                lamp.Break();
            }

            Break();
        }

        private void OnDestroy()
        {
            if (healthComponent != null)
            {
                healthComponent.OnDeath.RemoveListener(HandleHealthDeath);
            }
        }
    }
}
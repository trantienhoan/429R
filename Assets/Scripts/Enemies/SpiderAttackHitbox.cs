using Core;
using UnityEngine;
using Cam;

namespace Enemies
{
    public class SpiderAttackHitbox : MonoBehaviour
    {
        [Header("Explosion Settings")]
        [SerializeField] private float damage = 10f;
        [SerializeField] private float pushForce = 5f;
        [SerializeField] private float explosionRadius = 3f;

        [Header("VFX & SFX")]
        [SerializeField] private ParticleSystem explosionVFX;
        [SerializeField] private AudioClip explosionSFX;
        [SerializeField] private float shakeIntensity = 0.05f;
        [SerializeField] private float shakeDuration = 0.3f;

        private GameObject owner;
        private bool hasExploded;

        public void Initialize(GameObject owner, float damage, float pushForce)
        {
            this.owner = owner;
            this.damage = damage;
            this.pushForce = pushForce;
            hasExploded = false;
        }

        public void TriggerExplosion()
        {
            if (hasExploded) return;
            hasExploded = true;

            // 🔊 VFX & SFX
            if (explosionVFX != null)
            {
                var fx = Instantiate(explosionVFX, transform.position, Quaternion.identity);
                fx.Play();
                Destroy(fx.gameObject, fx.main.duration + fx.main.startLifetime.constantMax);
            }

            if (explosionSFX != null)
                AudioSource.PlayClipAtPoint(explosionSFX, transform.position);

            VRRigShake.Instance?.Shake(shakeIntensity, shakeDuration);

            // 💥 Explosion Logic
            Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius);
            foreach (var hit in hits)
            {
                if (hit.CompareTag("Player") || hit.CompareTag("TreeOfLight") || hit.CompareTag("Furniture"))
                {
                    var hp = hit.GetComponent<HealthComponent>();
                    if (hp != null)
                    {
                        Vector3 hitDir = (hit.transform.position - transform.position).normalized;
                        hp.TakeDamage(damage, hitDir, owner);
                    }

                    var rb = hit.attachedRigidbody;
                    if (rb != null)
                    {
                        Vector3 pushDir = (hit.transform.position - transform.position).normalized;
                        rb.AddForce(pushDir * pushForce, ForceMode.Impulse);
                    }
                }
            }
        }
    }
}
using Core;
using Cam;
using UnityEngine;

namespace Enemies
{
    public class SpiderAttackHitbox : MonoBehaviour
    {
        [Header("Explosion Settings")]
        [SerializeField] private float damage = 10f;
        [SerializeField] private float pushForce = 5f;
        [SerializeField] private float lifeDuration = 0.5f;

        [Header("VFX & SFX")]
        [SerializeField] private ParticleSystem explosionVFX;
        [SerializeField] private AudioClip explosionSFX;
        [SerializeField] private float shakeIntensity = 0.5f;
        [SerializeField] private float shakeDuration = 0.3f;

        private GameObject owner;
        private bool hasExploded = false;

        private void Start()
        {
            // Optional: auto destroy if left active too long
            Destroy(gameObject, lifeDuration);
        }

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

            // ✅ VFX: Spawn detached, self-destroying copy
            if (explosionVFX != null)
            {
                ParticleSystem fx = Instantiate(explosionVFX, transform.position, Quaternion.identity);
                fx.Play();
                Destroy(fx.gameObject, fx.main.duration + fx.main.startLifetime.constantMax); // clean up after
            }

            // ✅ SFX: Still works fine
            if (explosionSFX != null)
                AudioSource.PlayClipAtPoint(explosionSFX, transform.position);

            // ✅ Screen shake
            VRRigShake.Instance?.Shake(shakeIntensity, shakeDuration);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!hasExploded) return;

            if (other.CompareTag("Player") || other.CompareTag("TreeOfLight") || other.CompareTag("Furniture"))
            {
                var hp = other.GetComponent<HealthComponent>();
                if (hp != null)
                {
                    Vector3 hitDir = (other.transform.position - transform.position).normalized;
                    hp.TakeDamage(damage, hitDir, owner);
                }

                Rigidbody rb = other.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Vector3 forceDir = (other.transform.position - transform.position).normalized;
                    rb.AddForce(forceDir * pushForce, ForceMode.Impulse);
                }
            }
        }
    }
}
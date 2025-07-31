using UnityEngine;
using Cam;
using Core;

namespace Enemies
{
    public class SpiderAttackHitbox : MonoBehaviour
    {
        [Header("Explosion Settings")]
        [SerializeField] private float damage = 10f;
        [SerializeField] private float pushForce = 5f;

        [Header("VFX & SFX")]
        [SerializeField] private ParticleSystem explosionVFX;
        [SerializeField] private AudioClip explosionSfx;
        [SerializeField] private float shakeIntensity = 0.05f;
        [SerializeField] private float shakeDuration = 0.3f;

        [Header("Debug")]
        [SerializeField] private bool drawDebugGizmos = true;
        [SerializeField] private Color gizmoColor = Color.red;
        
        [Header("Continuous Push Settings")]
        [SerializeField] private float continuousPushForce = 2f; 
        [SerializeField] private bool applyContinuousPush = true;

        [Header("Directional Hit Settings")]
        [SerializeField] private Vector3 boxSize = new Vector3(0.5f, 0.5f, 1f);  // Small scene: width/height/depth (forward)
        [SerializeField] private float castDistance = 1f;  // Reach forward
        [SerializeField] private float directionalPushForce = 15f;  // Forward knockback

        private SphereCollider sphereCollider;

        private GameObject owner;
        private bool hasExploded;

        // Public property to expose damage for TreeOfLightPot
        private float damageAmount => damage;

        private void Awake()
        {
            sphereCollider = GetComponent<SphereCollider>();
            if (sphereCollider != null)
            {
                sphereCollider.isTrigger = true;  // Ensure it's a trigger for OnTriggerStay
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (!gameObject.activeInHierarchy || hasExploded || !applyContinuousPush) return;

            if (other.CompareTag("Player") || other.CompareTag("TreeOfLight") || other.CompareTag("Furniture"))
            {
                Rigidbody rb = other.attachedRigidbody;
                if (rb != null && rb.mass > 0f)
                {
                    Vector3 pushDir = (other.transform.position - transform.position).normalized;
                    rb.AddForce(pushDir * continuousPushForce * Time.deltaTime, ForceMode.Force); 
                }
            }
        }

        public void TriggerExplosion()
        {
            if (!isActiveAndEnabled || hasExploded) return;
            hasExploded = true;

            // 🔊 VFX & SFX
            if (explosionVFX != null)
            {
                var fx = Instantiate(explosionVFX, transform.position, Quaternion.identity);
                fx.Play();
                Destroy(fx.gameObject, fx.main.duration + fx.main.startLifetime.constantMax);
            }

            if (explosionSfx != null)
            {
                AudioSource.PlayClipAtPoint(explosionSfx, transform.position);
            }

            VRRigShake.Instance?.Shake(shakeIntensity, shakeDuration);

            // 💥 Explosion Logic (radial)
            float currentRadius = sphereCollider != null ? sphereCollider.radius : 3f;  // Fallback if no collider
            Collider[] hits = new Collider[32];
            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, currentRadius, hits);
            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = hits[i];
                if (hit == null) continue;

                if (hit.CompareTag("Player") || hit.CompareTag("TreeOfLight") || hit.CompareTag("Furniture"))
                {
                    var hp = hit.GetComponent<HealthComponent>();
                    if (hp != null)
                    {
                        Vector3 hitPoint = hit.ClosestPoint(transform.position);
                        hp.TakeDamage(damage, hitPoint, owner, true);
                    }

                    var rb = hit.attachedRigidbody;
                    if (rb != null && rb.mass > 0f)
                    {
                        Vector3 pushDir = (hit.transform.position - transform.position).normalized;
                        rb.AddForce(pushDir * pushForce, ForceMode.Impulse);
                    }
                }
            }

            // Directional hit for forward impact
            DirectionalHit();
        }

        private void DirectionalHit()
        {
            RaycastHit[] directionalHits = Physics.BoxCastAll(transform.position, boxSize / 2f, transform.forward, transform.rotation, castDistance);
            foreach (RaycastHit hit in directionalHits)
            {
                if (hit.collider.CompareTag("Player") || hit.collider.CompareTag("TreeOfLight") || hit.collider.CompareTag("Furniture"))
                {
                    Rigidbody rb = hit.collider.attachedRigidbody;
                    if (rb != null && rb.mass > 0f)
                    {
                        Vector3 pushDir = transform.forward;  // Directional push
                        rb.AddForceAtPosition(pushDir * directionalPushForce, hit.point, ForceMode.Impulse);  // At point for rotation
                        Debug.Log("Directional Push on: " + hit.collider.name);
                    }
                    // Damage (if not in radial)
                    HealthComponent hp = hit.collider.GetComponent<HealthComponent>();
                    if (hp != null) hp.TakeDamage(damageAmount, hit.point, owner, true);
                }
            }
        }
        
        public void Initialize(GameObject newOwner, float newDamage = -1f, float newPushForce = -1f, float radius = -1f, bool isKamikaze = false)
        {
            owner = newOwner;
            // Only override serialized values if parameters are provided (non-negative)
            if (newDamage >= 0f)
            {
                damage = newDamage;
            }
            if (newPushForce >= 0f)
            {
                pushForce = newPushForce;
            }
            hasExploded = false;
            if (radius >= 0f && sphereCollider != null)
            {
                sphereCollider.radius = radius;
            }
            if (isKamikaze)
            {
                boxSize *= 3f;  // 3x size for kamikaze
                castDistance *= 3f;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawDebugGizmos) return;
            Gizmos.color = gizmoColor;
            float gizmoRadius = sphereCollider != null ? sphereCollider.radius : 3f;
            Gizmos.DrawWireSphere(transform.position, gizmoRadius);

            // Directional box preview
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(transform.position + transform.forward * (castDistance / 2f), boxSize);
        }
    }
}
using UnityEngine;
using Cam;
using Core;

namespace Enemies
{
    public class SpiderAttackHitbox : MonoBehaviour
    {
        [Header("Explosion Settings")]
        [SerializeField] private float baseDamage = 10f;  // Base at scale=1
        [SerializeField] private float basePushForce = 5f;

        [Header("VFX & SFX")]
        [SerializeField] private ParticleSystem explosionVFX;
        [SerializeField] private AudioClip explosionSfx;
        [SerializeField] private float shakeIntensity = 0.05f;
        [SerializeField] private float shakeDuration = 0.3f;

        [Header("Debug")]
        [SerializeField] private bool drawDebugGizmos = true;
        [SerializeField] private Color gizmoColor = Color.red;
        
        [Header("Continuous Push Settings")]
        [SerializeField] private float baseContinuousPushForce = 2f; 
        [SerializeField] private bool applyContinuousPush = true;

        [Header("Directional Hit Settings")]
        [SerializeField] private Vector3 baseBoxSize = new Vector3(0.5f, 0.5f, 1f);  // Base at scale=1
        [SerializeField] private float baseCastDistance = 1f;  // Base at scale=1
        [SerializeField] private float baseDirectionalPushForce = 15f;  // Base at scale=1

        [Header("Scaling Settings")]
        [SerializeField] private bool scaleWithParent = true;  // Toggle for growth
        [SerializeField] private float damageScaleMultiplier = 1f;  // Extra tuning (e.g., 1.5f for stronger at large size)

        private SphereCollider sphereCollider;
        private ShadowMonster parentMonster;  // Reference to parent for scale

        private GameObject owner;
        private bool hasExploded;

        // Scaled values (computed on init)
        private float scaledDamage;
        private float scaledPushForce;
        private float scaledContinuousPushForce;
        private Vector3 scaledBoxSize;
        private float scaledCastDistance;
        private float scaledDirectionalPushForce;

        // Public property to expose damage for TreeOfLightPot
        public float DamageAmount => scaledDamage;

        private void Awake()
        {
            sphereCollider = GetComponent<SphereCollider>();
            if (sphereCollider != null)
            {
                sphereCollider.isTrigger = true;  // Ensure it's a trigger for OnTriggerStay
            }

            // Find parent monster (assume hitbox is child)
            parentMonster = GetComponentInParent<ShadowMonster>();
            if (parentMonster == null)
            {
                Debug.LogWarning("[SpiderAttackHitbox] No parent ShadowMonster found—scaling disabled.");
                scaleWithParent = false;
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
                    rb.AddForce(pushDir * scaledContinuousPushForce * Time.deltaTime, ForceMode.Force); 
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
                        hp.TakeDamage(scaledDamage, hitPoint, owner, true);
                    }

                    var rb = hit.attachedRigidbody;
                    if (rb != null && rb.mass > 0f)
                    {
                        Vector3 pushDir = (hit.transform.position - transform.position).normalized;
                        rb.AddForce(pushDir * scaledPushForce, ForceMode.Impulse);
                    }
                }
            }

            // Directional hit for forward impact
            DirectionalHit();
        }

        private void DirectionalHit()
        {
            RaycastHit[] directionalHits = new RaycastHit[32];
            int hitCount = Physics.BoxCastNonAlloc(transform.position, scaledBoxSize / 2f, transform.forward, directionalHits, transform.rotation, scaledCastDistance);
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = directionalHits[i];
                if (hit.collider == null) continue;

                if (hit.collider.CompareTag("Player") || hit.collider.CompareTag("TreeOfLight") || hit.collider.CompareTag("Furniture"))
                {
                    Rigidbody rb = hit.collider.attachedRigidbody;
                    if (rb != null && rb.mass > 0f)
                    {
                        Vector3 pushDir = transform.forward;  // Directional push
                        rb.AddForceAtPosition(pushDir * scaledDirectionalPushForce, hit.point, ForceMode.Impulse);  // At point for rotation
                        Debug.Log("Directional Push on: " + hit.collider.name);
                    }
                    // Damage (if not in radial)
                    HealthComponent hp = hit.collider.GetComponent<HealthComponent>();
                    if (hp != null) hp.TakeDamage(DamageAmount, hit.point, owner, true);
                }
            }
        }
        
        public void Initialize(GameObject newOwner, float newDamage = -1f, float newPushForce = -1f, float radius = -1f, bool isKamikaze = false)
        {
            owner = newOwner;
            // Only override serialized values if parameters are provided (non-negative)
            if (newDamage >= 0f)
            {
                baseDamage = newDamage;
            }
            if (newPushForce >= 0f)
            {
                basePushForce = newPushForce;
            }
            hasExploded = false;
            if (radius >= 0f && sphereCollider != null)
            {
                sphereCollider.radius = radius;
            }
            if (isKamikaze)
            {
                baseBoxSize *= 3f;  // 3x size for kamikaze
                baseCastDistance *= 3f;
            }

            // Apply scaling based on parent
            float scaleFactor = 1f;
            if (scaleWithParent && parentMonster != null)
            {
                scaleFactor = parentMonster.transform.localScale.y;  // Use Y as size proxy (or average XYZ)
            }

            // Compute scaled values
            scaledDamage = baseDamage * scaleFactor * damageScaleMultiplier;
            scaledPushForce = basePushForce * scaleFactor;
            scaledContinuousPushForce = baseContinuousPushForce * scaleFactor;
            scaledBoxSize = baseBoxSize * scaleFactor;
            scaledCastDistance = baseCastDistance * scaleFactor;
            scaledDirectionalPushForce = baseDirectionalPushForce * scaleFactor;

            // Optional: Scale collider radius too (if not set via param)
            if (radius < 0f && sphereCollider != null)
            {
                sphereCollider.radius *= scaleFactor;
            }

            Debug.Log($"[SpiderAttackHitbox] Initialized with scaleFactor={scaleFactor}, scaledDamage={scaledDamage}, scaledRadius={(sphereCollider ? sphereCollider.radius : 0)}");
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawDebugGizmos) return;
            Gizmos.color = gizmoColor;
            float gizmoRadius = sphereCollider != null ? sphereCollider.radius : 3f;
            Gizmos.DrawWireSphere(transform.position, gizmoRadius);

            // Directional box preview (use base for static view, or scaled if desired)
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(transform.position + transform.forward * (baseCastDistance / 2f), baseBoxSize);
        }
    }
}
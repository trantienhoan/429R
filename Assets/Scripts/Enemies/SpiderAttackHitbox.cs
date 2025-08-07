using UnityEngine;
using Cam;
using Core;
using System.Collections.Generic;

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
        [SerializeField] private ForceMode pushForceMode = ForceMode.Impulse;  // New: Configurable (Impulse for natural, VelocityChange for override)

        [Header("Scaling Settings")]
        [SerializeField] private bool scaleWithParent = true;  // Toggle for growth
        [SerializeField] private float damageScaleMultiplier = 1f;  // Extra tuning (e.g., 1.5f for stronger at large size)
        [SerializeField] private float minRadius = 0.1f;  // Minimum to prevent zero radius
        [SerializeField] private float minPushForce = 5f;  // New: Min force to ensure push even at small scale

        [Header("Target Settings")]  // New: Configurable
        [SerializeField] private LayerMask targetLayerMask;  // Layers to hit (assign in Inspector: Player, Enemy, etc.)
        [SerializeField] private string[] targetTags = { "Player", "TreeOfLight", "Furniture", "Enemy" };  // Configurable tags

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

        // For kamikaze-specific logic
        private bool isKamikaze;

        // Public property to expose damage for TreeOfLightPot
        public float DamageAmount => scaledDamage;

        private void Awake()
        {
            sphereCollider = GetComponent<SphereCollider>();
            if (sphereCollider == null)
            {
                Debug.LogWarning("[SpiderAttackHitbox] No SphereCollider found – adding one dynamically.");
                sphereCollider = gameObject.AddComponent<SphereCollider>();
            }
            sphereCollider.isTrigger = true;  // Ensure it's a trigger for OnTriggerStay
            sphereCollider.radius = Mathf.Max(sphereCollider.radius, minRadius);  // Prevent zero

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

            if (IsValidTarget(other.gameObject))
            {
                Rigidbody rb = other.attachedRigidbody;
                if (rb != null && rb.gameObject != owner)  // Skip owner
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

            // 💥 Explosion Logic (radial) - Simplified without HashSet
            float currentRadius = Mathf.Max(sphereCollider.radius, minRadius);  // Ensure min
            Collider[] hits = Physics.OverlapSphere(transform.position, currentRadius, targetLayerMask);  // Allocating for simplicity (64 max not needed)
            foreach (Collider hit in hits)
            {
                if (hit == null || hit.gameObject == owner) continue;

                GameObject hitObject = hit.gameObject;
                if (IsValidTarget(hitObject))
                {
                    HealthComponent hp = hitObject.GetComponent<HealthComponent>();
                    if (hp != null)
                    {
                        float finalDamage = scaledDamage;

                        // Reduce damage to 1/10 if kamikaze and target is Enemy
                        if (isKamikaze && hitObject.CompareTag("Enemy"))
                        {
                            finalDamage *= 0.1f;
                        }

                        Vector3 hitPoint = hit.ClosestPoint(transform.position);
                        hp.TakeDamage(finalDamage, hitPoint, owner, true);
                    }

                    Rigidbody rb = hit.attachedRigidbody;
                    if (rb != null)
                    {
                        Vector3 pushDir = (hitObject.transform.position - transform.position).normalized;
                        float effectiveForce = Mathf.Max(scaledPushForce, minPushForce);  // Ensure min force
                        rb.AddForce(pushDir * effectiveForce, pushForceMode);
                        Debug.Log($"[Radial Push] Applied {effectiveForce} force to {hitObject.name} (mass={rb.mass}, kinematic={rb.isKinematic}, mode={pushForceMode})");
                    }
                }
            }

            // Directional hit for forward impact
            DirectionalHit();
        }

        private void DirectionalHit()
        {
            RaycastHit[] directionalHits = Physics.BoxCastAll(transform.position, scaledBoxSize / 2f, transform.forward, transform.rotation, scaledCastDistance, targetLayerMask);  // Allocating for simplicity
            foreach (RaycastHit hit in directionalHits)
            {
                if (hit.collider == null || hit.collider.gameObject == owner) continue;

                GameObject hitObject = hit.collider.gameObject;
                if (IsValidTarget(hitObject))
                {
                    HealthComponent hp = hitObject.GetComponent<HealthComponent>();
                    if (hp != null)
                    {
                        float finalDamage = scaledDamage;

                        // Reduce damage to 1/10 if kamikaze and target is Enemy
                        if (isKamikaze && hitObject.CompareTag("Enemy"))
                        {
                            finalDamage *= 0.1f;
                        }

                        hp.TakeDamage(finalDamage, hit.point, owner, true);
                    }

                    Rigidbody rb = hit.collider.attachedRigidbody;
                    if (rb != null)
                    {
                        Vector3 pushDir = transform.forward;  // Directional push
                        float effectiveForce = Mathf.Max(scaledDirectionalPushForce, minPushForce);  // Ensure min force
                        rb.AddForceAtPosition(pushDir * effectiveForce, hit.point, pushForceMode);  // At point for rotation
                        Debug.Log($"Directional Push on: {hit.collider.name} with {effectiveForce} force (mass={rb.mass}, kinematic={rb.isKinematic}, mode={pushForceMode})");
                    }
                }
            }
        }
        
        public void Initialize(GameObject newOwner, float newDamage = -1f, float newPushForce = -1f, float radius = -1f, bool kamikaze = false)
        {
            owner = newOwner;
            isKamikaze = kamikaze;  // Set kamikaze flag
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
                sphereCollider.radius = Mathf.Max(radius, minRadius);  // Apply min radius
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
                sphereCollider.radius = Mathf.Max(sphereCollider.radius * scaleFactor, minRadius);
            }

            Debug.Log($"[SpiderAttackHitbox] Initialized with scaleFactor={scaleFactor}, scaledDamage={scaledDamage}, scaledRadius={(sphereCollider ? sphereCollider.radius : 0)}");
        }

        // New: Helper to check if target is valid (tags + layer)
        private bool IsValidTarget(GameObject target)
        {
            if (target == null) return false;
            foreach (string tag in targetTags)
            {
                if (target.CompareTag(tag)) return true;
            }
            return false;
        }

        // New: Reset for pooling/reuse
        public void ResetHitbox()
        {
            hasExploded = false;
            // Reset other states if needed
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
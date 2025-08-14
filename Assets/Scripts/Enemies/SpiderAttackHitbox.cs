using System.Collections.Generic;
using UnityEngine;

namespace Enemies
{
    [DisallowMultipleComponent]
    public class SpiderAttackHitbox : MonoBehaviour
    {
        [Header("Scale Source (set to spider root that actually grows)")]
        public Transform scaleSource;          // drag your spider root here
        public bool fallbackToSelfIfNull = true;
        [Tooltip("Clamp growth multiplier so physics doesn't explode.")]
        public Vector2 scaleClamp = new Vector2(0.25f, 5f);

        [Header("Base Settings")]
        [Tooltip("Base (pre-multipliers) absolute damage per activation.")]
        public float baseDamage = 10f;
        [Tooltip("Base (pre-multipliers) radial push impulse.")]
        public float basePushForce = 5f;
        [Tooltip("Base (pre-multipliers) explosion radius.")]
        public float baseExplosionRadius = 1.0f;

        [Header("VFX & SFX")]
        public ParticleSystem explosionVFX;
        public AudioSource explosionSfx;
        public float shakeIntensity = 0.01f;
        public float shakeDuration = 0.1f;

        [Header("Debug")]
        public bool debugVerboseLogs = false;   // turn on for deep debugging
        public bool drawDebugGizmos = true;
        public Color gizmoColor = Color.red;

        [Header("Continuous Push Settings")]
        public bool applyContinuousPush = true;
        public float baseContinuousPush = 50f;   // Force mode = Force (per FixedUpdate)

        [Header("Directional Hit Settings")]
        public Vector3 baseBoxSize = new Vector3(0.5f, 0.5f, 0.5f);
        public float baseCastDistance = 1f;
        public float baseDirectionalPushForce = 15f;
        public ForceMode pushForceMode = ForceMode.Impulse;

        [Header("Scaling Multipliers (designer tuning knobs)")]
        [Tooltip("Extra designer-controlled multiplier on top of growth scale.")]
        public float damageScaleMultiplier = 1f;
        public float forceScaleMultiplier = 1f;
        public float sizeScaleMultiplier = 1f;  // can tune feel without changing model scale
        public float minRadius = 0.1f;
        public float minPushForce = 5f;

        [Header("Target Settings")]
        public LayerMask targetLayerMask;
        [Tooltip("If empty, any tag on the layerMask is accepted.")]
        public List<string> targetTags = new List<string>(); // e.g. ["Player","Furniture"]

        [Header("Performance")]
        [Tooltip("Max colliders processed per query; increase if you have dense scenes.")]
        public int overlapBufferSize = 32;

        // --- runtime (per-activation) ---
        float _activeUntil = -1f;
        bool _doExplosion;
        bool _doDirectional;
        float _damageMul = 1f, _pushMul = 1f;

        // caches
        readonly HashSet<Rigidbody> _impulsedThisActivation = new HashSet<Rigidbody>();
        Collider[] _overlapBuffer;

        // properties
        public bool IsActive => Time.time <= _activeUntil;

        // —— Public API ————————————————————————————————————————————————————————

        /// <summary>
        /// Configure absolute damage, radius scale multiplier, and push force for upcoming activations.
        /// Call this before Activate(...) for each attack flavor (normal/dive/kamikaze).
        /// </summary>
        public void Configure(float absoluteDamage, float radiusScale, float pushForce)
        {
            baseDamage = Mathf.Max(0f, absoluteDamage);
            sizeScaleMultiplier = Mathf.Max(0.01f, radiusScale);
            basePushForce = Mathf.Max(0f, pushForce);
            // Keep baseDirectionalPushForce synced unless you want different behavior
            baseDirectionalPushForce = Mathf.Max(0f, pushForce);
        }

        /// <summary>
        /// Activate the hitbox for a duration.
        /// explode=true  => one-shot radial blast on activation (+ optional continuous if enabled)
        /// directional=true => forward box push every FixedUpdate (good for slashes).
        /// damageMultiplier/pushMultiplier are extra per-activation knobs.
        /// </summary>
        public void Activate(
            float duration,
            bool explode,
            bool directional,
            float damageMultiplier = 1f,
            float pushMultiplier   = 1f)
        {
            _activeUntil = Time.time + Mathf.Max(0.01f, duration);
            _doExplosion = explode;
            _doDirectional = directional;
            _damageMul = damageMultiplier;
            _pushMul = pushMultiplier;
            _impulsedThisActivation.Clear();

            if (_doExplosion)
                RadialBlast();

            if (debugVerboseLogs)
            {
                Debug.Log($"[SpiderAttackHitbox] Activate(explode={_doExplosion}, directional={_doDirectional}, dur={duration})");
                Debug.Log($"[SpiderAttackHitbox] VFX: {(IsAlive(explosionVFX) ? "OK" : "NULL")} | SFX: {(IsAlive(explosionSfx) ? "OK" : "NULL")}");
            }

            // VFX/SFX are best-effort; guard against destroyed refs when pooling
            if (_doExplosion && IsAlive(explosionVFX))
            {
                explosionVFX.Play(true);
            }
            if (_doExplosion && IsAlive(explosionSfx))
            {
                // Optional: set clip/volume here
                explosionSfx.Play();
            }

            // camera shake hook goes here (if you have a shaker)
        }

        // —— Unity ———————————————————————————————————————————————————————————————

        void Awake()
        {
            if (overlapBufferSize < 1) overlapBufferSize = 1;
            _overlapBuffer = new Collider[overlapBufferSize];
        }

        void OnDisable()
        {
            // Clear activation state so pooled objects don't linger "active"
            _activeUntil = -1f;
            _doExplosion = _doDirectional = false;
            _impulsedThisActivation.Clear();

            // stop VFX if still playing (pool-safety)
            if (IsAlive(explosionVFX)) explosionVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        void FixedUpdate()
        {
            if (!IsActive) return;

            if (applyContinuousPush)
                ContinuousSpherePush();

            if (_doDirectional)
                DirectionalOverlapPush();
        }

        void OnValidate()
        {
            scaleClamp.x = Mathf.Max(0.0001f, Mathf.Min(scaleClamp.x, scaleClamp.y));
        }

        // —— Internals ———————————————————————————————————————————————————————————

        float GrowthScale01()
        {
            Transform s = scaleSource != null ? scaleSource : (fallbackToSelfIfNull ? transform : null);
            if (s == null) return 1f;

            // use a uniform estimate for stability
            float k = Mathf.Max(0.0001f, s.lossyScale.x);
            return Mathf.Clamp(k, scaleClamp.x, scaleClamp.y);
        }

        void RadialBlast()
        {
            float g = GrowthScale01();
            float radius = Mathf.Max(minRadius, baseExplosionRadius * sizeScaleMultiplier * g);

            int count = Physics.OverlapSphereNonAlloc(
                transform.position, radius,
                _overlapBuffer, targetLayerMask, QueryTriggerInteraction.Collide);

            if (debugVerboseLogs)
                Debug.Log($"[SpiderAttackHitbox] RadialBlast: {count} hits @ r={radius:F2}");

            for (int i = 0; i < count; i++)
            {
                var c = _overlapBuffer[i];
                if (!IsValidTarget(c)) continue;

                var rb = c.attachedRigidbody;
                if (rb == null || rb.isKinematic) continue;

                // Damage
                var health = c.GetComponent<Core.HealthComponent>() ?? c.GetComponentInParent<Core.HealthComponent>();
                if (health != null)
                {
                    float damage = baseDamage * damageScaleMultiplier * _damageMul * g;
                    // Prefer the 4-arg signature if available in your project:
                    health.TakeDamage(damage, transform.position, gameObject, /*isExplosion:*/ true);
                    if (debugVerboseLogs) Debug.Log($"[Damage] Explode {damage:F1} -> {c.name}");
                }

                // Push (radial)
                var dir = (c.transform.position - transform.position).normalized;
                float force = Mathf.Max(minPushForce, basePushForce * forceScaleMultiplier * _pushMul * g);
                rb.AddForce(dir * force, pushForceMode);
                if (debugVerboseLogs) Debug.Log($"[Push] Explode force {force:F1} -> {c.name}");
            }
        }

        void DirectionalOverlapPush()
        {
            float g = GrowthScale01();

            // forward box centered half distance ahead
            Vector3 size = Vector3.Max(Vector3.one * 0.01f, baseBoxSize) * (sizeScaleMultiplier * g);
            Vector3 half = size * 0.5f;
            float dist = Mathf.Max(0.01f, baseCastDistance) * (sizeScaleMultiplier * g);
            Vector3 center = transform.position + transform.forward * (dist * 0.5f);

            int count = Physics.OverlapBoxNonAlloc(
                center, half, _overlapBuffer,
                transform.rotation, targetLayerMask, QueryTriggerInteraction.Collide);

            if (debugVerboseLogs)
                Debug.Log($"[SpiderAttackHitbox] DirectionalOverlap: {count} hits");

            for (int i = 0; i < count; i++)
            {
                var c = _overlapBuffer[i];
                if (!IsValidTarget(c)) continue;

                var rb = c.attachedRigidbody;
                if (rb == null || rb.isKinematic) continue;

                // Damage (you can gate to 1x per activation if desired)
                var health = c.GetComponent<Core.HealthComponent>() ?? c.GetComponentInParent<Core.HealthComponent>();
                if (health != null)
                {
                    float damage = baseDamage * damageScaleMultiplier * _damageMul * g;
                    health.TakeDamage(damage, transform.position, gameObject, /*isExplosion:*/ false);
                    if (debugVerboseLogs) Debug.Log($"[Damage] Directional {damage:F1} -> {c.name}");
                }

                // Single impulse per activation to avoid machine-gunning the same body
                if (_impulsedThisActivation.Add(rb))
                {
                    float force = Mathf.Max(minPushForce, baseDirectionalPushForce * forceScaleMultiplier * _pushMul * g);
                    rb.AddForce(transform.forward * force, pushForceMode);
                    if (debugVerboseLogs) Debug.Log($"[Push] Directional force {force:F1} -> {c.name}");
                }
            }
        }

        void ContinuousSpherePush()
        {
            float g = GrowthScale01();
            float radius = Mathf.Max(minRadius, baseExplosionRadius * sizeScaleMultiplier * g);

            int count = Physics.OverlapSphereNonAlloc(
                transform.position, radius,
                _overlapBuffer, targetLayerMask, QueryTriggerInteraction.Collide);

            if (debugVerboseLogs)
                Debug.Log($"[SpiderAttackHitbox] Continuous: {count} hits @ r={radius:F2}");

            for (int i = 0; i < count; i++)
            {
                var c = _overlapBuffer[i];
                if (!IsValidTarget(c)) continue;

                var rb = c.attachedRigidbody;
                if (rb == null || rb.isKinematic) continue;

                // Continuous damage (smaller, frame-rate independent)
                var health = c.GetComponent<Core.HealthComponent>() ?? c.GetComponentInParent<Core.HealthComponent>();
                if (health != null)
                {
                    float dps = baseDamage * damageScaleMultiplier * _damageMul * g * 0.5f; // tune 0.5f as needed
                    float dmg = dps * Time.fixedDeltaTime;
                    health.TakeDamage(dmg, transform.position, gameObject, /*isExplosion:*/ false);
                    if (debugVerboseLogs) Debug.Log($"[Damage] Continuous +{dmg:F2} -> {c.name}");
                }

                // Continuous push (Force mode)
                var dir = (c.transform.position - transform.position).normalized;
                float f = baseContinuousPush * forceScaleMultiplier * _pushMul * Time.fixedDeltaTime * g;
                rb.AddForce(dir * f, ForceMode.Force);
                if (debugVerboseLogs) Debug.Log($"[Push] Continuous +{f:F2} -> {c.name}");
            }
        }

        bool IsValidTarget(Collider c)
        {
            if (targetTags == null || targetTags.Count == 0) return true;
            // List<string>.Contains is okay; CompareTag is faster if you only have 1 tag.
            return targetTags.Contains(c.tag);
        }

        static bool IsAlive(Object o)
        {
            // Unity null + destroyed object guard
            return o != null;
        }

        void OnDrawGizmosSelected()
        {
            if (!drawDebugGizmos) return;
            Gizmos.color = gizmoColor;

            float g = Application.isPlaying ? GrowthScale01() : 1f;

            // sphere (explosion / continuous)
            float radius = Mathf.Max(minRadius, baseExplosionRadius * sizeScaleMultiplier * g);
            Gizmos.DrawWireSphere(transform.position, radius);

            // forward box (directional)
            Vector3 size = Vector3.Max(Vector3.one * 0.01f, baseBoxSize) * (sizeScaleMultiplier * g);
            Vector3 center = transform.position + transform.forward * (Mathf.Max(0.01f, baseCastDistance) * (sizeScaleMultiplier * g) * 0.5f);

            var old = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(center, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, size);
            Gizmos.matrix = old;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Enemies
{
    [DisallowMultipleComponent]
    public class SpiderAttackHitbox : MonoBehaviour
    {
        [Header("Scale Source (set to spider root that actually grows)")]
        public Transform scaleSource;          // <- drag your top-level spider object here
        public bool fallbackToSelfIfNull = true;
        [Tooltip("Clamp the growth multiplier so physics doesn't explode.")]
        public Vector2 scaleClamp = new Vector2(0.25f, 5f);

        [Header("Explosion Settings")]
        public float baseDamage = 10f;
        public float basePushForce = 5f;
        public float baseExplosionRadius = 1.0f;

        [Header("VFX & SFX")]
        public ParticleSystem explosionVFX;
        public AudioSource explosionSfx;
        public float shakeIntensity = 0.01f;
        public float shakeDuration = 0.1f;

        [Header("Debug")]
        public bool drawDebugGizmos = true;
        public Color gizmoColor = Color.red;

        [Header("Continuous Push Settings")]
        public float baseContinuousPush = 50f;
        public bool applyContinuousPush = true;

        [Header("Directional Hit Settings")]
        public Vector3 baseBoxSize = new Vector3(0.5f, 0.5f, 0.5f);
        public float baseCastDistance = 1f;
        public float baseDirectionalPushForce = 15f;
        public ForceMode pushForceMode = ForceMode.Impulse;

        [Header("Scaling Settings")]
        [Tooltip("Extra designer-controlled multiplier on top of growth scale.")]
        public float damageScaleMultiplier = 1f;
        public float forceScaleMultiplier = 1f;
        public float sizeScaleMultiplier = 1f;  // can tune feel without changing model scale
        public float minRadius = 0.1f;
        public float minPushForce = 5f;

        [Header("Target Settings")]
        public LayerMask targetLayerMask;
        public List<string> targetTags = new List<string>(); // e.g. ["Player","Furniture"]

        // --- runtime ---
        float activeUntil = -1f;
        bool doExplosion;
        bool doDirectional;
        float damageMul = 1f, pushMul = 1f;

        readonly HashSet<Rigidbody> _impulsedThisActivation = new HashSet<Rigidbody>();
        Collider[] _overlapBuffer = new Collider[32];

        public bool IsActive => Time.time <= activeUntil;

        float GrowthScale01()
        {
            Transform s = scaleSource != null ? scaleSource :
                (fallbackToSelfIfNull ? transform : null);
            if (s == null) return 1f;

            // use a uniform estimate from X to keep stable
            float k = Mathf.Max(0.0001f, s.lossyScale.x);
            k = Mathf.Clamp(k, scaleClamp.x, scaleClamp.y);
            return k;
        }

        // ---- public API ----
        /// Activate the hitbox for duration seconds.
        /// explode=true => one-shot radial blast now.
        /// directional=true => overlap box forward each FixedUpdate.
        public void Activate(float duration, bool explode, bool directional, float damageMultiplier = 1f, float pushMultiplier = 1f)
        {
            activeUntil = Time.time + Mathf.Max(0.01f, duration);
            doExplosion = explode;
            doDirectional = directional;
            damageMul = damageMultiplier;
            pushMul = pushMultiplier;
            _impulsedThisActivation.Clear();

            if (doExplosion) RadialBlast();

            if (doExplosion && explosionVFX) explosionVFX.Play(true);
            if (doExplosion && explosionSfx) explosionSfx.Play();
            // your camera shake hook here if you have one
        }

        void FixedUpdate()
        {
            if (!IsActive) return;

            if (applyContinuousPush)
                ContinuousSpherePush();

            if (doDirectional)
                DirectionalOverlapPush();
        }

        // --- internals ---
        void RadialBlast()
        {
            float g = GrowthScale01();
            float radius = Mathf.Max(minRadius, baseExplosionRadius * sizeScaleMultiplier * g);
            int count = Physics.OverlapSphereNonAlloc(transform.position, radius, _overlapBuffer, targetLayerMask, QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                var c = _overlapBuffer[i];
                if (!IsValidTarget(c)) continue;
                var rb = c.attachedRigidbody;
                if (rb == null || rb.isKinematic) continue;

                var dir = (c.transform.position - transform.position).normalized;
                float force = Mathf.Max(minPushForce, basePushForce * forceScaleMultiplier * pushMul * g);
                rb.AddForce(dir * force, pushForceMode);

                _impulsedThisActivation.Add(rb);
                // TODO call your IDamageable here (damage = baseDamage * damageScaleMultiplier * damageMul * g)
            }
        }

        void DirectionalOverlapPush()
        {
            float g = GrowthScale01();

            // forward box centered a bit in front of the hitbox
            Vector3 size = Vector3.Max(Vector3.one * 0.01f, baseBoxSize) * (sizeScaleMultiplier * g);
            Vector3 half = size * 0.5f;
            float dist = Mathf.Max(0.01f, baseCastDistance) * (sizeScaleMultiplier * g);

            // OverlapBox catches everything inside a box volume; we center it half distance forward
            Vector3 center = transform.position + transform.forward * (dist * 0.5f);
            int count = Physics.OverlapBoxNonAlloc(center, half, _overlapBuffer, transform.rotation, targetLayerMask, QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                var c = _overlapBuffer[i];
                if (!IsValidTarget(c)) continue;

                var rb = c.attachedRigidbody;
                if (rb == null || rb.isKinematic) continue;

                float force = Mathf.Max(minPushForce, baseDirectionalPushForce * forceScaleMultiplier * pushMul * g);
                if (_impulsedThisActivation.Add(rb))
                    rb.AddForce(transform.forward * force, pushForceMode);
            }
        }

        void ContinuousSpherePush()
        {
            float g = GrowthScale01();
            float radius = Mathf.Max(minRadius, baseExplosionRadius * sizeScaleMultiplier * g);
            int count = Physics.OverlapSphereNonAlloc(transform.position, radius, _overlapBuffer, targetLayerMask, QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                var c = _overlapBuffer[i];
                if (!IsValidTarget(c)) continue;

                var rb = c.attachedRigidbody;
                if (rb == null || rb.isKinematic) continue;

                var dir = (c.transform.position - transform.position).normalized;
                float f = baseContinuousPush * forceScaleMultiplier * pushMul * Time.fixedDeltaTime * g;
                rb.AddForce(dir * f, ForceMode.Force);
            }
        }

        bool IsValidTarget(Collider c)
        {
            if (targetTags == null || targetTags.Count == 0) return true;
            return targetTags.Contains(c.tag);
        }

        void OnDrawGizmosSelected()
        {
            if (!drawDebugGizmos) return;
            Gizmos.color = gizmoColor;

            float g = Application.isPlaying ? GrowthScale01() : 1f;
            float radius = Mathf.Max(minRadius, baseExplosionRadius * sizeScaleMultiplier * g);
            Gizmos.DrawWireSphere(transform.position, radius);

            Vector3 size = Vector3.Max(Vector3.one * 0.01f, baseBoxSize) * (sizeScaleMultiplier * g);
            Vector3 center = transform.position + transform.forward * (Mathf.Max(0.01f, baseCastDistance) * (sizeScaleMultiplier * g) * 0.5f);
            Matrix4x4 m = Matrix4x4.TRS(center, transform.rotation, Vector3.one);
            var old = Gizmos.matrix;
            Gizmos.matrix = m;
            Gizmos.DrawWireCube(Vector3.zero, size);
            Gizmos.matrix = old;
        }
    }
}

using UnityEngine;
//using Core;

namespace Core
{
    [RequireComponent(typeof(BoxCollider))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(HealthComponent))]
    public class JiggleBreakableBigObject : BreakableObject
    {
        [Header("Jiggle Settings")]
        [SerializeField] private float jiggleAmount = 0.1f;
        [SerializeField] private float jiggleDuration = 0.5f;
        [SerializeField] private AnimationCurve jiggleCurve;

        [Header("Physics Settings")]
        [SerializeField] private float objectMass = 50f;

        private BoxCollider boxCollider;
        private Rigidbody rigidBody;
        private Vector3 originalPosition;
        private Quaternion originalRotation;
        private float jiggleTimer;
        private bool isJiggling;
        private Vector3 jiggleDirection;
        private HealthComponent healthComponent;

        protected override void Awake()
        {
            base.Awake();

            boxCollider = GetComponent<BoxCollider>();
            rigidBody = GetComponent<Rigidbody>();
            healthComponent = GetComponent<HealthComponent>();

            originalPosition = transform.position;
            originalRotation = transform.rotation;

            SetupPhysics();

            if (Resources.Load<GameObject>("Prefabs/BreakableHealthBar") is GameObject healthBarPrefab)
            {
                GameObject healthBar = Instantiate(healthBarPrefab, transform);
                if (healthBar.TryGetComponent(out BreakableHealthBar bar))
                {
                    bar.Initialize(this);
                }
            }
        }

        private void Start()
        {
            if (healthComponent != null)
            {
                healthComponent.OnDeath.AddListener(HandleHealthDeath);
                Debug.Log($"[JiggleBreakableBigObject {gameObject.name}] Subscribed to OnDeath");
            }
        }

        private void SetupPhysics()
        {
            rigidBody.useGravity = true;
            rigidBody.isKinematic = false;
            rigidBody.mass = objectMass;
            rigidBody.linearDamping = 0.5f;
            rigidBody.interpolation = RigidbodyInterpolation.Interpolate;
            rigidBody.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rigidBody.maxAngularVelocity = 50f;
            rigidBody.solverIterations = 8;
            rigidBody.solverVelocityIterations = 3;

            boxCollider.isTrigger = false;
        }

        private void Update()
        {
            if (!isJiggling) return;

            jiggleTimer += Time.deltaTime;
            float progress = jiggleTimer / jiggleDuration;

            if (progress < 1.0f)
            {
                float curveValue = jiggleCurve != null ? jiggleCurve.Evaluate(progress) : Mathf.Sin(progress * Mathf.PI * 4);
                transform.position = originalPosition + jiggleDirection * jiggleAmount * curveValue;
            }
            else
            {
                transform.position = originalPosition;
                transform.rotation = originalRotation;
                isJiggling = false;
            }
        }

        protected override void OnCollisionEnter(Collision collision)
        {
            if ((collisionLayers.value & (1 << collision.gameObject.layer)) == 0) return;

            bool isWeapon = collision.gameObject.CompareTag("Weapon");
            float impactForce = collision.impulse.magnitude;

            bool shouldBreak = breakOnlyFromWeapons
                ? isWeapon && (!useImpactForce || impactForce >= minimumImpactForce)
                : isWeapon || (useImpactForce && impactForce >= minimumImpactForce);

            if (!shouldBreak) return;

            Vector3 hitPoint = collision.contacts[0].point;
            Vector3 hitDirection = collision.contacts[0].normal;

            TriggerJiggleEffect(hitDirection);

            float damage = useImpactForce
                ? Mathf.Max(impactForce * damageMultiplier, minimumImpactForce)
                : 20f;

            healthComponent.TakeDamage(damage, hitPoint, collision.gameObject);
        }

        private void TriggerJiggleEffect(Vector3 hitDirection)
        {
            jiggleTimer = 0f;
            jiggleDirection = -hitDirection.normalized;
            isJiggling = true;

            originalPosition = transform.position;
            originalRotation = transform.rotation;
        }

        protected override void HandleDestruction()
        {
            if (boxCollider != null)
            {
                boxCollider.enabled = false;
            }

            base.HandleDestruction();
        }

        private void HandleHealthDeath(HealthComponent health)
        {
            HandleBreaking();
            Debug.Log($"[JiggleBreakableBigObject {gameObject.name}] HandleHealthDeath called");
        }

        protected override void HandleBreaking()
        {
            DropItems(transform.position);

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
                Debug.Log($"[JiggleBreakableBigObject {gameObject.name}] Unsubscribed from OnDeath");
            }
        }
    }
}
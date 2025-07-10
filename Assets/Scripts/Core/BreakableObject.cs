using UnityEngine;
using UnityEngine.Events;
using System.Collections;

namespace Core
{
    public abstract class BreakableObject : MonoBehaviour
    {
        [Header("Breakable Object Settings")]
        [SerializeField] protected bool isBreakable = true;

        [Header("Item Drop Settings")]
        [SerializeField] protected GameObject[] itemDropPrefabs;
        [SerializeField] protected float dropForce = 5f;
        [SerializeField] protected float itemSpawnHeightOffset = 0.5f;

        [Header("Destruction Settings")]
        [SerializeField] protected float destroyDelay = 2f;

        [Header("Physics Settings")]
        [SerializeField] protected float mass = 1000f;
        [SerializeField] protected float linearDamping = 1f;
        [SerializeField] protected float angularDamping = 1f;
        [SerializeField] protected bool useGravity = true;
        [SerializeField] protected float breakUpwardForce = 2f;

        [Header("Collision Settings")]
        [SerializeField] protected LayerMask collisionLayers = -1;
        [SerializeField] protected float minimumImpactForce = 5f;
        [SerializeField] protected bool breakOnlyFromWeapons = true;
        [SerializeField] protected bool useImpactForce = true;
        [SerializeField] protected float damageMultiplier = 1f;

        [Header("Events")]
        public UnityEvent onBreak;

        private bool isBroken;
        private Rigidbody rb;

        protected virtual void Awake()
        {
            SetupRigidbody();
        }

        protected virtual void SetupRigidbody()
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }

            rb.mass = mass;
            rb.linearDamping = linearDamping;
            rb.angularDamping = angularDamping;
            rb.useGravity = useGravity;
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.maxAngularVelocity = 50f;
            rb.solverIterations = 6;
            rb.solverVelocityIterations = 2;
        }

        protected virtual void OnCollisionEnter(Collision collision)
        {
            if ((collisionLayers.value & (1 << collision.gameObject.layer)) == 0)
                return;

            bool isWeapon = collision.gameObject.CompareTag("Weapon");
            float impactForce = collision.impulse.magnitude;

            bool shouldBreak = breakOnlyFromWeapons
                ? isWeapon && (!useImpactForce || impactForce >= minimumImpactForce)
                : isWeapon || (useImpactForce && impactForce >= minimumImpactForce);

            if (shouldBreak)
            {
                Break();
            }
        }

        protected virtual void Break()
        {
            if (isBroken) return;

            isBroken = true;
            StartCoroutine(BreakSequence());
        }

        protected virtual IEnumerator BreakSequence()
        {
            yield return new WaitForSeconds(0.1f);

            if (rb != null)
            {
                rb.isKinematic = false;
                rb.AddForce(Vector3.up * breakUpwardForce, ForceMode.VelocityChange);
            }

            HandleDestruction();
        }
        protected abstract void HandleBreaking();

        protected virtual void HandleDestruction()
        {
            DropItems(transform.position);
            onBreak?.Invoke();
            StartCoroutine(DestroyAfterDelay());
        }

        protected virtual IEnumerator DestroyAfterDelay()
        {
            yield return new WaitForSeconds(destroyDelay);
            Destroy(gameObject);
        }

        protected virtual void DropItems(Vector3 originPoint)
        {
            if (itemDropPrefabs == null || itemDropPrefabs.Length == 0) return;

            foreach (var itemPrefab in itemDropPrefabs)
            {
                if (itemPrefab == null) continue;

                Vector3 randomDirection = Random.insideUnitSphere;
                randomDirection.y = Mathf.Abs(randomDirection.y);

                Vector3 spawnPosition = originPoint + Vector3.up * itemSpawnHeightOffset;
                GameObject droppedItem = Instantiate(itemPrefab, spawnPosition, Random.rotation);

                if (droppedItem == null) continue;

                Rigidbody itemRb = droppedItem.GetComponent<Rigidbody>();
                if (itemRb == null)
                    itemRb = droppedItem.AddComponent<Rigidbody>();

                itemRb.useGravity = true;
                itemRb.isKinematic = false;
                itemRb.interpolation = RigidbodyInterpolation.Interpolate;
                itemRb.mass = 1f;

                Vector3 force = randomDirection * dropForce;
                itemRb.AddForce(force, ForceMode.Impulse);
            }
        }
    }
}

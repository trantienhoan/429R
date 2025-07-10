using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.AI;
using Core;

namespace Items
{
    [RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
    public class Weapon : MonoBehaviour
    {
        [Header("Weapon Base Settings")]
        [SerializeField] private WeaponType weaponType = WeaponType.Sword;
        [SerializeField] private float baseDamage = 10f;
        [SerializeField] private float weaponMass = 0.9f;
        [SerializeField] private float impactForceMultiplier = 1f;
        [SerializeField] private float treeOfLightDamageMultiplier = 0.5f;
        [SerializeField] private float shadowMonsterDamageMultiplier = 1.5f;

        [Header("Movement Detection")]
        [SerializeField] private float swingDetectionThreshold = 2.5f;
        [SerializeField] private float verticalSwingThreshold = 0.8f;
        [SerializeField] private float horizontalSwingThreshold = 0.8f;

        [Header("Effects")]
        [SerializeField] private GameObject groundImpactEffectPrefab;
        [SerializeField] private GameObject airSlashEffectPrefab;

        private Rigidbody rb;
        private Vector3 lastPosition;
        private Vector3 movementDirection;
        private float currentSpeed;
        private Vector3 velocityHistory;
        private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;
        private bool isGrabbed;

        private enum WeaponType
        {
            Sword,
            Hammer,
            Axe,
            Spear
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();

            grabInteractable.selectEntered.AddListener(OnGrab);
            grabInteractable.selectExited.AddListener(OnRelease);

            gameObject.tag = "Weapon";
            if (rb != null) rb.mass = weaponMass;
        }

        private void OnGrab(SelectEnterEventArgs args)
        {
            isGrabbed = true;
            rb.isKinematic = true;
        }

        private void OnRelease(SelectExitEventArgs args)
        {
            isGrabbed = false;
            rb.isKinematic = false;
        }

        private void FixedUpdate()
        {
            Vector3 deltaPosition = transform.position - lastPosition;
            currentSpeed = deltaPosition.magnitude / Time.fixedDeltaTime;
            if (deltaPosition.magnitude > 0.001f) movementDirection = deltaPosition.normalized;
            velocityHistory = Vector3.Lerp(velocityHistory, deltaPosition / Time.fixedDeltaTime, 0.5f);
            lastPosition = transform.position;

            if (isGrabbed && currentSpeed > swingDetectionThreshold)
            {
                DetectWeaponSwingPattern();
            }
        }

        private void DetectWeaponSwingPattern()
        {
            float verticalDot = Vector3.Dot(movementDirection, -Vector3.up);
            float horizontalDot = Mathf.Abs(Vector3.Dot(movementDirection, Vector3.right));

            if (weaponType == WeaponType.Hammer && verticalDot > verticalSwingThreshold) { }
            if (weaponType == WeaponType.Sword && horizontalDot > horizontalSwingThreshold) { }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!isGrabbed) return;

            float damageAmount = baseDamage * (currentSpeed / 5f);
            Vector3 collisionPoint = collision.contacts[0].point;
            Vector3 collisionNormal = collision.contacts[0].normal;

            if (weaponType == WeaponType.Hammer &&
                Vector3.Dot(movementDirection, -Vector3.up) > verticalSwingThreshold &&
                collision.gameObject.CompareTag("Ground"))
            {
                if (groundImpactEffectPrefab != null)
                {
                    Instantiate(groundImpactEffectPrefab, collisionPoint, Quaternion.LookRotation(collisionNormal));
                }
                ApplyAreaDamage(collisionPoint, 3f, damageAmount * 1.5f);
            }

            if (weaponType == WeaponType.Sword &&
                Mathf.Abs(Vector3.Dot(movementDirection, Vector3.right)) > horizontalSwingThreshold)
            {
                if (airSlashEffectPrefab != null)
                {
                    GameObject airSlash = Instantiate(airSlashEffectPrefab, transform.position,
                        Quaternion.LookRotation(movementDirection));
                    Projectile projectile = airSlash.AddComponent<Projectile>();
                    projectile.Initialize(movementDirection, 15f, damageAmount);
                }
            }

            var breakableObject = collision.gameObject.GetComponent<JiggleBreakableObject>();
            if (breakableObject != null)
            {
                breakableObject.TakeDamage(damageAmount, collisionPoint, collisionNormal);
                return;
            }

            var treeOfLight = collision.gameObject.GetComponentInChildren<TreeOfLight>();
            if (treeOfLight != null)
            {
                treeOfLight.GetComponent<HealthComponent>().TakeDamage(treeOfLightDamageMultiplier * damageAmount, collisionPoint, gameObject);
                return;
            }

            var spiderHealth = collision.gameObject.GetComponent<HealthComponent>();
            if (spiderHealth != null && collision.gameObject.CompareTag("Enemy"))
            {
                spiderHealth.TakeDamage(shadowMonsterDamageMultiplier * damageAmount, collisionPoint, gameObject);
                Rigidbody spiderRb = collision.gameObject.GetComponent<Rigidbody>();
                if (spiderRb != null && !spiderRb.isKinematic)
                {
                    Vector3 forceDir = (collision.transform.position - transform.position).normalized;
                    forceDir.y = 0.5f;
                    spiderRb.AddForce(forceDir * 15f, ForceMode.Impulse);
                }
            }
        }

        private void ApplyAreaDamage(Vector3 center, float radius, float damage)
        {
            Collider[] hitColliders = new Collider[20];
            int count = Physics.OverlapSphereNonAlloc(center, radius, hitColliders);
            for (int i = 0; i < count; i++)
            {
                var breakable = hitColliders[i].GetComponent<JiggleBreakableObject>();
                if (breakable != null)
                {
                    float distance = Vector3.Distance(center, hitColliders[i].transform.position);
                    float damageWithFalloff = damage * (1f - (distance / radius));
                    Vector3 direction = (hitColliders[i].transform.position - center).normalized;
                    breakable.TakeDamage(damageWithFalloff, hitColliders[i].transform.position, direction);
                }
            }
        }

        private void OnDestroy()
        {
            if (grabInteractable != null)
            {
                grabInteractable.selectEntered.RemoveListener(OnGrab);
                grabInteractable.selectExited.RemoveListener(OnRelease);
            }
        }
    }

    public class Projectile : MonoBehaviour
    {
        private Vector3 direction;
        private float speed;
        private float damage;
        private float lifespan = 1.5f;

        public void Initialize(Vector3 dir, float spd, float dmg)
        {
            direction = dir;
            speed = spd;
            damage = dmg;
            Destroy(gameObject, lifespan);
        }

        private void Update()
        {
            transform.position += direction * speed * Time.deltaTime;
        }

        private void OnTriggerEnter(Collider other)
        {
            var breakable = other.GetComponent<JiggleBreakableObject>();
            if (breakable != null)
            {
                breakable.TakeDamage(damage, transform.position, direction);
            }
            Destroy(gameObject);
        }
    }
}

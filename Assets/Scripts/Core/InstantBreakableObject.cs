using UnityEngine;

namespace Core
{
    public class InstantBreakableObject : BreakableObject
    {
        [Header("Instant Break Settings")]
        [SerializeField] protected float minimumImpactForce = 5f;
        [SerializeField] protected bool breakOnlyFromWeapons = true;
        [SerializeField] protected bool useImpactForce = true;
        [SerializeField] protected LayerMask collisionLayers = -1; // -1 means all layers
        [SerializeField] protected float damageMultiplier = 1f; // New field to control damage scaling

        // Public methods to modify settings
        public void SetBreakSettings(float impactForce, bool onlyWeapons, bool useForce)
        {
            minimumImpactForce = impactForce;
            breakOnlyFromWeapons = onlyWeapons;
            useImpactForce = useForce;
        }

        private void OnCollisionEnter(Collision collision)
        {
            // Skip if collision layer is not in our mask
            if ((collisionLayers.value & (1 << collision.gameObject.layer)) == 0)
                return;

            bool isWeapon = collision.gameObject.CompareTag("Weapon");
            float impactForce = collision.impulse.magnitude;

            Debug.Log($"Collision detected with {collision.gameObject.name}. Is weapon: {isWeapon}, Impact force: {impactForce}");

            // Check break conditions
            bool shouldBreak = false;

            if (breakOnlyFromWeapons)
            {
                shouldBreak = isWeapon && (!useImpactForce || impactForce >= minimumImpactForce);
            }
            else
            {
                shouldBreak = isWeapon || (useImpactForce && impactForce >= minimumImpactForce);
            }

            if (shouldBreak)
            {
                Debug.Log($"Breaking condition met. Weapon: {isWeapon}, Force: {impactForce}, Required force: {minimumImpactForce}");
                Vector3 hitPoint = collision.contacts[0].point;
                Vector3 hitDirection = collision.contacts[0].normal;
                
                // Calculate damage based on impact force and weapon status
                float damage;
                if (useImpactForce)
                {
                    // Scale damage based on impact force, with a minimum damage
                    damage = Mathf.Max(impactForce * damageMultiplier, minimumImpactForce);
                    Debug.Log($"Calculated damage: {damage} (Impact force: {impactForce}, Multiplier: {damageMultiplier})");
                }
                else
                {
                    // If not using impact force, apply full health as damage
                    damage = health;
                    Debug.Log($"Using full health as damage: {damage}");
                }
                
                // Apply damage using base class method to ensure events are triggered
                base.TakeDamage(damage, hitPoint, hitDirection);
            }
        }

        protected override void HandleDestruction()
        {
            // Disable collider immediately
            if (TryGetComponent<Collider>(out Collider col))
            {
                col.enabled = false;
            }

            // Disable mesh renderer immediately
            if (TryGetComponent<MeshRenderer>(out MeshRenderer renderer))
            {
                renderer.enabled = false;
            }

            base.HandleDestruction();
        }

        protected override void HandleBreaking()
        {
            OnBreakingStart();
            Break();
        }

        protected virtual void OnBreakingStart()
        {
            // Base implementation is empty
            Debug.Log($"OnBreakingStart called on {gameObject.name}");
        }

        protected virtual void OnBreak()
        {
            // Base implementation is empty
            Debug.Log($"OnBreak called on {gameObject.name}");
        }

        protected override void Break()
        {
            OnBreak();
            base.Break();
        }
    }
}
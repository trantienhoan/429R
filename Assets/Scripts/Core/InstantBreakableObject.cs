using UnityEngine;

namespace Core
{
    public class InstantBreakableObject : BreakableObject
    {
        protected override void OnCollisionEnter(Collision collision)
        {
            // Skip if collision layer is not in our mask
            if ((base.collisionLayers.value & (1 << collision.gameObject.layer)) == 0)
                return;

            bool isWeapon = collision.gameObject.CompareTag("Weapon");
            float impactForce = collision.impulse.magnitude;

            //Debug.Log($"Collision detected with {collision.gameObject.name}. Is weapon: {isWeapon}, Impact force: {impactForce}");

            // Check break conditions
            bool shouldBreak = false;

            if (breakOnlyFromWeapons)
            {
                shouldBreak = isWeapon && (!base.useImpactForce || impactForce >= base.minimumImpactForce);
            }
            else
            {
                shouldBreak = isWeapon || (base.useImpactForce && impactForce >= base.minimumImpactForce);
            }

            if (shouldBreak)
            {
                //Debug.Log($"Breaking condition met. Weapon: {isWeapon}, Force: {impactForce}, Required force: {minimumImpactForce}");
                HandleBreaking(); //Break right away
            }
        }
        
        protected override void HandleBreaking()
        {
            Break();
        }
        
        protected override void Break()
        {
            // Disable collider immediately
            if (TryGetComponent(out Collider col))
            {
                col.enabled = false;
            }

            if (TryGetComponent(out MeshRenderer meshRenderer))
            {
                meshRenderer.enabled = false;
            }

            base.Break();
        }
        public override void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitDirection)
        {
            //Do nothing
        }
    }
}
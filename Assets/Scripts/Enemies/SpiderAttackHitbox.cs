using System.Collections;
using UnityEngine;

namespace Enemies
{
    [DisallowMultipleComponent]
    public class SpiderAttackHitbox : MonoBehaviour
    {
        [Header("Base Settings")]
        public float baseRadius = 1.0f;
        public float knockbackForce = 6f;

        [Header("Target Settings")]
        public LayerMask targetLayerMask;

        [Header("Debug")]
        public bool drawGizmos = true;
        public Color gizmoColor = Color.red;

        private GameObject owner;
        private readonly Collider[] hits = new Collider[32]; // Buffer

        public void Activate(float duration)
        {
            gameObject.SetActive(true); // Force active
            PerformOverlap(); // One-shot push on activate
            StartCoroutine(DeactivateAfter(duration));
        }

        private IEnumerator DeactivateAfter(float duration)
        {
            yield return new WaitForSeconds(duration);
            gameObject.SetActive(false);
        }

        private void PerformOverlap()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, baseRadius, hits, targetLayerMask);
            for (int i = 0; i < count; i++)
            {
                Collider hit = hits[i];
                if (hit.gameObject == gameObject || hit.gameObject == owner) continue;

                // Knockback/push
                Rigidbody rb = hit.attachedRigidbody;
                if (rb != null && !rb.isKinematic)
                {
                    Vector3 dir = (hit.transform.position - transform.position).normalized;
                    rb.AddForce(dir * knockbackForce, ForceMode.VelocityChange);
                }
            }
        }

        public void SetOwner(GameObject o)
        {
            owner = o;
        }

        void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;
            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(transform.position, baseRadius);
        }
    }
}
using UnityEngine;
using Core;

public class SpiderAttackHitbox : MonoBehaviour
{
    [SerializeField] private float damage = 10f;
    [SerializeField] private float pushForce = 5f;

    private GameObject owner;
    private bool hasHit = false;

    public void Initialize(GameObject owner, float damage, float pushForce)
    {
        this.owner = owner;
        this.damage = damage;
        this.pushForce = pushForce;
        hasHit = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasHit) return;

        if (other.CompareTag("Player") || other.CompareTag("TreeOfLight") || other.CompareTag("Furniture"))
        {
            var hp = other.GetComponent<HealthComponent>();
            if (hp != null)
            {
                Vector3 hitDir = (other.transform.position - transform.position).normalized;
                hp.TakeDamage(damage, hitDir, owner);
            }

            Rigidbody rb = other.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 forceDir = (other.transform.position - transform.position).normalized;
                rb.AddForce(forceDir * pushForce, ForceMode.Impulse);
            }

            hasHit = true; // Prevent multiple triggers in one attack
        }
    }
}
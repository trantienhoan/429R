using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;

public class ExplosiveBox : MonoBehaviour
{
    [Header("Explosion Settings")]
    public float explosionForce = 500f;
    public float explosionRadius = 5f;
    public float upwardModifier = 1f;
    public GameObject explosionEffect; // Optional explosion particle effect

    [Header("Item Drop Settings")]
    // List of items (prefabs) that can be dropped when the box explodes
    public GameObject[] dropItems;
    // Minimum and maximum number of items to drop
    public int minDrops = 1;
    public int maxDrops = 3;

    private bool hasExploded = false;

    // Detect collision with a two-handed weapon
    void OnCollisionEnter(Collision collision)
    {
        if (!hasExploded && collision.gameObject.CompareTag("Destroyer"))
        {
            Explode();
        }
    }

    void Explode()
    {
        hasExploded = true;

        // Spawn explosion effect if assigned
        if (explosionEffect != null)
        {
            Instantiate(explosionEffect, transform.position, Quaternion.identity);
        }

        // Apply explosion force to all nearby rigidbodies
        Collider[] colliders = Physics.OverlapSphere(transform.position, explosionRadius);
        foreach (Collider nearbyObject in colliders)
        {
            Rigidbody rb = nearbyObject.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.AddExplosionForce(explosionForce, transform.position, explosionRadius, upwardModifier, ForceMode.Impulse);
            }
        }

        // Drop items after the explosion
        DropItems();

        // Remove the box from the scene
        Destroy(gameObject);
    }

    void DropItems()
    {
        if (dropItems.Length == 0)
            return;

        // Determine a random number of items to drop within the specified range
        int dropCount = Random.Range(minDrops, maxDrops + 1);
        for (int i = 0; i < dropCount; i++)
        {
            // Pick a random item from the dropItems array
            int itemIndex = Random.Range(0, dropItems.Length);
            GameObject dropItem = dropItems[itemIndex];

            // Calculate a random spawn position near the box
            Vector3 spawnPos = transform.position + new Vector3(Random.Range(-1f, 1f), 0.5f, Random.Range(-1f, 1f));
            Instantiate(dropItem, spawnPos, Quaternion.identity);
        }
    }
}
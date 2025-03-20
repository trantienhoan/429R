using UnityEngine;

public class BreakableBedInteraction : MonoBehaviour
{
    [SerializeField] private float breakForce = 5f;
    [SerializeField] private float seedSpawnChance = 0.3f; // 30% chance to spawn seed when bed breaks
    
    private bool hasBroken = false;
    private Rigidbody[] bedParts;

    private void Start()
    {
        // Get all rigidbodies in the bed
        bedParts = GetComponentsInChildren<Rigidbody>();
        
        // Set up physics properties for each part
        foreach (Rigidbody part in bedParts)
        {
            part.isKinematic = true;
            part.useGravity = false;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasBroken) return;

        // Check if the collision force is strong enough to break the bed
        if (collision.impulse.magnitude >= breakForce)
        {
            BreakBed();
        }
    }

    private void BreakBed()
    {
        hasBroken = true;
        
        // Enable physics for all bed parts
        foreach (Rigidbody part in bedParts)
        {
            part.isKinematic = false;
            part.useGravity = true;
        }

        // Check if we should spawn a seed
        if (Random.value <= seedSpawnChance)
        {
            MagicalSeedManager.Instance.SpawnSeedFromBed();
        }
    }
} 
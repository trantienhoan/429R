using UnityEngine;

public class SeedSpawnManager : MonoBehaviour
{
    public GameObject magicSeedPrefab; // Assign your seed prefab in Inspector
    public Transform[] hidingSpots; // Assign all possible hiding spots (e.g., inside boxes/drawers)
    private GameObject currentSeed;

    void Start()
    {
        SpawnSeed();
    }

    void SpawnSeed()
    {
        int randomIndex = Random.Range(0, hidingSpots.Length);
        currentSeed = Instantiate(magicSeedPrefab, hidingSpots[randomIndex].position, Quaternion.identity);
        currentSeed.GetComponent<MagicSeed>().SetManager(this); // Link to seed script
    }

    public void RespawnSeed() // For next room or replay
    {
        if (currentSeed != null) Destroy(currentSeed);
        SpawnSeed();
    }
}

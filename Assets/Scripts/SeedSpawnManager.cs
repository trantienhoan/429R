using UnityEngine;

public class SeedSpawnManager : MonoBehaviour
{
    public GameObject magicSeedPrefab; // Assign your seed prefab in Inspector
    public Transform[] hidingSpots; // Assign all possible hiding spots (e.g., inside boxes/drawers)
    private GameObject currentSeed;
    public FairyController fairyController;


    // 🌟 Add references for walls, ceiling, and light
    public Light directionalLight;
    public Material emissionMaterial;
    public Transform wall000, wall001, wall002, wall003, ceiling;

    void Start()
    {
        SpawnSeed();
    }

    void SpawnSeed()
    {
        int randomIndex = Random.Range(0, hidingSpots.Length);
        currentSeed = Instantiate(magicSeedPrefab, hidingSpots[randomIndex].position, Quaternion.identity);

        MagicSeed seedScript = currentSeed.GetComponent<MagicSeed>();
        seedScript.SetManager(this); // Link SeedSpawnManager

        // 🌟 Assign scene references to MagicSeed
        seedScript.directionalLight = directionalLight;
        seedScript.emissionMaterial = emissionMaterial;
        seedScript.wall000 = wall000;
        seedScript.wall001 = wall001;
        seedScript.wall002 = wall002;
        seedScript.wall003 = wall003;
        seedScript.ceiling = ceiling;
    }


    public void RespawnSeed() // For next room or replay
    {
        if (currentSeed != null) Destroy(currentSeed);
        SpawnSeed();
    }
}

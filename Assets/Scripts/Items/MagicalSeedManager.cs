using UnityEngine;

public class MagicalSeedManager : MonoBehaviour
{
    public static MagicalSeedManager Instance { get; private set; }
    
    [Header("Seed Settings")]
    [SerializeField] private GameObject magicalSeedPrefab;
    [SerializeField] private Transform[] seedSpawnLocations; // Array of possible spawn locations
    [SerializeField] private float spawnChance = 0.5f; // Chance for seed to spawn in each location
    
    private GameObject currentSeed;
    private bool hasSeedBeenFound = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SpawnInitialSeed();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void SpawnInitialSeed()
    {
        // Try to spawn seed in each location based on spawn chance
        foreach (Transform location in seedSpawnLocations)
        {
            if (Random.value <= spawnChance)
            {
                SpawnSeedAtLocation(location);
                break; // Only spawn one seed
            }
        }
    }

    public void SpawnSeedAtTransform(Transform location)
    {
        SpawnSeedAtLocation(location);
    }

    private void SpawnSeedAtLocation(Transform location)
    {
        // Destroy existing seed if any
        if (currentSeed != null)
        {
            Destroy(currentSeed);
        }

        // Spawn new seed
        currentSeed = Instantiate(magicalSeedPrefab, location.position, location.rotation);
        
        // Add GrabbableObject component if it doesn't exist
        GrabbableObject grabbable = currentSeed.GetComponent<GrabbableObject>();
        if (grabbable == null)
        {
            grabbable = currentSeed.AddComponent<GrabbableObject>();
        }
        
        // Configure the grabbable object
        grabbable.isGrabbable = true;
        grabbable.useGravity = true;
        grabbable.mass = 1f;
        grabbable.drag = 0.5f;
        grabbable.angularDrag = 0.5f;
    }

    public void OnSeedFound()
    {
        hasSeedBeenFound = true;
        if (currentSeed != null)
        {
            Destroy(currentSeed);
            currentSeed = null;
        }
    }

    public bool HasSeedBeenFound()
    {
        return hasSeedBeenFound;
    }

    public bool IsSeedInScene()
    {
        return currentSeed != null;
    }
} 
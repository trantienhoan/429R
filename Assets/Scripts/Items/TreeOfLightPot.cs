using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Items
{
    public class TreeOfLightPot : MonoBehaviour
    {
        [Header("Tree Growth Settings")]
        [SerializeField] private GameObject treePrefab;
        [SerializeField] private Transform treeSpawnPoint;
        [SerializeField] private float growthDelay = 1.0f;
        [SerializeField] private float growthDuration = 30.0f;
        [SerializeField] private Vector3 maxTreeScale = new Vector3(1, 1, 1);
    
        [Header("Pot Orientation")]
        [SerializeField] private float tiltThreshold = 30f;
        [SerializeField] private float checkInterval = 0.5f;
    
        [Header("Monster Spawning")]
        [SerializeField] private ShadowMonsterSpawner monsterSpawner;
        [SerializeField] private Enemies.ShadowMonsterSpawner spawner;
        [SerializeField] private GameObject monsterPrefab;

        private GameObject SpawnMonster()
        {
            // Find a valid spawn position
            Vector3 spawnPosition = FindValidSpawnPosition();

            // Instantiate the monster
            GameObject monster = Instantiate(monsterPrefab, spawnPosition, Quaternion.identity);

            RegisterMonsterLocal(monster);

            return monster;
        }
        private Vector3 FindValidSpawnPositionLocal()
        {
            // Implement the logic needed to find a valid spawn position
            // For example:
            Vector3 potPosition = transform.position;
            float spawnRadius = 3f;
            Vector3 randomOffset = new Vector3(
                Random.Range(-spawnRadius, spawnRadius),
                0f,
                Random.Range(-spawnRadius, spawnRadius)
            );
            
            return potPosition + randomOffset;
        }
        private void RegisterMonsterLocal(GameObject monster)
        {
            // Any logic needed to keep track of spawned monsters
            // For example, you could add the monster to a list if needed:
            spawnedMonsters.Add(monster);
        }
        private void NotifySpawner(GameObject monster)
        {
            // If the error is in a call like: spawner.SomeMethod(this)
            // Change it to use the spawner reference and pass the monster instead:
            if (spawner != null)
            {
                spawner.NotifyMonsterSpawned(monster); // Or whatever the method should be
            }
        }


        [Header("Door Key")]
        [SerializeField] private GameObject doorKeyPrefab;
        [SerializeField] private Transform keySpawnPoint;
    
        [Header("Effects")]
        [SerializeField] private ParticleSystem breakEffect;
        [SerializeField] private AudioClip breakSound;
        [SerializeField] private AudioSource audioSource;
    
        [Header("Interaction")]
        [SerializeField] private XRSocketInteractor socketInteractor;
    
        // Private variables
        private TreeOfLight treeOfLight;
        private bool isGrowing = false;
        private bool isGrowthCompleted = false;
        private bool hasSeedBeenPlanted = false;
        private bool isPotUpright = true;
        private bool wasUpright = true;
        private Rigidbody rb;
        private Coroutine growthMonitorRoutine;
        private Coroutine orientationCheckRoutine;
    
        private void Awake()
        {
            // Set up references
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            rb = GetComponent<Rigidbody>();
            
            // Validate required components
            if (treePrefab == null)
            {
                Debug.LogError("Tree prefab not assigned to TreeOfLightPot!");
            }
        
            if (treeSpawnPoint == null)
            {
                Debug.LogError("Tree spawn point not assigned to TreeOfLightPot!");
                // Create a default spawn point if none exists
                GameObject spawnPoint = new GameObject("TreeSpawnPoint");
                spawnPoint.transform.SetParent(transform);
                spawnPoint.transform.localPosition = Vector3.zero;
                treeSpawnPoint = spawnPoint.transform;
                Debug.Log("Created default tree spawn point");
            }
        
            // Create key spawn point if it doesn't exist or if it's using the tree spawn point
            if (keySpawnPoint == null || keySpawnPoint == treeSpawnPoint)
            {
                GameObject keyPoint = new GameObject("KeySpawnPoint");
                keyPoint.transform.SetParent(transform);
                keyPoint.transform.localPosition = new Vector3(0, 0.5f, 0); // Spawn slightly above the pot
                keySpawnPoint = keyPoint.transform;
                Debug.Log("Created key spawn point");
            }
        
            // Validate monster spawner
            if (monsterSpawner == null)
            {
                Debug.LogWarning("Monster spawner not assigned to TreeOfLightPot!");
                monsterSpawner = FindObjectOfType<ShadowMonsterSpawner>();
                if (monsterSpawner != null)
                {
                    Debug.Log("Found ShadowMonsterSpawner in scene");
                }
            }
            
            // Setup Socket Interactor
            if (socketInteractor == null)
            {
                Debug.LogError("Socket interactor not assigned to TreeOfLightPot!");
                socketInteractor = GetComponent<XRSocketInteractor>();
            }
        }
        
        private void Start()
        {
            // Setup socket interactor
            if (socketInteractor != null)
            {
                socketInteractor.selectEntered.AddListener(OnSeedPlaced);
            }
            
            // Start the orientation checking routine
            orientationCheckRoutine = StartCoroutine(CheckOrientation());
        }
        
        private void OnSeedPlaced(SelectEnterEventArgs args)
        {
            if (hasSeedBeenPlanted) return;
            
            // Check if the placed object is a MagicalSeed
            MagicalSeed seed = args.interactableObject.transform.GetComponent<MagicalSeed>();
            if (seed == null)
            {
                Debug.Log("Object placed in socket is not a MagicalSeed");
                return;
            }
            
            Debug.Log("MagicalSeed placed in pot!");
            hasSeedBeenPlanted = true;
            
            // Tell the seed it's been planted
            seed.OnPlantedInPot();
            
            // Start the growth process after a delay
            StartCoroutine(StartTreeGrowth());
        }
        
        private IEnumerator StartTreeGrowth()
        {
            // Wait for the initial growth delay
            yield return new WaitForSeconds(growthDelay);
            
            // Instantiate the tree at the spawn point
            GameObject treeObject = Instantiate(treePrefab, treeSpawnPoint.position, treeSpawnPoint.rotation);
            treeObject.transform.SetParent(treeSpawnPoint);
            
            // Get the TreeOfLight component
            treeOfLight = treeObject.GetComponent<TreeOfLight>();
            if (treeOfLight == null)
            {
                Debug.LogError("Tree prefab does not have TreeOfLight component!");
                yield break;
            }
            
            // Setup tree with parent pot reference
            treeOfLight.SetParentPot(this);
            
            // Start the growth process
            treeOfLight.StartGrowth(growthDuration, maxTreeScale.x);
            isGrowing = true;
            
            // Start the growth monitor routine
            growthMonitorRoutine = StartCoroutine(MonitorTreeGrowth());
            
            // Begin spawning monsters when growth starts
            if (monsterSpawner != null)
            {
                monsterSpawner.StartSpawning();
                Debug.Log("Monster spawning started as growth begins");
            }
        }
        
        private IEnumerator MonitorTreeGrowth()
        {
            float elapsedTime = 0f;
            
            while (elapsedTime < growthDuration && !isGrowthCompleted)
            {
                // Only increase elapsed time if pot is upright
                if (isPotUpright)
                {
                    elapsedTime += Time.deltaTime;
                    
                    // Update tree growth progress
                    if (treeOfLight != null)
                    {
                        float progress = elapsedTime / growthDuration;
                        treeOfLight.UpdateGrowthProgress(progress);
                    }
                }
                
                yield return null;
            }
            
            // Complete growth when time is up
            CompleteTreeGrowth();
        }
        
        private void CompleteTreeGrowth()
        {
            isGrowthCompleted = true;
            isGrowing = false;
            
            // Notify tree growth is complete - will trigger light wave effect
            if (treeOfLight != null)
            {
                treeOfLight.CompleteGrowth();
            }
            
            // The tree light wave will kill all monsters
            // Note: We don't stop spawning here as the monsters should be killed by the light wave
            
            // Start the break sequence after a delay
            StartCoroutine(BreakSequence());
        }
        
        private IEnumerator BreakSequence()
        {
            // Wait for break delay
            yield return new WaitForSeconds(3f);
            
            // Play break effects
            if (breakEffect != null)
            {
                breakEffect.Play();
            }
            
            if (audioSource != null && breakSound != null)
            {
                audioSource.PlayOneShot(breakSound);
            }
            
            // Destroy tree
            if (treeOfLight != null)
            {
                Destroy(treeOfLight.gameObject);
            }
            
            // Stop monster spawning after tree breaks
            if (monsterSpawner != null)
            {
                monsterSpawner.StopSpawning();
            }
            
            // Spawn door key
            if (doorKeyPrefab != null && keySpawnPoint != null)
            {
                Instantiate(doorKeyPrefab, keySpawnPoint.position, keySpawnPoint.rotation);
                Debug.Log("Door key spawned!");
            }
            
            // Apply explosion force to pot parts
            foreach (Rigidbody childRb in GetComponentsInChildren<Rigidbody>())
            {
                if (childRb != rb && childRb != null)
                {
                    Vector3 explosionForce = Random.insideUnitSphere.normalized * 5f;
                    childRb.AddForce(explosionForce, ForceMode.Impulse);
                }
            }
        }
        
        private IEnumerator CheckOrientation()
        {
            while (true)
            {
                // Check if the pot's up direction is within threshold of world up
                Vector3 potUpDirection = transform.up;
                float angle = Vector3.Angle(potUpDirection, Vector3.up);
                
                // Determine if pot is upright based on the angle threshold
                wasUpright = isPotUpright;
                isPotUpright = angle < tiltThreshold;
                
                // Handle orientation changes
                if (wasUpright != isPotUpright)
                {
                    if (isPotUpright)
                    {
                        Debug.Log("Pot is now upright. Resuming growth.");
                        if (treeOfLight != null && isGrowing)
                        {
                            treeOfLight.ResumeGrowth();
                        }
                    }
                    else
                    {
                        Debug.Log("Pot is tilted. Pausing growth.");
                        if (treeOfLight != null && isGrowing)
                        {
                            treeOfLight.PauseGrowth();
                        }
                    }
                }
                
                yield return new WaitForSeconds(checkInterval);
            }
        }
        
        public bool IsPotUpright()
        {
            return isPotUpright;
        }
    }
}
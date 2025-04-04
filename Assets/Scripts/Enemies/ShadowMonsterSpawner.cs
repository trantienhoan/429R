using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Items;
using UnityEngine.Events;


namespace Enemies
{
    [System.Serializable]
    public class MonsterTrackerData
    {
        public List<GameObject> activeMonsters = new List<GameObject>();
    }

    public class ShadowMonsterSpawner : MonoBehaviour
    {
        [Header("Tree Dependency")]
        [SerializeField] private TreeOfLight treeOfLight;
        [SerializeField] private bool onlySpawnAfterTreeGrowth = true;
		[SerializeField] private TreeOfLightPot treeOfLightPot;
        
        [Header("Spawning Settings")]
        [SerializeField] private int maxMonstersAlive = 5;
        [SerializeField] private float spawnInterval = 5f;
        [SerializeField] private float minSpawnDistance = 10f;
        [SerializeField] private float maxSpawnDistance = 25f;
        [SerializeField] private Transform player;
        [SerializeField] private bool spawnAtNightOnly = true;
        [SerializeField] private bool spawnAwayFromLight = true;
        [SerializeField] private float minimumLightLevelToSpawn = 0.3f;
        [SerializeField] private float minSpawnDelay = 5f;
        [SerializeField] private float maxSpawnDelay = 15f;
        [SerializeField] private Transform[] spawnPoints;

        [Header("Monster Settings")]
        [SerializeField] private GameObject monsterPrefab;
        [SerializeField] private GameObject spiderPrefab; // For future implementation
        [SerializeField] private float spiderSpawnChance = 0.2f; // 20% chance to spawn spider instead
        
        [Header("Wave Settings")]
        [SerializeField] private bool useWaves = false;
        [SerializeField] private int baseWaveSize = 3;
        [SerializeField] private int additionalMonstersPerWave = 1;
        [SerializeField] private float timeBetweenWaves = 30f;

        [Header("Debug")]
        [SerializeField] private bool showSpawnPoints = true;
        [SerializeField] private bool debugLogs = false;

        // Unified monster tracking
        private List<ShadowMonster> activeMonsters = new List<ShadowMonster>();
        [SerializeField] private MonsterTrackerData monsterData = new MonsterTrackerData();
        
        // Spawning control
        private bool isSpawning = false;
        private Coroutine spawnCoroutine;
        private float nextSpawnTime = 0f;
        private int currentWave = 0;
        private bool canSpawnBasedOnTree = false;

        private void Start()
        {
            // Find player if not assigned
            if (player == null)
            {
                player = GameObject.FindGameObjectWithTag("Player")?.transform;
                if (player == null)
                {
                    LogError("No player found! Make sure your player has the 'Player' tag.");
                    enabled = false;
                    return;
                }
            }
            
            // Replace the TreeOfLight lookup with TreeOfLightPot lookup
    	if (treeOfLight == null && onlySpawnAfterTreeGrowth)
    	{
        	// Find the pot instead of the tree
        	if (treeOfLightPot == null)
        	{
        	    treeOfLightPot = FindAnyObjectByType<TreeOfLightPot>();
        	}
        
        	if (treeOfLightPot != null)
        	{
        	    // Subscribe to the seed placed event
        	    treeOfLightPot.onSeedPlaced.AddListener(OnSeedPlaced);
        	    Log("Waiting for seed to be placed in pot");
        	}
        	else
        	{
        	    LogWarning("TreeOfLightPot not found, monster spawning might not work correctly");
        	}
    	}

    	else if (!onlySpawnAfterTreeGrowth)
    	{
        	// Keep existing code
        	canSpawnBasedOnTree = true;
    	}
    	else if (treeOfLight != null)
    	{
        	// If tree is already assigned, subscribe to its events
        	SubscribeToTreeEvents();
    	}

            // Clear any pre-existing monsters from previous sessions
            CleanupMonsterLists();
            
            // Start spawning system based on configuration
            InitializeSpawningSystem();
        }
        
        private void OnDestroy()
        {
            // Unsubscribe from TreeOfLight events to prevent memory leaks
            if (treeOfLight != null)
            {
                UnsubscribeFromTreeEvents();
            }
        }
		
		private void OnSeedPlaced()
		{
    		// Start looking for the TreeOfLight now that the seed has been placed
    		StartCoroutine(WaitForTreeOfLight());
		}

		private IEnumerator WaitForTreeOfLight()
		{
    		// Give time for the tree to be created
    		yield return new WaitForSeconds(0.5f);
    
   	 		// Now look for the tree
    		treeOfLight = FindAnyObjectByType<TreeOfLight>();
    		if (treeOfLight != null)
    		{
        		SubscribeToTreeEvents();
        		Log("Found TreeOfLight after seed placement");
    		}
    		else
    		{
        		LogWarning("TreeOfLight still not found after seed placement");
        		// Optional: retry mechanism or fallback behavior
    		}
		}
        
        private void SubscribeToTreeEvents()
        {
            // Subscribe to the appropriate event from TreeOfLight
            // This is a placeholder - you need to implement the actual event in TreeOfLight
            if (treeOfLight != null)
            {
                treeOfLight.onGrowthStarted.AddListener(HandleTreeGrowthStarted);
            }
        }
        
        private void UnsubscribeFromTreeEvents()
        {
            if (treeOfLight != null)
            {
                treeOfLight.onGrowthStarted.RemoveListener(HandleTreeGrowthStarted);
            }
        }
	
		public UnityEvent OnGrowthStarted = new UnityEvent();
    
    	// Call this when tree growth starts
    	private void StartGrowth()
    	{
        	// Other growth code
    	    OnGrowthStarted.Invoke();
	    }

        
        private void HandleTreeGrowthStarted()
        {
            // Now we can start spawning monsters
            canSpawnBasedOnTree = true;
            Log("Tree growth started - Monster spawning now enabled");
            
            // If we're using waves, start the wave coroutine now
            if (useWaves && spawnCoroutine == null)
            {
                spawnCoroutine = StartCoroutine(SpawnWaves());
            }
        }

        private void CleanupMonsterLists()
        {
            activeMonsters.Clear();
            monsterData.activeMonsters.Clear();
        }

        private void InitializeSpawningSystem()
        {
            // Only start wave spawning if tree growth is not a factor or the tree is already growing
            if (useWaves && canSpawnBasedOnTree)
            {
                spawnCoroutine = StartCoroutine(SpawnWaves());
                Log("Wave spawning initialized");
            }
            else
            {
                Log("Regular spawning initialized (Update method)");
                // Regular spawning is handled in Update
            }
        }

        private void Update()
        {
            // Skip if using waves or not allowed to spawn based on tree
            if (useWaves || !canSpawnBasedOnTree) return;

            if (Time.time >= nextSpawnTime && CanSpawnMore())
            {
                if (!spawnAtNightOnly || IsNightTime())
                {
                    SpawnMonster();
                    nextSpawnTime = Time.time + spawnInterval;
                }
            }
        }

        private bool CanSpawnMore()
        {
            return activeMonsters.Count < maxMonstersAlive;
        }

        // Unified monster spawning method
        private GameObject SpawnMonster()
        {
            // Check if we can spawn based on the tree status
            if (onlySpawnAfterTreeGrowth && !canSpawnBasedOnTree)
            {
                Log("Attempted to spawn monster but tree growth hasn't started yet");
                return null;
            }
            
            // Check if we're at capacity
            if (!CanSpawnMore())
            {
                Log("At max monster capacity, can't spawn more");
                return null;
            }

            // Find a valid spawn position
            Vector3 spawnPosition = FindValidSpawnPosition();
            
            // Decide which monster type to spawn (basic monster or spider)
            GameObject prefabToSpawn = monsterPrefab;
            if (spiderPrefab != null && Random.value < spiderSpawnChance)
            {
                prefabToSpawn = spiderPrefab;
            }

            // Instantiate the monster
            GameObject monster = Instantiate(prefabToSpawn, spawnPosition, Quaternion.identity);
            
            // Register the monster properly
            RegisterMonster(monster);
            
            Log($"Monster spawned at {spawnPosition}");
            return monster;
        }
        
        // Unified monster registration
        public void RegisterMonster(GameObject monster)
        {
            if (monster == null) return;
            
            // Add to GameObject tracking list
            if (!monsterData.activeMonsters.Contains(monster))
            {
                monsterData.activeMonsters.Add(monster);
            }
            
            // Add to ShadowMonster tracking list
            ShadowMonster monsterComponent = monster.GetComponent<ShadowMonster>();
            if (monsterComponent != null && !activeMonsters.Contains(monsterComponent))
            {
                activeMonsters.Add(monsterComponent);
                
                // Add observer component if needed
                if (!monster.GetComponent<ShadowMonsterObserver>())
                {
                    ShadowMonsterObserver observer = monster.AddComponent<ShadowMonsterObserver>();
                    observer.Setup(this, monsterComponent);
                }
            }
        }
        
        // Unified monster unregistration
        public void UnregisterMonster(ShadowMonster monster)
        {
            if (monster == null) return;
            
            // Remove from ShadowMonster list
            if (activeMonsters.Contains(monster))
            {
                activeMonsters.Remove(monster);
            }
            
            // Remove from GameObject list
            GameObject monsterObject = monster.gameObject;
            if (monsterData.activeMonsters.Contains(monsterObject))
            {
                monsterData.activeMonsters.Remove(monsterObject);
            }
            
            Log($"Monster unregistered. Active count: {activeMonsters.Count}");
        }
        
        // Also support GameObject-based unregistration
        public void UnregisterMonster(GameObject monster)
        {
            if (monster == null) return;
            
            // Remove from GameObject list
            if (monsterData.activeMonsters.Contains(monster))
            {
                monsterData.activeMonsters.Remove(monster);
            }
            
            // Find and remove from ShadowMonster list
            ShadowMonster monsterComponent = monster.GetComponent<ShadowMonster>();
            if (monsterComponent != null && activeMonsters.Contains(monsterComponent))
            {
                activeMonsters.Remove(monsterComponent);
            }
        }
        
        // For TreeOfLightPot to call
        public void NotifyMonsterSpawned(GameObject monster)
        {
            RegisterMonster(monster);
        }

        // Wave-based spawning
        private IEnumerator SpawnWaves()
        {
            while (true)
            {
                yield return new WaitForSeconds(timeBetweenWaves);

                // Only spawn if tree conditions are met and it's nighttime if required
                if (canSpawnBasedOnTree && (!spawnAtNightOnly || IsNightTime()))
                {
                    currentWave++;
                    int monstersToSpawn = baseWaveSize + (additionalMonstersPerWave * (currentWave - 1));
                    
                    // Cap the number of monsters to our maximum
                    int actualSpawnCount = Mathf.Min(monstersToSpawn, maxMonstersAlive - activeMonsters.Count);
                    
                    Log($"Starting Wave {currentWave} with {actualSpawnCount} monsters");
                    
                    for (int i = 0; i < actualSpawnCount; i++)
                    {
                        SpawnMonster();
                        yield return new WaitForSeconds(1f); // Slight delay between spawns in a wave
                    }
                }
            }
        }

        // Manual spawning control methods
        public void StartSpawning()
        {
            if (!isSpawning && canSpawnBasedOnTree)
            {
                isSpawning = true;
                spawnCoroutine = StartCoroutine(SpawnMonsters());
                Log("Manual monster spawning started");
            }
            else if (!canSpawnBasedOnTree)
            {
                LogWarning("Cannot start spawning - Tree of Light hasn't started growing yet");
            }
        }

        public void StopSpawning()
        {
            if (isSpawning)
            {
                isSpawning = false;
                if (spawnCoroutine != null)
                {
                    StopCoroutine(spawnCoroutine);
                    spawnCoroutine = null;
                }

                Log("Monster spawning stopped");
            }
        }

        private IEnumerator SpawnMonsters()
        {
            while (isSpawning)
            {
                // Wait for random time between min and max delay
                float delay = Random.Range(minSpawnDelay, maxSpawnDelay);
                yield return new WaitForSeconds(delay);

                if (isSpawning && spawnPoints.Length > 0 && monsterPrefab != null && CanSpawnMore())
                {
                    // Choose random spawn point
                    Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];

                    // Spawn monster
                    GameObject monster = Instantiate(monsterPrefab, spawnPoint.position, spawnPoint.rotation);
                    RegisterMonster(monster);
                }
            }
        }

        private Vector3 FindValidSpawnPosition()
        {
            // Use spawn points if available and not empty
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
                return spawnPoint.position;
            }
            
            // Otherwise use dynamic position around player
            // Try to find a valid spawn position
            for (int i = 0; i < 10; i++) // Try 10 times
            {
                // Find random position around player
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float distance = Random.Range(minSpawnDistance, maxSpawnDistance);

                Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * distance;
                Vector3 spawnPos = player.position + offset;

                // Cast ray down to find ground
                RaycastHit hit;
                if (Physics.Raycast(spawnPos + Vector3.up * 50, Vector3.down, out hit, 100f,
                        LayerMask.GetMask("Ground", "Terrain")))
                {
                    spawnPos = hit.point;

                    // Check if not too close to light if that setting is enabled
                    if (spawnAwayFromLight)
                    {
                        float lightLevel = GetLightLevelAtPosition(spawnPos);
                        if (lightLevel > minimumLightLevelToSpawn)
                        {
                            continue; // Skip this position, try again
                        }
                    }

                    // Found a valid position
                    return spawnPos;
                }
            }

            // If we can't find a valid position, just spawn at random distance around player
            float fallbackAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float fallbackDistance = Random.Range(minSpawnDistance, maxSpawnDistance);
            Vector3 fallbackOffset =
                new Vector3(Mathf.Cos(fallbackAngle), 0, Mathf.Sin(fallbackAngle)) * fallbackDistance;

            return player.position + fallbackOffset;
        }

        private float GetLightLevelAtPosition(Vector3 position)
        {
            // Find all lights in the scene
            Light[] sceneLights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            float totalLightLevel = 0f;

            foreach (Light light in sceneLights)
            {
                if (!light.enabled) continue;

                float distance = Vector3.Distance(position, light.transform.position);

                // Skip if out of range
                if (distance > light.range)
                    continue;

                // Calculate light contribution based on distance and intensity
                float lightFalloff = 1f - Mathf.Clamp01(distance / light.range);
                totalLightLevel += light.intensity * lightFalloff;
            }

            return totalLightLevel;
        }

        private bool IsNightTime()
        {
            // This is a placeholder. In your actual game, you should
            // check your day/night cycle system to determine if it's night
            // TODO: Implement actual day/night check
            return true;
        }

        // Draw the spawn area in the editor for debugging
        private void OnDrawGizmosSelected()
        {
            if (!showSpawnPoints || player == null) return;

            // Min spawn distance
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(player.position, minSpawnDistance);

            // Max spawn distance
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(player.position, maxSpawnDistance);
            
            // Draw spawn points
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                Gizmos.color = Color.green;
                foreach (Transform spawnPoint in spawnPoints)
                {
                    if (spawnPoint != null)
                    {
                        Gizmos.DrawSphere(spawnPoint.position, 1f);
                        Gizmos.DrawLine(spawnPoint.position, spawnPoint.position + spawnPoint.forward * 2f);
                    }
                }
            }
        }

        // Public method to trigger a wave manually
        public void TriggerWave(int extraMonsters = 0)
        {
            if (canSpawnBasedOnTree)
            {
                currentWave++;
                int monstersToSpawn = baseWaveSize + (additionalMonstersPerWave * (currentWave - 1)) + extraMonsters;
                int actualSpawnCount = Mathf.Min(monstersToSpawn, maxMonstersAlive - activeMonsters.Count);
                
                if (actualSpawnCount > 0)
                {
                    StartCoroutine(SpawnWaveCoroutine(actualSpawnCount));
                    Log($"Manual wave triggered with {actualSpawnCount} monsters");
                }
                else
                {
                    Log("Cannot spawn more monsters - at capacity");
                }
            }
            else
            {
                LogWarning("Cannot trigger wave - Tree of Light hasn't started growing yet");
            }
        }

        private IEnumerator SpawnWaveCoroutine(int count)
        {
            for (int i = 0; i < count; i++)
            {
                SpawnMonster();
                yield return new WaitForSeconds(1f);
            }
        }

        // Public accessor for active monsters count
        public int GetActiveMonsterCount()
        {
            return activeMonsters.Count;
        }
        
        // Conditional logging methods
        private void Log(string message)
        {
            if (debugLogs)
            {
                Debug.Log($"[ShadowMonsterSpawner] {message}");
            }
        }
        
        private void LogWarning(string message)
        {
            Debug.LogWarning($"[ShadowMonsterSpawner] {message}");
        }
        
        private void LogError(string message)
        {
            Debug.LogError($"[ShadowMonsterSpawner] {message}");
        }
    }
}
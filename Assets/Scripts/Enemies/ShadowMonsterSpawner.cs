using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Enemies
{
    // Create a simple class to track monsters if you need one
    [System.Serializable]
    public class MonsterTrackerData
    {
        public List<GameObject> activeMonsters = new List<GameObject>();
    }

    public class ShadowMonsterSpawner : MonoBehaviour
    {
        [Header("Spawning Settings")] [SerializeField]
        [SerializeField] public GameObject monsterPrefab;
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
        [SerializeField] private MonsterTrackerData monsterData = new MonsterTrackerData();
        public Vector3 FindValidSpawnPosition()
        {
            // Implementation for finding a spawn position
            Vector3 spawnerPos = transform.position;
            float spawnRadius = 5f;
            Vector3 randomOffset = new Vector3(
                Random.Range(-spawnRadius, spawnRadius),
                0f,
                Random.Range(-spawnRadius, spawnRadius)
            );
            
            return spawnerPos + randomOffset;
        }
        
        // Method to register a monster
        public void RegisterMonster(GameObject monster)
        {
            if (monster != null && monsterData != null)
            {
                monsterData.activeMonsters.Add(monster);
            }
        }
        
        // Method for TreeOfLightPot to call instead of passing itself
        public void NotifyMonsterSpawned(GameObject monster)
        {
            // Do whatever you need when a monster is spawned
            RegisterMonster(monster);
        }

        private bool isSpawning = false;
        private Coroutine spawnCoroutine;


        [Header("Wave Settings")] [SerializeField]
        private bool useWaves = false;

        [SerializeField] private int baseWaveSize = 3;
        [SerializeField] private int additionalMonstersPerWave = 1;
        [SerializeField] private float timeBetweenWaves = 30f;

        [Header("Debug")] [SerializeField] private bool showSpawnPoints = true;

        // Internal variables
        private List<ShadowMonster> activeMonsters = new List<ShadowMonster>();
        private float nextSpawnTime = 0f;
        private int currentWave = 0;

        private void Start()
        {
            if (player == null)
            {
                player = GameObject.FindGameObjectWithTag("Player")?.transform;
                if (player == null)
                {
                    Debug.LogError("No player found! Make sure your player has the 'Player' tag.");
                    enabled = false;
                    return;
                }
            }

            if (useWaves)
            {
                StartCoroutine(SpawnWaves());
            }
        }

        private void Update()
        {
            if (useWaves) return; // Don't do regular spawning if using waves

            if (Time.time >= nextSpawnTime && activeMonsters.Count < maxMonstersAlive)
            {
                if (!spawnAtNightOnly || IsNightTime())
                {
                    SpawnMonster();
                    nextSpawnTime = Time.time + spawnInterval;
                }
            }
        }

        private GameObject SpawnMonster()
        {
            // Find a valid spawn position
            Vector3 spawnPosition = FindValidSpawnPosition();

            // Instantiate the monster
            GameObject monster = Instantiate(monsterPrefab, spawnPosition, Quaternion.identity);

            // Register the monster if it has the ShadowMonster component
            ShadowMonster monsterComponent = monster.GetComponent<ShadowMonster>();
            if (monsterComponent != null)
            {
                RegisterMonster(monsterComponent);

                // Since there's no OnMonsterDeath event, we need an alternative approach
                // Option 1: Check if ShadowMonster has another way to detect death
                // For example, if it has a public method SetDeathCallback:
                // monsterComponent.SetDeathCallback(() => UnregisterMonster(monsterComponent));

                // Option 2: If it doesn't, we can add a MonsterTracker component
                MonsterTracker tracker = monsterData;
                tracker.Initialize(this, monsterComponent);
            }
            else
            {
                Debug.LogWarning("Spawned monster prefab doesn't have a ShadowMonster component!");
            }

            Debug.Log("Monster spawned at " + spawnPosition);
            return monster;
        }



        private IEnumerator SpawnWaves()
        {
            while (true)
            {
                yield return new WaitForSeconds(timeBetweenWaves);

                if (!spawnAtNightOnly || IsNightTime())
                {
                    currentWave++;
                    int monstersToSpawn = baseWaveSize + (additionalMonstersPerWave * (currentWave - 1));

                    for (int i = 0; i < monstersToSpawn; i++)
                    {
                        SpawnMonster();
                        yield return new WaitForSeconds(1f); // Slight delay between spawns in a wave
                    }
                }
            }
        }

        public void StartSpawning()
        {
            if (!isSpawning)
            {
                isSpawning = true;
                spawnCoroutine = StartCoroutine(SpawnMonsters());
                Debug.Log("Monster spawning started");
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

                Debug.Log("Monster spawning stopped");
            }
        }

        private IEnumerator SpawnMonsters()
        {
            while (isSpawning)
            {
                // Wait for random time between min and max delay
                float delay = Random.Range(minSpawnDelay, maxSpawnDelay);
                yield return new WaitForSeconds(delay);

                if (isSpawning && spawnPoints.Length > 0 && monsterPrefab != null)
                {
                    // Choose random spawn point
                    Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];

                    // Spawn monster
                    GameObject monster = Instantiate(monsterPrefab, spawnPoint.position, spawnPoint.rotation);
                    Debug.Log("Shadow monster spawned at " + spawnPoint.name);
                }
            }
        }


        // This method is called when a new monster is spawned
        private void RegisterMonster(ShadowMonster monster)
        {
            if (!activeMonsters.Contains(monster))
            {
                activeMonsters.Add(monster);
            }
        }

        // This method is called when a monster dies
        public void UnregisterMonster(ShadowMonster monster)
        {
            if (activeMonsters.Contains(monster))
            {
                activeMonsters.Remove(monster);
            }
        }


        private Vector3 FindValidSpawnPosition()
        {
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
            Light[] sceneLights = FindObjectsOfType<Light>();
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
            // For testing, you could just always return true

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
        }

        // Public method to trigger a wave manually
        public void TriggerWave(int extraMonsters = 0)
        {
            if (!useWaves)
            {
                currentWave++;
                int monstersToSpawn = baseWaveSize + (additionalMonstersPerWave * (currentWave - 1)) + extraMonsters;

                StartCoroutine(SpawnWaveCoroutine(monstersToSpawn));
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
    }
}
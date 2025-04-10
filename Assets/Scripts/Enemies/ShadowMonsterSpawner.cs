using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Enemies;
using Core;

public class MonsterTrackerData
{
    public GameObject MonsterObject;
    public ShadowMonster SpiderReference;
}

public class ShadowMonsterSpawner : MonoBehaviour
{
    [Header("Monster Prefab")]
    [SerializeField] private GameObject monsterPrefab;

    [Header("Spawn Settings")]
    [SerializeField] private float spawnRadius = 10f;
    [SerializeField] private float spawnInterval = 5f;
    [SerializeField] private int maxMonsters = 10;

    [Header("Wave Settings")]
    [SerializeField] private int monstersPerWave = 3;
    [SerializeField] private float timeBetweenWaves = 10f;

    [Header("Spawn Points")]
    [SerializeField] private List<Transform> spawnPoints = new List<Transform>();

    [Header("References")]
    [SerializeField] private TreeOfLight tree; // Reference to the TreeOfLight
    private List<MonsterTrackerData> _activeMonsters = new List<MonsterTrackerData>();

    private bool isSpawning = false;

    private void Start()
    {
        if (tree == null)
        {
            LogError("Tree not assigned on ShadowMonsterSpawner!");
            enabled = false;
            return;
        }
        InitializeSpawningSystem();
    }
    public void BeginSpawning()
    {
        // Add the code here that starts the monster spawning process
        // For example, if you have a coroutine that handles spawning:
        StartCoroutine(SpawnMonsters());
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
        CleanupMonsterLists();
    }

    private void InitializeSpawningSystem()
    {
        StartCoroutine(DelayedStartSpawning());
    }

    private IEnumerator DelayedStartSpawning()
    {
        yield return null; // Wait one frame
        StartSpawning();
    }

    private void Update()
    {
        // You can add logic here to adjust spawning based on game state
    }

    private bool CanSpawnMore()
    {
        return _activeMonsters.Count < maxMonsters;
    }

    private GameObject SpawnMonster()
    {
        Vector3 spawnPosition = FindValidSpawnPosition();
        GameObject monsterInstance = Instantiate(monsterPrefab, spawnPosition, Quaternion.identity);

        // Rotate the monster to the desired initial orientation.
        monsterInstance.transform.rotation = Quaternion.Euler(0, -90, 0); // Adjust these values as needed

        RegisterMonster(monsterInstance);
        return monsterInstance;
    }

    public void RegisterMonster(GameObject monster)
    {
        ShadowMonsterSpider spider = monster.GetComponent<ShadowMonsterSpider>();
        if (spider != null)
        {
            // Spider-specific registration: Increase health
            HealthComponent health = monster.GetComponent<HealthComponent>();
            if (health != null)
            {
                health.SetMaxHealth(health.MaxHealth * 1.5f); // 50% more health
            }
            Debug.Log("Registered a ShadowMonsterSpider with increased health!");
            return;
        }

        ShadowMonster shadowMonster = monster.GetComponent<ShadowMonster>();
        if (shadowMonster != null)
        {
            // Generic ShadowMonster registration
            Debug.Log("Registered a ShadowMonster");
            return;
        }

        Debug.LogError("Monster is not a recognized ShadowMonster type!");
    }

    private void OnMonsterDeath(HealthComponent health)
    {
        UnregisterMonster(health);
    }
    public void UnregisterMonster(HealthComponent health)
    {
        if (health == null)
        {
            Debug.LogWarning("Trying to unregister a null health component!");
            return;
        }

        health.OnDeath -= OnMonsterDeath;

        //Check if monster is on the list
        var itemToRemove = _activeMonsters.FirstOrDefault(x => x.MonsterObject == health.gameObject);

        if (itemToRemove != null)
        {
            _activeMonsters.Remove(itemToRemove);
            Log($"Monster unregistered. Active monster count: {_activeMonsters.Count}");
        }
        else
        {
            LogWarning($"Trying to unregister a monster that wasnt registered");
        }
    }


    private IEnumerator SpawnWaves()
    {
        while (isSpawning && tree != null)
        {
            TriggerWave(monstersPerWave);
            yield return new WaitForSeconds(timeBetweenWaves);
        }
    }
    private IEnumerator SpawnMonsters()
    {
        StartSpawning();
        yield return null;
    }

    private void StartSpawning()
    {
        if (!isSpawning)
        {
            isSpawning = true;
            StartCoroutine(SpawnWaves());
            Log("Monster spawning started.");
        }
    }

    public void StopSpawning()
    {
        if (isSpawning)
        {
            isSpawning = false;
            StopAllCoroutines();
            Log("Monster spawning stopped.");
        }
    }

    private Vector3 FindValidSpawnPosition()
    {
        if (spawnPoints == null || spawnPoints.Count == 0)
        {
            LogWarning("No spawn points defined. Spawning at spawner's position.");
            return transform.position; // Fallback to spawner's position
        }

        int randomIndex = Random.Range(0, spawnPoints.Count);
        return spawnPoints[randomIndex].position;
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        if (spawnPoints != null)
        {
            foreach (var point in spawnPoints)
            {
                if (point != null)
                {
                    Gizmos.DrawWireSphere(point.position, spawnRadius);
                }
            }
        }
        else
        {
            Gizmos.DrawWireSphere(transform.position, spawnRadius);
        }
    }

    private void TriggerWave(int extraMonsters = 0)
    {
        StartCoroutine(SpawnWaveCoroutine(monstersPerWave + extraMonsters));
    }

    private IEnumerator SpawnWaveCoroutine(int count)
    {
        //Spawn a wave of monsters
        for (int i = 0; i < count; i++)
        {
            if (CanSpawnMore())
            {
                SpawnMonster();
                yield return new WaitForSeconds(spawnInterval);
            }
            else
            {
                LogWarning("Reached max monster limit. Cannot spawn more.");
                break;
            }
        }
    }

    public void CleanupMonsterLists()
    {
        //Iterate and kill all active monsters
        foreach (var monster in _activeMonsters.ToList()) // Iterate over a copy to avoid modification issues
        {
            if (monster.MonsterObject != null)
            {
                var health = monster.MonsterObject.GetComponent<HealthComponent>();
                if (health != null)
                {
                    health.TakeDamage(Mathf.Infinity); // Or Despawn() or whatever appropriate method
                }
                else
                {
                    Destroy(monster.MonsterObject); // Fallback if no health component
                }
            }
        }

        //Clear the active monster list
        _activeMonsters.Clear();
    }

    private void Log(string message)
    {
        Debug.Log($"[MonsterSpawner] {message}");
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning($"[MonsterSpawner] {message}");
    }

    private void LogError(string message)
    {
        Debug.LogError($"[MonsterSpawner] {message}");
    }
}
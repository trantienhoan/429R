using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Enemies;

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
    //public GameObject player;
    [SerializeField] private TreeOfLight tree; // Reference to the TreeOfLight
    private List<MonsterTrackerData> _activeMonsters = new List<MonsterTrackerData>();

    private bool isSpawning = false;

    private void Start()
    {
        if (tree == null)
        {
            Debug.LogError("Tree not assigned on ShadowMonsterSpawner!");
            enabled = false;
            return;
        }
        InitializeSpawningSystem();
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

    private void RegisterMonster(GameObject monsterInstance)
    {
        if (monsterInstance == null)
        {
            Debug.LogError("Trying to register a null monster instance!");
            return;
        }

        ShadowMonster monster = monsterInstance.GetComponent<ShadowMonster>();

        if (monster == null)
        {
            Debug.LogError("Monster instance does not have a ShadowMonster component!");
            return;
        }

        ShadowMonsterSpider spider = monster as ShadowMonsterSpider;

        MonsterTrackerData newMonster = new MonsterTrackerData
        {
            MonsterObject = monsterInstance,
            SpiderReference = spider
        };

        _activeMonsters.Add(newMonster);

        //Register to onDeath
        var health = monsterInstance.GetComponent<Core.HealthComponent>();
        health.OnDeath += () => UnregisterMonster(monsterInstance);
    }

    public void UnregisterMonster(GameObject monsterInstance)
    {
        if (monsterInstance == null)
        {
            Debug.LogWarning("Trying to unregister a null monster instance!");
            return;
        }

        //Check if monster is on the list
        var itemToRemove = _activeMonsters.FirstOrDefault(x => x.MonsterObject == monsterInstance);

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
            Debug.LogWarning("No spawn points defined. Spawning at spawner's position.");
            return transform.position; // Fallback to spawner's position
        }

        int randomIndex = Random.Range(0, spawnPoints.Count);
        return spawnPoints[randomIndex].position;
    }

    private void OnDrawGizmosSelected()
    {
        //Draw a sphere to show the spawn radius
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
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
                var health = monster.MonsterObject.GetComponent<Core.HealthComponent>();
                if (health != null)
                {
                    health.Kill(); // Or Despawn() or whatever appropriate method
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
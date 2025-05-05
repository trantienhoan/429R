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

    [Header("Scale Settings")]
    [SerializeField] private float initialScaleMultiplier = 0.01f;
    [SerializeField] private float scaleDuration = 2.0f;

    [Header("Spawn Settings")]
    [SerializeField] private float spawnRadius = 10f;
    [SerializeField] private float spawnInterval = 5f;
    [SerializeField] private int maxMonsters = 10;

    [Header("References")]
    [SerializeField] private TreeOfLight tree;
    [SerializeField] private List<GameObject> windows = new List<GameObject>(); // Initialize list

    [Header("Window Rotation Settings")]
    [SerializeField] private float openWindowRotationL = -147f;
    [SerializeField] private float openWindowRotationR = 147f;
    [SerializeField] private float closedWindowRotation = 0f;

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

        if (windows == null || windows.Count != 2)
        {
            LogWarning("Two windows must be assigned to the Monster Spawner!");
        }
    }

    public void BeginSpawning()
    {
        OpenWindows();
        StartSpawning(); // Start the actual spawning coroutine
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
        CleanupMonsterLists();
        CloseWindows();
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

        // Set initial scale
        monsterInstance.transform.localScale = monsterPrefab.transform.localScale * initialScaleMultiplier;

        monsterInstance.transform.rotation = Quaternion.Euler(0, -90, 0);

        RegisterMonster(monsterInstance);

        // Start scaling coroutine
        StartCoroutine(ScaleMonster(monsterInstance.transform, monsterPrefab.transform.localScale, scaleDuration));

        return monsterInstance;
    }

    private IEnumerator ScaleMonster(Transform monsterTransform, Vector3 targetScale, float duration)
    {
        Vector3 startScale = monsterTransform.localScale;
        float timer = 0;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float progress = timer / duration;
            monsterTransform.localScale = Vector3.Lerp(startScale, targetScale, progress);
            yield return null;
        }

        // Ensure it reaches the exact target scale
        monsterTransform.localScale = targetScale;
    }

    public void RegisterMonster(GameObject monster)
    {
        ShadowMonsterSpider spider = monster.GetComponent<ShadowMonsterSpider>();
        if (spider != null)
        {
            HealthComponent health = monster.GetComponent<HealthComponent>();
            if (health != null)
            {
                health.SetMaxHealth(health.MaxHealth * 1.5f);
            }
            Debug.Log("Registered a ShadowMonsterSpider with increased health!");
            return;
        }

        ShadowMonster shadowMonster = monster.GetComponent<ShadowMonster>();
        if (shadowMonster != null)
        {
            return;
        }
    }

    private void OnMonsterDeath(HealthComponent health)
    {
        UnregisterMonster(health);
    }

    public void UnregisterMonster(HealthComponent health)
    {
        if (health == null)
        {
            return;
        }

        health.OnDeath -= OnMonsterDeath;

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

    private IEnumerator SpawnMonsters()
    {
        while (isSpawning)
        {
            if (CanSpawnMore())
            {
                SpawnMonster();
                yield return new WaitForSeconds(spawnInterval);
            }
            else
            {
                LogWarning("Reached max monster limit. Cannot spawn more.");
                yield return null;
            }
        }
    }

    private void StartSpawning()
    {
        if (!isSpawning)
        {
            isSpawning = true;
            StartCoroutine(SpawnMonsters());
            Log("Monster spawning started.");
        }
    }

    public void StopSpawning()
    {
        if (isSpawning)
        {
            isSpawning = false;
            StopAllCoroutines();
            CloseWindows(); // Close windows when spawning stops
            Log("Monster spawning stopped.");
        }
    }

    private Vector3 FindValidSpawnPosition()
    {
        List<Transform> childSpawnPoints = new List<Transform>();
        foreach (Transform child in transform)
        {
            childSpawnPoints.Add(child);
        }

        if (childSpawnPoints == null || childSpawnPoints.Count == 0)
        {
            LogWarning("No spawn points defined. Spawning at spawner's position.");
            return transform.position;
        }

        int randomIndex = Random.Range(0, childSpawnPoints.Count);
        return childSpawnPoints[randomIndex].position;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
    }

    public void CleanupMonsterLists()
    {
        foreach (var monster in _activeMonsters.ToList())
        {
            if (monster.MonsterObject != null)
            {
                Destroy(monster.MonsterObject);
            }
        }

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

    private void OpenWindows()
    {
        if (windows == null || windows.Count != 2)
        {
            LogWarning("Two windows must be assigned to the Monster Spawner!");
            return;
        }

        GameObject windowL = windows[0];
        GameObject windowR = windows[1];

        if (windowL != null)
        {
            windowL.SetActive(true);
            windowL.transform.rotation = Quaternion.Euler(0, openWindowRotationL, 0); // Set absolute rotation
        }
        else
        {
            LogWarning("Window L is null!");
        }

        if (windowR != null)
        {
            windowR.SetActive(true);
            windowR.transform.rotation = Quaternion.Euler(0, openWindowRotationR, 0); // Set absolute rotation
        }
        else
        {
            LogWarning("Window R is null!");
        }
    }

    private void CloseWindows()
    {
        if (windows == null || windows.Count != 2)
        {
            return;
        }

        GameObject windowL = windows[0];
        GameObject windowR = windows[1];

        if (windowL != null)
        {
            windowL.SetActive(false); // Deactivate the window
            windowL.transform.rotation = Quaternion.Euler(0, closedWindowRotation, 0); // Set absolute rotation
        }
        else
        {
            LogWarning("Window L is null!");
        }

        if (windowR != null)
        {
            windowR.SetActive(false); // Deactivate the window
            windowR.transform.rotation = Quaternion.Euler(0, closedWindowRotation, 0); // Set absolute rotation
        }
        else
        {
            LogWarning("Window R is null!");
        }
    }
}


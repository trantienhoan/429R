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
    [SerializeField] private float dropDuration = 1.0f;

    [Header("Spawn Settings")]
    [SerializeField] private float spawnRadius = 10f;
    [SerializeField] private float spawnInterval = 5f;
    [SerializeField] private int maxMonsters = 10;
    [SerializeField] private float spawnHeight = 5f;

    [Header("Spawn Points")]
    [SerializeField] private List<Transform> cornerSpawnPoints = new List<Transform>();

    [Header("References")]
    [SerializeField] private TreeOfLight tree;
    [SerializeField] private List<GameObject> windows = new List<GameObject>();

    [Header("Window Rotation Settings")]
    [SerializeField] private float openWindowRotationL = -147f;
    [SerializeField] private float openWindowRotationR = 147f;
    [SerializeField] private float closedWindowRotation = 0f;

    private List<MonsterTrackerData> activeMonsters = new List<MonsterTrackerData>();
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

        if (cornerSpawnPoints == null || cornerSpawnPoints.Count == 0)
        {
            LogWarning("No corner spawn points defined. Please add some!");
        }
    }

    public void BeginSpawning()
    {
        OpenWindows();
        Invoke(nameof(StartSpawning), 7f);
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
        CleanupMonsterLists();
        CloseWindows();
    }

    private bool CanSpawnMore() => activeMonsters.Count < maxMonsters;

    private GameObject SpawnMonster()
    {
        Vector3 spawnPosition = FindValidSpawnPosition();
        GameObject monster = SpawnMonsterAt(spawnPosition, spawnPosition);
        return monster;
    }

    public GameObject SpawnMonsterAt(Vector3 startPosition, Vector3 endPosition)
    {
        GameObject monsterInstance = SpiderPool.Instance.GetSpider(startPosition, Quaternion.Euler(0, -90, 0));
        monsterInstance.transform.localScale = monsterPrefab.transform.localScale * initialScaleMultiplier;

        RegisterMonster(monsterInstance);
        StartCoroutine(ScaleAndDropMonster(monsterInstance.transform, monsterPrefab.transform.localScale, endPosition, scaleDuration, dropDuration));
        return monsterInstance;
    }

    private IEnumerator ScaleAndDropMonster(Transform monsterTransform, Vector3 targetScale, Vector3 targetPosition, float scaleDuration, float dropDuration)
    {
        Vector3 startScale = monsterTransform.localScale;
        Vector3 startPosition = monsterTransform.position;
        float timer = 0;

        while (timer < scaleDuration)
        {
            if (monsterTransform == null) yield break;
            timer += Time.deltaTime;
            monsterTransform.localScale = Vector3.Lerp(startScale, targetScale, timer / scaleDuration);
            yield return null;
        }

        if (monsterTransform == null) yield break;
        monsterTransform.localScale = targetScale;
        timer = 0;

        while (timer < dropDuration)
        {
            if (monsterTransform == null) yield break;
            timer += Time.deltaTime;
            monsterTransform.position = Vector3.Lerp(startPosition, targetPosition, timer / dropDuration);
            yield return null;
        }

        if (monsterTransform != null)
            monsterTransform.position = targetPosition;
    }

    public void RegisterMonster(GameObject monster)
    {
        ShadowMonster shadowMonster = monster.GetComponent<ShadowMonster>();
        if (shadowMonster != null)
        {
            HealthComponent health = monster.GetComponent<HealthComponent>();
            if (health != null)
            {
                health.SetMaxHealth(health.MaxHealth * 1.5f);
                health.OnDeath += OnMonsterDeath;
            }

            activeMonsters.Add(new MonsterTrackerData
            {
                MonsterObject = monster,
                SpiderReference = shadowMonster
            });

            Log("Registered a ShadowMonster!");
        }
        else
        {
            LogWarning("Tried to register a monster without ShadowMonster component.");
        }
    }

    private void OnMonsterDeath(HealthComponent health) => UnregisterMonster(health);

    public void UnregisterMonster(HealthComponent health)
    {
        if (health == null) return;
        health.OnDeath -= OnMonsterDeath;

        var itemToRemove = activeMonsters.FirstOrDefault(x => x.MonsterObject == health.gameObject);

        if (itemToRemove != null)
        {
            activeMonsters.Remove(itemToRemove);
            Log($"Monster unregistered. Active monster count: {activeMonsters.Count}");
        }
        else
        {
            LogWarning("Trying to unregister a monster that wasn't registered");
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
            CloseWindows();
            Log("Monster spawning stopped.");
        }
    }

    private Vector3 FindValidSpawnPosition()
    {
        if (cornerSpawnPoints == null || cornerSpawnPoints.Count == 0)
        {
            LogWarning("No corner spawn points defined. Spawning at spawner's position.");
            return transform.position;
        }

        int randomIndex = Random.Range(0, cornerSpawnPoints.Count);
        Vector3 chosenPosition = cornerSpawnPoints[randomIndex].position;
        return new Vector3(chosenPosition.x, chosenPosition.y + spawnHeight, chosenPosition.z);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
    }

    public void CleanupMonsterLists()
    {
        foreach (var monster in activeMonsters.ToList())
        {
            if (monster.MonsterObject != null)
            {
                SpiderPool.Instance.ReturnSpider(monster.MonsterObject);
            }
        }

        activeMonsters.Clear();
    }

    private void Log(string message) => Debug.Log($"[MonsterSpawner] {message}");
    private void LogWarning(string message) => Debug.LogWarning($"[MonsterSpawner] {message}");
    private void LogError(string message) => Debug.LogError($"[MonsterSpawner] {message}");

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
            windowL.transform.rotation = Quaternion.Euler(0, openWindowRotationL, 0);
        }
        else LogWarning("Window L is null!");

        if (windowR != null)
        {
            windowR.SetActive(true);
            windowR.transform.rotation = Quaternion.Euler(0, openWindowRotationR, 0);
        }
        else LogWarning("Window R is null!");
    }

    private void CloseWindows()
    {
        if (windows == null || windows.Count != 2) return;

        GameObject windowL = windows[0];
        GameObject windowR = windows[1];

        if (windowL != null)
        {
            windowL.SetActive(false);
            windowL.transform.rotation = Quaternion.Euler(0, closedWindowRotation, 0);
        }
        else LogWarning("Window L is null!");

        if (windowR != null)
        {
            windowR.SetActive(false);
            windowR.transform.rotation = Quaternion.Euler(0, closedWindowRotation, 0);
        }
        else LogWarning("Window R is null!");
    }
}

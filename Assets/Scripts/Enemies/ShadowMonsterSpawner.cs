using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Core;
using System.Linq;
using UnityEngine.AI;

namespace Enemies
{
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
        [SerializeField] private float spawnHeight = 5f;

        [Header("Spawn Points")]
        [SerializeField] private List<Transform> cornerSpawnPoints = new();

        [Header("References")]
        [SerializeField] private GameObject treeObject;

        [Header("Lamps and Lights")]
        [SerializeField] private List<Lamp> lamps = new();
        [SerializeField] private List<Light> roomLights = new();
        [SerializeField] private float lampBreakDelay = 2f;

        [Header("Monster Boost Settings")]  // Moved from GameManager
        [SerializeField] private string monsterTag = "Enemy";  // For refresh
        [SerializeField] private float healthIncreasePerLamp = 0.1f;  // 10% per lamp
        [SerializeField] private float sizeIncreasePerLamp = 0.1f;    // 10% per lamp
        [SerializeField] private float maxBoostMultiplier = 2f;       // Cap at 200%
        [SerializeField] private float scaleTransitionDuration = 1f;  // Smooth scale time

        private readonly List<MonsterTrackerData> activeMonsters = new();
        private bool isSpawning;
        private Coroutine spawnCoroutine;
        private int brokenLampsCount = 0;

        private void Awake()
        {
            if (monsterPrefab == null || treeObject == null)
            {
                LogError($"Missing {(monsterPrefab == null ? "monsterPrefab" : "treeObject")}");
                enabled = false;
                return;
            }

            if (cornerSpawnPoints == null || cornerSpawnPoints.Count == 0)
            {
                LogWarning("No corner spawn points defined. Using spawner position.");
                cornerSpawnPoints = new List<Transform> { transform };
            }

            // Auto-assign lamps if not set in Inspector
            if (lamps.Count == 0)
            {
                lamps = FindObjectsOfType<Lamp>().ToList();
                Log($"Auto-assigned {lamps.Count} lamps from scene.");
            }

            // Auto-assign room lights if not set (optional, assuming lights are on lamps or separate)
            if (roomLights.Count == 0)
            {
                roomLights = FindObjectsOfType<Light>().ToList();
                Log($"Auto-assigned {roomLights.Count} room lights from scene.");
            }
        }

        private void Start()
        {
            Lamp.OnLampBroken += HandleLampBroken;  // Subscribe to lamp breaks for boosts
        }

        private void OnDestroy()
        {
            Lamp.OnLampBroken -= HandleLampBroken;
            StopSpawning();
        }

        public void BeginSpawning()
        {
            if (isSpawning)
            {
                LogWarning("BeginSpawning called while already spawning!");
                return;
            }
            StartCoroutine(BreakLampsSequentially());
            StartSpawning();
        }

        public void StopSpawning()
        {
            if (!isSpawning)
            {
                return;
            }

            isSpawning = false;

            if (spawnCoroutine != null)
            {
                StopCoroutine(spawnCoroutine);
                spawnCoroutine = null;
            }

            foreach (var monsterData in activeMonsters.ToList())
            {
                if (monsterData.MonsterObject != null)
                {
                    var health = monsterData.MonsterObject.GetComponent<HealthComponent>();
                    if (health != null && !health.IsDead())
                    {
                        health.Kill(gameObject); // Only kill; let death state handle animation/disable
                    }
                    if (SpiderPool.Instance != null)
                    {
                        SpiderPool.Instance.ReturnSpider(monsterData.MonsterObject);
                    }
                    else
                    {
                        DestroyImmediate(monsterData.MonsterObject);
                    }
                }
            }
            activeMonsters.Clear();
            Log("Stopped spawning and cleared monsters");
        }

        private void StartSpawning()
        {
            if (isSpawning)
            {
                return;
            }
            isSpawning = true;
            spawnCoroutine = StartCoroutine(SpawnMonsters());
        }

        private IEnumerator SpawnMonsters()
        {
            while (isSpawning)
            {
                if (activeMonsters.Count < maxMonsters)
                {
                    Vector3 spawnPosition = FindValidSpawnPosition();
                    SpawnMonsterAt(spawnPosition, spawnPosition);
                }
                yield return new WaitForSeconds(spawnInterval);
            }
        }

        private void SpawnMonsterAt(Vector3 startPosition, Vector3 endPosition)
        {
            if (SpiderPool.Instance == null)
            {
                LogError("SpiderPool.Instance is null!");
                return;
            }

            GameObject monsterInstance = SpiderPool.Instance.GetSpider(startPosition, Quaternion.Euler(0, -90, 0));
            if (monsterInstance == null)
            {
                LogError("Failed to get monster from SpiderPool!");
                return;
            }

            monsterInstance.transform.localScale = monsterPrefab.transform.localScale * initialScaleMultiplier;
            monsterInstance.tag = "Enemy";

            Rigidbody rb = monsterInstance.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }

            RegisterMonster(monsterInstance);
            StartCoroutine(ScaleMonster(monsterInstance.transform, monsterPrefab.transform.localScale));
            StartCoroutine(WaitUntilGroundedThenEnableAI(monsterInstance));
        }

        private IEnumerator ScaleMonster(Transform monsterTransform, Vector3 targetScale)
        {
            if (monsterTransform == null) yield break;
            Vector3 startScale = monsterTransform.localScale;
            float timer = 0;

            while (timer < scaleDuration)
            {
                if (monsterTransform == null || !monsterTransform.gameObject.activeInHierarchy) yield break;
                timer += Time.deltaTime;
                monsterTransform.localScale = Vector3.Lerp(startScale, targetScale, timer / scaleDuration);
                yield return null;
            }

            if (monsterTransform != null)
            {
                monsterTransform.localScale = targetScale;
            }
        }

        private void RegisterMonster(GameObject monster)
        {
            ShadowMonster shadowMonster = monster.GetComponent<ShadowMonster>();
            if (shadowMonster != null)
            {
                shadowMonster.SetMaxHealth(150f);
                activeMonsters.Add(new MonsterTrackerData
                {
                    MonsterObject = monster,
                    SpiderReference = shadowMonster
                });
            }
            else
            {
                if (SpiderPool.Instance != null)
                {
                    SpiderPool.Instance.ReturnSpider(monster);
                }
                else
                {
                    DestroyImmediate(monster);
                }
            }
        }

        private IEnumerator WaitUntilGroundedThenEnableAI(GameObject spider)
        {
            if (spider == null) yield break;
            ShadowMonster sm = spider.GetComponent<ShadowMonster>();
            if (sm == null) yield break;

            float timeout = 5f;
            float elapsed = 0f;
            while (!sm.IsGrounded() && elapsed < timeout)
            {
                if (spider == null || !spider.activeInHierarchy) yield break;
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (spider != null && sm.IsGrounded())
            {
                sm.EnableAI();
            }
        }

        private Vector3 FindValidSpawnPosition()
        {
            Transform spawnPoint = cornerSpawnPoints[Random.Range(0, cornerSpawnPoints.Count)] ?? transform;
            Vector3 basePosition = spawnPoint.position;
            Vector3 randomOffset = Random.insideUnitSphere * spawnRadius;
            randomOffset.y = 0;
            Vector3 tryPosition = basePosition + randomOffset;

            if (NavMesh.SamplePosition(tryPosition, out NavMeshHit hit, spawnRadius, NavMesh.AllAreas))
            {
                return hit.position + Vector3.up * spawnHeight;
            }

            return basePosition + Vector3.up * spawnHeight;
        }

        private IEnumerator BreakLampsSequentially()
        {
            // Break lamps one by one with delay, destroying them
            foreach (Lamp lamp in lamps.ToList())
            {
                if (lamp != null)
                {
                    lamp.Break();  // Break the lamp
                    yield return new WaitForSeconds(lampBreakDelay);
                    Destroy(lamp.gameObject);  // Destroy after delay
                }
            }
            lamps.Clear();

            // Turn off all room lights to darken the room
            foreach (Light roomLight in roomLights)
            {
                if (roomLight != null)
                {
                    roomLight.enabled = false;
                }
            }
        }

        // Monster boost logic (moved from GameManager)
        private void HandleLampBroken(Lamp lamp)
        {
            brokenLampsCount++;
            UpdateAllMonsters();
        }

        private void UpdateAllMonsters()
        {
            float healthMultiplier = Mathf.Min(1 + (brokenLampsCount * healthIncreasePerLamp), maxBoostMultiplier);
            float sizeMultiplier = Mathf.Min(1 + (brokenLampsCount * sizeIncreasePerLamp), maxBoostMultiplier);

            foreach (MonsterTrackerData data in activeMonsters)
            {
                ShadowMonster sm = data.SpiderReference;
                if (sm == null) continue;

                // Health: Scale max and current proportionally
                float oldMax = sm.healthComponent.maxHealth;
                float newMax = oldMax * healthMultiplier;
                float healthRatio = sm.healthComponent.health / oldMax;
                sm.SetMaxHealth(newMax);
                sm.healthComponent.health = newMax * healthRatio;
                sm.healthComponent.OnHealthChanged?.Invoke(sm.healthComponent.health / newMax);

                // Size: Smooth lerp to new scale
                Vector3 targetScale = sm.transform.localScale * sizeMultiplier;  // Multiply current (handles time-growth)
                StartCoroutine(SmoothScale(sm.transform, targetScale));

                Debug.Log($"Updated Monster {sm.name}: Health = {newMax}, Scale = {targetScale}");
            }
        }

        private IEnumerator SmoothScale(Transform target, Vector3 endScale)
        {
            Vector3 startScale = target.localScale;
            float timer = 0f;
            while (timer < scaleTransitionDuration)
            {
                timer += Time.deltaTime;
                target.localScale = Vector3.Lerp(startScale, endScale, timer / scaleTransitionDuration);
                yield return null;
            }
            target.localScale = endScale;
        }

        private void Log(string message) => Debug.Log($"[ShadowMonsterSpawner {gameObject.name}] {message}");
        private void LogWarning(string message) => Debug.LogWarning($"[ShadowMonsterSpawner {gameObject.name}] {message}");
        private void LogError(string message) => Debug.LogError($"[ShadowMonsterSpawner {gameObject.name}] {message}");
    }
}
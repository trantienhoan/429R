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

        [Header("Despawn Settings")]
        [SerializeField] private float despawnDelayBetweenMonsters = 0.5f;  // Delay between each monster despawn
        [SerializeField] private float deathSequenceDuration = 3f;  // Time for each monster's death animation/scale-down before pooling

        [Header("Kamikaze Overpopulation Control")]
        [SerializeField] private int maxMonstersBeforeKamikaze = 23;  // Updated: Threshold to trigger random kamikaze (e.g., >=23)
        [SerializeField] private int kamikazeBatchSize = 1;  // How many to kamikaze per check (to reduce gradually)

        private readonly List<MonsterTrackerData> activeMonsters = new();
        private bool isSpawning;
        private Coroutine spawnCoroutine;

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
        }

        private void Start()
        {
            // Removed subscription to Lamp.OnLampBroken
        }

        private void OnDestroy()
        {
            // Removed unsubscription to Lamp.OnLampBroken
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

            StartCoroutine(DespawnAllSequentially());

            Log("Stopped spawning and initiated sequential monster despawn");
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
                    CheckForOverpopulation();  // Check after spawn
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

        // New: Check count and force kamikaze on random monsters if over threshold
        private void CheckForOverpopulation()
        {
            int excess = activeMonsters.Count - (maxMonstersBeforeKamikaze - 1);  // e.g., if 24, excess=1
            if (excess <= 0) return;

            // Shuffle list for random selection
            var shuffledMonsters = activeMonsters.OrderBy(x => Random.value).ToList();

            // Trigger kamikaze on up to batch size (or excess, whichever smaller)
            int toKamikaze = Mathf.Min(excess, kamikazeBatchSize);
            for (int i = 0; i < toKamikaze; i++)
            {
                var monster = shuffledMonsters[i].SpiderReference;
                if (monster != null && !monster.isInKamikazeMode && !monster.healthComponent.IsDead())
                {
                    monster.EnterKamikazeMode();
                    Debug.Log($"[Overpopulation] Forced {monster.name} into Kamikaze mode. Current count: {activeMonsters.Count}");
                }
            }
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

        private IEnumerator DespawnAllSequentially()
        {
            foreach (var monsterData in activeMonsters.ToList())
            {
                if (monsterData.MonsterObject != null && monsterData.SpiderReference != null)
                {
                    var health = monsterData.MonsterObject.GetComponent<HealthComponent>();
                    if (health != null && !health.IsDead())
                    {
                        monsterData.SpiderReference.Despawn();  // Triggers death sequence
                        yield return new WaitForSeconds(deathSequenceDuration + despawnDelayBetweenMonsters);
                    }

                    // Check if still exists before pooling
                    if (monsterData.MonsterObject != null)
                    {
                        if (SpiderPool.Instance != null)
                        {
                            SpiderPool.Instance.ReturnSpider(monsterData.MonsterObject);
                        }
                        else
                        {
                            Destroy(monsterData.MonsterObject);
                        }
                    }
                }
            }
            activeMonsters.Clear();
            Destroy(gameObject);  // Self-destruct after
        }

        private void Log(string message) => Debug.Log($"[ShadowMonsterSpawner {gameObject.name}] {message}");
        private void LogWarning(string message) => Debug.LogWarning($"[ShadowMonsterSpawner {gameObject.name}] {message}");
        private void LogError(string message) => Debug.LogError($"[ShadowMonsterSpawner {gameObject.name}] {message}");
    }
}
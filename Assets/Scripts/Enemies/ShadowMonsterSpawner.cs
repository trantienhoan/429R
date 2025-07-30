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
        [SerializeField] private List<GameObject> windows = new();

        [Header("Window Rotation Settings")]
        [SerializeField] private float openWindowRotationL = -147f;
        [SerializeField] private float openWindowRotationR = 147f;
        [SerializeField] private float closedWindowRotation;

        private readonly List<MonsterTrackerData> activeMonsters = new();
        private bool isSpawning;
        private Coroutine spawnCoroutine;
        private bool isInvokePending;

        private void Awake()
        {
            if (monsterPrefab == null || treeObject == null)
            {
                LogError($"Missing {(monsterPrefab == null ? "monsterPrefab" : "treeObject")}");
                enabled = false;
                return;
            }

            if (windows.Count != 2)
            {
                LogWarning("Two windows must be assigned!");
            }

            if (cornerSpawnPoints == null || cornerSpawnPoints.Count == 0)
            {
                LogWarning("No corner spawn points defined. Using spawner position.");
                cornerSpawnPoints = new List<Transform> { transform };
            }
        }

        public void BeginSpawning()
        {
            if (isSpawning)
            {
                LogWarning("BeginSpawning called while already spawning!");
                return;
            }
            isInvokePending = true;
            OpenWindows();
            Invoke(nameof(StartSpawning), 7f);
        }

        public void StopSpawning()
        {
            if (!isSpawning && !isInvokePending)
            {
                return;
            }

            isSpawning = false;
            isInvokePending = false;
            CancelInvoke(nameof(StartSpawning));

            if (spawnCoroutine != null)
            {
                StopCoroutine(spawnCoroutine);
                spawnCoroutine = null;
            }

            CloseWindows();

            foreach (var monsterData in activeMonsters.ToList())
            {
                if (monsterData.MonsterObject != null)
                {
                    var shadowMonster = monsterData.SpiderReference;
                    var health = monsterData.MonsterObject.GetComponent<HealthComponent>();
                    if (shadowMonster != null && health != null && !health.IsDead())
                    {
                        shadowMonster.DisableAI();
                        health.Kill(gameObject);
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
            isInvokePending = false;
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

        private void OpenWindows()
        {
            if (windows.Count != 2) return;
            for (int i = 0; i < windows.Count; i++)
            {
                if (windows[i] != null)
                {
                    windows[i].SetActive(true);
                    float rotation = i == 0 ? openWindowRotationL : openWindowRotationR;
                    windows[i].transform.rotation = Quaternion.Euler(0, rotation, 0);
                }
            }
        }

        private void CloseWindows()
        {
            if (windows.Count != 2) return;
            for (int i = 0; i < windows.Count; i++)
            {
                if (windows[i] != null)
                {
                    windows[i].SetActive(false);
                    windows[i].transform.rotation = Quaternion.Euler(0, closedWindowRotation, 0);
                }
            }
        }

        private void OnDestroy()
        {
            StopSpawning();
        }

        private void Log(string message) => Debug.Log($"[ShadowMonsterSpawner {gameObject.name}] {message}");
        private void LogWarning(string message) => Debug.LogWarning($"[ShadowMonsterSpawner {gameObject.name}] {message}");
        private void LogError(string message) => Debug.LogError($"[ShadowMonsterSpawner {gameObject.name}] {message}");
    }
}
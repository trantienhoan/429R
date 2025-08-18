using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.AI;
using Core;

namespace Enemies
{
    public class MonsterTrackerData
    {
        public GameObject MonsterObject;
        public ShadowMonster SpiderReference;
    }

    public class ShadowMonsterSpawner : MonoBehaviour
    {
        [Header("Monster Prefab / Pool")]
        [SerializeField] private GameObject monsterPrefab;

        [Header("Spawn Points (Scene Objects)")]
        [Tooltip("Assign any objects in the scene you want to spawn from. If the object is destroyed (or its HealthComponent dies), it will be skipped.")]
        [SerializeField] private List<Transform> spawnPointObjects = new();

        [Header("Spawn Settings")]
        [SerializeField] private float spawnInterval = 5f;
        [SerializeField] private int maxMonsters = 10;
        [Tooltip("How far from the spawn object we’re allowed to search for a spot on the NavMesh.")]
        [SerializeField] private float navmeshSampleRadius = 6f;
        [Tooltip("Lift spawn a bit to avoid ground interpenetration.")]
        [SerializeField] private float spawnLift = 0.5f;

        [Header("Scale Settings")]
        [SerializeField] private float initialScaleMultiplier = 0.01f;
        [SerializeField] private float scaleDuration = 0.25f;

        [Header("References")]
        [SerializeField] private GameObject treeObject;

        [Header("Lamps and Lights")]
        [SerializeField] private List<Lamp> lamps = new();
        [SerializeField] private List<Light> roomLights = new();
        [SerializeField] private float lampBreakDelay = 2f;

        [Header("Despawn Settings")]
        [SerializeField] private float despawnDelayBetweenMonsters = 0.5f;
        [SerializeField] private float deathSequenceDuration = 3f;

        [Header("Kamikaze Overpopulation Control")]
        [SerializeField] private int maxMonstersBeforeKamikaze = 23;
        [SerializeField] private int kamikazeBatchSize = 1;

        private readonly List<MonsterTrackerData> activeMonsters = new();
        private bool isSpawning;
        private Coroutine spawnCoroutine;

        // --------------- Unity lifecycle ---------------

        private void Awake()
        {
            if (monsterPrefab == null || treeObject == null)
            {
                LogError($"Missing {(monsterPrefab == null ? "monsterPrefab" : "treeObject")}.");
                enabled = false;
                return;
            }

            // Clean any null entries provided in the inspector
            spawnPointObjects = spawnPointObjects.Where(t => t != null).ToList();

            if (spawnPointObjects.Count == 0)
            {
                LogWarning("No spawn point objects assigned. Using spawner's own Transform.");
                spawnPointObjects.Add(transform);
            }
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
        }

        // --------------- Public API ---------------

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
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            {
                // synchronous cleanup if we can’t run a coroutine
                foreach (var m in activeMonsters.ToArray())
                    if (m?.SpiderReference) m.SpiderReference.Despawn();
                return;
            }
            StartCoroutine(DespawnAllSequentially());
        }

        // --------------- Spawning loop ---------------

        private void StartSpawning()
        {
            if (isSpawning) return;
            isSpawning = true;
            spawnCoroutine = StartCoroutine(SpawnMonsters());
        }

        private IEnumerator SpawnMonsters()
        {
            while (isSpawning)
            {
                // Drop any dead/destroyed monsters from the tracker
                PruneInactiveMonsters();

                if (activeMonsters.Count < maxMonsters)
                {
                    // Try to find a valid position near a random live spawn point
                    if (TryGetRandomSpawnPosition(out Vector3 spawnPos, out Quaternion spawnRot))
                    {
                        SpawnMonsterAt(spawnPos, spawnRot);
                        CheckForOverpopulation(); // <-- (restored) compile fix
                    }
                }

                yield return new WaitForSeconds(spawnInterval);
            }
        }

        // --------------- Spawn helpers ---------------

        private bool TryGetRandomSpawnPosition(out Vector3 pos, out Quaternion rot)
        {
            pos = default;
            rot = Quaternion.identity;

            // Clean out dead/destroyed spawn objects
            spawnPointObjects = spawnPointObjects
                .Where(t => t != null && t.gameObject.activeInHierarchy && !IsSpawnPointDead(t.gameObject))
                .ToList();

            if (spawnPointObjects.Count == 0)
            {
                LogWarning("No usable spawn point objects; using spawner Transform.");
                spawnPointObjects.Add(transform);
            }

            // Pick a random spawn transform
            Transform pick = spawnPointObjects[Random.Range(0, spawnPointObjects.Count)];
            Vector3 target = pick.position;

            // Sample NavMesh around that object
            if (NavMesh.SamplePosition(target, out NavMeshHit hit, navmeshSampleRadius, NavMesh.AllAreas))
            {
                pos = hit.position + Vector3.up * spawnLift;
                rot = Quaternion.LookRotation(pick.forward, Vector3.up);
                return true;
            }

            // Fallback: try the spawner’s position
            if (NavMesh.SamplePosition(transform.position, out hit, navmeshSampleRadius, NavMesh.AllAreas))
            {
                pos = hit.position + Vector3.up * spawnLift;
                rot = Quaternion.LookRotation(transform.forward, Vector3.up);
                return true;
            }

            LogWarning("Could not find a nearby NavMesh position to spawn.");
            return false;
        }

        private static bool IsSpawnPointDead(GameObject go)
        {
            var hc = go.GetComponent<HealthComponent>();
            return hc != null && hc.IsDead();
        }

        private void SpawnMonsterAt(Vector3 desiredPos, Quaternion rotation)
        {
            if (SpiderPool.Instance == null)
            {
                Debug.LogError("SpiderPool.Instance is null!");
                return;
            }

            // Sample valid NavMesh position first (improvement: prevent off-mesh errors)
            NavMeshHit hit;
            if (!NavMesh.SamplePosition(desiredPos, out hit, 20f, NavMesh.AllAreas)) { // Larger radius for reliability
                Debug.LogWarning($"No valid NavMesh near {desiredPos} for spawn - skipping.");
                return; // Don't spawn if invalid
            }

            // Get from pool at sampled position
            GameObject monsterInstance = SpiderPool.Instance.GetSpider(hit.position, rotation);
            if (monsterInstance == null)
            {
                Debug.LogError("Failed to get monster from SpiderPool!");
                return;
            }

            // Activate and tag
            monsterInstance.SetActive(true);
            monsterInstance.tag = "Enemy";

            // Small scale for pop-in effect
            monsterInstance.transform.localScale = monsterPrefab.transform.localScale * initialScaleMultiplier; // Assuming initialScaleMultiplier defined

            // Physics setup
            if (monsterInstance.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }

            // Register (assuming activeMonsters.Add or similar)
            RegisterMonster(monsterInstance);

            // Scale-up tween
            StartCoroutine(ScaleMonster(monsterInstance.transform, monsterPrefab.transform.localScale));

            // Wait for grounding then enable AI and force agent ready
            if (monsterInstance.TryGetComponent<ShadowMonster>(out var sm))
            {
                StartCoroutine(WaitUntilGroundedThenEnableAI(sm));
            }
        }

        private void RegisterMonster(GameObject monster)
        {
            var shadowMonster = monster.GetComponent<ShadowMonster>();
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
                if (SpiderPool.Instance != null) SpiderPool.Instance.ReturnSpider(monster);
                else Destroy(monster);
            }
        }

        private void PruneInactiveMonsters()
        {
            for (int i = activeMonsters.Count - 1; i >= 0; i--)
            {
                var m = activeMonsters[i];
                if (m == null || m.MonsterObject == null)
                {
                    activeMonsters.RemoveAt(i);
                    continue;
                }

                // If the GO was disabled/destroyed by pool or death, drop it
                if (!m.MonsterObject.activeInHierarchy)
                {
                    activeMonsters.RemoveAt(i);
                }
            }
        }

        private IEnumerator WaitUntilGroundedThenEnableAI(ShadowMonster sm)
        {
            while (!sm.isGrounded)
            {
                yield return null;
            }
            sm.TryMakeAgentReady(50f); // Force ready post-grounding
            sm.EnableAI();
        }

        private void CheckForOverpopulation()
        {
            int excess = activeMonsters.Count - (maxMonstersBeforeKamikaze - 1);
            if (excess <= 0) return;

            var shuffled = activeMonsters.Where(x => x?.SpiderReference != null)
                                         .OrderBy(_ => Random.value)
                                         .ToList();

            int toKamikaze = Mathf.Min(excess, kamikazeBatchSize);
            for (int i = 0; i < toKamikaze && i < shuffled.Count; i++)
            {
                var spider = shuffled[i].SpiderReference;
                if (spider != null && !spider.isInKamikazeMode && spider.healthComponent != null && !spider.healthComponent.IsDead())
                {
                    spider.EnterKamikazeMode();
                    Debug.Log($"[Overpopulation] Forced {spider.name} into Kamikaze mode. Current count: {activeMonsters.Count}");
                }
            }
        }

        // --------------- VFX / Lamps / Lights ---------------

        private IEnumerator BreakLampsSequentially()
        {
            foreach (Lamp lamp in lamps.ToList())
            {
                if (lamp != null)
                {
                    lamp.Break();
                    yield return new WaitForSeconds(lampBreakDelay);
                    Destroy(lamp.gameObject);
                }
            }
            lamps.Clear();

            foreach (Light roomLight in roomLights)
            {
                if (roomLight != null) roomLight.enabled = false;
            }
        }

        // --------------- Despawn ---------------

        private IEnumerator DespawnAllSequentially()
        {
            foreach (var monsterData in activeMonsters.ToList())
            {
                if (monsterData?.MonsterObject != null && monsterData.SpiderReference != null)
                {
                    var health = monsterData.MonsterObject.GetComponent<HealthComponent>();
                    if (health != null && !health.IsDead())
                    {
                        monsterData.SpiderReference.Despawn();
                        yield return new WaitForSeconds(deathSequenceDuration + despawnDelayBetweenMonsters);
                    }

                    if (monsterData.MonsterObject != null)
                    {
                        if (SpiderPool.Instance != null)
                            SpiderPool.Instance.ReturnSpider(monsterData.MonsterObject);
                        else
                            Destroy(monsterData.MonsterObject);
                    }
                }
            }
            activeMonsters.Clear();
            Log("All monsters despawned; spawner persists for reuse.");
        }

        // --------------- Small utilities ---------------

        private IEnumerator ScaleMonster(Transform monsterTransform, Vector3 targetScale)
        {
            if (monsterTransform == null) yield break;

            Vector3 startScale = monsterTransform.localScale;
            float t = 0f;

            while (t < scaleDuration)
            {
                if (monsterTransform == null || !monsterTransform.gameObject.activeInHierarchy) yield break;
                t += Time.deltaTime;
                monsterTransform.localScale = Vector3.Lerp(startScale, targetScale, t / scaleDuration);
                yield return null;
            }

            if (monsterTransform != null) monsterTransform.localScale = targetScale;
        }

        private void Log(string message) => Debug.Log($"[ShadowMonsterSpawner {name}] {message}");
        private void LogWarning(string message) => Debug.LogWarning($"[ShadowMonsterSpawner {name}] {message}");
        private void LogError(string message) => Debug.LogError($"[ShadowMonsterSpawner {name}] {message}");
    }
}

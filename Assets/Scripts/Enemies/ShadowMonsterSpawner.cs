using UnityEngine;
using System.Collections;

public class ShadowMonsterSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private GameObject shadowMonsterPrefab;
    [SerializeField] private float spawnRadius = 10f;
    [SerializeField] private float spawnHeight = 1f;
    [SerializeField] private int maxMonsters = 5;
    [SerializeField] private float spawnInterval = 3f;
    
    [Header("Wave Settings")]
    [SerializeField] private float waveInterval = 15f;
    [SerializeField] private int monstersPerWave = 3;
    [SerializeField] private float difficultyIncrease = 0.2f;
    
    private TreeOfLight treeOfLight;
    private bool isSpawning = false;
    private int currentWave = 0;
    private int activeMonsters = 0;

    private void Start()
    {
        treeOfLight = FindObjectOfType<TreeOfLight>();
        if (treeOfLight == null)
        {
            Debug.LogWarning("No Tree of Light found in scene!");
            enabled = false;
            return;
        }
    }

    public void StartSpawning()
    {
        if (isSpawning) return;
        
        isSpawning = true;
        currentWave = 0;
        StartCoroutine(SpawnWaves());
    }

    public void StopSpawning()
    {
        isSpawning = false;
        StopAllCoroutines();
    }

    private IEnumerator SpawnWaves()
    {
        while (isSpawning)
        {
            // Wait for wave interval
            yield return new WaitForSeconds(waveInterval);
            
            // Spawn monsters for this wave
            for (int i = 0; i < monstersPerWave && activeMonsters < maxMonsters; i++)
            {
                SpawnMonster();
                yield return new WaitForSeconds(spawnInterval);
            }
            
            // Increase difficulty for next wave
            currentWave++;
            monstersPerWave = Mathf.RoundToInt(monstersPerWave * (1 + difficultyIncrease));
        }
    }

    private void SpawnMonster()
    {
        if (activeMonsters >= maxMonsters) return;

        // Calculate random position around the tree
        float angle = Random.Range(0f, 360f);
        float distance = Random.Range(spawnRadius * 0.5f, spawnRadius);
        Vector3 spawnPosition = treeOfLight.transform.position + 
            new Vector3(
                Mathf.Cos(angle * Mathf.Deg2Rad) * distance,
                spawnHeight,
                Mathf.Sin(angle * Mathf.Deg2Rad) * distance
            );

        // Spawn the monster
        GameObject monster = Instantiate(shadowMonsterPrefab, spawnPosition, Quaternion.identity);
        activeMonsters++;

        // Subscribe to monster's death
        ShadowMonster shadowMonster = monster.GetComponent<ShadowMonster>();
        if (shadowMonster != null)
        {
            StartCoroutine(MonitorMonsterDeath(shadowMonster));
        }
    }

    private IEnumerator MonitorMonsterDeath(ShadowMonster monster)
    {
        while (monster != null)
        {
            yield return new WaitForSeconds(0.1f);
        }
        activeMonsters--;
    }

    private void OnDrawGizmosSelected()
    {
        // Visualize spawn radius
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
    }
} 
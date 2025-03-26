using UnityEngine;
using System.Collections;

public class ShadowMonsterSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private GameObject shadowMonsterPrefab;
    [SerializeField] private float spawnRadius = 10f;
    [SerializeField] private float spawnHeight = 1f;
    [SerializeField] private int maxMonsters = 5;
    [SerializeField] private float spawnInterval = 5f;
    
    [Header("Wave Settings")]
    [SerializeField] private float waveInterval = 15f;
    [SerializeField] private int monstersPerWave = 3;
    [SerializeField] private float difficultyIncrease = 0.2f;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private bool useSpawnPoints = false;
    [SerializeField] private bool randomizeSpawnPoints = true;
    
    [Header("Wall Crawling Settings")]
    [SerializeField] private bool spawnOnWalls = true;
    [SerializeField] private LayerMask spawnSurfacesLayerMask;
    [SerializeField] private float surfaceDetectionHeight = 10f;

    private int currentSpawnPointIndex = 0;
    private void SpawnMonster() 
   {
    if (!isSpawning || treeOfLight == null) return;
    
    Vector3 spawnPosition;
    Vector3 surfaceNormal = Vector3.up;
    bool validSurfaceFound = false;
    
    if (useSpawnPoints && spawnPoints != null && spawnPoints.Length > 0)
    {
        // Use defined spawn points
        if (randomizeSpawnPoints)
        {
            int randomIndex = Random.Range(0, spawnPoints.Length);
            spawnPosition = spawnPoints[randomIndex].position;
        }
        else
        {
            spawnPosition = spawnPoints[currentSpawnPointIndex].position;
            currentSpawnPointIndex = (currentSpawnPointIndex + 1) % spawnPoints.Length;
        }
        
        validSurfaceFound = true;
    }
    else
    {
        // Choose random spawn location
        if (spawnOnWalls)
        {
            // Try to find a valid surface (floor, wall, ceiling)
            for (int attempt = 0; attempt < 10; attempt++)
            {
                // Generate random point within spawn radius
                Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
                Vector3 randomPoint = treeOfLight.transform.position + 
                    new Vector3(randomCircle.x, Random.Range(0, surfaceDetectionHeight), randomCircle.y);
                
                // Cast rays in random directions to find surfaces
                Vector3 randomDirection = Random.onUnitSphere;
                if (Physics.Raycast(randomPoint, randomDirection, out RaycastHit hit, 
                                   spawnRadius, spawnSurfacesLayerMask))
                {
                    spawnPosition = hit.point + hit.normal * 0.1f; // Slightly offset from surface
                    surfaceNormal = hit.normal;
                    validSurfaceFound = true;
                    break;
                }
            }
            
            // If no valid surface found, use default spawn method
            if (!validSurfaceFound)
            {
                Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
                spawnPosition = treeOfLight.transform.position + 
                    new Vector3(randomCircle.x, spawnHeight, randomCircle.y);
                validSurfaceFound = true;
            }
        }
        else
        {
            // Original random spawn code
            Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
            spawnPosition = treeOfLight.transform.position + 
                new Vector3(randomCircle.x, spawnHeight, randomCircle.y);
            validSurfaceFound = true;
        }
    }
    
    if (validSurfaceFound)
    {
        // Create the monster
        GameObject monster = Instantiate(shadowMonsterPrefab, spawnPosition, Quaternion.identity);
        
        // Orient monster based on surface normal if spawning on walls
        if (spawnOnWalls)
        {
            monster.transform.rotation = Quaternion.FromToRotation(Vector3.up, surfaceNormal);
        }
        
        monster.transform.parent = transform; // Organize in hierarchy
    }
}


    [Serializable]
    public class DifficultyWave {
        public int waveNumber;
        public int monstersPerWave;
        public float monsterHealthMultiplier = 1.0f;
        public float monsterDamageMultiplier = 1.0f;
        public float monsterSpeedMultiplier = 1.0f;
    }

    [SerializeField] private DifficultyWave[] difficultyWaves;
    [SerializeField] private bool useCustomDifficultyWaves = false;

    private void ApplyWaveDifficulty() {
        if (useCustomDifficultyWaves && difficultyWaves != null && difficultyWaves.Length > 0) {
            // Find the appropriate difficulty wave
            DifficultyWave currentDifficulty = null;
            for (int i = difficultyWaves.Length - 1; i >= 0; i--) {
                if (currentWave >= difficultyWaves[i].waveNumber) {
                    currentDifficulty = difficultyWaves[i];
                    break;
                }
            }
        
            if (currentDifficulty != null) {
                monstersPerWave = currentDifficulty.monstersPerWave;
                // Store difficulty multipliers to apply to monsters when spawned
            }
        } else {
            // Original difficulty progression
            monstersPerWave = Mathf.RoundToInt(monstersPerWave * (1 + difficultyIncrease));
        }
    }
    
    [Header("Wave Feedback")]
    [SerializeField] private AudioClip newWaveSound;
    [SerializeField] private ParticleSystem waveStartVfx;
    [SerializeField] private bool showWaveUI = true;

    private void StartNewWave() {
        currentWave++;
    
        // Visual feedback
        if (waveStartVfx != null) {
            waveStartVfx.Play();
        }
    
        // Audio feedback
        if (newWaveSound != null) {
            AudioSource.PlayClipAtPoint(newWaveSound, treeOfLight.transform.position);
        }
    
        // UI notification could be implemented here
        if (showWaveUI) {
            // Call to UI manager to show wave number
        }
    }


    private TreeOfLight treeOfLight;
    private bool isSpawning = false;
    private int currentWave = 0;
    private int activeMonsters = 0;
    private List<ShadowMonster> activeMonstersList = new List<ShadowMonster>();

    private void OnEnable() {
        ShadowMonster.OnMonsterSpawned += RegisterMonster;
        ShadowMonster.OnMonsterDeath += UnregisterMonster;
    }

    private void OnDisable() {
        ShadowMonster.OnMonsterSpawned -= RegisterMonster;
        ShadowMonster.OnMonsterDeath -= UnregisterMonster;
    }

    private void RegisterMonster(ShadowMonster monster) {
        activeMonstersList.Add(monster);
        activeMonsters = activeMonstersList.Count;
    }

    private void UnregisterMonster(ShadowMonster monster) {
        activeMonstersList.Remove(monster);
        activeMonsters = activeMonstersList.Count;
    }

    private void Start()
    {
        currentHealth = maxHealth;
        monsterRenderer = GetComponentInChildren<Renderer>();
        animator = GetComponentInChildren<Animator>();
    
        // Find tree of light or player as target
        GameObject treeObj = GameObject.FindGameObjectWithTag("TreeOfLight");
        if (treeObj != null)
        {
            target = treeObj.transform;
        }
        else
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                target = player.transform;
            }
        }
    
        if (OnMonsterSpawned != null)
        {
            OnMonsterSpawned(this);
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
        CancelInvoke("SpawnMonster");
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
        if (!isSpawning || treeOfLight == null) return;
        
        // Spawn monster at random position within radius
        Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
        Vector3 spawnPosition = treeOfLight.transform.position + new Vector3(randomCircle.x, 0f, randomCircle.y);
        
        Instantiate(shadowMonsterPrefab, spawnPosition, Quaternion.identity);
    }
    private void Attack()
    {
        // Trigger attack animation
        if (animator != null)
        {
            animator.SetTrigger(AttackTrigger);
        }
    
        // Play attack effect
        if (attackEffect != null)
        {
            attackEffect.Play();
        }
    
        // Play attack sound
        if (audioSource != null && attackSound != null)
        {
            audioSource.PlayOneShot(attackSound);
        }
    
        // Deal damage to the tree or player
        if (target != null)
        {
            TreeOfLight tree = target.GetComponent<TreeOfLight>();
            if (tree != null)
            {
                tree.TakeDamage(attackDamage);
            }
            else
            {
                PlayerHealth playerHealth = target.GetComponent<PlayerHealth>();
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(attackDamage);
                }
            }
        }
    }


    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, surfaceDetectionRadius);
         
        if (isGrounded)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawRay(transform.position, surfaceNormal * 2);
        }
    }

} 
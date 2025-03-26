using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Items
{
    public class TreeOfLightPot : MonoBehaviour
    {
        [Header("Tree Growth Settings")]
        [SerializeField] private GameObject treePrefab;
        [SerializeField] private Transform treeSpawnPoint;
        [SerializeField] private float growthDelay = 1.0f;
        [SerializeField] private float growthDuration = 30.0f;
        [SerializeField] private Vector3 maxTreeScale = new Vector3(1, 1, 1);
    
        [Header("Pot Orientation")]
        [SerializeField] private float tiltThreshold = 30f;
        [SerializeField] private float checkInterval = 0.5f;
    
        [Header("Monster Spawning")]
        [SerializeField] private ShadowMonsterSpawner monsterSpawner;
    
        [Header("Door Key")]
        [SerializeField] private GameObject doorKeyPrefab;
        [SerializeField] private Transform keySpawnPoint;
    
        [Header("Effects")]
        [SerializeField] private ParticleSystem breakEffect;
        [SerializeField] private AudioClip breakSound;
        [SerializeField] private AudioSource audioSource;
    
        [Header("Interaction")]
        [SerializeField] private UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor socketInteractor;
    
        // Private variables
        private TreeOfLight treeOfLight;
        private bool isGrowing = false;
        private bool hasSeedBeenPlanted = false;
        private bool isPotUpright = true;
        private bool wasUpright = true;
        private float lastCheckTime = 0f;
        private Rigidbody rb;
    
        private void Awake()
        {
            // Set up references
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }
        
            // Validate required components
            if (treePrefab == null)
            {
                Debug.LogError("Tree prefab not assigned to TreeOfLightPot!");
            }
        
            if (treeSpawnPoint == null)
            {
                Debug.LogError("Tree spawn point not assigned to TreeOfLightPot!");
                // Create a default spawn point if none exists
                GameObject spawnPoint = new GameObject("TreeSpawnPoint");
                spawnPoint.transform.SetParent(transform);
                spawnPoint.transform.localPosition = Vector3.zero;
                treeSpawnPoint = spawnPoint.transform;
                Debug.Log("Created default tree spawn point");
            }
        
            // Create key spawn point if it doesn't exist or if it's using the tree spawn point
            if (keySpawnPoint == null || keySpawnPoint == treeSpawnPoint)
            {
                Debug.Log("Creating key spawn point");
                GameObject keyPoint = new GameObject("KeySpawnPoint");
                keyPoint.transform.SetParent(transform);
                keyPoint.transform.localPosition = new Vector3(0, 0.5f, 0); // Spawn slightly above the pot
                keySpawnPoint = keyPoint.transform;
                Debug.Log("Created key spawn point");
            }
        
            // Validate monster spawner
            if (monsterSpawner == null)
            {
                Debug.LogError("Monster spawner not assigned to TreeOfLightPot!");
                monsterSpawner = FindFirstObjectByType<ShadowMonsterSpawner>();
                if (monsterSpawner == null)
                {
                    Debug.LogError("No ShadowMonsterSpawner found in scene!");
                    enabled = false;
                    return;
                }
                Debug.Log("Found ShadowMonsterSpawner in scene");
            }
        
            // Validate maxTreeScale
            if (!IsValidScale(maxTreeScale))
            {
                Debug.LogWarning("Invalid maxTreeScale detected, setting to default value");
                maxTreeScale = Vector3.one;
            }
        
            // Set up socket listener
            if (socketInteractor != null)
            {
                socketInteractor.selectEntered.AddListener(OnSeedSocketed);
                socketInteractor.selectExited.AddListener(OnSeedUnsocketed);
                Debug.Log("Socket interactor listener set up");
            }
            else
            {
                Debug.LogError("Socket interactor not assigned!");
            }
        
            rb = GetComponent<Rigidbody>();
        }
    
        private bool IsValidScale(Vector3 scale)
        {
            return float.IsFinite(scale.x) && float.IsFinite(scale.y) && float.IsFinite(scale.z) &&
                   scale.x > 0 && scale.y > 0 && scale.z > 0 &&
                   scale.x < 1000 && scale.y < 1000 && scale.z < 1000; // Add reasonable upper bounds
        }
    
        private void Update()
        {
            // Only check orientation periodically to save performance
            if (Time.time - lastCheckTime > checkInterval)
            {
                lastCheckTime = Time.time;
                CheckPotOrientation();
            }
        }
    
        private void CheckPotOrientation()
        {
            // Check if the pot is upright (not tilted too much)
            float tiltAngle = Vector3.Angle(transform.up, Vector3.up);
            isPotUpright = tiltAngle < tiltThreshold;
        
            // Only log when orientation changes
            if (wasUpright != isPotUpright)
            {
                if (isPotUpright)
                {
                    Debug.Log($"Pot is now upright (tilt: {tiltAngle} degrees)");
                    // Resume tree growth if it exists and was growing
                    if (treeOfLight != null && isGrowing)
                    {
                        Debug.Log("Resuming tree growth");
                        treeOfLight.ResumeGrowth();
                    }
                }
                else
                {
                    Debug.Log($"Pot is tilted too much (tilt: {tiltAngle} degrees)");
                    // Pause tree growth if it exists and was growing
                    if (treeOfLight != null && isGrowing)
                    {
                        Debug.Log("Pausing tree growth due to pot tilt");
                        treeOfLight.StopGrowth();
                    }
                }
            
                wasUpright = isPotUpright;
            }
        }
    
        private void OnSeedSocketed(SelectEnterEventArgs args)
        {
            // Check if the socketed object has the MagicalSeed component
            var seed = args.interactableObject.transform.GetComponent<MagicalSeed>();
            if (seed == null)
            {
                // If it's not a seed, make the pot kinematic to prevent physics
                if (rb != null)
                {
                    rb.isKinematic = true;
                }
                return;
            }

            // Store the seed reference and start growth
            StartCoroutine(GrowTree(seed.gameObject));
        }
    
        private void OnSeedUnsocketed(SelectExitEventArgs args)
        {
            // Check if the unsocketed object has the MagicalSeed component
            var seed = args.interactableObject.transform.GetComponent<MagicalSeed>();
            if (seed == null)
            {
                // If it wasn't a seed, restore the pot's physics
                if (rb != null)
                {
                    rb.isKinematic = false;
                }
            }
        }
    
        private IEnumerator GrowTree(GameObject seed)
        {
            Debug.Log("Starting tree growth sequence...");
        
            // Wait for initial delay
            yield return new WaitForSeconds(growthDelay);
            Debug.Log($"Initial delay complete ({growthDelay}s)");
        
            // Make seed sink into the pot before destroying
            if (seed != null)
            {
                // Get the seed's Rigidbody and make it kinematic
                Rigidbody seedRb = seed.GetComponent<Rigidbody>();
                if (seedRb != null)
                {
                    seedRb.isKinematic = true;
                }

                // Animate the seed sinking
                float sinkDuration = 0.5f; // Duration of sinking animation
                float elapsedTime = 0f;
                Vector3 startPosition = seed.transform.position;
                Vector3 endPosition = startPosition + Vector3.down * 0.2f; // Move down 20cm

                while (elapsedTime < sinkDuration)
                {
                    elapsedTime += Time.deltaTime;
                    float t = elapsedTime / sinkDuration;
                    // Use smoothstep for a more natural sinking motion
                    t = t * t * (3f - 2f * t);
                    seed.transform.position = Vector3.Lerp(startPosition, endPosition, t);
                    yield return null;
                }

                // Destroy the seed after sinking
                Destroy(seed);
                Debug.Log("Seed sunk and destroyed");
            }
        
            // Spawn the tree if it doesn't exist
            if (treeOfLight == null && treePrefab != null)
            {
                Debug.Log("Spawning tree prefab...");
                try
                {
                    // Validate spawn point
                    if (treeSpawnPoint == null)
                    {
                        Debug.LogError("Tree spawn point is null!");
                        yield break;
                    }

                    // Spawn at tree spawn point
                    GameObject treeObject = Instantiate(treePrefab);
                    Debug.Log("Tree prefab instantiated");
                
                    // Ensure valid position and rotation
                    treeObject.transform.SetParent(treeSpawnPoint);
                    treeObject.transform.localPosition = Vector3.zero;
                    treeObject.transform.localRotation = Quaternion.identity;
                    
                    // Start with a very small scale to avoid AABB issues
                    treeObject.transform.localScale = new Vector3(0.001f, 0.001f, 0.001f);
                    Debug.Log("Tree positioned and scaled");

                    // Get the TreeOfLight component and start growth
                    treeOfLight = treeObject.GetComponent<TreeOfLight>();
                    if (treeOfLight != null)
                    {
                        Debug.Log("Found TreeOfLight component, starting growth");
                        // Set the parent pot reference directly
                        treeOfLight.SetParentPot(this);
                        treeOfLight.SetGrowthSpeed(1f / growthDuration);

                        // Validate and use maxTreeScale
                        Vector3 validatedScale = maxTreeScale;
                        if (!IsValidScale(validatedScale))
                        {
                            Debug.LogWarning("Invalid maxTreeScale, using default value");
                            validatedScale = Vector3.one;
                        }

                        // Use the largest component of maxTreeScale to ensure consistent scaling
                        float scale = Mathf.Max(validatedScale.x, validatedScale.y, validatedScale.z);
                        treeOfLight.StartGrowth(growthDuration, scale);
                        Debug.Log($"Tree growth started with duration: {growthDuration}, maxScale: {scale}");

                        // Start spawning monsters when the tree starts growing
                        if (monsterSpawner != null)
                        {
                            Debug.Log("Starting monster spawning");
                            monsterSpawner.StartSpawning();
                        }
                    }
                    else
                    {
                        Debug.LogError("TreeOfLight component not found on tree prefab!");
                        yield break;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error during tree spawning: {e.Message}\n{e.StackTrace}");
                    yield break;
                }
            }
            else
            {
                Debug.LogWarning("Tree already exists or prefab not assigned!");
            }

            // Wait for the tree to complete its growth
            Debug.Log($"Waiting for growth duration: {growthDuration}s");
            yield return new WaitForSeconds(growthDuration);
        
            // Stop spawning monsters when the tree is fully grown
            if (monsterSpawner != null)
            {
                Debug.Log("Stopping monster spawning - tree fully grown");
                monsterSpawner.StopSpawning();
            }

            Debug.Log("Tree growth complete - ready for monster spawning stage");
        }
    
        public void OnTreeBreak()
        {
            Debug.Log("Tree broke - starting pot break sequence");
        
            // Play break sound if available
            if (breakSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(breakSound);
            }

            // Play break effect if available
            if (breakEffect != null)
            {
                Debug.Log("Playing pot break effect");
                // Create a new GameObject for the particle system
                GameObject effectObject = new GameObject("PotBreakEffect");
                effectObject.transform.position = transform.position;
                ParticleSystem effect = Instantiate(breakEffect, effectObject.transform);
                effect.Play();
            
                // Destroy the effect object after the effect is done
                Destroy(effectObject, effect.main.duration);
            
                // Wait for effect to complete before proceeding
                StartCoroutine(HandleBreakSequence(effect.main.duration));
            }
            else
            {
                Debug.LogWarning("No break effect assigned to pot");
                StartCoroutine(HandleBreakSequence(0));
            }
        }
    
        private IEnumerator HandleBreakSequence(float delay = 0)
        {
            // Wait for the break effect to complete
            if (delay > 0)
            {
                Debug.Log($"Waiting {delay} seconds for pot break effect to complete");
                yield return new WaitForSeconds(delay);
            }

            // Spawn key if available
            if (doorKeyPrefab != null && keySpawnPoint != null)
            {
                Debug.Log("Spawning key");
                Instantiate(doorKeyPrefab, keySpawnPoint.position, keySpawnPoint.rotation);
            }
            else
            {
                Debug.LogWarning("Key prefab or spawn point not assigned!");
            }

            // Monster spawner code removed - will be added later
        
            // Disable the pot
            gameObject.SetActive(false);
        }
    
        public void StartBreakSequence()
        {
            Debug.Log("Starting pot break sequence");
            
            // Play break effect if available
            if (breakEffect != null)
            {
                breakEffect.Play();
                if (audioSource != null && breakSound != null)
                {
                    audioSource.PlayOneShot(breakSound);
                }
            }
            
            // Start the break sequence coroutine
            StartCoroutine(HandleBreakSequence(breakEffect.main.duration));
        }
    
        // This method is now disabled - all seed processing happens through the Socket Interactor
        private void OnTriggerEnter(Collider other)
        {
            // We're using XR Socket Interactor instead of collision triggers
            return;
        }
    }
}
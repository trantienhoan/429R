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
    
        // Commented out for now - you'll add this later
        // [Header("Monster Spawning")]
        // [SerializeField] private MonsterSpawner monsterSpawner;
    
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
                Debug.Log("Socket interactor listener set up");
            }
            else
            {
                Debug.LogError("Socket interactor not assigned!");
            }
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
            Debug.Log($"OnSeedSocketed called - isGrowing: {isGrowing}, hasSeedBeenPlanted: {hasSeedBeenPlanted}");
        
            // Don't process if already growing or has seed
            if (isGrowing || hasSeedBeenPlanted)
            {
                Debug.Log("Ignoring seed socket - already growing or has seed");
                return;
            }

            // Check if pot is upright before accepting seed
            if (!isPotUpright)
            {
                Debug.Log("Cannot plant seed: Pot is not upright!");
                return;
            }

            // Get the seed component
            MagicalSeed seed = args.interactableObject.transform.GetComponent<MagicalSeed>();
            if (seed != null)
            {
                Debug.Log("Valid seed detected in socket, starting growth...");
            
                // Disable the socket interactor to prevent other objects from being socketed
                if (socketInteractor != null)
                {
                    socketInteractor.enabled = false;
                    Debug.Log("Socket interactor disabled");
                }

                // Mark as growing and planted
                isGrowing = true;
                hasSeedBeenPlanted = true;
            
                // Make seed stay in position but visually present
                UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable seedGrab = seed.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
                if (seedGrab != null)
                {
                    seedGrab.enabled = false;
                    Debug.Log("Disabled seed grab interactable");
                }
            
                Rigidbody seedRb = seed.GetComponent<Rigidbody>();
                if (seedRb != null)
                {
                    seedRb.isKinematic = true;
                    Debug.Log("Disabled seed physics");
                }
            
                // Parent the seed to the pot to ensure it stays in place
                seed.transform.SetParent(socketInteractor.attachTransform);
                seed.transform.localPosition = Vector3.zero;
                seed.transform.localRotation = Quaternion.identity;

                // Start growth sequence
                StartCoroutine(GrowTree(seed.gameObject));
            }
            else
            {
                Debug.LogError("Object in socket does not have MagicalSeed component!");
            }
        }
    
        private IEnumerator GrowTree(GameObject seed)
        {
            Debug.Log("Starting tree growth sequence...");
        
            // Wait for initial delay
            yield return new WaitForSeconds(growthDelay);
            Debug.Log($"Initial delay complete ({growthDelay}s)");
        
            // Destroy the seed now that we're ready to grow the tree
            if (seed != null)
            {
                Destroy(seed);
                Debug.Log("Seed destroyed");
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
        
            // Monster spawning code removed - will be added later
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
    
        // This method is now disabled - all seed processing happens through the Socket Interactor
        private void OnTriggerEnter(Collider other)
        {
            // We're using XR Socket Interactor instead of collision triggers
            return;
        }
    }
}
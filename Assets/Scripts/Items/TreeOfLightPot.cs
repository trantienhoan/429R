using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using System.Collections;
using UnityEngine.Events;

public class TreeOfLightPot : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject treePrefab;
    [SerializeField] private GameObject doorKeyPrefab;
    [SerializeField] private Transform keySpawnPoint;
    [SerializeField] private Transform treeSpawnPoint;
    
    [Header("Growth Settings")]
    [SerializeField] private float growthDuration = 60f;
    [SerializeField] private float maxTreeScale = 1f;
    [SerializeField] private float growthDelay = 0.5f;
    [SerializeField] private float maxTiltAngle = 15f;
    
    [Header("Light Settings")]
    [SerializeField] private float initialLightIntensity = 0.1f;
    [SerializeField] private float growingLightIntensity = 0.3f;
    [SerializeField] private float finalLightIntensity = 2f;
    
    [Header("Break Settings")]
    [SerializeField] private float breakDelay = 1f;
    [SerializeField] private AudioClip breakSound;
    [SerializeField] private ParticleSystem breakEffect;
    
    private TreeOfLight treeOfLight;
    private AudioSource audioSource;
    private bool hasSpawnedKey = false;
    private ShadowMonsterSpawner monsterSpawner;
    private bool isGrowing = false;
    private bool hasSeedBeenPlanted = false;
    private UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor socketInteractor;
    private bool isPotUpright = false;
    private bool hasSeed = false;
    private bool wasUpright = false;
    private float tiltThreshold = 15f;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        socketInteractor = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor>();
        
        // Configure socket to only accept seeds
        if (socketInteractor != null)
        {
            Debug.Log("Found socket interactor, configuring...");
            // Set up interaction layers to only accept Default layer
            socketInteractor.interactionLayers = InteractionLayerMask.GetMask("Default");
            
            // Configure socket settings
            socketInteractor.enabled = true;
            
            // Subscribe to socket events
            socketInteractor.selectEntered.AddListener(OnSeedSocketed);
            Debug.Log("Socket interactor configured successfully");
        }
        else
        {
            Debug.LogError("No socket interactor found on pot!");
        }
        
        // Debug log to check prefab assignments
        if (doorKeyPrefab == null)
        {
            Debug.LogWarning("Door Key Prefab is not assigned in TreeOfLightPot!");
        }
        if (keySpawnPoint == null)
        {
            Debug.LogWarning("Key Spawn Point is not assigned in TreeOfLightPot!");
        }
        if (treePrefab == null)
        {
            Debug.LogError("Tree Prefab is not assigned in TreeOfLightPot!");
        }
        if (treeSpawnPoint == null)
        {
            Debug.LogError("Tree Spawn Point is not assigned in TreeOfLightPot!");
        }
        
        // Find the monster spawner
        monsterSpawner = Object.FindFirstObjectByType<ShadowMonsterSpawner>();
        if (monsterSpawner == null)
        {
            Debug.LogWarning("No ShadowMonsterSpawner found in scene!");
        }
    }

    private void Update()
    {
        if (!isGrowing) return;
        
        // Check if pot is tilted
        float tiltAngle = Vector3.Angle(transform.up, Vector3.up);
        bool isUpright = tiltAngle < tiltThreshold;
        
        if (isUpright != wasUpright)
        {
            wasUpright = isUpright;
            if (isUpright)
            {
                Debug.Log("Pot is upright - resuming growth");
                ResumeGrowth();
            }
            else
            {
                Debug.Log($"Pot is tilted ({tiltAngle} degrees) - pausing growth");
                PauseGrowth();
            }
        }
    }
    
    private bool CheckPotOrientation()
    {
        // Get the angle between the pot's up vector and world up vector
        float angle = Vector3.Angle(transform.up, Vector3.up);
        return angle <= maxTiltAngle;
    }
    
    private void ResumeGrowth()
    {
        if (!isGrowing) return;
        
        TreeOfLight treeOfLight = FindObjectOfType<TreeOfLight>();
        if (treeOfLight != null)
        {
            Debug.Log("Resuming tree growth");
            treeOfLight.ResumeGrowth();
        }
        else
        {
            Debug.LogWarning("No TreeOfLight found to resume!");
        }
    }
    
    private void PauseGrowth()
    {
        if (!isGrowing) return;
        
        TreeOfLight treeOfLight = FindObjectOfType<TreeOfLight>();
        if (treeOfLight != null)
        {
            Debug.Log("Pausing tree growth");
            treeOfLight.StopGrowth();
        }
        else
        {
            Debug.LogWarning("No TreeOfLight found to pause!");
        }
    }
    
    private void OnSeedSocketed(SelectEnterEventArgs args)
    {
        Debug.Log($"OnSeedSocketed called - isGrowing: {isGrowing}, hasSeedBeenPlanted: {hasSeedBeenPlanted}");
        
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

            // Start growing
            isGrowing = true;
            hasSeedBeenPlanted = true;

            // Destroy the seed immediately
            Destroy(seed.gameObject);
            Debug.Log("Seed destroyed");

            // Start growth sequence
            StartCoroutine(GrowTree());
        }
        else
        {
            Debug.LogError("Object in socket does not have MagicalSeed component!");
        }
    }
    
    private IEnumerator GrowTree()
    {
        Debug.Log("Starting tree growth sequence...");
        
        // Wait for initial delay
        yield return new WaitForSeconds(growthDelay);
        Debug.Log($"Initial delay complete ({growthDelay}s)");

        // Spawn the tree if it doesn't exist
        if (treeOfLight == null && treePrefab != null)
        {
            Debug.Log("Spawning tree prefab...");
            try
            {
                // Spawn at tree spawn point and parent it
                GameObject treeObject = Instantiate(treePrefab);
                Debug.Log("Tree prefab instantiated");
                
                // Parent to the TreeSpawnPoint for proper positioning and scaling
                treeObject.transform.SetParent(treeSpawnPoint);
                treeObject.transform.localPosition = Vector3.zero;
                treeObject.transform.localRotation = Quaternion.identity;
                treeObject.transform.localScale = Vector3.zero;
                Debug.Log("Tree positioned and scaled");

                // Get the TreeOfLight component and start growth
                treeOfLight = treeObject.GetComponent<TreeOfLight>();
                if (treeOfLight != null)
                {
                    Debug.Log("Found TreeOfLight component, starting growth");
                    // Set the parent pot reference directly
                    treeOfLight.SetParentPot(this);
                    treeOfLight.SetGrowthSpeed(1f / growthDuration);
                    treeOfLight.StartGrowth(growthDuration, maxTreeScale);
                    Debug.Log($"Tree growth started with duration: {growthDuration}, maxScale: {maxTreeScale}");
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
        
        // Start spawning monsters when fully grown
        if (monsterSpawner != null)
        {
            Debug.Log("Starting monster spawning");
            monsterSpawner.StartSpawning();
        }
        else
        {
            Debug.LogWarning("No monster spawner found to start spawning");
        }
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

        // Stop monster spawning if available
        if (monsterSpawner != null)
        {
            monsterSpawner.StopSpawning();
        }

        // Disable the pot
        gameObject.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"Trigger entered by: {other.gameObject.name}, Tag: {other.tag}");
        
        if (hasSeed || isGrowing) return;
        
        if (other.CompareTag("MagicalSeed"))
        {
            Debug.Log("Magical seed detected in pot!");
            hasSeed = true;
            
            // Disable physics on the seed
            Rigidbody seedRb = other.GetComponent<Rigidbody>();
            if (seedRb != null)
            {
                seedRb.isKinematic = true;
                Debug.Log("Disabled seed physics");
            }
            
            // Parent the seed to the pot
            other.transform.SetParent(transform);
            other.transform.localPosition = Vector3.zero;
            other.transform.localRotation = Quaternion.identity;
            Debug.Log("Parented seed to pot");
            
            // Start growing the tree
            StartGrowingTree();
        }
    }

    private void StartGrowingTree()
    {
        Debug.Log("Starting tree growth process...");
        
        if (treePrefab == null)
        {
            Debug.LogError("Tree prefab is not assigned!");
            return;
        }
        
        if (treeSpawnPoint == null)
        {
            Debug.LogError("Tree spawn point is not assigned!");
            return;
        }
        
        Debug.Log($"Spawning tree at position: {treeSpawnPoint.position}");
        GameObject tree = Instantiate(treePrefab, treeSpawnPoint.position, treeSpawnPoint.rotation);
        
        TreeOfLight treeOfLight = tree.GetComponent<TreeOfLight>();
        if (treeOfLight == null)
        {
            Debug.LogError("TreeOfLight component not found on tree prefab!");
            return;
        }
        
        Debug.Log("Setting up tree...");
        treeOfLight.SetParentPot(this);
        treeOfLight.SetGrowthSpeed(1f / growthDuration);
        
        Debug.Log($"Starting tree growth with duration: {growthDuration}, scale: {maxTreeScale}");
        treeOfLight.StartGrowth(growthDuration, maxTreeScale);
        
        isGrowing = true;
        Debug.Log("Tree growth process started successfully");
    }
} 
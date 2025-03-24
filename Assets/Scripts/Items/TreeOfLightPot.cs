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
    [SerializeField] private float maxTiltAngle = 15f; // Maximum allowed tilt angle
    
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
    
    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        socketInteractor = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor>();
        
        // Configure socket to only accept seeds (like Door_Locked)
        if (socketInteractor != null)
        {
            // Set up interaction layers to only accept Default layer (like Door_Locked)
            socketInteractor.interactionLayers = InteractionLayerMask.GetMask("Default");
            
            // Configure socket settings
            socketInteractor.enabled = true;
            
            // Subscribe to socket events
            socketInteractor.selectEntered.AddListener(OnSeedSocketed);
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
            Debug.LogWarning("Tree Prefab is not assigned in TreeOfLightPot!");
        }
        if (treeSpawnPoint == null)
        {
            Debug.LogWarning("Tree Spawn Point is not assigned in TreeOfLightPot!");
        }
        
        // Find the monster spawner
        monsterSpawner = Object.FindFirstObjectByType<ShadowMonsterSpawner>();
    }

    private void Update()
    {
        // Check if pot is upright
        bool wasUpright = isPotUpright;
        isPotUpright = CheckPotOrientation();
        
        // If pot is not upright and tree is growing, stop growth
        if (!isPotUpright && isGrowing && treeOfLight != null)
        {
            Debug.Log("Pot is not upright, stopping tree growth");
            StopGrowth();
        }
        // If pot becomes upright again and has a seed planted but not growing, resume growth
        else if (isPotUpright && !wasUpright && hasSeedBeenPlanted && !isGrowing && treeOfLight != null)
        {
            Debug.Log("Pot is upright again, resuming tree growth");
            ResumeGrowth();
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
        isGrowing = true;
        if (treeOfLight != null)
        {
            treeOfLight.ResumeGrowth();
        }
    }
    
    private void StopGrowth()
    {
        isGrowing = false;
        if (treeOfLight != null)
        {
            treeOfLight.StopGrowth();
        }
    }
    
    private void OnSeedSocketed(SelectEnterEventArgs args)
    {
        if (isGrowing || hasSeedBeenPlanted) return;

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
            Debug.Log("Seed detected in socket, starting growth...");
            // Start growing
            isGrowing = true;
            hasSeedBeenPlanted = true;
            
            // Disable the socket interactor to prevent other objects from being socketed
            if (socketInteractor != null)
            {
                socketInteractor.enabled = false;
            }

            // Destroy the seed immediately
            Destroy(seed.gameObject);

            // Start growth sequence
            StartCoroutine(GrowTree());
        }
    }
    
    private IEnumerator GrowTree()
    {
        Debug.Log("Starting tree growth...");
        // Wait for initial delay
        yield return new WaitForSeconds(growthDelay);

        // Spawn the tree if it doesn't exist
        if (treeOfLight == null && treePrefab != null)
        {
            Debug.Log("Spawning tree prefab...");
            // Spawn at tree spawn point and parent it
            GameObject treeObject = Instantiate(treePrefab);
            
            // First parent to the pot (this object) to ensure proper component finding
            treeObject.transform.SetParent(transform);
            treeObject.transform.localPosition = treeSpawnPoint.localPosition;
            treeObject.transform.localRotation = Quaternion.identity;
            treeObject.transform.localScale = Vector3.zero;

            // Get the TreeOfLight component and start growth
            treeOfLight = treeObject.GetComponent<TreeOfLight>();
            if (treeOfLight != null)
            {
                // Set the parent pot reference directly
                treeOfLight.SetParentPot(this);
                treeOfLight.SetGrowthSpeed(1f / growthDuration);
                treeOfLight.StartGrowth(growthDuration, maxTreeScale);
            }
            else
            {
                Debug.LogError("TreeOfLight component not found on tree prefab!");
                yield break;
            }
        }

        // Wait for the tree to complete its growth
        yield return new WaitForSeconds(growthDuration);
        
        // Start spawning monsters when fully grown
        if (monsterSpawner != null)
        {
            monsterSpawner.StartSpawning();
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
            breakEffect.Play();
            StartCoroutine(HandleBreakSequence(breakEffect.main.duration));
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
            Debug.Log($"Waiting {delay} seconds for break effect to complete");
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
    
    public void OnTreeFinalEffectComplete()
    {
        // This method is called when the tree's final effect is complete
        // You can add any additional logic here if needed
    }
} 
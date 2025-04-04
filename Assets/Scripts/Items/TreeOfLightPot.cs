using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using System.Collections;
using Core;
using Items;

public class TreeOfLightPot : MonoBehaviour
{
    private static readonly int StartGrowth = Animator.StringToHash("StartGrowth");

    // Add these methods
    public void UpdateTreeGrowthProgress(float progress) { /* Implementation */ }
    public void OnTreeGrowthStarted() { /* Implementation */ }
    public void OnTreeGrowthPaused() { /* Implementation */ }
    public void OnTreeGrowthResumed() { /* Implementation */ }
    public void OnTreeGrowthCompleted() { /* Implementation */ }
    public void PrepareForDestruction() { /* Implementation */ }

    // These seem to be already implemented
    public void OnTreeDamaged(float damage) { /* Implementation */ }
    public void OnTreeDestroyed() { /* Implementation */ }

    [Header("References")]
    [SerializeField] private GameObject treeModel;
    [SerializeField] private ParticleSystem growthParticles;
    [SerializeField] private Transform treeSpawnPoint;
    public Transform TreeSpawnPoint => treeSpawnPoint;
    [SerializeField] private Renderer potRenderer;
    [SerializeField] private AudioSource growthAudioSource;

    [Header("Growth Settings")]
    [SerializeField] private GameObject treeOfLightPrefab;
    [SerializeField] private float treeGrowthDuration = 5f;
    [SerializeField] private float seedShrinkDuration = 2f;
    [SerializeField] private string[] validSeedNames = { "Magical_Seed", "seed_of_light" };
    //[SerializeField] private string magicalSeedTag = "MagicalSeed";

    [Header("Physics Settings")]
    [SerializeField] private float baseMass = 5f; // Make it heavy at the bottom
    [SerializeField] private float centerOfMassYOffset = -0.2f; // Lower center of mass

    [Header("Health Settings")]
    [SerializeField] private HealthComponent healthComponent;
    [SerializeField] private float maxHealth = 100f;
    //[SerializeField] private GameObject brokenPotPrefab;
    //[SerializeField] private GameObject brokenTreePrefab;

    [Header("Visual Feedback")]
    [SerializeField] private Material potActiveMaterial;
    [SerializeField] private ParticleSystem plantingEffect;

    [Header("Events")]
    [SerializeField] private UnityEngine.Events.UnityEvent onTreeFullyGrown;
    [SerializeField] public UnityEngine.Events.UnityEvent onSeedPlaced;

    [SerializeField] private UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor socketInteractor;

    //[SerializeField] private float potMaxHealth = 100f;
    //private HealthComponent healthComponent;
    private TreeOfLight plantedTree;
    private bool seedPlanted;

    // This method will be called when a seed is planted
    public void PlantSeed(float newMaxHealth, TreeOfLight tree)
    {
        if (newMaxHealth > 0)
        {
            this.maxHealth = newMaxHealth;
        }
        if (seedPlanted)
            return;

        seedPlanted = true;
        plantedTree = tree;

        // Link the tree back to this pot
        tree.SetParentPot(this);

        // Only add a health component when the seed is planted
        if (healthComponent == null)
        {
            healthComponent = gameObject.AddComponent<HealthComponent>();
        }

        // Configure the health component
        if (healthComponent != null)
        {
            // Set the health using the existing methods from your HealthComponent
            healthComponent.SetMaxHealth(maxHealth);
            healthComponent.ResetHealth(); // Assuming this method exists to set current health to max

            // Subscribe to the health component's events using the pattern from your code
            healthComponent.OnHealthChanged += OnPotHealthChanged;
            healthComponent.OnDeath += OnPotHealthDepleted;
        }
    }

    // This method will be called when the pot's health changes
    private void OnPotHealthChanged(float currentHealth, float newMaxHealth)
    {
        // If the tree exists, update its health to match the pot's health
        if (plantedTree != null)
        {
            HealthComponent treeHealth = plantedTree.GetComponent<HealthComponent>();
            if (treeHealth != null)
            {
                // Use existing methods - disable event handlers temporarily to prevent feedback loops
                treeHealth.OnHealthChanged -= HandleTreeHealthChanged;

                // Set the health directly - adjust based on your actual API
                float healthPercentage = currentHealth / maxHealth;
                // Calculate target health for the tree
                float targetTreeHealth = healthPercentage * treeHealth.MaxHealth;
                float currentTreeHealth = treeHealth.Health;

                // Apply damage or healing as needed
                if (targetTreeHealth < currentTreeHealth)
                {
                    float damage = currentTreeHealth - targetTreeHealth;
                    treeHealth.TakeDamage(damage);
                }
                else if (targetTreeHealth > currentTreeHealth)
                {
                    float healing = targetTreeHealth - currentTreeHealth;
                    treeHealth.Heal(healing);
                }

                // Re-enable event handlers
                treeHealth.OnHealthChanged += HandleTreeHealthChanged;
            }
        }
    }

    // This method will be called when the pot's health is depleted
    private void OnPotHealthDepleted()
    {
        // Handle pot destruction here
        if (plantedTree != null)
        {
            // Call a public method on the tree instead of the private OnHealthDepleted
            plantedTree.Break(); // This is a public method in your TreeOfLight class
        }

        // Use your existing Break method
        Break();
    }

    // Method to directly access the health component (if needed by external scripts)
    public HealthComponent GetHealthComponent()
    {
        return healthComponent;
    }

    // Method to check if a seed has been planted
    public bool HasSeed()
    {
        return seedPlanted;
    }

    // Expose a method to take damage, which will only work if the seed is planted
    public void TakeDamage(float damage)
    {
        if (seedPlanted && healthComponent != null)
        {
            healthComponent.TakeDamage(damage);
        }
        // If no seed is planted or no health component, the pot is invulnerable
        // so we just ignore the damage
    }

    // State tracking
    private bool seedPlaced;
    private bool isGrowing;
    private HealthComponent treeHealthComponent;
    private bool hasMagicalSeed;
    private GameObject spawnedTree = null;
    private Animator potAnimator;

    public void Break()
        {
            // Replace pot and tree with broken versions
            //if (brokenPotPrefab != null)
                //Instantiate(brokenPotPrefab, transform.position, transform.rotation);
    
            //if (brokenTreePrefab != null && treeModel != null && treeModel.activeInHierarchy)
                //Instantiate(brokenTreePrefab, treeModel.transform.position, treeModel.transform.rotation);
                
            Destroy(gameObject);
        }

    public void OnTreeBroken()
    {
        // Route to existing functionality
        OnTreeDestroyed();
    }



    private void Awake()
    {
        potAnimator = GetComponent<Animator>();

        // Get the socket interactor directly
        socketInteractor = GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor>();
        if (socketInteractor == null)
        {
            Debug.LogError("TreeOfLightPot: No XR Socket Interactor found on this object or its children!");
        }

        // Find or set tree spawn point
        if (!treeSpawnPoint)
        {
            treeSpawnPoint = transform.Find("TreeSpawnPoint");
            if (!treeSpawnPoint)
            {
                Debug.LogWarning("TreeOfLightPot: No TreeSpawnPoint found, using pot's position");
                treeSpawnPoint = transform;
            }
        }

        // Ensure tree is initially hidden
        if (treeModel != null)
            treeModel.SetActive(false);
        else
            Debug.LogWarning("TreeOfLightPot: No tree model assigned!");

        // Setup health system
        SetupHealthSystem();

        Debug.Log("TreeOfLightPot initialized");
    }

    private void Start()
    {
        healthComponent = GetComponent<HealthComponent>();
        if (healthComponent == null)
        {
            healthComponent = gameObject.AddComponent<HealthComponent>();
            Debug.LogWarning("HealthComponent was not found. Adding one");
        }
        // Lower the center of mass to make it harder to tip over
        if (TryGetComponent(out Rigidbody rb))
        {
            rb.mass = baseMass;
            rb.centerOfMass = new Vector3(0, centerOfMassYOffset, 0);
        }
    }

    private void OnEnable()
    {
        if (socketInteractor != null)
        {
            // Register for socket events
            socketInteractor.selectEntered.AddListener(OnSeedPlaced);
            Debug.Log("TreeOfLightPot: Socket event listeners registered");
        }

        // Subscribe to health events if component exists
        if (healthComponent != null)
        {
            healthComponent.OnDeath += OnHealthDepleted;
        }
    }

    private void OnDisable()
    {
        if (socketInteractor != null)
        {
            socketInteractor.selectEntered.RemoveListener(OnSeedPlaced);
        }

        // Unsubscribe from health events if component exists
        if (healthComponent != null)
        {
            healthComponent.OnDeath -= OnHealthDepleted;
        }
    }

    private void SetupHealthSystem()
    {
        if (healthComponent == null)
            healthComponent = GetComponent<HealthComponent>();

        if (healthComponent == null)
            healthComponent = gameObject.AddComponent<HealthComponent>();

        // Initialize health if it's a new component
        if (healthComponent != null)
        {
            healthComponent.SetMaxHealth(maxHealth);
        }
    }

    private void OnHealthDepleted()
        {
            // Handle pot destruction here
            if (plantedTree != null)
            {
               // _plantedTree.Break(); // Tree call pot's Break
            }
    
            //Spawn Key
            SpawnKey();
            // Use your existing Break method
            Break();
        }
    
        private void SpawnKey()
        {
            if (plantedTree != null && plantedTree.keyPrefab != null)
            {
                Vector3 spawnPosition = transform.position + Vector3.up * 1.5f;
                GameObject key = Instantiate(plantedTree.keyPrefab, spawnPosition, Quaternion.identity);
    
                // Apply small upward force if rigidbody exists
                Rigidbody keyRb = key.GetComponent<Rigidbody>();
                if (keyRb != null)
                {
                    keyRb.AddForce(Vector3.up * 2f, ForceMode.Impulse);
                }
            }
        }
    
    private void LinkTreeHealth()
    {
        if (spawnedTree != null)
        {
            treeHealthComponent = spawnedTree.GetComponent<HealthComponent>();
            if (treeHealthComponent == null)
                treeHealthComponent = spawnedTree.AddComponent<HealthComponent>();
        }
        else if (treeModel != null)
        {
            treeHealthComponent = treeModel.GetComponent<HealthComponent>();
            if (treeHealthComponent == null)
                treeHealthComponent = treeModel.AddComponent<HealthComponent>();
        }
        else
        {
            Debug.LogWarning("TreeOfLightPot: No tree object available to link health");
            return;
        }

        // We'll use the damage relay system to connect the tree and pot health
        treeHealthComponent.SetMaxHealth(maxHealth);
        treeHealthComponent.OnHealthChanged += HandleTreeHealthChanged;
        healthComponent.OnHealthChanged += HandlePotHealthChanged;
    }

    private void HandleTreeHealthChanged(float currentHealth, float newMaxHealth)
    {
        // Skip if the tree health component doesn't exist yet
        if (treeHealthComponent == null) return;

        // Unsubscribe to prevent infinite loop of events
        healthComponent.OnHealthChanged -= HandlePotHealthChanged;

        // Calculate damage and apply it to the pot
        float damage = treeHealthComponent.GetHealthPercentage() - healthComponent.GetHealthPercentage();
        if (damage < 0)
        {
            damage = -damage * maxHealth;  // Convert percentage back to absolute value
            healthComponent.TakeDamage(damage);
        }
        else if (damage > 0)
        {
            damage = damage * maxHealth;  // Convert percentage back to absolute value
            healthComponent.Heal(damage);
        }

        // Re-subscribe
        healthComponent.OnHealthChanged += HandlePotHealthChanged;
    }

    private void HandlePotHealthChanged(float currentHealth, float newMaxHealth)
    {
        // Skip if the tree health component doesn't exist yet
        if (treeHealthComponent == null) return;

        // Unsubscribe to prevent infinite loop of events
        treeHealthComponent.OnHealthChanged -= HandleTreeHealthChanged;

        // Calculate damage and apply it to the tree
        float damage = healthComponent.GetHealthPercentage() - treeHealthComponent.GetHealthPercentage();
        if (damage < 0)
        {
            damage = -damage * maxHealth;  // Convert percentage back to absolute value
            treeHealthComponent.TakeDamage(damage);
        }
        else if (damage > 0)
        {
            damage = damage * maxHealth;  // Convert percentage back to absolute value
            treeHealthComponent.Heal(damage);
        }

        // Re-subscribe
        treeHealthComponent.OnHealthChanged += HandleTreeHealthChanged;
    }

    private void ActivatePot()
    {
        if (potRenderer != null && potActiveMaterial != null)
        {
            potRenderer.material = potActiveMaterial;
        }

        if (growthAudioSource != null)
        {
            growthAudioSource.Play();
        }
    }

    private void OnSeedPlaced(SelectEnterEventArgs args)
    {
        if (seedPlaced) return; // Prevent multiple activations

        // Added: Safety check to prevent null reference exception
        if (args == null || args.interactableObject == null || args.interactableObject.transform == null)
        {
            Debug.LogError("TreeOfLightPot: Invalid seed object in OnSeedPlaced");
            return;
        }

        GameObject seed = args.interactableObject.transform.gameObject;

        // Check if this is a valid seed
        if (!IsValidSeed(seed))
        {
            Debug.Log("TreeOfLightPot: Invalid seed type, ignoring.");
            return;
        }

        seedPlaced = true;
        hasMagicalSeed = true;

        // Invoke seed placed event
        onSeedPlaced?.Invoke();

        // Disable the socket interactor so nothing else can be placed
        if (socketInteractor != null)
        {
            socketInteractor.enabled = false;
        }

        // Notify the seed that it's been planted
        MagicalSeed seedComponent = seed.GetComponent<MagicalSeed>();
        if (seedComponent != null)
        {
            seedComponent.OnPlantedInPot();
        }

        // Activate pot visual and audio feedback
        ActivatePot();

        // Start growth sequence
        StartCoroutine(GrowthSequence(seed));
    }

    /// <summary>
    /// Checks if the provided item is a valid magical seed.
    /// </summary>
    private bool IsValidSeed(GameObject item)
    {
        if (item.CompareTag("MagicalSeed")) return true;

        foreach (string validName in validSeedNames)
        {
            if (item.name.Contains(validName))
            {
                return true;
            }
        }

        return false;
    }

    private IEnumerator GrowthSequence(GameObject seed)
    {
        if (isGrowing)
        {
            Debug.Log("TreeOfLightPot: Growth already in progress, ignoring");
            yield break;
        }

        // Added: Check for null seed
        if (seed == null)
        {
            Debug.LogError("TreeOfLightPot: Null seed passed to GrowthSequence");
            yield break;
        }

        isGrowing = true;
        Debug.Log("TreeOfLightPot: Starting growth sequence");

        // Play planting effect if available
        if (plantingEffect != null)
        {
            plantingEffect.Play();
        }

        // Step 1: Shrink seed
        Vector3 originalScale = seed.transform.localScale;
        Vector3 originalPosition = seed.transform.position;

        // Shrink the seed over time and move it to the tree spawn point
        float elapsed = 0f;
        while (elapsed < seedShrinkDuration)
        {
            // Added: Safety check in case seed is destroyed during animation
            if (seed == null) break;

            float t = elapsed / seedShrinkDuration;
            seed.transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, t);
            seed.transform.position = Vector3.Lerp(originalPosition, treeSpawnPoint.position, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Completely hide the seed (with null check)
        if (seed != null)
        {
            seed.transform.localScale = Vector3.zero;
            seed.transform.position = treeSpawnPoint.position;

            // Option to destroy seed after it's been "planted"
            // Destroying instead of just hiding
            Destroy(seed);
        }

        // Step 2: Play growth particles
        if (growthParticles != null)
        {
            growthParticles.transform.position = treeSpawnPoint.position;
            growthParticles.Play();
            Debug.Log("TreeOfLightPot: Playing growth particles");
        }

        // Step 3: Grow the tree - determine which method to use
        if (treeOfLightPrefab != null)
        {
            // Use the prefab spawning method
            GrowTreeFromPrefab();
        }
        else if (treeModel != null)
        {
            // Use the built-in tree model
            yield return GrowBuiltInTree();
        }

        // Invoke the tree fully grown event
        onTreeFullyGrown?.Invoke();

        isGrowing = false;
    }

    private IEnumerator GrowBuiltInTree()
    {
        // Position the tree at the spawn point
        treeModel.transform.position = treeSpawnPoint.position;
        treeModel.transform.rotation = treeSpawnPoint.rotation;

        // Make tree visible but start at zero scale
        treeModel.SetActive(true);
        treeModel.transform.localScale = Vector3.zero;

        // Link tree health with pot health
        LinkTreeHealth();

        // Grow the tree over time
        float elapsed = 0f;
        while (elapsed < treeGrowthDuration)
        {
            float t = elapsed / treeGrowthDuration;
            treeModel.transform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, t);
            elapsed += Time.deltaTime;
            yield return null; // This was yield break before which was a bug!
        }

        // Ensure final scale is exactly Vector3.one
        treeModel.transform.localScale = Vector3.one;
        Debug.Log("TreeOfLightPot: Tree fully grown");
    }

    private void GrowTreeFromPrefab()
    {
        if (treeSpawnPoint != null && treeOfLightPrefab != null)
        {
            // Create only one instance of the tree
            GameObject spawnedTreeObject = Instantiate(treeOfLightPrefab, treeSpawnPoint.position, treeSpawnPoint.rotation);

            if (spawnedTreeObject == null) // Check if Instantiate failed
            {
                Debug.LogError("TreeOfLightPot: Failed to instantiate tree prefab");
                return; // Exit the function if instantiation failed
            }

            TreeOfLight treeOfLight = spawnedTreeObject.GetComponent<TreeOfLight>(); // Get the TreeOfLight component

            if (treeOfLight == null) // Check if GetComponent failed
            {
                Debug.LogError("TreeOfLightPot: TreeOfLight component not found on prefab");
                Destroy(spawnedTreeObject); // Clean up the instantiated object
                return; // Exit the function if no TreeOfLight component
            }

            spawnedTreeObject.transform.SetParent(transform);

            // Activate the tree GameObject
            spawnedTreeObject.gameObject.SetActive(true);

            treeOfLight.StartGrowing(treeGrowthDuration, OnTreeGrowthComplete);
            
            treeOfLight.SetParentPot(this);

            // Link tree health with pot health - do this after spawning the tree
            LinkTreeHealth();
            
            // Start pot's growth animation or effects
            //StartPotGrowth();
        }
        else
        {
            Debug.LogError("TreeOfLightPot: Tree spawn point or tree prefab not assigned");
        }
    }

    //private void StartPotGrowth()
    //{
        //if (potAnimator != null)
        //{
        //    potAnimator.SetTrigger(StartGrowth);
        //}

        // Already handled in ActivatePot(), but could add additional effects here
    //}

    private void OnTreeGrowthComplete()
    {
        Debug.Log("TreeOfLightPot: Tree growth complete callback received");
        // Any additional actions when tree growth is complete
        SpawnKey();

	    // BREAK THE TREE AND POT
        if (plantedTree != null)
        {
            //Break the tree, this calls the OnPotHealthDepleted function to execute the Break function
            plantedTree.GetComponent<HealthComponent>().Health = 0;
        }
    }

    /// <summary>
    /// Alternative method for placing items manually (without XR interaction)
    /// </summary>
    public void OnItemPlaced(GameObject item)
    {
        if (seedPlaced || isGrowing) return;

        // Check if the placed item is the magical seed
        if (IsValidSeed(item))
        {
            seedPlaced = true;
            hasMagicalSeed = true;

            // Invoke seed placed event
            onSeedPlaced?.Invoke();

            // Activate pot visual and audio feedback
            ActivatePot();

            // Start growth sequence
            StartCoroutine(GrowthSequence(item));
        }
    }

    /// <summary>
    /// Public method to manually trigger tree growth (for debugging or special game events).
    /// </summary>
    public void ForceGrowth()
    {
        if (!seedPlaced && !isGrowing)
        {
            seedPlaced = true;
            hasMagicalSeed = true;

            // Create an empty GameObject to represent the seed
            GameObject dummySeed = new GameObject("DummySeed");
            dummySeed.transform.position = transform.position;

            // Activate pot visual and audio feedback
            ActivatePot();

            // Start growth sequence with the dummy seed
            StartCoroutine(GrowthSequence(dummySeed));
        }
    }
}
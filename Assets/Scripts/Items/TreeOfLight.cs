using UnityEngine;
using System.Collections;
using UnityEngine.Events;
using Core;

public class TreeOfLight : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;
    
    [Header("Growth Settings")]
    [SerializeField] private float growthDuration = 5f;
    [SerializeField] private float growthDelay = 0.5f;
    
    [Header("Light Settings")]
    [SerializeField] private float initialLightIntensity = 0.1f;
    [SerializeField] private float growingLightIntensity = 0.3f;
    [SerializeField] private float finalLightIntensity = 2f;
    [SerializeField] private float finalLightPulseSpeed = 1f;
    [SerializeField] private float finalLightPulseAmount = 0.2f;
    
    [Header("Final Blinking Effect")]
    [SerializeField] private float blinkDuration = 3f;
    [SerializeField] private float blinkSpeed = 10f;
    [SerializeField] private float blinkIntensityMin = 0.1f;
    [SerializeField] private float blinkIntensityMax = 4f;
    
    [Header("Events")]
    public UnityEvent onDamage;
    public UnityEvent onBreak;
    
    [Header("Break Settings")]
    [SerializeField] private float breakDelay = 1f;
    
    private float growthTimer = 0f;
    private bool isGrowing = false;
    private bool isBroken = false;
    private bool isFullyGrown = false;
    private Animator animator;
    private Light treeLight;
    private ShadowMonsterSpawner monsterSpawner;
    private static readonly int GrowTrigger = Animator.StringToHash("Grow");
    private static readonly int GrowthSpeed = Animator.StringToHash("GrowthSpeed");

    private void Awake()
    {
        // Get the animator component
        animator = GetComponent<Animator>();
        // Get the light component
        treeLight = GetComponentInChildren<Light>();
        // Get the monster spawner
        monsterSpawner = FindObjectOfType<ShadowMonsterSpawner>();

        // Initialize health
        currentHealth = maxHealth;

        // Setup health bar
        BreakableHealthBar healthBar = GetComponentInChildren<BreakableHealthBar>();
        if (healthBar != null)
        {
            // Make sure the health bar is properly positioned and scaled
            RectTransform rectTransform = healthBar.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                // Position above the tree
                rectTransform.localPosition = new Vector3(0, 1.5f, 0);
                // Make it smaller (1/400th of original size)
                rectTransform.localScale = new Vector3(0.0025f, 0.0025f, 0.0025f);
                // Ensure it's facing forward
                rectTransform.localRotation = Quaternion.identity;
                
                // Make sure the Canvas is set to World Space
                Canvas canvas = healthBar.GetComponent<Canvas>();
                if (canvas != null)
                {
                    canvas.renderMode = RenderMode.WorldSpace;
                    canvas.worldCamera = Camera.main;
                }
            }

            // Initialize the health bar (it will get health values through reflection)
            healthBar.Initialize(this);
            Debug.Log($"Health bar initialized with max health: {maxHealth}, current health: {currentHealth}");
        }
        else
        {
            Debug.LogWarning("No BreakableHealthBar found in Tree_of_Light! Please add the BreakableHealthBar prefab as a child.");
        }
    }

    private void Start()
    {
        // Make sure health is set to max at start
        currentHealth = maxHealth;
        
        // Initialize light
        if (treeLight != null)
        {
            treeLight.intensity = initialLightIntensity;
        }
        
        // Start growing
        StartCoroutine(Grow());
    }

    private void Update()
    {
        // Handle final light pulsing when fully grown
        if (isFullyGrown && treeLight != null)
        {
            float pulse = Mathf.Sin(Time.time * finalLightPulseSpeed) * finalLightPulseAmount;
            treeLight.intensity = finalLightIntensity + pulse;
        }
    }

    public void TakeDamage(float damage)
    {
        if (isBroken) return;
        
        currentHealth = Mathf.Max(0, currentHealth - damage);
        onDamage?.Invoke();
        
        Debug.Log($"Tree took {damage} damage. Current health: {currentHealth}/{maxHealth}");
        
        if (currentHealth <= 0)
        {
            Break();
        }
    }

    public void Break()
    {
        if (isBroken) return;
        isBroken = true;

        // Find the parent pot
        TreeOfLightPot parentPot = GetComponentInParent<TreeOfLightPot>();
        if (parentPot != null)
        {
            // If we're fully grown, tell the pot we're breaking
            if (isFullyGrown)
            {
                parentPot.Break();
            }
            else
            {
                // If not fully grown, just break the pot
                parentPot.Break();
            }
        }

        // Disable the tree
        gameObject.SetActive(false);
    }

    public void SetFullyGrown(bool value)
    {
        isFullyGrown = value;
    }

    public float GetHealthPercentage()
    {
        return currentHealth / maxHealth;
    }

    public float GetCurrentHealth()
    {
        return currentHealth;
    }

    public float GetMaxHealth()
    {
        return maxHealth;
    }

    private IEnumerator Grow()
    {
        yield return new WaitForSeconds(growthDelay);

        isGrowing = true;
        float startTime = Time.time;

        while (growthTimer < growthDuration)
        {
            float normalizedTime = growthTimer / growthDuration;
            
            // Update animator
            animator.SetFloat(GrowthSpeed, normalizedTime);
            
            // Update light intensity during growth
            if (treeLight != null)
            {
                treeLight.intensity = Mathf.Lerp(initialLightIntensity, growingLightIntensity, normalizedTime);
            }

            growthTimer += Time.deltaTime;
            yield return null;
        }

        // Growth complete
        isGrowing = false;
        isFullyGrown = true;

        // Trigger final blinking effect
        StartCoroutine(FinalBlinkEffect());
    }

    private IEnumerator FinalBlinkEffect()
    {
        float blinkTimer = 0f;
        float baseIntensity = finalLightIntensity;

        while (blinkTimer < blinkDuration)
        {
            // Calculate blink intensity
            float blinkProgress = blinkTimer / blinkDuration;
            float blinkIntensity = Mathf.Lerp(blinkIntensityMin, blinkIntensityMax, 
                Mathf.PingPong(Time.time * blinkSpeed, 1f));
            
            // Apply increasing frequency as we progress
            float frequency = Mathf.Lerp(2f, 10f, blinkProgress);
            float pulseValue = Mathf.Sin(Time.time * frequency) * 0.5f + 0.5f;
            
            // Set light intensity
            if (treeLight != null)
            {
                treeLight.intensity = Mathf.Lerp(blinkIntensityMin, blinkIntensity, pulseValue);
            }

            blinkTimer += Time.deltaTime;
            yield return null;
        }

        // Final flash
        if (treeLight != null)
        {
            treeLight.intensity = blinkIntensityMax;
        }

        yield return new WaitForSeconds(0.1f);

        // Break both tree and pot
        Break();
    }
} 
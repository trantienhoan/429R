using System;
using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using Items;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class TreeOfLight : MonoBehaviour
{
    [Header("Light Settings")]
    [SerializeField] private Light treeLight;
    [SerializeField] private float maxLightIntensity = 2f;
    [SerializeField] private float initialLightIntensity = 0f;
    [SerializeField] private float growingLightIntensity = 1f;
    [SerializeField] private float finalLightPulseSpeed = 1f;
    [SerializeField] private float finalLightPulseAmount = 0.2f;
    
    [Header("Light Wave Effect")]
    [SerializeField] private GameObject lightWavePrefab;
    [SerializeField] private float lightWaveRadius = 30f;
    [SerializeField] private float lightWaveSpeed = 10f;
    [SerializeField] private float lightWaveDuration = 3f;
    [SerializeField] private Color lightWaveColor = Color.white;
    
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth = 100f;
    
    [Header("Growth Settings")]
    [SerializeField] private Vector3 startScale = Vector3.zero;
    [SerializeField] private Vector3 targetScale = Vector3.one;
    [SerializeField] private AnimationCurve growthCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Break Effects")]
    [SerializeField] private ParticleSystem breakEffect;
    [SerializeField] private float breakEffectDuration = 1f;
    [SerializeField] private float breakDelay = 2f;
    
    [Header("Events")]
    public UnityEvent onBreak;
    public UnityEvent onDamage;
    public UnityEvent OnGrowthComplete;
    public UnityEvent OnLightWaveCreated;
    
    
    private bool isFullyGrown = false;
    private bool isGrowing = false;
    private bool isPaused = false;
    private float growthDuration;
    private float elapsedGrowthTime = 0f;
    private float animationSpeed = 1f;
    private Coroutine growthRoutine;
    private Coroutine finalEffectRoutine;
    
    private Animator animator;
    private static readonly int GrowTrigger = Animator.StringToHash("Grow");
    private static readonly int GrowthSpeed = Animator.StringToHash("GrowthSpeed");
    
    private XRGrabInteractable grabInteractable;
    private TreeOfLightPot parentPot;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        grabInteractable = GetComponent<XRGrabInteractable>();
        
        if (treeLight != null)
        {
            treeLight.intensity = initialLightIntensity;
        }
        
        currentHealth = maxHealth;
        transform.localScale = startScale;
    }

    public void SetParentPot(TreeOfLightPot pot)
    {
        parentPot = pot;
        Debug.Log($"Parent pot reference set: {(parentPot != null ? "success" : "failed")}");
    }

    public void SetGrowthSpeed(float speed)
    {
        if (speed <= 0)
        {
            Debug.LogWarning("Attempted to set growth speed to zero or negative value");
            speed = 0.01f; // Set to minimal value instead
        }
        
        animationSpeed = speed;
        if (animator != null)
        {
            animator.SetFloat(GrowthSpeed, speed);
        }
    }

    public void StartGrowth(float duration, float targetScale)
    {
        Debug.Log($"TreeOfLight: Starting growth with duration: {duration}, targetScale: {targetScale}");

        // Validate parameters
        if (duration <= 0)
        {
            Debug.LogWarning("Invalid growth duration, using default value of 1");
            duration = 1f;
        }

        if (targetScale <= 0)
        {
            Debug.LogWarning("Invalid target scale, using default value of 1");
            targetScale = 1f;
        }

        growthDuration = duration;
        this.targetScale = new Vector3(targetScale, targetScale, targetScale);
        
        // Ensure we have valid components
        if (animator == null)
        {
            Debug.LogWarning("Animator component missing!");
        }
        else
        {
            animator.SetTrigger(GrowTrigger);
            animator.SetFloat(GrowthSpeed, animationSpeed);
        }

        // Set growth state
        isGrowing = true;
        isPaused = false;
        elapsedGrowthTime = 0f;
        
        // Start growth animation
        if (grabInteractable != null)
        {
            grabInteractable.enabled = false; // Disable grabbing while growing
        }
    }
    
    public void PauseGrowth()
    {
        if (isGrowing && !isPaused)
        {
            isPaused = true;
            
            // Pause animation
            if (animator != null)
            {
                animator.speed = 0;
            }
            
            Debug.Log("Tree growth paused");
        }
    }
    
    public void ResumeGrowth()
    {
        if (isGrowing && isPaused)
        {
            isPaused = false;
            
            // Resume animation
            if (animator != null)
            {
                animator.speed = 1;
            }
            
            Debug.Log("Tree growth resumed");
        }
    }
    
    public void UpdateGrowthProgress(float progress)
    {
        if (!isPaused && isGrowing)
        {
            // Scale the tree based on progress
            transform.localScale = Vector3.Lerp(startScale, targetScale, growthCurve.Evaluate(progress));
            
            // Update light intensity
            if (treeLight != null)
            {
                treeLight.intensity = Mathf.Lerp(initialLightIntensity, growingLightIntensity, progress);
            }
            
            // Update elapsed time
            elapsedGrowthTime = progress * growthDuration;
        }
    }
    public float GetHealthPercentage()
    {
        // Example implementation - adjust based on your actual health tracking
        return currentHealth / maxHealth;
    }

    public void CompleteGrowth()
    {
        if (isGrowing)
        {
            isGrowing = false;
            isPaused = false;
            isFullyGrown = true;
            
            // Set to final scale
            transform.localScale = targetScale;
            
            // Update light
            if (treeLight != null)
            {
                treeLight.intensity = maxLightIntensity;
                
                // Start pulsing effect
                if (finalEffectRoutine != null)
                {
                    StopCoroutine(finalEffectRoutine);
                }
                finalEffectRoutine = StartCoroutine(PulseLight());
            }
            
            // Create light wave that kills all monsters
            StartCoroutine(CreateLightWaveEffect());
            
            // Trigger completion event
            OnGrowthComplete?.Invoke();
        }
    }
    
    private IEnumerator CreateLightWaveEffect()
    {
        Debug.Log("Creating light wave effect to destroy monsters");
        
        // Create wave effect if we have a prefab
        if (lightWavePrefab != null)
        {
            GameObject lightWave = Instantiate(lightWavePrefab, transform.position, Quaternion.identity);
            LightWaveEffect waveEffect = lightWave.GetComponent<LightWaveEffect>();
            
            if (waveEffect != null)
            {
                // Configure the wave effect
                waveEffect.SetParameters(lightWaveRadius, lightWaveSpeed, lightWaveDuration, lightWaveColor);
                waveEffect.StartWave();
            }
            else
            {
                Debug.LogWarning("LightWaveEffect component not found on lightWavePrefab");
                
                // Create a simple expanding sphere effect as fallback
                StartCoroutine(SimpleExpandingSphereEffect());
            }
        }
        else
        {
            Debug.LogWarning("Light wave prefab not assigned, using simple effect");
            // Create a simple expanding sphere effect as fallback
            StartCoroutine(SimpleExpandingSphereEffect());
        }
        
        // Kill all monsters in the scene
        yield return new WaitForSeconds(0.5f); // Wait a moment for the wave to expand
        
        ShadowMonster[] monsters = FindObjectsOfType<ShadowMonster>();
        foreach (ShadowMonster monster in monsters)
        {
            monster.TakeDamage(float.MaxValue); // Kill instantly
        }
        
        OnLightWaveCreated?.Invoke();
    }
    
    private IEnumerator SimpleExpandingSphereEffect()
    {
        // Create a simple sphere to represent the light wave
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.position = transform.position;
        sphere.transform.localScale = Vector3.one * 0.1f;
        
        // Create light material
        Material waveMaterial = new Material(Shader.Find("Standard"));
        waveMaterial.color = lightWaveColor;
        waveMaterial.EnableKeyword("_EMISSION");
        waveMaterial.SetColor("_EmissionColor", lightWaveColor * 2f);
        
        // Apply material
        MeshRenderer renderer = sphere.GetComponent<MeshRenderer>();
        renderer.material = waveMaterial;
        
        // Make the sphere not collide with anything
        Collider collider = sphere.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }
        
        // Expand the sphere
        float elapsed = 0f;
        while (elapsed < lightWaveDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / lightWaveDuration;
            float currentRadius = progress * lightWaveRadius;
            
            sphere.transform.localScale = Vector3.one * currentRadius;
            
            // Fade out as wave expands
            Color fadeColor = lightWaveColor;
            fadeColor.a = 1f - progress;
            waveMaterial.color = fadeColor;
            
            yield return null;
        }
        
        // Destroy the sphere when done
        Destroy(sphere);
    }
    
    private IEnumerator PulseLight()
    {
        while (isFullyGrown)
        {
            // Pulse light up and down
            float pulse = Mathf.Sin(Time.time * finalLightPulseSpeed) * finalLightPulseAmount;
            if (treeLight != null)
            {
                treeLight.intensity = maxLightIntensity + pulse;
            }
            yield return null;
        }
    }

    public void TakeDamage(float amount)
    {
        currentHealth -= amount;
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            // Handle death/destruction
        }
    }
    
    private IEnumerator BreakTree()
    {
        // Wait for break delay
        yield return new WaitForSeconds(breakDelay);
        
        // Play break effects
        if (breakEffect != null)
        {
            ParticleSystem effect = Instantiate(breakEffect, transform.position, Quaternion.identity);
            Destroy(effect.gameObject, breakEffectDuration);
        }
        
        // Trigger break event
        onBreak?.Invoke();
        
        // Destroy tree
        Destroy(gameObject);
    }

    private bool IsValidScale(Vector3 scale)
    {
        return !float.IsNaN(scale.x) && !float.IsNaN(scale.y) && !float.IsNaN(scale.z) &&
               !float.IsInfinity(scale.x) && !float.IsInfinity(scale.y) && !float.IsInfinity(scale.z) &&
               scale.x > 0 && scale.y > 0 && scale.z > 0;
    }
}
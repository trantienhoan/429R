using UnityEngine;
using System.Collections;
using UnityEngine.Events;
using Core;


public class TreeOfLight : MonoBehaviour
{
    [Header("Light Settings")]
    [SerializeField] private Light treeLight;
    [SerializeField] private float maxLightIntensity = 2f;
    [SerializeField] private float initialLightIntensity = 0f;
    [SerializeField] private float growingLightIntensity = 1f;
    [SerializeField] private float finalLightPulseSpeed = 1f;
    [SerializeField] private float finalLightPulseAmount = 0.2f;
    
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    
    [Header("Growth Settings")]
    [SerializeField] private Vector3 startScale = Vector3.zero;
    [SerializeField] private Vector3 targetScale = Vector3.one;
    
    [Header("Break Effects")]
    [SerializeField] private ParticleSystem breakEffect;
    [SerializeField] private float breakEffectDuration = 1f;
    
    [Header("Events")]
    public UnityEvent onBreak;
    public UnityEvent onDamage;
    public UnityEvent OnGrowthComplete;
    
    private float currentScale = 0f;
    private float currentLightIntensity = 0f;
    private float currentHealth;
    private bool isFullyGrown = false;
    private bool isGrowing = false;
    private float growthDuration; // Set by TreeOfLightPot
    private float maxScale; // Set by TreeOfLightPot
    private float elapsedGrowthTime = 0f; // Track how long we've been growing
    private float animationSpeed = 1f; // Store the animation speed
    
    private Animator animator;
    private static readonly int GrowTrigger = Animator.StringToHash("Grow");
    private static readonly int GrowthSpeed = Animator.StringToHash("GrowthSpeed");
    
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;
    private TreeOfLightPot parentPot;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        
        if (treeLight != null)
        {
            treeLight.intensity = 0f;
        }
        
        currentHealth = maxHealth;
    }

    public void SetParentPot(TreeOfLightPot pot)
    {
        parentPot = pot;
        Debug.Log($"Parent pot reference set: {(parentPot != null ? "success" : "failed")}");
    }

    public void SetGrowthSpeed(float speed)
    {
        animationSpeed = speed;
        if (animator != null)
        {
            animator.SetFloat(GrowthSpeed, speed);
        }
    }

    public void StartGrowth(float duration, float scale)
    {
        Debug.Log($"TreeOfLight.StartGrowth called with duration: {duration}, scale: {scale}");
        growthDuration = duration;
        maxScale = scale;
        isGrowing = true;
        elapsedGrowthTime = 0f;
        
        if (animator != null)
        {
            // Get the animation length
            var animatorState = animator.GetCurrentAnimatorStateInfo(0);
            float animationLength = animatorState.length;
            
            // Calculate the animation speed to match the desired duration
            animationSpeed = animationLength / duration;
            
            Debug.Log($"Animation length: {animationLength}, Setting speed to: {animationSpeed}");
            animator.SetTrigger(GrowTrigger);
            animator.SetFloat(GrowthSpeed, animationSpeed);
        }
        else
        {
            Debug.LogWarning("No Animator component found on tree!");
        }
        
        StartCoroutine(GrowCoroutine());
    }

    public void ResumeGrowth()
    {
        isGrowing = true;
        if (animator != null)
        {
            animator.SetFloat(GrowthSpeed, animationSpeed);
        }
        Debug.Log($"Tree growth resumed from {elapsedGrowthTime} seconds");
    }

    public void StopGrowth()
    {
        isGrowing = false;
        if (animator != null)
        {
            animator.SetFloat(GrowthSpeed, 0f);
        }
        Debug.Log($"Tree growth paused at {elapsedGrowthTime} seconds");
    }

    private IEnumerator GrowCoroutine()
    {
        Debug.Log("Starting GrowCoroutine");
        isGrowing = true;
        
        while (elapsedGrowthTime < growthDuration)
        {
            if (!isGrowing)
            {
                Debug.Log("Growth paused, waiting...");
                yield return null;
                continue;
            }

            elapsedGrowthTime += Time.deltaTime;
            float growthProgress = elapsedGrowthTime / growthDuration;
            
            // Update tree scale
            Vector3 newScale = Vector3.Lerp(startScale, targetScale * maxScale, growthProgress);
            transform.localScale = newScale;
            
            // Update light intensity
            if (treeLight != null)
            {
                float lightProgress = Mathf.Lerp(initialLightIntensity, growingLightIntensity, growthProgress);
                treeLight.intensity = lightProgress;
            }
            
            yield return null;
        }

        Debug.Log("Growth complete!");
        isGrowing = false;
        isFullyGrown = true;
        OnGrowthComplete?.Invoke();
        
        // Start final effect
        StartCoroutine(FinalEffectCoroutine());
    }

    private IEnumerator FinalEffectCoroutine()
    {
        if (treeLight != null)
        {
            float elapsedTime = 0f;
            float pulseDuration = 1f / finalLightPulseSpeed;
            float baseIntensity = maxLightIntensity;
            float targetIntensity = 0f;

            while (elapsedTime < pulseDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / pulseDuration;
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                // Pulse the light
                float pulse = Mathf.Sin(elapsedTime * finalLightPulseSpeed * 2f * Mathf.PI) * finalLightPulseAmount;
                float currentIntensity = Mathf.Lerp(baseIntensity, targetIntensity, smoothT) + pulse;
                treeLight.intensity = currentIntensity;

                yield return null;
            }

            // Ensure we reach the final intensity
            treeLight.intensity = targetIntensity;
        }

        // Wait a moment before breaking
        yield return new WaitForSeconds(0.5f);

        // Now break the tree
        Break();
    }

    public void TakeDamage(float damage)
    {
        if (!isFullyGrown) return;
        
        currentHealth = Mathf.Max(0, currentHealth - damage);
        onDamage?.Invoke();
        
        if (currentHealth <= 0)
        {
            Break();
        }
    }

    public void Break()
    {
        if (grabInteractable != null)
        {
            grabInteractable.enabled = false;
        }
        
        if (breakEffect != null)
        {
            Debug.Log("Playing tree break effect");
            // Create a new GameObject for the particle system
            GameObject effectObject = new GameObject("TreeBreakEffect");
            effectObject.transform.position = transform.position;
            ParticleSystem effect = Instantiate(breakEffect, effectObject.transform);
            effect.Play();
            
            // Destroy the effect object after the effect is done
            Destroy(effectObject, effect.main.duration);
            
            // Trigger pot's break effect immediately
            if (parentPot != null)
            {
                parentPot.OnTreeBreak();
            }
            
            // Wait for effect to complete before disabling the tree
            StartCoroutine(DestroyAfterEffect(effect.main.duration));
        }
        else
        {
            Debug.LogWarning("No break effect assigned to tree");
            OnBreak();
        }
    }

    private IEnumerator DestroyAfterEffect(float duration)
    {
        yield return new WaitForSeconds(duration);
        OnBreak();
    }

    private void OnBreak()
    {
        Debug.Log("Tree breaking - triggering break event");
        onBreak.Invoke();
        gameObject.SetActive(false);
    }

    public float GetHealthPercentage()
    {
        return currentHealth / maxHealth;
    }
} 
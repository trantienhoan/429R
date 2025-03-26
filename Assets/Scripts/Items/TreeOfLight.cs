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
    
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    
    [Header("Growth Settings")]
    [SerializeField] private Vector3 startScale = Vector3.zero;
    [SerializeField] private Vector3 targetScale = Vector3.one;
    [SerializeField] private AnimationCurve growthCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Break Effects")]
    [SerializeField] private ParticleSystem breakEffect;
    [SerializeField] private float breakEffectDuration = 1f;
    [SerializeField] private float breakDelay = 2f; // Longer delay before breaking
    
    [Header("Events")]
    public UnityEvent onBreak;
    public UnityEvent onDamage;
    public UnityEvent OnGrowthComplete;
    
    private float currentHealth;
    private bool isFullyGrown = false;
    private bool isGrowing = false;
    private float growthDuration; // Set by TreeOfLightPot
    private float maxScale; // Set by TreeOfLightPot
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

        // Ensure we have valid components
        if (animator == null)
        {
            Debug.LogError("Animator component missing!");
            return;
        }

        // Store initial scale
        Vector3 initialScale = transform.localScale;
        if (!IsValidScale(initialScale))
        {
            Debug.LogWarning("Invalid initial scale, using small default value");
            initialScale = new Vector3(0.001f, 0.001f, 0.001f);
            transform.localScale = initialScale;
        }

        // Set growth state
        isGrowing = true;
        growthDuration = duration;
        maxScale = targetScale;
        elapsedGrowthTime = 0f;

        // Start growth animation using the correct trigger name
        animator.SetTrigger(GrowTrigger);
        animator.SetFloat(GrowthSpeed, 1f / duration);

        // Start growth coroutine
        StartCoroutine(GrowCoroutine(duration, initialScale, targetScale));
    }

    private IEnumerator GrowCoroutine(float duration, Vector3 startScale, float targetScale)
    {
        Debug.Log($"TreeOfLight: Starting growth coroutine from {startScale} to {targetScale}");
        float elapsedTime = 0f;
        Vector3 currentScale = startScale;

        while (elapsedTime < duration)
        {
            // Only update time and scale if growing
            if (isGrowing)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / duration;

                // Use smooth step for more natural growth
                float smoothT = t * t * (3f - 2f * t);
                float scaleValue = Mathf.Lerp(startScale.x, targetScale, smoothT);

                // Apply uniform scaling
                currentScale = new Vector3(scaleValue, scaleValue, scaleValue);
                if (IsValidScale(currentScale))
                {
                    transform.localScale = currentScale;
                }
                else
                {
                    Debug.LogWarning($"Invalid scale detected during growth: {currentScale}");
                }

                // Update light intensity during growth
                if (treeLight != null)
                {
                    float lightProgress = Mathf.Lerp(initialLightIntensity, growingLightIntensity, smoothT);
                    treeLight.intensity = lightProgress;
                }
            }

            yield return null;
        }

        // Ensure we end up at exactly the target scale
        Vector3 finalScale = new Vector3(targetScale, targetScale, targetScale);
        if (IsValidScale(finalScale))
        {
            transform.localScale = finalScale;
        }
        else
        {
            Debug.LogWarning($"Invalid final scale: {finalScale}");
        }

        Debug.Log("TreeOfLight: Growth complete");
        
        // Mark as fully grown and trigger completion event
        isFullyGrown = true;
        isGrowing = false;
        OnGrowthComplete?.Invoke();
        
        // Start the final effect sequence
        StartCoroutine(FinalEffectCoroutine());
    }

    private bool IsValidScale(Vector3 scale)
    {
        return float.IsFinite(scale.x) && float.IsFinite(scale.y) && float.IsFinite(scale.z) &&
               scale.x > 0 && scale.y > 0 && scale.z > 0 &&
               scale.x < 1000 && scale.y < 1000 && scale.z < 1000;
    }

    public void ResumeGrowth()
    {
        if (isFullyGrown) return;
        
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

    private void SetupAnimation(float duration)
    {
        if (animator != null)
        {
            // Try to get the animation length safely
            try
            {
                var animatorState = animator.GetCurrentAnimatorStateInfo(0);
                float animationLength = animatorState.length;
                
                // Ensure we don't divide by zero
                if (duration > 0 && animationLength > 0)
                {
                    animationSpeed = animationLength / duration;
                }
                else
                {
                    animationSpeed = 1f;
                }
                
                Debug.Log($"Animation length: {animationLength}, Setting speed to: {animationSpeed}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Could not get animation state: {e.Message}");
                animationSpeed = 1f;
            }
            
            animator.SetTrigger(GrowTrigger);
            animator.SetFloat(GrowthSpeed, animationSpeed);
        }
        else
        {
            Debug.LogWarning("No Animator component found on tree!");
        }
    }

    private IEnumerator FinalEffectCoroutine()
    {
        Debug.Log("Starting final light effect");
        
        if (treeLight != null)
        {
            // Start at growing intensity and pulse for a while
            float pulseStartTime = Time.time;
            float pulseDuration = breakDelay; // Pulse until scheduled break
            
            while (Time.time - pulseStartTime < pulseDuration)
            {
                // Calculate pulsing light
                float pulse = Mathf.Sin((Time.time - pulseStartTime) * finalLightPulseSpeed * 2f * Mathf.PI) * finalLightPulseAmount;
                treeLight.intensity = growingLightIntensity + pulse;
                
                yield return null;
            }
            
            // Ensure we reach the final intensity
            treeLight.intensity = growingLightIntensity;
        }
        
        yield return new WaitForSeconds(0.5f);

        Debug.Log("Final effect complete, tree will now break");
        if (parentPot != null)
        {
            parentPot.StartBreakSequence();
        }
        else
        {
            Debug.LogWarning("No parent pot reference found!");
        }
    }

    public void TakeDamage(float damage)
    {
        if (!isFullyGrown || damage <= 0) return;
        
        currentHealth = Mathf.Max(0, currentHealth - damage);
        onDamage?.Invoke();
        
        // Flash the light when damaged
        if (treeLight != null)
        {
            StartCoroutine(DamageFlash());
        }
        
        if (currentHealth <= 0)
        {
            Break();
        }
    }

    private IEnumerator DamageFlash()
    {
        float originalIntensity = treeLight.intensity;
        treeLight.intensity = originalIntensity * 1.5f;
        yield return new WaitForSeconds(0.1f);
        treeLight.intensity = originalIntensity;
    }

    private void Break(bool breakPot = false)
    {
        if (!isFullyGrown) return;
        
        Debug.Log("Tree breaking - starting break sequence");
        
        // Play break effect if available
        if (breakEffect != null)
        {
            breakEffect.Play();
            StartCoroutine(DestroyAfterEffect(breakEffectDuration));
        }
        else
        {
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
        onBreak?.Invoke();
        gameObject.SetActive(false);
    }

    public float GetHealthPercentage()
    {
        return currentHealth / maxHealth;
    }
    
    private void OnDisable()
    {
        // Clean up any running coroutines
        StopAllCoroutines();
    }
}
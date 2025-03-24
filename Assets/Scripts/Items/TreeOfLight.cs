using UnityEngine;
using System.Collections;
using UnityEngine.Events;
using Core;


public class TreeOfLight : MonoBehaviour
{
    [Header("Light Settings")]
    [SerializeField] private Light treeLight;
    [SerializeField] private float maxLightIntensity = 2f;
    [SerializeField] private float finalLightPulseSpeed = 1f;
    [SerializeField] private float finalLightPulseAmount = 0.2f;
    
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    
    [Header("Break Effects")]
    [SerializeField] private ParticleSystem breakEffect;
    [SerializeField] private float breakEffectDuration = 1f;
    
    [Header("Events")]
    public UnityEvent onBreak;
    public UnityEvent onDamage;
    
    private float currentScale = 0f;
    private float currentLightIntensity = 0f;
    private float currentHealth;
    private bool isFullyGrown = false;
    private bool isGrowing = false;
    private float growthDuration; // Set by TreeOfLightPot
    private float maxScale; // Set by TreeOfLightPot
    private float elapsedGrowthTime = 0f; // Track how long we've been growing
    
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
        if (animator != null)
        {
            animator.SetFloat(GrowthSpeed, speed);
        }
    }

    public void StartGrowth(float duration, float scale)
    {
        growthDuration = duration;
        maxScale = scale;
        isGrowing = true;
        if (animator != null)
        {
            animator.SetTrigger(GrowTrigger);
            animator.SetFloat(GrowthSpeed, 1f / duration);
        }
        StartCoroutine(GrowCoroutine());
    }

    public void ResumeGrowth()
    {
        isGrowing = true;
        if (animator != null)
        {
            animator.SetFloat(GrowthSpeed, 1f / growthDuration);
        }
        StartCoroutine(GrowCoroutine());
    }

    public void StopGrowth()
    {
        isGrowing = false;
        if (animator != null)
        {
            animator.SetFloat(GrowthSpeed, 0f);
        }
        StopAllCoroutines();
        Debug.Log($"Tree growth stopped at {elapsedGrowthTime:F1} seconds");
    }

    private IEnumerator GrowCoroutine()
    {
        float elapsedGrowthTime = 0f;
        float startScale = 0f;
        float targetScale = maxScale;
        Vector3 startPosition = transform.localPosition;
        Vector3 targetPosition = transform.localPosition + Vector3.up * 0.5f; // Move up slightly during growth

        // Set initial scale
        transform.localScale = Vector3.zero;
        if (treeLight != null)
        {
            treeLight.intensity = 0f;
        }

        // Start growth animation
        if (animator != null)
        {
            animator.SetTrigger("Grow");
        }

        while (elapsedGrowthTime < growthDuration && isGrowing)
        {
            elapsedGrowthTime += Time.deltaTime;
            float t = elapsedGrowthTime / growthDuration;
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            // Scale up
            float currentScale = Mathf.Lerp(startScale, targetScale, smoothT);
            transform.localScale = new Vector3(currentScale, currentScale, currentScale);

            // Move up slightly
            transform.localPosition = Vector3.Lerp(startPosition, targetPosition, smoothT);

            // Adjust light intensity
            if (treeLight != null)
            {
                float lightIntensity = Mathf.Lerp(0f, maxLightIntensity, smoothT);
                treeLight.intensity = lightIntensity;
            }

            yield return null;
        }

        // Ensure we reach the final values
        transform.localScale = Vector3.one * targetScale;
        transform.localPosition = targetPosition;
        if (treeLight != null)
        {
            treeLight.intensity = maxLightIntensity;
        }

        // Only trigger break if we completed growth
        if (isGrowing)
        {
            Debug.Log("Tree growth complete, starting final effect");
            StartCoroutine(FinalEffectCoroutine());
        }
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
            breakEffect.Play();
            StartCoroutine(DestroyAfterEffect());
        }
        else
        {
            Debug.LogWarning("No break effect assigned to tree");
            OnBreak();
        }
    }

    private IEnumerator DestroyAfterEffect()
    {
        if (breakEffect != null)
        {
            // Wait for the particle system to finish
            yield return new WaitForSeconds(breakEffect.main.duration);
        }
        else
        {
            yield return new WaitForSeconds(breakEffectDuration);
        }
        
        OnBreak();
    }

    private void OnBreak()
    {
        Debug.Log("Tree breaking - triggering break event");
        onBreak.Invoke();
        if (parentPot != null)
        {
            parentPot.OnTreeBreak();
        }
        gameObject.SetActive(false);
    }

    public float GetHealthPercentage()
    {
        return currentHealth / maxHealth;
    }
} 
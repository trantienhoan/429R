using UnityEngine;
using System.Collections;
using Core; // Assuming HealthComponent is in the Core namespace
using Items; // Assuming ItemDropHandler is in the Items namespace
using UnityEngine.Events;

public class TreeOfLight : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private float growthSpeed = 1f;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string growthAnimationName = "TreeGrowth";

    [Header("Light Effects")]
    [SerializeField] private Light treeLight;
    [SerializeField] private float maxLightIntensity = 2f;
    [SerializeField] private float pulseSpeed = 1f;

    [Header("References")]
    [SerializeField] private TreeOfLightPot parentPot;
    [SerializeField] public ShadowMonsterSpawner monsterSpawner;
    [SerializeField] private ItemDropHandler itemDropHandler; // Reference to the ItemDropHandler
    [SerializeField] private HealthComponent healthComponent;
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private UnityEvent onGrowthComplete;
    private bool isGrowing = false;
    private bool isComplete = false;
    private bool isBlinding = false;

    // Cache the ID of the "Growth" animation parameter
    private int growthProgressID;
    private Coroutine pulseCoroutine;

    private void Start()
    {
        //Get parent pot
        //Get ShadowMonsterSpawner

        if(onGrowthComplete == null)
        {
            onGrowthComplete = new UnityEvent();
        }
    }

    private void Awake()
    {
        growthProgressID = Animator.StringToHash("GrowTrigger");
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                Debug.LogError("Animator is missing on TreeOfLight!");
                enabled = false;
                return;
            }
        }

        if (treeLight == null)
        {
            treeLight = GetComponentInChildren<Light>();
            if (treeLight == null)
            {
                Debug.LogWarning("TreeLight not found on TreeOfLight or its children.");
            }
        }

        if (healthComponent == null)
        {
            healthComponent = GetComponent<HealthComponent>();
            if (healthComponent == null)
            {
                healthComponent = gameObject.AddComponent<HealthComponent>();
            }
        }
        healthComponent.SetMaxHealth(maxHealth);
        healthComponent.OnDeath += Break;

        if (itemDropHandler == null)
        {
            itemDropHandler = GetComponent<ItemDropHandler>();
            if (itemDropHandler == null)
            {
                Debug.LogError("ItemDropHandler is missing on TreeOfLight!");
                enabled = false;
                return;
            }
        }
    }

    private void OnHealthChanged(float currentHealth, float maxHealth)
    {
        Debug.Log($"Tree Health Changed: {currentHealth} / {maxHealth}");
    }

    private void OnEnable()
    {
        healthComponent.OnHealthChanged += OnHealthChanged;
    }

    private void OnDisable()
    {
        healthComponent.OnHealthChanged -= OnHealthChanged;
    }

    public void SetParentPot(TreeOfLightPot pot)
    {
        parentPot = pot;
    }

    public void SetGrowthSpeed(float speed)
    {
        growthSpeed = speed;
    }

    public void BeginGrowth(float duration)
    {
        if (animator == null)
        {
            Debug.LogError("Animator not found!");
            return;
        }

        if (!string.IsNullOrEmpty(growthAnimationName))
        {
            isGrowing = true;
            animator.speed = growthSpeed;
            animator.Play(growthAnimationName);
            animator.SetTrigger(growthProgressID);
            if (pulseCoroutine == null)
            {
                pulseCoroutine = StartCoroutine(PulseLight());
            }

            // Assuming the growth animation length is equal to duration

            // Get animation state information
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            float clipLength = stateInfo.length; // Get the clip length

            AnimatorClipInfo[] animatorClipInfo = animator.GetCurrentAnimatorClipInfo(0);
            float animationSpeed = 1f; // Assuming default speed of 1, get the animation speed set inside the animator controller
            if(animatorClipInfo.Length > 0)
            {
                animationSpeed = animatorClipInfo[0].clip.frameRate;
            }

            float visualDuration = clipLength / animator.speed;

            Invoke(nameof(CompleteVisualGrowth), visualDuration);
        }
        else
        {
            Debug.LogError("Growth animation name is null or empty!");
        }
    }

    public void PauseAnimation()
    {
        animator.speed = 0;
        StopCoroutine(PulseLight());
    }

    public void ResumeAnimation()
    {
        animator.speed = growthSpeed;
        StartCoroutine(PulseLight());
    }

    public void UpdateVisualGrowth(float progress)
    {
        if (animator != null)
        {
            animator.SetFloat(growthProgressID, progress);
        }
    }

    private void OnDestroy()
    {
        if (isGrowing)
        {
            CancelInvoke(nameof(CompleteVisualGrowth));
        }

        if (pulseCoroutine != null)
        {
            StopCoroutine(PulseLight());
        }
    }

    public void CompleteVisualGrowth()
    {
        if (!isComplete)
        {
            isGrowing = false;
            isComplete = true;
            if (monsterSpawner != null)
            {
                monsterSpawner.StopSpawning(); // Stop monster spawning
                monsterSpawner.CleanupMonsterLists(); //Kill the current shadow monsters
            }
            itemDropHandler.SetHasGrown(true);

            if (parentPot != null)
            {
                ItemDropHandler potItemDrop = parentPot.GetComponent<ItemDropHandler>();
                if (potItemDrop != null)
                {
                    potItemDrop.SetHasGrown(true);
                }
                HealthComponent potHealth = parentPot.GetComponent<HealthComponent>();
                if (potHealth != null)
                {
                    //DO NOT KILL THE POT.  COMMENT OUT OR REMOVE THIS LINE
                    //potHealth.TakeDamage(potHealth.MaxHealth);
                }
            }
            StartCoroutine(CompletionSequence());
            onGrowthComplete?.Invoke();
        }
    }
    IEnumerator BlindingLightEffect()
    {
        isBlinding = true;
        float originalIntensity = treeLight.intensity;
        float elapsedTime = 0f;

        while (elapsedTime < 1f)
        {
            elapsedTime += Time.deltaTime;
            treeLight.intensity = Mathf.Lerp(originalIntensity, maxLightIntensity * 4f, elapsedTime);
            yield return null;
        }

        elapsedTime = 0f;

        while (elapsedTime < 1f)
        {
            elapsedTime += Time.deltaTime;
            treeLight.intensity = Mathf.Lerp(maxLightIntensity * 4f, originalIntensity, elapsedTime);
            yield return null;
        }
        isBlinding = false;
    }
    IEnumerator PulseLight()
    {
        float startIntensity = treeLight.intensity;
        while (true)
        {
            float sin = Mathf.Sin(Time.time * pulseSpeed);
            treeLight.intensity = startIntensity + (sin * maxLightIntensity);
            yield return null;
        }
    }
    IEnumerator CompletionSequence()
    {
        yield return BlindingLightEffect();
        yield return new WaitForSeconds(1f);
        // Drop items before destorying
        itemDropHandler.DropItems();

        // Destroy the TreeOfLight GameObject.
        Destroy(gameObject);
    }
    public void OnHit()
    {
        if (!isBlinding)
        {
            StartCoroutine(BlindingLightEffect());
        }
    }
    public void OnHealthDepleted()
    {
        Break();
    }
    public void Break()
    {
        // Handle what happens when the tree is destroyed.
        if(healthComponent != null)
        {
            healthComponent.OnDeath -= Break;
        }
        // May need to trigger item drop based on tree growth state using the ItemDropHandler
        itemDropHandler.DropItems();
        Destroy(gameObject);
    }

    private bool IsValidScale(Vector3 scale)
    {
        float minComponent = Mathf.Min(scale.x, scale.y, scale.z);
        float maxComponent = Mathf.Max(scale.x, scale.y, scale.z);
        return minComponent > 0f && maxComponent < 100f;
    }
}
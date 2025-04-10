using UnityEngine;
using System.Collections;
using Core; // Assuming HealthComponent is in the Core namespace
using UnityEngine.Events;
using Items;

public class TreeOfLight : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private float growthSpeed = 1f;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private AnimationClip growthAnimClip;
    [SerializeField] private string growthAnimationName = "TreeGrowth";

    [Header("Light Effects")]
    [SerializeField] private Light treeLight;
    [SerializeField] private float maxLightIntensity = 2f;
    [SerializeField] private float pulseSpeed = 1f;
    [SerializeField] private float blindingIntensityMultiplier = 4f;

    [Header("References")]
    [SerializeField] private TreeOfLightPot parentPot;
    [SerializeField] private ShadowMonsterSpawner monsterSpawner;
    [SerializeField] private HealthComponent healthComponent;
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private UnityEvent onGrowthComplete;
    private bool isGrowing = false;
    private bool isComplete = false;
    private bool isBlinding = false;

    // Cache the ID of the "Growth" animation parameter
    private int _growthProgressID;
    private Coroutine pulseCoroutine;
    public TreeOfLightPot ParentPot {get { return parentPot; }}
    
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
        if(growthAnimClip == null)
        {
            Debug.LogError("No growth animation attached to the Tree!");
        }

        _growthProgressID = Animator.StringToHash("GrowTrigger");
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
    }

    private void OnHealthChanged(object sender, HealthComponent.HealthChangedEventArgs e)
    {
        Debug.Log($"Tree Health Changed: {e.CurrentHealth} / {e.MaxHealth}");
    }

    private void OnEnable()
    {
        healthComponent.OnHealthChanged += OnHealthChanged;
        healthComponent.OnDeath += OnDeathHandler; // Subscribe to OnDeath
    }

    private void OnDisable()
    {
        healthComponent.OnHealthChanged -= OnHealthChanged;
        healthComponent.OnDeath -= OnDeathHandler; // Unsubscribe from OnDeath
    }
    public void SetParentPot(TreeOfLightPot pot)
    {
        parentPot = pot;
    }

    private void OnDeathHandler(HealthComponent health)
    {
        // Handle the death of the TreeOfLight here
        Debug.Log("TreeOfLight has died!");
        //Potentially despawn monsters
        monsterSpawner.CleanupMonsterLists();
        // You might want to trigger some visual effects or game logic here
        StopAllCoroutines();
        Destroy(gameObject); // Or play a destruction animation before destroying
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
            animator.SetTrigger(_growthProgressID);
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
            animator.SetFloat(_growthProgressID, progress);
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
            treeLight.intensity = Mathf.Lerp(originalIntensity, maxLightIntensity * blindingIntensityMultiplier, elapsedTime);
            yield return null;
        }

        elapsedTime = 0f;

        while (elapsedTime < 1f)
        {
            elapsedTime += Time.deltaTime;
            treeLight.intensity = Mathf.Lerp(maxLightIntensity * blindingIntensityMultiplier, originalIntensity, elapsedTime);
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
        yield return StartCoroutine(BlindingLightEffect());
        yield return new WaitForSeconds(1f);
        // Destroy the TreeOfLight GameObject.
        Destroy(gameObject);
    }
    public void OnHit()
    {
        if (isBlinding)
        {
            StopCoroutine(BlindingLightEffect());
            isBlinding = false;
        }

        StartCoroutine(BlindingLightEffect());
    }
    public void OnHealthDepleted()
    {

    }
}
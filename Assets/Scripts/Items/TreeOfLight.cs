using UnityEngine;
using System.Collections;
using Core;
using UnityEngine.Events;
using Items;
using System;
using System.Linq;

public class TreeOfLight : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private float growthSpeed = 1f;
    [SerializeField] private float growthStartDelay = 5f;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string growthAnimationName = "Seed_Grow";

    [Header("References")]
    [SerializeField] private TreeOfLightPot parentPot;
    [SerializeField] private HealthComponent healthComponent;
    [SerializeField] private UnityEvent onGrowthComplete;
    [SerializeField] private ShadowMonsterSpawner monsterSpawner;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip growthSound;

    private bool isGrowing = false;
    private bool isComplete = false;

    private int _growthProgressID;

    public TreeOfLightPot ParentPot { get { return parentPot; } }

    public event EventHandler OnGrowthComplete;

    private void Start()
    {
        if (onGrowthComplete == null)
        {
            onGrowthComplete = new UnityEvent();
        }
    }

    private void Awake()
    {
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

        if (healthComponent == null)
        {
            healthComponent = GetComponent<HealthComponent>();
            if (healthComponent == null)
            {
                healthComponent = gameObject.AddComponent<HealthComponent>();
            }
        }

        if (monsterSpawner == null)
        {
            Debug.LogWarning("Monster Spawner not assigned!");
        }
    }

    private void OnEnable()
    {
        healthComponent.OnHealthChanged += OnHealthChanged;
        healthComponent.OnDeath += OnDeathHandler;
    }

    private void OnDisable()
    {
        healthComponent.OnHealthChanged -= OnHealthChanged;
        healthComponent.OnDeath -= OnDeathHandler;
    }

    public void SetParentPot(TreeOfLightPot pot)
    {
        parentPot = pot;
    }

    private void OnHealthChanged(object sender, HealthComponent.HealthChangedEventArgs e)
    {
        Debug.Log($"Tree Health Changed: {e.CurrentHealth} / {e.MaxHealth}");
    }

    public void SetGrowthSpeed(float speed)
    {
        growthSpeed = speed;
    }
    private bool hasBegunGrowth = false;

    public void BeginGrowth(bool canGrow = true)
    {
        if (hasBegunGrowth)
        {
            Debug.LogWarning("BeginGrowth called multiple times!");
            return;
        }
        hasBegunGrowth = true;

        if (animator == null)
        {
            Debug.LogError("Animator is not assigned!");
            return;
        }
        Debug.Log("Animator is assigned: True");

        StartCoroutine(StartGrowthWithDelay());
    }

    void Update()
    {
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            //Debug.Log($"Current animation state: {stateInfo.normalizedTime}  Name: {stateInfo.IsName(growthAnimationName)}");
            if (stateInfo.IsName(growthAnimationName))
            {
                // Animation is playing
            }
            else
            {
                Debug.Log($"Animator is NOT playing {growthAnimationName}. Current state: {stateInfo.IsName(growthAnimationName)}");
            }
        }
    }

    private IEnumerator StartGrowthWithDelay()
    {
        string animName = growthAnimationName;
        AnimationClip clip = animator.runtimeAnimatorController.animationClips.FirstOrDefault(c => c.name == animName);

        if (!string.IsNullOrEmpty(growthAnimationName))
        {
            if (clip != null)
            {
                float animationLength = clip.length;
                Debug.Log("Animation Length: " + animationLength);
                monsterSpawner?.BeginSpawning();
                float growthSpeed = this.growthSpeed;
                float delay = animationLength / growthSpeed;
                Debug.Log($"Calculated delay: {delay}");  // ADDED LOG
                Debug.Log($"Time.timeScale: {Time.timeScale}"); // ADDED LOG
                Debug.Log("CompleteVisualGrowthDelayed started");
                StartCoroutine(CompleteVisualGrowthDelayed(delay));
            }
            else
            {
                Debug.LogError("Animation clip not found!  Ensure the animation name is correct and the clip exists on the Animator Controller.");
            }
        }
        else
        {
            Debug.LogError("Growth animation name is null or empty!");
            yield return null;
        }
    }

    private IEnumerator CompleteVisualGrowthDelayed(float delay)
    {
        Debug.Log("CompleteVisualGrowthDelayed started"); // ADDED LOG
        Debug.Log($"CompleteVisualGrowthDelayed: Waiting for {delay} seconds");
        yield return new WaitForSeconds(delay);
        Debug.Log("CompleteVisualGrowthDelayed: Delay complete, calling CompleteVisualGrowth");
        CompleteVisualGrowth();
    }

    private void OnDestroy()
    {
        if (isGrowing)
        {
            CancelInvoke(nameof(CompleteVisualGrowth));
        }
    }

    public void CompleteVisualGrowth()
    {
       Debug.Log("CompleteVisualGrowth called!"); // ADDED LOG
        if (!isComplete)
        {
            Debug.Log("CompleteVisualGrowth: isComplete is false, proceeding"); // ADDED LOG
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
                    //PotHealth actions
                }
            }
        }
        else
        {
             Debug.Log("CompleteVisualGrowth: isComplete is true, exiting"); // ADDED LOG
        }

        if (healthComponent != null)
        {
            Debug.Log("CompleteVisualGrowth: healthComponent found, taking damage"); // ADDED LOG
            healthComponent.TakeDamage(healthComponent.MaxHealth, transform.position, gameObject);  // Inflict fatal damage
        }
        else
        {
            Debug.LogError("CompleteVisualGrowth: healthComponent is NULL!"); // ADDED LOG
        }

        OnGrowthComplete?.Invoke(this, EventArgs.Empty); // Invoke the event
        onGrowthComplete?.Invoke(); // Invoke the UnityEvent as well

        // Find all monsters and deal damage
        GameObject[] monsters = GameObject.FindGameObjectsWithTag("Monster"); // Replace "Monster" with your monster's tag
        Debug.Log($"CompleteVisualGrowth: Found {monsters.Length} monsters, applying fatal damage."); // ADDED LOG
        foreach (GameObject monster in monsters)
        {
            HealthComponent monsterHealth = monster.GetComponent<HealthComponent>();
            if (monsterHealth != null)
            {
                monsterHealth.TakeDamage(monsterHealth.MaxHealth, transform.position, gameObject); // Apply fatal damage
            }
            else
            {
                Debug.LogWarning($"Monster {monster.name} has no HealthComponent!");
            }
        }
    }

    private void OnDeathHandler(HealthComponent health)
    {
        Debug.Log("TreeOfLight has died!");
        StopAllCoroutines();
        monsterSpawner?.StopSpawning();
        StartCoroutine(DelayedDestruction());
    }

    private IEnumerator DelayedDestruction()
    {
        yield return new WaitForSeconds(2f);
        if (parentPot != null)
        {
            Destroy(parentPot.gameObject);
        }
        Destroy(gameObject);
    }
}
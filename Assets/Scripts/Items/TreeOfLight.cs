using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using Enemies;
using Core;

namespace Items
{
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
        [SerializeField] public HealthComponent healthComponent;
        [SerializeField] private UnityEvent onGrowthComplete;
        [SerializeField] private ShadowMonsterSpawner monsterSpawner;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip growthSound;

        private bool isGrowing;
        private bool isComplete;
        private bool hasBegunGrowth;

        public TreeOfLightPot ParentPot => parentPot;

        public event System.EventHandler OnGrowthComplete;
        public event System.Action OnPotDeath;

        private void Awake()
        {
            animator ??= GetComponent<Animator>();
            if (animator == null)
            {
                Debug.LogError("Animator is missing on TreeOfLight!");
                enabled = false;
                return;
            }

            healthComponent ??= GetComponent<HealthComponent>();
            if (healthComponent == null)
            {
                healthComponent = gameObject.AddComponent<HealthComponent>();
            }

            if (monsterSpawner == null)
            {
                Debug.LogWarning("Monster Spawner not assigned!");
            }

            onGrowthComplete ??= new UnityEvent();
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

        public void SetParentPot(TreeOfLightPot pot) => parentPot = pot;

        private void OnHealthChanged(object sender, HealthComponent.HealthChangedEventArgs e)
        {
            Debug.Log($"Tree Health Changed: {e.CurrentHealth} / {e.MaxHealth}");
        }

        public void SetGrowthSpeed(float speed) => growthSpeed = speed;

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

            StartCoroutine(StartGrowthWithDelay());
        }

        private void Update()
        {
            if (isGrowing && !isComplete)
            {
                transform.Rotate(Vector3.up * (30f * Time.deltaTime));
            }
        }

        private IEnumerator StartGrowthWithDelay()
        {
            if (!string.IsNullOrEmpty(growthAnimationName))
            {
                var clip = animator.runtimeAnimatorController.animationClips.FirstOrDefault(c => c.name == growthAnimationName);
                if (clip != null)
                {
                    monsterSpawner?.BeginSpawning();
                    float delay = clip.length / growthSpeed;
                    isGrowing = true;
                    yield return new WaitForSeconds(delay);
                    CompleteVisualGrowth();
                }
                else
                {
                    Debug.LogError("Animation clip not found! Ensure the animation name is correct and the clip exists.");
                }
            }
            else
            {
                Debug.LogError("Growth animation name is null or empty!");
            }
        }

        private void CompleteVisualGrowth()
        {
            if (isComplete) return;

            isGrowing = false;
            isComplete = true;

            if (parentPot != null)
            {
                var potItemDrop = parentPot.GetComponent<ItemDropHandler>();
                potItemDrop?.SetHasGrown(true);
            }

            if (healthComponent != null)
            {
                healthComponent.TakeDamage(healthComponent.MaxHealth, transform.position, gameObject);
            }

            OnGrowthComplete?.Invoke(this, System.EventArgs.Empty);
            onGrowthComplete?.Invoke();

            var monsters = GameObject.FindGameObjectsWithTag("Monster");
            foreach (var monster in monsters)
            {
                var monsterHealth = monster.GetComponent<HealthComponent>();
                if (monsterHealth != null)
                {
                    monsterHealth.TakeDamage(monsterHealth.MaxHealth, transform.position, gameObject);
                }
            }
        }

        private void OnDeathHandler(HealthComponent health)
        {
            StopAllCoroutines();
            monsterSpawner?.StopSpawning();
            OnPotDeath?.Invoke();
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
}

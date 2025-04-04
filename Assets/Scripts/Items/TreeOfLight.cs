using System;
using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using Core;
using Enemies; // Added missing namespace for ShadowMonsterSpawner

namespace Items
{
    public class TreeOfLight : MonoBehaviour
    {
        [Header("Light Settings")]
        [SerializeField] private Light treeLight;
        [SerializeField] private float maxLightIntensity = 2f;
        [SerializeField] private float initialLightIntensity = 0.1f;
        [SerializeField] private float growingLightIntensity = 0.5f;
        [SerializeField] private float finalLightIntensityMultiplier = 3f;
        [SerializeField] private float finalLightPulseSpeed = 1f;
        [SerializeField] private float finalLightPulseAmount = 0.2f;

        [Header("Growth Settings")]
        [SerializeField] private Vector3 startScale = Vector3.zero;
        [SerializeField] private Vector3 targetScale = Vector3.one;
        [SerializeField] private AnimationCurve growthCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private AnimationCurve lightIntensityCurve;
        [SerializeField] private float growthDuration = 60f;
        [SerializeField] private bool rotateWhileGrowing = true;
        [SerializeField] private float rotationSpeed = 30f;

        [Header("Completion Effect")]
        [SerializeField] private GameObject completionEffectPrefab; // Changed to GameObject
        private ParticleSystem completionEffect;
        private bool isComplete = false;

        private TreeOfLightPot _treeOfLightPot;

        //[SerializeField] private float completionEffectDuration = 3f;
        [SerializeField] public GameObject keyPrefab;
        //[SerializeField] private bool destroyPotOnCompletion = true;

        [Header("Breaking Effects")]
        [SerializeField] private ParticleSystem hitParticleEffect;
        [SerializeField] private ParticleSystem breakParticleEffect;
        [SerializeField] private AudioClip breakSound;
        [SerializeField] private GameObject intactModel;
        [SerializeField] private float destroyDelayAfterBreak = 2.0f;

        [Header("Events")]
        public UnityEvent onGrowthStarted; // Added for compatibility with ShadowMonsterSpawner
        public UnityEvent onGrowthStart;   // Keep for backward compatibility
        public UnityEvent onGrowthPaused;
        public UnityEvent onGrowthResumed;
        public UnityEvent onGrowthComplete;
        public UnityEvent onTreeHit;
        public UnityEvent onTreeBroken;

        private TreeOfLightPot parentPot;

        // State tracking
        private bool isFullyGrown;
        private bool isGrowing;
        private bool isPaused;
        private float growthProgress;
        private float animationSpeed = 1f;

        // Coroutines
        private Coroutine growthRoutine;
        private Coroutine finalEffectRoutine;

        // Components
        private Animator animator;
        private HealthComponent healthComponent;

        // Animation parameters
        private static readonly int GrowTrigger = Animator.StringToHash("GrowTrigger");
        private static readonly int GrowthSpeed = Animator.StringToHash("GrowthSpeed");

        // Callbacks
        private Action onGrowthCompleteCallback;
        private float lastRecordedHealth;

        private void Start()
        {
            // Instantiate the completionEffect from the prefab
            if (completionEffectPrefab != null)
            {
                GameObject go = Instantiate(completionEffectPrefab, transform); // Instantiate the prefab
                completionEffect = go.GetComponent<ParticleSystem>();

                if (completionEffect != null)
                {
                    completionEffect.Stop(); // Ensure it's stopped, not just paused
                    completionEffect.transform.localPosition = Vector3.zero;
                    completionEffect.transform.localRotation = Quaternion.identity;
                }
                else
                {
                    Debug.LogError("ParticleSystem component not found on instantiated prefab!");
                }
            }
            else
            {
                Debug.LogError("Completion Effect Prefab not assigned!");
            }
        }

        private void Awake()
        {
            onGrowthStarted = new UnityEvent();

            // Get components
            animator = GetComponent<Animator>();
            healthComponent = GetComponent<HealthComponent>();
            if (healthComponent == null)
            {
                healthComponent = gameObject.AddComponent<HealthComponent>();
            }

            // Initialize default curves if needed
            if (lightIntensityCurve == null || lightIntensityCurve.keys.Length == 0)
            {
                lightIntensityCurve = new AnimationCurve();
                lightIntensityCurve.AddKey(0f, 0f);
                lightIntensityCurve.AddKey(0.7f, 0.3f);
                lightIntensityCurve.AddKey(0.9f, 0.5f);
                lightIntensityCurve.AddKey(1f, 1f);
            }

            // Initialize light
            if (treeLight != null)
            {
                treeLight.intensity = initialLightIntensity;
            }

            // Initialize scale
            transform.localScale = startScale;
        }
        private void OnHealthChanged(float currentHealth, float maxHealth)
        {
            // Implement your health changed logic here
            // For example:
            if (currentHealth < lastRecordedHealth)
            {
                // Tree took damage
                OnHit();
            }

            // Update last recorded health
            lastRecordedHealth = currentHealth;
        }

        private void OnEnable()
        {
            // Subscribe to health events if health component exists
            if (healthComponent != null)
            {
                healthComponent.OnHealthChanged += OnHealthChanged;
                healthComponent.OnDeath += OnHealthDepleted;
                lastRecordedHealth = healthComponent.Health; // Fixed: removed parentheses
            }
        }

        private void OnDisable()
        {
            // Unsubscribe from health events
            if (healthComponent != null)
            {
                healthComponent.OnHealthChanged -= OnHealthChanged;
                healthComponent.OnDeath -= OnHealthDepleted;
            }
        }

        /// <summary>
        /// Links this tree to its parent pot
        /// </summary>
        public void SetParentPot(TreeOfLightPot pot)
        {
            parentPot = pot;
            Debug.Log($"TreeOfLight: Parent pot reference set: {(parentPot != null ? "success" : "failed")}");
        }

        /// <summary>
        /// Adjusts the speed of tree growth
        /// </summary>
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

        /// <summary>
        /// Starts the tree growth process
        /// </summary>
        
        /// <summary>
        /// Starts growing with specified duration and completion callback
        /// </summary>
        
        // Keep this for backward compatibility
        public void StartGrowing(float duration, Action onComplete = null)
        {
            // Return if already growing or fully grown
            if (isGrowing || isFullyGrown)
                return;

            Debug.Log("TreeOfLight: Starting growth process");

            // Set growth state
            isGrowing = true;
            isPaused = false;
            growthProgress = 0f;

            // Set the callback to be triggered when growth completes
            onGrowthCompleteCallback = onComplete;

            // Override growth duration if needed
            growthDuration = duration > 0 ? duration : growthDuration;

            // Start growth animation
            if (animator != null)
            {
                Debug.Log("animationSpeed: " + animationSpeed);
                animator.SetTrigger(GrowTrigger);
                animator.SetFloat(GrowthSpeed, animationSpeed);
            }

            // Notify listeners - invoke both events for compatibility
            onGrowthStart?.Invoke();
            onGrowthStarted?.Invoke();

            // Start the actual growth coroutine
            if (growthRoutine != null)
            {
                StopCoroutine(growthRoutine);
            }
            growthRoutine = StartCoroutine(GrowthRoutine());

            // Notify the parent pot that growth has started
            if (parentPot != null)
            {
                parentPot.OnTreeGrowthStarted();
            }
        }

        private IEnumerator GrowthRoutine()
        {
            float elapsedGrowthTime = 0f;

            while (isGrowing && growthProgress < 1.0f)
            {
                if (!isPaused)
                {
                    // Update elapsed time and progress
                    elapsedGrowthTime += Time.deltaTime * animationSpeed;
                    growthProgress = Mathf.Clamp01(elapsedGrowthTime / growthDuration);

                    // Update visuals
                    UpdateGrowthProgress(growthProgress);

                    // Notify parent pot about growth progress
                    if (parentPot != null)
                    {
                        parentPot.UpdateTreeGrowthProgress(growthProgress);
                    }

                    // Check if growth is complete
                    if (growthProgress >= 1.0f && !isFullyGrown)
                    {
                        CompleteGrowth();
                    }
                }
                yield return null;
            }
        }

        /// <summary>
        /// Pauses the growth process
        /// </summary>
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

                onGrowthPaused?.Invoke();

                // Notify parent pot
                if (parentPot != null)
                {
                    parentPot.OnTreeGrowthPaused();
                }

                Debug.Log("Tree growth paused");
            }
        }

        /// <summary>
        /// Resumes the growth process after pause
        /// </summary>
        public void ResumeGrowth()
        {
            if (isGrowing && isPaused)
            {
                isPaused = false;

                // Resume animation
                if (animator != null)
                {
                    animator.speed = animationSpeed;
                }

                onGrowthResumed?.Invoke();

                // Notify parent pot
                if (parentPot != null)
                {
                    parentPot.OnTreeGrowthResumed();
                }

                Debug.Log("Tree growth resumed");
            }
        }

        /// <summary>
        /// Updates the visual representation of growth progress
        /// </summary>
        public void UpdateGrowthProgress(float progress)
        {
            // Scale the tree based on progress
            Vector3 newScale = Vector3.Lerp(startScale, targetScale, growthCurve.Evaluate(progress));
            if (IsValidScale(newScale))
            {
                transform.localScale = newScale;
            }
            if (isGrowing && !isPaused && rotateWhileGrowing)
            {
                // Rotate the tree around its Y-axis
                transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
            }

            // Update light intensity based on the special curve
            if (treeLight != null)
            {
                // Use the custom curve for light intensity
                float intensityProgress = lightIntensityCurve.Evaluate(progress);

                // Calculate final intensity value
                float targetIntensity = initialLightIntensity;

                // For the last 10% of growth, dramatically increase light
                if (progress < 0.9f)
                {
                    targetIntensity = Mathf.Lerp(initialLightIntensity, growingLightIntensity, intensityProgress);
                }
                else
                {
                    // Map 0.9-1.0 range to 0-1 for dramatic increase
                    float finalProgress = (progress - 0.9f) / 0.1f;
                    targetIntensity = Mathf.Lerp(growingLightIntensity, maxLightIntensity * finalLightIntensityMultiplier, finalProgress);
                }

                treeLight.intensity = targetIntensity;
            }
        }
        private void OnDestroy()
        {
            StopAllCoroutines();
            if (completionEffect != null && completionEffect.gameObject != null)
            {
                Destroy(completionEffect.gameObject);
            }
        }


        /// <summary>
        /// Gets the current growth progress (0-1)
        /// </summary>
        public float GetGrowthProgress()
        {
            return growthProgress;
        }

        /// <summary>
        /// Checks if the tree is currently in growing state
        /// </summary>
        public bool IsGrowing()
        {
            return isGrowing && !isFullyGrown;
        }

        /// <summary>
        /// Checks if growth is currently paused
        /// </summary>
        public bool IsPaused()
        {
            return isPaused;
        }

        /// <summary>
        /// Completes the growth process immediately
        /// </summary>
        public void CompleteGrowth()
        {
            if (!isComplete)
            {
                isGrowing = false;
                isComplete = true;
                StartCoroutine(CompletionSequence());
                parentPot?.OnTreeGrowthCompleted();
            }
        }

        /// <summary>
        /// Kills all shadow monsters when the tree is fully grown
        /// </summary>
        private void KillAllShadowMonsters()
        {
            // Find the ShadowMonsterSpawner
            ShadowMonsterSpawner spawner = FindFirstObjectByType<ShadowMonsterSpawner>();
            if (spawner != null)
            {
                // Stop the spawner from creating new monsters
                spawner.StopSpawning();

                // Find all active ShadowMonster objects in the scene
                ShadowMonster[] monsters = FindObjectsByType<ShadowMonster>(FindObjectsSortMode.None);
                foreach (ShadowMonster monster in monsters)
                {
                    // Kill/destroy each monster
                    Destroy(monster.gameObject);
                }

                Debug.Log($"TreeOfLight: Destroyed {monsters.Length} shadow monsters");
            }
            else
            {
                Debug.LogWarning("TreeOfLight: ShadowMonsterSpawner not found in scene");
            }
        }

        private IEnumerator BlindingLightEffect()
        {
            if (treeLight != null)
            {
                // Create a blinding flash
                float startIntensity = treeLight.intensity;
                float maxBlindingIntensity = maxLightIntensity * 2f;

                // Quickly increase intensity for blinding effect
                float flashDuration = 0.5f;
                float elapsed = 0f;

                while (elapsed < flashDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / flashDuration;
                    treeLight.intensity = Mathf.Lerp(startIntensity, maxBlindingIntensity, t);
                    yield return null;
                }

                // Gradually return to normal brightness
                elapsed = 0f;
                float fadeDuration = 1.5f;

                while (elapsed < fadeDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / fadeDuration;
                    treeLight.intensity = Mathf.Lerp(maxBlindingIntensity, maxLightIntensity, t);
                    yield return null;
                }

                // Start pulsing light effect
                StartCoroutine(PulseLight());
            }
        }

        private IEnumerator PulseLight()
        {
            if (treeLight != null)
            {
                float baseIntensity = maxLightIntensity;

                while (isFullyGrown)
                {
                    // Create pulsing effect
                    float pulseFactor = Mathf.Sin(Time.time * finalLightPulseSpeed) * finalLightPulseAmount + 1.0f;
                    treeLight.intensity = baseIntensity * pulseFactor;
                    yield return null;
                }
            }
        }

        private IEnumerator CompletionSequence()
        {
            if (completionEffect != null)
            {
                completionEffect.Play();
                Debug.Log("Particle Effect Played!");
            }
            else
            {
                Debug.LogError("Completion Effect is null!");
            }

            yield return new WaitForSeconds(5f);
            Debug.Log("Sequence Complete");
            Destroy(completionEffect.gameObject);
        }

        /// <summary>
        /// Called when the tree takes damage
        /// </summary>
        public void OnHit()
        {
            // Play hit particle effect
            if (hitParticleEffect != null)
            {
                hitParticleEffect.Play();
            }

            // Trigger hit event
            onTreeHit?.Invoke();
        }

        /// <summary>
        /// Called when tree health is depleted
        /// </summary>
        private void OnHealthDepleted()
        {
            Break();
        }

        /// <summary>
        /// Breaks the tree using particle effects similar to JiggleBreakableBigObject
        /// </summary>
        public void Break()
        {
            // Don't allow breaking twice
            if (!enabled)
                return;

            // Play break particle effect
            if (breakParticleEffect != null)
            {
                breakParticleEffect.Play();
            }

            // Play breaking sound
            if (breakSound != null)
            {
                AudioSource.PlayClipAtPoint(breakSound, transform.position);
            }

            // Hide the intact model
            if (intactModel != null)
            {
                intactModel.SetActive(false);
            }

            // Trigger broken event
            onTreeBroken?.Invoke();

            TreeOfLightPot parentPot = this.parentPot;
            this.parentPot = null;

            // Notify parent pot if it exists
            if (parentPot != null)
            {
                parentPot.OnTreeBroken();
                parentPot.Break();
            }

            // Disable this component
            this.enabled = false;

            // Optional: Destroy this object after a delay
            Destroy(gameObject, destroyDelayAfterBreak);
        }

        /// <summary>
        /// Validates a scale value to prevent errors
        /// </summary>
        private bool IsValidScale(Vector3 scale)
        {
            // Check for NaN or infinity
            if (float.IsNaN(scale.x) || float.IsInfinity(scale.x) ||
                float.IsNaN(scale.y) || float.IsInfinity(scale.y) ||
                float.IsNaN(scale.z) || float.IsInfinity(scale.z))
            {
                Debug.LogWarning("Invalid scale detected: " + scale);
                return false;
            }

            return true;
        }
    }
}
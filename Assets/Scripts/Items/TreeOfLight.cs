using System;
using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using Items;
using Core;
using UnityEngine.XR.Interaction.Toolkit;

namespace Items
{
    public class TreeOfLight : MonoBehaviour
    {
        [Header("Light Settings")]
        [SerializeField] private Light treeLight;
        [SerializeField] private float maxLightIntensity = 2f;
        [SerializeField] private float initialLightIntensity = 0f;
        [SerializeField] private float growingLightIntensity = 0.5f;
        [SerializeField] private float finalLightIntensityMultiplier = 3f;
        [SerializeField] private float finalLightPulseSpeed = 1f;
        [SerializeField] private float finalLightPulseAmount = 0.2f;
        
        [Header("Light Wave Effect")]
        [SerializeField] private GameObject lightWavePrefab;
        [SerializeField] private float lightWaveRadius = 30f;
        [SerializeField] private float lightWaveSpeed = 10f;
        [SerializeField] private float lightWaveDuration = 3f;
        [SerializeField] private Color lightWaveColor = Color.white;
        
        [Header("Growth Settings")]
        [SerializeField] private Vector3 startScale = Vector3.zero;
        [SerializeField] private Vector3 targetScale = Vector3.one;
        [SerializeField] private AnimationCurve growthCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private AnimationCurve lightIntensityCurve;
        [SerializeField] private float growthDuration = 60f;
        
        [Header("Completion Effect")]
        [SerializeField] private ParticleSystem completionEffect;
        [SerializeField] private float completionEffectDuration = 3f;
        [SerializeField] private GameObject keyPrefab;
        [SerializeField] private bool destroyPotOnCompletion = true;
        
        [Header("Events")]
        public UnityEvent OnGrowthStart;
        public UnityEvent OnGrowthPaused;
        public UnityEvent OnGrowthResumed;
        public UnityEvent OnGrowthComplete;
        public UnityEvent OnLightWaveCreated;
        
        // State tracking
        private bool isFullyGrown = false;
        private bool isGrowing = false;
        private bool isPaused = false;
        private float elapsedGrowthTime = 0f;
        private float growthProgress = 0f;
        private float animationSpeed = 1f;
        
        // Coroutines
        private Coroutine growthRoutine;
        private Coroutine finalEffectRoutine;
        
        // Components
        private Animator animator;
        private TreeOfLightPot parentPot;
        private HealthComponent healthComponent;
        
        // Animation parameters
        private static readonly int GrowTrigger = Animator.StringToHash("Grow");
        private static readonly int GrowthSpeed = Animator.StringToHash("GrowthSpeed");

        // Callbacks
        private Action onGrowthCompleteCallback;

        private void Awake()
        {
            // Get components
            animator = GetComponent<Animator>();
            healthComponent = GetComponent<HealthComponent>();
            
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

        private void OnEnable()
        {
            // Subscribe to health events if health component exists
            if (healthComponent != null)
            {
                healthComponent.OnDamaged.AddListener(OnHealthDamaged);
                healthComponent.OnDeath.AddListener(OnHealthDepleted);
            }
        }

        private void OnDisable()
        {
            // Unsubscribe from health events
            if (healthComponent != null)
            {
                healthComponent.OnDamaged.RemoveListener(OnHealthDamaged);
                healthComponent.OnDeath.RemoveListener(OnHealthDepleted);
            }
        }

        private void Update()
        {
            // Update growth if active and not paused
            if (isGrowing && !isPaused)
            {
                elapsedGrowthTime += Time.deltaTime * animationSpeed;
                growthProgress = Mathf.Clamp01(elapsedGrowthTime / growthDuration);
                
                UpdateGrowthProgress(growthProgress);
                
                // Notify parent pot about growth progress
                if (parentPot != null)
                {
                    parentPot.UpdateTreeGrowthProgress(growthProgress);
                }
                
                if (growthProgress >= 1.0f && !isFullyGrown)
                {
                    CompleteGrowth();
                }
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
        public void StartGrowth()
        {
            // Return if already growing or fully grown
            if (isGrowing || isFullyGrown)
                return;
                
            Debug.Log("TreeOfLight: Starting growth process");

            // Set growth state
            isGrowing = true;
            isPaused = false;
            elapsedGrowthTime = 0f;
            growthProgress = 0f;
            
            // Start growth animation
            if (animator != null)
            {
                animator.SetTrigger(GrowTrigger);
                animator.SetFloat(GrowthSpeed, animationSpeed);
            }
            
            // Notify listeners
            OnGrowthStart?.Invoke();
            
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

        /// <summary>
        /// Starts growing with specified duration and completion callback
        /// </summary>
        public void StartGrowing(float duration, Action onGrowthCompleteCallback = null)
        {
            // Set the callback to be triggered when growth completes
            if (onGrowthCompleteCallback != null)
            {
                this.onGrowthCompleteCallback = onGrowthCompleteCallback;
            }
            
            // Override growth duration if needed
            this.growthDuration = duration > 0 ? duration : this.growthDuration;
            
            // Use the existing growth method
            StartGrowth();
        }
        
        private IEnumerator GrowthRoutine()
        {
            while (isGrowing && growthProgress < 1.0f)
            {
                if (!isPaused)
                {
                    UpdateGrowthProgress(growthProgress);
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
                
                OnGrowthPaused?.Invoke();
                
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
                
                OnGrowthResumed?.Invoke();
                
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
            if (isGrowing && !isFullyGrown)
            {
                isGrowing = false;
                isPaused = false;
                isFullyGrown = true;
                
                // Set to final scale
                transform.localScale = targetScale;
                
                // Start blinding light effect
                StartCoroutine(BlindingLightEffect());
                
                // Create light wave that affects monsters
                StartCoroutine(CreateLightWaveEffect());
                
                // Trigger completion event
                OnGrowthComplete?.Invoke();
                
                // Execute callback if set
                if (onGrowthCompleteCallback != null)
                {
                    onGrowthCompleteCallback.Invoke();
                }
                
                // Notify parent pot that growth is complete
                if (parentPot != null)
                {
                    parentPot.OnTreeGrowthCompleted();
                }
                
                // Start the sequence to drop key and handle completion
                StartCoroutine(CompletionSequence());
            }
        }
        
        private IEnumerator BlindingLightEffect()
        {
            if (treeLight != null)
            {
                // Create a blinding flash
                float startIntensity = treeLight.intensity;
                float peakIntensity = maxLightIntensity * finalLightIntensityMultiplier * 2;
                float duration = 1.5f;
                float elapsed = 0f;
                
                // Ramp up
                while (elapsed < duration * 0.3f)
                {
                    elapsed += Time.deltaTime;
                    float progress = elapsed / (duration * 0.3f);
                    treeLight.intensity = Mathf.Lerp(startIntensity, peakIntensity, progress);
                    yield return null;
                }
                
                // Hold
                treeLight.intensity = peakIntensity;
                yield return new WaitForSeconds(duration * 0.4f);
                
                // Ramp down
                elapsed = 0f;
                while (elapsed < duration * 0.3f)
                {
                    elapsed += Time.deltaTime;
                    float progress = elapsed / (duration * 0.3f);
                    treeLight.intensity = Mathf.Lerp(peakIntensity, maxLightIntensity, progress);
                    yield return null;
                }
                
                // Set final intensity
                treeLight.intensity = maxLightIntensity;
                
                // Start pulsing effect
                if (finalEffectRoutine != null)
                {
                    StopCoroutine(finalEffectRoutine);
                }
                finalEffectRoutine = StartCoroutine(PulseLight());
            }
        }
        
        private IEnumerator CreateLightWaveEffect()
        {
            Debug.Log("Creating light wave effect to affect monsters");
            
            // Create wave effect if we have a prefab
            if (lightWavePrefab != null)
            {
                GameObject lightWave = Instantiate(lightWavePrefab, transform.position, Quaternion.identity);
                var waveEffect = lightWave.GetComponent<LightWaveEffect>();
                
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
            
            // Affect all monsters in the scene
            yield return new WaitForSeconds(0.5f); // Wait a moment for the wave to expand
            
            // Find all shadow monsters and damage them
            // Using GameObject.FindObjectsOfType for better compatibility
            var monsters = GameObject.FindObjectsOfType<MonoBehaviour>()
                .Where(m => m.GetType().Name.Contains("ShadowMonster"))
                .ToArray();
                
            foreach (var monster in monsters)
            {
                // Use reflection to safely call TakeDamage if it exists
                var takeDamageMethod = monster.GetType().GetMethod("TakeDamage");
                if (takeDamageMethod != null)
                {
                    takeDamageMethod.Invoke(monster, new object[] { float.MaxValue });
                }
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
        
        private IEnumerator CompletionSequence()
        {
            // Wait a moment for the light effects to complete
            yield return new WaitForSeconds(2f);
            
            // Play completion effect
            if (completionEffect != null)
            {
                ParticleSystem effect = Instantiate(completionEffect, transform.position, Quaternion.identity);
                Destroy(effect.gameObject, completionEffectDuration);
            }
            
            // Drop key item
            if (keyPrefab != null)
            {
                Vector3 dropPosition = transform.position + Vector3.up * 0.5f;
                GameObject key = Instantiate(keyPrefab, dropPosition, Quaternion.identity);
                
                // Add force to make it drop naturally
                Rigidbody keyRb = key.GetComponent<Rigidbody>();
                if (keyRb != null)
                {
                    keyRb.AddForce(Vector3.up * 2f, ForceMode.Impulse);
                    keyRb.AddTorque(new Vector3(UnityEngine.Random.Range(-1f, 1f), 
                                                UnityEngine.Random.Range(-1f, 1f), 
                                                UnityEngine.Random.Range(-1f, 1f)) * 0.5f, ForceMode.Impulse);
                }
            }
            
            // Wait a moment before destroying
            yield return new WaitForSeconds(1f);
            
            // Destroy tree and pot if configured to do so
            if (parentPot != null)
            {
                // Let the pot know to prepare for destruction
                parentPot.PrepareForDestruction();
                
                // Only destroy the pot if that option is enabled
                if (destroyPotOnCompletion)
                {
                    Destroy(parentPot.gameObject);
                }
            }
            
            // Destroy the tree
            Destroy(gameObject);
        }

        // Health component event handlers
        private void OnHealthDamaged(float damage)
        {
            // You can add visual effects for damage here if needed
            Debug.Log($"Tree took {damage} damage");
            
            // Notify parent pot
            if (parentPot != null)
            {
                parentPot.OnTreeDamaged(damage);
            }
        }
        
        private void OnHealthDepleted()
        {
            // Tree is destroyed by something other than completion
            Debug.Log("Tree of Light was destroyed before completion");
            
            // Clean up
            if (finalEffectRoutine != null)
            {
                StopCoroutine(finalEffectRoutine);
            }
            
            if (growthRoutine != null)
            {
                StopCoroutine(growthRoutine);
            }
            
            // Notify parent pot about destruction
            if (parentPot != null)
            {
                parentPot.OnTreeDestroyed();
            }
            
            // Destroy the tree
            Destroy(gameObject);
        }
        
        private bool IsValidScale(Vector3 scale)
        {
            return !float.IsNaN(scale.x) && !float.IsNaN(scale.y) && !float.IsNaN(scale.z) &&
                   !float.IsInfinity(scale.x) && !float.IsInfinity(scale.y) && !float.IsInfinity(scale.z) &&
                   scale.x >= 0 && scale.y >= 0 && scale.z >= 0;
        }
    }
}
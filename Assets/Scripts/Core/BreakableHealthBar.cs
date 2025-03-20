using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;

namespace Core
{
    public class BreakableHealthBar : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private Image healthFillImage;
        [SerializeField] private TextMeshProUGUI healthText;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Settings")]
        [SerializeField] private float displayDuration = 3f;
        [SerializeField] private float fadeOutDuration = 1f;
        [SerializeField] private Vector3 offset = new Vector3(0, 1.5f, 0);

        private MonoBehaviour targetObject;
        private float displayTimer;
        private float initialHealth;
        private Transform mainCamera;

        private void Awake()
        {
            // Get the parent object
            var parent = transform.parent;
            if (parent != null)
            {
                // Try to find any component that has a GetCurrentHealth method
                var components = parent.GetComponents<MonoBehaviour>();
                foreach (var component in components)
                {
                    var getCurrentHealthMethod = component.GetType().GetMethod("GetCurrentHealth");
                    if (getCurrentHealthMethod != null)
                    {
                        Initialize(component);
                        return;
                    }
                }
                Debug.LogError($"BreakableHealthBar: Parent object {parent.name} does not have a component with GetCurrentHealth method!");
            }
            else
            {
                Debug.LogError("BreakableHealthBar: No parent object found!");
            }
        }

        public void Initialize(MonoBehaviour target)
        {
            Debug.Log($"BreakableHealthBar: Initialize called for {gameObject.name}");
            
            // Validate UI components
            if (healthFillImage == null)
            {
                Debug.LogError($"BreakableHealthBar: healthFillImage is not assigned on {gameObject.name}!");
                return;
            }
            if (healthText == null)
            {
                Debug.LogError($"BreakableHealthBar: healthText is not assigned on {gameObject.name}!");
                return;
            }
            if (canvasGroup == null)
            {
                Debug.LogError($"BreakableHealthBar: canvasGroup is not assigned on {gameObject.name}!");
                return;
            }

            targetObject = target;
            
            // Get initial health using reflection
            var getCurrentHealthMethod = target.GetType().GetMethod("GetCurrentHealth");
            if (getCurrentHealthMethod != null)
            {
                initialHealth = (float)getCurrentHealthMethod.Invoke(target, null);
                Debug.Log($"BreakableHealthBar: Initial health set to {initialHealth}");
            }
            else
            {
                Debug.LogError($"BreakableHealthBar: Target object {target.name} does not have a GetCurrentHealth method!");
                return;
            }
            
            // Subscribe to events
            var onDamageEvent = target.GetType().GetField("onDamage")?.GetValue(target) as UnityEvent;
            if (onDamageEvent != null)
            {
                onDamageEvent.AddListener(OnDamageTaken);
                Debug.Log($"BreakableHealthBar: Subscribed to onDamage event");
            }
            else
            {
                Debug.LogError($"BreakableHealthBar: Target object {target.name} does not have an onDamage event!");
                return;
            }
            
            var onBreakEvent = target.GetType().GetField("onBreak")?.GetValue(target) as UnityEvent;
            if (onBreakEvent != null)
            {
                onBreakEvent.AddListener(OnBreak);
                Debug.Log($"BreakableHealthBar: Subscribed to onBreak event");
            }
            
            // Hide initially
            canvasGroup.alpha = 0;
            
            // Cache camera reference
            mainCamera = Camera.main?.transform;
            if (mainCamera == null)
            {
                Debug.LogWarning("BreakableHealthBar: No main camera found!");
            }
            else
            {
                Debug.Log("BreakableHealthBar: Main camera reference cached");
            }
        }

        private void Update()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main?.transform;
                if (mainCamera == null) return;
            }

            // Face the camera
            transform.rotation = mainCamera.rotation;

            // Update position relative to parent
            transform.position = transform.parent.position + offset;

            // Handle fade out
            if (displayTimer > 0)
            {
                displayTimer -= Time.deltaTime;
                if (displayTimer <= fadeOutDuration)
                {
                    canvasGroup.alpha = displayTimer / fadeOutDuration;
                }
            }
        }

        public void OnDamageTaken()
        {
            Debug.Log($"BreakableHealthBar: Damage taken on {gameObject.name}");
            
            // Get current health using reflection
            var getCurrentHealthMethod = targetObject.GetType().GetMethod("GetCurrentHealth");
            if (getCurrentHealthMethod != null)
            {
                float currentHealth = (float)getCurrentHealthMethod.Invoke(targetObject, null);
                float healthPercentage = currentHealth / initialHealth;

                // Update UI
                healthFillImage.fillAmount = healthPercentage;
                healthText.text = $"{Mathf.RoundToInt(currentHealth)}/{Mathf.RoundToInt(initialHealth)}";

                // Show and reset timer
                canvasGroup.alpha = 1;
                displayTimer = displayDuration;
                Debug.Log($"BreakableHealthBar: Health updated to {currentHealth}/{initialHealth}");
            }
        }

        public void OnBreak()
        {
            Debug.Log($"BreakableHealthBar: Break event received on {gameObject.name}");
            // Update UI to show zero health
            healthFillImage.fillAmount = 0;
            healthText.text = "0/" + Mathf.RoundToInt(initialHealth);
            
            // Start fade out
            displayTimer = fadeOutDuration;
        }

        private void OnDestroy()
        {
            if (targetObject != null)
            {
                var onDamageEvent = targetObject.GetType().GetField("onDamage")?.GetValue(targetObject) as UnityEvent;
                if (onDamageEvent != null)
                {
                    onDamageEvent.RemoveListener(OnDamageTaken);
                }
                
                var onBreakEvent = targetObject.GetType().GetField("onBreak")?.GetValue(targetObject) as UnityEvent;
                if (onBreakEvent != null)
                {
                    onBreakEvent.RemoveListener(OnBreak);
                }
                Debug.Log($"BreakableHealthBar: Cleaned up event listeners on {gameObject.name}");
            }
        }
    }
} 
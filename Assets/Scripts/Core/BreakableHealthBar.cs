using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

        private BreakableObject breakableObject;
        private float displayTimer;
        private float initialHealth;
        private Transform mainCamera;

        public void Initialize(BreakableObject breakable)
        {
            Debug.Log($"BreakableHealthBar: Initialize called for {gameObject.name}");
            breakableObject = breakable;
            initialHealth = breakable.GetCurrentHealth();
            
            // Subscribe to events
            breakable.onDamage.AddListener(OnDamageTaken);
            breakable.onBreak.AddListener(OnBreak);
            
            // Hide initially
            canvasGroup.alpha = 0;
            
            // Cache camera reference
            mainCamera = Camera.main?.transform;
            if (mainCamera == null)
            {
                Debug.LogWarning("BreakableHealthBar: No main camera found!");
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

        private void OnDamageTaken()
        {
            Debug.Log($"BreakableHealthBar: Damage taken on {gameObject.name}");
            float currentHealth = breakableObject.GetCurrentHealth();
            float healthPercentage = currentHealth / initialHealth;

            // Update UI
            healthFillImage.fillAmount = healthPercentage;
            healthText.text = $"{Mathf.RoundToInt(currentHealth)}/{Mathf.RoundToInt(initialHealth)}";

            // Show and reset timer
            canvasGroup.alpha = 1;
            displayTimer = displayDuration;
            Debug.Log($"BreakableHealthBar: Health updated to {currentHealth}/{initialHealth}");
        }

        private void OnBreak()
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
            if (breakableObject != null)
            {
                breakableObject.onDamage.RemoveListener(OnDamageTaken);
                breakableObject.onBreak.RemoveListener(OnBreak);
                Debug.Log($"BreakableHealthBar: Cleaned up event listeners on {gameObject.name}");
            }
        }
    }
} 
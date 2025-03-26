using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

namespace Items
{
    public class Portal : MonoBehaviour
    {
        [Header("Portal Settings")]
        [SerializeField] private string targetSceneName;
        [SerializeField] private float minScale = 0.1f;
        [SerializeField] private float maxScale = 1f;
        [SerializeField] private float scaleSpeed = 2f;
        [SerializeField] private float transitionDelay = 0.5f;

        [Header("Visual Effects")]
        [SerializeField] private ParticleSystem portalParticles;
        [SerializeField] private AudioClip portalSound;
        [SerializeField] private AudioSource audioSource;

        private Vector3 targetScale;
        private bool isScaling = false;
        private bool isTransitioning = false;

        private void Start()
        {
            // Start with minimum scale
            transform.localScale = Vector3.one * minScale;
            targetScale = Vector3.one * minScale;

            // Get audio source if not assigned
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            // Start particle system if available
            if (portalParticles != null)
            {
                portalParticles.Play();
            }
        }

        private void Update()
        {
            // Smoothly scale the portal
            if (isScaling)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * scaleSpeed);
            }
        }

        public void OnDoorUnlocked()
        {
            Debug.Log("Portal scaling up - door unlocked");
            targetScale = Vector3.one * maxScale;
            isScaling = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!isTransitioning && other.CompareTag("Player"))
            {
                StartCoroutine(TransitionToScene());
            }
        }

        private IEnumerator TransitionToScene()
        {
            isTransitioning = true;
            Debug.Log($"Starting transition to scene: {targetSceneName}");

            // Play transition sound if available
            if (portalSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(portalSound);
            }

            // Wait for transition delay
            yield return new WaitForSeconds(transitionDelay);

            // Load the target scene
            SceneManager.LoadScene(targetSceneName);
        }
    }
} 
using System.Collections;
using UnityEngine;

//using UnityEngine.SceneManagement;

namespace Items
{
    public class Bomb : MonoBehaviour
    {
        [SerializeField] private float delay = 4f;
        [SerializeField] private float particleDelay = 2f;
        [SerializeField] private ParticleSystem explosionEffectPrefab;
        [SerializeField] private float scaleDuration = 2f;
        [SerializeField] private Vector3 maxScale = Vector3.one * 5f;

        private bool isArmed;

        void Start()
        {
            ArmBomb(); // Immediately arm the bomb when it's in the scene
        }

        private void ArmBomb()
        {
            if (!isArmed)
            {
                isArmed = true;
                StartCoroutine(BombSequence());
            }
        }

        private IEnumerator BombSequence()
        {
            // Start scaling up immediately
            StartCoroutine(ScaleBomb());

            // Wait for the initial particle delay
            yield return new WaitForSeconds(particleDelay);

            // Play the particle effect
            if (explosionEffectPrefab != null)
            {
                ParticleSystem explosionEffect = Instantiate(explosionEffectPrefab, null, true);
                explosionEffect.transform.position = transform.position; // Set position
                explosionEffect.Play(); // Start the particle system
            }
            else
            {
                Debug.LogError("explosionEffect is null!  Make sure it's assigned in the Inspector.");
            }


            // Wait for the remaining time
            if (delay > particleDelay)
            {
                yield return new WaitForSeconds(delay - particleDelay);
            }
            else
            {
                Debug.LogWarning("Particle Delay is same or more than total delay");
            }
            RestartGame();
        }

        private IEnumerator ScaleBomb()
        {
            float timeElapsed = 0f;
            Vector3 startScale = transform.localScale;

            if (scaleDuration <= 0)
            {
                Debug.LogWarning("Scale duration is 0 or less. Scaling will not occur.");
                yield break; // Exit the coroutine
            }

            while (timeElapsed < scaleDuration)
            {
                transform.localScale = Vector3.Lerp(startScale, maxScale, timeElapsed / scaleDuration);
                timeElapsed += Time.deltaTime;
                yield return null;
            }

            transform.localScale = maxScale;
        }

        private void RestartGame()
        {
            SceneTransitionManager.Singleton.GoToSceneAsync(0);
        }
    }
}

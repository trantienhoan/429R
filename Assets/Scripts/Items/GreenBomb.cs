using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class GreenBomb : MonoBehaviour
{
    [SerializeField] private float delay = 4f;
    [SerializeField] private float particleDelay = 2f;
    [SerializeField] private ParticleSystem explosionEffectPrefab;
    [SerializeField] private float scaleDuration = 2f;
    [SerializeField] private Vector3 maxScale = Vector3.one * 5f;
    [SerializeField] private int sceneToLoad = 2; // Scene 2: Mini game scene

    private bool _isArmed = false;
    private Vector3 _initialScale;

    void Start()
    {
        _initialScale = transform.localScale;
        ArmBomb(); // Immediately arm the bomb when it's in the scene
    }

    private void ArmBomb()
    {
        if (!_isArmed)
        {
            _isArmed = true;
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
            ParticleSystem explosionEffect = Instantiate(explosionEffectPrefab);
            explosionEffect.transform.position = transform.position; // Set position
            explosionEffect.transform.SetParent(null); // Detach from parent
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
        MoveToScene();
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

        transform.localScale = maxScale; // Ensure final scale is set
    }


    private void MoveToScene()
    {
        SceneManager.LoadScene(sceneToLoad);
    }
}
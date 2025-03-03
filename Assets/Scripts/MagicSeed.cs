using UnityEngine;
using System.Collections;

public class MagicSeed : MonoBehaviour
{
    public ParticleSystem hintParticles; // Assign a particle system in Inspector
    private SeedSpawnManager manager;
    private bool isPickedUp = false;

    void Start()
    {
        StartCoroutine(HintSequence());
    }

    public void SetManager(SeedSpawnManager m)
    {
        manager = m;
    }

    IEnumerator HintSequence()
    {
        yield return new WaitForSeconds(180f); // 3 minutes
        PlayHint();
        yield return new WaitForSeconds(120f); // 2 minutes
        PlayHint();
        yield return new WaitForSeconds(60f); // 1 minute
        PlayHint();
        yield return new WaitForSeconds(30f); // 30 seconds
        PlayHint();
        yield return new WaitForSeconds(15f); // 15 seconds
        PlayHint();
        yield return new WaitForSeconds(5f); // 5 seconds
        PlayHint();
        InvokeRepeating(nameof(PlayHint), 0f, 5f); // Every 5 seconds until picked up
    }

    void PlayHint()
    {
        if (!isPickedUp) hintParticles.Play();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !isPickedUp) // Tag your VR hand as "Player"
        {
            isPickedUp = true;
            hintParticles.Stop();
            CancelInvoke(); // Stop hints
            FindObjectOfType<FairyController>().SummonFairy(transform.position);
            gameObject.SetActive(false); // Hide seed
        }
    }
}
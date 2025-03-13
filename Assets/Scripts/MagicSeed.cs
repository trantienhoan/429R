using UnityEngine;
using System.Collections;

public class MagicSeed : MonoBehaviour
{
    public ParticleSystem hintParticles; // Assign a particle system in Inspector
    private SeedSpawnManager manager;
    private bool isPickedUp = false;
    private FairyController fairyController;

    void Start()
    {
        fairyController = FindObjectOfType<FairyController>();

        if (fairyController == null)
        {
            Debug.LogError("⚠️ MagicSeed: No FairyController found in the scene! Make sure it's in the hierarchy.");
        }

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
        Debug.Log("🔹 MagicSeed: OnTriggerEnter triggered with " + other.gameObject.name);

        if (other.CompareTag("Player") && !isPickedUp)
        {
            Debug.Log("✅ MagicSeed: Player picked up the seed!");

            isPickedUp = true;
            StopCoroutine(HintSequence());
            CancelInvoke();

            if (hintParticles != null)
            {
                Debug.Log("🌟 MagicSeed: Stopping hint particles.");
                hintParticles.Stop();
            }

            if (fairyController != null)
            {
                Debug.Log("🧚 MagicSeed: Summoning Fairy at " + transform.position);
                fairyController.SummonFairy(transform.position);
            }
            else
            {
                Debug.LogWarning("⚠️ MagicSeed: FairyController not found!");
            }

            // 🔥 Force release from XR system
            var interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.XRGrabInteractable>();
            if (interactable != null && interactable.isSelected)
            {
                Debug.Log("🎮 MagicSeed: Forcing release from XR system.");
                interactable.interactionManager.SelectExit(interactable.firstInteractorSelecting, interactable);
                interactable.enabled = false; // Prevent re-grabbing
            }
            else
            {
                Debug.LogWarning("⚠️ MagicSeed: No XRGrabInteractable found!");
            }

            // ✅ Ensure rigidbody doesn't interfere
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                Debug.Log("🔧 MagicSeed: Adjusting Rigidbody properties.");
                rb.isKinematic = false;
                rb.useGravity = true;
            }

            Debug.Log("🚀 MagicSeed: Removing object.");
            Destroy(gameObject); // Completely remove seed
        }
    }
}
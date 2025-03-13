using UnityEngine;
using System.Collections;

public class MagicSeed : MonoBehaviour
{
    public ParticleSystem hintParticles; // Assign a particle system in Inspector
    private SeedSpawnManager manager;
    private bool isPickedUp = false;
    private FairyController fairyController => manager.fairyController;

    // 🌟 Add references for scene effects
    public Light directionalLight; // Assign the Directional Light in Inspector
    public Material emissionMaterial; // Assign the emission material in Inspector
    public Transform wall000, wall001, wall002, wall003, ceiling; // Assign walls & ceiling

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

            // 🌟 Start all scene effects
            StartCoroutine(DarkenLight());
            StartCoroutine(MoveWalls());

            Destroy(gameObject); // Completely remove seed
        }
    }
    // 🌟 Coroutine to darken light and adjust temperature
    IEnumerator DarkenLight()
    {
        float duration = 5f; // Time to complete the transition
        float timer = 0f;
        Color startColor = directionalLight.color;
        Color targetColor = Color.black; // Darken filter color
        float startTemp = directionalLight.colorTemperature;
        float targetTemp = 1f; // Drop temperature

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = timer / duration;
            directionalLight.color = Color.Lerp(startColor, targetColor, t);
            directionalLight.colorTemperature = Mathf.Lerp(startTemp, targetTemp, t);
            yield return null;
        }
    }

    // 🌟 Coroutine to move walls in different directions
    IEnumerator MoveWalls()
    {
        float duration = 7f; // Time to complete the movement
        float distance = 7f; // Distance each wall moves
        float timer = 0f;

        Vector3 startWall000 = wall000.position;
        Vector3 startWall001 = wall001.position;
        Vector3 startWall002 = wall002.position;
        Vector3 startWall003 = wall003.position;
        Vector3 startCeiling = ceiling.position;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = timer / duration;

            wall000.position = Vector3.Lerp(startWall000, startWall000 + Vector3.forward * distance, t);
            wall001.position = Vector3.Lerp(startWall001, startWall001 + Vector3.right * distance, t);
            wall002.position = Vector3.Lerp(startWall002, startWall002 + Vector3.back * distance, t);
            wall003.position = Vector3.Lerp(startWall003, startWall003 + Vector3.left * distance, t);
            ceiling.position = Vector3.Lerp(startCeiling, startCeiling + Vector3.up * distance, t);

            yield return null;
        }
    }
}
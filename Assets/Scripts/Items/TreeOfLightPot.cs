using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using System.Collections;

public class TreeOfLightPot : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject treeModel;
    [SerializeField] private ParticleSystem growthParticles;
    
    [Header("Growth Settings")]
    [SerializeField] private float treeGrowthDuration = 5f;
    [SerializeField] private float seedShrinkDuration = 2f;
    
    [SerializeField] private UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor socketInteractor;
    private bool seedPlaced = false;
    
    private bool isGrowing = false;
    
    private void Awake()
    {
        // Get the socket interactor directly
        socketInteractor = GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor>();
        if (socketInteractor == null)
        {
            Debug.LogError("TreeOfLightPot: No XR Socket Interactor found on this object or its children!");
        }
        
        // Ensure tree is initially hidden
        if (treeModel != null)
            treeModel.SetActive(false);
        else
            Debug.LogWarning("TreeOfLightPot: No tree model assigned!");
            
        Debug.Log("TreeOfLightPot initialized");
    }
    
    private void OnEnable()
    {
        if (socketInteractor != null)
        {
            // Register for socket events
            socketInteractor.selectEntered.AddListener(OnSeedPlaced);
            Debug.Log("TreeOfLightPot: Socket event listeners registered");
        }
    }
    
    private void OnDisable()
    {
        if (socketInteractor != null)
        {
            socketInteractor.selectEntered.RemoveListener(OnSeedPlaced);
        }
    }
    
    private void OnSeedPlaced(SelectEnterEventArgs args)
    {
        if (seedPlaced) return; // Prevent multiple activations
    
        seedPlaced = true;
    
        // Get the selected object (seed)
        GameObject seed = args.interactableObject.transform.gameObject;
    
        // Start growth sequence
        StartCoroutine(GrowthSequence(seed));
    }
    
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("MagicalSeed") || other.CompareTag("Seed"))
        {
            // Implement seed sucking behavior
            StartCoroutine(SuckSeedIntoSocket(other.gameObject));
        }
    }
    private IEnumerator SuckSeedIntoSocket(GameObject seed)
    {
        // Disable seed's collider and rigidbody during animation
        Collider seedCollider = seed.GetComponent<Collider>();
        Rigidbody seedRigidbody = seed.GetComponent<Rigidbody>();
        if (seedCollider) seedCollider.enabled = false;
        if (seedRigidbody) seedRigidbody.isKinematic = true;
       
        // Animate the seed moving to the socket
        Vector3 startPos = seed.transform.position;
        Vector3 targetPos = transform.position + Vector3.up * 0.3f; // Adjust based on your pot's socket position
        float duration = 0.5f;
        float elapsedTime = 0;
       
        while (elapsedTime < duration)
        {
            seed.transform.position = Vector3.Lerp(startPos, targetPos, elapsedTime / duration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
       
        // Place seed in final position and trigger any growth effects
        seed.transform.position = targetPos;
        seed.transform.SetParent(transform); // Parent to pot
       
        var dummyArgs = new SelectEnterEventArgs();
        // You might need to populate some properties of dummyArgs
        OnSeedPlaced(dummyArgs);
    }
    
    private IEnumerator GrowthSequence(GameObject seed)
    {
        if (isGrowing)
        {
            Debug.Log("TreeOfLightPot: Growth already in progress, ignoring");
            yield break;
        }
        
        isGrowing = true;
        Debug.Log("TreeOfLightPot: Starting growth sequence");
        
        // Step 1: Shrink seed
        if (seed != null)
        {
            Vector3 originalScale = seed.transform.localScale;
            
            // Shrink the seed over time
            float elapsed = 0f;
            while (elapsed < seedShrinkDuration)
            {
                float t = elapsed / seedShrinkDuration;
                seed.transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, t);
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            // Completely hide the seed
            seed.transform.localScale = Vector3.zero;
        }
        
        // Step 2: Play growth particles
        if (growthParticles != null)
        {
            growthParticles.Play();
            Debug.Log("TreeOfLightPot: Playing growth particles");
        }
        
        // Step 3: Grow the tree
        if (treeModel != null)
        {
            // Make tree visible but start at zero scale
            treeModel.SetActive(true);
            treeModel.transform.localScale = Vector3.zero;
            
            // Grow the tree over time
            float elapsed = 0f;
            while (elapsed < treeGrowthDuration)
            {
                float t = elapsed / treeGrowthDuration;
                treeModel.transform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, t);
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            // Ensure final scale is exactly Vector3.one
            treeModel.transform.localScale = Vector3.one;
            Debug.Log("TreeOfLightPot: Tree fully grown");
        }
        
        isGrowing = false;
    }
}
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
//using Core;
//using Items;

namespace Items
{
    public class TreeOfLightPot : MonoBehaviour
    {
        [Header("Prefabs and References")]
        [SerializeField] private GameObject existingTreeOfLight;
        [SerializeField] private ParticleSystem growthParticlePrefab; // Add this line
        [SerializeField] private UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor seedSocket;
        public Transform treeSpawnPoint;

        [Header("Timings")]
        [Tooltip("Duration of seed transition to spawn point.")]
        [SerializeField] private float seedMoveDuration = 1.0f;
        [SerializeField] private float deactivationDelay = 5f;

        [Header("Events")]
        public UnityEvent onGrowthStarted;
        public UnityEvent onGrowthCompleted;

        private bool isGrowing = false;
        private bool hasGrown = false;
        private MagicalSeed plantedSeed = null;
        private Quaternion initialSeedRotation;
        private Vector3 initialSeedScale;
        private GameObject treeInstance;
        private TreeOfLight tree;

        private ParticleSystem growthParticleInstance;

        private void Awake()
        {
            seedSocket = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor>();

            if (seedSocket != null)
            {
                seedSocket.selectEntered.AddListener(OnSeedSocketEntered);
            }

            if (existingTreeOfLight == null && treeSpawnPoint != null)
            {
                existingTreeOfLight = treeSpawnPoint.GetComponentInChildren<TreeOfLight>()?.gameObject;
                if (existingTreeOfLight == null)
                {
                    Debug.LogError("Could not find TreeOfLight as a child of treeSpawnPoint.  Please assign it in the Inspector.");
                }
            }

            // Initialize growthParticleInstance here
            if (growthParticlePrefab == null)
            {
                Debug.LogError("Growth Particle is not assigned in TreeOfLightPot!");
            }

            growthParticleInstance = growthParticlePrefab;
        }

        private void OnSeedSocketEntered(SelectEnterEventArgs args)
        {
            Debug.Log("OnSeedSocketEntered called!");

            if (args.interactableObject is UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable)
            {
                MagicalSeed seed = grabInteractable.GetComponent<MagicalSeed>();
                if (seed != null)
                {
                    Debug.Log("Seed found, calling PlaceSeed");
                    StartCoroutine(PlaceSeed(seed));  // Add this line to start the PlaceSeed coroutine
                }
                else
                {
                    Debug.LogWarning("Grabbed object is not a MagicalSeed!");
                }
            }
            else
            {
                Debug.LogWarning("Interactable is not XRGrabInteractable!");
            }
        }

        private IEnumerator PlaceSeed(MagicalSeed seed)
        {
            Debug.Log("PlaceSeed called!");

            // Save initial properties
            initialSeedRotation = seed.transform.rotation;
            initialSeedScale = seed.transform.localScale;

            // Get the XRGrabInteractable, Rigidbody, and Collider components
            UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable = seed.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            Rigidbody rb = seed.GetComponent<Rigidbody>();

            // Turn off interaction and set Rigidbody to kinematic IMMEDIATELY
            if (grabInteractable != null)
            {
                grabInteractable.enabled = false;
            }
            else
            {
                Debug.LogWarning("XRGrabInteractable is null on the seed!");
            }

            if (rb != null)
            {
                StartCoroutine(SetKinematicDelayed(rb));
            }
            else
            {
                Debug.LogWarning("Rigidbody is null on the seed!");
            }

            // Make sure seed is not re-planted
            seedSocket.socketActive = false;

            // Assign planted Seed to be used on other functions
            plantedSeed = seed;

            // Activate the tree FIRST
            treeInstance = ActivateExistingTree();
            if (treeInstance == null)
            {
                Debug.LogError("Failed to activate tree.  Aborting PlaceSeed.");
                yield break;
            }

            tree = treeInstance.GetComponent<TreeOfLight>(); // Get the TreeOfLight component
            if (tree != null)
            {
                tree.SetParentPot(this); //Connect the parent pot
                StartGrowthParticles(); // moved start particle here
                tree.BeginGrowth(); // Start tree growth, pass the growthSpeed
                onGrowthStarted.Invoke();
            }
            else
            {
                Debug.LogWarning("Tree component is null on instantiated tree!");
            }

            // Start the smooth movement coroutine AFTER activating the tree
            yield return StartCoroutine(MoveSeedToSpawnPoint(seed, seedMoveDuration));
        }

        private IEnumerator SetKinematicDelayed(Rigidbody rb)
        {
            yield return new WaitForSeconds(0.0001f); // A short delay
            rb.isKinematic = true;
        }

        private GameObject ActivateExistingTree()
        {
            if (existingTreeOfLight != null)
            {
                existingTreeOfLight.SetActive(true); // Activate the existing instance
                return existingTreeOfLight; // Return the existing instance
            }
            else
            {
                Debug.LogError("existingTreeOfLight is not assigned in TreeOfLightPot!");
                return null; // Return null in case of error
            }
        }

        private IEnumerator MoveSeedToSpawnPoint(MagicalSeed seed, float duration)
        {
            Vector3 startPosition = seed.transform.position;
            Quaternion startRotation = seed.transform.rotation;
            Vector3 targetScale = Vector3.zero; // Target scale is now zero

            float timeElapsed = 0;

            while (timeElapsed < duration)
            {
                float t = timeElapsed / duration;
                // Smooth the movement with SmoothStep
                t = t * t * (3f - 2f * t);

                seed.transform.position = Vector3.Lerp(startPosition, treeSpawnPoint.position, t);
                seed.transform.rotation = Quaternion.Slerp(startRotation, treeSpawnPoint.rotation, t);
                seed.transform.localScale = Vector3.Lerp(initialSeedScale, targetScale, t); // Smoothly scale towards targetScale

                timeElapsed += Time.deltaTime;
                yield return null;
            }

            // Ensure the final position, rotation, and scale are EXACTLY correct
            seed.transform.position = treeSpawnPoint.position;
            seed.transform.rotation = treeSpawnPoint.rotation;
            seed.transform.localScale = targetScale; // Apply target scale

            Renderer treeRenderer = treeInstance.GetComponentInChildren<Renderer>();
            if (treeRenderer != null)
            {
                treeRenderer.enabled = true; // Make the tree visible
            }
            else
            {
                Debug.LogWarning("Renderer component not found on the tree or its children!");
            }
        }

        // NEW: Method to handle the growth finished event
        private void HandleGrowthFinished()
        {
            Debug.Log("Tree growth finished!");
            onGrowthCompleted.Invoke();
            StartCoroutine(DeactivateTreeAfterDelay());
        }

        // NEW: Coroutine to deactivate the tree after a delay
        private IEnumerator DeactivateTreeAfterDelay()
        {
            Debug.Log($"Deactivating tree in {deactivationDelay} seconds...");
            yield return new WaitForSeconds(deactivationDelay);
            Debug.Log("Deactivating tree now.");
            StopGrowthParticles();
            if (treeInstance != null)
            {
                treeInstance.SetActive(false);
            }
            else
            {
                Debug.LogWarning("treeInstance is null, cannot deactivate.");
            }
        }

        private void StartGrowthParticles()
        {
            if (growthParticleInstance != null)
            {
                growthParticleInstance.gameObject.SetActive(true);
                growthParticleInstance.Play();
            }
            else
            {
                Debug.LogError("Growth Particle Instance not found!");
            }
        }

        private void StopGrowthParticles()
        {
            if (growthParticleInstance != null)
            {
                growthParticleInstance.Stop();
                growthParticleInstance.gameObject.SetActive(false);
            }
        }
    }
}

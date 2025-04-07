using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using Core;
using Items;

namespace Items
{
    public class TreeOfLightPot : MonoBehaviour
    {
        [Header("Prefabs and References")]
        [SerializeField] private GameObject treeOfLightPrefab;
        [SerializeField] private UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor seedSocket;
        public Transform treeSpawnPoint;
        [SerializeField] private ParticleSystem growthParticles;
        [Tooltip("Set to the ShadowMonsterSpawner from the scene to connect spawner and tree")]
        [SerializeField] private ShadowMonsterSpawner monsterSpawner;

        [Header("Timings")]
        [SerializeField] private float growthStartDelay = 0.5f;
        [Tooltip("Duration of seed transition to spawn point.")]
        [SerializeField] private float seedMoveDuration = 1.0f;

        [Header("Health Settings")]
        [SerializeField] private float potMaxHealth = 100f;

        [Header("Events")]
        public UnityEvent onGrowthStarted;
        public UnityEvent onGrowthCompleted;

        private bool isGrowing = false;
        private bool hasGrown = false;
        private MagicalSeed plantedSeed = null;
        private Quaternion initialSeedRotation;
        private Vector3 initialSeedScale;
        private UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor socketInteractor;
        private HealthComponent healthComponent;
        private ItemDropHandler itemDropHandler;
        private GameObject treeInstance;

        private void Awake()
        {
            seedSocket = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor>();
            Debug.Log("seedSocket is null: " + (seedSocket == null));

            healthComponent = GetComponent<HealthComponent>();
            if (healthComponent == null)
            {
                healthComponent = gameObject.AddComponent<HealthComponent>();
                healthComponent.SetMaxHealth(potMaxHealth);
                healthComponent.OnDeath += OnPotDeath;
            }

            itemDropHandler = GetComponent<ItemDropHandler>();
            if (itemDropHandler == null)
            {
                itemDropHandler = gameObject.AddComponent<ItemDropHandler>();
            }

            if (seedSocket != null)
            {
                seedSocket.selectEntered.AddListener(OnSeedSocketEntered);
                Debug.Log("Added listener to seedSocket.selectEntered");
            }
            else
            {
                Debug.LogError("XRSocketInteractor not found on this GameObject!");
            }
        }

        private void OnSeedSocketEntered(SelectEnterEventArgs args)
        {
            Debug.Log("OnSeedSocketEntered called!");

            if (args.interactableObject is UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable)
            {
                if (grabInteractable != null)
                {
                    MagicalSeed seed = grabInteractable.GetComponent<MagicalSeed>();
                    if (seed != null)
                    {
                        Debug.Log("Seed found, calling PlaceSeed");
                        StartCoroutine(PlaceSeed(seed)); // Start the coroutine HERE
                    }
                    else
                    {
                        Debug.LogWarning("Grabbed object is not a MagicalSeed!");
                    }
                }
                else
                {
                    Debug.LogWarning("GrabInteractable is null!");
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

            // Start the smooth movement coroutine
            yield return StartCoroutine(MoveSeedToSpawnPoint(seed, seedMoveDuration));

            // Instantiate the tree immediately after placing the seed
            treeInstance = InstantiateTree(); // Assign the returned treeInstance

            yield return null;

            // Set tree's parentPot
            if (treeInstance != null)
            {
                TreeOfLight tree = treeInstance.GetComponent<TreeOfLight>(); // Get the TreeOfLight component
                if (tree != null)
                {
                    tree.SetParentPot(this); //Connect the parent pot
                    tree.monsterSpawner = monsterSpawner; // Connect monster spawner
                    Debug.Log("Calling tree.BeginGrowth");
                    tree.BeginGrowth(seedMoveDuration); // Pass the duration
                    onGrowthStarted.Invoke();
                }
                else
                {
                    Debug.LogWarning("Tree component is null on instantiated tree!"); // ADD THIS LINE
                }
            }
            else
            {
                Debug.LogWarning("treeInstance is null!");
            }
            yield return null;
            //StartGrowth(); The growth are now being handled by BeginGrowth
        }


        private IEnumerator SetKinematicDelayed(Rigidbody rb)
        {
            yield return new WaitForSeconds(0.1f); // A short delay
            rb.isKinematic = true;
        }

        private GameObject InstantiateTree()
        {
            if (treeOfLightPrefab != null)
            {
                GameObject treeInstance = Instantiate(treeOfLightPrefab, treeSpawnPoint.position, Quaternion.identity);

                // Parent the tree to the treeSpawnPoint
                treeInstance.transform.SetParent(treeSpawnPoint, false);

                treeInstance.transform.localPosition = Vector3.zero; // keep tree within spawnPoint
                treeInstance.transform.localRotation = Quaternion.identity; // keep tree within spawnPoint
                return treeInstance; // Return the instance
            }
            else
            {
                Debug.LogError("TreeOfLight prefab is not assigned in TreeOfLightPot!");
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
            seed.gameObject.SetActive(false);
            seed.transform.SetParent(treeSpawnPoint);
        }

        private void OnPotDeath()
        {
            Debug.Log("TreeOfLightPot: has died!");
            // Handle what happens when the pot is destroyed.
            // May need to trigger item drop based on tree growth state using the new ItemDropHandler
            itemDropHandler.DropItems();
            Destroy(gameObject);
        }
        private void OnDestroy()
        {
            if (healthComponent != null)
            {
                healthComponent.OnDeath -= OnPotDeath;
            }
            if (treeInstance != null)
            {
                Destroy(treeInstance);
            }
        }
    }
}
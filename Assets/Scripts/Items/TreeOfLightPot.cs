using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using Core;

namespace Items
{
    public class TreeOfLightPot : MonoBehaviour
    {
        [Header("Prefabs and References")]
        [SerializeField] private GameObject existingTreeOfLight;
        [SerializeField] private ParticleSystem growthParticlePrefab;
        [SerializeField] private UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor seedSocket;
        public Transform treeSpawnPoint;

        [Header("Timings")]
        [Tooltip("Duration of seed transition to spawn point.")]
        [SerializeField] private float seedMoveDuration = 1.0f;
        [SerializeField] private float deactivationDelay = 5f;

        [Header("Events")]
        public UnityEvent onGrowthStarted;
        public UnityEvent onGrowthCompleted;

        private bool isGrowing;
        private bool hasGrown;
        private MagicalSeed plantedSeed;
        private Quaternion initialSeedRotation;
        private Vector3 initialSeedScale;
        private GameObject treeInstance;
        private TreeOfLight tree;
        private HealthComponent healthComponent;

        private ParticleSystem growthParticleInstance;

        private void Awake()
        {
            if (treeSpawnPoint == null)
            {
                Debug.LogError("Tree spawn point not assigned on TreeOfLightPot!");
                return;
            }

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
                    Debug.LogError("Could not find TreeOfLight as a child of treeSpawnPoint. Please assign it in the Inspector.");
                }
            }

            if (growthParticlePrefab == null)
            {
                Debug.LogError("Growth Particle is not assigned in TreeOfLightPot!");
            }
            else
            {
                growthParticleInstance = Instantiate(growthParticlePrefab, treeSpawnPoint.position, Quaternion.identity, transform);
            }

            healthComponent = GetComponent<HealthComponent>();
            if (healthComponent == null)
            {
                healthComponent = gameObject.AddComponent<HealthComponent>();
                Debug.LogWarning("HealthComponent is not assigned on TreeOfLightPot! Adding one automatically.");
            }
        }

        private void OnSeedSocketEntered(SelectEnterEventArgs args)
        {
            if (args.interactableObject is UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable)
            {
                MagicalSeed seed = grabInteractable.GetComponent<MagicalSeed>();
                if (seed != null)
                {
                    StartCoroutine(PlaceSeed(seed));
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
            initialSeedRotation = seed.transform.rotation;
            initialSeedScale = seed.transform.localScale;

            var grabInteractable = seed.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            var rb = seed.GetComponent<Rigidbody>();

            if (grabInteractable != null) grabInteractable.enabled = false;
            if (rb != null) StartCoroutine(SetKinematicDelayed(rb));

            seedSocket.socketActive = false;
            plantedSeed = seed;

            treeInstance = ActivateExistingTree();
            if (treeInstance == null) yield break;

            tree = treeInstance.GetComponent<TreeOfLight>();
            if (tree != null)
            {
                tree.SetParentPot(this);
                tree.OnPotDeath += HandlePotDeath;
                if (tree.healthComponent != null)
                {
                    tree.healthComponent.OnDeath += HandleTreeOfDeath;
                }
                else
                {
                    Debug.LogError("TreeOfLight's HealthComponent is null!");
                }
                StartGrowthParticles();
                tree.BeginGrowth();
                onGrowthStarted.Invoke();
            }

            yield return StartCoroutine(MoveSeedToSpawnPoint(seed, seedMoveDuration));
        }

        private IEnumerator SetKinematicDelayed(Rigidbody rb)
        {
            yield return new WaitForSeconds(0.0f);
            rb.isKinematic = true;
        }

        private GameObject ActivateExistingTree()
        {
            if (existingTreeOfLight != null)
            {
                existingTreeOfLight.SetActive(true);
                return existingTreeOfLight;
            }
            else
            {
                Debug.LogError("existingTreeOfLight is not assigned in TreeOfLightPot!");
                return null;
            }
        }

        private IEnumerator MoveSeedToSpawnPoint(MagicalSeed seed, float duration)
        {
            Vector3 startPosition = seed.transform.position;
            Quaternion startRotation = seed.transform.rotation;
            Vector3 targetPosition = treeSpawnPoint.position - Vector3.up * 0.1f;
            Vector3 targetScale = Vector3.zero;

            float timeElapsed = 0;

            while (timeElapsed < duration)
            {
                float t = timeElapsed / duration;
                t = t * t * (3f - 2f * t);

                seed.transform.position = Vector3.Lerp(startPosition, targetPosition, t);
                seed.transform.rotation = Quaternion.Slerp(startRotation, treeSpawnPoint.rotation, t);
                seed.transform.localScale = Vector3.Lerp(initialSeedScale, targetScale, t);

                timeElapsed += Time.deltaTime;
                yield return null;
            }

            seed.transform.position = targetPosition;
            seed.transform.rotation = treeSpawnPoint.rotation;
            seed.transform.localScale = targetScale;

            var treeRenderer = treeInstance.GetComponentInChildren<Renderer>();
            if (treeRenderer != null) treeRenderer.enabled = true;
        }

        private void StartGrowthParticles()
        {
            if (growthParticleInstance != null)
            {
                growthParticleInstance.gameObject.SetActive(true);
                growthParticleInstance.Play();
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

        private void HandlePotDeath()
        {
            StopGrowthParticles();
            if (tree != null) tree.OnPotDeath -= HandlePotDeath;
            if (healthComponent != null)
            {
                healthComponent.TakeDamage(healthComponent.MaxHealth, transform.position, gameObject);
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (tree != null && tree.healthComponent != null)
            {
                tree.healthComponent.OnDeath -= HandleTreeOfDeath;
                tree.OnPotDeath -= HandlePotDeath;
            }
        }

        private void HandleTreeOfDeath(HealthComponent health)
        {
            StopGrowthParticles();
            if (tree != null && tree.healthComponent != null)
            {
                tree.healthComponent.OnDeath -= HandleTreeOfDeath;
                tree.OnPotDeath -= HandlePotDeath;
            }

            if (healthComponent != null)
            {
                healthComponent.TakeDamage(healthComponent.MaxHealth, transform.position, gameObject);
                Destroy(gameObject);
            }
        }
    }
}

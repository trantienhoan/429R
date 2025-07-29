using System.Linq;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using Core;
using Enemies;

namespace Items
{
    public class TreeOfLightPot : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private float growthDuration = 5f; // Keep short for testing
        [SerializeField] private float maxHealth = 100f;

        [Header("Animation")]
        [SerializeField] private Animator animator;
        [SerializeField] private string growthAnimationName = "Seed_Grow";

        [Header("Prefabs and References")]
        [SerializeField] private ParticleSystem growthParticlePrefab;
        [SerializeField] private UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor seedSocket;
        [SerializeField] private Transform treeSpawnPoint;
        [SerializeField] private ShadowMonsterSpawner monsterSpawner;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip growthSound;

        [Header("Events")]
        public UnityEvent onGrowthStarted;
        public UnityEvent onGrowthCompleted;

        [SerializeField] private float seedMoveDuration = 1.0f;

        private bool isGrowing;
        private bool isComplete;
        private bool hasBegunGrowth;
        private MagicalSeed plantedSeed;
        private Quaternion initialSeedRotation;
        private Vector3 initialSeedScale;
        private HealthComponent healthComponent;
        private ParticleSystem growthParticleInstance;
        private ItemDropHandler itemDropHandler;

        public bool IsGrowing => isGrowing;

        private void Awake()
        {
            gameObject.tag = "TreeOfLight";
            Debug.Log($"TreeOfLightPot Awake: Tag = {gameObject.tag}, Active = {gameObject.activeInHierarchy}, Position = {transform.position}");

            if (treeSpawnPoint == null)
            {
                treeSpawnPoint = transform.Find("SeedSocketAttach")?.transform ?? transform;
                Debug.LogWarning($"TreeOfLightPot: treeSpawnPoint not assigned, using {(treeSpawnPoint == transform ? "self transform" : "SeedSocketAttach")}");
            }

            animator ??= transform.Find("Tree_Of_Light")?.GetComponent<Animator>();
            if (animator == null)
            {
                Debug.LogError("TreeOfLightPot: Animator is missing on Tree_Of_Light!");
                enabled = false;
                return;
            }
            animator.enabled = false;
            Debug.Log("TreeOfLightPot: Animator disabled on Awake");

            seedSocket = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor>();
            if (seedSocket != null)
            {
                seedSocket.selectEntered.AddListener(OnSeedSocketEntered);
            }
            else
            {
                Debug.LogError("TreeOfLightPot: XRSocketInteractor is missing!");
            }

            if (growthParticlePrefab == null)
            {
                Debug.LogError("TreeOfLightPot: Growth Particle is not assigned!");
            }
            else
            {
                growthParticleInstance = Instantiate(growthParticlePrefab, treeSpawnPoint.position, Quaternion.identity, transform);
                growthParticleInstance.gameObject.SetActive(false);
            }

            healthComponent = GetComponent<HealthComponent>();
            if (healthComponent == null)
            {
                healthComponent = gameObject.AddComponent<HealthComponent>();
                Debug.LogWarning("TreeOfLightPot: HealthComponent not assigned! Adding one automatically.");
            }
            healthComponent.SetMaxHealth(maxHealth);

            itemDropHandler = GetComponent<ItemDropHandler>();
            if (itemDropHandler == null)
            {
                Debug.LogError("TreeOfLightPot: ItemDropHandler is missing!");
            }

            if (monsterSpawner == null)
            {
                Debug.LogWarning("TreeOfLightPot: Monster Spawner not assigned!");
            }
        }

        private void OnEnable()
        {
            healthComponent.OnHealthChanged += OnHealthChanged;
            healthComponent.OnDeath += OnDeathHandler;
        }

        private void OnDisable()
        {
            healthComponent.OnHealthChanged -= OnHealthChanged;
            healthComponent.OnDeath -= OnDeathHandler;
            if (seedSocket != null)
            {
                seedSocket.selectEntered.RemoveListener(OnSeedSocketEntered);
            }
        }

        private void OnHealthChanged(object sender, HealthComponent.HealthChangedEventArgs e)
        {
            Debug.Log($"TreeOfLightPot: Health Changed: {e.CurrentHealth} / {e.MaxHealth} by {e.DamageSource?.name ?? "Unknown"}");
        }

        private void OnSeedSocketEntered(SelectEnterEventArgs args)
        {
            if (args.interactableObject is UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable)
            {
                MagicalSeed seed = grabInteractable.GetComponent<MagicalSeed>();
                if (seed != null)
                {
                    Debug.Log($"TreeOfLightPot: Seed {seed.name} detected in socket");
                    StartCoroutine(PlaceSeed(seed));
                }
                else
                {
                    Debug.LogWarning("TreeOfLightPot: Grabbed object is not a MagicalSeed!");
                }
            }
            else
            {
                Debug.LogWarning("TreeOfLightPot: Interactable is not XRGrabInteractable!");
            }
        }

        private IEnumerator PlaceSeed(MagicalSeed seed)
        {
            Debug.Log($"TreeOfLightPot: Placing seed {seed.name}, TreeSpawnPoint = {treeSpawnPoint.position}");
            initialSeedRotation = seed.transform.rotation;
            initialSeedScale = seed.transform.localScale;

            var grabInteractable = seed.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            var rb = seed.GetComponent<Rigidbody>();

            if (grabInteractable != null) grabInteractable.enabled = false;
            if (rb != null) StartCoroutine(SetKinematicDelayed(rb));

            seedSocket.socketActive = false;
            plantedSeed = seed;

            StartGrowthParticles();
            BeginGrowth();
            onGrowthStarted.Invoke();

            yield return StartCoroutine(MoveSeedToSpawnPoint(seed, seedMoveDuration));
        }

        private IEnumerator SetKinematicDelayed(Rigidbody rb)
        {
            yield return new WaitForSeconds(0.0f);
            rb.isKinematic = true;
        }

        private IEnumerator MoveSeedToSpawnPoint(MagicalSeed seed, float duration)
        {
            Debug.Log($"TreeOfLightPot: Moving seed to {treeSpawnPoint.position} over {duration}s");
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

            Debug.Log("TreeOfLightPot: Seed movement complete");
        }

        private void StartGrowthParticles()
        {
            if (growthParticleInstance != null)
            {
                growthParticleInstance.gameObject.SetActive(true);
                growthParticleInstance.Play();
                Debug.Log("TreeOfLightPot: Growth particles started");
            }
        }

        private void StopGrowthParticles()
        {
            if (growthParticleInstance != null)
            {
                growthParticleInstance.Stop();
                growthParticleInstance.gameObject.SetActive(false);
                Debug.Log("TreeOfLightPot: Growth particles stopped");
            }
        }

        public void BeginGrowth()
        {
            if (hasBegunGrowth)
            {
                Debug.LogWarning("TreeOfLightPot: BeginGrowth called multiple times!");
                return;
            }
            hasBegunGrowth = true;

            if (animator == null)
            {
                Debug.LogError("TreeOfLightPot: Animator is not assigned!");
                return;
            }

            animator.enabled = true;
            Debug.Log("TreeOfLightPot: Animator enabled for growth");

            StartCoroutine(GrowthCoroutine());
        }

        private void Update()
        {
            if (isGrowing && !isComplete)
            {
                transform.Rotate(Vector3.up * (30f * Time.deltaTime));
            }
        }

        private IEnumerator GrowthCoroutine()
        {
            Debug.Log($"TreeOfLightPot: Starting growth, Animation = {growthAnimationName}, Duration = {growthDuration}s");
            if (!string.IsNullOrEmpty(growthAnimationName))
            {
                var clip = animator.runtimeAnimatorController.animationClips.FirstOrDefault(c => c.name == growthAnimationName);
                if (clip != null)
                {
                    float clipDuration = clip.length;
                    float animatorSpeed = clipDuration / growthDuration;
                    animator.speed = animatorSpeed;
                    Debug.Log($"TreeOfLightPot: Set Animator speed = {animatorSpeed}, Clip Duration = {clipDuration}s, Target Duration = {growthDuration}s");

                    monsterSpawner?.BeginSpawning();
                    Debug.Log("TreeOfLightPot: Triggered monsterSpawner.BeginSpawning");
                    animator.Play(growthAnimationName, -1, 0f);
                    isGrowing = true;

                    if (audioSource != null && growthSound != null)
                    {
                        audioSource.PlayOneShot(growthSound);
                        Debug.Log("TreeOfLightPot: Playing growth sound");
                    }

                    yield return new WaitForSeconds(growthDuration);
                    Debug.Log("TreeOfLightPot: Growth duration completed, calling CompleteVisualGrowth");
                    CompleteVisualGrowth();
                }
                else
                {
                    Debug.LogError($"TreeOfLightPot: Animation clip '{growthAnimationName}' not found!");
                }
            }
            else
            {
                Debug.LogError("TreeOfLightPot: Growth animation name is null or empty!");
            }
        }

        private void CompleteVisualGrowth()
        {
            if (isComplete)
            {
                Debug.LogWarning("TreeOfLightPot: CompleteVisualGrowth called multiple times!");
                return;
            }

            isGrowing = false;
            isComplete = true;
            Debug.Log("TreeOfLightPot: Growth completed, isComplete = true");

            if (itemDropHandler != null)
            {
                itemDropHandler.SetHasGrown(true);
                itemDropHandler.DropItems(); // Drop key on growth completion
                Debug.Log("TreeOfLightPot: Called SetHasGrown and DropItems on ItemDropHandler");
            }
            else
            {
                Debug.LogError("TreeOfLightPot: ItemDropHandler is null!");
            }

            onGrowthCompleted.Invoke();
            Debug.Log("TreeOfLightPot: onGrowthCompleted event invoked");

            // Kill all ShadowMonsters
            var monsters = GameObject.FindGameObjectsWithTag("Monster");
            Debug.Log($"TreeOfLightPot: Found {monsters.Length} objects tagged 'Monster'");
            foreach (var monster in monsters)
            {
                var monsterHealth = monster.GetComponent<HealthComponent>();
                if (monsterHealth != null)
                {
                    monsterHealth.TakeDamage(monsterHealth.MaxHealth, transform.position, gameObject);
                    Debug.Log($"TreeOfLightPot: Killed {monster.name}, Health = {monsterHealth.Health}");
                }
                else
                {
                    Debug.LogWarning($"TreeOfLightPot: {monster.name} tagged 'Monster' but has no HealthComponent!");
                }
            }

            // Fallback: Kill all HealthComponents tagged as Enemy
            var allEnemies = FindObjectsOfType<HealthComponent>().Where(h => h.gameObject.CompareTag("Enemy")).ToList();
            Debug.Log($"TreeOfLightPot: Found {allEnemies.Count} HealthComponents tagged 'Enemy'");
            foreach (var enemyHealth in allEnemies)
            {
                enemyHealth.TakeDamage(enemyHealth.MaxHealth, transform.position, gameObject);
                Debug.Log($"TreeOfLightPot: Killed enemy {enemyHealth.gameObject.name}, Health = {enemyHealth.Health}");
            }

            StartCoroutine(DelayedSelfDestruction(1f));
        }

        private IEnumerator DelayedSelfDestruction(float delay)
        {
            Debug.Log($"TreeOfLightPot: Delaying self-destruction for {delay}s");
            yield return new WaitForSeconds(delay);
            if (healthComponent != null)
            {
                healthComponent.TakeDamage(healthComponent.MaxHealth * 2f, transform.position, gameObject); // Ensure lethal damage
                Debug.Log("TreeOfLightPot: Applied lethal damage for self-destruction");
            }
            else
            {
                Debug.LogError("TreeOfLightPot: HealthComponent is null during self-destruction!");
                Destroy(gameObject); // Fallback destruction
            }
        }

        private void OnDeathHandler(HealthComponent health)
        {
            StopAllCoroutines();
            StopGrowthParticles();
            monsterSpawner?.StopSpawning();
            Debug.Log("TreeOfLightPot: OnDeathHandler called, stopping spawner and particles");

            // Disable Tree_Of_Light
            var treeOfLight = transform.Find("Tree_Of_Light")?.gameObject;
            if (treeOfLight != null)
            {
                treeOfLight.SetActive(false);
                Debug.Log("TreeOfLightPot: Disabled Tree_Of_Light on death");
            }

            // Ensure key drop if growth completed
            if (itemDropHandler != null && isComplete)
            {
                itemDropHandler.SetHasGrown(true); // Redundant but ensures consistency
                itemDropHandler.DropItems();
                Debug.Log("TreeOfLightPot: Dropped items on death after growth completion");
            }
            else if (itemDropHandler != null && !isComplete)
            {
                itemDropHandler.DropItems();
                Debug.Log("TreeOfLightPot: Dropped items due to death before completion");
            }
            else
            {
                Debug.LogWarning("TreeOfLightPot: ItemDropHandler is null or missing!");
            }
        }

        private void OnDestroy()
        {
            if (seedSocket != null)
            {
                seedSocket.selectEntered.RemoveListener(OnSeedSocketEntered);
            }
        }
    }
}
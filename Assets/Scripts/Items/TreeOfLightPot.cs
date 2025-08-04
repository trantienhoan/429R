using UnityEngine;
using UnityEngine.Events;
using Items;
using Core;
using System.Collections;
using Enemies;

namespace Items
{
    public class TreeOfLightPot : MonoBehaviour
    {
        [SerializeField] private HealthComponent healthComponent;
        [SerializeField] private Transform treeSpawnPoint;
        [SerializeField] private GameObject treeChild; // Drag "Tree_Of_Light" child here in Inspector
        [SerializeField] private float absorbDuration = 1f;
        [SerializeField] private float growTime = 5f;
        [SerializeField] private GameObject growingVFXPrefab;
        [SerializeField] private ParticleSystem deathVFX;
        [SerializeField] private AudioClip growingSFX;
        [SerializeField] private AudioClip deathSFX;
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private GameObject keyPrefab;
        [SerializeField] private GameObject bombPrefab;
        [SerializeField] private float explosionForce = 500f;
        [SerializeField] private float explosionRadius = 5f;
        [SerializeField] private float treeRotationSpeed = 90f;
        [SerializeField] private float particleScaleStartTime = 0.03f;
        [SerializeField] private float minParticleScale = 0.1f;
        [SerializeField] private float maxParticleScale = 0.3f;

        public UnityEvent OnTreeGrown;

        private bool isGrowing;
        private float growTimer;
        private GameObject currentTree;
        private GameObject currentSeed;
        private Coroutine absorbRoutine;
        private GameObject currentGrowingVFXInstance;
        private ShadowMonsterSpawner monsterSpawner;

        public bool IsGrowing => isGrowing;

        private void Awake()
        {
            if (healthComponent == null)
            {
                healthComponent = GetComponent<HealthComponent>();
            }
            if (treeSpawnPoint == null)
            {
                treeSpawnPoint = transform.Find("SeedSocketAttach");
                if (treeSpawnPoint == null)
                {
                    Debug.LogError($"[TreeOfLightPot {gameObject.name}] SeedSocketAttach not found as child!");
                }
            }
            if (sfxSource == null)
            {
                sfxSource = GetComponent<AudioSource>();
            }
            if (healthComponent != null)
            {
                healthComponent.IsInvulnerable = true;
            }
            if (treeChild == null)
            {
                Debug.LogError($"[TreeOfLightPot {gameObject.name}] treeChild not assigned in Inspector! Tree growth will skip.");
            }
            else
            {
                treeChild.SetActive(false); // Ensure inactive at start
            }
            gameObject.tag = "TreeOfLight";

            monsterSpawner = FindObjectOfType<ShadowMonsterSpawner>();
            if (monsterSpawner == null)
            {
                Debug.LogError($"[TreeOfLightPot {gameObject.name}] No ShadowMonsterSpawner found in scene!");
            }
        }

        private void Start()
        {
            if (healthComponent != null)
            {
                healthComponent.OnDeath.AddListener(OnTreeDeath);
            }
        }

        private void OnDestroy()
        {
            if (healthComponent != null)
            {
                healthComponent.OnDeath.RemoveListener(OnTreeDeath);
            }
            if (currentTree != null)
            {
                var treeHealth = currentTree.GetComponent<HealthComponent>();
                if (treeHealth != null)
                {
                    treeHealth.OnDeath.RemoveListener(OnCurrentTreeDeath);
                }
            }
            if (absorbRoutine != null)
            {
                StopCoroutine(absorbRoutine);
            }
        }

        public void TriggerDestruction()
        {
            if (healthComponent != null)
            {
                healthComponent.Kill();
                Debug.Log($"[TreeOfLightPot {gameObject.name}] Triggering destruction");
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            Debug.Log($"[TreeOfLightPot {gameObject.name}] Trigger entered by {other.tag} on layer {other.gameObject.layer}");
            if (other.CompareTag("MagicalSeed") && !isGrowing && currentSeed == null)
            {
                Debug.Log($"[TreeOfLightPot {gameObject.name}] Valid MagicalSeed detected - starting process");
                currentSeed = other.gameObject;
                MagicalSeed seedScript = currentSeed.GetComponent<MagicalSeed>();
                if (seedScript != null)
                {
                    seedScript.DisablePhysics();
                }
                absorbRoutine = StartCoroutine(AbsorbSeed());
            }
        }

        private IEnumerator AbsorbSeed()
        {
            Debug.Log($"[TreeOfLightPot {gameObject.name}] Starting seed absorption");
            Vector3 startPos = currentSeed.transform.position;
            Vector3 startScale = currentSeed.transform.localScale;
            float t = 0f;

            while (t < absorbDuration)
            {
                t += Time.deltaTime;
                float frac = t / absorbDuration;
                currentSeed.transform.position = Vector3.Lerp(startPos, treeSpawnPoint.position, frac);
                currentSeed.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, frac);
                yield return null;
            }

            Destroy(currentSeed);
            currentSeed = null;
            absorbRoutine = null;
            Debug.Log($"[TreeOfLightPot {gameObject.name}] Seed absorbed");
            StartGrowing();
        }

        private void Update()
        {
            if (isGrowing)
            {
                growTimer += Time.deltaTime;
                if (growTimer >= growTime)
                {
                    CompleteGrowth();
                }
                else
                {
                    if (currentTree != null)
                    {
                        currentTree.transform.Rotate(Vector3.up, treeRotationSpeed * Time.deltaTime);
                    }

                    if (currentGrowingVFXInstance != null && growTimer > particleScaleStartTime)
                    {
                        float progress = (growTimer - particleScaleStartTime) / (growTime - particleScaleStartTime);
                        float scale = Mathf.Lerp(minParticleScale, maxParticleScale, progress);
                        currentGrowingVFXInstance.transform.localScale = Vector3.one * scale;
                    }
                }
            }
        }

        private void StartGrowing()
        {
            Debug.Log($"[TreeOfLightPot {gameObject.name}] Starting tree growth");
            isGrowing = true;
            growTimer = 0f;

            if (monsterSpawner != null)
            {
                monsterSpawner.BeginSpawning();
            }

            if (treeChild == null)
            {
                Debug.LogError($"[TreeOfLightPot {gameObject.name}] No tree available to grow! Skipping.");
                return;
            }

            currentTree = treeChild;
            currentTree.SetActive(true);

            Rigidbody treeRb = currentTree.GetComponent<Rigidbody>();
            if (treeRb != null)
            {
                treeRb.linearVelocity = Vector3.zero;
                treeRb.angularVelocity = Vector3.zero;
                treeRb.isKinematic = true;
                treeRb.useGravity = false;
                treeRb.interpolation = RigidbodyInterpolation.Interpolate;
            }

            var treeHealth = currentTree.GetComponent<HealthComponent>();
            if (treeHealth != null)
            {
                treeHealth.OnDeath.AddListener(OnCurrentTreeDeath);
            }
            else
            {
                treeHealth = currentTree.AddComponent<HealthComponent>();
                treeHealth.SetMaxHealth(100f);
                treeHealth.OnDeath.AddListener(OnCurrentTreeDeath);
            }

            Animator anim = currentTree.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                if (anim.runtimeAnimatorController == null)
                {
                    Debug.LogError($"[TreeOfLightPot {gameObject.name}] Animator has no runtimeAnimatorController assigned!");
                }
                else
                {
                    Debug.Log($"[TreeOfLightPot {gameObject.name}] Animator controller found: {anim.runtimeAnimatorController.name}");
                }
                anim.enabled = true;
                anim.Rebind();
                anim.Update(0f);
                if (anim.HasState(0, Animator.StringToHash("SeedGrow")))
                {
                    anim.Play("SeedGrow", -1, 0f);
                    Debug.Log($"[TreeOfLightPot {gameObject.name}] Playing 'SeedGrow' animation");
                }
                else
                {
                    Debug.LogError($"[TreeOfLightPot {gameObject.name}] Animator does not have state 'SeedGrow'! Playing default state instead.");
                    anim.Play(anim.GetCurrentAnimatorStateInfo(0).shortNameHash);
                }
            }
            else
            {
                Debug.LogError($"[TreeOfLightPot {gameObject.name}] No Animator component found in treeChild or its children!");
            }

            if (growingVFXPrefab != null)
            {
                currentGrowingVFXInstance = Instantiate(growingVFXPrefab, treeSpawnPoint.position + Vector3.up * 0.25f, Quaternion.identity, treeSpawnPoint.parent);
                currentGrowingVFXInstance.SetActive(true);
                currentGrowingVFXInstance.transform.localScale = Vector3.one * minParticleScale;
                ParticleSystem ps = currentGrowingVFXInstance.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    var main = ps.main;
                    main.loop = true;
                    ps.Clear();
                    ps.Simulate(0f, true, true);
                    ps.Play();
                    Debug.Log($"[TreeOfLightPot {gameObject.name}] Instantiated and playing growingVFX at {treeSpawnPoint.position}. Playing: {ps.isPlaying}");
                }
                else
                {
                    Debug.LogError($"[TreeOfLightPot {gameObject.name}] growingVFXPrefab does not have a ParticleSystem component!");
                }
            }
            else
            {
                Debug.LogError($"[TreeOfLightPot {gameObject.name}] growingVFXPrefab is not assigned!");
            }

            if (sfxSource != null && growingSFX != null)
            {
                sfxSource.PlayOneShot(growingSFX);
            }

            if (healthComponent != null)
            {
                healthComponent.IsInvulnerable = false;
                Debug.Log($"[TreeOfLightPot {gameObject.name}] Started growing, invulnerability disabled");
            }
        }

        private void CompleteGrowth()
        {
            Debug.Log($"[TreeOfLightPot {gameObject.name}] Completing growth");
            isGrowing = false;
            if (currentGrowingVFXInstance != null)
            {
                ParticleSystem ps = currentGrowingVFXInstance.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    ps.Stop();
                    var main = ps.main;
                    main.loop = false;
                    Destroy(currentGrowingVFXInstance, main.duration);
                    Debug.Log($"[TreeOfLightPot {gameObject.name}] Stopping growingVFX");
                }
                else
                {
                    Destroy(currentGrowingVFXInstance);
                }
            }
            DropItem(keyPrefab);
            OnTreeGrown?.Invoke();

            if (monsterSpawner != null)
            {
                monsterSpawner.StopSpawning();
                Destroy(monsterSpawner.gameObject);
            }

            GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
            foreach (GameObject enemy in enemies)
            {
                var health = enemy.GetComponent<HealthComponent>();
                if (health != null)
                {
                    health.Kill();
                }
            }

            Invoke("TriggerDestruction", 0.5f); // Delay explosion to after key drop
        }

        private void OnTreeDeath(HealthComponent health)
        {
            if (deathVFX != null)
            {
                deathVFX.Play();
            }
            if (sfxSource != null && deathSFX != null)
            {
                sfxSource.PlayOneShot(deathSFX);
            }

            if (isGrowing)
            {
                DropItem(bombPrefab);
            }
            else
            {
                float bigExplosionForce = 2000f;
                float bigExplosionRadius = 5f;
                Collider[] colliders = Physics.OverlapSphere(transform.position, bigExplosionRadius);
                int hitCount = 0;
                foreach (Collider hit in colliders)
                {
                    if (hit.gameObject != gameObject && hit.CompareTag("Enemy")) // Add tag check here
                    {
                        hitCount++;
                        Rigidbody rb = hit.attachedRigidbody;
                        if (rb != null)
                        {
                            Vector3 direction = (hit.transform.position - transform.position).normalized;
                            rb.AddForce(direction * bigExplosionForce, ForceMode.Impulse);
                        }

                        HealthComponent enemyHealth = hit.GetComponent<HealthComponent>();
                        if (enemyHealth != null)
                        {
                            enemyHealth.TakeDamage(1000f);
                        }
                    }
                }
                Debug.Log($"[TreeOfLightPot {gameObject.name}] Exploding pot after growth complete. Hit {hitCount} objects.");
            }

            if (currentTree != null)
            {
                var treeHealth = currentTree.GetComponent<HealthComponent>();
                if (treeHealth != null)
                {
                    treeHealth.Kill();
                    Debug.Log($"[TreeOfLightPot {gameObject.name}] Tree destroyed along with pot");
                }
                else
                {
                    Destroy(currentTree);
                }
            }

            Debug.Log($"[TreeOfLightPot {gameObject.name}] OnTreeDeath called");
        }

        private void OnCurrentTreeDeath(HealthComponent health)
        {
            if (isGrowing)
            {
                DropItem(bombPrefab);
            }
            TriggerDestruction();
        }

        private void DropItem(GameObject itemPrefab)
        {
            if (itemPrefab != null)
            {
                Instantiate(itemPrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity);
                Debug.Log($"[TreeOfLightPot {gameObject.name}] Dropped {itemPrefab.name}");
            }
        }
    }
}
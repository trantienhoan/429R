using UnityEngine;
using UnityEngine.Events;
using Items;
using Core;
using System.Collections;

namespace Items
{
    public class TreeOfLightPot : MonoBehaviour
    {
        [SerializeField] private GameObject treeOfLight;
        [SerializeField] private HealthComponent healthComponent;
        [SerializeField] private Transform treeSpawnPoint;
        [SerializeField] private float absorbDuration = 2f;
        [SerializeField] private float growTime = 5f;
        [SerializeField] private GameObject growingVFXPrefab;
        [SerializeField] private ParticleSystem deathVFX;
        [SerializeField] private AudioClip growingSFX;
        [SerializeField] private AudioClip deathSFX;
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private GameObject keyPrefab;
        [SerializeField] private GameObject bombPrefab;
        [SerializeField] private Rigidbody potRigidbody;
        [SerializeField] private float explosionForce = 500f;
        [SerializeField] private float explosionRadius = 5f;

        public UnityEvent OnTreeGrown;

        private bool isGrowing;
        private float growTimer;
        private GameObject currentTree;
        private GameObject currentSeed;
        private Coroutine absorbRoutine;
        private GameObject currentGrowingVFXInstance;

        public bool IsGrowing => isGrowing;

        private void Awake()
        {
            if (healthComponent == null)
            {
                healthComponent = GetComponent<HealthComponent>();
            }
            if (potRigidbody == null)
            {
                potRigidbody = GetComponent<Rigidbody>();
            }
            if (potRigidbody != null)
            {
                potRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
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
            if (treeOfLight == null)
            {
                Debug.LogWarning($"[TreeOfLightPot {gameObject.name}] TreeOfLight reference is not set!");
            }
            gameObject.tag = "TreeOfLight";
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

        private void OnCollisionEnter(Collision collision)
        {
            Debug.Log($"[TreeOfLightPot {gameObject.name}] Collision entered by {collision.gameObject.tag} on layer {collision.gameObject.layer} - this means Is Trigger is NOT enabled!");
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
                if (potRigidbody != null)
                {
                    potRigidbody.linearVelocity = Vector3.zero;
                    potRigidbody.angularVelocity = Vector3.zero;
                    potRigidbody.isKinematic = true;
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
            }
        }

        private void StartGrowing()
        {
            Debug.Log($"[TreeOfLightPot {gameObject.name}] Starting tree growth");
            isGrowing = true;
            growTimer = 0f;

            if (potRigidbody != null)
            {
                potRigidbody.isKinematic = true;
            }

            currentTree = Instantiate(treeOfLight, treeSpawnPoint.position, treeSpawnPoint.rotation);
            currentTree.SetActive(true); // Ensure active
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
                anim.Update(0f); // Force initial update
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
                Debug.LogError($"[TreeOfLightPot {gameObject.name}] No Animator component found in TreeOfLight or its children!");
            }

            if (growingVFXPrefab != null)
            {
                currentGrowingVFXInstance = Instantiate(growingVFXPrefab, treeSpawnPoint.position, Quaternion.identity, treeSpawnPoint.parent);
                currentGrowingVFXInstance.SetActive(true);
                ParticleSystem ps = currentGrowingVFXInstance.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    var main = ps.main;
                    main.loop = true;
                    ps.Clear();
                    ps.Simulate(0f, true, true);
                    ps.Play();
                    Debug.Log($"[TreeOfLightPot {gameObject.name}] Instantiated and playing growingVFX at {treeSpawnPoint.position}");
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
            TriggerDestruction();
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
                // Explode the pot after growth complete
                Collider[] colliders = Physics.OverlapSphere(transform.position, explosionRadius);
                foreach (Collider hit in colliders)
                {
                    Rigidbody rb = hit.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.AddExplosionForce(explosionForce, transform.position, explosionRadius);
                    }
                }
                Debug.Log($"[TreeOfLightPot {gameObject.name}] Exploding pot after growth complete");
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
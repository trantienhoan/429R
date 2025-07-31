using UnityEngine;
using UnityEngine.AI;
using UnityEngine.XR.Interaction.Toolkit;
using Core;
using Items;
using System.Collections;

namespace Enemies
{
    public class ShadowMonster : BaseMonster
    {
        [Header("References")]
        [SerializeField] public Animator animator;
        [SerializeField] public NavMeshAgent agent;
        [SerializeField] public SpiderAttackHitbox attackHitbox;
        [SerializeField] public Rigidbody rb;
        [SerializeField] private ParticleSystem deathVFX;
        [SerializeField] private AudioClip deathSfx;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] public UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;
        [SerializeField] public HealthComponent healthComponent;
        
        [Header("Settings")]
        public float chaseRange = 15f;
        public float attackRange = 2f;
        public float kamikazeRange = 5f;
        public float attackCooldown = 2f;
        public float chargeDelay = 2.5f;
        public float normalAttackDamage = 10f;
        public float kamikazeAttackDamage = 50f;
        public float kamikazeHealthThreshold = 0.19f;
        private float kamikazeRadiusMultiplier = 3f;
        public float pushForce = 5f;
        public float stuckTimeThreshold = 9f;
        public float idleTimeBeforeDive = 5f;
        [SerializeField] private float growthTime = 30f;
        [SerializeField] private Vector3 startScale = new Vector3(0.5f, 0.5f, 0.5f);
        [SerializeField] private Vector3 fullScale = new Vector3(1f, 1f, 1f);
        public float diveSpeed = 10f;
        public float diveTimeout = 3f;

        public Transform currentTarget;
        private Vector3 lastPosition;
        private float stuckTimer;
        public float chargeTimer { get; private set; }
        private bool isCharging;
        public bool isInKamikazeMode;
        private float originalExplosionRadius;
        public float lastAttackTime;
        public StateMachine stateMachine { get; private set; }
        private Coroutine attackCoroutine;
        private float growthTimer;
        private float targetRefreshTimer;
        private const float targetRefreshInterval = 0.5f;

        public bool isGrounded { get; private set; }
        public bool IsBeingHeld { get; private set; }

        [SerializeField] private Transform groundCheckPoint;
        [SerializeField] private float groundCheckDistance = 0.5f;
        [SerializeField] private LayerMask groundLayer;

        public bool IsGrounded()
        {
            bool grounded = Physics.Raycast(groundCheckPoint.position, Vector3.down, out RaycastHit hit, groundCheckDistance, groundLayer);
            isGrounded = grounded;
            if (animator != null)
            {
                animator.SetBool("isGrounded", grounded);
                animator.Update(0f);
            }
            return grounded;
        }

        public string GetStateName(int stateHash)
        {
            if (stateHash == Animator.StringToHash("Base Layer.Idle")) return "Idle";
            if (stateHash == Animator.StringToHash("Base Layer.Idle_On_Air")) return "Idle_On_Air";
            if (stateHash == Animator.StringToHash("Base Layer.Run")) return "Run";
            if (stateHash == Animator.StringToHash("Base Layer.Charge")) return "Charge";
            if (stateHash == Animator.StringToHash("Base Layer.Attack")) return "Attack";
            if (stateHash == Animator.StringToHash("Base Layer.Hurt")) return "Hurt";
            if (stateHash == Animator.StringToHash("Base Layer.Dive")) return "Dive";
            if (stateHash == Animator.StringToHash("Base Layer.Dead")) return "Dead";
            if (stateHash == Animator.StringToHash("Base Layer.Kamikaze")) return "Kamikaze";
            return "Unknown";
        }

        public void EnsureAgentOnNavMesh()
        {
            if (agent != null && !agent.isActiveAndEnabled)
            {
                agent.enabled = true;
                agent.speed = 3.5f;
                agent.stoppingDistance = 0f;
            }
            if (agent != null && !agent.isOnNavMesh && isGrounded)
            {
                NavMeshHit hit;
                if (NavMesh.SamplePosition(transform.position, out hit, 2f, NavMesh.AllAreas))
                {
                    transform.position = hit.position;
                    agent.Warp(hit.position);
                }
            }
        }

        public void EnableAI()
        {
            if (!enabled)
            {
                enabled = true;
            }
            EnsureAgentOnNavMesh();
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                stateMachine.ChangeState(new IdleState(this));
            }
        }

        public void DisableAI()
        {
            enabled = false;

            if (attackHitbox != null)
            {
                attackHitbox.gameObject.SetActive(false);
            }
            if (attackCoroutine != null)
            {
                StopCoroutine(attackCoroutine);
                attackCoroutine = null;
            }
            if (animator != null)
            {
                animator.SetBool("isGrounded", false);
                animator.SetBool("isRunning", false);
                animator.SetBool("isCharging", false);
                animator.ResetTrigger("Attack");
                animator.ResetTrigger("KamikazeAttack");
                animator.Update(0f);
            }

            // Change state FIRST to allow OnExit to run while agent is enabled
            stateMachine.ChangeState(null);

            // Then disable agent
            if (agent != null && agent.isActiveAndEnabled)
            {
                agent.enabled = false;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            stateMachine = new StateMachine();
            audioSource = GetComponent<AudioSource>();
            grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            healthComponent = GetComponent<HealthComponent>();
            gameObject.tag = "Enemy";
            if (!healthComponent)
            {
                healthComponent = gameObject.AddComponent<HealthComponent>();
            }
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }
            if (rb != null)
            {
                rb.constraints = RigidbodyConstraints.FreezeRotation;
            }
            if (attackHitbox != null)
            {
                originalExplosionRadius = attackHitbox.GetComponent<SphereCollider>().radius;
                attackHitbox.Initialize(gameObject, normalAttackDamage, pushForce);
            }
            if (animator != null)
            {
                animator.SetBool("isGrounded", isGrounded);
                animator.SetBool("isRunning", false);
                animator.SetBool("isCharging", false);
                animator.ResetTrigger("Attack");
                animator.ResetTrigger("KamikazeAttack");
            }
            transform.localScale = startScale;
            growthTimer = 0f;
            EnsureAgentOnNavMesh();
        }

        protected override void Start()
        {
            base.Start();
            if (grabInteractable != null)
            {
                grabInteractable.selectEntered.AddListener(OnGrabbed);
                grabInteractable.selectExited.AddListener(OnReleased);
            }
            if (healthComponent != null)
            {
                healthComponent.OnTakeDamage.AddListener(OnHealthChanged);
                healthComponent.OnDeath.AddListener(OnDeath);
            }
            EnableAI();
        }

        private void OnEnable()
        {
            ResetSpider();
        }

        private void Update()
        {
            isGrounded = IsGrounded();
            EnsureAgentOnNavMesh();
            stateMachine.Tick();
            CheckIfStuck();
            growthTimer += Time.deltaTime;
            float growthProgress = Mathf.Clamp01(growthTimer / growthTime);
            transform.localScale = Vector3.Lerp(startScale, fullScale, growthProgress);
            targetRefreshTimer += Time.deltaTime;
            if (targetRefreshTimer >= targetRefreshInterval)
            {
                GetClosestTarget();
                targetRefreshTimer = 0f;
            }
#if UNITY_EDITOR
            if (Input.GetKeyDown(KeyCode.G) && isGrounded)
            {
                if (animator != null)
                {
                    animator.SetBool("isGrounded", true);
                    animator.SetBool("isRunning", false);
                    animator.SetBool("isCharging", false);
                    animator.ResetTrigger("Attack");
                    animator.ResetTrigger("KamikazeAttack");
                    stateMachine.ChangeState(new IdleState(this));
                }
            }
            if (Input.GetKeyDown(KeyCode.C))
            {
                stateMachine.ChangeState(new ChaseState(this));
            }
#endif
        }

        private void CheckIfStuck()
        {
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh && Vector3.Distance(transform.position, lastPosition) < 0.1f)
            {
                stuckTimer += Time.deltaTime;
                if (stuckTimer > stuckTimeThreshold && !isInKamikazeMode)
                {
                    EnterKamikazeMode();
                }
            }
            else
            {
                stuckTimer = 0f;
                lastPosition = transform.position;
            }
        }

        public override void TakeDamage(float damageAmount, Vector3 hitPoint = default, GameObject damageSource = null)
        {
            if (healthComponent != null)
            {
                healthComponent.TakeDamage(damageAmount, hitPoint, damageSource);
            }
        }

        private void OnHealthChanged(HealthComponent.HealthChangedEventArgs e)
        {
            if (e.DamageAmount > 0 && !healthComponent.IsDead() && stateMachine.CurrentState is not HurtState && !IsBeingHeld)
            {
                isCharging = false;
                stateMachine.ChangeState(new HurtState(this));
                if (healthComponent.GetHealthPercentage() <= kamikazeHealthThreshold)
                {
                    EnterKamikazeMode();
                }
            }
        }

        private void OnDeath(HealthComponent health)
        {
            stateMachine.ChangeState(new DeadState(this));
            StartCoroutine(DelayedScaleDown(2f)); // Delay for death animation
        }
        private IEnumerator DelayedScaleDown(float delay)
        {
            yield return new WaitForSeconds(delay);
            StartCoroutine(ScaleDownAndDisable());
        }

        protected override void OnDeathHandler()
        {
            // Handled by HealthComponent and DeadState
        }

        public void SetMaxHealth(float value)
        {
            if (healthComponent != null)
            {
                healthComponent.SetMaxHealth(value);
            }
        }

        public void StartCharge()
        {
            isCharging = true;
            chargeTimer = 0f;
            if (animator != null && isCharging)  // Explicit read to satisfy compiler
            {
                animator.SetBool("isCharging", true);
                animator.SetBool("isRunning", false);
                animator.SetBool("isGrounded", isGrounded);
                animator.ResetTrigger("Attack");
                animator.Update(0f);
            }
        }

        public bool IsChargeComplete()
        {
            chargeTimer += Time.deltaTime;
            return chargeTimer >= chargeDelay;
        }

        public void PerformAttack()
        {
            if (Time.time < lastAttackTime + attackCooldown)
            {
                return;
            }
            lastAttackTime = Time.time;
            isCharging = false;
            if (attackHitbox != null)
            {
                attackHitbox.GetComponent<SphereCollider>().radius = originalExplosionRadius;
                attackHitbox.Initialize(gameObject, normalAttackDamage, pushForce);
                attackCoroutine = StartCoroutine(PerformAttackCoroutine(false));
            }
        }

        public void PerformKamikazeAttack()
        {
            if (Time.time < lastAttackTime + attackCooldown)
            {
                return;
            }
            lastAttackTime = Time.time;
            if (attackHitbox != null)
            {
                attackHitbox.GetComponent<SphereCollider>().radius = originalExplosionRadius * kamikazeRadiusMultiplier;
                attackHitbox.Initialize(gameObject, kamikazeAttackDamage, pushForce);
                attackCoroutine = StartCoroutine(PerformAttackCoroutine(true));
            }
        }

        private IEnumerator PerformAttackCoroutine(bool isKamikaze)
        {
            if (attackHitbox != null)
            {
                attackHitbox.gameObject.SetActive(true);
                if (isKamikaze)
                {
                    if (animator != null && animator.GetCurrentAnimatorStateInfo(0).IsName("Kamikaze"))  // Gate to state
                    {
                        animator.SetTrigger("KamikazeAttack");
                    }
                    yield return new WaitForSeconds(0.2f);
                    attackHitbox.TriggerExplosion(); 
                    healthComponent.Kill(gameObject);
                }
                else
                {
                    if (animator != null && animator.GetCurrentAnimatorStateInfo(0).IsName("Attack"))  // Gate to state
                    {
                        animator.SetTrigger("Attack");
                    }
                    yield return new WaitForSeconds(0.3f);
                    attackHitbox.TriggerExplosion();  // SFX/damage
                }
                yield return new WaitForSeconds(0.5f);
                if (attackHitbox != null)
                {
                    attackHitbox.gameObject.SetActive(false);
                }
            }
            attackCoroutine = null;
        }

        private void EnterKamikazeMode()
        {
            isInKamikazeMode = true;
            if (agent != null)
            {
                agent.speed *= 1.5f;
            }
            stateMachine.ChangeState(new KamikazeState(this));
        }

        public void Pickup()
        {
            IsBeingHeld = true;
            if (agent != null && agent.isActiveAndEnabled)
            {
                agent.updatePosition = false;  // Pause control, don't disable
                agent.updateRotation = false;
                agent.isStopped = true;  // Safe now
            }
            if (attackHitbox != null)
            {
                attackHitbox.gameObject.SetActive(false);
            }
            if (attackCoroutine != null)
            {
                StopCoroutine(attackCoroutine);
                attackCoroutine = null;
            }
            if (animator != null)
            {
                animator.SetBool("isGrounded", false);
                animator.SetBool("isRunning", false);
                animator.SetBool("isCharging", false);
                animator.ResetTrigger("Attack");
                animator.ResetTrigger("KamikazeAttack");
                animator.SetTrigger("IdleOnAir");
                animator.Update(0f);
            }
            stateMachine.ChangeState(new HeldState(this));
        }

        public void Release()
        {
            IsBeingHeld = false;
            currentTarget = null;
            if (attackHitbox != null)
            {
                attackHitbox.gameObject.SetActive(false);
            }
            if (attackCoroutine != null)
            {
                StopCoroutine(attackCoroutine);
                attackCoroutine = null;
            }
            if (animator != null)
            {
                animator.SetBool("isGrounded", isGrounded);
                animator.SetBool("isRunning", false);
                animator.SetBool("isCharging", false);
                animator.ResetTrigger("Attack");
                animator.ResetTrigger("KamikazeAttack");
                animator.Update(0f);
            }
            EnsureAgentOnNavMesh();
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.updatePosition = true;  // Resume control
                agent.updateRotation = true;
                agent.Warp(transform.position);  // Force reposition
            }
            if (isGrounded && agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                stateMachine.ChangeState(new IdleState(this));
            }
        }

        public Transform GetClosestTarget()
        {
            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
            GameObject[] trees = GameObject.FindGameObjectsWithTag("TreeOfLight");
            Transform closestTarget = null;
            float closestDistance = Mathf.Infinity;

            foreach (GameObject obj in trees)
            {
                if (obj != null && obj.activeInHierarchy)
                {
                    var treePot = obj.GetComponent<TreeOfLightPot>();
                    if (treePot != null && treePot.IsGrowing)
                    {
                        float distance = Vector3.Distance(transform.position, obj.transform.position);
                        if (distance < closestDistance)
                        {
                            closestDistance = distance;
                            closestTarget = obj.transform;
                        }
                    }
                }
            }

            if (closestTarget == null)
            {
                foreach (GameObject obj in players)
                {
                    if (obj != null && obj.activeInHierarchy)
                    {
                        float distance = Vector3.Distance(transform.position, obj.transform.position);
                        if (distance < closestDistance)
                        {
                            closestDistance = distance;
                            closestTarget = obj.transform;
                        }
                    }
                }
            }

            currentTarget = closestTarget;
            return closestTarget;
        }

        public float GetDistanceToTarget()
        {
            if (currentTarget == null)
            {
                currentTarget = GetClosestTarget();
            }
            return currentTarget != null ? Vector3.Distance(transform.position, currentTarget.position) : Mathf.Infinity;
        }

        public void PlayDeathEffects()
        {
            if (animator != null)
            {
                animator.SetTrigger("Dead");
            }
            if (deathVFX != null)
            {
                deathVFX.Play();
            }
            if (deathSfx != null && audioSource != null)
            {
                audioSource.PlayOneShot(deathSfx);
            }
        }
        
        public void ResetChargeTimer()
        {
            chargeTimer = 0f;
            isCharging = false;
            if (animator != null)
            {
                animator.SetBool("isCharging", false);
            }
        }

        public void ResetSpider()
        {
            if (stateMachine.CurrentState != null && !(stateMachine.CurrentState is DeadState))
            {
                return;
            }
            stuckTimer = 0f;
            chargeTimer = 0f;
            lastAttackTime = 0f;
            isCharging = false;
            isInKamikazeMode = false;
            currentTarget = null;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            transform.localScale = startScale;
            growthTimer = 0f;
            if (attackCoroutine != null)
            {
                StopCoroutine(attackCoroutine);
                attackCoroutine = null;
            }
            if (attackHitbox != null)
            {
                attackHitbox.gameObject.SetActive(false);
            }
            if (animator != null)
            {
                animator.Rebind();
                animator.Update(0f);
                animator.SetBool("isGrounded", isGrounded);
                animator.SetBool("isRunning", false);
                animator.SetBool("isCharging", false);
                animator.ResetTrigger("Attack");
                animator.ResetTrigger("KamikazeAttack");
            }
            EnsureAgentOnNavMesh();
            if (healthComponent != null)
            {
                healthComponent.ResetHealth();
            }
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                stateMachine.ChangeState(new IdleState(this));
            }
            else
            {
                // Fallback: Start coroutine to wait
                StartCoroutine(WaitForAgentReady());
            }
        }

        private IEnumerator WaitForAgentReady()
        {
            yield return new WaitForSeconds(0.1f);  // Small delay
            EnsureAgentOnNavMesh();
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                stateMachine.ChangeState(new IdleState(this));
            }
        }
        private void OnGrabbed(SelectEnterEventArgs args)
        {
            Pickup();
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            Release();
        }

        private void OnDestroy()
        {
            if (grabInteractable != null)
            {
                grabInteractable.selectEntered.RemoveListener(OnGrabbed);
                grabInteractable.selectExited.RemoveListener(OnReleased);
            }
            if (healthComponent != null)
            {
                healthComponent.OnTakeDamage.RemoveListener(OnHealthChanged);
                healthComponent.OnDeath.RemoveListener(OnDeath);
            }
        }

        public IEnumerator ScaleDownAndDisable()
        {
            yield return StartCoroutine(DestroyAfterDelay(3f));
        }

        new private IEnumerator DestroyAfterDelay(float delay)
        {
            float timer = 0f;
            Vector3 startScale = transform.localScale;
            while (timer < delay)
            {
                timer += Time.deltaTime;
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, timer / delay);
                yield return null;
            }
            gameObject.SetActive(false);
        }
    }
}
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.XR.Interaction.Toolkit;
using Core;
using Items;
using System.Collections;
using Random = UnityEngine.Random;

namespace Enemies
{
    /// <summary>
    /// ShadowMonster AI controller.
    /// Manages navigation, targeting, attacks (via delegated hitbox), growth, and XR interactions.
    /// Uses a state machine for behaviors (idle, chase, attack, etc.).
    /// </summary>
    public class ShadowMonster : BaseMonster
    {
        private static readonly int Grounded = Animator.StringToHash("isGrounded");
        private static readonly int IsRunning = Animator.StringToHash("isRunning");
        private static readonly int IsCharging = Animator.StringToHash("isCharging");
        private static readonly int KamikazeAttack = Animator.StringToHash("KamikazeAttack");
        private static readonly int IdleOnAir = Animator.StringToHash("IdleOnAir");
        private static readonly int Attack = Animator.StringToHash("Attack");
        private static readonly int Attack2 = Animator.StringToHash("Attack2");
        private static readonly int Attack3 = Animator.StringToHash("Attack3");
        private static readonly int Dead = Animator.StringToHash("Dead");

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
        [SerializeField] public float chaseRange = 15f;
        [SerializeField] public float attackRange = 2f;
        [SerializeField] public float kamikazeRange = 5f;
        [SerializeField] public float attackCooldown = 2f;
        [SerializeField] public float chargeDelay = 2.5f;
        [SerializeField] public float normalAttackDamage = 10f;
        [SerializeField] public float kamikazeAttackDamage = 50f;
        [SerializeField] public float kamikazeHealthThreshold = 0.19f;
        [SerializeField] public float pushForce = 5f;
        [SerializeField] public float stuckTimeThreshold = 9f;
        [SerializeField] public float idleTimeBeforeDive = 5f;
        [SerializeField] public float maxHoldTime = 5f;
        [SerializeField] public float struggleShakeIntensity = 0.1f;

        [SerializeField] private float growthTime = 30f;
        [SerializeField] private Vector3 startScale = new Vector3(0.5f, 0.5f, 0.5f);
        [SerializeField] private Vector3 fullScale = new Vector3(1f, 1f, 1f);

        [Header("Dive Settings")]
        [SerializeField] private LayerMask wallLayer;
        [SerializeField] public float diveForceMultiplier = 1.5f;
        [SerializeField] public float diveSpeed = 10f;
        [SerializeField] public float diveTimeout = 3f;
        [SerializeField] public float diveDamage = 15f;

        [Header("NavMesh Scaling")]
        [SerializeField] private float originalAgentRadius = 0.5f;
        [SerializeField] private float originalAgentHeight = 2f;
        [SerializeField] private float originalBaseOffset = 0f;

        [Header("Throw and Break Settings")]
        [SerializeField] private float velocityThreshold = 0.1f;
        [SerializeField] private float breakDamage = 10f;
        [SerializeField] private float settleDelayAfterLand = 0.5f; // Increased for better settling

        [Header("Ground Check Settings")]
        [SerializeField] private float groundRayDistance = 0.5f;
        [SerializeField] private LayerMask groundLayer;
        [SerializeField] private float minFloorY = -10f;

        [SerializeField] private Transform groundCheckPoint;

        [Header("Target Layers")]
        [SerializeField] private LayerMask playerLayer;
        [SerializeField] private LayerMask treeLayer;

        public Transform currentTarget;
        private Vector3 lastPosition;
        private Vector3 lastValidPosition; // For fallback warping
        public float stuckTimer;
        public float chargeTimer { get; private set; }
        private bool isCharging;
        public bool isInKamikazeMode;
        public float lastAttackTime;
        public StateMachine stateMachine { get; private set; }
        private float growthTimer;
        private float targetRefreshTimer;
        private const float k_TargetRefreshInterval = 0.5f;

        public bool isGrounded { get; private set; }
        public bool isBeingHeld { get; private set; }

        private bool isThrown;
        private bool isBreakingHold;

        private bool IsAgentValid => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;

        // === Lifecycle ===

        protected override void Awake()
        {
            base.Awake();

            stateMachine = new StateMachine();
            audioSource = GetComponent<AudioSource>();
            grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            healthComponent = GetComponent<HealthComponent>() ?? gameObject.AddComponent<HealthComponent>();
            animator = GetComponent<Animator>();
            rb = GetComponent<Rigidbody>();

            if (rb != null)
            {
                rb.constraints = RigidbodyConstraints.FreezeRotation;
                rb.useGravity = true;
                rb.isKinematic = false;
            }

            if (agent != null)
            {
                agent.updatePosition = false;
                agent.updateRotation = false;
            }

            if (attackHitbox != null)
            {
                attackHitbox.scaleSource = transform;
                attackHitbox.sizeScaleMultiplier = 1f;
                attackHitbox.forceScaleMultiplier = 1f;
                attackHitbox.damageScaleMultiplier = 1f;
            }

            transform.localScale = startScale;
            growthTimer = 0f;
            ResetAnimator();
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

            currentTarget = GetClosestTarget();
            EnableAI();
        }

        private void OnEnable()
        {
            ResetSpider();
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

        private void Update()
        {
            isGrounded = IsGrounded();
            EnsureAgentOnNavMesh();
            stateMachine.Tick();
            CheckIfStuck();

            // Growth
            growthTimer += Time.deltaTime;
            float growthProgress = Mathf.Clamp01(growthTimer / growthTime);
            transform.localScale = Vector3.Lerp(startScale, fullScale, growthProgress);
            UpdateAgentOnScale();

            // Refresh target
            targetRefreshTimer += Time.deltaTime;
            if (targetRefreshTimer >= k_TargetRefreshInterval)
            {
                GetClosestTarget();
                targetRefreshTimer = 0f;
            }

            // Post-throw settle
            if (!isBeingHeld && isThrown && !(stateMachine.CurrentState is DiveState))
            {
                if (isGrounded && rb.linearVelocity.magnitude < velocityThreshold)
                {
                    StartCoroutine(ResumeAIWithDelay(settleDelayAfterLand));
                }
            }

            // Clamp falls
            if (transform.position.y < minFloorY)
            {
                Vector3 pos = transform.position;
                pos.y = minFloorY;
                transform.position = pos;
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            }

            // Sync agent
            if (IsAgentValid)
            {
                agent.nextPosition = transform.position;
                lastValidPosition = transform.position; // Track for fallback
            }
        }

        private void FixedUpdate()
        {
            if (!enabled || !IsAgentValid || isBeingHeld || isThrown || stateMachine.CurrentState is DiveState || stateMachine.CurrentState is HeldState)
                return;

            // Prevent downward velocity bounce
            if (isGrounded && rb.linearVelocity.y < 0)
            {
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            }

            rb.linearVelocity = new Vector3(agent.desiredVelocity.x, rb.linearVelocity.y, agent.desiredVelocity.z);

            if (agent.desiredVelocity.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(new Vector3(agent.desiredVelocity.x, 0f, agent.desiredVelocity.z));
            }
        }

        // === Ground / NavMesh ===

        public bool IsGrounded()
        {
            float scaleFactor = transform.localScale.y;
            float adjustedDistance = groundRayDistance * scaleFactor;

            bool grounded = Physics.Raycast(groundCheckPoint.position, Vector3.down, adjustedDistance, groundLayer);
            isGrounded = grounded;

            if (animator != null)
            {
                animator.SetBool(Grounded, grounded);
            }

            return grounded;
        }

        public void EnsureAgentOnNavMesh()
        {
            if (agent == null) return;

            if (!agent.isActiveAndEnabled)
            {
                agent.enabled = true;
                agent.speed = 3.5f;
                agent.stoppingDistance = 0f;
            }

            if (!agent.isOnNavMesh)
            {
                float scaleFactor = transform.localScale.y;
                if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 50f * scaleFactor, NavMesh.AllAreas)) // Increased radius
                {
                    transform.position = hit.position;
                    agent.Warp(hit.position);
                }
                else if (lastValidPosition != Vector3.zero)
                {
                    transform.position = lastValidPosition;
                    agent.Warp(lastValidPosition);
                }
            }
        }

        private void UpdateAgentOnScale()
        {
            if (agent == null) return;

            float scaleFactor = Mathf.Clamp(transform.localScale.y, 0.5f, 1.0f);
            agent.radius = Mathf.Clamp(originalAgentRadius * scaleFactor, 0.25f, 0.5f);
            agent.height = Mathf.Clamp(originalAgentHeight * scaleFactor, 1f, 2f);
            agent.baseOffset = originalBaseOffset * scaleFactor;
            EnsureAgentOnNavMesh();
        }

        // === AI Control ===

        public void EnableAI()
        {
            enabled = true;
            EnsureAgentOnNavMesh();

            if (IsAgentValid)
            {
                stateMachine.ChangeState(new IdleState(this));
            }
        }

        public void DisableAI()
        {
            enabled = false;

            if (attackHitbox != null)
                attackHitbox.gameObject.SetActive(false);

            ResetAnimator();
            stateMachine.ChangeState(null);
        }

        private void CheckIfStuck()
        {
            if (!isGrounded)
            {
                stuckTimer = 0f;
                return;
            }

            if (IsAgentValid)
            {
                if (agent.hasPath || agent.remainingDistance > 0.1f)
                {
                    if (Vector3.Distance(transform.position, lastPosition) < 0.1f)
                    {
                        stuckTimer += Time.deltaTime;
                        if (stuckTimer > stuckTimeThreshold && !isInKamikazeMode)
                        {
                            stateMachine.ChangeState(new DiveState(this));
                        }
                    }
                    else
                    {
                        stuckTimer = 0f;
                        lastPosition = transform.position;
                    }
                }
                else
                {
                    stuckTimer = 0f;
                }
            }
            else
            {
                stuckTimer = 0f;
                lastPosition = transform.position;
            }
        }

        // === Targeting ===

        public Transform GetClosestTarget()
        {
            Transform closestTarget = null;
            float closestDistance = Mathf.Infinity;

            // Prefer growing TreeOfLightPot
            Collider[] treeHits = Physics.OverlapSphere(transform.position, chaseRange, treeLayer);
            foreach (var hit in treeHits)
            {
                var treePot = hit.GetComponent<TreeOfLightPot>();
                if (treePot != null && treePot.IsGrowing)
                {
                    float d = Vector3.Distance(transform.position, hit.transform.position);
                    if (d < closestDistance)
                    {
                        closestDistance = d;
                        closestTarget = hit.transform;
                    }
                }
            }

            // Fallback to players
            if (closestTarget == null)
            {
                Collider[] playerHits = Physics.OverlapSphere(transform.position, chaseRange, playerLayer);
                foreach (var hit in playerHits)
                {
                    float d = Vector3.Distance(transform.position, hit.transform.position);
                    if (d < closestDistance)
                    {
                        closestDistance = d;
                        closestTarget = hit.transform;
                    }
                }
            }

            currentTarget = closestTarget;
            return closestTarget;
        }

        public float GetDistanceToTarget()
        {
            if (currentTarget == null) currentTarget = GetClosestTarget();
            return currentTarget ? Vector3.Distance(transform.position, currentTarget.position) : Mathf.Infinity;
        }

        // === Charge / Attack ===

        public void StartCharge()
        {
            isCharging = true;
            chargeTimer = 0f;

            if (animator != null)
            {
                animator.SetBool(IsCharging, true);
                animator.SetBool(IsRunning, false);
                animator.SetBool(Grounded, isGrounded);
                animator.ResetTrigger("Attack");
            }
        }

        public bool IsChargeComplete()
        {
            chargeTimer += Time.deltaTime;
            return chargeTimer >= chargeDelay;
        }

        public void PerformAttack()
        {
            if (Time.time < lastAttackTime + attackCooldown) return;

            lastAttackTime = Time.time;
            isCharging = false;

            int[] attackTriggers = { Attack, Attack2, Attack3 };
            int selectedTrigger = attackTriggers[Random.Range(0, attackTriggers.Length)];

            if (animator != null) animator.SetTrigger(selectedTrigger);

            if (attackHitbox != null)
            {
                StartHitbox(normalAttackDamage, 1.0f, 0.5f, false, false);
            }
        }

        public void PerformKamikazeAttack()
        {
            if (Time.time < lastAttackTime + attackCooldown) return;
            lastAttackTime = Time.time;

            if (animator != null) animator.SetTrigger(KamikazeAttack);

            if (attackHitbox != null)
            {
                StartHitbox(kamikazeAttackDamage, 3.0f, 0.6f, true, false);
            }
        }

        public IEnumerator PerformDiveAttackCoroutine()
        {
            if (attackHitbox == null || !isActiveAndEnabled) yield break;

            StartHitbox(diveDamage, 1.5f, 0.6f, false, false);
            yield return null;
        }

        private void StartHitbox(float absoluteDamage, float radiusScale, float duration, bool explode, bool directional)
        {
            if (!isActiveAndEnabled || attackHitbox == null) return;

            attackHitbox.Configure(absoluteDamage, radiusScale, pushForce);
            attackHitbox.Activate(duration, explode, directional);
        }

        public void EnterKamikazeMode()
        {
            isInKamikazeMode = true;
            if (agent != null) agent.speed *= 1.5f;
            stateMachine.ChangeState(new KamikazeState(this));
        }

        // === Health / Damage ===

        public override void TakeDamage(float damageAmount, Vector3 hitPoint = default, GameObject damageSource = null)
        {
            if (healthComponent != null)
            {
                healthComponent.TakeDamage(damageAmount, hitPoint, damageSource);
            }
        }

        private void OnHealthChanged(HealthComponent.HealthChangedEventArgs e)
        {
            if (e.DamageAmount > 0 && !healthComponent.IsDead() && !(stateMachine.CurrentState is HurtState) && !isBeingHeld)
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
            StartCoroutine(DelayedScaleDown(2f));
        }

        protected override void OnDeathHandler() { }

        public void SetMaxHealth(float value)
        {
            if (healthComponent != null) healthComponent.SetMaxHealth(value);
        }

        // === Grab / Throw ===

        private void OnGrabbed(SelectEnterEventArgs args) => Pickup();

        private void OnReleased(SelectExitEventArgs args) => Release();

        public void Pickup()
        {
            isBeingHeld = true;
            isThrown = false;

            if (agent != null) agent.isStopped = true;
            if (attackHitbox != null) attackHitbox.gameObject.SetActive(false);

            if (animator != null)
            {
                animator.SetBool(Grounded, false);
                animator.SetBool(IsRunning, false);
                animator.SetBool(IsCharging, false);
                animator.ResetTrigger("Attack");
                animator.ResetTrigger("KamikazeAttack");
                animator.SetTrigger(IdleOnAir);
            }

            stateMachine.ChangeState(new HeldState(this));
        }

        public void Release()
        {
            isBeingHeld = false;
            currentTarget = null;

            if (attackHitbox != null) attackHitbox.gameObject.SetActive(false);

            if (animator != null)
            {
                animator.SetBool(Grounded, isGrounded);
                animator.SetBool(IsRunning, false);
                animator.SetBool(IsCharging, false);
                animator.ResetTrigger("Attack");
                animator.ResetTrigger("KamikazeAttack");
            }

            if (agent != null) agent.enabled = false; // Disable to prevent snap during flight

            if (isBreakingHold)
            {
                isBreakingHold = false;
                stateMachine.ChangeState(new DiveState(this));

                if (grabInteractable != null && grabInteractable.interactorsSelecting.Count > 0)
                {
                    var player = grabInteractable.interactorsSelecting[0].transform.root.GetComponent<HealthComponent>();
                    if (player != null) player.TakeDamage(breakDamage);
                }
            }
            else
            {
                isThrown = true;
            }

            StartCoroutine(ReEnableGrabAfter(1f));
        }

        private IEnumerator ReEnableGrabAfter(float delay)
        {
            if (grabInteractable != null)
            {
                grabInteractable.enabled = false;
                yield return new WaitForSeconds(delay);
                grabInteractable.enabled = true;
            }
        }

        private IEnumerator ResumeAIWithDelay(float delay)
        {
            yield return new WaitForSeconds(delay);

            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            agent.enabled = true;
            EnsureAgentOnNavMesh();
            isThrown = false;
        }

        public void ForceBreakHold()
        {
            if (grabInteractable != null && grabInteractable.isSelected)
            {
                isBreakingHold = true;
                if (grabInteractable.interactorsSelecting.Count > 0)
                {
                    var interactor = grabInteractable.interactorsSelecting[0];
                    grabInteractable.interactionManager.SelectExit(
                        interactor,
                        (UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)grabInteractable);
                }
            }
        }

        // === Death / Reset ===

        public void PlayDeathEffects()
        {
            if (animator != null) animator.SetTrigger(Dead);
            if (deathVFX != null) deathVFX.Play();
            if (deathSfx != null && audioSource != null) audioSource.PlayOneShot(deathSfx);
        }

        public void Despawn()
        {
            if (healthComponent != null) healthComponent.Kill(gameObject);
            else StartCoroutine(ScaleDownAndDisable());
        }

        private IEnumerator DelayedScaleDown(float delay)
        {
            yield return new WaitForSeconds(delay);
            StartCoroutine(ScaleDownAndDisable());
        }

        public IEnumerator ScaleDownAndDisable()
        {
            float timer = 0f;
            Vector3 start = transform.localScale;
            const float duration = 3f;
            while (timer < duration)
            {
                timer += Time.deltaTime;
                transform.localScale = Vector3.Lerp(start, Vector3.zero, timer / duration);
                yield return null;
            }
            gameObject.SetActive(false);
        }

        public void ResetSpider()
        {
            if (stateMachine.CurrentState != null && !(stateMachine.CurrentState is DeadState)) return;

            stuckTimer = 0f;
            chargeTimer = 0f;
            lastAttackTime = 0f;
            isCharging = false;
            isInKamikazeMode = false;
            currentTarget = null;
            isThrown = false;
            isBeingHeld = false;
            isBreakingHold = false;

            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            transform.localScale = startScale;
            growthTimer = 0f;

            if (attackHitbox != null) attackHitbox.gameObject.SetActive(false);

            ResetAnimator();
            EnsureAgentOnNavMesh();

            if (healthComponent != null) healthComponent.ResetHealth();

            if (IsAgentValid)
                stateMachine.ChangeState(new IdleState(this));
            else
                StartCoroutine(WaitForAgentReady());
        }

        private void ResetAnimator()
        {
            if (animator == null) return;

            animator.SetBool(Grounded, isGrounded);
            animator.SetBool(IsRunning, false);
            animator.SetBool(IsCharging, false);
            animator.ResetTrigger("Attack");
            animator.ResetTrigger("Attack2");
            animator.ResetTrigger("Attack3");
            animator.ResetTrigger("KamikazeAttack");
        }

        private IEnumerator WaitForAgentReady()
        {
            yield return new WaitForSeconds(0.1f);
            EnsureAgentOnNavMesh();
            if (IsAgentValid)
            {
                stateMachine.ChangeState(new IdleState(this));
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (stateMachine.CurrentState is DiveState && (wallLayer.value & (1 << collision.gameObject.layer)) != 0)
            {
                stateMachine.ChangeState(new IdleState(this));
            }
        }

        public void ResetChargeTimer()
        {
            chargeTimer = 0f;
            isCharging = false;
            if (animator != null) animator.SetBool(IsCharging, false);
        }
    }
}
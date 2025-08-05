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
        private static readonly int Grounded = Animator.StringToHash("isGrounded");
        private static readonly int IsRunning = Animator.StringToHash("isRunning");
        private static readonly int IsCharging = Animator.StringToHash("isCharging");
        private static readonly int KamikazeAttack = Animator.StringToHash("KamikazeAttack");
        private static readonly int IdleOnAir = Animator.StringToHash("IdleOnAir");
        private static readonly int Attack = Animator.StringToHash("Attack");
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
        public float chaseRange = 15f;
        public float attackRange = 2f;
        public float kamikazeRange = 5f;
        public float attackCooldown = 2f;
        public float chargeDelay = 2.5f;
        public float normalAttackDamage = 10f;
        public float kamikazeAttackDamage = 50f;
        public float kamikazeHealthThreshold = 0.19f;
        private readonly float kamikazeRadiusMultiplier = 3f;
        public float pushForce = 5f;
        public float stuckTimeThreshold = 9f;
        public float idleTimeBeforeDive = 5f;
        [SerializeField] private float growthTime = 30f;
        [SerializeField] private Vector3 startScale = new Vector3(0.5f, 0.5f, 0.5f);
        [SerializeField] private Vector3 fullScale = new Vector3(1f, 1f, 1f);
        
        [Header("Dive Settings")]
        [SerializeField] private LayerMask wallLayer;
        [SerializeField] public float diveForceMultiplier = 1.5f; 
        public float diveSpeed = 10f;
        public float diveTimeout = 3f;

        [Header("NavMesh Scaling")]
        [SerializeField] private float originalAgentRadius = 0.5f;  // Base agent radius
        [SerializeField] private float originalAgentHeight = 2f;    // Base agent height
        [SerializeField] private float originalBaseOffset = 0f;     // Base offset

        [Header("Throw and Break Settings")]
        [SerializeField] public float maxHoldTime = 5f;  // Time before break free
        [SerializeField] private float velocityThreshold = 0.1f;  // For settling after throw/dive
        [SerializeField] public float struggleShakeIntensity = 0.1f;  // For feedback during long hold
        [SerializeField] private float breakDamage = 10f;  // Optional: Damage to player on break
        [SerializeField] private float settleDelayAfterLand = 0.2f;  // Delay before resuming AI after land

        private bool isBreakingHold = false;
        private bool isThrown = false;  // Track post-release physics flight
        
        [Header("Ground Check Settings")]
        [SerializeField] private float groundRayDistance = 0.5f;  // Raycast down distance (adjust to scale)
        [SerializeField] private LayerMask groundLayer;  // Ensure assigned in Inspector
        [SerializeField] private float minFloorY = -10f;  // Clamp threshold for position (adjust to scene)

        public Transform currentTarget;
        private Vector3 lastPosition;
        public float stuckTimer;
        public float chargeTimer { get; private set; }
        private bool isCharging;
        public bool isInKamikazeMode;
        private float originalExplosionRadius;
        public float lastAttackTime;
        public StateMachine stateMachine { get; private set; }
        private Coroutine attackCoroutine;
        private float growthTimer;
        private float targetRefreshTimer;
        private const float k_TargetRefreshInterval = 0.5f;

        public bool isGrounded { get; private set; }
        public bool isBeingHeld { get; private set; }

        [SerializeField] private Transform groundCheckPoint;

        [Header("Target Layers")]  // New for optimized GetClosestTarget
        [SerializeField] private LayerMask playerLayer;
        [SerializeField] private LayerMask treeLayer;

        public bool IsGrounded()
        {
            float scaleFactor = transform.localScale.y;
            float adjustedDistance = groundRayDistance * scaleFactor;

            // Raycast down from groundCheckPoint
            bool grounded = Physics.Raycast(groundCheckPoint.position, -Vector3.up, adjustedDistance, groundLayer);
            isGrounded = grounded;
            if (animator != null)
            {
                animator.SetBool(Grounded, grounded);
                animator.Update(0f);
            }
            if (!grounded)
            {
                string layerName = groundLayer.value == 0 ? "INVALID - Assign in Inspector!" : LayerMask.LayerToName(Mathf.RoundToInt(Mathf.Log(groundLayer.value + 1, 2)) - 1);
                Debug.LogWarning($"[IsGrounded] Failed at scale {scaleFactor}: Position={groundCheckPoint.position}, Layer={layerName}, Adjusted Distance={adjustedDistance}");
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
            if (agent != null && !agent.isOnNavMesh)
            {
                float scaleFactor = transform.localScale.y;
                NavMeshHit hit;
                if (NavMesh.SamplePosition(transform.position, out hit, 10f * scaleFactor, NavMesh.AllAreas))  // Increased sample distance to reduce snap risk
                {
                    transform.position = hit.position;
                    agent.Warp(hit.position);
                    Debug.Log("[EnsureAgent] Warped to NavMesh: " + hit.position);
                }
                else
                {
                    Debug.LogWarning("[EnsureAgent] No NavMesh position found near " + transform.position);
                }
            }
        }
        
        public void ForceBreakHold()
        {
            if (grabInteractable != null && grabInteractable.isSelected)
            {
                isBreakingHold = true;
                if (grabInteractable.interactorsSelecting.Count > 0)
                {
                    var interactor = grabInteractable.interactorsSelecting[0];
                    grabInteractable.interactionManager.SelectExit(interactor, (UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)grabInteractable);
                    Debug.Log("[ForceBreakHold] Forced release.");
                }
                else
                {
                    Debug.LogWarning("[ForceBreakHold] No interactors selecting.");
                }
            }
        }

        private void UpdateAgentOnScale()
        {
            if (agent != null)
            {
                float scaleFactor = transform.localScale.y;  // Use Y for height consistency
                agent.radius = originalAgentRadius * scaleFactor;
                agent.height = originalAgentHeight * scaleFactor;
                agent.baseOffset = originalBaseOffset * scaleFactor - (scaleFactor - 1f) * 0.1f;  // Slight negative offset for larger scales to "sink" in
                EnsureAgentOnNavMesh();  // Force re-sample after changes
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
            ResetAnimator();  // Use new method

            // Change state FIRST to allow OnExit to run while agent is enabled
            stateMachine.ChangeState(null);
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
                rb.useGravity = true;  // Ensure gravity for throws/dives
                rb.isKinematic = false;  // Non-kinematic for constant physics (damage, throws, pushes)
            }
            if (agent != null)
            {
                agent.updatePosition = false;  // Decouple agent from direct transform control
                agent.updateRotation = false;  // Manual rotation
            }
            if (attackHitbox != null)
            {
                var collider = attackHitbox.GetComponent<SphereCollider>();
                originalExplosionRadius = collider.radius;  // Cache the actual collider radius
                attackHitbox.Initialize(gameObject, normalAttackDamage, pushForce);
            }
            ResetAnimator();  // Initial reset
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
            currentTarget = GetClosestTarget();
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
            UpdateAgentOnScale();  // Update agent after scaling
            targetRefreshTimer += Time.deltaTime;
            if (targetRefreshTimer >= k_TargetRefreshInterval)
            {
                GetClosestTarget();
                targetRefreshTimer = 0f;
            }
#if UNITY_EDITOR
            if (Input.GetKeyDown(KeyCode.G) && isGrounded)
            {
                ResetAnimator();
                stateMachine.ChangeState(new IdleState(this));
            }
            if (Input.GetKeyDown(KeyCode.C))
            {
                stateMachine.ChangeState(new ChaseState(this));
            }
#endif
            if (animator != null)
            {
                string currentState = GetStateName(animator.GetCurrentAnimatorStateInfo(0).fullPathHash);
                //Debug.Log("[Animator] Current State: " + currentState + ", Grounded Bool: " + animator.GetBool("isGrounded"));
            }

            // Handle post-release settling for throws/drops
            if (!isBeingHeld && isThrown && stateMachine.CurrentState is not DiveState)
            {
                if (IsGrounded() && rb.linearVelocity.magnitude < velocityThreshold)
                {
                    StartCoroutine(ResumeAIWithDelay(settleDelayAfterLand));  // Delay to let physics settle
                }
            }

            // Clamp position to prevent deep falls
            if (transform.position.y < minFloorY)
            {
                Vector3 pos = transform.position;
                pos.y = minFloorY;
                transform.position = pos;
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);  // Zero Y velocity
                Debug.LogWarning("[Update] Clamped position to prevent fall-through.");
            }

            // Sync agent position (always, if on NavMesh)
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.nextPosition = transform.position;
            }
        }

        private void FixedUpdate()
        {
            // Manual sync: Apply agent's desired velocity to Rigidbody if in AI mode (not held/thrown/diving)
            if (enabled && agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh && !isBeingHeld && !isThrown && stateMachine.CurrentState is not DiveState && stateMachine.CurrentState is not HeldState)
            {
                rb.linearVelocity = new Vector3(agent.desiredVelocity.x, rb.linearVelocity.y, agent.desiredVelocity.z);  // Apply horizontal nav, preserve vertical physics

                // Manual rotation towards movement
                if (agent.desiredVelocity.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.LookRotation(new Vector3(agent.desiredVelocity.x, 0f, agent.desiredVelocity.z));
                }
            }
        }

        private void CheckIfStuck()
        {
            if (!isGrounded) 
            {
                stuckTimer = 0f;  // Reset if in air – no stuck
                return;
            }
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                if (agent.hasPath || agent.remainingDistance > 0.1f)  // Only when should be moving
                {
                    if (Vector3.Distance(transform.position, lastPosition) < 0.1f)
                    {
                        stuckTimer += Time.deltaTime;
                        if (stuckTimer > stuckTimeThreshold && !isInKamikazeMode)
                        {
                            stateMachine.ChangeState(new DiveState(this));  // Dive instead of Kamikaze
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

        public override void TakeDamage(float damageAmount, Vector3 hitPoint = default, GameObject damageSource = null)
        {
            if (healthComponent != null)
            {
                healthComponent.TakeDamage(damageAmount, hitPoint, damageSource);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (stateMachine.CurrentState is DiveState && ((1 << collision.gameObject.layer) & wallLayer) != 0)
            {
                Debug.Log("[Dive] Hit wall/border, exiting Dive");
                stateMachine.ChangeState(new IdleState(this));  // Or Chase if target nearby
            }
        }

        private void OnHealthChanged(HealthComponent.HealthChangedEventArgs e)
        {
            if (e.DamageAmount > 0 && !healthComponent.IsDead() && stateMachine.CurrentState is not HurtState && !isBeingHeld)
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
                animator.SetBool(IsCharging, true);
                animator.SetBool(IsRunning, false);
                animator.SetBool(Grounded, isGrounded);
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
                var collider = attackHitbox.GetComponent<SphereCollider>();
                collider.radius = originalExplosionRadius;
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
                var collider = attackHitbox.GetComponent<SphereCollider>();
                collider.radius = originalExplosionRadius * kamikazeRadiusMultiplier;
                attackHitbox.Initialize(gameObject, kamikazeAttackDamage, pushForce, isKamikaze: true);
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
                        animator.SetTrigger(KamikazeAttack);
                    }
                    yield return new WaitForSeconds(0.2f);
                    attackHitbox.TriggerExplosion(); 
                    healthComponent.Kill(gameObject);
                }
                else
                {
                    if (animator != null && animator.GetCurrentAnimatorStateInfo(0).IsName("Attack"))  // Gate to state
                    {
                        animator.SetTrigger(Attack);
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

        public void EnterKamikazeMode()
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
            isBeingHeld = true;
            isThrown = false;
            if (agent != null)
            {
                agent.isStopped = true;  // Pause agent during grab
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
                animator.SetBool(Grounded, false);
                animator.SetBool(IsRunning, false);
                animator.SetBool(IsCharging, false);
                animator.ResetTrigger("Attack");
                animator.ResetTrigger("KamikazeAttack");
                animator.SetTrigger(IdleOnAir);
                animator.Update(0f);
            }
            stateMachine.ChangeState(new HeldState(this));
        }

        public void Release()
        {
            isBeingHeld = false;
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
                animator.SetBool(Grounded, isGrounded);
                animator.SetBool(IsRunning, false);
                animator.SetBool(IsCharging, false);
                animator.ResetTrigger("Attack");
                animator.ResetTrigger("KamikazeAttack");
                animator.Update(0f);
            }
            EnsureAgentOnNavMesh();

            if (isBreakingHold)
            {
                isBreakingHold = false;
                stateMachine.ChangeState(new DiveState(this));  // Force Dive (handles physics)
                // Optional: Damage player
                if (grabInteractable.interactorsSelecting.Count > 0)
                {
                    var player = grabInteractable.interactorsSelecting[0].transform.root.GetComponent<HealthComponent>();
                    if (player != null) player.TakeDamage(breakDamage);
                }
            }
            else
            {
                isThrown = true;  // Mark for Update to handle landing
                // No immediate state change; wait for land in Update
            }

            // Temporarily disable grab to prevent instant re-grab (e.g., after break/dive)
            StartCoroutine(ReEnableGrabAfter(1f));  // Adjust delay
        }

        private IEnumerator ReEnableGrabAfter(float delay)
        {
            grabInteractable.enabled = false;
            yield return new WaitForSeconds(delay);
            grabInteractable.enabled = true;
        }

        public Transform GetClosestTarget()
        {
            Transform closestTarget = null;
            float closestDistance = Mathf.Infinity;

            // Check trees
            Collider[] treeHits = Physics.OverlapSphere(transform.position, chaseRange, treeLayer);
            foreach (Collider hit in treeHits)
            {
                var treePot = hit.GetComponent<TreeOfLightPot>();
                if (treePot != null && treePot.IsGrowing)
                {
                    float distance = Vector3.Distance(transform.position, hit.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestTarget = hit.transform;
                    }
                }
            }

            // Fallback to players if no trees
            if (closestTarget == null)
            {
                Collider[] playerHits = Physics.OverlapSphere(transform.position, chaseRange, playerLayer);
                foreach (Collider hit in playerHits)
                {
                    float distance = Vector3.Distance(transform.position, hit.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestTarget = hit.transform;
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
                animator.SetTrigger(Dead);
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
        public void Despawn()
        {
            if (healthComponent != null)
            {
                healthComponent.Kill(gameObject);  // Triggers OnDeath -> PlayDeathEffects -> ScaleDown
            }
            else
            {
                StartCoroutine(ScaleDownAndDisable());  // Fallback no VFX
            }
        }
        
        public void ResetChargeTimer()
        {
            chargeTimer = 0f;
            isCharging = false;
            if (animator != null)
            {
                animator.SetBool(IsCharging, false);
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
            ResetAnimator();
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

        private void ResetAnimator()
        {
            if (animator != null)
            {
                animator.Rebind();
                animator.Update(0f);
                animator.SetBool(Grounded, isGrounded);
                animator.SetBool(IsRunning, false);
                animator.SetBool(IsCharging, false);
                animator.ResetTrigger("Attack");
                animator.ResetTrigger("KamikazeAttack");
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

        private IEnumerator DestroyAfterDelay(float delay)
        {
            float timer = 0f;
            Vector3 currentScale = transform.localScale;
            while (timer < delay)
            {
                timer += Time.deltaTime;
                transform.localScale = Vector3.Lerp(currentScale, Vector3.zero, timer / delay);
                yield return null;
            }
            gameObject.SetActive(false);
        }

        private IEnumerator ResumeAIWithDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            isThrown = false;
            Debug.Log("[ResumeAIWithDelay] Resumed AI after settle delay.");
        }
    }
}
using UnityEngine;
using UnityEngine.AI;

using Core;
using Items;
using System.Collections;

namespace Enemies
{
    /// <summary>
    /// ShadowMonster – simple, reliable enemy:
    /// - NavMeshAgent is enabled only when close to NavMesh (prevents errors)
    /// - Melee applies instant knockback (VelocityChange) + plays hit VFX at contact
    /// - Keeps all fields other scripts reference (dive/held/idle/kamikaze)
    /// </summary>
    public class ShadowMonster : BaseMonster
    {
        // Animator hashes
        private static readonly int GroundedHash = Animator.StringToHash("isGrounded");
        private static readonly int IsRunningHash = Animator.StringToHash("isRunning");
        private static readonly int IsChargingHash = Animator.StringToHash("isCharging");
        private static readonly int DeadHash = Animator.StringToHash("Dead");

        [Header("Refs")]
        [SerializeField] public Animator animator;
        [SerializeField] public NavMeshAgent agent;
        [SerializeField] public Rigidbody rb;
        [SerializeField] public SpiderAttackHitbox attackHitbox;   // optional – we also do physics impact here
        [SerializeField] public HealthComponent healthComponent;
        [SerializeField] public UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;
        [SerializeField] private ParticleSystem deathVFX;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip deathSfx;

        [Header("Target Layers")]
        [SerializeField] private LayerMask damageLayers = ~0;      // set to props/players/etc.
        [SerializeField] private LayerMask groundLayer = ~0;

        [Header("Ranges & Timers")]
        [SerializeField] public float chaseRange = 15f;
        [SerializeField] public float attackRange = 2f;
        [SerializeField] public float attackCooldown = 1.25f;
        [SerializeField] public float chargeTime = 1.5f; // Added for separate charge duration
        [SerializeField] public float idleTimeBeforeDive = 5f;

        [Header("Attack")]
        [SerializeField] public float normalAttackDamage = 10f;
        [SerializeField] public float attackRadius = 1.2f;
        [SerializeField] public float attackKnockback = 6f;        // VelocityChange
        [SerializeField] public ParticleSystem hitVfxPrefab;

        [Header("Kamikaze")]
        [SerializeField] public float kamikazeRange = 5f;
        [SerializeField] public float kamikazeAttackDamage = 50f;
        [SerializeField] public float kamikazeHealthThreshold = 0.19f;

        [Header("Dive")]
        [SerializeField] public float diveSpeed = 10f;
        [SerializeField] public float diveForceMultiplier = 1.5f;
        [SerializeField] public float diveTimeout = 3f;
        [SerializeField] public float diveDamage = 15f;

        [Header("Held/Thrown")]
        [SerializeField] public float maxHoldTime = 5f;
        [SerializeField] public float struggleShakeIntensity = 0.1f;

        [Header("Growth")]
        [SerializeField] private float growthTime = 30f;
        [SerializeField] private Vector3 startScale = new Vector3(0.5f, 0.5f, 0.5f);
        [SerializeField] private Vector3 fullScale = Vector3.one;

        [Header("NavMesh/Agent")]
        [SerializeField] private float baseAgentSpeed = 3.5f;
        [SerializeField] private float originalAgentRadius = 0.5f;
        [SerializeField] private float originalAgentHeight = 2f;
        [SerializeField] private float originalBaseOffset = 0f;

        [Header("Ground Check")]
        [SerializeField] private Transform groundCheckPoint;
        [SerializeField] private float groundRayDistance = 0.5f;
        [SerializeField] private float minFloorY = -10f;
        
        [SerializeField] private LayerMask losObstacles;     // walls, props, etc.
        private readonly Collider[] _targetBuffer = new Collider[32]; // reuse to avoid GC
        private const float StickinessFactor = 0.9f;

        // State
        public StateMachine stateMachine { get; private set; }
        public Transform currentTarget;

        public bool isGrounded { get; private set; }
        public bool isBeingHeld { get; private set; }
        public bool isInKamikazeMode;

        public float lastAttackTime;        // used by other states
        public float chargeTimer { get; private set; }
        public bool attackInProgress;
        public float nextAttackAllowed;

        // internal
        private Vector3 lastPosition;
        private Vector3 lastValidPosition;
        private Vector3 spawnPosition;
        private float growthTimer;
        private float targetRefreshTimer;
        private const float TargetRefreshInterval = 0.5f;
        private bool isThrown;
        private bool isBreakingHold;
        [SerializeField] public float stuckTimer;

        // Quick helpers
        private bool IsAgentValid => agent && agent.isActiveAndEnabled && agent.isOnNavMesh;

        // ===== Unity =====
        protected override void Awake()
        {
            base.Awake();

            stateMachine = new StateMachine();
            animator = animator ? animator : GetComponent<Animator>();
            rb       = rb       ? rb       : GetComponent<Rigidbody>();
            agent    = agent    ? agent    : GetComponent<NavMeshAgent>();
            healthComponent = healthComponent ? healthComponent : GetComponent<HealthComponent>() ?? gameObject.AddComponent<HealthComponent>();
            grabInteractable = grabInteractable ? grabInteractable : GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            audioSource = audioSource ? audioSource : GetComponent<AudioSource>();

            if (rb)
            {
                rb.useGravity = true;
                rb.isKinematic = false;
                rb.constraints = RigidbodyConstraints.FreezeRotation;
            }

            // IMPORTANT: keep agent disabled if not on mesh (prefab should have it disabled)
            if (agent)
            {
                if (!agent.isOnNavMesh) agent.enabled = false; // prevents “Failed to create agent …”
                agent.speed = Mathf.Max(0.01f, baseAgentSpeed);
                agent.updatePosition = false;
                agent.updateRotation = false;
                agent.stoppingDistance = 0f;
            }

            spawnPosition = transform.position;
            transform.localScale = startScale;
            ResetAnimator();
        }

        protected override void Start()
        {
            base.Start();

            if (grabInteractable)
            {
                grabInteractable.selectEntered.AddListener(_ => Pickup());
                grabInteractable.selectExited.AddListener(_ => Release());
            }

            if (healthComponent)
            {
                healthComponent.OnTakeDamage.AddListener(OnHealthChanged);
                healthComponent.OnDeath.AddListener(OnDeath);
            }

            currentTarget = GetClosestTarget();
            EnableAI(); // will only start if agent is valid
        }

        private void OnEnable()
        {
            ResetSpider(); // safe reset when pooled
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
        }

        private void Update()
        {
            isGrounded = IsGrounded();
            EnsureAgentOnNavMesh();

            stateMachine.Tick();
            CheckIfStuck();

            // Growth
            growthTimer += Time.deltaTime;
            float t = Mathf.Clamp01(growthTimer / growthTime);
            transform.localScale = Vector3.Lerp(startScale, fullScale, t);
            UpdateAgentOnScale();

            // Refresh target periodically
            targetRefreshTimer += Time.deltaTime;
            if (targetRefreshTimer >= TargetRefreshInterval)
            {
                GetClosestTarget();
                targetRefreshTimer = 0f;
            }

            // clamp falls
            if (transform.position.y < minFloorY && rb)
            {
                Vector3 p = transform.position;
                p.y = minFloorY;
                transform.position = p;
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            }

            // keep agent synced to body
            if (IsAgentValid)
            {
                agent.nextPosition = transform.position;
                lastValidPosition = transform.position;
            }
            if (IsAgentValid && !isBeingHeld && !isThrown &&
                !(stateMachine.CurrentState is DiveState) &&
                !(stateMachine.CurrentState is HeldState))
            {
                // While AI is controlling movement, turn off gravity and stick to mesh height
                if (rb) rb.useGravity = false;
                AlignToNavMeshHeight(); // smooth Y glue to NavMesh
            }
            else
            {
                if (rb) rb.useGravity = true; // free fall when thrown/held/dive
            }
            
            isGrounded = IsGrounded(); // Ensure this raycasts properly.
            if (transform.position.y < minFloorY) { stateMachine.ChangeState(new DeadState(this)); } // Fall death.
            if (healthComponent.IsDead() && !(stateMachine.CurrentState is DeadState)) { stateMachine.ChangeState(new DeadState(this)); }
        }

        public void HandleDamage(float dmg, Vector3 sourcePos) {
            healthComponent.TakeDamage(dmg);
            if (!healthComponent.IsDead()) stateMachine.ChangeState(new HurtState(this));
        }
        private void AlignToNavMeshHeight(float maxSnap = 1.5f, float lerp = 0.4f)
        {
            if (!IsAgentValid) return;
            if (NavMesh.SamplePosition(transform.position, out var hit, maxSnap, NavMesh.AllAreas))
            {
                var p = transform.position;
                p.y = Mathf.Lerp(p.y, hit.position.y + agent.baseOffset, lerp);
                transform.position = p;
                agent.nextPosition = p; // keep agent and body in sync
            }
        }
        private void FixedUpdate()
        {
            if (!enabled || !IsAgentValid || isBeingHeld || isThrown ||
                stateMachine.CurrentState is DiveState || stateMachine.CurrentState is HeldState)
                return;

            if (!rb) return;

            // Zero vertical velocity while AI is in control; height is handled by AlignToNavMeshHeight()
            rb.linearVelocity = new Vector3(
                Mathf.Lerp(rb.linearVelocity.x, agent.desiredVelocity.x, 0.8f),
                0f,
                Mathf.Lerp(rb.linearVelocity.z, agent.desiredVelocity.z, 0.8f)
            );

            if (agent.desiredVelocity.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(new Vector3(agent.desiredVelocity.x, 0f, agent.desiredVelocity.z));
        }

        // ===== Ground / NavMesh =====
        public bool IsGrounded()
        {
            float scaled = groundRayDistance * Mathf.Max(0.5f, transform.localScale.y);
            Vector3 origin = groundCheckPoint ? groundCheckPoint.position : transform.position;
            bool g = Physics.Raycast(origin, Vector3.down, scaled, groundLayer);
            isGrounded = g;
            if (animator) animator.SetBool(GroundedHash, g);
            return g;
        }

        public void EnsureAgentOnNavMesh()
        {
            if (!agent || isBeingHeld || isThrown || stateMachine.CurrentState is HeldState || stateMachine.CurrentState is DiveState)
                return;

            if (!agent.enabled)
                return; // we only enable it via TryMakeAgentReady

            if (!agent.isOnNavMesh)
            {
                float scale = Mathf.Max(0.5f, transform.localScale.y);
                if (NavMesh.SamplePosition(transform.position, out var hit, 50f * scale, NavMesh.AllAreas))
                {
                    transform.position = hit.position;
                    agent.Warp(hit.position);
                }
            }
        }

        private void UpdateAgentOnScale()
        {
            if (!agent) return;
            float s = Mathf.Clamp(transform.localScale.y, 0.5f, 1f);
            agent.radius = Mathf.Clamp(originalAgentRadius * s, 0.25f, 0.5f);
            agent.height = Mathf.Clamp(originalAgentHeight * s, 1f, 2f);
            agent.baseOffset = originalBaseOffset * s;
        }

        // ===== AI control =====
        public void EnableAI()
        {
            enabled = true;
            if (TryMakeAgentReady())
                stateMachine.ChangeState(new IdleState(this));
        }

        public void DisableAI()
        {
            enabled = false;
            if (attackHitbox) attackHitbox.gameObject.SetActive(false);
            ResetAnimator();
            stateMachine.ChangeState(null);
        }

        private void CheckIfStuck()
        {
            if (!isGrounded) { lastPosition = transform.position; return; }
            if (!IsAgentValid) { lastPosition = transform.position; return; }

            bool tryingToMove = agent.hasPath || agent.remainingDistance > 0.1f || agent.desiredVelocity.sqrMagnitude > 0.01f;
            if (!tryingToMove) { lastPosition = transform.position; return; }

            if (Vector3.Distance(transform.position, lastPosition) < 0.1f)
            {
                stuckTimer += Time.deltaTime;
                if (stuckTimer >= idleTimeBeforeDive && !isInKamikazeMode)
                    stateMachine.ChangeState(new DiveState(this));
            }
            else
            {
                stuckTimer = 0f;
                lastPosition = transform.position;
            }
        }

        // ===== Targeting =====
        public Transform GetClosestTarget(bool requireLineOfSight = true)
        {
            Transform best = currentTarget;             // start sticky: prefer what we already have
            float bestScore = best ? (best.position - transform.position).sqrMagnitude : float.PositiveInfinity;

            // Non-alloc query to reduce GC
            int count = Physics.OverlapSphereNonAlloc(transform.position, chaseRange, _targetBuffer, damageLayers, QueryTriggerInteraction.Ignore);

            Vector3 origin = transform.position;

            for (int i = 0; i < count; i++)
            {
                var t = _targetBuffer[i]?.transform;
                if (!t || t == transform) continue;

                // (Optional) filter: if you only want the player or things with HealthComponent, uncomment:
                // if (!t.TryGetComponent<HealthComponent>(out _)) continue;

                // Optional line-of-sight test
                if (requireLineOfSight)
                {
                    Vector3 targetPos = t.position + Vector3.up * 0.5f;
                    if (Physics.Linecast(origin + Vector3.up * 0.5f, targetPos, out RaycastHit hit, losObstacles, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.transform != t) continue; // something blocks LOS
                    }
                }

                float d2 = (t.position - origin).sqrMagnitude;

                // Stickiness: slightly discount the current target so we don't retarget unless clearly better
                if (best && t == best) d2 *= StickinessFactor;

                if (d2 < bestScore)
                {
                    bestScore = d2;
                    best = t;
                }
            }

            // If nothing found but we had a target and it's still within hard range, keep it
            if (!best && currentTarget)
            {
                float keep2 = (currentTarget.position - origin).sqrMagnitude;
                if (keep2 <= chaseRange * chaseRange) best = currentTarget;
            }

            currentTarget = best;
            return best;
        }

        public float GetDistanceToTarget()
        {
            if (!currentTarget) currentTarget = GetClosestTarget();
            if (currentTarget)
            {
                Vector3 flatPos = currentTarget.position;
                flatPos.y = transform.position.y; // Horizontal distance only
                return Vector3.Distance(transform.position, flatPos);
            }
            return Mathf.Infinity;
        }

        // ===== Charge / Attack =====
        public void StartCharge()
        {
            chargeTimer = 0f;
            if (animator)
            {
                animator.SetBool(IsChargingHash, true);
                animator.SetBool(IsRunningHash, false);
                animator.SetBool(GroundedHash, isGrounded);
                SafeReset("Attack");
            }
        }

        public bool IsChargeComplete()
        {
            chargeTimer += Time.deltaTime;
            return chargeTimer >= chargeTime;
        }

        public void ResetChargeTimer()
        {
            chargeTimer = 0f;
            if (animator) animator.SetBool(IsChargingHash, false);
        }

        public void PerformAttack()
        {
            if (attackInProgress || Time.time < nextAttackAllowed) return;

            attackInProgress = true;
            lastAttackTime = Time.time;
            nextAttackAllowed = Time.time + attackCooldown;

            if (animator) SafeSet("Attack");

            // instant physics impact + damage
            DoPhysicsMeleeImpact(transform.position, attackRadius, normalAttackDamage, attackKnockback);

            // optional legacy hitbox usage
            if (attackHitbox)
            {
                attackHitbox.SetOwner(gameObject);
                attackHitbox.Activate(0.2f);
            }
        }

        public void OnAttackEnd() => attackInProgress = false;  // call from animation event (optional)

        public void PerformKamikazeAttack()
        {
            if (healthComponent && healthComponent.IsDead()) return;

            // damage + knockback around self then die
            DoPhysicsMeleeImpact(transform.position, kamikazeRange, kamikazeAttackDamage, attackKnockback * 1.25f);
            healthComponent?.Kill(gameObject);
        }

        public IEnumerator PerformDiveAttackCoroutine()
        {
            // small window where we do a “sweep” impact once dive starts
            DoPhysicsMeleeImpact(transform.position, attackRadius * 1.25f, diveDamage, attackKnockback);
            yield return null;
        }

        /// <summary>
        /// Apply damage + instant knockback to overlapped targets. Spawns VFX at contact point.
        /// </summary>
        public void DoPhysicsMeleeImpact(Vector3 center, float radius, float damage, float knockback)
        {
            var colliders = Physics.OverlapSphere(center, radius, damageLayers, QueryTriggerInteraction.Ignore);
            foreach (var col in colliders)
            {
                if (!col || col.transform == transform) continue;

                // contact point and direction
                Vector3 contact = col.ClosestPoint(center);
                Vector3 dir = (contact - center);
                if (dir.sqrMagnitude < 0.0001f) dir = (col.attachedRigidbody ? col.attachedRigidbody.worldCenterOfMass : col.transform.position) - center;
                if (dir.sqrMagnitude < 0.0001f) dir = Vector3.up;
                dir.Normalize();

                // damage
                var hc = col.GetComponentInParent<HealthComponent>();
                if (hc) hc.TakeDamage(damage, contact, gameObject);

                // knockback
                var body = col.attachedRigidbody;
                if (body && !body.isKinematic)
                {
                    body.WakeUp();
                    body.AddForce(dir * knockback, ForceMode.VelocityChange);
                }

                // VFX
                if (hitVfxPrefab)
                {
                    var v = Instantiate(hitVfxPrefab, contact, Quaternion.LookRotation(dir));
                    Destroy(v.gameObject, 2f);
                }
            }
        }

        public void EnterKamikazeMode()
        {
            if (isInKamikazeMode) return;
            isInKamikazeMode = true;
            if (agent) agent.speed = baseAgentSpeed * 1.5f;
            stateMachine.ChangeState(new KamikazeState(this));
        }

        // ===== Health =====
        public override void TakeDamage(float damageAmount, Vector3 hitPoint = default, GameObject damageSource = null)
        {
            healthComponent?.TakeDamage(damageAmount, hitPoint, damageSource);
        }

        private void OnHealthChanged(HealthComponent.HealthChangedEventArgs e)
        {
            if (healthComponent.IsDead() || isBeingHeld) return;

            stateMachine.ChangeState(new HurtState(this));

            if (healthComponent.GetHealthPercentage() <= kamikazeHealthThreshold)
                EnterKamikazeMode();
        }

        private void OnDeath(HealthComponent _)
        {
            stateMachine.ChangeState(new DeadState(this));
            if (animator) animator.SetTrigger(DeadHash);
            if (deathVFX) deathVFX.Play();
            if (audioSource && deathSfx) audioSource.PlayOneShot(deathSfx);

            StartCoroutine(DelayedScaleDown(2f));
        }

        public void SetMaxHealth(float v) => healthComponent?.SetMaxHealth(v);

        // ===== Grab / Throw =====
        public void Pickup()
        {
            isBeingHeld = true;
            isThrown = false;

            if (agent && agent.enabled) agent.isStopped = true;
            if (attackHitbox) attackHitbox.gameObject.SetActive(false);

            ResetChargeTimer();

            if (animator)
            {
                animator.SetBool(GroundedHash, false);
                animator.SetBool(IsRunningHash, false);
                animator.SetBool(IsChargingHash, false);
                SafeReset("Attack");
            }

            stateMachine.ChangeState(new HeldState(this));
        }

        public void Release()
        {
            isBeingHeld = false;
            currentTarget = null;

            if (attackHitbox) attackHitbox.gameObject.SetActive(false);

            if (animator)
            {
                animator.SetBool(GroundedHash, isGrounded);
                animator.SetBool(IsRunningHash, false);
                animator.SetBool(IsChargingHash, false);
                SafeReset("Attack");
            }

            // let it fly; we re-enable agent after it lands
            if (agent) agent.enabled = false;

            if (isBreakingHold)
            {
                isBreakingHold = false;
                stateMachine.ChangeState(new DiveState(this));
            }
            else
            {
                isThrown = true;
                StartCoroutine(ResumeAIWithDelay(0.5f));
            }
        }

        private IEnumerator ResumeAIWithDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (!isBeingHeld && isThrown)
            {
                if (rb)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                TryMakeAgentReady();
                isThrown = false;
            }
        }

        // ===== Death / Reset =====
        public void Despawn()
        {
            if (healthComponent) healthComponent.Kill(gameObject);
            else StartCoroutine(ScaleDownAndDisable());
        }

        private IEnumerator DelayedScaleDown(float delay)
        {
            yield return new WaitForSeconds(delay);
            yield return ScaleDownAndDisable();
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
            // allow reset from pool/respawn
            if (rb)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            isThrown = false;
            isBeingHeld = false;
            isBreakingHold = false;
            isInKamikazeMode = false;
            lastAttackTime = 0f;
            chargeTimer = 0f;

            transform.localScale = startScale;
            growthTimer = 0f;

            if (attackHitbox) attackHitbox.gameObject.SetActive(false);
            ResetAnimator();
            healthComponent?.ResetHealth();

            // DO NOT enable agent here; wait for TryMakeAgentReady()
            if (TryMakeAgentReady())
                stateMachine.ChangeState(new IdleState(this));
            else
                stateMachine.ChangeState(new IdleState(this)); // will idle until we can resume
        }

        private void ResetAnimator()
        {
            if (!animator) return;
            animator.SetBool(GroundedHash, isGrounded);
            animator.SetBool(IsRunningHash, false);
            animator.SetBool(IsChargingHash, false);
            SafeReset("Attack");
        }

        // ===== Agent helpers =====
        public bool AgentReady() => agent && agent.isActiveAndEnabled && agent.isOnNavMesh;

        // Call to (re)enable + place the agent on NavMesh when close enough
        public bool TryMakeAgentReady(float sampleRadius = 100f)
        {
            if (!agent) return false;

            // Always disable before we try to place the agent to avoid the "create agent" error.
            if (agent.enabled) agent.enabled = false;

            float scale = Mathf.Max(0.5f, transform.localScale.y);
            if (!NavMesh.SamplePosition(transform.position, out var hit, sampleRadius * scale, NavMesh.AllAreas))
                return false;

            // Move transform first, then enable + Warp.
            transform.position = hit.position;
            agent.enabled = true;
            agent.Warp(hit.position);
            agent.isStopped = false;
            return agent.isOnNavMesh;
        }

        public bool TryResumeAgent()
        {
            if (!TryMakeAgentReady()) return false;
            agent.isStopped = false;
            return true;
        }

        public void SafeStopAgent()
        {
            if (agent && agent.isActiveAndEnabled) agent.isStopped = true;
        }

        // ===== Animator helpers (avoid warnings when a trigger is missing) =====
        protected bool HasTrigger(string name)
        {
            if (!animator) return false;
            foreach (var p in animator.parameters)
                if (p.type == AnimatorControllerParameterType.Trigger && p.name == name)
                    return true;
            return false;
        }
        public void SafeSet(string trig)   { if (HasTrigger(trig)) animator.SetTrigger(trig); } // Made public
        public void SafeReset(string trig) { if (HasTrigger(trig)) animator.ResetTrigger(trig); } // Made public

        private void OnCollisionEnter(Collision collision)
        {
            if (stateMachine.CurrentState is DiveState)
            {
                // end dive when you hit anything solid (optional)
                stateMachine.ChangeState(new IdleState(this));
            }
        }

        public void ForceBreakHold()
        {
            if (!isBeingHeld) return;

            isBreakingHold = true;

            // Optional: Shake for visual feedback
            StartCoroutine(ShakeCoroutine(0.5f));

            // Force release
            if (grabInteractable && grabInteractable.isSelected)
            {
                var interactor = UnityEngine.XR.Interaction.Toolkit.Interactables.XRSelectInteractableExtensions.GetOldestInteractorSelecting((UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)grabInteractable);
                grabInteractable.interactionManager.SelectExit((UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor)interactor, (UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)grabInteractable);
            }
            else
            {
                Release();
            }
        }

        private IEnumerator ShakeCoroutine(float duration)
        {
            float timer = 0f;
            Vector3 originalPos = transform.localPosition;
            while (timer < duration)
            {
                transform.localPosition = originalPos + Random.insideUnitSphere * struggleShakeIntensity;
                timer += Time.deltaTime;
                yield return null;
            }
            transform.localPosition = originalPos;
        }

        public void PlayDeathEffects(bool isKamikaze = false)
        {
            if (animator) animator.SetTrigger(DeadHash);

            if (deathVFX)
            {
                deathVFX.Play();
                if (isKamikaze) deathVFX.transform.localScale *= 1.5f; // Bigger for kamikaze
            }

            if (audioSource && deathSfx) audioSource.PlayOneShot(deathSfx);
        }
    }
}
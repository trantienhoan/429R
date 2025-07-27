using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using UnityEngine.XR.Interaction.Toolkit;

namespace Enemies
{
    public class ShadowMonster : MonoBehaviour
    {
        private enum MonsterState { OnAir, Idle, Wandering, Chasing, Charging, Attacking, Held, Dead }
        private MonsterState currentState = MonsterState.OnAir;

        [Header("AI Settings")]
        [SerializeField] private float chaseRange = 15f;
        [SerializeField] private float attackRange = 2f;
        [SerializeField] private float wanderRadius = 5f;
        [SerializeField] private float wanderDelay = 5f;

        [Header("Explosion Settings")]
        [SerializeField] private float chargeDelay = 2.5f;
        [SerializeField] private float damageAmount = 50f;
        [SerializeField] private GameObject attackHitbox;
        [SerializeField] private float postExplosionCooldown = 1f;

        [Header("Health Settings")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float invulnerabilityDuration = 0.5f;
        private float currentHealth;
        private bool isDead = false;
        private bool isInvulnerable = false;
        private bool isCharging = false;
        private bool canExplode = true;

        [Header("Grounding")]
        [SerializeField] private Transform groundCheckPoint;
        [SerializeField] private float groundCheckDistance = 0.3f;
        [SerializeField] private LayerMask groundLayer;
        private bool isGrounded;

        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private ParticleSystem hitVFX;
        [SerializeField] private AudioClip hitSFX;
        private AudioSource audioSource;
        private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;

        private static readonly int IdleHash = Animator.StringToHash("Idle");
        private static readonly int IdleOnAirHash = Animator.StringToHash("Spider_Idle_On_Air");
        private static readonly int ChargeHash = Animator.StringToHash("Spider_Charge");
        private static readonly int AttackHash = Animator.StringToHash("Spider_Attack");
        private static readonly int DieHash = Animator.StringToHash("Spider_Die");
        private static readonly int HurtHash = Animator.StringToHash("Spider_Hurt");
        private static readonly int IsGroundedParam = Animator.StringToHash("IsGrounded");
        private static readonly int IsChargingParam = Animator.StringToHash("IsCharging");
        private static readonly int IsDeadParam = Animator.StringToHash("IsDead");
        private static readonly int TriggerHurtParam = Animator.StringToHash("TriggerHurt");
        private static readonly int TriggerAttackParam = Animator.StringToHash("TriggerAttack");
        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int WalkCycleHash = Animator.StringToHash("Spider_Walk_Cycle");

        private Coroutine chargingRoutine;
        private GameObject target;
        private float lastWanderTime;

        private void Awake()
        {
            currentHealth = maxHealth;
            audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
            agent = agent ?? GetComponent<NavMeshAgent>();
            animator = animator ?? GetComponent<Animator>();
            grabInteractable = grabInteractable ?? GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();

            if (agent == null || animator == null || grabInteractable == null)
            {
                Debug.LogError($"Missing component(s) on {gameObject.name}: NavMeshAgent: {agent}, Animator: {animator}, XRGrabInteractable: {grabInteractable}");
                enabled = false;
                return;
            }

            grabInteractable.selectEntered.AddListener(OnGrabbed);
            grabInteractable.selectExited.AddListener(OnReleased);
        }

        private void OnEnable() => ResetSpider();

        private void OnDestroy()
        {
            if (grabInteractable != null)
            {
                grabInteractable.selectEntered.RemoveListener(OnGrabbed);
                grabInteractable.selectExited.RemoveListener(OnReleased);
            }
        }

        private void FixedUpdate() 
        {
    if (isDead || currentState == MonsterState.Dead || currentState == MonsterState.Held) return;

    CheckGrounded();
    UpdateAnimatorParameters();

    // Handle grounding state transitions
    if (currentState == MonsterState.OnAir && isGrounded)
        ChangeState(MonsterState.Idle);
    else if (currentState != MonsterState.OnAir && !isGrounded)
        ChangeState(MonsterState.OnAir);

    switch (currentState)
    {
        case MonsterState.Idle:
            FindTarget();
            if (target != null && Vector3.Distance(transform.position, target.transform.position) <= chaseRange)
            {
                ChangeState(MonsterState.Chasing);
            }
            else if (Time.time - lastWanderTime > wanderDelay)
            {
                ChangeState(MonsterState.Wandering);
            }
            break;

        case MonsterState.Wandering:
            if (!agent.enabled || !agent.isOnNavMesh)
            {
                if (TryWarpToNavMesh())
                {
                    agent.isStopped = false;
                    Wander(); // Set initial wander destination
                }
            }
            else if (agent.remainingDistance <= agent.stoppingDistance && !agent.pathPending)
            {
                // Only set new wander destination when current path is complete
                if (Time.time - lastWanderTime > wanderDelay)
                {
                    Wander();
                    lastWanderTime = Time.time;
                }
            }
            // Check for target while wandering
            FindTarget();
            if (target != null && Vector3.Distance(transform.position, target.transform.position) <= chaseRange)
            {
                ChangeState(MonsterState.Chasing);
            }
            PlayAnimation(WalkCycleHash);
            break;

        case MonsterState.Chasing:
            if (!agent.enabled || !agent.isOnNavMesh)
            {
                if (TryWarpToNavMesh())
                {
                    agent.isStopped = false;
                    ChaseTarget(); // Set initial chase destination
                }
            }
            else
            {
                ChaseTarget(); // Continuously update target position
                if (target != null && Vector3.Distance(transform.position, target.transform.position) <= attackRange)
                {
                    ChangeState(MonsterState.Charging);
                }
                else if (target == null || Vector3.Distance(transform.position, target.transform.position) > chaseRange)
                {
                    ChangeState(MonsterState.Idle);
                }
            }
            PlayAnimation(WalkCycleHash);
            break;

        case MonsterState.Charging:
            if (target == null || Vector3.Distance(transform.position, target.transform.position) > attackRange)
            {
                isCharging = false;
                if (chargingRoutine != null) StopCoroutine(chargingRoutine);
                ChangeState(MonsterState.Idle);
            }
            break;
    }
}

        private void CheckGrounded()
        {
            isGrounded = Physics.CheckSphere(groundCheckPoint.position, groundCheckDistance, groundLayer, QueryTriggerInteraction.Ignore);
            Debug.Log($"Ground check: isGrounded={isGrounded}, position={groundCheckPoint.position}");
            if (isGrounded)
                Debug.DrawRay(groundCheckPoint.position, Vector3.down * groundCheckDistance, Color.green, 0.1f);
            else
                Debug.DrawRay(groundCheckPoint.position, Vector3.down * groundCheckDistance, Color.red, 0.1f);
        }

        private bool TryWarpToNavMesh()
        {
            float searchRadius = 10f; // Increase radius for better chance of finding a spot
            NavMeshAgent agent = GetComponent<NavMeshAgent>();

            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, searchRadius, NavMesh.AllAreas))
            {
                agent.enabled = true;
                agent.Warp(hit.position);
                if (agent.isOnNavMesh)
                {
                    agent.ResetPath();
                    return true;
                }
                else
                {
                    agent.enabled = false; // Disable if warp didn't place on NavMesh
                    Debug.LogWarning($"ShadowMonster at {transform.position} failed to warp to valid NavMesh position.");
                    return false;
                }
            }
            else
            {
                // Fallback: Find the nearest NavMesh edge
                if (NavMesh.FindClosestEdge(transform.position, out hit, NavMesh.AllAreas))
                {
                    agent.enabled = true;
                    agent.Warp(hit.position);
                    if (agent.isOnNavMesh)
                    {
                        agent.ResetPath();
                        return true;
                    }
                    else
                    {
                        agent.enabled = false;
                        Debug.LogWarning($"ShadowMonster at {transform.position} failed to warp to NavMesh edge.");
                        return false;
                    }
                }
                else
                {
                    Debug.LogWarning($"ShadowMonster at {transform.position} could not find a valid NavMesh position within {searchRadius} units.");
                    agent.enabled = false;
                    return false;
                }
            }
        }

        private void ChangeState(MonsterState newState)
        {
    if (isDead || currentState == newState) return;
    Debug.Log($"Changing state from {currentState} to {newState}, isGrounded={isGrounded}");

    currentState = newState;

    if (animator != null)
    {
        animator.SetBool(IsGroundedParam, isGrounded);
        animator.SetBool(IsChargingParam, newState == MonsterState.Charging);
        animator.SetBool(IsDeadParam, newState == MonsterState.Dead);
        animator.SetFloat(SpeedParam, 0f);
    }

    switch (newState)
    {
        case MonsterState.OnAir:
            if (agent.enabled) agent.enabled = false;
            PlayAnimation(IdleOnAirHash);
            break;

        case MonsterState.Idle:
            if (!agent.enabled && isGrounded && TryWarpToNavMesh())
            {
                agent.isStopped = true;
            }
            PlayAnimation(IdleHash);
            break;

        case MonsterState.Wandering:
            lastWanderTime = Time.time;
            if (!agent.enabled && TryWarpToNavMesh())
            {
                agent.isStopped = false;
                Wander();
            }
            else if (agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                Wander();
            }
            PlayAnimation(WalkCycleHash);
            break;

        case MonsterState.Chasing:
            if (!agent.enabled && TryWarpToNavMesh())
            {
                agent.isStopped = false;
                ChaseTarget();
            }
            else if (agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                ChaseTarget();
            }
            PlayAnimation(WalkCycleHash);
            break;

        case MonsterState.Charging:
            isCharging = true;
            if (agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
            PlayAnimation(ChargeHash);
            chargingRoutine = StartCoroutine(StartExplosionAfterDelay());
            break;

        case MonsterState.Attacking:
            if (agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
            PlayAnimation(AttackHash);
            StartCoroutine(ExplodeDuringAttack());
            break;

        case MonsterState.Dead:
            if (agent.enabled) agent.enabled = false;
            PlayAnimation(DieHash);
            break;

        case MonsterState.Held:
            if (agent.enabled) agent.enabled = false;
            PlayAnimation(IdleOnAirHash);
            break;
    }
}

        private void FindTarget()
        {
            GameObject closest = null;
            float closestDist = Mathf.Infinity;
            string[] priorityTags = { "Player", "TreeOfLight", "Furniture" };

            foreach (string tag in priorityTags)
            {
                var objects = GameObject.FindGameObjectsWithTag(tag);
                foreach (var obj in objects)
                {
                    if (obj == null) continue;
                    float dist = Vector3.Distance(transform.position, obj.transform.position);
                    if (dist < chaseRange && dist < closestDist)
                    {
                        closest = obj;
                        closestDist = dist;
                    }
                }
                if (closest != null) break;
            }

            target = closest;
        }

        private void ChaseTarget()
        {
            if (agent.enabled && target != null && agent.isOnNavMesh)
            {
                agent.SetDestination(target.transform.position);
                Debug.Log($"Chasing target at {target.transform.position}");
            }
        }

        private void Wander()
        {
            if (!agent.enabled || !agent.isOnNavMesh) return;
            Vector3 randomPoint = transform.position + Random.insideUnitSphere * wanderRadius;
            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
                Debug.Log($"Wander destination set to {hit.position}");
            }
        }

        private IEnumerator StartExplosionAfterDelay()
        {
            yield return new WaitForSeconds(chargeDelay);
            if (isDead || !canExplode || currentState != MonsterState.Charging)
            {
                isCharging = false;
                ChangeState(MonsterState.Idle);
                yield break;
            }
            ChangeState(MonsterState.Attacking);
        }

        private IEnumerator ExplodeDuringAttack()
        {
            yield return new WaitForSeconds(0.3f);
            if (isDead || target == null || !canExplode)
            {
                ChangeState(MonsterState.Idle);
                yield break;
            }

            if (attackHitbox == null)
            {
                Debug.LogError("Attack hitbox is not assigned!");
                ChangeState(MonsterState.Idle);
                yield break;
            }

            attackHitbox.SetActive(true);
            if (attackHitbox.TryGetComponent(out SpiderAttackHitbox hitbox))
            {
                hitbox.Initialize(gameObject, damageAmount, 5f);
                hitbox.TriggerExplosion();
            }

            yield return new WaitForSeconds(0.3f);
            attackHitbox.SetActive(false);

            yield return new WaitForSeconds(postExplosionCooldown);
            Kill(gameObject);
        }

        public void TakeDamage(float damage, Vector3 hitPoint, GameObject source = null)
        {
            if (isDead || isInvulnerable) return;

            currentHealth -= damage;
            SpawnHitVFX(hitPoint);
            PlayHurtAnimation();

            if (currentState == MonsterState.Charging)
            {
                isCharging = false;
                canExplode = false;
                if (chargingRoutine != null) StopCoroutine(chargingRoutine);
                ChangeState(MonsterState.Idle);
            }

            if (currentHealth <= 0)
                Kill(source);
            else
                StartCoroutine(InvulnerabilityRoutine());
        }

        private void SpawnHitVFX(Vector3 pos)
        {
            if (hitVFX != null)
            {
                var fx = Instantiate(hitVFX, pos, Quaternion.identity);
                fx.Play();
                Destroy(fx.gameObject, fx.main.duration + 0.5f);
            }
            if (audioSource && hitSFX) audioSource.PlayOneShot(hitSFX);
        }

        private void PlayHurtAnimation()
        {
            if (animator != null && !animator.GetCurrentAnimatorStateInfo(0).IsName("Spider_Hurt"))
            {
                animator.SetTrigger(TriggerHurtParam);
                StartCoroutine(ResetToIdleAfterHurt());
            }
        }

        private IEnumerator ResetToIdleAfterHurt()
        {
            yield return new WaitForSeconds(0.4f);
            if (!isDead && currentState != MonsterState.Charging && currentState != MonsterState.Attacking)
                ChangeState(isGrounded ? MonsterState.Idle : MonsterState.OnAir);
        }

        private IEnumerator InvulnerabilityRoutine()
        {
            isInvulnerable = true;
            yield return new WaitForSeconds(invulnerabilityDuration);
            isInvulnerable = false;
        }

        private void Kill(GameObject source = null)
        {
            if (isDead) return;

            isDead = true;
            isCharging = false;
            canExplode = false;

            if (chargingRoutine != null) StopCoroutine(chargingRoutine);
            StopAllCoroutines();
            ChangeState(MonsterState.Dead);
            StartCoroutine(ScaleDownAndDestroy());
        }

        private IEnumerator ScaleDownAndDestroy()
        {
            float duration = 0.5f;
            Vector3 original = transform.localScale;
            float t = 0;

            while (t < duration)
            {
                t += Time.deltaTime;
                transform.localScale = Vector3.Lerp(original, Vector3.zero, t / duration);
                yield return null;
            }

            if (SpiderPool.Instance != null)
                SpiderPool.Instance.ReturnSpider(gameObject);
            else
                Destroy(gameObject);
        }

        private void PlayAnimation(int hash)
        {
            if (animator == null || !animator.HasState(0, hash)) return;

            if (animator.GetCurrentAnimatorStateInfo(0).shortNameHash != hash)
            {
                animator.Play(hash, 0, 0);
            }
        }

        public void OnPickup()
        {
            if (isDead) return;

            if (currentState == MonsterState.Charging)
            {
                isCharging = false;
                canExplode = false;
                if (chargingRoutine != null) StopCoroutine(chargingRoutine);
                StopAllCoroutines();
            }

            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.enabled = false;
            }

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null) { Debug.LogError("Rigidbody missing!"); return; }
            rb.isKinematic = false;
            rb.useGravity = true;

            PlayAnimation(IdleOnAirHash);
            ChangeState(MonsterState.Held);
        }
        
        public void SetMaxHealth(float newMaxHealth)
        {
            maxHealth = newMaxHealth;
            currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth); // Ensure current health stays within bounds
        }

        // Add this method to check if the monster is grounded
        public bool IsGrounded()
        {
            float distanceToGround = 0.1f; // Adjust based on monster size
            return Physics.Raycast(transform.position, Vector3.down, distanceToGround);
        }

        // Add this method to enable AI behavior
        public void EnableAI()
        {
            if (!agent.enabled && TryWarpToNavMesh()) // Check if agent is disabled and warp if needed
            {
                agent.isStopped = false; // Start the agent's movement
                // Optionally, set an initial state here if your monster uses state logic
            }
        }

        public void OnRelease()
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null) { Debug.LogError("Rigidbody missing!"); return; }
            rb.isKinematic = false;
            rb.useGravity = true;
            StartCoroutine(WaitForLandingThenReset());
        }

        private IEnumerator WaitForLandingThenReset()
        {
            float timeout = 2f;
            float elapsed = 0f;
            while (!isGrounded && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (!isGrounded)
            {
                Debug.LogWarning($"ShadowMonster did not land within {timeout}s. Attempting to warp to NavMesh.");
            }

            if (TryWarpToNavMesh() && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
            }
            else
            {
                Debug.LogWarning("Warp to NavMesh failed, transitioning to OnAir state.");
            }

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
            else
            {
                Debug.LogError("Rigidbody missing!");
            }

            ChangeState(isGrounded ? MonsterState.Idle : MonsterState.OnAir);
        }

        private void OnGrabbed(SelectEnterEventArgs args) => OnPickup();
        private void OnReleased(SelectExitEventArgs args) => OnRelease();

        private void ResetSpider()
        {
            isDead = false;
            isCharging = false;
            canExplode = true;
            isInvulnerable = false;
            currentHealth = maxHealth;
            target = null;

            if (animator != null)
            {
                animator.Rebind();
                animator.Update(0f);
            }

            if (TryWarpToNavMesh()) agent.isStopped = true;

            transform.localScale = Vector3.one;
            ChangeState(isGrounded ? MonsterState.Idle : MonsterState.OnAir);
        }

        private void UpdateAnimatorParameters()
        {
            if (animator == null) return;

            animator.SetBool(IsGroundedParam, isGrounded);
            animator.SetBool(IsChargingParam, isCharging);
            animator.SetBool(IsDeadParam, isDead);

            float currentSpeed = agent?.velocity.magnitude ?? 0f;
            animator.SetFloat(SpeedParam, currentSpeed);
        }

        private void OnValidate()
        {
            if (chaseRange < attackRange) chaseRange = attackRange + 1f;
            if (attackRange <= 0) attackRange = 2f;
            if (maxHealth <= 0) maxHealth = 100f;
            if (groundCheckDistance <= 0) groundCheckDistance = 0.3f;
            if (chargeDelay <= 0) chargeDelay = 2.5f;
            if (postExplosionCooldown <= 0) postExplosionCooldown = 1f;
        }
    }
}
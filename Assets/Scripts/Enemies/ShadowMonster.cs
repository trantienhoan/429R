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
        private bool wasGroundedLastFrame;

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
        private static readonly int VelocityParam = Animator.StringToHash("Velocity");

        private Coroutine chargingRoutine;
        private GameObject target;
        private float lastWanderTime;
        private int currentAnimationHash = -1;
        private bool isPlayingHurtAnimation = false;
        private static readonly int WalkCycleHash = Animator.StringToHash("Spider_Walk_Cycle");

        private void Awake()
        {
            currentHealth = maxHealth;
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null && hitSFX != null)
                audioSource = gameObject.AddComponent<AudioSource>();
            
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            if (animator == null) animator = GetComponent<Animator>();
            grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();

            if (agent == null || animator == null || grabInteractable == null)
            {
                Debug.LogError($"Missing component(s) on {gameObject.name}: NavMeshAgent: {agent}, Animator: {animator}, XRGrabInteractable: {grabInteractable}");
                enabled = false;
                return;
            }

            Debug.Log($"IdleOnAirHash: {IdleOnAirHash} (Spider_Idle_On_Air)");
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

        private void Update()
        {
            if (isDead || currentState == MonsterState.Dead || currentState == MonsterState.Held)
                return;

            CheckGrounded();
            UpdateAnimatorParameters();

            if (currentState == MonsterState.OnAir && isGrounded)
            {
                Debug.Log("Transitioning from OnAir to Idle due to grounding");
                ChangeState(MonsterState.Idle);
            }
            else if (currentState != MonsterState.OnAir && !isGrounded)
            {
                Debug.Log("Transitioning to OnAir due to no ground");
                ChangeState(MonsterState.OnAir);
            }

            wasGroundedLastFrame = isGrounded;

            switch (currentState)
            {
                case MonsterState.OnAir:
                    break;

                case MonsterState.Idle:
                    FindTarget();
                    if (target != null && Vector3.Distance(transform.position, target.transform.position) <= chaseRange)
                    {
                        Debug.Log($"Found target: {target.name}, changing to chasing");
                        ChangeState(MonsterState.Chasing);
                    }
                    else if (Time.time - lastWanderTime > wanderDelay)
                    {
                        Debug.Log("Starting to wander");
                        ChangeState(MonsterState.Wandering);
                    }
                    break;

                case MonsterState.Wandering:
                    lastWanderTime = Time.time;
                    if (!agent.enabled && TryWarpToNavMesh())
                    {
                        Wander();
                    }
                    PlayAnimation(Animator.StringToHash("Spider_Walk_Cycle")); // Ensure walk animation exists
                    break;

                case MonsterState.Chasing:
                    if (!agent.enabled && TryWarpToNavMesh())
                    {
                        agent.isStopped = false;
                        ChaseTarget();
                    }
                    PlayAnimation(Animator.StringToHash("Spider_Walk_Cycle")); // Ensure walk animation exists
                    break;

                case MonsterState.Charging:
                    if (target == null || Vector3.Distance(transform.position, target.transform.position) > attackRange)
                    {
                        Debug.Log("Lost target or target out of attack range, returning to idle");
                        isCharging = false;
                        if (chargingRoutine != null)
                        {
                            StopCoroutine(chargingRoutine);
                            chargingRoutine = null;
                        }
                        ChangeState(MonsterState.Idle);
                    }
                    break;
            }
        }

        private void CheckGrounded()
        {
            isGrounded = Physics.Raycast(groundCheckPoint.position, Vector3.down, groundCheckDistance, groundLayer);
            Debug.DrawRay(groundCheckPoint.position, Vector3.down * groundCheckDistance, isGrounded ? Color.green : Color.red, 0.1f);
        }

        public bool IsGrounded() => isGrounded;

        public void EnableAI()
        {
            if (!agent.enabled && TryWarpToNavMesh())
            {
                agent.isStopped = false;
                ChangeState(isGrounded ? MonsterState.Idle : MonsterState.OnAir);
            }
        }

        private bool TryWarpToNavMesh()
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 1f, NavMesh.AllAreas))
            {
                agent.enabled = true;
                agent.Warp(hit.position);
                agent.ResetPath();
                return true;
            }
            Debug.LogError($"ShadowMonster at {transform.position} could not find a valid NavMesh position.");
            agent.enabled = false;
            return false;
        }

        private void ChangeState(MonsterState newState)
        {
    if (isDead || currentState == newState) return;

    Debug.Log($"Changing state from {currentState} to {newState}, isGrounded: {isGrounded}");
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
                Wander();
            }
            PlayAnimation(Animator.StringToHash("Spider_Walk_Cycle")); // Use walk animation
            break;

        case MonsterState.Chasing:
            if (!agent.enabled && TryWarpToNavMesh())
            {
                agent.isStopped = false;
            }
            PlayAnimation(Animator.StringToHash("Spider_Walk_Cycle")); // Use walk animation
            break;

        case MonsterState.Charging:
            isCharging = true;
            if (agent.enabled) agent.isStopped = true;
            PlayAnimation(ChargeHash);
            chargingRoutine = StartCoroutine(StartExplosionAfterDelay());
            break;

        case MonsterState.Attacking:
            if (agent.enabled) agent.isStopped = true;
            PlayAnimation(AttackHash);
            StartCoroutine(ExplodeDuringAttack());
            break;

        case MonsterState.Dead:
            if (agent.enabled) agent.enabled = false;
            PlayAnimation(DieHash);
            break;

        case MonsterState.Held:
            if (agent.enabled) agent.enabled = false;
            break;
    }
}
        private void FindTarget()
        {
            GameObject closest = null;
            float closestDist = Mathf.Infinity;
            string[] priorityTags = { "Player", "TreeOfLight", "Furniture" };

            Debug.Log("Searching for targets...");
            foreach (string tag in priorityTags)
            {
                var objects = GameObject.FindGameObjectsWithTag(tag);
                Debug.Log($"Found {objects.Length} objects with tag {tag}");
                foreach (var obj in objects)
                {
                    if (obj == null) continue;
                    float dist = Vector3.Distance(transform.position, obj.transform.position);
                    Debug.Log($"Checking {obj.name} at distance {dist}");
                    if (dist < chaseRange && dist < closestDist)
                    {
                        closest = obj;
                        closestDist = dist;
                    }
                }
                if (closest != null) break;
            }

            if (closest != target)
            {
                Debug.Log($"Target changed from {(target ? target.name : "null")} to {(closest ? closest.name : "null")}, distance: {closestDist}");
            }
            else if (closest == null)
            {
                Debug.Log("No valid target found within chase range");
            }

            target = closest;
        }

        private void ChaseTarget()
        {
            if (agent.enabled && target != null && agent.isOnNavMesh)
            {
                agent.SetDestination(target.transform.position);
                Debug.Log($"Chasing target {target.name} at {agent.destination}");
            }
            else
            {
                Debug.LogWarning("Chase failed: Agent disabled, no target, or not on NavMesh");
            }
        }

        private void Wander()
        {
            if (!agent.enabled || !agent.isOnNavMesh) return;
            Vector3 randomPoint = transform.position + Random.insideUnitSphere * wanderRadius;
            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
                Debug.Log($"Wandering to {hit.position}");
            }
            else
            {
                Debug.LogWarning("Wander failed: No valid NavMesh position");
            }
        }

        private IEnumerator StartExplosionAfterDelay()
        {
            Debug.Log("Starting charge delay");
            yield return new WaitForSeconds(chargeDelay);
            if (isDead || !canExplode || currentState != MonsterState.Charging)
            {
                Debug.Log("Explosion aborted: Monster is dead, cannot explode, or not in Charging state");
                isCharging = false;
                ChangeState(MonsterState.Idle);
                yield break;
            }
            Debug.Log("Charge complete, transitioning to attacking");
            ChangeState(MonsterState.Attacking);
        }

        private IEnumerator ExplodeDuringAttack()
        {
            yield return new WaitForSeconds(0.3f);
            if (isDead || target == null || !canExplode)
            {
                Debug.Log("Explosion aborted: Monster is dead or no target");
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
            else
            {
                Debug.LogError("SpiderAttackHitbox component missing on attack hitbox!");
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
            if (audioSource && hitSFX)
                audioSource.PlayOneShot(hitSFX);
        }

        private void PlayHurtAnimation()
        {
            if (animator != null && !isPlayingHurtAnimation)
            {
                isPlayingHurtAnimation = true;
                animator.SetTrigger(TriggerHurtParam);
                StartCoroutine(ResetToIdleAfterHurt());
            }
        }

        private IEnumerator ResetToIdleAfterHurt()
        {
            yield return new WaitForSeconds(0.4f);
            isPlayingHurtAnimation = false;
            if (!isDead && currentState != MonsterState.Charging && currentState != MonsterState.Attacking)
            {
                currentAnimationHash = -1;
                ChangeState(isGrounded ? MonsterState.Idle : MonsterState.OnAir);
            }
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

            Debug.Log("Spider is being killed");
            isDead = true;
            isCharging = false;
            canExplode = false;

            if (chargingRoutine != null)
            {
                StopCoroutine(chargingRoutine);
                chargingRoutine = null;
            }

            StopAllCoroutines();
            ChangeState(MonsterState.Dead);
            StartCoroutine(ScaleDownAndDestroy());
        }

        private IEnumerator ScaleDownAndDestroy()
        {
            Debug.Log("Starting death animation");
            float duration = 0.5f;
            Vector3 original = transform.localScale;
            float t = 0;

            while (t < duration)
            {
                t += Time.deltaTime;
                transform.localScale = Vector3.Lerp(original, Vector3.zero, t / duration);
                yield return null;
            }

            Debug.Log("Death animation complete, returning to pool");
            if (SpiderPool.Instance != null)
            {
                SpiderPool.Instance.ReturnSpider(gameObject);
            }
            else
            {
                Debug.LogWarning("SpiderPool not found, destroying GameObject directly");
                Destroy(gameObject);
            }
        }

        private void PlayAnimation(int hash)
        {
            if (animator == null || animator.layerCount == 0)
            {
                Debug.LogError("Animator is null or has no layers!");
                return;
            }

            if (!animator.HasState(0, hash))
            {
                Debug.LogError($"Animation state with hash {hash} not found in animator");
                return;
            }

            AnimatorStateInfo currentStateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (currentStateInfo.shortNameHash == hash)
            {
                Debug.Log($"Animation {hash} already playing, skipping");
                return;
            }

            Debug.Log($"Playing animation with hash {hash} for state {currentState}");
            SetAnimationParameters(hash);
            animator.Play(hash, 0, 0);
            currentAnimationHash = hash;
        }

        private void SetAnimationParameters(int hash)
        {
            if (animator == null) return;

            animator.SetBool(IsChargingParam, false);
            animator.SetBool(IsDeadParam, false);
            animator.SetFloat(SpeedParam, 0f);

            if (hash == IdleHash)
            {
                animator.SetBool(IsGroundedParam, true);
            }
            else if (hash == IdleOnAirHash)
            {
                animator.SetBool(IsGroundedParam, false);
            }
            else if (hash == ChargeHash)
            {
                animator.SetBool(IsChargingParam, true);
            }
            else if (hash == AttackHash)
            {
                animator.SetTrigger(TriggerAttackParam);
            }
            else if (hash == DieHash)
            {
                animator.SetBool(IsDeadParam, true);
            }
        }

        public void SetMaxHealth(float newMaxHealth)
        {
            maxHealth = newMaxHealth;
            currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        }

        private MonsterState previousState;
        
        public void OnPickup()
        {
            if (isDead) return;

            Debug.Log("ShadowMonster picked up");
            previousState = currentState; // Store current state
            if (currentState == MonsterState.Charging)
            {
                isCharging = false;
                canExplode = false;
                if (chargingRoutine != null)
                {
                    StopCoroutine(chargingRoutine);
                    chargingRoutine = null;
                }
                StopAllCoroutines();
            }

            if (agent.enabled)
            {
                agent.isStopped = true;
                agent.enabled = false;
            }

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                Debug.LogError("Rigidbody missing on ShadowMonster!");
                return;
            }
            rb.isKinematic = false;
            rb.useGravity = true;

            if (animator != null)
            {
                animator.SetBool(IsChargingParam, false);
                animator.SetBool(IsGroundedParam, false);
                PlayAnimation(IdleOnAirHash); // Play Spider_Idle_On_Air
            }

            ChangeState(MonsterState.Held);
        }

        public void OnRelease()
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                Debug.LogError("Rigidbody missing on ShadowMonster!");
                return;
            }
            rb.isKinematic = false;
            rb.useGravity = true;
            StartCoroutine(WaitForLandingThenReset());
        }

        private IEnumerator WaitForLandingThenReset()
        {
            float timeout = 5f;
            float elapsed = 0f;
            while (!isGrounded && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (!isGrounded)
            {
                Debug.LogWarning("Monster failed to land within timeout, forcing reset");
            }

            if (TryWarpToNavMesh())
            {
                agent.isStopped = true;
            }

            Rigidbody rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            if (previousState == MonsterState.Charging && target != null && Vector3.Distance(transform.position, target.transform.position) <= attackRange)
            {
                Debug.Log("Resuming charging after release");
                ChangeState(MonsterState.Charging);
            }
            else
            {
                ChangeState(isGrounded ? MonsterState.Idle : MonsterState.OnAir);
            }
        }

        private void OnGrabbed(SelectEnterEventArgs args) => OnPickup();
        private void OnReleased(SelectExitEventArgs args) => OnRelease();

        // private IEnumerator WaitForLandingThenReset()
        // {
        //     float timeout = 5f;
        //     float elapsed = 0f;
        //     while (!isGrounded && elapsed < timeout)
        //     {
        //         elapsed += Time.deltaTime;
        //         yield return null;
        //     }
        //
        //     if (!isGrounded)
        //     {
        //         Debug.LogWarning("Monster failed to land within timeout, forcing reset");
        //     }
        //
        //     if (TryWarpToNavMesh())
        //     {
        //         agent.isStopped = true;
        //     }
        //
        //     Rigidbody rb = GetComponent<Rigidbody>();
        //     rb.isKinematic = true;
        //     rb.useGravity = false;
        //     ChangeState(MonsterState.Idle);
        // }

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

            if (TryWarpToNavMesh())
            {
                agent.isStopped = true;
            }

            transform.localScale = Vector3.one;
            CheckGrounded();
            wasGroundedLastFrame = false;
            ChangeState(isGrounded ? MonsterState.Idle : MonsterState.OnAir);
        }

        private void UpdateAnimatorParameters()
        {
            if (animator == null) return;

            animator.SetBool(IsGroundedParam, isGrounded);
            animator.SetBool(IsChargingParam, isCharging);
            animator.SetBool(IsDeadParam, isDead);

            float currentSpeed = 0f;
            if (agent != null && agent.enabled && agent.hasPath && !agent.isStopped)
            {
                currentSpeed = agent.velocity.magnitude;
            }

            animator.SetFloat(SpeedParam, currentSpeed);
            animator.SetFloat(VelocityParam, currentSpeed);
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
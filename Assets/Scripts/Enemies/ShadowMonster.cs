using UnityEngine;
using UnityEngine.AI;
using System.Collections;

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

        private static readonly int IdleHash = Animator.StringToHash("Idle");
        private static readonly int IdleOnAirHash = Animator.StringToHash("Spider_Idle_On_Air");
        private static readonly int ChargeHash = Animator.StringToHash("Spider_Charge");
        private static readonly int AttackHash = Animator.StringToHash("Spider_Attack");
        private static readonly int DieHash = Animator.StringToHash("Spider_Die");
        private static readonly int HurtHash = Animator.StringToHash("Spider_Hurt");
        
        private static readonly int IsGroundedParam = Animator.StringToHash("IsGrounded");
        private static readonly int IsChargingParam = Animator.StringToHash("IsCharging");
        private static readonly int IsDeadParam = Animator.StringToHash("IsDead");
        private static readonly int StateParam = Animator.StringToHash("State");
        private static readonly int TriggerHurtParam = Animator.StringToHash("TriggerHurt");
        private static readonly int TriggerAttackParam = Animator.StringToHash("TriggerAttack");
        
        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int VelocityParam = Animator.StringToHash("Velocity");
        
        // Fix the walk animation hash
        private static readonly int WalkHash = Animator.StringToHash("Spider_Walk_Cycle");

        private Coroutine chargingRoutine;
        private GameObject target;
        private float lastWanderTime;
        private int currentAnimationHash = -1;
        private bool isPlayingHurtAnimation = false;

        private void Awake()
        {
            currentHealth = maxHealth;
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null && hitSFX != null)
                audioSource = gameObject.AddComponent<AudioSource>();
        }

        private void OnEnable() => ResetSpider();

        private void Update()
        {
            if (isDead || currentState == MonsterState.Dead || currentState == MonsterState.Held)
                return;

            CheckGrounded();
            UpdateAnimatorParameters();

            // Fix the grounding state transitions
            if (currentState == MonsterState.OnAir && isGrounded)
            {
                ChangeState(MonsterState.Idle);
            }
            else if (currentState != MonsterState.OnAir && !isGrounded)
            {
                ChangeState(MonsterState.OnAir);
            }

            wasGroundedLastFrame = isGrounded;

            switch (currentState)
            {
                case MonsterState.OnAir:
                    break;

                case MonsterState.Idle:
                    FindTarget();
                    if (target != null) 
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
                    if (agent.enabled && agent.remainingDistance < 0.5f)
                    {
                        Debug.Log("Wandering complete, returning to idle");
                        ChangeState(MonsterState.Idle);
                    }
                    FindTarget();
                    if (target != null)
                    {
                        Debug.Log("Found target while wandering, changing to chasing");
                        ChangeState(MonsterState.Chasing);
                    }
                    break;

                case MonsterState.Chasing:
                    if (target == null) 
                    {
                        Debug.Log("Lost target, returning to idle");
                        ChangeState(MonsterState.Idle);
                    }
                    else 
                    {
                        float distToTarget = Vector3.Distance(transform.position, target.transform.position);
                        if (distToTarget <= attackRange)
                        {
                            Debug.Log($"Target in attack range ({distToTarget}), charging");
                            ChangeState(MonsterState.Charging);
                        }
                        else
                        {
                            ChaseTarget();
                        }
                    }
                    break;
            }
        }

        private void CheckGrounded()
        {
            isGrounded = Physics.Raycast(groundCheckPoint.position, Vector3.down, groundCheckDistance, groundLayer);
        }
        public bool IsGrounded()
        {
            return isGrounded;
        }
        public void EnableAI()
        {
            if (!agent.enabled) agent.enabled = true;
            agent.isStopped = false;
            ChangeState(isGrounded ? MonsterState.Idle : MonsterState.OnAir);
        }

        private void ChangeState(MonsterState newState)
        {
            if (isDead || currentState == newState)
                return;

            Debug.Log($"ShadowMonster: Changing state from {currentState} to {newState}, isGrounded: {isGrounded}");

            currentState = newState;

            // Update parameters first
            UpdateAnimatorParameters();

            switch (newState)
            {
                case MonsterState.OnAir:
                    if (agent.enabled) agent.enabled = false;
                    PlayAnimation(IdleOnAirHash); // Restore this
                    break;

                case MonsterState.Idle:
                    if (!agent.enabled && isGrounded) 
                    {
                        agent.enabled = true;
                        if (!agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 1f, NavMesh.AllAreas))
                        {
                            agent.Warp(hit.position);
                        }
                    }
                    if (agent.enabled) agent.isStopped = true;
                    PlayAnimation(IdleHash); // Restore this
                    break;

                case MonsterState.Wandering:
                    lastWanderTime = Time.time;
                    if (!agent.enabled) agent.enabled = true;
                    PlayAnimation(IdleHash); // Use Idle animation for wandering
                    Wander();
                    break;

                case MonsterState.Chasing:
                    if (!agent.enabled) agent.enabled = true;
                    agent.isStopped = false;
                    PlayAnimation(IdleHash); // Use Idle animation for chasing (or create a Walk animation)
                    break;

                case MonsterState.Charging:
                    isCharging = true;
                    if (agent.enabled) agent.isStopped = true;
                    PlayAnimation(ChargeHash); // Restore this
                    chargingRoutine = StartCoroutine(StartExplosionAfterDelay());
                    break;

                case MonsterState.Attacking:
                    if (agent.enabled) agent.isStopped = true;
                    PlayAnimation(AttackHash); // Restore this
                    StartCoroutine(ExplodeDuringAttack());
                    break;

                case MonsterState.Dead:
                    agent.enabled = false;
                    PlayAnimation(DieHash); // Restore this
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

            foreach (string tag in priorityTags)
            {
                foreach (var obj in GameObject.FindGameObjectsWithTag(tag))
                {
                    float dist = Vector3.Distance(transform.position, obj.transform.position);
                    if (dist < chaseRange && dist < closestDist) // Add chase range check
                    {
                        closest = obj;
                        closestDist = dist;
                    }
                }
                if (closest != null) break;
            }

            // Debug target finding
            if (closest != target)
            {
                Debug.Log($"Target changed from {(target ? target.name : "null")} to {(closest ? closest.name : "null")}, distance: {closestDist}");
            }

            target = closest;
        }

        private void ChaseTarget()
        {
            if (agent.enabled && target != null && agent.isOnNavMesh)
                agent.SetDestination(target.transform.position);
        }

        private void Wander()
        {
            if (!agent.enabled || !agent.isOnNavMesh) return;
            Vector3 randomPoint = transform.position + Random.insideUnitSphere * wanderRadius;
            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
                agent.SetDestination(hit.position);
        }

        private IEnumerator StartExplosionAfterDelay()
        {
            yield return new WaitForSeconds(chargeDelay);
            if (isDead || !canExplode) yield break;
            ChangeState(MonsterState.Attacking);
        }

        private IEnumerator ExplodeDuringAttack()
        {
            yield return new WaitForSeconds(0.3f);
            if (attackHitbox != null)
            {
                attackHitbox.SetActive(true);
                if (attackHitbox.TryGetComponent(out SpiderAttackHitbox hitbox))
                {
                    hitbox.Initialize(gameObject, damageAmount, 5f);
                    hitbox.TriggerExplosion();
                }
            }

            yield return new WaitForSeconds(0.3f);
            if (attackHitbox != null) attackHitbox.SetActive(false);

            yield return new WaitForSeconds(postExplosionCooldown);
            Kill(gameObject); // Self-destruct
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
                animator.SetTrigger(TriggerHurtParam); // Use trigger instead of CrossFade
                StartCoroutine(ResetToIdleAfterHurt());
            }
        }

        private IEnumerator ResetToIdleAfterHurt()
        {
            yield return new WaitForSeconds(0.4f);
            isPlayingHurtAnimation = false;
            if (!isDead && currentState != MonsterState.Charging && currentState != MonsterState.Attacking)
            {
                currentAnimationHash = -1; // Reset to allow new animation
                ChangeState(isGrounded ? MonsterState.Idle : MonsterState.OnAir);
            }
        }

        private IEnumerator InvulnerabilityRoutine()
        {
            isInvulnerable = true;
            yield return new WaitForSeconds(invulnerabilityDuration);
            isInvulnerable = false;
        }

        public void Kill(GameObject source = null)
        {
            if (isDead) return;
            
            Debug.Log("Spider is being killed");
            isDead = true;
            isCharging = false;
            canExplode = false;
            
            // Stop all running coroutines
            if (chargingRoutine != null) 
            {
                StopCoroutine(chargingRoutine);
                chargingRoutine = null;
            }
            
            // Stop all other coroutines
            StopAllCoroutines();
            
            ChangeState(MonsterState.Dead);
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

            Debug.Log("Death animation complete, destroying spider");
            
            if (SpiderPool.Instance != null)
                SpiderPool.Instance.ReturnSpider(gameObject);
            else
                Destroy(gameObject);
        }

        private void PlayAnimation(int hash)
        {
            if (animator == null || animator.layerCount == 0) return;

            // Check if the animation state exists
            if (!animator.HasState(0, hash)) 
            {
                Debug.LogWarning($"Animation state with hash {hash} not found in animator");
                return;
            }

            // Don't restart the same animation if it's already playing
            AnimatorStateInfo currentStateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (currentStateInfo.shortNameHash == hash) return;

            // Set appropriate parameters before playing animation
            SetAnimationParameters(hash);
            
            // Play the animation
            animator.Play(hash, 0, 0);
            currentAnimationHash = hash;
        }

        private void SetAnimationParameters(int hash)
        {
            if (animator == null) return;

            // Set parameters based on which animation we're playing
            if (hash == IdleHash)
            {
                animator.SetBool(IsGroundedParam, true);
                animator.SetFloat(SpeedParam, 0f);
            }
            else if (hash == IdleOnAirHash)
            {
                animator.SetBool(IsGroundedParam, false);
                animator.SetFloat(SpeedParam, 0f);
            }
            else if (hash == ChargeHash)
            {
                animator.SetBool(IsChargingParam, true);
                animator.SetFloat(SpeedParam, 0f);
            }
            else if (hash == AttackHash)
            {
                animator.SetBool(IsChargingParam, false);
                animator.SetFloat(SpeedParam, 0f);
            }
            else if (hash == DieHash)
            {
                animator.SetBool(IsDeadParam, true);
                animator.SetFloat(SpeedParam, 0f);
            }
        }
        
        public void SetMaxHealth(float newMaxHealth)
        {
            maxHealth = newMaxHealth;
            currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        }
        public void OnPickup()
        {
            isCharging = false;
            canExplode = false;
            if (chargingRoutine != null) StopCoroutine(chargingRoutine);
            ChangeState(MonsterState.Held);
            Rigidbody rb = GetComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        public void OnRelease()
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.useGravity = true;
            StartCoroutine(WaitForLandingThenReset());
        }

        private IEnumerator WaitForLandingThenReset()
        {
            yield return new WaitUntil(() => isGrounded);
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 1f, NavMesh.AllAreas))
            {
                agent.enabled = true;
                agent.Warp(hit.position);
                agent.ResetPath();
                agent.isStopped = true;
            }
            Rigidbody rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            ChangeState(MonsterState.Idle);
        }

        private void ResetSpider()
        {
            isDead = false;
            isCharging = false;
            canExplode = true;
            isInvulnerable = false;
            currentHealth = maxHealth;
            target = null;

            animator.Rebind();
            animator.Update(0f);

            if (NavMesh.SamplePosition(transform.position, out NavMeshHit navHit, 1f, NavMesh.AllAreas))
            {
                agent.enabled = true;
                agent.Warp(navHit.position);
                agent.ResetPath();
                agent.isStopped = true;
            }

            transform.localScale = Vector3.one;
            
            // Force a ground check and proper state initialization
            CheckGrounded();
            wasGroundedLastFrame = false; // Force state transition logic to work
            ChangeState(isGrounded ? MonsterState.Idle : MonsterState.OnAir);
        }

        private void UpdateAnimatorParameters()
        {
            if (animator == null) return;
            
            animator.SetBool(IsGroundedParam, isGrounded);
            animator.SetBool(IsChargingParam, isCharging);
            animator.SetBool(IsDeadParam, isDead);
            
            animator.SetInteger(StateParam, (int)currentState);
            
            float currentSpeed = 0f;
            if (agent != null && agent.enabled && agent.hasPath && !agent.isStopped)
            {
                currentSpeed = agent.velocity.magnitude;
            }
            
            animator.SetFloat(SpeedParam, currentSpeed);
            animator.SetFloat(VelocityParam, currentSpeed);
            
            if (currentState == MonsterState.Wandering || currentState == MonsterState.Chasing)
            {
                Debug.Log($"Movement - Speed: {currentSpeed}, Agent enabled: {agent.enabled}, Has path: {agent.hasPath}, Is stopped: {agent.isStopped}");
            }
        }
    }
}
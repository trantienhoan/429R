using UnityEngine;
using UnityEngine.AI;
using System.Collections;

namespace Enemies
{
    public class ShadowMonster : BaseMonster
    {
        private enum MonsterState { Held, Falling, Idle, Wandering, Chasing, Charging, Attacking, Dead }
        private MonsterState currentState = MonsterState.Falling;

        private NavMeshAgent agent;

        [Header("Animation")]
        [SerializeField] private Animator animator;
        private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
        private static readonly int IsDeadHash = Animator.StringToHash("IsDead");
        private static readonly int IdleHash = Animator.StringToHash("Idle");
        private static readonly int IdleOnAirHash = Animator.StringToHash("Spider_Idle_On_Air");
        private static readonly int ChargeHash = Animator.StringToHash("Spider_Charge");
        private static readonly int AttackHash = Animator.StringToHash("Spider_Attack");
        private static readonly int WalkHash = Animator.StringToHash("Spider_Walk_Cycle");
        private static readonly int DieHash = Animator.StringToHash("Spider_Die");
        private static readonly int HurtHash = Animator.StringToHash("Spider_Hurt");

        [Header("Explosion Attack")]
        [SerializeField] private float chaseRange = 10f;
        [SerializeField] private float attackRange = 2f;
        [SerializeField] private float chargeDelay = 2.5f;
        [SerializeField] private float damageAmount = 100f;
        [SerializeField] private GameObject attackHitbox;
        [SerializeField] private float postExplosionCooldown = 1.0f;

        [Header("Wandering")]
        [SerializeField] private float wanderRadius = 5f;
        [SerializeField] private float wanderDelay = 5f;

        [Header("Ground Check")]
        [SerializeField] private LayerMask groundLayer;
        [SerializeField] private float groundCheckDistance = 0.2f;

        [Header("Health & Damage")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float invulnerabilityDuration = 0.5f;
        [SerializeField] private ParticleSystem hitVFX;
        [SerializeField] private AudioClip hitSFX;

        private float currentHealth;
        private bool isInvulnerable;
        private bool isDead;
        private bool isGrounded;
        private bool isCharging;
        private Coroutine chargingRoutine;
        private bool canExplode = true;

        private GameObject target;
        private float lastWanderTime;
        private AudioSource audioSource;

        protected override void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            if (attackHitbox != null) attackHitbox.SetActive(false);

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null && hitSFX != null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        private void OnEnable() => ResetMonster();

        private void Update()
        {
            if (isDead || currentState == MonsterState.Attacking || currentState == MonsterState.Charging) return;

            isGrounded = IsGrounded();
            animator.SetBool(IsGroundedHash, isGrounded);

            if (currentState == MonsterState.Held)
            {
                PlayAnimation(IdleOnAirHash);
                return;
            }

            if (!isGrounded)
            {
                ChangeState(MonsterState.Falling);
                return;
            }

            switch (currentState)
            {
                case MonsterState.Falling:
                    if (isGrounded) ChangeState(MonsterState.Idle);
                    break;
                case MonsterState.Idle:
                    FindTarget();
                    if (target != null) ChangeState(MonsterState.Chasing);
                    else if (Time.time - lastWanderTime > wanderDelay) ChangeState(MonsterState.Wandering);
                    break;
                case MonsterState.Wandering:
                    if (agent.remainingDistance < 0.5f) ChangeState(MonsterState.Idle);
                    FindTarget();
                    break;
                case MonsterState.Chasing:
                    if (target == null) ChangeState(MonsterState.Idle);
                    else if (IsInAttackRange()) ChangeState(MonsterState.Charging);
                    else ChaseTarget();
                    break;
            }

            if (!isCharging && currentState != MonsterState.Attacking && agent.enabled && agent.velocity.magnitude > 0.1f)
            {
                animator.SetBool(IsMovingHash, true);
                PlayAnimation(WalkHash);
            }
            else
            {
                animator.SetBool(IsMovingHash, false);
            }
        }

        private void ChangeState(MonsterState newState)
        {
            if (currentState == newState || isDead) return;
            currentState = newState;

            switch (newState)
            {
                case MonsterState.Idle:
                    PlayIdleAnimation();
                    break;
                case MonsterState.Wandering:
                    lastWanderTime = Time.time;
                    Wander();
                    break;
                case MonsterState.Chasing:
                    agent.isStopped = false;
                    break;
                case MonsterState.Charging:
                    isCharging = true;
                    agent.isStopped = true;
                    PlayAnimation(ChargeHash);
                    chargingRoutine = StartCoroutine(StartExplosionAfterDelay());
                    break;
                case MonsterState.Attacking:
                    agent.isStopped = true;
                    PlayAnimation(AttackHash);
                    StartCoroutine(ExplodeDuringAttack());
                    break;
                case MonsterState.Held:
                    agent.enabled = false;
                    break;
                case MonsterState.Dead:
                    animator.SetBool(IsDeadHash, true);
                    agent.enabled = false;
                    PlayAnimation(DieHash);
                    StartCoroutine(ScaleDownAndDestroy());
                    break;
            }
        }

        private bool IsInAttackRange()
        {
            return target != null && Vector3.Distance(transform.position, target.transform.position) <= attackRange;
        }

        private void FindTarget()
        {
            GameObject closest = null;
            float closestDistance = Mathf.Infinity;
            string[] priorityTags = { "TreeOfLight", "Player", "Furniture" };

            foreach (string tag in priorityTags)
            {
                foreach (GameObject obj in GameObject.FindGameObjectsWithTag(tag))
                {
                    float dist = Vector3.Distance(transform.position, obj.transform.position);
                    if (dist < closestDistance)
                    {
                        closest = obj;
                        closestDistance = dist;
                    }
                }
                if (closest != null) break;
            }

            target = closest;
        }

        private void ChaseTarget()
        {
            if (target == null || !agent.enabled || !agent.isOnNavMesh) return;
            agent.SetDestination(target.transform.position);
        }

        private void Wander()
        {
            if (!agent.enabled || !agent.isOnNavMesh) return;
            Vector3 randomDirection = Random.insideUnitSphere * wanderRadius + transform.position;
            if (NavMesh.SamplePosition(randomDirection, out NavMeshHit navHit, wanderRadius, NavMesh.AllAreas))
            {
                agent.SetDestination(navHit.position);
                agent.isStopped = false;
                PlayAnimation(WalkHash);
            }
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
                var hitbox = attackHitbox.GetComponent<SpiderAttackHitbox>();
                if (hitbox != null)
                {
                    hitbox.Initialize(gameObject, damageAmount, 5f);
                    hitbox.TriggerExplosion();
                }
            }

            yield return new WaitForSeconds(0.3f);
            if (attackHitbox != null) attackHitbox.SetActive(false);

            yield return new WaitForSeconds(postExplosionCooldown);
            Kill(gameObject);

            yield return new WaitForSeconds(0.5f);
            if (!isDead)
            {
                Debug.LogWarning("Force-death fallback triggered.");
                Die(gameObject, damageAmount);
            }
        }

        public void TakeDamage(float damage, Vector3 hitPoint, GameObject damageSource = null)
        {
            if (isDead || isInvulnerable) return;

            Debug.Log($"{gameObject.name} took {damage} damage from {damageSource?.name ?? "Unknown"} at {hitPoint}");

            currentHealth -= damage;
            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

            SpawnHitVFX(hitPoint);
            PlayHurtAnimation();

            if (currentHealth <= 0f)
            {
                Kill(damageSource);
            }
            else
            {
                StartCoroutine(InvulnerabilityRoutine());
            }
        }

        private void SpawnHitVFX(Vector3 position)
        {
            if (hitVFX != null)
            {
                var vfx = Instantiate(hitVFX, position, Quaternion.identity);
                vfx.Play();
                Destroy(vfx.gameObject, vfx.main.duration + 1f);
            }

            if (audioSource != null && hitSFX != null)
            {
                audioSource.PlayOneShot(hitSFX);
            }
        }

        private void PlayHurtAnimation()
        {
            if (animator == null || !HasAnimationState(HurtHash)) return;
            animator.CrossFade(HurtHash, 0.05f);
            StartCoroutine(ResetToIdleAfterHurt());
        }

        private IEnumerator ResetToIdleAfterHurt()
        {
            yield return new WaitForSeconds(0.4f);
            if (!isDead && currentState != MonsterState.Charging && currentState != MonsterState.Attacking)
            {
                PlayIdleAnimation();
            }
        }

        private IEnumerator InvulnerabilityRoutine()
        {
            isInvulnerable = true;
            yield return new WaitForSeconds(invulnerabilityDuration);
            isInvulnerable = false;
        }

        public void Kill(GameObject damageSource = null)
        {
            if (isDead) return;
            Debug.Log($"{gameObject.name} is being forcefully killed by {damageSource?.name ?? "Unknown"}.");
            Die(damageSource, maxHealth);
        }

        protected void Die(GameObject damageSource, float damage)
        {
            if (isDead) return;
            Debug.Log($"{gameObject.name} has died. Killed by {damageSource?.name ?? "Unknown"} with {damage} damage.");
            isDead = true;
            isCharging = false;
            if (chargingRoutine != null) StopCoroutine(chargingRoutine);
            ChangeState(MonsterState.Dead);
        }

        private IEnumerator ScaleDownAndDestroy()
        {
            float duration = 0.5f;
            Vector3 originalScale = transform.localScale;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, t / duration);
                yield return null;
            }

            if (SpiderPool.Instance != null)
                SpiderPool.Instance.ReturnSpider(gameObject);
            else
                Destroy(gameObject);
        }

        private void ResetMonster()
        {
            isDead = false;
            isCharging = false;
            canExplode = true;
            isInvulnerable = false;
            currentHealth = maxHealth;
            target = null;

            animator.Rebind();
            animator.Update(0f);
            animator.SetBool(IsDeadHash, false);

            if (NavMesh.SamplePosition(transform.position, out NavMeshHit navHit, 1f, NavMesh.AllAreas))
            {
                agent.enabled = true;
                agent.Warp(navHit.position);
                agent.ResetPath();
                agent.isStopped = true;
            }

            transform.localScale = Vector3.one;
            ChangeState(MonsterState.Falling);
        }

        private bool IsGrounded()
        {
            Ray ray = new Ray(transform.position + Vector3.up * 0.1f, Vector3.down);
            return Physics.Raycast(ray, out _, groundCheckDistance + 0.2f, groundLayer);
        }

        private void PlayIdleAnimation() => PlayAnimation(IdleHash);

        private void PlayAnimation(int animationHash)
        {
            if (animator == null || !animator.isActiveAndEnabled || animator.layerCount == 0) return;
            if (!HasAnimationState(animationHash)) return;

            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (!animator.IsInTransition(0) && stateInfo.shortNameHash != animationHash)
            {
                animator.CrossFade(animationHash, 0.2f, 0);
            }
        }

        private bool HasAnimationState(int hash)
        {
            for (int i = 0; i < animator.layerCount; i++)
                if (animator.HasState(i, hash)) return true;
            return false;
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
            yield return new WaitUntil(IsGrounded);
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit navHit, 1f, NavMesh.AllAreas))
            {
                agent.enabled = true;
                agent.Warp(navHit.position);
                agent.isStopped = true;
            }
            Rigidbody rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            ChangeState(MonsterState.Idle);
        }
    }
}
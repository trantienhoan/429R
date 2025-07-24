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

        private Coroutine chargingRoutine;
        private GameObject target;
        private float lastWanderTime;

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

            // Transition between airborne and idle
            if (!isGrounded && wasGroundedLastFrame)
                ChangeState(MonsterState.OnAir);
            else if (isGrounded && !wasGroundedLastFrame)
                ChangeState(MonsterState.Idle);

            wasGroundedLastFrame = isGrounded;

            switch (currentState)
            {
                case MonsterState.OnAir:
                    PlayAnimation(IdleOnAirHash);
                    break;

                case MonsterState.Idle:
                    FindTarget();
                    if (target != null) ChangeState(MonsterState.Chasing);
                    else if (Time.time - lastWanderTime > wanderDelay) ChangeState(MonsterState.Wandering);
                    break;

                case MonsterState.Wandering:
                    if (agent.remainingDistance < 0.5f)
                        ChangeState(MonsterState.Idle);
                    FindTarget();
                    break;

                case MonsterState.Chasing:
                    if (target == null) ChangeState(MonsterState.Idle);
                    else if (Vector3.Distance(transform.position, target.transform.position) <= attackRange)
                        ChangeState(MonsterState.Charging);
                    else
                        ChaseTarget();
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

            currentState = newState;

            switch (newState)
            {
                case MonsterState.OnAir:
                    if (agent.enabled) agent.enabled = false;
                    PlayAnimation(IdleOnAirHash);
                    break;

                case MonsterState.Idle:
                    if (!agent.enabled) agent.enabled = true;
                    agent.isStopped = true;
                    PlayAnimation(IdleHash);
                    break;

                case MonsterState.Wandering:
                    lastWanderTime = Time.time;
                    Wander();
                    break;

                case MonsterState.Chasing:
                    if (!agent.enabled) agent.enabled = true;
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

                case MonsterState.Dead:
                    agent.enabled = false;
                    PlayAnimation(DieHash);
                    StartCoroutine(ScaleDownAndDestroy());
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
                    if (dist < closestDist)
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
            if (animator != null && animator.HasState(0, HurtHash))
            {
                animator.CrossFade(HurtHash, 0.1f);
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

        public void Kill(GameObject source = null)
        {
            if (isDead) return;
            isDead = true;
            if (chargingRoutine != null) StopCoroutine(chargingRoutine);
            ChangeState(MonsterState.Dead);
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
            if (animator != null && animator.layerCount > 0 && animator.HasState(0, hash))
                animator.Play(hash, 0, 0);
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
            ChangeState(MonsterState.OnAir); // Let grounding logic kick in
        }
    }
}
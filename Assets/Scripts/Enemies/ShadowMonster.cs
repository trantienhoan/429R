using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using Core;

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
        private static readonly int IsAttackingHash = Animator.StringToHash("IsAttacking");
        private static readonly int IsDeadHash = Animator.StringToHash("IsDead");
        private static readonly int IsInAttackRangeHash = Animator.StringToHash("IsInAttackRange");
        private static readonly int IdleHash = Animator.StringToHash("Idle");
        private static readonly int IdleOnAirHash = Animator.StringToHash("Spider_Idle_On_Air");
        private static readonly int ChargeHash = Animator.StringToHash("Spider_Charge");
        private static readonly int AttackHash = Animator.StringToHash("Spider_Attack");
        private static readonly int WalkHash = Animator.StringToHash("Spider_Walk_Cycle");
        private static readonly int DieHash = Animator.StringToHash("Spider_Die");

        [Header("Attack")]
        [SerializeField] private float chaseRange = 10f;
        [SerializeField] private float attackRange = 2f;
        [SerializeField] private float attackAngleThreshold = 0.5f;
        [SerializeField] private float chargeDelay = 0.75f;
        [SerializeField] private float rotationSpeed = 5f;
        [SerializeField] private float damageAmount = 10f;
        [SerializeField] private float attackCooldown = 1.5f;
        [SerializeField] private GameObject attackHitbox;

        [Header("Ground Check")]
        [SerializeField] private LayerMask groundLayer;
        [SerializeField] private float groundCheckDistance = 0.2f;

        [SerializeField] private float wanderRadius = 5f;
        [SerializeField] private float wanderDelay = 5f;

        private float lastWanderTime;
        private GameObject target;
        private bool isDead;
        private bool isGrounded;
        private Transform hitboxTransform;

        private new void Awake()
        {
            base.Awake();
            agent = GetComponent<NavMeshAgent>();

            if (attackHitbox != null)
            {
                hitboxTransform = attackHitbox.transform;
                attackHitbox.SetActive(false);
            }
        }

        private void OnEnable() => ResetMonster();

        private void Update()
        {
            if (isDead || currentState == MonsterState.Attacking || currentState == MonsterState.Charging) return;

            isGrounded = IsGrounded();
            animator.SetBool(IsGroundedHash, isGrounded);
            bool inAttackRange = IsInAttackRange();
            animator.SetBool(IsInAttackRangeHash, inAttackRange);

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
                    if (target == null)
                    {
                        ChangeState(MonsterState.Idle);
                        return;
                    }
                    if (IsInAttackRange()) ChangeState(MonsterState.Charging);
                    else ChaseTarget();
                    break;
            }

            if (agent.enabled && agent.velocity.magnitude > 0.1f)
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
            if (currentState == newState) return;
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
                case MonsterState.Charging:
                    agent.isStopped = true;
                    StartCoroutine(AttackSequence());
                    break;
                case MonsterState.Attacking:
                    agent.isStopped = true;
                    animator.SetBool(IsAttackingHash, true);
                    PlayAnimation(AttackHash);
                    break;
                case MonsterState.Held:
                    agent.enabled = false;
                    break;
                case MonsterState.Dead:
                    animator.SetBool(IsDeadHash, true);
                    Die();
                    break;
            }
        }

        private bool IsInAttackRange()
        {
            if (target == null) return false;
            float dist = Vector3.Distance(transform.position, target.transform.position);
            return dist <= attackRange;
        }

        private void FindTarget()
        {
            GameObject closest = null;
            float closestDistance = Mathf.Infinity;
            string[] priorityTags = { "TreeOfLight", "Player", "Furniture" };

            foreach (string searchTag in priorityTags)
            {
                GameObject[] candidates = GameObject.FindGameObjectsWithTag(searchTag);
                foreach (GameObject obj in candidates)
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
            if (target == null || !agent.isOnNavMesh || !agent.enabled) return;
            agent.isStopped = false;
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

        private IEnumerator AttackSequence()
        {
            agent.isStopped = true;
            PlayAnimation(ChargeHash);

            yield return new WaitForSeconds(chargeDelay);

            if (!isGrounded || isDead)
            {
                ChangeState(MonsterState.Idle);
                yield break;
            }

            ChangeState(MonsterState.Attacking);
            yield return PerformAttack();
        }

        private IEnumerator PerformAttack()
        {
            attackHitbox.SetActive(true);
            float t = 0f;
            float duration = 0.35f;
            float maxScale = 0.003f;

            while (t < duration)
            {
                t += Time.deltaTime;
                float scale = Mathf.Lerp(0f, maxScale, t / duration);
                hitboxTransform.localScale = Vector3.one * scale;
                yield return null;
            }

            if (target != null)
            {
                float dist = Vector3.Distance(transform.position, target.transform.position);
                Vector3 toTarget = (target.transform.position - transform.position).normalized;
                float dot = Vector3.Dot(transform.forward, toTarget);

                if (dist <= attackRange && dot > attackAngleThreshold)
                {
                    HealthComponent hp = target.GetComponent<HealthComponent>();
                    if (hp != null)
                        hp.TakeDamage(damageAmount, toTarget, gameObject);
                }
            }

            yield return new WaitForSeconds(0.15f);
            attackHitbox.SetActive(false);
            hitboxTransform.localScale = Vector3.zero;

            animator.SetBool(IsAttackingHash, false);
            ChangeState(MonsterState.Idle);
            yield return new WaitForSeconds(attackCooldown);
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
            {
                if (animator.HasState(i, hash))
                    return true;
            }
            return false;
        }

        public void OnPickup()
        {
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

        private void Die()
        {
            isDead = true;
            agent.enabled = false;
            PlayAnimation(DieHash);
            StartCoroutine(ScaleDownAndDestroy());
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
            target = null;

            animator.Rebind();
            animator.Update(0f);
            animator.SetBool(IsDeadHash, false);
            animator.SetBool(IsAttackingHash, false);

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
            bool grounded = Physics.Raycast(ray, out _, groundCheckDistance + 0.2f, groundLayer);
            Debug.DrawRay(ray.origin, ray.direction * (groundCheckDistance + 0.2f), grounded ? Color.green : Color.red);
            return grounded;
        }
    }
}

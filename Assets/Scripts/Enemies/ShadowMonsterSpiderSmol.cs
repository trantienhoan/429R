
using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using Core;

namespace Enemies
{
    public class ShadowMonsterSpiderSmol : ShadowMonster
    {
        [Header("Animation")]
        [SerializeField] private Animator animator;
        [SerializeField] private string idleAnimationName = "Idle";
        [SerializeField] private string idleOnAirAnimationName = "Spider_Idle_On_Air";
        [SerializeField] private string chargeAnimationName = "Spider_Charge";
        [SerializeField] private string attackAnimationName = "Spider_Attack";
        [SerializeField] private string walkAnimationName = "Spider_Walk_Cycle";
        [SerializeField] private string dieAnimationName = "Spider_Die";

        [Header("Attack")]
        [SerializeField] private float chaseRange = 10f;
        [SerializeField] private float attackRange = 2f;
        [SerializeField] private float attackAngleThreshold = 0.5f;
        [SerializeField] private float chargeDelay = 0.75f;
        [SerializeField] private float rotationSpeed = 5f;
        [SerializeField] private float damageAmount = 10f;
        [SerializeField] private float attackCooldown = 1.5f;

        [Header("Target")]
        [SerializeField] private string targetTag = "Player";

        [Header("Ground Check")]
        [SerializeField] private LayerMask groundLayer;
        [SerializeField] private float groundCheckDistance = 0.2f;

        [SerializeField] private float wanderRadius = 5f;
        [SerializeField] private float wanderDelay = 5f;

        private float lastWanderTime;
        private float lastAttackTime;
        private GameObject target;
        private bool isGrounded;
        private bool isCharging = false;
        private bool isAttacking = false;
        private bool isDead = false;
        private bool isMoving = false;
        private bool isBeingHeld = false;

        private void Awake()
        {
            healthComponent = GetComponent<HealthComponent>();
            if (healthComponent == null || animator == null || (agent = GetComponent<NavMeshAgent>()) == null)
            {
                Debug.LogError("Missing required component(s)");
                enabled = false;
                return;
            }

            healthComponent.OnDeath += Die;
        }

        private void OnEnable() => ResetSpider();
        private void Start() => PlayIdleAnimation();

        private void Update()
        {
            isGrounded = IsGrounded();
            animator.SetBool("IsGrounded", isGrounded);

            if (isDead)
                return;

            if (isBeingHeld)
            {
                PlayAnimation(idleOnAirAnimationName);
                return;
            }

            if (!isGrounded)
            {
                agent.isStopped = true;
                PlayAnimation(idleOnAirAnimationName);
                return;
            }

            // Agent can re-enable if grounded and not being held
            if (!agent.enabled && isGrounded && !isBeingHeld)
            {
                agent.enabled = true;
                agent.Warp(transform.position);
            }

            // Target finding & chasing
            FindTarget();

            if (target == null)
            {
                if (Time.time - lastWanderTime > wanderDelay)
                {
                    Wander();
                    lastWanderTime = Time.time;
                }
            }
            else
            {
                HandleCombat();
            }

            // Movement animation
            if (agent.enabled && agent.velocity.magnitude > 0.1f)
            {
                PlayAnimation(walkAnimationName);
                animator.SetBool("IsMoving", true);
                isMoving = true;
            }
            else if (isMoving)
            {
                PlayIdleAnimation();
                animator.SetBool("IsMoving", false);
                isMoving = false;
            }
        }

        private void FindTarget()
        {
            GameObject closest = null;
            float closestDistance = Mathf.Infinity;

            string[] priorityTags = { targetTag, "TreeOfLight", "Furniture" };

            foreach (string tag in priorityTags)
            {
                GameObject[] candidates = GameObject.FindGameObjectsWithTag(tag);
                foreach (GameObject obj in candidates)
                {
                    float dist = Vector3.Distance(transform.position, obj.transform.position);
                    if (dist < closestDistance)
                    {
                        closest = obj;
                        closestDistance = dist;
                    }
                }

                if (closest != null)
                {
                    target = closest;
                    return;
                }
            }

            target = null;
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

            if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
                agent.isStopped = false;
                PlayAnimation(walkAnimationName);
                isMoving = true;
            }
        }
        private void HandleCombat()
        {
            if (target == null) return;

            Vector3 directionToTarget = (target.transform.position - transform.position).normalized;
            RotateTowardsTarget();

            float distanceToTarget = Vector3.Distance(transform.position, target.transform.position);
            bool isInAttackRange = distanceToTarget <= attackRange;
            bool canAttack = Time.time - lastAttackTime > attackCooldown && !isAttacking && !isCharging;

            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, directionToTarget, out RaycastHit hit, chaseRange))
            {
                if (hit.collider.gameObject != target)
                {
                    StopMovement();
                    return;
                }

                if (isInAttackRange && canAttack)
                    StartCoroutine(Attack());
                else
                    ChaseTarget();
            }
        }

        private IEnumerator Attack()
        {
            isCharging = true;
            PlayAnimation(chargeAnimationName);
            agent.isStopped = true;

            yield return new WaitForSeconds(chargeDelay);

            isCharging = false;
            isAttacking = true;
            PlayAnimation(attackAnimationName);

            yield return new WaitForSeconds(0.5f);

            if (target != null && !isDead)
            {
                float distance = Vector3.Distance(transform.position, target.transform.position);
                Vector3 toTarget = (target.transform.position - transform.position).normalized;
                float dot = Vector3.Dot(transform.forward, toTarget);

                if (distance <= attackRange && dot > attackAngleThreshold)
                {
                    HealthComponent targetHealth = target.GetComponent<HealthComponent>();
                    if (targetHealth != null)
                    {
                        Vector3 hitDirection = toTarget;
                        targetHealth.TakeDamage(damageAmount, hitDirection, gameObject);
                    }
                }
            }

            yield return new WaitForSeconds(1f);

            isAttacking = false;
            lastAttackTime = Time.time;
            if (!isDead && isGrounded)
            {
                agent.isStopped = false;
            }
        }

        private void RotateTowardsTarget()
        {
            if (target == null || isDead || isAttacking || isCharging) return;

            Vector3 direction = (target.transform.position - transform.position).normalized;
            direction.y = 0;

            Quaternion lookRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * rotationSpeed);
        }

        private void StopMovement()
        {
            if (agent != null && agent.enabled)
            {
                agent.isStopped = true;
            }
            PlayIdleAnimation();
            isMoving = false;
        }

        private void PlayIdleAnimation() => PlayAnimation(idleAnimationName);

        private void PlayAnimation(string animationName)
        {
            if (animator == null || string.IsNullOrEmpty(animationName)) return;
            if (!IsCurrentAnimation(animationName))
                animator.CrossFade(animationName, 0.2f);
        }

        private bool IsCurrentAnimation(string animationName) =>
            animator.GetCurrentAnimatorStateInfo(0).IsName(animationName);

        public void OnPickup()
        {
            isBeingHeld = true;

            if (agent != null)
                agent.enabled = false;

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }

            PlayAnimation(idleOnAirAnimationName);
        }

        public void OnRelease()
        {
            isBeingHeld = false;

            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 1f, NavMesh.AllAreas))
            {
                if (agent != null)
                {
                    agent.enabled = true;
                    agent.Warp(hit.position);
                }
            }

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }

        private void Die(HealthComponent _)
        {
            if (isDead) return;
            isDead = true;

            if (agent != null) agent.enabled = false;
            if (animator != null)
            {
                animator.CrossFade(dieAnimationName, 0.1f);
            }

            StartCoroutine(ScaleDownAndDestroy());
        }

        private IEnumerator ScaleDownAndDestroy()
        {
            float duration = 0.5f;
            Vector3 originalScale = transform.localScale;
            float t = 0;

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

        public void ResetSpider()
        {
            isDead = false;
            isCharging = false;
            isAttacking = false;
            isMoving = false;
            target = null;

            if (animator != null)
            {
                animator.enabled = true;
                animator.Rebind();
                animator.Update(0f);
            }

            if (agent != null && NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 1f, NavMesh.AllAreas))
            {
                agent.enabled = true;
                agent.Warp(hit.position);
                agent.ResetPath();
                agent.isStopped = true;
            }

            transform.localScale = Vector3.one;
            PlayIdleAnimation();
        }

        private bool IsGrounded()
        {
            Ray ray = new Ray(transform.position + Vector3.up * 0.1f, Vector3.down);
            bool grounded = Physics.Raycast(ray, out RaycastHit hit, groundCheckDistance + 0.2f, groundLayer);
            Debug.DrawRay(ray.origin, ray.direction * (groundCheckDistance + 0.2f), grounded ? Color.green : Color.red);
            return grounded;
        }
    }
}

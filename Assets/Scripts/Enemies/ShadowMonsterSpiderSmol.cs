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
        [SerializeField] private string pickupAnimationName = "Pickup";
        [SerializeField] private string idleAnimationName = "Idle";
        [SerializeField] private string idleOnAirAnimationName = "Spider_Idle_On_Air";
        [SerializeField] private string chargeAnimationName = "Spider_Charge";
        [SerializeField] private string attackAnimationName = "Spider_Attack";
        [SerializeField] private string walkAnimationName = "Spider_Walk_Cycle";
        [SerializeField] private string hurtLightAnimationName = "Spider_Hurt_Light";
        [SerializeField] private string hurtAnimationName = "Spider_Hurt";
        [SerializeField] private string dieAnimationName = "Spider_Die";

        [Header("Attack")]
        [SerializeField] private float chaseRange = 10f;
        [SerializeField] private float attackRange = 2f;
		[SerializeField] private float attackAngleThreshold = 0.5f; //dot product value for attack cone
        [SerializeField] private float chargeDelay = 0.75f;
        [SerializeField] private float rotationSpeed = 5f;
        [SerializeField] private float damageAmount = 10f;
        [SerializeField] private float attackCooldown = 1.5f;

        [Header("Target")]
        [SerializeField] private string targetTag = "Player"; // Make target tag configurable

        [Header("Ragdoll Death")]
        [SerializeField] private Rigidbody[] ragdollRigidbodies;
        [SerializeField] private float spinForce = 300f;
        [SerializeField] private float delayBeforePooling = 2.5f;

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
            if (!isGrounded)
            {
                PlayAnimation(idleOnAirAnimationName);
                agent.isStopped = true;
                return;
            }

            if (isDead || agent == null || !agent.enabled) return;

            FindTarget();
            if (target == null)
            {
                if (Time.time - lastWanderTime > wanderDelay)
                {
                    Wander();
                    lastWanderTime = Time.time;
                }
                return;
            }

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
				{
					StartCoroutine(Attack());
				}
                else if (distanceToTarget <= chaseRange)
                {
                    ChaseTarget();
                }
                else
                {
                    StopMovement();
                }
            }

            if (agent.velocity.magnitude > 0.1f && isGrounded && !isCharging && !isAttacking)
            {
                if (!isMoving)
                {
                    PlayAnimation(walkAnimationName);
                    isMoving = true;
                }
            }
            else if (isMoving)
            {
                PlayIdleAnimation();
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
                    Debug.Log("Target found: " + target.name);
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

            Vector3 randomDirection = Random.insideUnitSphere * wanderRadius;
            randomDirection += transform.position;

            if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
                agent.isStopped = false;
                PlayAnimation(walkAnimationName);
                isMoving = true;
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
						Vector3 hitDirection = toTarget; // Direction from attacker to target
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

        protected override void HandlePostFade()
        {
            if (SpiderPool.Instance != null)
                SpiderPool.Instance.ReturnSpider(gameObject);
            else
                Destroy(gameObject);
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
        private void PlayPickupAnimation() => PlayAnimation(pickupAnimationName);

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
            PlayPickupAnimation();
            if (agent != null)
                agent.enabled = false;
        }

        private void Die(HealthComponent _)
        {
            if (isDead) return;
            isDead = true;

            if (animator != null) animator.enabled = false;
            if (agent != null && agent.enabled) agent.isStopped = true;

            EnableRagdoll(true);
            ApplySpinOnDeath();
            StartCoroutine(DelayedReturnToPool());
        }

        private void EnableRagdoll(bool enabled)
        {
            foreach (var rb in ragdollRigidbodies)
            {
                rb.isKinematic = !enabled;
                rb.detectCollisions = enabled;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        private void ApplySpinOnDeath()
        {
            foreach (var rb in ragdollRigidbodies)
            {
                Vector3 randomTorque = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized * spinForce;
                rb.AddTorque(randomTorque, ForceMode.Impulse);
            }
        }

        private IEnumerator DelayedReturnToPool()
        {
            yield return new WaitForSeconds(delayBeforePooling);
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

            EnableRagdoll(false);

            if (animator != null)
            {
                animator.enabled = true;
                animator.Rebind();
                animator.Update(0f);
            }

            if (agent != null)
            {
                if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 1f, NavMesh.AllAreas))
                {
                    agent.enabled = true;
                    agent.Warp(hit.position);
                    agent.ResetPath();
                    agent.isStopped = true;
                }
                else
                {
                    Debug.LogWarning("Could not find a valid NavMesh position for the spider.");
                    agent.enabled = false;
                }
            }

            transform.localScale = Vector3.one;
            PlayIdleAnimation();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject.CompareTag("Player"))
                OnPickup();
        }

        private bool IsGrounded()
        {
            Ray ray = new Ray(transform.position + Vector3.up * 0.1f, Vector3.down);
            bool grounded = Physics.Raycast(ray, out RaycastHit hit, groundCheckDistance + 0.2f, groundLayer);
            Debug.DrawRay(ray.origin, ray.direction * (groundCheckDistance + 0.2f), grounded ? Color.green : Color.red);
            return grounded;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, transform.position + Vector3.down * groundCheckDistance);
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, transform.forward * chaseRange);
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 2f);
        }
    }
}
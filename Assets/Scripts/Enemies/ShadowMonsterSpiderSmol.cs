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
        [SerializeField] private float chargeDelay = 0.75f;
        [SerializeField] private float rotationSpeed = 5f;
        [SerializeField] private float damageAmount = 10f;

        [Header("Target")]
        [SerializeField] private string treeOfLightTag = "TreeOfLight";

        [Header("Ragdoll Death")]
        [SerializeField] private Rigidbody[] ragdollRigidbodies;
        [SerializeField] private float spinForce = 300f;
        [SerializeField] private float delayBeforePooling = 2.5f;

        private GameObject target;
        private bool isGrounded;
        private bool isCharging = false;
        private bool isAttacking = false;
        private bool isDead = false;
        private bool isMoving = false;

        private void Awake()
        {
            healthComponent = GetComponent<HealthComponent>();
            if (healthComponent == null)
            {
                Debug.LogError("HealthComponent not found on " + gameObject.name);
                enabled = false;
            }

            if (animator == null)
            {
                Debug.LogError("Animator not assigned on " + gameObject.name);
                enabled = false;
            }

            agent = GetComponent<NavMeshAgent>();
            if (agent == null)
            {
                Debug.LogError("NavMeshAgent not found on " + gameObject.name);
                enabled = false;
            }

            healthComponent.OnDeath += Die;
        }

        private void OnEnable()
        {
            ResetSpider();
        }

        private void Start()
        {
            PlayIdleAnimation();
        }

        private void Update()
        {
            if (isDead || agent == null || !agent.enabled) return;

            isGrounded = Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 0.3f) 
                         || (agent != null && agent.isOnNavMesh && !agent.isOnOffMeshLink);

            if (!isGrounded)
            {
                PlayAnimation(idleOnAirAnimationName);
                isMoving = false;
                return;
            }

            FindTarget();

            if (target != null)
            {
                Vector3 directionToTarget = (target.transform.position - transform.position).normalized;
                float distanceToTarget = Vector3.Distance(transform.position, target.transform.position);

                // Raycast toward target to check visibility
                if (Physics.Raycast(transform.position + Vector3.up * 0.5f, directionToTarget, out RaycastHit hit, chaseRange))
                {
                    if (hit.collider.gameObject == target)
                    {
                        if (distanceToTarget <= attackRange)
                        {
                            agent.isStopped = true; // Stop before attacking
                            if (!isCharging && !isAttacking)
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
                    else
                    {
                        StopMovement(); // Obstructed
                    }
                }
                else
                {
                    StopMovement(); // Nothing hit
                }

                RotateTowardsTarget();
            }
            else
            {
                StopMovement();
            }

            if (agent.enabled == false && !isDead && !isAttacking)
            {
                PlayAnimation(idleOnAirAnimationName); // if being held
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
            if (target != null) return; // prevent from searching the same target again and again.

            GameObject treeOfLight = GameObject.FindGameObjectWithTag(treeOfLightTag);
            if (treeOfLight != null)
            {
                target = treeOfLight;
                Debug.Log("Target acquired: " + target.name); // Debug log
                return;
            }

            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                target = playerObj;
                 Debug.Log("Target acquired: " + target.name); // Debug log
                return;
            }
        }

        private void ChaseTarget()
        {
            if (target == null || !agent.isOnNavMesh || !agent.enabled) return;
            agent.isStopped = false;
            agent.SetDestination(target.transform.position);
        }

        private IEnumerator Attack()
        {
            isCharging = true;
            PlayAnimation(chargeAnimationName);
            agent.isStopped = true;

            yield return new WaitForSeconds(chargeDelay);

            isCharging = false;
            isAttacking = true;

            if (target != null)
            {
                float distanceToTarget = Vector3.Distance(transform.position, target.transform.position);
                if (distanceToTarget <= attackRange)
                {
                    PlayAnimation(attackAnimationName);
                    HealthComponent targetHealth = target.GetComponent<HealthComponent>();
                    if (targetHealth != null)
                    {
                        targetHealth.TakeDamage(damageAmount, Vector3.zero, gameObject);
                    }

                    yield return new WaitForSeconds(1f);
                }
            }

            isAttacking = false;
        }

        private void RotateTowardsTarget()
        {
            if (target == null || isDead || isAttacking || isCharging) return;

            Vector3 direction = (target.transform.position - transform.position).normalized;
            direction.y = 0;

            Quaternion lookRotation = Quaternion.LookRotation(-direction); // ← use -direction if spider faces backwards
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

        private void PlayIdleAnimation()
        {
            PlayAnimation(idleAnimationName);
        }

        private void PlayPickupAnimation()
        {
            PlayAnimation(pickupAnimationName);
        }

        private void PlayAnimation(string animationName)
        {
            if (animator != null && !string.IsNullOrEmpty(animationName) && !IsCurrentAnimation(animationName))
            {
                animator.CrossFade(animationName, 0.2f);
            }
        }
        private bool IsCurrentAnimation(string animationName)
        {
            return animator.GetCurrentAnimatorStateInfo(0).IsName(animationName);
        }

        public void OnPickup()
        {
            PlayPickupAnimation();
            if (agent != null)
            {
                agent.enabled = false;
            }
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
                Vector3 randomTorque = new Vector3(
                    Random.Range(-1f, 1f),
                    Random.Range(-1f, 1f),
                    Random.Range(-1f, 1f)
                ).normalized * spinForce;

                rb.AddTorque(randomTorque, ForceMode.Impulse);
            }
        }

        private IEnumerator DelayedReturnToPool()
        {
            yield return new WaitForSeconds(delayBeforePooling);

            if (SpiderPool.Instance != null)
            {
                SpiderPool.Instance.ReturnSpider(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
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
                animator.Update(0f); // apply immediately
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
            {
                OnPickup();
            }
        }
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Vector3 direction = transform.forward * chaseRange; // Use chaseRange here
            Gizmos.DrawRay(transform.position, direction);
        }
    }
}
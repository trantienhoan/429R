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
        [SerializeField] private string attackAnimationName = "Spider_Attack"; // Single attack animation
        [SerializeField] private string walkAnimationName = "Spider_Walk_Cycle";
        [SerializeField] private string hurtLightAnimationName = "Spider_Hurt_Light";
        [SerializeField] private string hurtAnimationName = "Spider_Hurt";
        [SerializeField] private string dieAnimationName = "Spider_Die";

        [Header("Attack")]
        [SerializeField] private float chaseRange = 10f;
        [SerializeField] private float attackRange = 2f;
        [SerializeField] private float chargeDelay = 0.75f;
        [SerializeField] private float rotationSpeed = 5f; // Add rotation speed
        [SerializeField] private float damageAmount = 10f;

        [Header("Target")]
        [SerializeField] private string treeOfLightTag = "TreeOfLight";
        private GameObject target;
        private int attackIndex = 0;
        private bool isGrounded;
        private bool isCharging = false;
        private bool isAttacking = false; // Add attacking state
        private NavMeshAgent agent;
        private bool isMoving = false; // Add moving state

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
        }

        private void Start()
        {
            // Initial state
            PlayIdleAnimation();
        }

        private void Update()
        {
            isGrounded = Physics.Raycast(transform.position, Vector3.down, 0.1f);

            if (!isGrounded)
            {
                PlayAnimation(idleOnAirAnimationName);
                isMoving = false;
            }
            else
            {
                // Find the target
                FindTarget();

                if (target != null)
                {
                    float distanceToTarget = Vector3.Distance(transform.position, target.transform.position);

                    if (distanceToTarget <= attackRange && !isCharging && !isAttacking) //Added !isAttacking condition
                    {
                        StartCoroutine(Attack());
                    }
                    else if (distanceToTarget <= chaseRange)
                    {
                        ChaseTarget();
                    }
                    else
                    {
                        PlayIdleAnimation();
                        agent.isStopped = true;
                        isMoving = false;
                    }
                }
                else
                {
                    PlayIdleAnimation();
                    agent.isStopped = true;
                    isMoving = false;
                }

                // Handle movement animation
                if (agent.velocity.magnitude != 0f && isGrounded && !isCharging && !isAttacking) // Disable movement animation during charge/attack
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

                // Rotate to face the target, but only during charge or attack
                if ((isCharging || isAttacking) && target != null)
                {
                    RotateTowardsTarget();
                }
            }
        }

        private void FindTarget()
        {
            // Prioritize TreeOfLight
            GameObject treeOfLight = GameObject.FindGameObjectWithTag(treeOfLightTag);
            if (treeOfLight != null)
            {
                target = treeOfLight;
                return;
            }

            // If no TreeOfLight, target the Player
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                target = player;
                return;
            }

            // No target found
            target = null;
        }

        private void ChaseTarget()
        {
            agent.isStopped = false;
            agent.destination = target.transform.position;
            if (!isMoving)
            {
                PlayAnimation(walkAnimationName);
                isMoving = true;
            }
        }

        private IEnumerator Attack()
        {
            isCharging = true;
            PlayAnimation(chargeAnimationName);
            agent.isStopped = true; // Stop moving during charge

            yield return new WaitForSeconds(chargeDelay);

            isCharging = false;
            isAttacking = true; // Set attacking state

            if (target != null)
            {
                float distanceToTarget = Vector3.Distance(transform.position, target.transform.position);
                if (distanceToTarget <= attackRange)
                {
                    PlayAnimation(attackAnimationName); // Play attack animation
                    // Apply damage
                    HealthComponent targetHealth = target.GetComponent<HealthComponent>();
                    if (targetHealth != null)
                    {
                        targetHealth.TakeDamage(damageAmount, Vector3.zero, gameObject);
                    }

                    yield return new WaitForSeconds(1f); // Adjust to match attack animation duration.
                }
            }

            isAttacking = false; // Clear attacking state
        }
        
        private void RotateTowardsTarget()
        {
            Vector3 direction = (target.transform.position - transform.position).normalized;
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * rotationSpeed);
        }

        public void OnPickup()
        {
            PlayPickupAnimation();
        }

        private void PlayPickupAnimation()
        {
            PlayAnimation(pickupAnimationName);
        }

        private void PlayIdleAnimation()
        {
            PlayAnimation(idleAnimationName);
        }

        private void PlayAnimation(string animationName)
        {
            animator.CrossFade(animationName, 0.2f);
        }

        private void OnCollisionEnter(Collision collision)
        {
            //Example of the logic to detect the Player pickup the monster
            if (collision.gameObject.CompareTag("Player"))
            {
                OnPickup();
            }
        }

        private void Die()
        {
            PlayAnimation(dieAnimationName);
            // Disable the spider or destroy it after the animation
            Destroy(gameObject, 2f); // Destroy after 2 seconds
        }
    }
}
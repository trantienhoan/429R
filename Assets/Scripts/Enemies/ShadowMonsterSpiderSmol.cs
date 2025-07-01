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
        [SerializeField] private string hurtLightAnimationName = "Spider_Hurt_Light";
        [SerializeField] private string hurtAnimationName = "Spider_Hurt";
        [SerializeField] private string dieAnimationName = "Spider_Die";
        [Header("Attack")]
        [SerializeField] private float chaseRange = 10f;
        [SerializeField] private float attackRange = 2f;
        [SerializeField] private float chargeDelay = 0.75f;
        [SerializeField] private string[] attackAnimationNames = { "Spider_Attack1", "Spider_Attack2", "Spider_Attack3" };
        [SerializeField] private float damageAmount = 10f;
        [Header("Target")]
        [SerializeField] private string treeOfLightTag = "TreeOfLight";
        private GameObject target;
        private int attackIndex = 0;
        private bool isGrounded;
        private bool isCharging = false;
        private NavMeshAgent agent;
        private HealthComponent healthComponent;

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
            }
            else
            {
                // Find the target
                FindTarget();

                if (target != null)
                {
                    float distanceToTarget = Vector3.Distance(transform.position, target.transform.position);

                    if (distanceToTarget <= attackRange && !isCharging)
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
                    }
                }
                else
                {
                    PlayIdleAnimation();
                    agent.isStopped = true;
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
            // Consider playing a "Run" animation here
        }
        private IEnumerator Attack()
        {
            isCharging = true;
            PlayAnimation(chargeAnimationName);
            agent.isStopped = true; // Stop moving during charge

            yield return new WaitForSeconds(chargeDelay);

            if (target != null)
            {
                PlayAnimation(attackAnimationNames[attackIndex]);
                // Apply damage (you'll need a reference to the target's HealthComponent)
                HealthComponent targetHealth = target.GetComponent<HealthComponent>();
                if (targetHealth != null)
                {
                    targetHealth.TakeDamage(damageAmount);
                }

                attackIndex = (attackIndex + 1) % attackAnimationNames.Length; // Cycle through attacks
            }

            isCharging = false;
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
        public void TakeDamage(float damage)
        {
            float damagePercentage = damage / healthComponent.currentHealth;

            if (damagePercentage < 0.01f)
            {
                PlayAnimation(hurtLightAnimationName);
            }
            else
            {
                PlayAnimation(hurtAnimationName);
            }

            healthComponent.TakeDamage(damage);

            if (healthComponent.currentHealth <= 0)
            {
                Die();
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
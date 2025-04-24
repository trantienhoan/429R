using UnityEngine;
using System.Collections;
using Core;
using System;

namespace Enemies
{
    public class ShadowMonsterSpider : ShadowMonster
    {
        [Header("Movement Settings")]
        [SerializeField] private float patrolSpeed = 5f;
        [SerializeField] private float chaseSpeed = 10f;
        [SerializeField] private float slowdownDistance = 2f;
        [SerializeField] private float attackSpeedMultiplier = 0.5f;
        [SerializeField] private float reTargetingRange = 20f;

        [Header("Attack Settings")]
        [SerializeField] private float attackDamage = 20f;
        [SerializeField] private float jumpHeight = 2f;

        private GameObject target;
        private Vector2 currentDirection;
        private bool isAttacking = false;
        private SpiderController spiderController;
        private GameObject player;

        private void Awake()
        {
            healthComponent = GetComponent<HealthComponent>();
            if (healthComponent == null)
            {
                Debug.LogError("HealthComponent not found on " + gameObject.name);
                enabled = false;
                return;
            }
        }

        private void Start()
        {
            spiderController = GetComponent<SpiderController>();

            if (spiderController == null)
            {
                Debug.LogError("SpiderController not found on ShadowMonsterSpider!");
                enabled = false;
                return;
            }

            FindTarget();

            if (target == null)
            {
                Debug.LogWarning("TreeOfLightPot not found. Spider will patrol randomly.");
                SetRandomDirection();
            }
            else
            {
                SetMovementDirection(target.transform.position, patrolSpeed);
            }

            healthComponent.OnTakeDamage += OnTakeDamage; // Subscribe to OnTakeDamage
        }

        private void OnDestroy()
        {
            healthComponent.OnTakeDamage -= OnTakeDamage; // Unsubscribe
        }

        private void Update()
        {
            if (healthComponent.IsDead()) return;

            if (target != null)
            {
                float distanceToTarget = Vector3.Distance(transform.position, target.transform.position);

                if (distanceToTarget <= slowdownDistance && !isAttacking)
                {
                    SetMovementDirection(target.transform.position, patrolSpeed * attackSpeedMultiplier);
                    isAttacking = true;
                    Attack();
                }
                else
                {
                    SetMovementDirection(target.transform.position, chaseSpeed);
                }
            }
            else
            {
                SetMovementDirection(currentDirection, patrolSpeed);

                if (UnityEngine.Random.value < 0.01f)
                {
                    FindTarget();

                    if (target != null && Vector3.Distance(transform.position, target.transform.position) > reTargetingRange)
                    {
                        SetRandomDirection();
                        target = null;
                    }
                    else if (target != null)
                    {
                        SetMovementDirection(target.transform.position, patrolSpeed);
                    }
                }
            }
        }

        private void FindTarget()
        {
            GameObject treeOfLightPot = GameObject.FindGameObjectWithTag("TreeOfLightPot");
            if (treeOfLightPot != null)
            {
                target = treeOfLightPot;
                return;
            }

            if (player == null)
            {
                player = GameObject.FindGameObjectWithTag("Player");
            }

            target = player;
        }

        private void SetMovementDirection(Vector3 targetPosition, float speed)
        {
            Vector2 direction = (targetPosition - transform.position).normalized;
            SetMovementDirection(direction, speed);
        }

        private void SetMovementDirection(Vector2 direction, float speed)
        {
            spiderController.SetMovementDirection(direction * speed);
        }

        private void SetRandomDirection()
        {
            currentDirection = UnityEngine.Random.insideUnitCircle.normalized;
        }

        private void Attack()
        {
            //Debug.Log("Spider Attack!");

            if (jumpHeight > 0 && UnityEngine.Random.value > 0.5f)
            {
                JumpAttack();
            }
            else
            {
                RunAttack();
            }
        }

        private void JumpAttack()
        {
            //Debug.Log("Spider Jump Attack!");
            isAttacking = false;
        }

        private void RunAttack()
        {
            //Debug.Log("Spider Run Attack!");
            isAttacking = false;
        }

        private void OnTakeDamage(object sender, HealthComponent.HealthChangedEventArgs e)
        {
            // Retarget to the damage source
            if (e.DamageSource != null)
            {
                target = e.DamageSource;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject == target)
            {
                HealthComponent targetHealth = target.GetComponent<HealthComponent>();
                if (targetHealth != null)
                {
                    targetHealth.TakeDamage(attackDamage, target.transform.position, gameObject);
                }
                isAttacking = false;
            }
        }
    }
}
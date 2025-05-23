using UnityEngine;
using Core;
//using System.Collections;

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
        [SerializeField] private float findTargetInterval = 2f;

        [Header("Attack Settings")]
        [SerializeField] private float attackDamage = 20f;
        [SerializeField] private float jumpHeight = 2f;
        [SerializeField] private float attackBodyMoveSpeed = 5f;
        [SerializeField] private float attackCooldown = 5f; // Cooldown duration in seconds

        [Header("SpiderPivot Offset")]
        [SerializeField] private float pivotOffsetX = 2f; // Offset in the X direction
        [SerializeField] private float maxDistanceFromPlayer = 50f;

        [Header("Jump Attack Settings")]
        [SerializeField] private float blowbackDistance = 3f; // Distance to move back before jumping
        [SerializeField] private float blowbackDuration = 0.3f; // Duration of the blowback

        private GameObject target;
        private Vector2 currentDirection;
        private bool isAttacking;
        private SpiderController spiderController;
        private SpiderPivot spiderPivot; // Reference to SpiderPivot
        private GameObject player;
        private float lastFindTargetTime;
        private float lastAttackTime; // Time of last attack

        private HealthComponent targetHealth;

        [Header("Pull To Light")]
        [SerializeField] private float pullSpeed = 10f;
        private bool isBeingPulled;
        private Vector3 pullTarget;

        private void Awake()
        {
            healthComponent = GetComponent<HealthComponent>();
            if (healthComponent == null)
            {
                Debug.LogError("HealthComponent not found on " + gameObject.name);
                enabled = false;
            }
        }

        private void Start()
        {
            spiderController = GetComponent<SpiderController>();
            spiderPivot = GetComponent<SpiderPivot>(); // Get SpiderPivot component

            if (spiderController == null)
            {
                Debug.LogError("SpiderController not found on ShadowMonsterSpider!");
                enabled = false;
                return;
            }

            if (spiderPivot == null) // Check for SpiderPivot
            {
                Debug.LogError("SpiderPivot not found on ShadowMonsterSpider!");
                enabled = false;
                return;
            }

            // Apply the initial offset
            Vector3 pivotPosition = spiderPivot.transform.localPosition;
            pivotPosition.x = pivotOffsetX;
            spiderPivot.transform.localPosition = pivotPosition;

            player = GameObject.FindGameObjectWithTag("Player"); //Find the player here to avoid doing it every frame

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

            healthComponent.OnTakeDamage += OnTakeDamage;
            lastFindTargetTime = Time.time;
        }

        private void OnDestroy()
        {
            healthComponent.OnTakeDamage -= OnTakeDamage;
        }

        private void Update()
        {
            if (healthComponent.IsDead())
            {
                Die(); // Call Die function
                return;
            }

            if (isBeingPulled)
            {
                PullToTarget();
                return;
            }
            
            FindTarget();

            // Check distance from player
            Debug.Log("Player object: " + player);

            if (player != null)
            {
                float distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);
                Debug.Log("Distance to player: " + distanceToPlayer + ", Max distance: " + maxDistanceFromPlayer);
                if (distanceToPlayer > maxDistanceFromPlayer)
                {
                    Debug.Log("Spider is too far from the player, dying.");
                    healthComponent.TakeDamage(healthComponent.Health, transform.position, gameObject); // Kill the spider using TakeDamage
                    return;
                }
            }

            // Periodically attempt to find the target
            if (Time.time - lastFindTargetTime > findTargetInterval)
            {
                lastFindTargetTime = Time.time;
            }

            if (target != null)
            {
                float distanceToTarget = Vector3.Distance(transform.position, target.transform.position);

                // Check if within reTargetingRange, NOT already being pulled, and attack is off cooldown
                if (distanceToTarget <= reTargetingRange && !isBeingPulled && Time.time - lastAttackTime > attackCooldown)
                {
                    if (jumpHeight > 0 && UnityEngine.Random.value > 0.5f)
                    {
                        JumpAttack();
                    }
                    else
                    {
                        if (target != null)
                            PullToPosition(target.transform.position); // Directly pull for regular attack
                    }
                }
                else if (!isBeingPulled)
                {
                    if (target != null)
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
            // Check if our target is dead, if so null it
            if (target != null)
            {
                HealthComponent targetHealth = target.GetComponent<HealthComponent>();
                if (targetHealth != null && targetHealth.IsDead())
                {
                    target = null;
                }
            }

            GameObject treeOfLightPot = null;
            GameObject furniture = null;
            GameObject player = null;

            // Find all potential targets
            GameObject[] pots = GameObject.FindGameObjectsWithTag("TreeOfLightPot");
            if (pots.Length > 0)
            {
                treeOfLightPot = pots[0]; // Take the first one
            }

            GameObject[] furnitures = GameObject.FindGameObjectsWithTag("Furniture");
            if (furnitures.Length > 0)
            {
                furniture = furnitures[0]; // Take the first one
            }

            if (player == null)
            {
                player = GameObject.FindGameObjectWithTag("Player");
            }

            // Prioritize targets
            if (treeOfLightPot != null)
            {
                target = treeOfLightPot;
            }
            else if (furniture != null)
            {
                target = furniture;
            }
            else if (player != null)
            {
                target = player;
            }
            else
            {
                target = null;
                Debug.LogWarning("No target found, patroling randomly");
            }
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

        private void JumpAttack()
        {
            StartCoroutine(PerformJumpAttack());
        }

        private System.Collections.IEnumerator PerformJumpAttack()
        {
            isAttacking = true;
    
            // 1. Blowback
            Vector3 blowbackDirection = (transform.position - target.transform.position).normalized;
            Vector3 blowbackPosition = transform.position + blowbackDirection * blowbackDistance;
    
            float blowbackTime = 0f;
            Vector3 startPosition = transform.position;
            while (blowbackTime < blowbackDuration)
            {
                blowbackTime += Time.deltaTime;
                transform.position = Vector3.Lerp(startPosition, blowbackPosition, blowbackTime / blowbackDuration);
                yield return null;
            }
    
            // 2. Pull to Target
            if (target != null) // Add this null check
            {
                PullToPosition(target.transform.position);
            }
    
            isAttacking = false;
        }

        //This function is called from Animation event
        public void ApplyDamage()
        {
            if (target != null)
            {
                targetHealth = target.GetComponent<HealthComponent>();
                if (targetHealth != null)
                {
                    targetHealth.TakeDamage(attackDamage, transform.position, gameObject);
                }
            }
        }

        private void OnTakeDamage(object sender, HealthComponent.HealthChangedEventArgs e)
        {
            // Retarget to the damage source
            if (e.DamageSource != null)
            {
                target = e.DamageSource;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            // Check if colliding with the TreeOfLight or Player and is not currently attacking
            if ((collision.gameObject == target || collision.gameObject.CompareTag("Player")) && !isAttacking)
            {
                ApplyDamage(); // Deal damage
                lastAttackTime = Time.time; // Record the time of the attack

                // If TreeOfLight is target, stop being pulled and clear the target
                if (collision.gameObject == target)
                {
                    isBeingPulled = false; // Stop being pulled
                    //target = null; // Clear the target - REMOVE THIS LINE

                    // Move away from the TreeOfLight
                    SetRandomDirection();
                    SetMovementDirection(currentDirection, patrolSpeed);
                }

                //If colliding with player, apply the regular logic and cooldown
            }
        }

        private void Die()
        {
            Debug.Log("Die() function called!");
            // Disable SpiderController to stop movement
            spiderController.enabled = false;

            // Apply forces to legs to simulate collapse
            Transform[] legs = GetComponent<Spider>().legsArray; // Get leg array
            foreach (Transform leg in legs)
            {
                if (leg != null)
                {
                    Rigidbody rb = leg.GetComponent<Rigidbody>();
                    if (rb == null)
                    {
                        rb = leg.gameObject.AddComponent<Rigidbody>(); // Add Rigidbody if it doesn't exist
                        rb.mass = 10f;
                    }

                    // Apply a random force to each leg
                    rb.AddForce(Random.insideUnitSphere * 5f, ForceMode.Impulse);
                }
            }
            Destroy(gameObject);
        }

        // Public method to pull the spider to a specific position
        public void PullToPosition(Vector3 position)
        {
            isBeingPulled = true;
            pullTarget = position;
        }

        private void PullToTarget()
        {
            if (spiderPivot != null)
            {
                // Rotate the spiderPivot to face the pullTarget
                Quaternion targetRotation = Quaternion.LookRotation(pullTarget - spiderPivot.transform.position);
                spiderPivot.transform.rotation = Quaternion.Slerp(spiderPivot.transform.rotation, targetRotation, Time.deltaTime * pullSpeed);

                // Move the spiderPivot towards the pullTarget
                spiderPivot.transform.position = Vector3.MoveTowards(spiderPivot.transform.position, pullTarget, pullSpeed * Time.deltaTime);

                if (Vector3.Distance(spiderPivot.transform.position, pullTarget) < 0.1f)
                {
                    isBeingPulled = false;
                }
            }
            else
            {
                isBeingPulled = false;
            }
        }
    }
}
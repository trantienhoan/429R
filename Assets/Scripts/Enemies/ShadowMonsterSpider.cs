//using System.Collections;
//using System.Collections.Generic;
//using Enemies;
using UnityEngine;
//using UnityEngine.AI;

namespace Enemies
{
    public enum SpiderState
    {
        Spawning,
        Wandering,
        Targeting,
        Attacking,
        Damaged,
        Dead
    }

    [DefaultExecutionOrder(1)]
    public class ShadowMonsterSpider : MonoBehaviour
    {
        // References
        [SerializeField] private SpiderController controller;
        [SerializeField] private Animator animator;
        [SerializeField] private Spider spiderIK;
        
        // Animation hashes - replacing string lookups
        private readonly int hashIsWalkingParam = Animator.StringToHash("isWalking");
        private readonly int hashIsAttackingParam = Animator.StringToHash("isAttacking");
        private readonly int hashIsDamagedParam = Animator.StringToHash("isDamaged");
        private readonly int hashIsDeadParam = Animator.StringToHash("isDead");
        
        // AI Settings
        [SerializeField] private float targetingRange = 10f;
        [SerializeField] private float attackRange = 2f;
        [SerializeField] private float wanderRadius = 5f;
        [SerializeField] private float minDistanceToWanderPoint = 1f;
        [SerializeField] private float maxWanderDistance = 20f;
        #pragma warning disable 0649
        [SerializeField] private Vector3 startPosition;
        #pragma warning restore 0649
        
        // Movement settings
        [SerializeField] private float normalSpeed = 1f;
        [SerializeField] private float chaseSpeed = 1.5f;
        
        // Attack settings
        [SerializeField] private float attackDuration = 1f;
        [SerializeField] private float attackCooldown = 0.5f;
        [SerializeField] private float damageRecoveryTime = 0.5f;
        
        // State management
        [SerializeField] private SpiderState currentState;
        private float stateTimer;
        
        [SerializeField] private float detectionRadius = 10f;
        [SerializeField] private LayerMask targetLayer;
        
        // Target management
        private Transform currentTarget;
        private Vector3 wanderPoint;
        private Vector3 originalPosition;
        
        // Health system
        [SerializeField] private float maxHealth = 100f;
        private float currentHealth;
        [SerializeField] private float knockbackForce = 5f;
        
        private void Awake()
        {
            currentHealth = maxHealth;
            originalPosition = transform.position;
            startPosition = originalPosition;
            SetNewWanderPoint();
        }
        
        private void Start()
        {
            // Initialize components if not set
            if (controller == null)
                controller = GetComponent<SpiderController>();
                
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
                
            if (spiderIK == null)
                spiderIK = GetComponent<Spider>();
        }
        
        private void Update()
        {
            if (currentState == SpiderState.Dead)
                return;
                
            // Update state machine
            UpdateStateMachine();
            
            // Handle movement based on current state
            HandleMovement();
            
            // Update animations
            UpdateAnimations();
        }
        
        private void UpdateStateMachine()
        {
            stateTimer += Time.deltaTime;
            
            switch (currentState)
            {
                case SpiderState.Spawning:
                    if (stateTimer > 2f) // Spawn animation time
                    {
                        currentState = SpiderState.Wandering;
                        stateTimer = 0f;
                    }
                    break;
                    
                case SpiderState.Wandering:
                    // Look for targets
                    FindTarget();
                    
                    // Check if we reached the wander point
                    if (Vector3.Distance(transform.position, wanderPoint) < minDistanceToWanderPoint)
                    {
                        SetNewWanderPoint();
                    }
                    
                    // Return to original position if wandered too far
                    if (Vector3.Distance(transform.position, originalPosition) > maxWanderDistance)
                    {
                        wanderPoint = originalPosition;
                    }
                    break;
                    
                case SpiderState.Targeting:
                    // Check if target is still valid
                    if (currentTarget == null || Vector3.Distance(transform.position, currentTarget.position) > targetingRange * 1.5f)
                    {
                        currentState = SpiderState.Wandering;
                        currentTarget = null;
                        break;
                    }
                    
                    // Check if we're in attack range
                    if (Vector3.Distance(transform.position, currentTarget.position) < attackRange)
                    {
                        currentState = SpiderState.Attacking;
                        stateTimer = 0f;
                    }
                    break;
                    
                case SpiderState.Attacking:
                    // Attack animation time
                    if (stateTimer > attackDuration)
                    {
                        // Apply damage to target at appropriate time in animation
                        if (stateTimer > attackDuration * 0.5f && currentTarget != null)
                        {
                            // Damage logic would go here
                        }
                        
                        // End attack state
                        if (stateTimer > attackDuration + attackCooldown)
                        {
                            currentState = currentTarget != null ? SpiderState.Targeting : SpiderState.Wandering;
                            stateTimer = 0f;
                        }
                    }
                    break;
                    
                case SpiderState.Damaged:
                    // Damage recovery time
                    if (stateTimer > damageRecoveryTime)
                    {
                        currentState = currentTarget != null ? SpiderState.Targeting : SpiderState.Wandering;
                        stateTimer = 0f;
                    }
                    break;
            }
        }
        
        private void HandleMovement()
        {
            Vector3 targetPosition = Vector3.zero;
            float speedMultiplier = normalSpeed;
            
            switch (currentState)
            {
                case SpiderState.Wandering:
                    targetPosition = wanderPoint;
                    break;
                    
                case SpiderState.Targeting:
                    if (currentTarget != null)
                    {
                        targetPosition = currentTarget.position;
                        speedMultiplier = chaseSpeed;
                    }
                    break;
                    
                case SpiderState.Attacking:
                case SpiderState.Damaged:
                case SpiderState.Dead:
                    // Don't move
                    controller.SetMovementDirection(Vector2.zero);
                    return;
            }
            
            // Calculate direction to target in world space
            Vector3 direction = targetPosition - transform.position;
            
            if (direction.magnitude > 0.1f)
            {
                // Convert world direction to local direction (relative to current orientation)
                Vector3 localDirection = transform.InverseTransformDirection(direction);
                Vector2 movementDirection = new Vector2(localDirection.x, localDirection.z).normalized;
                
                // Apply to controller
                controller.SetMovementDirection(movementDirection * speedMultiplier);
                
                // Rotate toward target
                if (currentState != SpiderState.Damaged)
                {
                    Quaternion lookRotation = Quaternion.LookRotation(direction);
                    transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);
                }
            }
            else
            {
                controller.SetMovementDirection(Vector2.zero);
            }
        }
        
        private void UpdateAnimations()
        {
            // Using cached property IDs instead of strings
            animator.SetBool(hashIsWalkingParam, controller.Speed > 0.1f);
            animator.SetBool(hashIsAttackingParam, currentState == SpiderState.Attacking);
            animator.SetBool(hashIsDamagedParam, currentState == SpiderState.Damaged);
            animator.SetBool(hashIsDeadParam, currentState == SpiderState.Dead);
        }
        
        private void FindTarget()
        {
            // Pre-allocate array for efficiency
            Collider[] colliderArray = new Collider[10]; // Adjust size as needed
    
            // Use non-allocating version of OverlapSphere
            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, detectionRadius, colliderArray, targetLayer);
    
            // Process only valid hits
            for (int i = 0; i < hitCount; i++)
            {
                var hitObject = colliderArray[i];
                if (hitObject.CompareTag("Player"))
                {
                    currentTarget = hitObject.transform;
                    currentState = SpiderState.Targeting;
                    return;
                }
            }
        }
        
        private void SetNewWanderPoint()
        {
            // Generate random point within wander radius
            Vector2 randomCirclePoint = Random.insideUnitCircle * wanderRadius;
            Vector3 randomDirection = new Vector3(randomCirclePoint.x, 0, randomCirclePoint.y);
            wanderPoint = transform.position + randomDirection;
            
            // Make sure the wander point is not too far from original position
            if (Vector3.Distance(wanderPoint, originalPosition) > maxWanderDistance)
            {
                Vector3 directionToOriginal = originalPosition - transform.position;
                wanderPoint = transform.position + directionToOriginal.normalized * wanderRadius;
            }
        }
        
        public void TakeDamage(float damage)
        {
            if (currentState == SpiderState.Dead)
                return;
                
            // Apply damage
            currentHealth -= damage;
            
            // Check for death
            if (currentHealth <= 0)
            {
                Die();
                return;
            }
            
            // Enter damaged state
            currentState = SpiderState.Damaged;
            stateTimer = 0f;
            
            // Apply knockback if there's a target
            if (currentTarget != null)
            {
                Vector3 knockbackDirection = transform.position - currentTarget.position;
                controller.AddVelocity(new Vector2(knockbackDirection.x, knockbackDirection.z).normalized * knockbackForce);
            }
        }
        
        private void Die()
        {
            currentState = SpiderState.Dead;
            controller.SetMovementDirection(Vector2.zero);
            
            // Disable colliders
            Collider[] colliders = GetComponentsInChildren<Collider>();
            foreach (Collider col in colliders)
            {
                col.enabled = false;
            }
            
            // Additional death logic could go here
        }
    }
}
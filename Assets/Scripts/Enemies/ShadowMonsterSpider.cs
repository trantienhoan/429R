using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using Enemies;

public class ShadowMonsterSpider : MonoBehaviour 
{
    // Target detection
    [Header("Target Detection")]
    [SerializeField] private string treeOfLightPotTag = "TreeOfLightPot";
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private float detectionRange = 20f;
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private LayerMask detectionLayers;
    private Transform currentTarget;
    
    // Movement settings
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float turnSpeed = 120f;
    [SerializeField] private float surfaceDetectionDistance = 2f;
    [SerializeField] private LayerMask surfaceLayers; // Wall, Ceiling, Ground layers
    [SerializeField] private Transform raycastOrigin;
    [SerializeField] private float raySphereRadius = 0.5f;
    
    // IK Legs
    [Header("IK System")]
    [SerializeField] private Transform[] legTargets;
    [SerializeField] private Transform[] legIKSolvers;
    [SerializeField] private float legMoveDistance = 0.5f;
    [SerializeField] private float legMoveDuration = 0.2f;
    [SerializeField] private float footHeight = 0.3f;
    private bool[] legMoving;
    
    // Health System
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float knockbackForce = 5f;
    private float currentHealth;
    
    // Attack settings
    [Header("Attack Settings")]
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private float attackCooldown = 2f;
    private float lastAttackTime;
    
    // Visual effects
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem deathParticles;
    [SerializeField] private Animator spiderAnimator;
    
    // State management
    private enum SpiderState { Spawning, Wandering, Targeting, Attacking, Damaged, Dead }
    private SpiderState currentState = SpiderState.Spawning;
    
    // Surface movement
    private Vector3 gravityDirection = Vector3.down;
    private Vector3 surfaceNormal = Vector3.up;
    private RaycastHit surfaceHit;
    
    // Path management
    [SerializeField] private Transform[] waypointsBetweenSpawnAndWindow;
    [SerializeField] private Transform[] waypointsFromWindowToRoom;
    private int currentWaypointIndex = 0;
    private List<Transform> currentPath = new List<Transform>();
    
    void Awake()
    {
        legMoving = new bool[legTargets.Length];
        currentHealth = maxHealth;
        lastAttackTime = -attackCooldown; // Allow immediate attack
        
        // Initialize animation if available
        if (spiderAnimator == null)
        {
            spiderAnimator = GetComponentInChildren<Animator>();
        }
    }
    
    void Start()
    {
        // Set initial path from spawn to window
        foreach (Transform waypoint in waypointsBetweenSpawnAndWindow)
        {
            currentPath.Add(waypoint);
        }
        
        // Register with monster manager if it exists
        MonsterManager monsterManager = FindAnyObjectByType<MonsterManager>();
        if (monsterManager != null)
        {
            ShadowMonster monsterComponent = gameObject.GetComponent<ShadowMonster>();
			if (monsterComponent != null) {
   			 	MonsterManager.Instance.RegisterMonster(monsterComponent);
			}
        }
    }
    
    void Update()
    {
        if (currentState == SpiderState.Dead)
            return;
        
        // Detect surface under spider for proper orientation
        DetectSurface();
        
        // Orient spider to surface
        AlignToSurface();
        
        switch (currentState)
        {
            case SpiderState.Spawning:
                FollowPathToWindow();
                break;
            case SpiderState.Wandering:
                WanderBehavior();
                break;
            case SpiderState.Targeting:
                TargetBehavior();
                break;
            case SpiderState.Attacking:
                AttackBehavior();
                break;
            case SpiderState.Damaged:
                // Handled by coroutine
                break;
        }
        
        // Handle leg IK movement
        UpdateLegPositions();
    }
    
    private void DetectSurface()
    {
        if (Physics.SphereCast(transform.position, raySphereRadius, gravityDirection, out surfaceHit, surfaceDetectionDistance, surfaceLayers))
        {
            surfaceNormal = surfaceHit.normal;
        }
    }
    
    private void AlignToSurface()
    {
        // Calculate rotation to align with surface
        Quaternion targetRotation = Quaternion.FromToRotation(transform.up, surfaceNormal) * transform.rotation;
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 8f);
        
        // Update gravity direction based on surface
        gravityDirection = -surfaceNormal;
        
        // Keep spider adhered to surface
        if (surfaceHit.distance > 0.1f)
        {
            transform.position = Vector3.Lerp(
                transform.position, 
                transform.position + gravityDirection * (surfaceHit.distance - 0.1f), 
                Time.deltaTime * 5f);
        }
    }
    
    private void FollowPathToWindow()
    {
        if (currentPath.Count == 0 || currentWaypointIndex >= currentPath.Count)
        {
            // If we've reached the window, transition to entering room
            if (currentWaypointIndex >= currentPath.Count && 
                currentPath.Count > 0 && 
                currentPath[0] == waypointsBetweenSpawnAndWindow[0])
            {
                currentPath.Clear();
                foreach (Transform waypoint in waypointsFromWindowToRoom)
                {
                    currentPath.Add(waypoint);
                }
                currentWaypointIndex = 0;
            }
            // If we're inside the room, start looking for targets
            else
            {
                currentState = SpiderState.Wandering;
                return;
            }
        }
        
        Transform currentWaypoint = currentPath[currentWaypointIndex];
        
        // Move towards waypoint
        Vector3 directionToWaypoint = currentWaypoint.position - transform.position;
        directionToWaypoint -= Vector3.Project(directionToWaypoint, transform.up); // Remove up component
        directionToWaypoint.Normalize();
        
        // Rotate towards waypoint
        if (directionToWaypoint != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToWaypoint, transform.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, 
                targetRotation, 
                turnSpeed * Time.deltaTime);
        }
        
        // Move forward
        transform.position += transform.forward * moveSpeed * Time.deltaTime;
        
        // Check if we've reached waypoint
        float distanceToWaypoint = Vector3.Distance(
            transform.position,
            currentWaypoint.position);
            
        if (distanceToWaypoint < 0.5f)
        {
            currentWaypointIndex++;
        }
    }
    
    private void WanderBehavior()
    {
        // Find target
        FindTarget();
        if (currentTarget != null)
        {
            currentState = SpiderState.Targeting;
            return;
        }
        
        // If no target, just move forward and occasionally turn
        transform.position += transform.forward * moveSpeed * 0.5f * Time.deltaTime;
        
        // Random turning
        if (Random.value < 0.01f)
        {
            float randomTurn = Random.Range(-90f, 90f);
            StartCoroutine(TurnOverTime(randomTurn, 1f));
        }
		
		GameObject pot = GameObject.FindGameObjectWithTag(treeOfLightPotTag);
		if (pot == null) {
    		// Handle missing pot - set a default target or use a different behavior
    		Debug.LogWarning("TreeOfLightPot not found. Check if tag is defined and assigned.");
    		return; // Or set a default target
		}

    }
    
    private void FindTarget()
    {
        // Look for TreeOfLightPot first
        GameObject pot = GameObject.FindGameObjectWithTag(treeOfLightPotTag);
        if (pot != null)
        {
            // Check if we can see the pot with a raycast
            Vector3 directionToPot = pot.transform.position - transform.position;
            if (Physics.Raycast(transform.position, directionToPot, out RaycastHit hit, detectionRange, detectionLayers))
            {
                if (hit.transform.CompareTag(treeOfLightPotTag))
                {
                    currentTarget = pot.transform;
                    return;
                }
            }
        }
        
        // Look for player second
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player != null)
        {
            Vector3 directionToPlayer = player.transform.position - transform.position;
            if (Physics.Raycast(transform.position, directionToPlayer, out RaycastHit hit, detectionRange, detectionLayers))
            {
                if (hit.transform.CompareTag(playerTag))
                {
                    currentTarget = player.transform;
                }
            }
        }
    }
    
    private void TargetBehavior()
    {
        if (currentTarget == null)
        {
            currentState = SpiderState.Wandering;
            return;
        }
        
        // Check if target still exists and visible
        if (!CanSeeTarget())
        {
            currentTarget = null;
            currentState = SpiderState.Wandering;
            return;
        }
        
        // Get direction to target
        Vector3 directionToTarget = currentTarget.position - transform.position;
        directionToTarget -= Vector3.Project(directionToTarget, transform.up); // Project onto current orientation plane
        directionToTarget.Normalize();
        
        // Rotate towards target
        if (directionToTarget != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToTarget, transform.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, 
                targetRotation, 
                turnSpeed * Time.deltaTime);
        }
        
        // Move towards target
        transform.position += transform.forward * moveSpeed * Time.deltaTime;
        
        // Check if within attack range
        float distanceToTarget = Vector3.Distance(transform.position, currentTarget.position);
        if (distanceToTarget <= attackRange)
        {
            currentState = SpiderState.Attacking;
        }
    }
    
    private bool CanSeeTarget()
    {
        if (currentTarget == null)
            return false;
            
        Vector3 directionToTarget = currentTarget.position - transform.position;
        float distanceToTarget = directionToTarget.magnitude;
        
        if (distanceToTarget > detectionRange)
            return false;
            
        if (Physics.Raycast(transform.position, directionToTarget, out RaycastHit hit, distanceToTarget, detectionLayers))
        {
            return hit.transform == currentTarget;
        }
        
        return false;
    }
    
    private void AttackBehavior()
    {
        if (currentTarget == null)
        {
            currentState = SpiderState.Wandering;
            return;
        }
        
        // Look at target
        Vector3 directionToTarget = currentTarget.position - transform.position;
        directionToTarget -= Vector3.Project(directionToTarget, transform.up);
        
        if (directionToTarget != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToTarget, transform.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, 
                targetRotation, 
                turnSpeed * Time.deltaTime);
        }
        
        // Check if still in attack range
        float distanceToTarget = Vector3.Distance(transform.position, currentTarget.position);
        if (distanceToTarget > attackRange * 1.1f)
        {
            currentState = SpiderState.Targeting;
            return;
        }
        
        // Attack if cooldown passed
        if (Time.time >= lastAttackTime + attackCooldown)
        {
            PerformAttack();
        }
    }
    
    private void PerformAttack()
    {
        lastAttackTime = Time.time;
        
        // Play attack animation
        if (spiderAnimator != null)
        {
            spiderAnimator.SetTrigger("Attack");
        }
        
        // Apply damage to target based on tag
        if (currentTarget.CompareTag(treeOfLightPotTag))
        {
            // Get the pot component - using general MonoBehaviour instead of specific class
            MonoBehaviour pot = currentTarget.GetComponent<MonoBehaviour>();
            if (pot != null)
            {
                // Try to invoke TakeDamage method without requiring specific type
                pot.SendMessage("TakeDamage", attackDamage, SendMessageOptions.DontRequireReceiver);
                
                // Debug log
                Debug.Log("Spider attacked TreeOfLightPot: " + attackDamage + " damage");
            }
        }
        else if (currentTarget.CompareTag(playerTag))
        {
            // Handle player damage using SendMessage
            currentTarget.SendMessage("TakeDamage", attackDamage, SendMessageOptions.DontRequireReceiver);
            Debug.Log("Spider attacked Player: " + attackDamage + " damage");
        }
    }
    
    public void TakeDamage(float damage, Vector3 hitDirection = default)
    {
        if (currentState == SpiderState.Dead)
            return;
        
        currentHealth -= damage;
        
        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            StartCoroutine(DamageReaction(hitDirection));
        }
    }
    
    private IEnumerator DamageReaction(Vector3 hitDirection)
    {
        currentState = SpiderState.Damaged;
        
        // Play damage animation
        if (spiderAnimator != null)
        {
            spiderAnimator.SetTrigger("Hit");
        }
        
        // Apply knockback - project onto current orientation plane
        if (hitDirection != Vector3.zero)
        {
            hitDirection -= Vector3.Project(hitDirection, transform.up);
            hitDirection.Normalize();
            
            float knockbackTime = 0.2f;
            float elapsed = 0f;
            
            while (elapsed < knockbackTime)
            {
                transform.position += hitDirection * knockbackForce * Time.deltaTime;
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
        
        // Wait for a moment
        yield return new WaitForSeconds(0.3f);
        
        // Resume normal behavior
        currentState = currentTarget != null ? SpiderState.Targeting : SpiderState.Wandering;
    }
    
    private void Die()
    {
        currentState = SpiderState.Dead;
        
        // Play death animation
        if (spiderAnimator != null)
        {
            spiderAnimator.SetTrigger("Die");
        }
        
        // Spawn particles
        if (deathParticles != null)
        {
            deathParticles.Play();
        }
        
        // Relax IK legs
        RelaxIKLegs();
        
        // Notify monster manager
        MonsterManager manager = FindAnyObjectByType<MonsterManager>();
        if (manager != null)
        {
            // Using generic gameObject reference instead of specific component
            manager.SendMessage("UnregisterMonster", gameObject, SendMessageOptions.DontRequireReceiver);
        }
        
        // Remove after delay
        Destroy(gameObject, 5f);
    }
    
    private void RelaxIKLegs()
    {
        // Add rigidbodies to leg parts to make them dangle
        Transform[] legParts = GetComponentsInChildren<Transform>();
        foreach (Transform part in legParts)
        {
            if (part.name.Contains("Leg") && part != transform)
            {
                if (part.GetComponent<Rigidbody>() == null)
                {
                    Rigidbody rb = part.gameObject.AddComponent<Rigidbody>();
                    rb.useGravity = true;
                    rb.mass = 0.1f;
                    
                    // Add collider if needed
                    if (part.GetComponent<Collider>() == null)
                    {
                        CapsuleCollider col = part.gameObject.AddComponent<CapsuleCollider>();
                        col.radius = 0.05f;
                        col.height = 0.2f;
                        col.direction = 2; // Z axis
                    }
                }
            }
        }
        
        // Disable IK solvers
        if (legIKSolvers != null)
        {
            foreach (Transform solver in legIKSolvers)
            {
                solver.gameObject.SetActive(false);
            }
        }
    }
    
    private void UpdateLegPositions()
    {
        if (legTargets == null || legTargets.Length == 0)
            return;
            
        for (int i = 0; i < legTargets.Length; i++)
        {
            if (legMoving[i])
                continue;
                
            Vector3 targetPosition = legTargets[i].position;
            
            // Raycast down from leg position
            Ray ray = new Ray(legTargets[i].position + transform.up * footHeight, -transform.up);
            if (Physics.Raycast(ray, out RaycastHit hit, footHeight * 2, surfaceLayers))
            {
                Vector3 desiredPosition = hit.point;
                
                // If leg is too far from desired position, move it
                float distanceToDesired = Vector3.Distance(targetPosition, desiredPosition);
                if (distanceToDesired > legMoveDistance)
                {
                    StartCoroutine(MoveLeg(i, desiredPosition));
                }
            }
        }
    }
    
    private IEnumerator MoveLeg(int legIndex, Vector3 targetPosition)
    {
        legMoving[legIndex] = true;
        
        Vector3 startPosition = legTargets[legIndex].position;
        float elapsed = 0f;
        
        // Create arc for leg movement
        while (elapsed < legMoveDuration)
        {
            float t = elapsed / legMoveDuration;
            
            // Add arc movement
            float height = Mathf.Sin(t * Mathf.PI) * footHeight;
            Vector3 arcPosition = Vector3.Lerp(startPosition, targetPosition, t) + transform.up * height;
            
            legTargets[legIndex].position = arcPosition;
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        legTargets[legIndex].position = targetPosition;
        legMoving[legIndex] = false;
    }
    
    private IEnumerator TurnOverTime(float angle, float duration)
    {
        float elapsed = 0f;
        Quaternion startRotation = transform.rotation;
        Quaternion targetRotation = transform.rotation * Quaternion.Euler(0, angle, 0);
        
        while (elapsed < duration)
        {
            transform.rotation = Quaternion.Slerp(
                startRotation, 
                targetRotation, 
                elapsed / duration);
                
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        transform.rotation = targetRotation;
    }
    
    // Method to set path from spawner
    public void SetPath(Transform[] path)
    {
        currentPath.Clear();
        foreach (Transform waypoint in path)
        {
            currentPath.Add(waypoint);
        }
        currentWaypointIndex = 0;
    }
}
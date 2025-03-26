using UnityEngine;

public class ShadowMonster : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private float stoppingDistance = 1f;
    
    [Header("Attack Settings")]
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float attackCooldown = 1f;
    
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 50f;
    [SerializeField] private float currentHealth;
    
    [Header("Effects")]
    [SerializeField] private ParticleSystem attackEffect;
    [SerializeField] private AudioClip attackSound;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private Material normalMaterial;
    [SerializeField] private Material damageMaterial;
    [SerializeField] private float damageFlashDuration = 0.2f;
    [SerializeField] private ParticleSystem deathEffect;
    [SerializeField] private AudioClip deathSound;
    
    [Header("Surface Movement")]
    [SerializeField] private float raycastDistance = 1.0f;
    [SerializeField] private LayerMask surfaceLayers;
    [SerializeField] private bool canWallCrawl = true;
    [SerializeField] private float gravityMultiplier = 5f;
    [SerializeField] private float surfaceDetectionRadius = 0.5f;

    [Header("Window Navigation")]
    [SerializeField] private LayerMask windowLayerMask;
    [SerializeField] private float windowDetectionRange = 5f;
    [SerializeField] private float windowPassThroughSpeed = 2f;
    
    private void CheckForWindows()
    {
        if (isPassingThroughWindow) return;
    
        // Check if there's a window between us and the target
        Vector3 directionToTarget = (target.position - transform.position).normalized;
    
        if (Physics.Raycast(transform.position, directionToTarget, out RaycastHit hit, 
                windowDetectionRange, windowLayerMask))
        {
            // Found a window
            targetWindow = hit.transform;
            StartCoroutine(PassThroughWindow(targetWindow));
        }
    }

    private IEnumerator PassThroughWindow(Transform window)
    {
        isPassingThroughWindow = true;
    
        // Get window center
        Vector3 windowCenter = window.position;
    
        // Move towards window center
        float startTime = Time.time;
        Vector3 startPos = transform.position;
    
        while (Vector3.Distance(transform.position, windowCenter) > 0.1f)
        {
            float journeyLength = Vector3.Distance(startPos, windowCenter);
            float distanceCovered = (Time.time - startTime) * windowPassThroughSpeed;
            float fractionOfJourney = distanceCovered / journeyLength;
        
            transform.position = Vector3.Lerp(startPos, windowCenter, fractionOfJourney);
            yield return null;
        }
    
        // Once at window center, teleport slightly to other side
        Vector3 directionToTarget = (target.position - windowCenter).normalized;
        transform.position = windowCenter + directionToTarget * 0.5f;
    
        isPassingThroughWindow = false;
        targetWindow = null;
    }

    private bool isPassingThroughWindow = false;
    private Transform targetWindow = null;

    private Vector3 surfaceNormal = Vector3.up;
    private bool isGrounded = false;
    private Transform currentSurface;


    private Renderer monsterRenderer;

    
    public enum MonsterState { Idle, Chasing, Attacking, Dead }
    private MonsterState currentState = MonsterState.Idle;
    private Transform target;
    private float lastAttackTime;
    private bool isDead = false;
    private Animator animator;
    private static readonly int AttackTrigger = Animator.StringToHash("Attack");
    public delegate void MonsterEvent(ShadowMonster monster);
    public static event MonsterEvent OnMonsterSpawned;
    public static event MonsterEvent OnMonsterDeath;

    private void Start()
    {
        monsterRenderer = GetComponentInChildren<Renderer>();
        currentHealth = maxHealth;
        animator = GetComponent<Animator>();
        OnMonsterSpawned?.Invoke(this);
        
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
        
        // Find the Tree of Light
        target = GameObject.FindGameObjectWithTag("TreeOfLight")?.transform;
        if (target == null)
        {
            Debug.LogWarning("No Tree of Light found in scene!");
            enabled = false;
            return;
        }
        
        Debug.Log($"ShadowMonster spawned - targeting Tree of Light at {target.position}");
        
    }
    private void CheckSurface()
    {
        isGrounded = false;
    
        // Cast multiple rays in a sphere to detect surfaces
        RaycastHit closestHit = new RaycastHit();
        float closestDistance = float.MaxValue;
    
        for (int i = 0; i < 6; i++)
        {
            Vector3 direction = Vector3.zero;
        
            // Use 6 primary directions
            switch (i)
            {
                case 0: direction = Vector3.down; break;
                case 1: direction = Vector3.up; break;
                case 2: direction = Vector3.forward; break;
                case 3: direction = Vector3.back; break;
                case 4: direction = Vector3.left; break;
                case 5: direction = Vector3.right; break;
            }
        
            if (Physics.SphereCast(transform.position, surfaceDetectionRadius, direction, 
                    out RaycastHit hit, raycastDistance, surfaceLayers))
            {
                if (hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    closestHit = hit;
                    isGrounded = true;
                }
            }
        }
    
        if (isGrounded)
        {
            surfaceNormal = closestHit.normal;
            currentSurface = closestHit.transform;
        }
        else
        {
            surfaceNormal = Vector3.up;
            currentSurface = null;
        }
    }


    private void Update()
    {
        if (isDead) return;

        CheckSurface();
        CheckForWindows();
    
        // Move towards the tree
        Vector3 direction = (target.position - transform.position).normalized;
    
        // Adjust direction based on current surface
        if (canWallCrawl && currentSurface != null)
        {
            // Project movement direction onto the surface plane
            direction = Vector3.ProjectOnPlane(direction, surfaceNormal).normalized;
        }
    
        // Rotate to face direction
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction, surfaceNormal);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        // Check if we're in attack range
        float distanceToTarget = Vector3.Distance(transform.position, target.position);
    
        if (distanceToTarget <= attackRange)
        {
            // Attack the tree
            if (Time.time >= lastAttackTime + attackCooldown)
            {
                Attack();
                lastAttackTime = Time.time;
            }
        }
        else if (distanceToTarget > stoppingDistance)
        {
            // Move towards the tree
            transform.position += direction * moveSpeed * Time.deltaTime;
        
            // Apply gravity if not on a surface and can wall crawl
            if (!isGrounded && canWallCrawl)
            {
                transform.position += Physics.gravity.normalized * gravityMultiplier * Time.deltaTime;
            }
        }
    }

    private void Attack()
    {
        TreeOfLight tree = target.GetComponent<TreeOfLight>();
        if (tree != null)
        {
            // Play attack animation if available
            if (animator != null)
            {
                animator.SetTrigger(AttackTrigger);
            }
            
            // Play attack effect if available
            if (attackEffect != null)
            {
                attackEffect.Play();
            }
            
            // Play attack sound if available
            if (attackSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(attackSound);
            }
            
            // Deal damage to the tree
            tree.TakeDamage(attackDamage);
            Debug.Log($"ShadowMonster attacked Tree of Light for {attackDamage} damage");
        }
    }

    public void TakeDamage(float damage)
    {
        if (isDead) return;
        
        currentHealth -= damage;
        
        // Visual feedback
        if (monsterRenderer != null && normalMaterial != null && damageMaterial != null)
            StartCoroutine(FlashDamage());

        if (currentHealth <= 0)
        {
            Die();
        }
    }
    private IEnumerator FlashDamage() {
        monsterRenderer.material = damageMaterial;
        yield return new WaitForSeconds(damageFlashDuration);
        monsterRenderer.material = normalMaterial;
    }

    private void Die()
    {
        isDead = true;
        // Play death animation if available
        if (animator != null) {
            animator.SetTrigger("Death");
        }
    
        // Play death effect if available
        if (deathEffect != null) {
            deathEffect.Play();
        }
    
        // Play death sound if available
        if (deathSound != null && audioSource != null) {
            audioSource.PlayOneShot(deathSound);
        }

        OnMonsterDeath?.Invoke(this);
    
        // Disable colliders
        Collider[] colliders = GetComponents<Collider>();
        foreach (Collider c in colliders) {
            c.enabled = false;
        }
    
        Destroy(gameObject, 3f); // Longer time to allow effects to finish
    }

    private void OnDrawGizmosSelected()
    {
        // Visualize attack range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
} 
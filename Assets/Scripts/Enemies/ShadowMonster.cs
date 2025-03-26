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
    
    private Transform target;
    private float lastAttackTime;
    private bool isDead = false;
    private Animator animator;
    private static readonly int AttackTrigger = Animator.StringToHash("Attack");

    private void Start()
    {
        currentHealth = maxHealth;
        animator = GetComponent<Animator>();
        
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

    private void Update()
    {
        if (isDead) return;

        // Move towards the tree
        Vector3 direction = (target.position - transform.position).normalized;
        transform.rotation = Quaternion.Slerp(transform.rotation, 
            Quaternion.LookRotation(direction), 
            rotationSpeed * Time.deltaTime);

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
        
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        isDead = true;
        // You can add death animation or effects here
        Destroy(gameObject, 1f);
    }

    private void OnDrawGizmosSelected()
    {
        // Visualize attack range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
} 
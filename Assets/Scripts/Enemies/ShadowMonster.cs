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
    
    private Transform target;
    private float lastAttackTime;
    private bool isDead = false;

    private void Start()
    {
        currentHealth = maxHealth;
        // Find the Tree of Light
        target = GameObject.FindGameObjectWithTag("TreeOfLight")?.transform;
        if (target == null)
        {
            Debug.LogWarning("No Tree of Light found in scene!");
            enabled = false;
            return;
        }
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
            tree.TakeDamage(attackDamage);
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
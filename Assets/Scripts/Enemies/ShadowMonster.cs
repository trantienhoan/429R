using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System;

public class ShadowMonster : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float health = 100f;
    [SerializeField] private float speed = 3.5f;
    [SerializeField] private float damage = 10f;
    
    [Header("Attack Settings")]
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float attackCooldown = 2f;
    [SerializeField] private float detectionRadius = 10f;
    
    [Header("Effects")]
    [SerializeField] private ParticleSystem deathEffect;
    [SerializeField] private AudioClip deathSound;
    [SerializeField] private AudioClip attackSound;
    [SerializeField] private AudioSource audioSource;
    
    [Header("Light Weakness")]
    [SerializeField] private float lightDamageMultiplier = 2f;
    [SerializeField] private float minLightIntensityToDamage = 0.5f;
    
    // References
    private NavMeshAgent agent;
    private Animator animator;
    private GameObject player;
    private PlayerHealth playerHealth;
    //private bool isDead = false;
    private float lastAttackTime;
    
    // Delegate for spawner to listen to
    public delegate void MonsterEventHandler(ShadowMonster monster);
    
    // Events
    public event MonsterEventHandler MonsterSpawned;
    public event MonsterEventHandler MonsterDeath;
    public event Action OnMonsterDeath;
    
    // Animation hashes
    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int Attack = Animator.StringToHash("Attack");
    private static readonly int IsDeadHash = Animator.StringToHash("IsDead");
    
    private void Awake()
    {
        // Set up references
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // Configure NavMeshAgent
        if (agent != null)
        {
            agent.speed = speed;
            agent.stoppingDistance = attackRange * 0.8f;
        }
    }
    
    private void Start()
    {
        // Find player
        player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogWarning("Player not found. Make sure it has the 'Player' tag.");
        }
        else
        {
            playerHealth = player.GetComponent<PlayerHealth>();
            if (playerHealth == null)
            {
                Debug.LogWarning("Player does not have a PlayerHealth component");
            }
        }
        
        // Notify that monster has spawned
        if (MonsterSpawned != null)
        {
            MonsterSpawned(this);
        }
    }
    
    private void Update()
    {
        if (isDead)
        {
            // Check if player is within detection range
            float distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);
            
            if (distanceToPlayer <= detectionRadius)
            {
                // Move toward player
                if (agent != null)
                {
                    agent.SetDestination(player.transform.position);
                }
                
                // Update animation
                if (animator != null)
                {
                    animator.SetFloat(Speed, agent.velocity.magnitude);
                }
                
                // Check if in attack range and cooldown has elapsed
                if (distanceToPlayer <= attackRange && Time.time >= lastAttackTime + attackCooldown)
                {
                    AttackPlayer();
                }
            }
            else
            {
                // Stop moving if player is out of detection range
                if (agent != null)
                {
                    agent.SetDestination(transform.position);
                }
                
                // Update animation to idle
                if (animator != null)
                {
                    animator.SetFloat(Speed, 0);
                }
            }
        }
        
        // Check for light damage
        CheckForLightDamage();
    }
    
    private void AttackPlayer()
    {
        // Update last attack time
        lastAttackTime = Time.time;
        
        // Play attack animation
        if (animator != null)
        {
            animator.SetTrigger(Attack);
        }
        
        // Play attack sound
        if (audioSource != null && attackSound != null)
        {
            audioSource.PlayOneShot(attackSound);
        }
        
        // Apply damage to player
        if (playerHealth != null)
        {
            // Allow a small delay before applying damage to match animation
            StartCoroutine(DealDamageWithDelay(0.5f));
        }
    }
    
    private IEnumerator DealDamageWithDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (playerHealth != null && !isDead)
        {
            // Check if still in range before applying damage
            float distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);
            if (distanceToPlayer <= attackRange * 1.5f)
            {
                playerHealth.TakeDamage(damage);
            }
        }
    }
    
    public void TakeDamage(float damageAmount)
    {
        if (isDead) return;
        
        health -= damageAmount;
        
        if (health <= 0)
        {
            Die();
        }
    }
    private bool isDead = false;

    private void Die()
    {
        isDead = true;
        
        // Stop movement
        if (agent != null)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }
        
        // Play death animation
        if (animator != null)
        {
            animator.SetBool(IsDead, true);
        }
        
        // Play death effects
        if (deathEffect != null)
        {
            Instantiate(deathEffect, transform.position, Quaternion.identity);
        }
        
        // Play death sound
        if (audioSource != null && deathSound != null)
        {
            audioSource.PlayOneShot(deathSound);
        }
        
        // Disable colliders
        Collider[] colliders = GetComponents<Collider>();
        foreach (Collider collider in colliders)
        {
            collider.enabled = false;
        }
        
        // Notify that monster has died
        if (MonsterDeath != null)
        {
            MonsterDeath(this);
        }
        
        OnMonsterDeath?.Invoke();

        // Destroy after a delay
        StartCoroutine(DestroyAfterDelay(3f));
    }
    
    public virtual bool IsDead()
    {
        return isDead;
    }
    public void SetDead(bool value)
    {
        isDead = value;
        
        // Update animator
        if (animator != null)
        {
            animator.SetBool(IsDeadHash, isDead);
        }
    }

    private IEnumerator DestroyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        // Fade out
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        float fadeTime = 1f;
        float elapsed = 0;
        
        // Store original materials
        Material[] materials = new Material[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].material.HasProperty("_Color"))
            {
                materials[i] = renderers[i].material;
            }
        }
        
        // Fade out materials
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = elapsed / fadeTime;
            
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] != null && materials[i].HasProperty("_Color"))
                {
                    Color color = materials[i].color;
                    color.a = 1 - normalizedTime;
                    materials[i].color = color;
                }
            }
            
            yield return null;
        }
        
        Destroy(gameObject);
    }
    
    private void CheckForLightDamage()
    {
        // Check nearby lights that could harm the shadow monster
        Light[] nearbyLights = FindObjectsOfType<Light>();
        
        foreach (Light light in nearbyLights)
        {
            // Skip inactive lights or those below the damage threshold
            if (!light.enabled || light.intensity < minLightIntensityToDamage)
                continue;
                
            // Calculate distance and check if in range
            float distanceToLight = Vector3.Distance(transform.position, light.transform.position);
            float lightRange = light.range;
            
            // Skip if out of light range
            if (distanceToLight > lightRange)
                continue;
                
            // Calculate damage based on light intensity and distance
            float distanceFactor = 1 - (distanceToLight / lightRange);
            float lightDamage = light.intensity * lightDamageMultiplier * distanceFactor * Time.deltaTime;
            
            // Apply damage
            TakeDamage(lightDamage);
        }
    }
    
    // Called by light wave effect to instantly kill monster
    public void OnHitByLightWave()
    {
        TakeDamage(health); // Instantly kill
    }
    
    // Draw gizmos for visualization in the editor
    private void OnDrawGizmosSelected()
    {
        // Detection radius
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        
        // Attack range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
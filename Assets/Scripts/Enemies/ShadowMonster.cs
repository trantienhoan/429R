using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System;
using Core;

public class ShadowMonster : MonoBehaviour
{
    [Header("Light Weakness")]
    [SerializeField] private float lightDamageMultiplier = 2f;
    [SerializeField] private float minLightIntensityToDamage = 0.5f;

    // References
    private NavMeshAgent agent;
    private GameObject player;
    [SerializeField] private float maxHealth = 100f;

    protected HealthComponent healthComponent;
    private float lastAttackTime;

    // Animation hashes
    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int Attack = Animator.StringToHash("Attack");
    private static readonly int IsDeadHash = Animator.StringToHash("IsDead");

    [SerializeField] private float detectionRadius = 10f;

    private void Awake()
    {
        healthComponent = GetComponent<HealthComponent>();
        if (healthComponent == null)
        {
            healthComponent = gameObject.AddComponent<HealthComponent>();
        }

        healthComponent.SetMaxHealth(maxHealth);
        agent = GetComponent<NavMeshAgent>();
    }
    
    private void Start()
    {
        // Subscribe to the OnDeath event
        healthComponent.OnDeath += OnDeathHandler;
        player = GameObject.FindGameObjectWithTag("Player");

        if (player == null)
        {
            Debug.LogError("Player not found. Make sure the player has the 'Player' tag.");
        }
    }

    private void Update()
    {
        if (player == null) return;

        if (!healthComponent.IsDead())
        {
            // Check if player is within detection range
            float distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);

            if (distanceToPlayer <= detectionRadius)
            {
                // Move toward player
                if (agent != null && agent.enabled)
                {
                    agent.SetDestination(player.transform.position);
                }
            }
            else
            {
                // Stop moving if player is out of detection range
                if (agent != null && agent.enabled)
                {
                    agent.SetDestination(transform.position);
                }
            }
        }

        // Check for light damage
        CheckForLightDamage();
    }

    public float damage;

    public virtual void TakeDamage(float damageAmount, Vector3 hitPoint = default, GameObject damageSource = null)
    {
        healthComponent?.TakeDamage(damageAmount, hitPoint, damageSource); // Use the HealthComponent's TakeDamage
    }

    private void OnDeathHandler(HealthComponent healthComponent)
    {
        if (agent != null)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }

        StartCoroutine(DestroyAfterDelay(3f));
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
            if (renderers[i].material != null && renderers[i].material.HasProperty("_Color"))
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
        Light[] nearbyLights = FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

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
            TakeDamage(lightDamage, transform.position, gameObject);
        }
    }

    // Called by light wave effect to instantly kill monster
    public void OnHitByLightWave()
    {
        TakeDamage(healthComponent.MaxHealth, transform.position, gameObject); // Instantly kill
    }

    // Draw gizmos for visualization in the editor
    private void OnDrawGizmosSelected()
    {
        // Detection radius
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }

    private void OnDestroy()
    {
        healthComponent.OnDeath -= OnDeathHandler;
    }
}
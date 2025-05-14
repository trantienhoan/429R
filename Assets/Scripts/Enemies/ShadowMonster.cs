using UnityEngine;
 using UnityEngine.AI;
 using System.Collections;
 //using System; //Unnecessary

 using Core;

 public class ShadowMonster : BaseMonster // Inherit from BaseMonster
 {
  [Header("Light Weakness")]
  [SerializeField] private float lightDamageMultiplier = 2f;
  [SerializeField] private float minLightIntensityToDamage = 0.5f;

  [SerializeField] private float detectionRadius = 10f;

  // Missing variable declarations
  // protected NavMeshAgent agent; //Moved to BaseMonster
  // protected GameObject player; //Moved to BaseMonster
  // protected HealthComponent healthComponent; //Moved to BaseMonster

  public float damage; // is it used?

  // Missing Awake method
  protected override void Awake()
  {
   base.Awake(); // Call the base class's Awake method
   // Perform any specific initialization for ShadowMonster here
  }

  // Missing Start method
  protected override void Start()
  {
   base.Start(); // Call the base class's Start method
   // Perform any specific initialization for ShadowMonster here
  }

  private void Update()
  {
   if (player == null || healthComponent.IsDead()) return;

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

   // Check for light damage
   CheckForLightDamage();
  }


  public override void TakeDamage(float damageAmount, Vector3 hitPoint = default, GameObject damageSource = null)
  {
   healthComponent?.TakeDamage(damageAmount, hitPoint, damageSource); // Use the HealthComponent's TakeDamage
  }


  protected override void OnDeathHandler(HealthComponent healthComponent)
  {
   base.OnDeathHandler(healthComponent);
  }

  protected override IEnumerator DestroyAfterDelay(float delay)
  {
   return base.DestroyAfterDelay(delay);
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
    if (lightDamage > 0) // Add this check
    {
     TakeDamage(lightDamage, transform.position, gameObject);
    }
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

  protected override void OnDestroy()
  {
   base.OnDestroy();
  }
 }

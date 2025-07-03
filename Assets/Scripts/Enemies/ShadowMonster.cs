using UnityEngine;
using UnityEngine.AI;
using Core;

public class ShadowMonster : BaseMonster
{
    [Header("Light Weakness")]
    [SerializeField] private float lightDamageMultiplier = 2f;
    [SerializeField] private float minLightIntensityToDamage = 0.5f;
    [SerializeField] private float detectionRadius = 10f;
    
    protected override void Start()
    {
        base.Start();
    }

    private void Update()
    {
        if (player == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);
        bool inDetectionRange = distanceToPlayer <= detectionRadius;

        if (agent != null && agent.enabled)
        {
            agent.SetDestination(inDetectionRange ? player.transform.position : transform.position);
        }

        CheckForLightDamage();
    }

    private void CheckForLightDamage()
    {
        Light[] nearbyLights = FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (Light light in nearbyLights)
        {
            if (!light.enabled || light.intensity < minLightIntensityToDamage) continue;

            float distanceToLight = Vector3.Distance(transform.position, light.transform.position);
            float lightRange = light.range;

            if (distanceToLight > lightRange) continue;

            float distanceFactor = 1 - (distanceToLight / lightRange);
            float lightDamage = light.intensity * lightDamageMultiplier * distanceFactor * Time.deltaTime;
            
        }
    }
}

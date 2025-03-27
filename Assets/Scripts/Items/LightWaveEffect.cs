using System.Collections;
using UnityEngine;

public class LightWaveEffect : MonoBehaviour
{
    [Header("Wave Parameters")]
    [SerializeField] private float radius = 30f;
    [SerializeField] private float speed = 10f;
    [SerializeField] private float duration = 3f;
    [SerializeField] private Color waveColor = Color.white;
    
    [Header("Visual Effects")]
    [SerializeField] private Material waveMaterial;
    [SerializeField] private Light waveLight;
    [SerializeField] private ParticleSystem particleEffect;
    [SerializeField] private AnimationCurve intensityCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
    [SerializeField] private float maxLightIntensity = 8f;
    
    private bool isActive = false;
    private float elapsedTime = 0f;
    private Transform waveTransform;
    
    private void Awake()
    {
        // Initialize transforms
        waveTransform = transform;
        
        // Start with zero scale
        waveTransform.localScale = Vector3.zero;
        
        // Setup light component if it exists
        if (waveLight != null)
        {
            waveLight.color = waveColor;
            waveLight.intensity = 0;
        }
        
        // Configure material if it exists
        if (waveMaterial != null)
        {
            waveMaterial.color = waveColor;
            if (waveMaterial.HasProperty("_EmissionColor"))
            {
                waveMaterial.EnableKeyword("_EMISSION");
                waveMaterial.SetColor("_EmissionColor", waveColor * 2);
            }
        }
        
        // Apply material to renderer
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null && waveMaterial != null)
        {
            renderer.material = waveMaterial;
        }
        
        // Configure particle system if it exists
        if (particleEffect != null)
        {
            var main = particleEffect.main;
            main.startColor = waveColor;
        }
    }
    
    public void SetParameters(float radius, float speed, float duration, Color color)
    {
        this.radius = radius;
        this.speed = speed;
        this.duration = duration;
        this.waveColor = color;
        
        // Update visuals
        if (waveLight != null)
        {
            waveLight.color = waveColor;
        }
        
        if (waveMaterial != null)
        {
            waveMaterial.color = waveColor;
            if (waveMaterial.HasProperty("_EmissionColor"))
            {
                waveMaterial.SetColor("_EmissionColor", waveColor * 2);
            }
        }
        
        if (particleEffect != null)
        {
            var main = particleEffect.main;
            main.startColor = waveColor;
        }
    }
    
    public void StartWave()
    {
        if (!isActive)
        {
            isActive = true;
            elapsedTime = 0f;
            StartCoroutine(ExpandWave());
            
            // Play particle effect if it exists
            if (particleEffect != null)
            {
                particleEffect.Play();
            }
        }
    }
    
    private IEnumerator ExpandWave()
    {
        // Start with zero scale
        waveTransform.localScale = Vector3.zero;
        elapsedTime = 0f;
        
        // Calculate how long it will take to reach max radius at the given speed
        float maxTime = radius / speed;
        float actualDuration = Mathf.Min(duration, maxTime);
        
        while (elapsedTime < actualDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / actualDuration;
            
            // Calculate current radius based on speed and time
            float currentRadius = Mathf.Min(progress * radius, radius);
            
            // Update scale
            waveTransform.localScale = Vector3.one * currentRadius;
            
            // Update light intensity following curve
            if (waveLight != null)
            {
                waveLight.intensity = intensityCurve.Evaluate(progress) * maxLightIntensity;
            }
            
            // Update material opacity
            if (waveMaterial != null)
            {
                Color color = waveColor;
                color.a = intensityCurve.Evaluate(progress);
                waveMaterial.color = color;
            }
            
            yield return null;
        }
        
        // Ensure final scale is set
        waveTransform.localScale = Vector3.one * radius;
        
        // Fade out effects
        StartCoroutine(FadeOutEffect());
    }
    
    private IEnumerator FadeOutEffect()
    {
        float fadeTime = 1f;
        float elapsed = 0f;
        
        // Initial values
        float startLightIntensity = 0f;
        if (waveLight != null)
        {
            startLightIntensity = waveLight.intensity;
        }
        
        // Fade out over time
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / fadeTime;
            
            // Fade light
            if (waveLight != null)
            {
                waveLight.intensity = Mathf.Lerp(startLightIntensity, 0, progress);
            }
            
            // Fade material
            if (waveMaterial != null)
            {
                Color color = waveMaterial.color;
                color.a = Mathf.Lerp(color.a, 0, progress);
                waveMaterial.color = color;
            }
            
            yield return null;
        }
        
        // Destroy after fade out
        Destroy(gameObject);
    }
    
    // Call this from external scripts to destroy all monsters in range of the current wave
    public void DestroyMonstersInRange()
    {
        float currentRadius = waveTransform.localScale.x;
        
        // Find all monsters within the current radius
        Collider[] colliders = Physics.OverlapSphere(transform.position, currentRadius);
        
        foreach (Collider collider in colliders)
        {
            ShadowMonster monster = collider.GetComponent<ShadowMonster>();
            if (monster != null)
            {
                monster.TakeDamage(float.MaxValue);
            }
        }
    }
}
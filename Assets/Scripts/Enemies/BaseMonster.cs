using UnityEngine;
using System.Collections;
using Core;

public abstract class BaseMonster : MonoBehaviour
{
    protected GameObject player;
    protected HealthComponent healthComponent;

    protected virtual void Awake()
    {
        healthComponent = GetComponent<HealthComponent>();
        if (healthComponent == null)
        {
            healthComponent = gameObject.AddComponent<HealthComponent>();
        }
    }

    protected virtual void Start()
    {
        healthComponent.OnDeath += OnDeathHandler;
        player = GameObject.FindGameObjectWithTag("Player");

        if (player == null)
        {
            Debug.LogError("Player not found. Make sure the player has the 'Player' tag.");
        }
    }

    public virtual void TakeDamage(float damageAmount, Vector3 hitPoint = default, GameObject damageSource = null)
    {
        healthComponent?.TakeDamage(damageAmount, hitPoint, damageSource);
    }

    protected virtual void OnDeathHandler(HealthComponent healthComponent)
    {
        StartCoroutine(DestroyAfterDelay(3f));
    }

    protected virtual IEnumerator DestroyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        float fadeTime = 1f;
        float elapsed = 0;

        Material[] materials = new Material[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].material != null && renderers[i].material.HasProperty("_Color"))
            {
                materials[i] = renderers[i].material;
            }
        }

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

        HandlePostFade();
    }

    protected virtual void HandlePostFade()
    {
        Destroy(gameObject);
    }

    protected virtual void OnDestroy()
    {
        healthComponent.OnDeath -= OnDeathHandler;
    }
}

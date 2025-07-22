using System.Collections;
using UnityEngine;
using Core;

namespace Enemies
{
    public abstract class BaseMonster : MonoBehaviour
    {
        private static readonly int Color1 = Shader.PropertyToID("_Color");

        protected GameObject Player;
        protected HealthComponent HealthComponent;

        protected virtual void Awake()
        {
            HealthComponent = GetComponent<HealthComponent>();
            if (HealthComponent == null)
            {
                Debug.LogWarning($"{name} is missing a HealthComponent. Adding one.");
                HealthComponent = gameObject.AddComponent<HealthComponent>();
            }
        }

        protected virtual void Start()
        {
            if (HealthComponent != null)
            {
                HealthComponent.OnDeath += OnDeathHandler;
            }
            else
            {
                Debug.LogWarning($"{name} has no HealthComponent in BaseMonster.Start()!");
            }

            Player = GameObject.FindGameObjectWithTag("Player");
            if (Player == null)
            {
                Debug.LogError("Player not found. Make sure the player has the 'Player' tag.");
            }
        }

        public virtual void TakeDamage(float damageAmount, Vector3 hitPoint = default, GameObject damageSource = null)
        {
            HealthComponent?.TakeDamage(damageAmount, hitPoint, damageSource);
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
            float elapsed = 0f;

            Material[] materials = new Material[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i].material != null && renderers[i].material.HasProperty(Color1))
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
                    if (materials[i] != null && materials[i].HasProperty(Color1))
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
            if (HealthComponent != null)
            {
                HealthComponent.OnDeath -= OnDeathHandler;
            }
        }
    }
}

using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using Core;

public abstract class BaseMonster : MonoBehaviour
 {
  [SerializeField] protected float maxHealth = 100f;
  protected NavMeshAgent agent;
  protected GameObject player;
  protected HealthComponent healthComponent;

  // Animation hashes
  protected static readonly int Speed = Animator.StringToHash("Speed");
  protected static readonly int Attack = Animator.StringToHash("Attack");
  protected static readonly int IsDeadHash = Animator.StringToHash("IsDead");

  protected virtual void Awake()
  {
   healthComponent = GetComponent<HealthComponent>();
   if (healthComponent == null)
   {
    healthComponent = gameObject.AddComponent<HealthComponent>();
   }

   healthComponent.SetMaxHealth(maxHealth);
   agent = GetComponent<NavMeshAgent>();
  }

  protected virtual void Start()
  {
   // Subscribe to the OnDeath event
   healthComponent.OnDeath += OnDeathHandler;
   player = GameObject.FindGameObjectWithTag("Player");

   if (player == null)
   {
    Debug.LogError("Player not found. Make sure the player has the 'Player' tag.");
   }
  }

  public virtual void TakeDamage(float damageAmount, Vector3 hitPoint = default, GameObject damageSource = null)
  {
   healthComponent?.TakeDamage(damageAmount, hitPoint, damageSource); // Use the HealthComponent's TakeDamage
  }

  protected virtual void OnDeathHandler(HealthComponent healthComponent)
  {
   if (agent != null)
   {
    agent.isStopped = true;
    agent.enabled = false;
   }

   StartCoroutine(DestroyAfterDelay(3f));
  }

  protected virtual IEnumerator DestroyAfterDelay(float delay)
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

   HandlePostFade();
  }

  /// <summary>
  /// Default behavior after fade-out. Can be overridden by subclasses (e.g., pooled enemies).
  /// </summary>
  protected virtual void HandlePostFade()
  {
   Destroy(gameObject);
  }
     protected virtual void OnDestroy()
        {
            healthComponent.OnDeath -= OnDeathHandler;
        }
 }

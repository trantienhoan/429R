using UnityEngine;

namespace Game.Weapons
{
    /// <summary>
    /// A shot that's a ball of smoke (the Elon Boong's, user 2026-10-30: "change the Bullet to a smoke ball, do small explode
    /// damage when hit anything"): the first thing it hits (anything but its gun and the player, see Ignore Shooter) sets off
    /// a small blast there (Blast: what's within Radius is hurt by Damage and pushed, the player never) and the ball is gone.
    /// Flying Max Seconds without hitting anything, it puffs out. Its own body barely hits (its Weapon Power is tiny), so the
    /// blast is the damage. Tools > 429 Game > Weapons > Elon Boong: Smoke Ball Shot sets the Elon bullet up with it.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class SmokeBall : MonoBehaviour
    {
        [Header("Blast")]
        [Tooltip("How far the blast reaches, in metres.")]
        [Min(0.05f)]
        [SerializeField] private float radius = 0.8f;
        [Tooltip("Damage to what's in reach (whole numbers, like a punch: 1, a hard one 2).")]
        [Min(0f)]
        [SerializeField] private float damage = 2f;
        [Tooltip("How hard loose things in reach are pushed away, in metres per second.")]
        [Min(0f)]
        [SerializeField] private float push = 3f;
        [Tooltip("Its effects (e.g. the Elon punch's: Flash_round_yellow, Hit_Blast, Smoke_Flash_Small_Puff).")]
        [SerializeField] private GameObject[] effects = System.Array.Empty<GameObject>();
        [Tooltip("Size of the effects.")]
        [Min(0.01f)]
        [SerializeField] private float effectSize = 0.35f;
        [SerializeField] private AudioClip sound;
        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.5f;

        [Header("Flying")]
        [Tooltip("Seconds it flies before it puffs out on its own (no damage).")]
        [Min(0.1f)]
        [SerializeField] private float maxSeconds = 2.9f;
        [Tooltip("The puff when it goes out on its own, e.g. Smoke_Flash_Small_Puff. Empty: it just goes.")]
        [SerializeField] private GameObject fizzleEffect;
        [Min(0.01f)]
        [SerializeField] private float fizzleSize = 0.25f;

        private Rigidbody body;
        private float bornAt;
        private bool gone;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
        }

        private void OnEnable()
        {
            bornAt = Time.time;
            gone = false;
        }

        private void Update()
        {
            if (gone || Time.time - bornAt < maxSeconds) return;
            gone = true;
            if (fizzleEffect != null) EffectPool.Play(fizzleEffect, transform.position, fizzleSize);
            Destroy(gameObject);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (gone) return;
            gone = true;
            var at = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            // The player's shot: it hurts in full, but never the player (no Player Event).
            Blast.Explode(at, radius, damage, push, body, effects, sound, volume, null, false, effectSize);
            Destroy(gameObject);
        }
    }
}

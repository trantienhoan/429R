using System.Collections.Generic;
using Game.Combat;
using UnityEngine;

namespace Balls
{
    /// <summary>
    /// 429R gate 3 (user 2026-11-23: "the last boss Black Pumpkin Ball only comes out when all other 15 balls are pocketed
    /// ... at least 3 times heavier than other balls, no need to be bigger ... can be broken by Player or push into hole to
    /// kill it"): the health of the Black Pumpkin Ball, on its main ball. The player's hits hurt it the way they hurt the
    /// giant pumpkins (Enemy Hit Damage's one rule: the same damage numbers, hit effects and buzz); the dash does Special
    /// Hit. At 0 it tells Broken (the gate does the rest). A pocket kills it too (the gate sees that).
    /// </summary>
    [DisallowMultipleComponent]
    public class BlackPumpkinBall : MonoBehaviour
    {
        [Tooltip("Its health (the gate sets it).")]
        [Min(1f)] public float health = 150f;
        [Tooltip("One body (a fist, a weapon) hurts it at most this often (seconds).")]
        [Min(0f)] public float hitCooldown = 0.25f;

        /// <summary>Its health left.</summary>
        public float Health => hp;
        /// <summary>Broken by the player.</summary>
        public bool IsBroken => dead;
        /// <summary>The ball it's on.</summary>
        public BallPlayer Owner { get; private set; }
        /// <summary>Its health ran out.</summary>
        public event System.Action<BlackPumpkinBall> Broken;

        private float hp;
        private bool dead;
        private readonly Dictionary<Object, float> nextHitBy = new Dictionary<Object, float>();

        public void Init(BallPlayer owner, float startHealth)
        {
            Owner = owner;
            health = hp = Mathf.Max(1f, startHealth);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (dead) return;
            var hit = EnemyHitDamage.Judge(collision, 1f, null, out bool byPlayer);
            if (!byPlayer || hit.Damage <= 0f) return;
            var key = (Object)collision.rigidbody ?? collision.collider;
            if (key != null && nextHitBy.TryGetValue(key, out float next) && Time.time < next) return;
            if (key != null) nextHitBy[key] = Time.time + hitCooldown;
            var at = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            var settings = HitSettings.Shared;
            if (settings != null)
            {
                if (settings.ShowsEffect(hit, true)) settings.PlayEffect(at, hit, 2f);
                if (hit.IsSmash) settings.PlaySmash(at);
            }
            float damage = Mathf.Max(1f, HitSettings.Whole(hit.Damage));
            HitBuzz.Dealt(collision, damage);
            if (hp - damage <= 0f) HitBuzz.Killed(collision, damage);
            TakeDamage(damage, hit.IsBig);
        }

        /// <summary>The gate's special move (the dash) ran into it.</summary>
        public void SpecialHit(float damage) => TakeDamage(Mathf.Max(1f, HitSettings.Whole(damage)), true);

        private void TakeDamage(float damage, bool big)
        {
            if (dead) return;
            hp -= damage;
            DamageNumbers.Show(transform, damage, big, hp <= 0f);
            Sfx.Play(Sfx.Sound.BallHit, transform.position, 0.9f);
            if (hp > 0f) return;
            hp = 0f;
            dead = true;
            Broken?.Invoke(this);
        }
    }
}

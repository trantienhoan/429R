using Game.Bonus;
using Game.Combat;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// Drops Count random candies (Candies) where this enemy dies, when the player kills it (not a fall, a stage
    /// clean-up or a Live Limit), like the secret room's Giant Spider (user, 2026-11-20: "kill it to drop 3 candies").
    /// </summary>
    [DisallowMultipleComponent]
    public class CandiesOnDeath : MonoBehaviour
    {
        [Min(0)]
        [SerializeField] private int count = 3;
        [Tooltip("Picked at random for each candy.")]
        [SerializeField] private GameObject[] candies = System.Array.Empty<GameObject>();
        [Tooltip("Seconds the candies take to come out.")]
        [Min(0f)]
        [SerializeField] private float seconds = 0.5f;

        private void OnEnable() => EnemyHitDamage.Died += OnDied;
        private void OnDisable() => EnemyHitDamage.Died -= OnDied;

        private void OnDied(EnemyHitDamage enemy)
        {
            if (enemy == null || enemy.gameObject != gameObject || !enemy.KilledByPlayer || enemy.noReward) return;
            if (count <= 0 || candies == null || candies.Length == 0) return;
            CandyFountain.Play(CandyFountain.TopOf(gameObject), count, candies, seconds, new Vector2(2.5f, 4f), new Vector2(0.8f, 2f));
        }
    }
}

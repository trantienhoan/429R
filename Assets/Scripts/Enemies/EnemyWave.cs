using System.Collections.Generic;
using Game.Combat;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// Remembers the enemies each spawner (an EnemyManager FSM) has made in its current wave, so the spawner can keep
    /// only a few of them alive at once and wait until the wave is (almost) beaten before calling its boss. Enemies
    /// from other spawners, breakables or ghosts don't count towards a wave. Used by the PlayMaker actions in the
    /// Enemies category: Start Wave, Add Enemy To Wave, Wait For Wave Room and Wait For Wave Cleared.
    /// </summary>
    public static class EnemyWave
    {
        private static readonly Dictionary<object, List<GameObject>> waves = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            waves.Clear();
        }

        /// <summary>Forgets the spawner's enemies from before: a new wave starts.</summary>
        public static void Start(object spawner)
        {
            waves.Remove(spawner);
        }

        public static void Add(object spawner, GameObject enemy)
        {
            if (spawner == null || enemy == null) return;
            if (!waves.TryGetValue(spawner, out var list)) waves[spawner] = list = new List<GameObject>();
            if (!list.Contains(enemy)) list.Add(enemy);
        }

        /// <summary>How many of the spawner's wave are still alive.</summary>
        public static int Alive(object spawner)
        {
            if (spawner == null || !waves.TryGetValue(spawner, out var list)) return 0;
            list.RemoveAll(enemy => !IsAlive(enemy));
            return list.Count;
        }

        /// <summary>Gone, switched off, or out of health (still playing its death) all count as dead.</summary>
        public static bool IsAlive(GameObject enemy)
        {
            if (enemy == null || !enemy.activeInHierarchy) return false;
            var hits = enemy.GetComponent<EnemyHitDamage>();
            if (hits == null) hits = enemy.GetComponentInChildren<EnemyHitDamage>();
            return hits == null || !hits.IsDead;
        }
    }
}

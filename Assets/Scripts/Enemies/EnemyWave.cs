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

        /// <summary>Counts the enemy in the spawner's wave. Not the rare white bug: it only runs, and the wave doesn't wait for it.</summary>
        public static void Add(object spawner, GameObject enemy)
        {
            if (spawner == null || enemy == null || SpiderBrain.IsAnomalyBug(enemy)) return;
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

        /// <summary>Every enemy of the spawner's wave still alive dies (its Health FSM's health goes to 0), and the wave
        /// is forgotten.</summary>
        public static void KillAll(object spawner)
        {
            if (spawner == null || !waves.TryGetValue(spawner, out var list)) return;
            foreach (var enemy in list.ToArray()) Kill(enemy);
            waves.Remove(spawner);
        }

        /// <summary>Kills one enemy through its Health FSM, so it plays its own death; destroyed if it has none.</summary>
        public static void Kill(GameObject enemy)
        {
            if (!IsAlive(enemy)) return;
            foreach (var fsm in enemy.GetComponentsInChildren<PlayMakerFSM>())
            {
                if (!fsm.FsmName.EndsWith("Health")) continue;
                var health = fsm.FsmVariables.FindFsmFloat("health");
                if (health == null) continue;
                health.Value = 0f;
                return;
            }
            Object.Destroy(enemy);
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

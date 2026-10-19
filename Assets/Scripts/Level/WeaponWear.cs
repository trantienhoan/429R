using UnityEngine;
using FsmFloat = HutongGames.PlayMaker.FsmFloat;

namespace Game.Leveling
{
    /// <summary>
    /// Changes how fast a weapon wears out. Weapons made in PlayMaker lose 1 "health" (their Health FSM) on every hard
    /// hit and break at 0. With the player's Weapon Efficiency (a level-up stat) part of each point is given back, so with
    /// 20% less wear a hit costs 0.8. While Boost is above 1 (2 while it's held in two hands, Two Handed Grip) each hit
    /// costs that many times as much, so it can take more than the FSM did. Big losses (a pop by the Fairy's shield) are
    /// left alone. Player Level puts it on each weapon the player picks up; nothing to set up.
    /// </summary>
    // Before the PlayMaker FSMs, so the weapon's Health FSM never sees the full loss.
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public class WeaponWear : MonoBehaviour
    {
        private const string HealthFsm = "Health";
        private const string HealthVariable = "health";
        // A hard hit costs 1; anything bigger isn't wear.
        private const float MostWear = 1.5f;

        /// <summary>
        /// Extra wear set while the game runs (not saved): each hard hit costs this many times as much, e.g. 2 while the
        /// weapon is held in two hands (Two Handed Grip). 1 = none.
        /// </summary>
        [System.NonSerialized] public float boost = 1f;

        private FsmFloat health;
        private float last;

        /// <summary>Starts looking after the thing if it's a weapon that wears out (and isn't looked after yet).</summary>
        public static void Watch(GameObject thing)
        {
            if (thing == null || thing.TryGetComponent<WeaponWear>(out _)) return;
            if (!thing.CompareTag("Weapon") && !thing.TryGetComponent<Game.Combat.WeaponPower>(out _)) return;

            foreach (var fsm in thing.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName != HealthFsm) continue;
                var value = fsm.FsmVariables.FindFsmFloat(HealthVariable);
                if (value == null) continue;
                var wear = thing.AddComponent<WeaponWear>();
                wear.health = value;
                wear.last = value.Value;
                return;
            }
        }

        private void Update()
        {
            if (health == null)
            {
                enabled = false;
                return;
            }

            float lost = last - health.Value;
            // What a hard hit really costs: less with Weapon Efficiency, more while boosted (then some more is taken).
            if (lost > 0.001f && lost <= MostWear) health.Value += lost * (1f - PlayerLevel.WeaponWearShare * Mathf.Max(0f, boost));
            last = health.Value;
        }
    }
}

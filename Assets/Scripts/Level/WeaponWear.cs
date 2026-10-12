using UnityEngine;
using FsmFloat = HutongGames.PlayMaker.FsmFloat;

namespace Game.Leveling
{
    /// <summary>
    /// Makes a weapon wear out slower with the player's Weapon Efficiency (a level-up stat). Weapons made in PlayMaker
    /// lose 1 "health" (their Health FSM) on every hard hit and break at 0; this gives part of each point back, so with
    /// 20% less wear a hit costs 0.8. Big losses (a pop by the Fairy's shield) are left alone. Player Level puts it on
    /// each weapon the player picks up; nothing to set up.
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
            if (lost > 0.001f && lost <= MostWear) health.Value += lost * (1f - PlayerLevel.WeaponWearShare);
            last = health.Value;
        }
    }
}

using System;
using UnityEngine;
using FsmFloat = HutongGames.PlayMaker.FsmFloat;

namespace Game.Saving
{
    /// <summary>
    /// Lasting boosts to the player's health cap: the extra cap from Boss Seeds (kept in the save here, until it's
    /// reset or lost) and the extra cap from the Max Health level-up stat (kept in the save with the level, see Player
    /// Level, which tells this how much it is). It works on the PlayerHealth FSM's floats: the cap is
    /// DefautPlayerHealth (the health the player starts and restarts with) and the health is PlayerHealth. When the save
    /// is read the bonuses are added to the cap (and to the health, so the player starts full), also into the fresh FSM of
    /// a reloaded scene (Re-Dream); Reset Progress takes them off again.
    /// </summary>
    public static class PlayerBoosts
    {
        private const string HealthFsm = "PlayerHealth";
        private const string HealthVariable = "PlayerHealth";
        private const string CapVariable = "DefautPlayerHealth";

        /// <summary>Raised when a boost changes, so the save is written.</summary>
        public static event Action Changed;

        /// <summary>How much Boss Seeds have raised the health cap in total (saved).</summary>
        public static float HealthCapBonus { get; private set; }

        /// <summary>How much the Max Health level-up stat raises the health cap (saved with the level).</summary>
        public static float LevelHealthBonus { get; private set; }

        // How much of the bonuses the PlayerHealth FSM's cap holds right now, so they're never added twice.
        private static float applied;

        // The PlayerHealth FSM that holds them. A reloaded scene (e.g. Re-Dream) has a new one, back at its start value,
        // so the bonuses go into it in full, once.
        private static PlayMakerFSM appliedTo;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            HealthCapBonus = 0f;
            LevelHealthBonus = 0f;
            applied = 0f;
            appliedTo = null;
            Changed = null;
        }

        /// <summary>Raises the cap for good (saved shortly after), and the health too if heal is on.</summary>
        public static void RaiseHealthCap(float amount, bool heal)
        {
            if (amount <= 0f) return;
            HealthCapBonus += amount;
            Apply(heal);
            Changed?.Invoke();
        }

        /// <summary>
        /// Sets how much the Max Health level-up stat adds to the cap (Player Level calls it, and saves the points
        /// itself). A raise also adds to the health if heal is on.
        /// </summary>
        public static void SetLevelHealthBonus(float amount, bool heal)
        {
            LevelHealthBonus = Mathf.Max(0f, amount);
            Apply(heal);
        }

        /// <summary>The saved bonus, from the Save Manager when the save is read (0 with no save).</summary>
        public static void LoadSaveData(float bonus)
        {
            HealthCapBonus = Mathf.Max(0f, bonus);
            Apply(true);
        }

        /// <summary>The player's health right now (PlayerHealth). False when there's no PlayerHealth FSM.</summary>
        public static bool TryGetHealth(out float value)
        {
            value = 0f;
            if (!Find(out var health, out _)) return false;
            value = health.Value;
            return true;
        }

        /// <summary>Sets the player's health (e.g. back to what it was at Get a Rest). False when there's no PlayerHealth FSM.</summary>
        public static bool SetHealth(float value)
        {
            if (!Find(out var health, out _)) return false;
            health.Value = value;
            return true;
        }

        /// <summary>
        /// Sets the player's health to full: the cap (DefautPlayerHealth, with the bonuses), e.g. a revive. False when
        /// there's no PlayerHealth FSM.
        /// </summary>
        public static bool HealToFull()
        {
            if (!Find(out var health, out var cap)) return false;
            health.Value = cap.Value;
            return true;
        }

        /// <summary>
        /// Sets the player's health to this share of the cap (0.5 = half, 2 = twice the cap: health has no cap), e.g. a
        /// revive with lucky coins. False when there's no PlayerHealth FSM.
        /// </summary>
        public static bool HealToShare(float share)
        {
            if (!Find(out var health, out var cap)) return false;
            health.Value = cap.Value * Mathf.Max(0f, share);
            return true;
        }

        /// <summary>Takes the Boss Seeds' bonus off again (Reset Progress).</summary>
        public static void Reset()
        {
            HealthCapBonus = 0f;
            Apply(false);
        }

        // Moves the FSM's cap to the start value plus the bonuses; a raise also adds to the health if heal is on.
        private static void Apply(bool heal)
        {
            float total = HealthCapBonus + LevelHealthBonus;
            bool found = Find(out var health, out var cap, out var fsm);
            // Not the FSM holding the bonuses (the scene reloaded): its cap has none of them yet.
            if (found && fsm != appliedTo)
            {
                appliedTo = fsm;
                applied = 0f;
            }
            float change = total - applied;
            if (Mathf.Abs(change) < 0.001f) return;
            if (!found)
            {
                Debug.LogWarning($"[PlayerBoosts] No '{HealthFsm}' FSM with '{HealthVariable}' and '{CapVariable}' found, so the health cap bonus ({total}) isn't applied.");
                return;
            }
            cap.Value += change;
            if (heal && change > 0f) health.Value += change;
            applied = total;
        }

        private static bool Find(out FsmFloat health, out FsmFloat cap) => Find(out health, out cap, out _);

        private static bool Find(out FsmFloat health, out FsmFloat cap, out PlayMakerFSM found)
        {
            health = cap = null;
            found = null;
            var player = HutongGames.PlayMaker.FsmVariables.GlobalVariables.FindFsmGameObject("Player");
            if (player != null && player.Value != null)
            {
                foreach (var fsm in player.Value.GetComponents<PlayMakerFSM>())
                    if (fsm.FsmName == HealthFsm) found = fsm;
            }
            if (found == null)
            {
                foreach (var fsm in UnityEngine.Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (fsm.FsmName == HealthFsm) { found = fsm; break; }
            }
            if (found == null) return false;
            health = found.FsmVariables.FindFsmFloat(HealthVariable);
            cap = found.FsmVariables.FindFsmFloat(CapVariable);
            return health != null && cap != null;
        }
    }
}

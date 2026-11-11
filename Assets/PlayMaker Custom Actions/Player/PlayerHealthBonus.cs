using UnityEngine;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Player")]
    [Tooltip("Extra healing for the player as if they ate more candies, and/or a higher health cap. Each candy heals " +
             "Candy Min to Candy Max (whole numbers); above the starting health only Above Share of it counts, rounded " +
             "up, and above 1.5x the starting health only 10% (Player Level's Candy Slow Heal From and Share), like " +
             "candy itself. Works on the PlayerHealth FSM's PlayerHealth and DefautPlayerHealth floats. Put it " +
             "after a Send Event Health1 to the player, which plays the heal effects and already counts as one candy: " +
             "a seed that heals like 7 candies has Extra Candies 6. Each candy heals more with the player's Max Health " +
             "level-up stat, like eaten candy.")]
    public class PlayerHealthBonus : FsmStateAction
    {
        [Tooltip("Candies' worth of healing on top of the Health1 event.")]
        public FsmInt extraCandies;

        [Tooltip("The smallest heal of one candy.")]
        public FsmFloat candyMin;

        [Tooltip("The largest heal of one candy.")]
        public FsmFloat candyMax;

        [HasFloatSlider(0f, 1f)]
        [Tooltip("Share of a candy's heal that counts above the starting health (up to 1.5x it; above that 10%), e.g. 0.5 = " +
                 "50% (user, 2026-11-05: candies above max health heal half) (rounded up).")]
        public FsmFloat aboveShare;

        [Tooltip("Raise the starting health (the health cap) by this much, e.g. 30. It's saved, and lasts until the save is reset or lost.")]
        public FsmFloat raiseCap;

        [Tooltip("Raising the cap also adds the same amount of health, so the heart stays as full as it was.")]
        public FsmBool healTheRaise;

        private const string HealthFsm = "PlayerHealth";
        private const string HealthVariable = "PlayerHealth";
        private const string CapVariable = "DefautPlayerHealth";

        public override void Reset()
        {
            extraCandies = 6;
            candyMin = 1f;
            candyMax = 7f;
            aboveShare = 0.5f;
            raiseCap = 0f;
            healTheRaise = true;
        }

        public override void OnEnter()
        {
            var fsm = FindPlayerHealth();
            var health = fsm != null ? fsm.FsmVariables.FindFsmFloat(HealthVariable) : null;
            var cap = fsm != null ? fsm.FsmVariables.FindFsmFloat(CapVariable) : null;
            if (health == null || cap == null)
            {
                LogWarning($"No '{HealthFsm}' FSM with '{HealthVariable}' and '{CapVariable}' floats found; nothing changed.");
                Finish();
                return;
            }

            // Saved, and put back on the cap whenever the save is read.
            if (raiseCap.Value > 0f) Game.Saving.PlayerBoosts.RaiseHealthCap(raiseCap.Value, healTheRaise.Value);

            float share = Mathf.Clamp01(aboveShare.Value);
            for (int i = 0; i < extraCandies.Value; i++)
            {
                float amount = CandyHeal.Scale(RandomAmount.Roll(candyMin.Value, candyMax.Value, true), true);
                // Up to the cap in full, then the share (rounded up), then above 1.5x the cap only 10% (CandyHeal).
                health.Value += CandyHeal.AboveCap(health.Value, amount, cap.Value, share, true);
            }
            Finish();
        }

        private static PlayMakerFSM FindPlayerHealth()
        {
            var player = FsmVariables.GlobalVariables.FindFsmGameObject("Player");
            if (player != null && player.Value != null)
            {
                foreach (var fsm in player.Value.GetComponents<PlayMakerFSM>())
                    if (fsm.FsmName == HealthFsm) return fsm;
            }
            foreach (var fsm in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                if (fsm.FsmName == HealthFsm) return fsm;
            return null;
        }
    }
}

using UnityEngine;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Player")]
    [Tooltip("Extra healing for the player as if they ate more candies, and/or a higher health cap. Each candy heals " +
             "Candy Min to Candy Max (whole numbers); above the starting health only Above Share of it counts, rounded " +
             "up, like candy itself. Works on the PlayerHealth FSM's PlayerHealth and DefautPlayerHealth floats. Put it " +
             "after a Send Event Health1 to the player, which plays the heal effects and already counts as one candy: " +
             "a seed that heals like 7 candies has Extra Candies 6.")]
    public class PlayerHealthBonus : FsmStateAction
    {
        [Tooltip("Candies' worth of healing on top of the Health1 event.")]
        public FsmInt extraCandies;

        [Tooltip("The smallest heal of one candy.")]
        public FsmFloat candyMin;

        [Tooltip("The largest heal of one candy.")]
        public FsmFloat candyMax;

        [HasFloatSlider(0f, 1f)]
        [Tooltip("Share of a candy's heal that counts above the starting health, e.g. 0.7 = 70% (rounded up).")]
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
            aboveShare = 0.7f;
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
                float amount = RandomAmount.Roll(candyMin.Value, candyMax.Value, true);
                float full = Mathf.Clamp(cap.Value - health.Value, 0f, amount);       // up to the cap: in full
                float over = Mathf.Ceil((amount - full) * share - 0.0001f);             // above it: the share, rounded up
                health.Value += full + over;
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

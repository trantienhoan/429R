using UnityEngine;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory(ActionCategory.Math)]
    [Tooltip("Adds a random amount between Min and Max to a Float Variable, e.g. a heal of 1 to 7. With Candy Heal " +
             "Bonus ticked (eating candy), the amount grows with the player's Max Health level-up stat.")]
    public class FloatAddRandom : FsmStateAction
    {
        [RequiredField]
        [UIHint(UIHint.Variable)]
        [Tooltip("The Float Variable to add to.")]
        public FsmFloat floatVariable;

        [RequiredField]
        [Tooltip("The smallest amount to add.")]
        public FsmFloat min;

        [RequiredField]
        [Tooltip("The largest amount to add.")]
        public FsmFloat max;

        [Tooltip("Only whole numbers (1, 2, 3 ...), Min and Max included. Off: any amount in between, like 2.37.")]
        public FsmBool wholeNumbers;

        [Tooltip("Optional: the variable never goes above this, e.g. DefautPlayerHealth.")]
        public FsmFloat maxValue;

        [Tooltip("Optional: above this, e.g. DefautPlayerHealth, only Above Share of the amount is added. Up to it the " +
                 "amount counts in full, so from 118 a roll of 7 adds 2 + 70% of 5.")]
        public FsmFloat reducedAbove = new FsmFloat { UseVariable = true };

        [HasFloatSlider(0f, 1f)]
        [Tooltip("The share of the amount that's added above Reduced Above, e.g. 0.7 = 70%. With Whole Numbers it's " +
                 "rounded up: 1.3 becomes 2, and a roll of 1 still adds 1.")]
        public FsmFloat aboveShare = 0.7f;

        [UIHint(UIHint.Variable)]
        [Tooltip("Optional: store the amount that was actually added.")]
        public FsmFloat storeAmount;

        [Tooltip("Tick for eating candy (the player's heal): the amount grows with the player's Max Health (a level-up " +
                 "stat, Candy Heal Per Point), and is 1.5x while the player is high on a boong.")]
        public FsmBool candyHealBonus;

        public override void Reset()
        {
            floatVariable = null;
            min = 1f;
            max = 7f;
            wholeNumbers = true;
            maxValue = new FsmFloat { UseVariable = true };
            reducedAbove = new FsmFloat { UseVariable = true };
            aboveShare = 0.7f;
            storeAmount = null;
            candyHealBonus = false;
        }

        public override void OnEnter()
        {
            float amount = RandomAmount.Roll(min.Value, max.Value, wholeNumbers.Value);
            // Added after FSMs already used this action, so it can be missing (unticked) in them.
            if (candyHealBonus != null && candyHealBonus.Value && amount > 0f)
                amount = CandyHeal.Scale(amount, wholeNumbers.Value);
            float start = floatVariable.Value;
            if (reducedAbove != null && !reducedAbove.IsNone && amount > 0f)
            {
                float full = Mathf.Clamp(reducedAbove.Value - start, 0f, amount);   // up to the cap: in full
                float over = (amount - full) * Mathf.Clamp01(aboveShare != null ? aboveShare.Value : 1f);
                // A hair under, so 10 x 0.7 (7.0000001 in floats) stays 7.
                if (wholeNumbers.Value) over = Mathf.Ceil(over - 0.0001f);
                amount = full + over;
            }
            float result = start + amount;
            if (!maxValue.IsNone) result = Mathf.Min(result, maxValue.Value);

            floatVariable.Value = result;
            if (!storeAmount.IsNone) storeAmount.Value = result - start;
            Finish();
        }
    }

    /// <summary>
    /// A candy's heal with the player's Max Health bonus (and 1.5x while high on a boong), shared by Float Add Random and
    /// Player Health Bonus.
    /// </summary>
    internal static class CandyHeal
    {
        public static float Scale(float amount, bool wholeNumbers)
        {
            amount *= Game.Leveling.PlayerLevel.CandyHealScale * Game.Leveling.PlayerBuffs.HealScale;
            // Rounded up or down at random by how far it is to the next whole number, so even +5% counts: a 4 at +5%
            // (4.2) heals 5 one time in five.
            return wholeNumbers ? Mathf.Floor(amount + Random.value) : amount;
        }
    }

    /// <summary>The random roll shared by Float Add Random and Float Subtract Random.</summary>
    internal static class RandomAmount
    {
        public static float Roll(float min, float max, bool wholeNumbers)
        {
            if (max < min) (min, max) = (max, min);
            if (wholeNumbers)
            {
                int low = Mathf.CeilToInt(min), high = Mathf.FloorToInt(max);
                if (low <= high) return Random.Range(low, high + 1);
            }
            return Random.Range(min, max);
        }
    }
}

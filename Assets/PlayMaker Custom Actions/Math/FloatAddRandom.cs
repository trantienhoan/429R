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
                 "amount counts in full, so from 118 a roll of 7 adds 2 + 50% of 5. Above Player Level's Candy Slow Heal " +
                 "From times this (1.5x) only its Candy Slow Heal Share (10%) is added (user, 2026-11-10).")]
        public FsmFloat reducedAbove = new FsmFloat { UseVariable = true };

        [HasFloatSlider(0f, 1f)]
        [Tooltip("The share of the amount that's added above Reduced Above (up to 1.5x it), e.g. 0.5 = 50% (user, " +
                 "2026-11-05: candies above max health heal half). With Whole Numbers it's rounded up: 1.3 becomes 2, " +
                 "and a roll of 1 still adds 1.")]
        public FsmFloat aboveShare = 0.5f;

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
            aboveShare = 0.5f;
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
                amount = CandyHeal.AboveCap(start, amount, reducedAbove.Value, aboveShare != null ? aboveShare.Value : 1f, wholeNumbers.Value);
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

        /// <summary>
        /// What a heal of this amount really adds to health at Start, with this cap (max health), in three bands (user,
        /// 2026-11-10: "+50% above max, +10% above 1.5x max"): up to the cap in full; from the cap to Player Level's Candy
        /// Slow Heal From x the cap (1.5x), Above Share of it (rounded up with whole numbers, so a 1 still heals 1);
        /// above that only Candy Slow Heal Share (10%; rounded at random with whole numbers, so it really averages 10%).
        /// A heal that crosses a line is split across the bands.
        /// </summary>
        public static float AboveCap(float start, float amount, float cap, float aboveShare, bool wholeNumbers)
        {
            if (amount <= 0f) return amount;
            aboveShare = Mathf.Clamp01(aboveShare);
            float full = Mathf.Clamp(cap - start, 0f, amount);   // up to the cap: in full
            float rest = amount - full;
            if (rest <= 0f) return full;
            // Health left between the cap (or where it is now) and the slow line; each point of the heal fills Above
            // Share of it (none at a share of 0, so the heal never gets past the cap then).
            float room = Mathf.Max(0f, cap * Game.Leveling.PlayerLevel.CandySlowHealFrom - Mathf.Max(start, cap));
            float half = aboveShare > 0f ? Mathf.Min(rest, room / aboveShare) : rest;
            float over = half * aboveShare;
            // A hair under, so 10 x 0.7 (7.0000001 in floats) stays 7.
            if (wholeNumbers) over = Mathf.Ceil(over - 0.0001f);
            float slow = (rest - half) * Game.Leveling.PlayerLevel.CandySlowHealShare;
            // Rounded up or down at random by how far it is to the next whole number (0.4: 1 four times in ten, never
            // more), so it really averages Candy Slow Heal Share.
            if (wholeNumbers)
            {
                float whole = Mathf.Floor(slow);
                slow = whole + (Random.value < slow - whole ? 1f : 0f);
            }
            return full + over + slow;
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

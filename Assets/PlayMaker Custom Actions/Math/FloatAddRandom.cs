using UnityEngine;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory(ActionCategory.Math)]
    [Tooltip("Adds a random amount between Min and Max to a Float Variable, e.g. a heal of 1 to 7.")]
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

        [UIHint(UIHint.Variable)]
        [Tooltip("Optional: store the amount that was rolled.")]
        public FsmFloat storeAmount;

        public override void Reset()
        {
            floatVariable = null;
            min = 1f;
            max = 7f;
            wholeNumbers = true;
            maxValue = new FsmFloat { UseVariable = true };
            storeAmount = null;
        }

        public override void OnEnter()
        {
            float amount = RandomAmount.Roll(min.Value, max.Value, wholeNumbers.Value);
            float result = floatVariable.Value + amount;
            if (!maxValue.IsNone) result = Mathf.Min(result, maxValue.Value);

            floatVariable.Value = result;
            if (!storeAmount.IsNone) storeAmount.Value = amount;
            Finish();
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

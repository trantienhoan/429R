namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory(ActionCategory.Math)]
    [Tooltip("Subtracts a random amount between Min and Max from a Float Variable, e.g. damage of 1 to 3.")]
    public class FloatSubtractRandom : FsmStateAction
    {
        [RequiredField]
        [UIHint(UIHint.Variable)]
        [Tooltip("The Float Variable to subtract from.")]
        public FsmFloat floatVariable;

        [RequiredField]
        [Tooltip("The smallest amount to subtract.")]
        public FsmFloat min;

        [RequiredField]
        [Tooltip("The largest amount to subtract.")]
        public FsmFloat max;

        [Tooltip("Only whole numbers (1, 2, 3 ...), Min and Max included. Off: any amount in between, like 2.37.")]
        public FsmBool wholeNumbers;

        [UIHint(UIHint.Variable)]
        [Tooltip("Optional: store the amount that was rolled.")]
        public FsmFloat storeAmount;

        public override void Reset()
        {
            floatVariable = null;
            min = 1f;
            max = 3f;
            wholeNumbers = true;
            storeAmount = null;
        }

        public override void OnEnter()
        {
            float amount = RandomAmount.Roll(min.Value, max.Value, wholeNumbers.Value);
            floatVariable.Value -= amount;
            if (!storeAmount.IsNone) storeAmount.Value = amount;
            Finish();
        }
    }
}

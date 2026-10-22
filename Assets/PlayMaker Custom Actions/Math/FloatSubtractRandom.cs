namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory(ActionCategory.Math)]
    [Tooltip("Subtracts a random amount between Min and Max from a Float Variable, e.g. damage of 1 to 3. With Player " +
             "Toughness ticked (damage to the player), the amount is cut by the player's Toughness level-up stat.")]
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

        [Tooltip("Tick for damage to the player: the amount is cut by the player's Toughness (a level-up stat), and is " +
                 "1.5x while the player is high on a boong; never below 1.")]
        public FsmBool playerToughness;

        public override void Reset()
        {
            floatVariable = null;
            min = 1f;
            max = 3f;
            wholeNumbers = true;
            storeAmount = null;
            playerToughness = false;
        }

        public override void OnEnter()
        {
            float amount = RandomAmount.Roll(min.Value, max.Value, wholeNumbers.Value);
            // Added after FSMs already used this action, so it can be missing (unticked) in them.
            if (playerToughness != null && playerToughness.Value && amount > 0f)
            {
                amount *= Game.Leveling.PlayerLevel.DamageShare * Game.Leveling.PlayerBuffs.DamageTakenScale;
                if (wholeNumbers.Value) amount = UnityEngine.Mathf.Round(amount);
                amount = UnityEngine.Mathf.Max(amount, 1f);
            }
            floatVariable.Value -= amount;
            if (!storeAmount.IsNone) storeAmount.Value = amount;
            Finish();
        }
    }
}

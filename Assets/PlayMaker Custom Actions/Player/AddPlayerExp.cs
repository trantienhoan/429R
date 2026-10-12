namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Player")]
    [Tooltip("Gives the player EXP towards their next level (Player Level), e.g. for beating something made in PlayMaker. " +
             "Enemies with Enemy Hit Damage give their own EXP when they die, so they don't need this.")]
    public class AddPlayerExp : FsmStateAction
    {
        [RequiredField]
        [Tooltip("How much EXP: 1 for a small enemy, 2 for a normal one, 5 for a mini boss, 50 for the boss.")]
        public FsmInt amount;

        public override void Reset()
        {
            amount = 1;
        }

        public override void OnEnter()
        {
            Game.Leveling.PlayerLevel.AddExp(amount.Value);
            Finish();
        }
    }
}

using Game.Steam;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Steam")]
    [Tooltip("Adds to a Steam stat, e.g. +1 to BUGS_SQUASHED each time a bug dies. An achievement that uses the stat as its " +
             "progress (set on the Steamworks site) unlocks by itself when the stat reaches its number. " +
             "Does nothing on Quest or when Steam isn't running.")]
    public class SteamAddToStat : FsmStateAction
    {
        [RequiredField]
        [Tooltip("The stat's API name, exactly as on the Steamworks site. It must be an INT stat.")]
        public FsmString statName;

        [Tooltip("How much to add. A negative number subtracts.")]
        public FsmInt amount;

        public override void Reset()
        {
            statName = "";
            amount = 1;
        }

        public override void OnEnter()
        {
            SteamAchievements.AddToStat(statName.Value, amount.Value);
            Finish();
        }
    }
}

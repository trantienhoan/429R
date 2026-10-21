using Game.Bonus;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Bonus")]
    [Tooltip("Put it in a stage's enemy-wave state, the one that waits for the Yellow Seed. It notes the player's Yellow " +
             "Seeds when the state starts and, when the state ends (the seed was taken), whether any enemy was still " +
             "alive. Play Bonus Round, later, uses that: the bonus round is for a player who killed every enemy before " +
             "taking the seed (a seed that timed out doesn't count).")]
    public class WatchBonusRound : FsmStateAction
    {
        public override void OnEnter()
        {
            BonusRound.NoteStageStart();
        }

        public override void OnExit()
        {
            BonusRound.NoteSeedTaken();
        }
    }
}

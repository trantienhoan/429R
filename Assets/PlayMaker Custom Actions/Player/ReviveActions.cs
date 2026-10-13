using Game.Combat;
using Game.Saving;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Player")]
    [Tooltip("The player just died: with a revive left this run (and a Player Revive on the XR Origin) it uses one up " +
             "and sends Revive; otherwise Game Over, and the game over goes on as before.")]
    public class TryRevive : FsmStateAction
    {
        [RequiredField]
        [Tooltip("Sent when the player gets back up (a revive was used up).")]
        public FsmEvent revive;

        [Tooltip("Sent when there's no revive left: the game is over.")]
        public FsmEvent gameOver;

        public override void Reset()
        {
            revive = null;
            gameOver = FsmEvent.Finished;
        }

        public override void OnEnter()
        {
            Finish();
            if (PlayerRevive.Instance != null && Checkpoints.UseRevive()) Fsm.Event(revive);
            else Fsm.Event(gameOver);
        }
    }

    [ActionCategory("Player")]
    [Tooltip("Gets the player back up where they fell: Player Revive on the XR Origin plays \"Not yet\", the death fall " +
             "backwards, full health and the wake-up aura. Finishes once the player can be hurt again.")]
    public class RevivePlayer : FsmStateAction
    {
        public override void OnEnter()
        {
            var reviver = PlayerRevive.Instance;
            if (reviver == null)
            {
                Finish();
                return;
            }
            reviver.Revive();
        }

        public override void OnUpdate()
        {
            var reviver = PlayerRevive.Instance;
            if (reviver == null || !reviver.IsReviving) Finish();
        }
    }
}

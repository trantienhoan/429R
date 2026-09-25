using Game.Saving;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Save")]
    [Tooltip("Saves the game now: what the player carries (e.g. candies), collected/spent totals and shop purchases. " +
             "The game also saves by itself after changes, on pause and on quit, so this is for checkpoints.")]
    public class SaveGame : FsmStateAction
    {
        [Tooltip("Sent if the game couldn't be saved (no Save Manager in the scene, or the file couldn't be written).")]
        public FsmEvent failedEvent;

        public override void Reset()
        {
            failedEvent = null;
        }

        public override void OnEnter()
        {
            var manager = SaveManager.Instance;
            if (manager == null) LogWarning("There is no SaveManager in the scene. Run Tools > 429 Game > Save > Set Up Save Manager.");
            if (manager == null || !manager.SaveNow()) Fsm.Event(failedEvent);
            Finish();
        }
    }
}

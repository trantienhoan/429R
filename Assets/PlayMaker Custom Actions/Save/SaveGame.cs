using Game.Saving;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Save")]
    [Tooltip("Saves the game now, into the save slot the player picked: what they carry (e.g. candies), collected/spent " +
             "totals, shop purchases, level and checkpoint. The game also saves by itself at every checkpoint (Set " +
             "Checkpoint), after changes, on pause and on quit, so this is rarely needed. Fails before a slot is picked.")]
    public class SaveGame : FsmStateAction
    {
        [Tooltip("Sent if the game couldn't be saved (no Save Manager in the scene, no save slot picked yet, or the file " +
                 "couldn't be written).")]
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

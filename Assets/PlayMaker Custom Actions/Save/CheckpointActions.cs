using Game.Saving;
using UnityEngine;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Save")]
    [Tooltip("The player reached a checkpoint, e.g. \"CP Bedroom\": the game saves at once (a small \"Saved\" sign shows), " +
             "and starting this save slot again (e.g. after Get a Rest) goes on from here.")]
    public class SetCheckpoint : FsmStateAction
    {
        [RequiredField]
        [Tooltip("The checkpoint's name. Go To Checkpoint sends it as an event, so the state that picks where to go " +
                 "needs a transition with this exact name.")]
        public FsmString checkpoint;

        public override void Reset()
        {
            checkpoint = "CP Bedroom";
        }

        public override void OnEnter()
        {
            Checkpoints.Reach(checkpoint.Value);
            Finish();
        }
    }

    [ActionCategory("Save")]
    [Tooltip("Picks where the game goes on from: sends the checkpoint's name as an event (e.g. \"CP Bedroom\"), or Start " +
             "Over when there is none. Not after dying (the game starting): it goes to the save slot's last checkpoint. " +
             "After Dying (the game is over: Try Revive had no revive left): it starts a new run and goes to Start Over. " +
             "Going to a checkpoint puts the player at Player Spot first. Waits until the player has picked a save slot " +
             "and it has been read (the start menu), so this is also where the game waits at the start.")]
    public class GoToCheckpoint : FsmStateAction
    {
        [Tooltip("Ticked: the game is over (the player died). Unticked: the game is starting (goes on where they quit).")]
        public FsmBool afterDying;

        [Tooltip("Sent when there's no checkpoint to go to: start the game over.")]
        public FsmEvent startOver;

        [Tooltip("Ticked: leave the player where they are. Unticked: put them at Player Spot before going to the checkpoint.")]
        public FsmBool stayPut;

        [Tooltip("Where the player stands when the game goes on from a checkpoint, in the world (0,0,0: the middle of " +
                 "the rooms). They keep facing the way they face.")]
        public FsmVector3 playerSpot;

        public override void Reset()
        {
            afterDying = true;
            startOver = FsmEvent.Finished;
            stayPut = false;
            playerSpot = Vector3.zero;
        }

        public override void OnEnter()
        {
            TryGo();
        }

        public override void OnUpdate()
        {
            TryGo();
        }

        private void TryGo()
        {
            // The save is read once a save slot is picked (Save Slot Menu); with no Save Manager there's no checkpoint
            // to wait for.
            if (!Checkpoints.IsLoaded && SaveManager.Instance != null) return;

            // Resume also tells the loading screen the game has gone on (the player is placed below, in this same call).
            string target = afterDying.Value ? Checkpoints.Die() : Checkpoints.Resume();
            Finish();
            if (string.IsNullOrEmpty(target))
            {
                Fsm.Event(startOver);
                return;
            }
            if (stayPut == null || !stayPut.Value) Checkpoints.PlacePlayer(playerSpot != null && !playerSpot.IsNone ? playerSpot.Value : Vector3.zero);
            Fsm.Event(FsmEvent.GetFsmEvent(target));
        }
    }

    [ActionCategory("Save")]
    [Tooltip("Starts a new run: no checkpoint, all revives again. E.g. when the game is beaten and starts over.")]
    public class StartNewRun : FsmStateAction
    {
        public override void OnEnter()
        {
            Checkpoints.StartNewRun();
            Finish();
        }
    }

    [ActionCategory("Save")]
    [Tooltip("Gives the player more revives for good: every run from now on they can die this many more times and " +
             "get back up (and this run too). E.g. from a special chest. Saved.")]
    public class AddExtraRevive : FsmStateAction
    {
        [RequiredField]
        public FsmInt amount;

        public override void Reset()
        {
            amount = 1;
        }

        public override void OnEnter()
        {
            Checkpoints.AddExtraRevive(amount.Value);
            Finish();
        }
    }
}

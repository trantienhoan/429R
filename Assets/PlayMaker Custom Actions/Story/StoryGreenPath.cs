using Game.Story;
using UnityEngine;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Story")]
    [Tooltip("The green light path (user 2026-11-27). To Exit Zone (a room's way on, the state right after the seed piece " +
             "is taken): a glowing green path from the player's feet to a teleport zone at Exit Position; standing in it, " +
             "everything goes dark but the path, a poof, a teleport tube runs round them and they are moved to Centre; " +
             "then Finish Event (empty: FINISHED) and the room goes and the next one comes as before. Lead To Target (the " +
             "start of the game): the path leads from their feet up to Target (the Fairy) until the state ends; it never " +
             "finishes by itself. Off the path for a while, it draws again from their feet. Settings and looks: " +
             "Resources/Green Path Settings (Tools > 429 Game > Story > Set Up Green Path).")]
    public class StoryGreenPath : FsmStateAction
    {
        public enum Mode
        {
            ToExitZone,
            LeadToTarget,
        }

        [Tooltip("To Exit Zone: a path to the teleport zone, which moves the player to Centre. Lead To Target: a path up to " +
                 "Target, no zone.")]
        public Mode mode;

        [Tooltip("To Exit Zone: where the zone is, in the world (the rooms sit at the world's middle). It moves to the " +
                 "nearest walkable floor within the settings' Exit Snap, so furniture never hides it.")]
        public FsmVector3 exitPosition;

        [Tooltip("Optional: an object to go to instead of Exit Position (Lead To Target: who to lead to, e.g. Fairy_Pos).")]
        public FsmGameObject target;

        [Tooltip("To Exit Zone: where the zone sends the player (the room's middle, on its floor).")]
        public FsmVector3 centre;

        [Tooltip("To Exit Zone: sent once the player stands in the middle (empty: FINISHED).")]
        public FsmEvent finishEvent;

        private GreenPathRun run;

        public override void Reset()
        {
            mode = Mode.ToExitZone;
            exitPosition = new FsmVector3 { Value = new Vector3(3f, 0f, 0f) };
            target = null;
            centre = new FsmVector3 { Value = Vector3.zero };
            finishEvent = null;
        }

        public override void OnEnter()
        {
            var to = target != null && target.Value != null ? target.Value.transform : null;
            var spot = to != null ? to.position : exitPosition.IsNone ? Vector3.zero : exitPosition.Value;
            var middle = centre.IsNone ? Vector3.zero : centre.Value;
            run = GreenPathRun.Begin(mode == Mode.LeadToTarget ? GreenPathRun.Kind.Lead : GreenPathRun.Kind.ExitZone, spot, middle, to);
        }

        public override void OnUpdate()
        {
            if (run == null || !run.Done || mode == Mode.LeadToTarget) return;
            run = null;
            if (finishEvent != null) Fsm.Event(finishEvent);
            else Finish();
        }

        public override void OnExit()
        {
            if (run == null) return;
            run.Stop();
            run = null;
        }
    }
}

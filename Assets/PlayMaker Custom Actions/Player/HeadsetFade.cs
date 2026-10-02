using Fader = Game.Locomotion.HeadsetFade;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Player")]
    [Tooltip("Fades the headset view out to a colour (black unless you pick one) or back in, e.g. to hide clearing the " +
             "map and moving the player. Unlike Camera Fade Out, it shows inside the headset. Uses the Headset Fade on " +
             "the Main Camera (Tools > 429 Game > Player > Set Up Death Fall makes it). Works while the game is paused.")]
    public class HeadsetFade : FsmStateAction
    {
        public enum Direction
        {
            Out,
            In,
        }

        [Tooltip("Out: fade to the colour. In: fade back to the game.")]
        public Direction fade;

        [RequiredField]
        [Tooltip("How long the fade takes, in seconds.")]
        public FsmFloat time;

        [Tooltip("Optional: the colour to fade to. None keeps the fade's own colour (black).")]
        public FsmColor color;

        [Tooltip("On: the action finishes when the fade is done. Off: it finishes at once and the fade carries on.")]
        public bool waitToFinish;

        [Tooltip("Optional: event sent when the action finishes.")]
        public FsmEvent finishEvent;

        private Fader fader;

        public override void Reset()
        {
            fade = Direction.Out;
            time = 1f;
            color = new FsmColor { UseVariable = true };
            waitToFinish = true;
            finishEvent = null;
        }

        public override void OnEnter()
        {
            fader = Fader.Find();
            if (fader == null)
            {
                LogWarning("There's no Headset Fade on the Main Camera. Tools > 429 Game > Player > Set Up Death Fall makes it.");
                Done();
                return;
            }

            if (!color.IsNone) fader.Color = color.Value;
            if (fade == Direction.Out) fader.FadeOut(time.Value);
            else fader.FadeIn(time.Value);

            if (!waitToFinish) Done();
        }

        public override void OnUpdate()
        {
            if (fader == null || !fader.IsFading) Done();
        }

        private void Done()
        {
            Finish();
            Fsm.Event(finishEvent);
        }
    }
}

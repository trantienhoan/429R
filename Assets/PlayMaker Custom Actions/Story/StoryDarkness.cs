using Game.Story;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Story")]
    [Tooltip("The story's dark walk: Begin Dark fades the world dark (ambient, sky, every light, candle flames) and gives " +
             "the weak Fairy a green light of her own, so she is the only light to walk to: only on the first meeting " +
             "(Story Start); on later runs it starts dark and the rooftop path lights up slowly over Light Up Seconds. End " +
             "Dark fades it all back (\"the floor starts to show up\"). Settings: Resources/Story Dark Walk Settings. " +
             "Finishes at once (the fade goes on by itself).")]
    public class StoryDarkness : FsmStateAction
    {
        public enum Mode
        {
            BeginDark,
            EndDark,
        }

        [Tooltip("Begin Dark (the start of the walk) or End Dark (the Fairy reached).")]
        public Mode mode;

        [Tooltip("Seconds the fade takes.")]
        public FsmFloat fadeSeconds;

        [Tooltip("Begin Dark on a later run (the Fairy met before): seconds the rooftop path takes to light up.")]
        public FsmFloat lightUpSeconds;

        public override void Reset()
        {
            mode = Mode.BeginDark;
            fadeSeconds = 1.2f;
            lightUpSeconds = 6f;
        }

        public override void OnEnter()
        {
            if (mode == Mode.EndDark) StoryDarkWalk.End(fadeSeconds.Value);
            else if (StoryStart.Current == StoryStart.Mode.FirstMeeting) StoryDarkWalk.Begin(fadeSeconds.Value);
            else StoryDarkWalk.LightUp(lightUpSeconds == null || lightUpSeconds.IsNone || lightUpSeconds.Value <= 0f ? 6f : lightUpSeconds.Value);
            Finish();
        }
    }
}

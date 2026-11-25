using Game.Story;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Story")]
    [Tooltip("Remembers a story step in the save slot for good (a save unlock): Met The Fairy (Story_Met_Fairy) or Beat " +
             "The Boss (Story_Boss_Beaten, in GAMESTAGES \"Thank For Playing\"). The start rooftop goes by them (Story Start).")]
    public class StoryRemember : FsmStateAction
    {
        public enum What
        {
            MetTheFairy,
            BeatTheBoss,
        }

        [Tooltip("What to remember.")]
        public What what;

        public override void Reset()
        {
            what = What.BeatTheBoss;
        }

        public override void OnEnter()
        {
            StoryStart.Remember(what == What.BeatTheBoss ? StoryStart.BossBeaten : StoryStart.MetFairy);
            Finish();
        }
    }
}

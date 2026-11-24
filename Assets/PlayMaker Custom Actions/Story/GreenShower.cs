using Game.Story;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Story")]
    [Tooltip("The girl's green shower: green sparkles rain round the player for a few seconds (the story's ending; " +
             "look, length and width in Resources/Story Seed Settings). Finishes at once.")]
    public class GreenShower : FsmStateAction
    {
        public override void OnEnter()
        {
            StoryShower.Play(StorySeedSettings.Instance);
            Finish();
        }
    }
}

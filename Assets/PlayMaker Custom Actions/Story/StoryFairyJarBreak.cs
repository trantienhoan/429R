using Game.Story;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Story")]
    [Tooltip("The final boss is beaten: the Fairy's jar by the arena (Story Fairy Jar, on Boss_Area) breaks: her purple " +
             "lock breaks, the jar shatters and she flashes away, then waits in the middle of the arena as before. First " +
             "in GAMESTAGES \"wait\". Nothing happens if there is no jar.")]
    public class StoryFairyJarBreak : FsmStateAction
    {
        public override void OnEnter()
        {
            StoryFairyJar.BreakAll();
            Finish();
        }
    }
}

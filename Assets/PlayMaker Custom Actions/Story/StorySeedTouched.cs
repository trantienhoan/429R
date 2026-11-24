using Game.Story;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Story")]
    [Tooltip("The player took a story seed: on the main game the bright light fills the view and the girl's voice plays " +
             "until the next room starts (colours, voice and timing in Resources/Story Seed Settings). In a gate it does " +
             "nothing. Needs a Story Seed on the Game Object.")]
    public class StorySeedTouched : FsmStateAction
    {
        [RequiredField]
        [Tooltip("The seed, with its Story Seed.")]
        public FsmOwnerDefault gameObject;

        public override void Reset()
        {
            gameObject = null;
        }

        public override void OnEnter()
        {
            var go = Fsm.GetOwnerDefaultTarget(gameObject);
            var seed = go != null ? go.GetComponent<StorySeed>() : null;
            if (seed != null) seed.Touched();
            Finish();
        }
    }
}

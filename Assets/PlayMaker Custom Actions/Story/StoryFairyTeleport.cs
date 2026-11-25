using Game.Story;
using UnityEngine;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Story")]
    [Tooltip("The Fairy teleports away (user 2026-11-25): her teleport move (0.3 s), then the green flash, its light and " +
             "her teleport sound, and she is hidden (Story Fairy Acts, Resources/Story Fairy Settings). Put it before her " +
             "Destroy Self and give that a delay of about 0.5 s so the move shows. Finishes at once.")]
    public class StoryFairyTeleport : FsmStateAction
    {
        [Tooltip("The Fairy (the owner by default).")]
        public FsmOwnerDefault gameObject;

        public override void Reset()
        {
            gameObject = null;
        }

        public override void OnEnter()
        {
            var go = Fsm.GetOwnerDefaultTarget(gameObject);
            if (go != null)
            {
                var acts = go.GetComponent<StoryFairyActs>();
                if (acts == null) acts = go.AddComponent<StoryFairyActs>();
                acts.StartTeleport();
            }
            Finish();
        }
    }
}

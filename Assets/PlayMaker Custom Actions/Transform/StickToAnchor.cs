using UnityEngine;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory(ActionCategory.Transform)]
    [Tooltip("Makes an object move and turn with another one (its anchor) without becoming its child, so it keeps its " +
             "own size: e.g. a growing pumpkin with the tree branch it grows on, as the tree shakes and jiggles. With " +
             "Destroy With Anchor ticked it goes when the anchor goes, and as soon as the anchor's (or its parent's) " +
             "Health FSM health reaches 0, e.g. pumpkins still growing when their tree dies.")]
    public class StickToAnchor : FsmStateAction
    {
        [RequiredField]
        [Tooltip("The object to stick on, e.g. the pumpkin Create Object just made (its Store Object variable).")]
        public FsmOwnerDefault gameObject;

        [RequiredField]
        [Tooltip("What it sticks to, e.g. the branch slot it was made at.")]
        public FsmGameObject anchor;

        [Tooltip("Destroy it with the anchor (or when the anchor's Health FSM reaches 0).")]
        public FsmBool destroyWithAnchor;

        public override void Reset()
        {
            gameObject = null;
            anchor = null;
            destroyWithAnchor = true;
        }

        public override void OnEnter()
        {
            var go = Fsm.GetOwnerDefaultTarget(gameObject);
            var target = anchor.Value;
            if (go != null && target != null)
            {
                if (!go.TryGetComponent<Game.FollowAnchor>(out var follow)) follow = go.AddComponent<Game.FollowAnchor>();
                follow.Attach(target.transform, destroyWithAnchor.Value);
            }
            Finish();
        }
    }
}

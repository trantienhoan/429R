using Game.VFX;
using UnityEngine;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory(ActionCategory.Tween)]
    [Tooltip("Makes an object jiggle like jelly: it squashes, springs back up and wobbles away from the player (or from " +
             "Away From), e.g. when it's hit. Uses the object's Jiggle component, which sets how big and how long; one " +
             "with the default settings is added if it has none. Finishes at once, the jiggle plays on by itself.")]
    public class JiggleObject : FsmStateAction
    {
        [RequiredField]
        [Tooltip("What jiggles.")]
        public FsmOwnerDefault gameObject;

        [Tooltip("1 = a full jiggle (the Jiggle component's Squash and Tilt), 0.3 = a little one.")]
        public FsmFloat strength;

        [Tooltip("Optional: it rocks away from this, e.g. what hit it. Empty: away from the player.")]
        public FsmGameObject awayFrom;

        public override void Reset()
        {
            gameObject = null;
            strength = 1f;
            awayFrom = new FsmGameObject { UseVariable = true };
        }

        public override void OnEnter()
        {
            var go = Fsm.GetOwnerDefaultTarget(gameObject);
            if (go != null)
            {
                if (!go.TryGetComponent<Jiggle>(out var jiggle)) jiggle = go.AddComponent<Jiggle>();
                jiggle.Hit(strength.Value, awayFrom.Value);
            }
            Finish();
        }
    }
}

using System;
using UnityEngine;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory(ActionCategory.Tween)]
    [Tooltip("Like Tween Punch, but in local space, so the punch moves along with its parent. Use it for things that " +
             "ride on a moving object, e.g. a canvas on the camera: Tween Punch sets world rotation, so such a canvas " +
             "stops turning with your head while the punch plays.")]
    public class TweenPunchLocal : TweenComponentBase<Transform>
    {
        [Tooltip("Punch local position, local rotation, or scale.")]
        public TweenPunch.PunchType punchType;

        [Tooltip("Punch magnitude.")]
        public FsmVector3 value;

        // The punch is added on top of the object's own movement, and taken back off if the state is left early,
        // so nothing gets stuck where the punch started (same as the patched Tween Punch).
        private Transform transform;
        private Vector3 appliedVector3;
        private Quaternion appliedRotation = Quaternion.identity;

        public override void Reset()
        {
            base.Reset();
            punchType = TweenPunch.PunchType.Rotation;
            value = null;
        }

        public override void OnEnter()
        {
            base.OnEnter();
            if (Finished) return;

            easeType.Value = EasingFunction.Ease.Punch;
            transform = cachedComponent;
            appliedVector3 = Vector3.zero;
            appliedRotation = Quaternion.identity;
        }

        // Same motion as Tween Punch, only on the local values. Clamped like Tween Punch: one way only.
        protected override void DoTween()
        {
            SetPunch(Mathf.Clamp01(easingFunction(0, 1, normalizedTime)));
        }

        public override void OnExit()
        {
            if (transform != null) SetPunch(0f);
        }

        // Moves the object by the change in punch since last time, so its own movement carries on underneath.
        private void SetPunch(float amount)
        {
            switch (punchType)
            {
                case TweenPunch.PunchType.Position:
                    var offset = value.Value * amount;
                    transform.localPosition += offset - appliedVector3;
                    appliedVector3 = offset;
                    break;
                case TweenPunch.PunchType.Rotation:
                    var rotation = Quaternion.Euler(value.Value * amount);
                    transform.localRotation = transform.localRotation * Quaternion.Inverse(appliedRotation) * rotation;
                    appliedRotation = rotation;
                    break;
                case TweenPunch.PunchType.Scale:
                    var grow = value.Value * amount;
                    transform.localScale += grow - appliedVector3;
                    appliedVector3 = grow;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

#if UNITY_EDITOR
        public override string AutoName()
        {
            return "TweenPunchLocal: " + ActionHelpers.GetValueLabel(Fsm, gameObject) + " " + punchType + " " + ActionHelpers.GetValueLabel(value);
        }
#endif
    }
}

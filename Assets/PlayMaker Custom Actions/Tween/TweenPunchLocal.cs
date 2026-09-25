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

        private Transform transform;
        private Vector3 startVector3;
        private Vector3 endVector3;
        private Quaternion startRotation;
        private Quaternion midRotation;
        private Quaternion endRotation;

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

            switch (punchType)
            {
                case TweenPunch.PunchType.Position:
                    startVector3 = transform.localPosition;
                    endVector3 = startVector3 + value.Value;
                    break;
                case TweenPunch.PunchType.Rotation:
                    startRotation = transform.localRotation;
                    midRotation = startRotation * Quaternion.Euler(value.Value * 0.5f);
                    endRotation = startRotation * Quaternion.Euler(value.Value);
                    break;
                case TweenPunch.PunchType.Scale:
                    startVector3 = transform.localScale;
                    endVector3 = startVector3 + value.Value;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        // Same motion as Tween Punch, only on the local values.
        protected override void DoTween()
        {
            var lerp = easingFunction(0, 1, normalizedTime);

            switch (punchType)
            {
                case TweenPunch.PunchType.Position:
                    transform.localPosition = Vector3.Lerp(startVector3, endVector3, lerp);
                    break;
                case TweenPunch.PunchType.Rotation:
                    transform.localRotation = lerp < 0.5f
                        ? Quaternion.Slerp(startRotation, midRotation, lerp * 2f)
                        : Quaternion.Slerp(midRotation, endRotation, (lerp - 0.5f) * 2f);
                    break;
                case TweenPunch.PunchType.Scale:
                    transform.localScale = Vector3.Lerp(startVector3, endVector3, lerp);
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

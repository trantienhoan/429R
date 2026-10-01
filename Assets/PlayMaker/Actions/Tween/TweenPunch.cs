// (c) Copyright HutongGames, LLC. All rights reserved.
// See also: EasingFunctionLicense.txt

using System;
using UnityEngine;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory(ActionCategory.Tween)]
    [Tooltip("Punches a GameObject's position, rotation, or scale and springs back to starting state")]
    public class TweenPunch : TweenComponentBase<Transform>
    {
        public enum PunchType { Position, Rotation, Scale}

        [Tooltip("Punch position, rotation, or scale.")]
        public PunchType punchType;

        [Tooltip("Punch magnitude.")]
        public FsmVector3 value;

        private Transform transform;
        private RectTransform rectTransform;
        
        // 429 Game: the punch is added on top of the object's own movement (physics, animation, its parent, a teleport)
        // instead of pinning it to where it was when the punch started, and leaving the state early takes what's left
        // of the punch back off, so nothing is left hanging in the air or out of place.
        private Vector3 punch;
        private Vector3 appliedVector3;
        private Quaternion appliedRotation = Quaternion.identity;

        public override void Reset()
        {
            base.Reset();

            punchType = PunchType.Position;
            value = null;        
        }

        public override void OnEnter()
        {
            base.OnEnter();
            if (Finished) return;

            easeType.Value = EasingFunction.Ease.Punch;

            transform = cachedComponent;
            rectTransform = transform as RectTransform;

            // 429 Game: a position punch is given in world space; it's kept in the parent's space so it can be taken back
            // off exactly, even if the parent moves or turns meanwhile.
            punch = punchType == PunchType.Position && rectTransform == null && transform.parent != null
                ? transform.parent.InverseTransformVector(value.Value)
                : value.Value;
            appliedVector3 = Vector3.zero;
            appliedRotation = Quaternion.identity;
        }

        protected override void DoTween()
        {
            // Clamped like the Lerp and Slerp this used to be: the punch goes one way only.
            SetPunch(Mathf.Clamp01(easingFunction(0, 1, normalizedTime)));
        }

        public override void OnExit()
        {
            // 429 Game: a punch cut short by leaving the state is taken back off.
            if (transform != null) SetPunch(0f);
        }

        // 429 Game: moves the object by the change in punch since last time, so its own movement carries on underneath.
        private void SetPunch(float amount)
        {
            switch (punchType)
            {
                case PunchType.Position:
                    var offset = punch * amount;
                    if (rectTransform != null)
                    {
                        rectTransform.anchoredPosition3D += offset - appliedVector3;
                    }
                    else
                    {
                        transform.localPosition += offset - appliedVector3;
                    }
                    appliedVector3 = offset;
                    break;
                case PunchType.Rotation:
                    var rotation = Quaternion.Euler(punch * amount);
                    transform.localRotation = transform.localRotation * Quaternion.Inverse(appliedRotation) * rotation;
                    appliedRotation = rotation;
                    break;
                case PunchType.Scale:
                    var grow = punch * amount;
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
            return "TweenPunch: " + ActionHelpers.GetValueLabel(Fsm, gameObject) + " " + punchType + " " + ActionHelpers.GetValueLabel(value);
        }
#endif

    }

}

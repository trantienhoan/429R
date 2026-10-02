using UnityEngine;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory(ActionCategory.Transform)]
    [Tooltip("Turns an object so its front faces the player's head (the headset), or another object. It only turns " +
             "around its up axis, so it stays upright. Unlike Look At, the model's front doesn't have to be its blue Z " +
             "arrow: the treasure chests face Left (away from their red X arrow).")]
    public class FacePlayer : FsmStateAction
    {
        public enum ModelFront
        {
            Forward,
            Back,
            Right,
            Left,
        }

        [RequiredField]
        [Tooltip("The object to turn.")]
        public FsmOwnerDefault gameObject;

        [Tooltip("Face this object. Left empty, the player's head (the headset).")]
        public FsmGameObject target;

        [Tooltip("Which side of the model is its front, by its arrows in the Scene view: Forward = the blue Z arrow, " +
                 "Right = the red X arrow, Left = the side opposite the red arrow, Back = opposite the blue arrow.")]
        public ModelFront front;

        [Tooltip("Keep facing the player every frame.")]
        public bool everyFrame;

        public override void Reset()
        {
            gameObject = null;
            target = null;
            front = ModelFront.Forward;
            everyFrame = false;
        }

        // Turns after animations have moved things this frame, like Look At.
        public override void OnPreprocess()
        {
            Fsm.HandleLateUpdate = true;
        }

        public override void OnEnter()
        {
            DoFace();
            if (!everyFrame) Finish();
        }

        public override void OnLateUpdate()
        {
            DoFace();
        }

        private void DoFace()
        {
            var go = Fsm.GetOwnerDefaultTarget(gameObject);
            if (go == null) return;

            var look = target != null ? target.Value : null;
            if (look == null && Camera.main != null) look = Camera.main.gameObject;
            if (look == null) return;

            var direction = look.transform.position - go.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;

            go.transform.rotation = Quaternion.LookRotation(direction) * Quaternion.Euler(0f, FrontTurn(), 0f);
        }

        // How far to turn so that side, not the blue Z arrow, ends up facing the target.
        private float FrontTurn()
        {
            switch (front)
            {
                case ModelFront.Back: return 180f;
                case ModelFront.Right: return -90f;
                case ModelFront.Left: return 90f;
                default: return 0f;
            }
        }
    }
}

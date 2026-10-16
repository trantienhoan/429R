using UnityEngine;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory(ActionCategory.Physics)]
    [Tooltip("Makes a physics object jump away from the player (or from Away From): sets its Rigidbody's speed straight " +
             "away from them along the floor, plus up, whichever way the object faces. E.g. a pumpkin hopping away when " +
             "it's hit. Finishes at once, the jump plays on by itself.")]
    public class JumpAway : FsmStateAction
    {
        [RequiredField]
        [CheckForComponent(typeof(Rigidbody))]
        [Tooltip("What jumps (it needs a Rigidbody).")]
        public FsmOwnerDefault gameObject;

        [Tooltip("It jumps away from this. Empty: away from the player (the headset).")]
        public FsmGameObject awayFrom;

        [Tooltip("Speed away from them along the floor, in m/s (with Up Speed 4, 5 m/s lands about 4 m further away).")]
        public FsmFloat speed;

        [Tooltip("Speed upwards, in m/s (4 = a hop about 0.8 m high).")]
        public FsmFloat upSpeed;

        [Tooltip("A random turn to either side, in degrees, so it doesn't always go straight back.")]
        public FsmFloat spread;

        public override void Reset()
        {
            gameObject = null;
            awayFrom = new FsmGameObject { UseVariable = true };
            speed = 5f;
            upSpeed = 4f;
            spread = 20f;
        }

        public override void OnEnter()
        {
            var go = Fsm.GetOwnerDefaultTarget(gameObject);
            if (go != null && go.TryGetComponent(out Rigidbody body) && !body.isKinematic)
            {
                var from = awayFrom.Value != null ? awayFrom.Value.transform.position
                    : Camera.main != null ? Camera.main.transform.position
                    : go.transform.position + go.transform.forward;
                var away = go.transform.position - from;
                away.y = 0f;
                if (away.sqrMagnitude < 0.0001f) away = new Vector3(-go.transform.forward.x, 0f, -go.transform.forward.z);
                if (away.sqrMagnitude < 0.0001f) away = Vector3.forward;
                away = Quaternion.Euler(0f, Random.Range(-spread.Value, spread.Value), 0f) * away.normalized;
                body.linearVelocity = away * speed.Value + Vector3.up * upSpeed.Value;
            }
            Finish();
        }
    }
}

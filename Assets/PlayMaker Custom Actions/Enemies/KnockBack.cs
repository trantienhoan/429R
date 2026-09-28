using Game.Enemies;
using UnityEngine;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Enemies")]
    [Tooltip("Knocks an enemy back, away from the player (or another object): it slides back fast, slows to a stop, " +
             "hops a little and stays put a moment before walking again. Made for NavMesh enemies, which physics can't " +
             "push; it slides along the NavMesh, so it won't go through walls. A new hit while it slides starts a fresh push.")]
    public class KnockBack : FsmStateAction
    {
        [RequiredField]
        [Tooltip("The enemy to knock back.")]
        public FsmOwnerDefault gameObject;

        [Tooltip("Knock it away from this object. Left empty, away from the player (the headset).")]
        public FsmGameObject awayFrom;

        [Tooltip("How far it slides back, in metres.")]
        public FsmFloat distance;

        [Tooltip("How long the slide takes, in seconds. It starts fast and slows down.")]
        public FsmFloat duration;

        [Tooltip("How high it hops during the slide, in metres. 0 = stays on the ground.")]
        public FsmFloat hopHeight;

        [Tooltip("How long it stays still after landing before it can walk again, in seconds.")]
        public FsmFloat stunTime;

        public override void Reset()
        {
            gameObject = null;
            awayFrom = null;
            distance = 2.5f;
            duration = 0.45f;
            hopHeight = 0.25f;
            stunTime = 0.3f;
        }

        public override void OnEnter()
        {
            var target = Fsm.GetOwnerDefaultTarget(gameObject);
            if (target != null && TryGetSource(out var from))
            {
                if (!target.TryGetComponent<Knockback>(out var knockback)) knockback = target.AddComponent<Knockback>();
                knockback.Push(from, distance.Value, duration.Value, hopHeight.Value, stunTime.Value);
            }
            Finish();
        }

        private bool TryGetSource(out Vector3 position)
        {
            var source = awayFrom != null ? awayFrom.Value : null;
            if (source == null && Camera.main != null) source = Camera.main.gameObject;

            position = source != null ? source.transform.position : Vector3.zero;
            return source != null;
        }
    }
}

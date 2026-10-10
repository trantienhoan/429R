using UnityEngine;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Player")]
    [Tooltip("Throws the player through the air in an arc: Height metres up and Distance metres away from From (or from " +
             "this GameObject), then back down onto the floor. For big hits, e.g. a boss slam.")]
    public class LaunchPlayer : FsmStateAction
    {
        [Tooltip("Where the hit came from: the player flies away from it. Empty = this FSM's GameObject.")]
        public FsmGameObject from;

        [Tooltip("How high the player is thrown, in metres.")]
        public FsmFloat height;

        [Tooltip("How far along the floor the player is thrown, in metres.")]
        public FsmFloat distance;

        public override void Reset()
        {
            from = null;
            height = 1.2f;
            distance = 2.5f;
        }

        public override void OnEnter()
        {
            var source = from != null && !from.IsNone && from.Value != null ? from.Value : Owner;
            Vector3 position = source != null ? source.transform.position : Vector3.zero;
            Game.Combat.PlayerLaunch.Launch(position, height.Value, distance.Value);
            Finish();
        }
    }
}

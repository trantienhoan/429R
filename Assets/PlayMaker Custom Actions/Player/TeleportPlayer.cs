using Unity.XR.CoreUtils;
using UnityEngine;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Player")]
    [Tooltip("Moves the player so their HEAD is right above a spot, with their feet on its height, wherever they're " +
             "standing in their real room. (Set Position on the Player moves the XR Origin: the middle of their real " +
             "play space. They land as far from the spot as they stand from that middle.) Can also turn them to face a " +
             "direction. Hide it with Headset Fade, so the player doesn't see the world jump.")]
    public class TeleportPlayer : FsmStateAction
    {
        [Title("Go To Object")]
        [Tooltip("Optional: an object to move the player TO, e.g. a spawn point. Leave it None to use Position alone. " +
                 "(The player is always the one who moves; setting it to the Player does nothing, so that's ignored.)")]
        public FsmGameObject target;

        [Tooltip("Without a Target: where to put the player, e.g. 0,0,0 for the middle of the map; its height is the " +
                 "floor they stand on. With a Target: added to the Target's position.")]
        public FsmVector3 position;

        [Tooltip("Also turn the player to face Y Angle.")]
        public FsmBool turn;

        [Tooltip("With Turn: the way to face, in degrees around the up axis (0 = the world's forward, the blue Z arrow). " +
                 "With a Target: added to the way the Target faces.")]
        public FsmFloat yAngle;

        public override void Reset()
        {
            target = null;
            position = new FsmVector3 { Value = Vector3.zero };
            turn = false;
            yAngle = 0f;
        }

        public override void OnEnter()
        {
            DoTeleport();
            Finish();
        }

        private void DoTeleport()
        {
            var origin = Object.FindAnyObjectByType<XROrigin>();
            if (origin == null || origin.Camera == null)
            {
                LogWarning("There's no XR Origin with a camera in the scene.");
                return;
            }

            var rig = origin.transform;
            var head = origin.Camera.transform;
            var spot = position.IsNone ? Vector3.zero : position.Value;
            float facing = yAngle.IsNone ? 0f : yAngle.Value;
            var go = target.Value;
            // The player itself (the XR Origin or any part of it) isn't a place to go to.
            if (go != null && go.transform.IsChildOf(rig))
            {
                LogWarning("Go To Object is the player itself, so it's ignored and Position alone is used. Set it to None.");
                go = null;
            }
            if (go != null)
            {
                spot += go.transform.position;
                facing += go.transform.eulerAngles.y;
            }

            // Turned around the head first, so turning doesn't move it.
            if (turn.Value)
            {
                var forward = head.forward;
                forward.y = 0f;
                // Looking straight down: the top of the head points forward.
                if (forward.sqrMagnitude < 0.0001f) forward = head.up;
                float current = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
                rig.RotateAround(head.position, Vector3.up, Mathf.DeltaAngle(current, facing));
            }

            // Then moved so the head is above the spot and the floor of the play space is at its height.
            var headOffset = head.position - rig.position;
            rig.position = new Vector3(spot.x - headOffset.x, spot.y, spot.z - headOffset.z);

            // The rig's Character Controller would otherwise put it back where it was on its next move.
            Physics.SyncTransforms();
        }
    }
}

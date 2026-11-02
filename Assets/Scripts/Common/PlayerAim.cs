using HutongGames.PlayMaker;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Where FSMs aim when their target is the player. $Player is the XR Origin, the middle of the play area on the floor:
    /// walking in real life moves the head away from it, and enemies looking or running at it went for a spot beside the
    /// player. PlayMaker's Look At, Smooth Look At, Get Distance and Set Agent Destination As Game Object ask here, so no FSM
    /// has to change (their sources in Assets/PlayMaker and PlayMaker PathFinding are edited: a PlayMaker update undoes that).
    /// </summary>
    public static class PlayerAim
    {
        private const string PlayerVariable = "Player";

        private static FsmGameObject player;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => player = null;

        /// <summary>The target's position; for the player's XR Origin, the point on its floor right under their head.</summary>
        public static Vector3 PositionOf(GameObject target)
        {
            var at = target.transform.position;
            if (player == null) player = FsmVariables.GlobalVariables.FindFsmGameObject(PlayerVariable);
            if (player == null || player.Value != target) return at;
            var eyes = Camera.main;
            if (eyes == null) return at;
            var head = eyes.transform.position;
            return new Vector3(head.x, at.y, head.z);
        }
    }
}

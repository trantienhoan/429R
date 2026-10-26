using UnityEngine;

namespace Game
{
    /// <summary>
    /// Shakes the player's view: an event to the Info FSM on the camera rig (the PlayMaker global MainCam), which punches
    /// it about: ShakeSmall, ShakeMed, ShakeBig or ShakeBigger. A weaker shake never cuts a stronger one short, whoever
    /// sent that one (its Info FSM state says which is running).
    /// </summary>
    public static class ViewShake
    {
        public const string Small = "ShakeSmall";
        public const string Med = "ShakeMed";
        public const string Big = "ShakeBig";
        public const string Bigger = "ShakeBigger";

        private static PlayMakerFSM info;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => info = null;

        /// <summary>Shakes the view, unless a stronger shake is still going.</summary>
        public static void Send(string shake)
        {
            var fsm = Info();
            if (fsm == null || string.IsNullOrEmpty(shake) || Strength(shake) <= Running(fsm)) return;
            fsm.SendEvent(shake);
        }

        /// <summary>
        /// A bang at 'point' shakes the view by how close the player's head is: ShakeMed within 'near' metres, ShakeSmall
        /// within 'far', nothing farther.
        /// </summary>
        public static void Near(Vector3 point, float near, float far)
        {
            var eyes = Camera.main;
            if (eyes == null) return;
            float distance = Vector3.Distance(eyes.transform.position, point);
            if (distance <= near) Send(Med);
            else if (distance <= far) Send(Small);
        }

        private static PlayMakerFSM Info()
        {
            if (info != null) return info;
            var cam = HutongGames.PlayMaker.FsmVariables.GlobalVariables.FindFsmGameObject("MainCam");
            if (cam == null || cam.Value == null) return null;
            foreach (var fsm in cam.Value.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName == "Info") info = fsm;
            }
            return info;
        }

        private static int Strength(string shake)
        {
            switch (shake)
            {
                case Small: return 1;
                case Med: return 2;
                case Big: return 3;
                case Bigger: return 4;
                default: return 2;
            }
        }

        // The shake the Info FSM is in the middle of (its states: Tween = small, Shake Med, Shake 2 = big, Shake 3 =
        // bigger); 0 when it's still.
        private static int Running(PlayMakerFSM fsm)
        {
            switch (fsm.ActiveStateName)
            {
                case "Tween": return 1;
                case "Shake Med": return 2;
                case "Shake 2": return 3;
                case "Shake 3": return 4;
                default: return 0;
            }
        }
    }
}

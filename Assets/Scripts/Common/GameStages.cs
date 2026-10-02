using UnityEngine;
using FsmVariables = HutongGames.PlayMaker.FsmVariables;

namespace Game
{
    /// <summary>
    /// Talks to the GAMESTAGES FSM (the object in the PlayMaker global GAMESTAGES), the same way a "Set Fsm Bool" action
    /// with Game Object = GAMESTAGES and FSM Name = GAMESTAGES does.
    /// </summary>
    public static class GameStages
    {
        private const string OwnerVariable = "GAMESTAGES";
        private const string FsmName = "GAMESTAGES";

        /// <summary>
        /// Sets a bool variable of the GAMESTAGES FSM. Returns false if it doesn't have one by that name, with a warning
        /// unless Warn If Missing is off (e.g. while the game is closing and GAMESTAGES may be gone already).
        /// </summary>
        public static bool SetBool(string variable, bool value, Object context = null, bool warnIfMissing = true)
        {
            if (string.IsNullOrEmpty(variable)) return false;

            var fsm = FindFsm();
            // Find, not Get: Get hands back a new, unconnected variable when the name doesn't exist.
            var flag = fsm != null ? fsm.FsmVariables.FindFsmBool(variable) : null;
            if (flag == null)
            {
                if (!warnIfMissing) return false;
                Debug.LogWarning(fsm == null
                    ? $"[GameStages] No GAMESTAGES FSM found (PlayMaker global '{OwnerVariable}'), so '{variable}' wasn't set."
                    : $"[GameStages] The GAMESTAGES FSM has no bool variable '{variable}'. Add it in its Variables tab.", context);
                return false;
            }

            flag.Value = value;
            return true;
        }

        private static PlayMakerFSM FindFsm()
        {
            var owner = FsmVariables.GlobalVariables.FindFsmGameObject(OwnerVariable);
            if (owner == null || owner.Value == null) return null;

            foreach (var fsm in owner.Value.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName == FsmName) return fsm;
            }
            return null;
        }
    }
}

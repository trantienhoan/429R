using UnityEngine;
using FsmVariables = HutongGames.PlayMaker.FsmVariables;

namespace Game
{
    /// <summary>
    /// Talks to the GAMESTAGES FSM (the object in the PlayMaker global GAMESTAGES), the same way a "Set Fsm Bool" action
    /// with Game Object = GAMESTAGES and FSM Name = GAMESTAGES does; SetFsmBool does the same for any FSM whose object is
    /// held in a PlayMaker global (e.g. an EnemyManager).
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
            return SetFsmBool(OwnerVariable, FsmName, variable, value, context, warnIfMissing);
        }

        /// <summary>
        /// Sets a bool variable of the FSM called <paramref name="fsmName"/> on the object held in the PlayMaker global
        /// GameObject <paramref name="globalObject"/>, like a "Set Fsm Variable" action with Game Object = that global
        /// (e.g. EnemyManager0, FSM EnemyManager). Returns false, with a warning unless Warn If Missing is off, if the
        /// global is empty (its object hasn't started yet) or the FSM or the bool isn't there.
        /// </summary>
        public static bool SetFsmBool(string globalObject, string fsmName, string variable, bool value, Object context = null, bool warnIfMissing = true)
        {
            if (string.IsNullOrEmpty(globalObject) || string.IsNullOrEmpty(variable)) return false;

            var owner = FsmVariables.GlobalVariables.FindFsmGameObject(globalObject);
            PlayMakerFSM fsm = null;
            if (owner != null && owner.Value != null)
            {
                foreach (var candidate in owner.Value.GetComponents<PlayMakerFSM>())
                {
                    if (candidate.FsmName != fsmName) continue;
                    fsm = candidate;
                    break;
                }
            }

            // Find, not Get: Get hands back a new, unconnected variable when the name doesn't exist.
            var flag = fsm != null ? fsm.FsmVariables.FindFsmBool(variable) : null;
            if (flag == null)
            {
                if (!warnIfMissing) return false;
                string problem = owner == null ? $"there's no PlayMaker global GameObject '{globalObject}'"
                    : owner.Value == null ? $"the PlayMaker global '{globalObject}' is empty (its object hasn't started yet)"
                    : fsm == null ? $"'{owner.Value.name}' has no FSM called '{fsmName}'"
                    : $"the {fsmName} FSM on '{owner.Value.name}' has no bool variable '{variable}' (add it in its Variables tab)";
                Debug.LogWarning($"[GameStages] {char.ToUpper(problem[0])}{problem.Substring(1)}, so '{variable}' wasn't set.", context);
                return false;
            }

            flag.Value = value;
            return true;
        }
    }
}

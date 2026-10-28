using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// For PlayMaker breakables that judge a hit by its speed alone: a "Damage" FSM that stores the hit's speed in its
    /// "collisionForce" float and checks it in a "Check force" state (the whack pumpkins, the old breakables). Judge puts
    /// in the hit's real strength instead (a punch's power, a weapon's) and has the FSM check again. Fist does it for
    /// punches, Fair Hits for weapons.
    /// </summary>
    public static class PlayMakerHits
    {
        public const string DamageFsm = "Damage";
        public const string ForceVariable = "collisionForce";
        public const string CheckState = "Check force";

        /// <summary>
        /// Collision Events Fair Hits has switched off (so only the player's hits count): they still mark a state as
        /// listening for a hit.
        /// </summary>
        public static readonly HashSet<HutongGames.PlayMaker.Actions.CollisionEvent> Muted = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Muted.Clear();

        /// <summary>
        /// Has the Damage FSMs on <paramref name="hit"/> judge this collision with <paramref name="force"/> as its force,
        /// if they're waiting for a hit, or have just turned this very hit down by its speed alone (the FSM heard the
        /// collision first). A hit that already counted isn't counted again.
        /// </summary>
        public static void Judge(GameObject hit, Collision collision, float force)
        {
            foreach (var fsm in hit.GetComponents<PlayMakerFSM>())
            {
                if (!fsm.enabled || fsm.FsmName != DamageFsm) continue;
                var variable = fsm.FsmVariables.FindFsmFloat(ForceVariable);
                if (variable == null || Array.Find(fsm.FsmStates, state => state.Name == CheckState) == null) continue;
                // Already hurt by this hit (or by one just before): not twice.
                if (!Waiting(fsm) && !JustJudged(fsm, variable.Value, collision)) continue;
                variable.Value = force;
                fsm.SetState(CheckState);
            }
        }

        // Waiting for a hit, or checking one (the whack pumpkins' "wait for collision" too), or in any state that listens
        // for a hit's force (a whack pumpkin's laugh, which a real hit cuts short).
        private static bool Waiting(PlayMakerFSM fsm)
        {
            string state = fsm.ActiveStateName;
            if (string.Equals(state, "Wait for Collision", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(state, "shake", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(state, CheckState, StringComparison.OrdinalIgnoreCase)) return true;
            var active = fsm.Fsm.ActiveState;
            if (active == null) return false;
            foreach (var action in active.Actions)
            {
                if (action is HutongGames.PlayMaker.Actions.CollisionEvent listens && (listens.Enabled || Muted.Contains(listens)) &&
                    listens.storeForce != null && listens.storeForce.Name == ForceVariable) return true;
            }
            return false;
        }

        // It has just turned this very hit down by its speed alone: it went on from Check Force with this collision's
        // speed as its force, and not to where a hit that counts goes ("next": it already counted, not twice).
        private static bool JustJudged(PlayMakerFSM fsm, float force, Collision collision)
        {
            var before = fsm.Fsm.PreviousActiveState;
            if (before == null || !string.Equals(before.Name, CheckState, StringComparison.OrdinalIgnoreCase) ||
                Mathf.Abs(force - collision.relativeVelocity.magnitude) >= 0.01f) return false;
            foreach (var passed in before.Transitions)
            {
                if (passed.EventName == "next" && passed.ToState == fsm.ActiveStateName) return false;
            }
            return true;
        }
    }
}

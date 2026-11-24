using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// The player's hits only ever count as weak on this thing (the rooftop's Pumpkin_Tree; user, 2026-11-23: "Player hit
    /// to the Tree count as weak, only bug should do big damage to the Tree"). Its own Damage FSM counts a hit of theirs
    /// once, in its weak-hit state (Deal 1 damage: a shake, a sound, 1 health, then a short wait); the player's blasts
    /// (exploding punches, explosive throws, grenades, smoke balls) go the same way instead of taking their damage off its
    /// health (Blast asks this first). The bugs' bites (its "Damage" event) keep their own, bigger damage. Set up by
    /// Tools > 429 Game > Items > Pumpkin Tree: Weak Player Hits, Big Bug Bites.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerHitsWeak : MonoBehaviour
    {
        [Tooltip("Its Damage FSM.")]
        [SerializeField] private string damageFsm = "Damage";
        [Tooltip("The state that FSM waits for a hit in: only then does a hit of the player's count (one per its wait).")]
        [SerializeField] private string waitState = "Wait for Collision";
        [Tooltip("The state that counts a weak hit.")]
        [SerializeField] private string weakHitState = "Deal 1 damage";

        private PlayMakerFSM fsm;

        /// <summary>
        /// A blast of the player's reached 'target' (the thing with the health): true if it's one of these, which takes
        /// the blast as one weak hit instead of its damage.
        /// </summary>
        public static bool TakeBlast(Transform target)
        {
            if (target == null || !target.TryGetComponent<PlayerHitsWeak>(out var weak) || !weak.isActiveAndEnabled) return false;
            weak.WeakHit();
            return true;
        }

        /// <summary>One weak hit, if the Damage FSM is waiting for one (if not, it's still counting the last one).</summary>
        public void WeakHit()
        {
            if (fsm == null)
            {
                foreach (var f in GetComponents<PlayMakerFSM>())
                {
                    if (f.FsmName != damageFsm) continue;
                    fsm = f;
                    break;
                }
            }
            if (fsm == null || !fsm.enabled || fsm.ActiveStateName != waitState) return;
            fsm.SetState(weakHitState);
        }
    }
}

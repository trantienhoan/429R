using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Keeps the number of a kind of thing alive at once under a limit, e.g. the Pumpkin_On_Tree every broken piece of
    /// furniture leaves behind: when a new one appears past the limit, the oldest one breaks the usual way (Break Oldest:
    /// its FSM's health goes to 0, so it bursts into pieces and drops what it holds, e.g. Pumpkin_On_Tree's candies), or
    /// pops (its FSM gets the Pop Event, e.g. Pumpkin_On_Tree's Health "Dead": a bubble pop, no pieces, nothing dropped).
    /// If its FSM can't do that, it's simply removed. Goes on the prefab's root; everything with the same Group counts
    /// together.
    /// </summary>
    [DisallowMultipleComponent]
    public class LiveLimit : MonoBehaviour
    {
        [Tooltip("Everything with this group name counts together. Empty: the object's name without \"(Clone)\".")]
        [SerializeField] private string group = "";
        [Tooltip("How many can be alive at once. One more appearing pops the oldest.")]
        [Min(1)]
        [SerializeField] private int limit = 42;
        [Tooltip("Break the oldest the usual way (its FSM's Health Variable to 0: pieces, and what it holds, e.g. candies) " +
                 "instead of popping it.")]
        [SerializeField] private bool breakOldest = true;
        [Tooltip("The FSM that breaks or pops it, the health float that breaks it, and the event that pops it.")]
        [SerializeField] private string popFsm = "Health";
        [SerializeField] private string healthVariable = "health";
        [SerializeField] private string popEvent = "Dead";
        [Tooltip("The state the event leads to; if the FSM isn't there afterwards, it couldn't take the event, so the " +
                 "object is removed instead. Empty: always trust the event.")]
        [SerializeField] private string popState = "pop";

        // Oldest first, per group.
        private static readonly Dictionary<string, LinkedList<LiveLimit>> alive = new();

        private LinkedListNode<LiveLimit> node;
        private string key;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => alive.Clear();

        /// <summary>How many of a group are alive right now.</summary>
        public static int Count(string group) => alive.TryGetValue(group, out var list) ? list.Count : 0;

        private void OnEnable()
        {
            key = string.IsNullOrEmpty(group) ? name.Replace("(Clone)", "").Trim() : group;
            if (!alive.TryGetValue(key, out var list)) alive[key] = list = new LinkedList<LiveLimit>();
            node = list.AddLast(this);
            while (list.Count > limit)
            {
                var oldest = OldestNotHeld(list);
                if (oldest == null || oldest == this) break;
                oldest.Pop();
            }
        }

        // The oldest one nobody is holding: one in the player's hand (e.g. on its way to the cauldron) is left alone.
        private static LiveLimit OldestNotHeld(LinkedList<LiveLimit> list)
        {
            for (var n = list.First; n != null; n = n.Next)
            {
                var one = n.Value;
                if (one == null) continue;
                if (one.TryGetComponent(out UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable grab) && grab.isSelected) continue;
                return one;
            }
            return null;
        }

        private void OnDisable() => Leave();

        private void Leave()
        {
            if (node == null) return;
            node.List?.Remove(node);
            node = null;
        }

        private void Pop()
        {
            // Out of the count straight away, so it isn't broken or popped twice while it goes.
            Leave();
            foreach (var fsm in GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName != popFsm || !fsm.enabled) continue;
                if (breakOldest)
                {
                    // Its FSM watches its health and breaks it (Pumpkin_On_Tree: Compare Health, every frame).
                    var health = fsm.FsmVariables.FindFsmFloat(healthVariable);
                    if (health == null) break;
                    health.Value = 0f;
                    return;
                }
                fsm.SendEvent(popEvent);
                if (string.IsNullOrEmpty(popState) || fsm.ActiveStateName == popState) return;
                break;
            }
            Destroy(gameObject);
        }
    }
}

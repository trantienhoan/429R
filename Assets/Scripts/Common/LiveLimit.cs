using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Keeps the number of a kind of thing alive at once under a limit, e.g. the Pumpkin_On_Tree every broken piece of
    /// furniture leaves behind: when a new one appears past the limit, the oldest one pops (its FSM gets the Pop Event,
    /// e.g. Pumpkin_On_Tree's Health "Dead", which plays a bubble pop and removes it without breaking into pieces).
    /// If its FSM can't take the event right then, it's simply removed. Goes on the prefab's root; everything with the
    /// same Group counts together.
    /// </summary>
    [DisallowMultipleComponent]
    public class LiveLimit : MonoBehaviour
    {
        [Tooltip("Everything with this group name counts together. Empty: the object's name without \"(Clone)\".")]
        [SerializeField] private string group = "";
        [Tooltip("How many can be alive at once. One more appearing pops the oldest.")]
        [Min(1)]
        [SerializeField] private int limit = 42;
        [Tooltip("The FSM that pops it, and the event that does it.")]
        [SerializeField] private string popFsm = "Health";
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
            // Out of the count straight away, so it isn't popped twice while its pop sound plays.
            Leave();
            foreach (var fsm in GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName != popFsm || !fsm.enabled) continue;
                fsm.SendEvent(popEvent);
                if (string.IsNullOrEmpty(popState) || fsm.ActiveStateName == popState) return;
                break;
            }
            Destroy(gameObject);
        }
    }
}

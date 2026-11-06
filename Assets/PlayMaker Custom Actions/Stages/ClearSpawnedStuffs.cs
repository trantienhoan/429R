using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Stages")]
    [Tooltip("Clears what's lying around in CurrentlySpawnedStuffs (every top-level object in it) before the next stage " +
             "is made, e.g. the PlayGround's trees, bushes, hideouts, candies and pieces when it sinks and the rooftop " +
             "comes (user, 2026-11-06: hundreds of them came along and sounded at once). What the player holds stays, and " +
             "so do the objects named in Keep.")]
    public class ClearSpawnedStuffs : FsmStateAction
    {
        [Tooltip("The holder (empty: the global CurrentlySpawnedStuffs).")]
        public FsmGameObject holder;

        [Tooltip("Objects whose name starts with one of these stay.")]
        public FsmString[] keep;

        [Tooltip("How many were cleared (optional).")]
        [UIHint(UIHint.Variable)]
        public FsmInt storeCleared;

        public override void Reset()
        {
            holder = null;
            keep = new FsmString[0];
            storeCleared = null;
        }

        public override void OnEnter()
        {
            var root = holder != null && holder.Value != null ? holder.Value : null;
            if (root == null)
            {
                var global = FsmVariables.GlobalVariables.FindFsmGameObject("CurrentlySpawnedStuffs");
                root = global != null ? global.Value : null;
            }
            int cleared = 0;
            if (root != null)
            {
                var children = new Transform[root.transform.childCount];
                for (int i = 0; i < children.Length; i++) children[i] = root.transform.GetChild(i);
                foreach (var child in children)
                {
                    if (child == null || Kept(child) || Held(child)) continue;
                    Object.Destroy(child.gameObject);
                    cleared++;
                }
            }
            if (storeCleared != null && !storeCleared.IsNone) storeCleared.Value = cleared;
            Finish();
        }

        private bool Kept(Transform t)
        {
            if (keep == null) return false;
            foreach (var k in keep)
            {
                if (k != null && !string.IsNullOrEmpty(k.Value) && t.name.StartsWith(k.Value)) return true;
            }
            return false;
        }

        // Something in it is in the player's hand.
        private static bool Held(Transform t)
        {
            foreach (var grab in t.GetComponentsInChildren<XRBaseInteractable>(true))
            {
                if (grab.isSelected) return true;
            }
            return false;
        }
    }
}

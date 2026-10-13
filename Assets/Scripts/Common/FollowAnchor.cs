using UnityEngine;

namespace Game
{
    /// <summary>
    /// Keeps an object stuck to another one, its anchor, without being its child: it moves and turns with the anchor
    /// but keeps its own size. E.g. a growing pumpkin on the branch slot it grows from, so it sways and jiggles with the
    /// Pumpkin_Tree without being stretched by the tree's squash. With Destroy With Anchor on, it's destroyed when the
    /// anchor is (the tree dies), and right away when the anchor's Health FSM reaches 0 (the tree starts dying). A
    /// Rigidbody on it is made kinematic while it follows. The PlayMaker action "Stick To Anchor" adds it.
    /// </summary>
    [DisallowMultipleComponent]
    public class FollowAnchor : MonoBehaviour
    {
        [Tooltip("What it sticks to. It keeps where it sits on it, as it was when it was stuck on.")]
        [SerializeField] private Transform anchor;
        [Tooltip("Destroy it with its anchor, e.g. a pumpkin still growing when its tree dies.")]
        [SerializeField] private bool destroyWithAnchor = true;

        private Vector3 offset;
        private Quaternion turn;
        private bool attached;
        private HutongGames.PlayMaker.FsmFloat anchorHealth;

        /// <summary>Sticks it to the anchor where it is now.</summary>
        public void Attach(Transform target, bool destroyWith)
        {
            anchor = target;
            destroyWithAnchor = destroyWith;
            attached = anchor != null;
            if (!attached) return;

            // Where it sits on the anchor, in the anchor's turn but not its scale.
            var inverse = Quaternion.Inverse(anchor.rotation);
            offset = inverse * (transform.position - anchor.position);
            turn = inverse * transform.rotation;
            if (TryGetComponent<Rigidbody>(out var body)) body.isKinematic = true;
            anchorHealth = FindHealth(anchor);
        }

        private void Start()
        {
            if (!attached && anchor != null) Attach(anchor, destroyWithAnchor);
        }

        // After the anchor's own animation, jiggle and shakes have moved it this frame.
        private void LateUpdate()
        {
            if (!attached) return;
            if (anchor == null || (anchorHealth != null && anchorHealth.Value <= 0f))
            {
                attached = false;
                if (destroyWithAnchor) Destroy(gameObject);
                return;
            }
            transform.SetPositionAndRotation(anchor.position + anchor.rotation * offset, anchor.rotation * turn);
        }

        // The health in the Health FSM of the anchor or the first thing above it that has one (the tree), if any.
        private static HutongGames.PlayMaker.FsmFloat FindHealth(Transform from)
        {
            for (var t = from; t != null; t = t.parent)
            {
                foreach (var fsm in t.GetComponents<PlayMakerFSM>())
                {
                    if (fsm.FsmName != "Health") continue;
                    var health = fsm.FsmVariables.FindFsmFloat("health");
                    if (health != null) return health;
                }
            }
            return null;
        }
    }
}

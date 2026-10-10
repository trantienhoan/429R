using System.Collections.Generic;
using UnityEngine;

namespace Game.Rooms
{
    /// <summary>
    /// A prop that hangs on a room wall (picture, banner, hanging shelf, spider web). Its Rigidbody is frozen so it
    /// stays on the wall; when the room's walls go, the room (Drop Wall Hung Props) calls <see cref="Drop"/> and it
    /// falls to the floor. Goes on the prop's root, next to its Rigidbody.
    /// </summary>
    [DisallowMultipleComponent]
    public class WallHungProp : MonoBehaviour
    {
        [Tooltip("When it drops, a heavier mass is lowered to this, so a shelf made very heavy to stay put on the wall " +
                 "doesn't crush or fling what it lands on.")]
        [Min(0.1f)] [SerializeField] private float massWhenDropped = 20f;

        /// <summary>Every wall-hung prop that's in the game right now.</summary>
        public static readonly List<WallHungProp> Active = new List<WallHungProp>();

        public bool Dropped { get; private set; }

        private void OnEnable() => Active.Add(this);

        private void OnDisable() => Active.Remove(this);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Active.Clear();

        public void Drop()
        {
            if (Dropped) return;
            Dropped = true;
            if (!TryGetComponent(out Rigidbody body)) return;
            body.isKinematic = false;
            body.useGravity = true;
            body.constraints = RigidbodyConstraints.None;
            if (body.mass > massWhenDropped) body.mass = massWhenDropped;
            body.WakeUp();
        }
    }
}

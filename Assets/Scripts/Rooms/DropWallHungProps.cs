using UnityEngine;

namespace Game.Rooms
{
    /// <summary>
    /// When the room plays one of its "walls go" Animator states, every Wall Hung Prop in the room drops to the floor
    /// (pictures, banners and hanging shelves would otherwise float where the walls were). Goes on the room's root,
    /// next to its Animator (Bedroom_Base_Z, Livingroom_Base_Z).
    /// </summary>
    [DisallowMultipleComponent]
    public class DropWallHungProps : MonoBehaviour
    {
        [Tooltip("The room's Animator. Empty: the one on this GameObject.")]
        [SerializeField] private Animator animator;
        [Tooltip("Animator states (base layer) that take the walls away.")]
        [SerializeField] private string[] wallsGoStates = { "Bedroom_Mini_GO", "Bedroom_GO" };
        [Tooltip("Seconds after such a state starts before the props drop (when the walls start to fall).")]
        [Min(0f)] [SerializeField] private float delay = 0.7f;
        [Tooltip("Props up to this far (flat) from the room's middle drop. Grows with the room's scale (the living " +
                 "room is scaled up 1.5x for its second room).")]
        [Min(0.5f)] [SerializeField] private float roomRadius = 6.5f;

        private int[] wallsGoHashes;
        private int lastState;
        private float dropAt = -1f;

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            wallsGoHashes = new int[wallsGoStates.Length];
            for (int i = 0; i < wallsGoStates.Length; i++) wallsGoHashes[i] = Animator.StringToHash(wallsGoStates[i]);
        }

        private void Update()
        {
            if (animator == null || !animator.isActiveAndEnabled) return;
            int state = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
            if (state != lastState)
            {
                lastState = state;
                if (System.Array.IndexOf(wallsGoHashes, state) >= 0) dropAt = Time.time + delay;
            }
            if (dropAt >= 0f && Time.time >= dropAt)
            {
                dropAt = -1f;
                DropAll();
            }
        }

        private void DropAll()
        {
            var middle = transform.position;
            var scale = transform.lossyScale;
            float radius = roomRadius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            foreach (var prop in WallHungProp.Active.ToArray())
            {
                if (prop == null) continue;
                var flat = prop.transform.position - middle;
                flat.y = 0f;
                if (flat.magnitude <= radius) prop.Drop();
            }
        }
    }
}

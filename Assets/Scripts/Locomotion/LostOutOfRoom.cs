using UnityEngine;

namespace Game.Locomotion
{
    /// <summary>
    /// A stage pumpkin (Pumpkin_3, 5, 7: the game waits for it to break) that gets out of the room (user, 2026-11-20:
    /// "When Pumpkin 3 5 7 somehow jumps out of the room and drop to fall edge, drop their health to 0"): over a fall
    /// edge's spot (at any height, so a fast fall past the edge's box still counts) or Drop metres below where it
    /// appeared, its health runs out, so the stage goes on. Not while it's held.
    /// </summary>
    [DisallowMultipleComponent]
    public class LostOutOfRoom : MonoBehaviour
    {
        [Tooltip("Lost once it's this far below where it appeared (m).")]
        [Min(0.5f)]
        [SerializeField] private float drop = 2.5f;

        private float startY, next;
        private bool done;
        private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grab;

        private void Start()
        {
            startY = transform.position.y;
            grab = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        }

        private void Update()
        {
            if (done || Time.time < next) return;
            next = Time.time + 0.25f;
            if (grab != null && grab.isSelected) return;
            var at = transform.position;
            bool below = at.y < startY - drop;
            bool overEdge = false;
            if (!below)
            {
                // Over an edge's spot: the edge's own box, from far below to well above.
                for (float y = -6f; y <= 3f && !overEdge; y += 1.5f) overEdge = FallEdge.Covers(new Vector3(at.x, startY + y, at.z));
            }
            if (!below && !overEdge) return;
            done = true;
            Debug.Log($"[Lost Out Of Room] {name} got out of the room ({(below ? "fell" : "over a fall edge")}): its health runs out.");
            FallEdge.RunOut(gameObject);
        }
    }
}

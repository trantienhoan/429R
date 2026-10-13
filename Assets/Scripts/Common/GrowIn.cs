using UnityEngine;

namespace Game
{
    /// <summary>
    /// Grows an object smoothly into its size when it appears, instead of popping in at it. E.g. Pumpkin_00's cracked
    /// shape: Create Object puts it under the pumpkin at its own size, which is bigger than the pumpkin it takes over
    /// from, so it starts as big as the pumpkin (Start At Parent Size) and grows to its size over Grow Seconds, and the
    /// pumpkin's collider grows along with it (Grow Parent Collider), so hits land on the whole bigger shape. Goes on
    /// the prefab's root; its size is read as it starts, after Create Object has parented it.
    /// </summary>
    [DisallowMultipleComponent]
    public class GrowIn : MonoBehaviour
    {
        [Tooltip("Grow the colliders on its parent by as much as this grows (sphere, box and capsule colliders), e.g. so " +
                 "the pumpkin's collider fits its bigger cracked shape.")]
        [SerializeField] private bool growParentCollider = true;
        [Tooltip("Start as big as its parent's own model (a local scale of 1), e.g. a cracked shape taking over from the " +
                 "pumpkin it's under. Unticked: start at Start Share of its size.")]
        [SerializeField] private bool startAtParentSize = true;
        [Tooltip("How big it starts, as a share of its size (when not starting at its parent's size).")]
        [Range(0f, 1f)]
        [SerializeField] private float startShare = 0.3f;
        [Tooltip("Seconds it takes to grow to its size.")]
        [Min(0.01f)]
        [SerializeField] private float growSeconds = 2f;

        private Vector3 from;
        private Vector3 to;
        private float grown = -1f;

        // The parent's colliders and their sizes as this started.
        private Collider[] colliders;
        private Vector3[] centers;
        private Vector3[] sizes;

        private void Start()
        {
            to = transform.localScale;
            from = startAtParentSize && transform.parent != null ? Vector3.one : to * startShare;
            if (from == to || Mathf.Approximately(from.x, 0f)) return;
            grown = 0f;
            transform.localScale = from;

            if (!growParentCollider || transform.parent == null) return;
            colliders = transform.parent.GetComponents<Collider>();
            centers = new Vector3[colliders.Length];
            sizes = new Vector3[colliders.Length];
            for (int i = 0; i < colliders.Length; i++)
            {
                switch (colliders[i])
                {
                    case SphereCollider sphere:
                        centers[i] = sphere.center;
                        sizes[i] = new Vector3(sphere.radius, 0f, 0f);
                        break;
                    case BoxCollider box:
                        centers[i] = box.center;
                        sizes[i] = box.size;
                        break;
                    case CapsuleCollider capsule:
                        centers[i] = capsule.center;
                        sizes[i] = new Vector3(capsule.radius, capsule.height, 0f);
                        break;
                }
            }
        }

        private void Update()
        {
            if (grown < 0f) return;
            grown = Mathf.Min(grown + Time.deltaTime / growSeconds, 1f);
            // Gently out of the start and into its size.
            float t = grown * grown * (3f - 2f * grown);
            transform.localScale = Vector3.LerpUnclamped(from, to, t);
            GrowColliders(transform.localScale.x / from.x);
            if (grown >= 1f) grown = -1f;
        }

        // The parent's colliders, as many times their first size as this has grown (from the parent's middle, as this does).
        private void GrowColliders(float times)
        {
            if (colliders == null) return;
            for (int i = 0; i < colliders.Length; i++)
            {
                switch (colliders[i])
                {
                    case SphereCollider sphere:
                        sphere.center = centers[i] * times;
                        sphere.radius = sizes[i].x * times;
                        break;
                    case BoxCollider box:
                        box.center = centers[i] * times;
                        box.size = sizes[i] * times;
                        break;
                    case CapsuleCollider capsule:
                        capsule.center = centers[i] * times;
                        capsule.radius = sizes[i].x * times;
                        capsule.height = sizes[i].y * times;
                        break;
                }
            }
        }
    }
}

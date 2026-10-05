using System.Collections.Generic;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// The space ghost balloons roam about in, e.g. the PlayGround: a box, drawn in the Scene view while it's selected.
    /// Ghost balloons that start inside it, or within 15 m of it, float from spot to spot inside it. Goes on any object,
    /// e.g. the PlayGround's root; added in the Editor, it fits itself over that object, 1.5 to 4 m above its floor.
    /// </summary>
    [DisallowMultipleComponent]
    public class GhostRoamArea : MonoBehaviour
    {
        [Tooltip("The box's middle, in this object's own space.")]
        [SerializeField] private Vector3 center = new(0f, 2.75f, 0f);
        [Tooltip("The box's size, in this object's own space.")]
        [SerializeField] private Vector3 size = new(8f, 2.5f, 8f);

        private const float Reach = 15f;
        private static readonly List<GhostRoamArea> areas = new();

        private void OnEnable()
        {
            areas.Add(this);
        }

        private void OnDisable()
        {
            areas.Remove(this);
        }

        /// <summary>The area 'point' is in, or else the nearest one within 15 m of it; null if there's none.</summary>
        public static GhostRoamArea Around(Vector3 point)
        {
            GhostRoamArea nearest = null;
            float best = Reach;
            foreach (var area in areas)
            {
                float distance = Vector3.Distance(point, area.ClosestPoint(point));
                if (distance >= best) continue;
                best = distance;
                nearest = area;
            }
            return nearest;
        }

        /// <summary>A random spot inside it.</summary>
        public Vector3 RandomPoint()
        {
            var inside = new Vector3(Random.value - 0.5f, Random.value - 0.5f, Random.value - 0.5f);
            return transform.TransformPoint(center + Vector3.Scale(inside, size));
        }

        // The spot in it nearest to 'point' ('point' itself when it's inside).
        private Vector3 ClosestPoint(Vector3 point)
        {
            var local = transform.InverseTransformPoint(point);
            var half = size * 0.5f;
            local = Vector3.Max(center - half, Vector3.Min(center + half, local));
            return transform.TransformPoint(local);
        }

#if UNITY_EDITOR
        // Added in the Editor: fits itself over the object's parts, a metre in from their edges, 1.5 to 4 m above the
        // lowest of them.
        private void Reset()
        {
            var renderers = GetComponentsInChildren<Renderer>();
            var bounds = new Bounds();
            bool any = false;
            foreach (var shown in renderers)
            {
                if (shown is not MeshRenderer && shown is not SkinnedMeshRenderer) continue;
                if (any) bounds.Encapsulate(shown.bounds);
                else bounds = shown.bounds;
                any = true;
            }
            if (!any) return;

            var worldCenter = new Vector3(bounds.center.x, bounds.min.y + 2.75f, bounds.center.z);
            var worldSize = new Vector3(Mathf.Max(1f, bounds.size.x - 2f), 2.5f, Mathf.Max(1f, bounds.size.z - 2f));
            var scale = transform.lossyScale;
            center = transform.InverseTransformPoint(worldCenter);
            size = new Vector3(worldSize.x / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
                worldSize.y / Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
                worldSize.z / Mathf.Max(0.0001f, Mathf.Abs(scale.z)));
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.6f, 0.4f, 1f, 0.8f);
            Gizmos.DrawWireCube(center, size);
        }
#endif
    }
}

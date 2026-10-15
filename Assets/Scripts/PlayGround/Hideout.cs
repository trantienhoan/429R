using System.Collections.Generic;
using UnityEngine;

namespace Game.PlayGround
{
    /// <summary>
    /// A place a laughing pumpkin can hide in that the player and the bugs walk through, e.g. a patch of tall grass: its
    /// trigger collider is the hiding space, and a pumpkin inside it counts as out of sight. Laughing pumpkins look for
    /// these as well as bushes and trees. Shy grass switches it off while it has ducked (Covering). Goes on the patch's
    /// root, with a trigger Box Collider over the grass.
    /// Tools > 429 Game > PlayGround > Set Up PlayGround Field makes the PG_Grass_Hideout prefabs.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class Hideout : MonoBehaviour
    {
        private static readonly List<Hideout> all = new();
        private Collider space;

        /// <summary>Every hideout there is now.</summary>
        public static IReadOnlyList<Hideout> All => all;

        /// <summary>Whether it hides what's in it right now (shy grass that has ducked doesn't).</summary>
        public bool Covering { get; set; } = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => all.Clear();

        private void Awake()
        {
            space = GetComponent<Collider>();
        }

        private void OnEnable() => all.Add(this);

        private void OnDisable() => all.Remove(this);

        private Bounds Box => space != null && space.enabled ? space.bounds : new Bounds(transform.position, Vector3.zero);

        /// <summary>The floor in its middle, where a pumpkin sits to hide.</summary>
        public Vector3 Spot
        {
            get
            {
                var box = Box;
                return new Vector3(box.center.x, box.min.y, box.center.z);
            }
        }

        /// <summary>How wide (the narrow way) and how tall the hiding space is.</summary>
        public float Width => Mathf.Min(Box.size.x, Box.size.z);
        public float Height => Box.size.y;

        /// <summary>Is 'point' (a little above it, so a point on the floor counts) inside this hideout?</summary>
        public bool Contains(Vector3 point)
        {
            if (!Covering || space == null || !space.enabled) return false;
            var inside = point + Vector3.up * 0.2f;
            return Box.Contains(inside) && (space.ClosestPoint(inside) - inside).sqrMagnitude < 0.0001f;
        }

        /// <summary>Is 'point' inside any hideout (so it can't be seen)?</summary>
        public static bool Covers(Vector3 point)
        {
            foreach (var hideout in all)
            {
                if (hideout != null && hideout.Contains(point)) return true;
            }
            return false;
        }
    }
}

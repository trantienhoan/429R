using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Lair > Place Storage Room Webs (user, 2026-11-25: the storage room as the first pumpkin lair,
    /// concept Exports/Lair_Concept/concept_v1.png). Puts the lair webs and egg sacs (Prefabs/Lair) into the storage
    /// room: the room kit's "Mini" set in Prefabs/Bases/Bedroom_Kit_Base. Each piece is a child of the kit part it sits
    /// on, so it shows, folds away and comes back with that part (Room Kit): corner webs on the corner posts, clusters and
    /// round webs on the wall pieces, the threads with their sacs on the roof (it flies off first). Also the wall pieces
    /// of the lair plan (LairDecor): a pumpkin vine climbing the west wall from a bug hole, more gnawed bug holes low on
    /// the wainscoting, claw scratches on the door; these are put where a ray from inside the room meets the kit's own
    /// surface and are children of what they sit on (the door's scratches swing with the door). Re-runnable: the
    /// pieces it placed before ("Lair_" objects under the set's parts) are replaced. Prefab only, no scene change.
    /// Room facts: inner wall faces at x/z +-3.70, ceiling about 2.97 m, door north at x -2.25, window south at x +2.25.
    /// </summary>
    public static class LairPlaceStorageMenu
    {
        private const string Tag = "[Lair Storage Room]";
        private const string KitPath = "Assets/Prefabs/Bases/Bedroom_Kit_Base.prefab";
        private const string SetName = "Mini";
        private const string LairFolder = "Assets/Prefabs/Lair/";
        internal const float Inner = 3.70f;
        internal const float Top = 2.95f;

        internal struct Spot
        {
            public string prefab, part;
            public Vector3 at, facing;
            public float size;
            public Spot(string prefab, string part, Vector3 at, Vector3 facing, float size)
            {
                this.prefab = prefab;
                this.part = part;
                this.at = at;
                this.facing = facing;
                this.size = size;
            }
        }

        // A web across a corner: its hub this far out along the corner's middle line, under the ceiling, facing the room.
        internal static Spot Corner(float x, float z, float size, float height = Top)
        {
            var into = new Vector3(-Mathf.Sign(x), 0f, -Mathf.Sign(z)).normalized;
            // Its width is 0.884 m at size 1; across a square corner the gap is twice the distance out.
            float out_ = 0.884f * size * 0.5f;
            return new Spot("Lair_Web_Corner", "BR_Kit_Corner", new Vector3(x, height, z) + into * out_, into, size);
        }

        private static IEnumerable<Spot> Spots()
        {
            // The four top corners (the concept's webs in every corner).
            yield return Corner(Inner, Inner, 1.25f);
            yield return Corner(-Inner, Inner, 1.1f);
            yield return Corner(Inner, -Inner, 1.15f);
            yield return Corner(-Inner, -Inner, 0.95f);
            // A sagging web lower across the south-west corner.
            var sw = new Vector3(1f, 0f, 1f).normalized;
            yield return new Spot("Lair_Web_Sheet", "BR_Kit_Corner", new Vector3(-Inner, 2.35f, -Inner) + sw * 0.66f, sw, 1.0f);
            // Sacs on threads from the ceiling near the corners (on the roof).
            yield return new Spot("Lair_Web_Strand_Sac", "Roof", new Vector3(3.05f, Top, 3.2f), Vector3.forward, 1.2f);
            yield return new Spot("Lair_Web_Strand_Sac", "Roof", new Vector3(2.55f, Top, 3.45f), Vector3.right, 0.85f);
            yield return new Spot("Lair_Web_Strand_Sac", "Roof", new Vector3(-3.2f, Top, 3.05f), Vector3.left, 1.0f);
            yield return new Spot("Lair_Web_Strand_Sac", "Roof", new Vector3(3.3f, Top, -2.95f), Vector3.back, 1.1f);
            // Sac clusters high on the east and west walls.
            yield return new Spot("Lair_Egg_Sac_Cluster", "BR_Kit_Wall_3m", new Vector3(Inner - 0.03f, 2.35f, 2.6f), Vector3.left, 1.2f);
            yield return new Spot("Lair_Egg_Sac_Cluster", "BR_Kit_Wall_3m", new Vector3(-Inner + 0.03f, 2.4f, -2.6f), Vector3.right, 1.0f);
            // Round webs above the boxes (north wall, right) and the shelves (west wall).
            yield return new Spot("Lair_Web_Round", "BR_Kit_Wall_3m", new Vector3(2.0f, 2.2f, Inner - 0.03f), Vector3.back, 0.9f);
            yield return new Spot("Lair_Web_Round", "BR_Kit_Wall_1p5m", new Vector3(-Inner + 0.03f, 2.3f, 0.4f), Vector3.right, 0.8f);
        }

        // Pieces put on the kit's own surface: a ray from 'from' along 'dir' finds it (the piece's +Z = the surface's
        // facing). 'floor' puts the piece's pivot down at the floor under the hit (the vine grows from there). 'spin'
        // turns it round its facing.
        internal struct Surface
        {
            public string prefab;
            public Vector3 from, dir;
            public float size, spin;
            public bool floor;
            public Surface(string prefab, Vector3 from, Vector3 dir, float size, float spin = 0f, bool floor = false)
            {
                this.prefab = prefab;
                this.from = from;
                this.dir = dir;
                this.size = size;
                this.spin = spin;
                this.floor = floor;
            }
        }

        private static IEnumerable<Surface> Surfaces()
        {
            // The pumpkin vine up the west wall, from a bug hole at its root.
            yield return new Surface("Lair_Bug_Hole", new Vector3(-3f, 0.12f, 2.12f), Vector3.left, 1.1f, 20f);
            // Bug holes low on the wainscoting, where no furniture stands.
            yield return new Surface("Lair_Bug_Hole", new Vector3(-3f, 0.42f, -1.95f), Vector3.left, 0.8f, 75f);
            yield return new Surface("Lair_Bug_Hole", new Vector3(3f, 0.35f, 1.63f), Vector3.right, 1.1f, 140f);   // in the gap between the shelf and the box tower
            yield return new Surface("Lair_Bug_Hole", new Vector3(0.45f, 0.28f, 3f), Vector3.forward, 0.9f, 200f);
            yield return new Surface("Lair_Bug_Hole", new Vector3(0.4f, 0.5f, -3f), Vector3.back, 0.75f, 300f);
            // (The claw scratches on the door are out: user 2026-11-26, "they look all bad".)
        }

        [MenuItem("Tools/429 Game/Lair/Place Storage Room Webs")]
        public static void Place() => PlaceInSet(SetName, Spots(), Surfaces(), Tag, "the storage room");

        // Puts the pieces into one set of the room kit (Mini = the storage room, Big = the bedroom): the Spots on the
        // named parts, the Surfaces where a ray meets the set's own faces. Earlier Lair_ pieces in that set go first.
        internal static void PlaceInSet(string SetName, IEnumerable<Spot> spots, IEnumerable<Surface> surfaces, string Tag, string room, string KitPath = KitPath)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError(Tag + " Stop play mode first; nothing changed.");
                return;
            }
            var root = PrefabUtility.LoadPrefabContents(KitPath);
            try
            {
                var set = root.transform.Find(SetName);
                if (set == null)
                {
                    Debug.LogError($"{Tag} {KitPath} has no '{SetName}' set; nothing changed.");
                    return;
                }
                // Earlier placements go (the outermost "Lair_" objects anywhere under the set's parts).
                int removed = 0;
                var old = new List<GameObject>();
                foreach (Transform part in set)
                    foreach (var t in part.GetComponentsInChildren<Transform>(true))
                        if (t.name.StartsWith("Lair_") && (t.parent == null || !t.parent.name.StartsWith("Lair_"))) old.Add(t.gameObject);
                foreach (var o in old)
                {
                    if (o == null) continue;
                    Object.DestroyImmediate(o);
                    removed++;
                }
                var placed = new List<string>();
                foreach (var s in spots)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LairFolder + s.prefab + ".prefab");
                    if (prefab == null)
                    {
                        Debug.LogError($"{Tag} {LairFolder}{s.prefab}.prefab is missing (run Tools > 429 Game > Lair > Set Up Lair Webs).");
                        continue;
                    }
                    var part = Nearest(set, s.part, root.transform.TransformPoint(s.at));
                    if (part == null)
                    {
                        Debug.LogError($"{Tag} The '{SetName}' set has no {s.part}; {s.prefab} skipped.");
                        continue;
                    }
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                    go.transform.SetPositionAndRotation(root.transform.TransformPoint(s.at),
                        root.transform.rotation * Quaternion.LookRotation(s.facing, Vector3.up));
                    go.transform.localScale = Vector3.one * s.size;
                    go.transform.SetParent(part, true);
                    placed.Add($"{s.prefab} on {part.name}");
                }
                foreach (var s in surfaces)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LairFolder + s.prefab + ".prefab");
                    if (prefab == null)
                    {
                        Debug.LogError($"{Tag} {LairFolder}{s.prefab}.prefab is missing (run Tools > 429 Game > Lair > Set Up Lair Story Props).");
                        continue;
                    }
                    var from = root.transform.TransformPoint(s.from);
                    var dir = root.transform.TransformDirection(s.dir);
                    if (!RayHit(set, from, dir, out var point, out var normal, out var on))
                    {
                        Debug.LogError($"{Tag} Nothing of the '{SetName}' set from {s.from} along {s.dir}; {s.prefab} skipped.");
                        continue;
                    }
                    if (s.floor) point = new Vector3(point.x, root.transform.position.y, point.z);
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                    go.transform.SetPositionAndRotation(point, Quaternion.LookRotation(normal, root.transform.up) * Quaternion.Euler(0f, 0f, s.spin));
                    go.transform.localScale = Vector3.one * s.size;
                    go.transform.SetParent(on, true);
                    placed.Add($"{s.prefab} on {on.name}");
                }
                PrefabUtility.SaveAsPrefabAsset(root, KitPath);
                Debug.Log($"{Tag} {placed.Count} pieces placed in {room} ({removed} earlier ones replaced): " +
                          string.Join("; ", placed) + ". Prefab only, no scene change.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("Tools/429 Game/Lair/Place Storage Room Webs", true)]
        private static bool CanPlace() => !EditorApplication.isPlayingOrWillChangePlaymode;

        // The nearest front face of the set's own meshes (not the Lair_ pieces) along a ray: where, its facing, and the
        // object it belongs to.
        private static bool RayHit(Transform set, Vector3 from, Vector3 dir, out Vector3 point, out Vector3 normal, out Transform on)
        {
            point = normal = Vector3.zero;
            on = null;
            float best = float.MaxValue;
            dir.Normalize();
            foreach (var mf in set.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || InLair(mf.transform, set)) continue;
                if (mf.GetComponent<MeshRenderer>() == null) continue;
                var vertices = mf.sharedMesh.vertices;
                var tris = mf.sharedMesh.triangles;
                var m = mf.transform.localToWorldMatrix;
                for (int i = 0; i < tris.Length; i += 3)
                {
                    var a = m.MultiplyPoint3x4(vertices[tris[i]]);
                    var b = m.MultiplyPoint3x4(vertices[tris[i + 1]]);
                    var c = m.MultiplyPoint3x4(vertices[tris[i + 2]]);
                    var face = Vector3.Cross(b - a, c - a);
                    if (Vector3.Dot(face, dir) >= 0f) continue;          // only faces turned toward the ray
                    if (!RayTriangle(from, dir, a, b, c, out float d) || d >= best) continue;
                    best = d;
                    point = from + dir * d;
                    normal = face.normalized;
                    on = mf.transform;
                }
            }
            return on != null;
        }

        private static bool InLair(Transform t, Transform set)
        {
            for (; t != null && t != set; t = t.parent)
                if (t.name.StartsWith("Lair_")) return true;
            return false;
        }

        // Moller-Trumbore.
        private static bool RayTriangle(Vector3 from, Vector3 dir, Vector3 a, Vector3 b, Vector3 c, out float distance)
        {
            distance = 0f;
            var e1 = b - a;
            var e2 = c - a;
            var p = Vector3.Cross(dir, e2);
            float det = Vector3.Dot(e1, p);
            if (Mathf.Abs(det) < 1e-9f) return false;
            float inv = 1f / det;
            var s = from - a;
            float u = Vector3.Dot(s, p) * inv;
            if (u < 0f || u > 1f) return false;
            var q = Vector3.Cross(s, e1);
            float v = Vector3.Dot(dir, q) * inv;
            if (v < 0f || u + v > 1f) return false;
            distance = Vector3.Dot(e2, q) * inv;
            return distance > 0f;
        }

        // The set's part with this name nearest to a point.
        private static Transform Nearest(Transform set, string name, Vector3 point)
        {
            Transform best = null;
            float bestDistance = float.MaxValue;
            foreach (Transform part in set)
            {
                if (part.name != name) continue;
                var look = part.GetComponentInChildren<Renderer>();
                var middle = look != null ? look.bounds.center : part.position;
                float d = (middle - point).sqrMagnitude;
                if (d >= bestDistance) continue;
                bestDistance = d;
                best = part;
            }
            return best;
        }
    }
}

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Rooms > Fit Kit Room Doors (user, 2026-11-20: "the door leaves a big gap with the frame, Player
    /// can look outside of the room", then with a picture: "this is how a door should be set, the dark metal should
    /// overlap the frame inside the room"). The kit leaves stood behind the frame's trim, inside the wall's hole, so round
    /// their edges one saw past the trim into the hole and outside. Now each closed door (Chained, Locked, Shortcut,
    /// Double, X) is hung on the room side: its planks just in front of the frame's face, at its own width (it already
    /// reaches past the frame's inner edges, so its sides and hinges lie over the frame) and a little taller so its top
    /// overlaps the frame too (at least Overlap past each inner edge). Its root moves (and stretches), so its box and parts
    /// go with it. The ajar Secret_Door is left as it is. Running it again changes nothing. The Build ... Kit Room menus
    /// run it too.
    /// </summary>
    public static class KitDoorFitMenu
    {
        private static readonly string[] Rooms = { "Assets/Prefabs/Bases/Bedroom_Kit_Base.prefab", "Assets/Prefabs/Bases/Livingroom_Kit_Base.prefab" };
        private static readonly string[] Doors = { "Chained_Door", "Locked_Door", "Shortcut_Door", "Double_Door", "X_Door" };
        // How far the leaf reaches over the frame past its inner edges (sides and top), and the gap to the frame's face.
        private const float Overlap = 0.04f;
        private const float Gap = 0.003f;

        [MenuItem("Tools/429 Game/Rooms/Fit Kit Room Doors")]
        public static void FitAll()
        {
            var notes = new List<string>();
            foreach (var path in Rooms)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (Fit(root, notes) > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            Debug.Log("[Kit Doors] " + (notes.Count > 0 ? string.Join("; ", notes) : "no doors found"));
        }

        /// <summary>Hangs every closed door under Room on the room side of its frame; how many it fitted.</summary>
        public static int Fit(GameObject room, List<string> notes)
        {
            int fitted = 0;
            foreach (var door in room.GetComponentsInChildren<Transform>(true))
            {
                if (System.Array.IndexOf(Doors, door.name) < 0 || door.parent == null) continue;
                var way = door.parent;
                var frame = way.GetComponent<MeshFilter>();
                if (frame == null || frame.sharedMesh == null) continue;

                // The leaf in the door's own space (the doorway's, with the door at its pivot, unturned, unscaled).
                var leaf = new List<Vector3>();
                foreach (var mf in door.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null) continue;
                    foreach (var v in mf.sharedMesh.vertices) leaf.Add(door.InverseTransformPoint(mf.transform.TransformPoint(v)));
                }
                if (leaf.Count == 0) continue;
                var lo = leaf[0];
                var hi = leaf[0];
                foreach (var p in leaf) { lo = Vector3.Min(lo, p); hi = Vector3.Max(hi, p); }
                float mid = (lo.x + hi.x) / 2f;

                // The frame (doorway space): its inner edges are the vertices nearest the leaf's middle; the trim is what
                // stands there, and the room is on the side where the trim stands out as far as the frame does.
                var pts = new List<Vector3>();
                foreach (var v in frame.sharedMesh.vertices) pts.Add(way.InverseTransformPoint(frame.transform.TransformPoint(v)));
                float left = float.MinValue, right = float.MaxValue, top = float.MaxValue;
                float frameMin = float.MaxValue, frameMax = float.MinValue;
                foreach (var p in pts)
                {
                    frameMin = Mathf.Min(frameMin, p.z);
                    frameMax = Mathf.Max(frameMax, p.z);
                    if (p.x <= mid) left = Mathf.Max(left, p.x);
                    else right = Mathf.Min(right, p.x);
                }
                float trimMin = float.MaxValue, trimMax = float.MinValue;
                foreach (var p in pts)
                {
                    bool edge = Mathf.Abs(p.x - left) < 0.005f || Mathf.Abs(p.x - right) < 0.005f;
                    if (p.x > left - 0.01f && p.x < right + 0.01f && p.y > 1f) top = Mathf.Min(top, p.y);
                    if (!edge) continue;
                    trimMin = Mathf.Min(trimMin, p.z);
                    trimMax = Mathf.Max(trimMax, p.z);
                }
                if (left == float.MinValue || right == float.MaxValue || top == float.MaxValue || trimMin > trimMax)
                {
                    notes.Add(way.name + "/" + door.name + ": no frame opening found, left as it is");
                    continue;
                }
                float side = Mathf.Abs(trimMax - frameMax) <= Mathf.Abs(trimMin - frameMin) ? 1f : -1f;
                float face = side > 0f ? trimMax : trimMin;

                // The planks' back (the room side's far face): the 3rd percentile of depth, so a ring or pin poking out
                // behind doesn't count.
                var depth = new List<float>();
                foreach (var p in leaf) depth.Add(p.z * side);
                depth.Sort();
                float back = depth[Mathf.Clamp(Mathf.FloorToInt(depth.Count * 0.03f), 0, depth.Count - 1)] * side;

                float sx = Mathf.Max(1f, (right - left + 2f * Overlap) / (hi.x - lo.x));
                float sy = Mathf.Max(1f, (top + Overlap - lo.y) / (hi.y - lo.y));
                door.localRotation = Quaternion.identity;
                door.localScale = new Vector3(sx, sy, 1f);
                door.localPosition = new Vector3((left + right) / 2f - sx * mid, lo.y - sy * lo.y, face + side * Gap - back);
                notes.Add($"{way.name}/{door.name} on the {(side > 0 ? "+z" : "-z")} side, {face + side * Gap - back:F3} m out, x{sx:F3} y{sy:F3} (opening {right - left:F2} x {top:F2} m)");
                fitted++;
            }
            return fitted;
        }
    }
}

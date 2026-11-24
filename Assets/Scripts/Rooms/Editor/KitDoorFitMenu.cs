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
    /// go with it. Running it again changes nothing. The Build ... Kit Room menus run it too.
    /// The bedroom's Secret_Door (the storage room's horseshoe shortcut door) is set as the user set it in
    /// Exports/Remodel/Bedroom_Kit.blend (2026-11-24, with pictures: "this is how I want to set the door"): see FitSecret.
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
            return fitted + FitSecret(room, notes);
        }

        // The user's own placement of the secret leaf in Bedroom_Kit.blend (BR_Kit_Door_Secret on BR_Kit_Door_Wide,
        // 2026-11-24): shut, it stands IN the frame's opening, 9 mm off the floor, 5.1 mm from the latch-side jamb, its
        // hinge side over the hinge-side jamb (2.8 cm), its planks across the frame's face and its battens and iron
        // 3.98 cm out past the face into the room.
        private const float SecretFloor = 0.009f;
        private const float SecretLatchGap = 0.0051f;
        private const float SecretOut = 0.0398f;

        [MenuItem("Tools/429 Game/Rooms/Fit Secret Door")]
        public static void FitSecretMenu()
        {
            const string path = "Assets/Prefabs/Bases/Bedroom_Kit_Base.prefab";
            var notes = new List<string>();
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (FitSecret(root, notes) > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            Debug.Log("[Kit Doors] " + (notes.Count > 0 ? string.Join("; ", notes) : "no Secret_Door found"));
        }

        /// <summary>
        /// Sets every Secret_Door as the user set it (above). It starts ajar and its FSM shuts it to rotation 0 about its
        /// root (the hinge), so only the root moves: its shut pose lands where the user put it. Running it again changes
        /// nothing.
        /// </summary>
        public static int FitSecret(GameObject room, List<string> notes)
        {
            int fitted = 0;
            foreach (var door in room.GetComponentsInChildren<Transform>(true))
            {
                if (door.name != "Secret_Door" || door.parent == null) continue;
                var way = door.parent;
                var frame = way.GetComponent<MeshFilter>();
                if (frame == null || frame.sharedMesh == null) continue;

                // The shut leaf in the doorway's space (what shows of it).
                var ajar = door.localRotation;
                door.localRotation = Quaternion.identity;
                var lo = Vector3.positiveInfinity;
                var hi = Vector3.negativeInfinity;
                foreach (var mf in door.GetComponentsInChildren<MeshFilter>(true))
                {
                    var mr = mf.GetComponent<MeshRenderer>();
                    if (mf.sharedMesh == null || mr == null || !mr.enabled) continue;
                    foreach (var v in mf.sharedMesh.vertices)
                    {
                        var p = way.InverseTransformPoint(mf.transform.TransformPoint(v));
                        lo = Vector3.Min(lo, p);
                        hi = Vector3.Max(hi, p);
                    }
                }
                door.localRotation = ajar;
                if (lo.x > hi.x) continue;

                // The opening's jambs and the frame's face on the room side (where the trim stands out as far as the
                // frame does), as in Fit.
                float mid = (lo.x + hi.x) / 2f;
                float left = float.MinValue, right = float.MaxValue, frameMin = float.MaxValue, frameMax = float.MinValue;
                foreach (var v in frame.sharedMesh.vertices)
                {
                    frameMin = Mathf.Min(frameMin, v.z);
                    frameMax = Mathf.Max(frameMax, v.z);
                    if (v.x <= mid) left = Mathf.Max(left, v.x);
                    else right = Mathf.Min(right, v.x);
                }
                float trimMin = float.MaxValue, trimMax = float.MinValue;
                foreach (var v in frame.sharedMesh.vertices)
                {
                    if (Mathf.Abs(v.x - left) >= 0.005f && Mathf.Abs(v.x - right) >= 0.005f) continue;
                    trimMin = Mathf.Min(trimMin, v.z);
                    trimMax = Mathf.Max(trimMax, v.z);
                }
                if (left == float.MinValue || right == float.MaxValue || trimMin > trimMax)
                {
                    notes.Add(way.name + "/" + door.name + ": no frame opening found, left as it is");
                    continue;
                }
                float side = Mathf.Abs(trimMax - frameMax) <= Mathf.Abs(trimMin - frameMin) ? 1f : -1f;
                float face = side > 0f ? trimMax : trimMin;

                // The hinge (the root) is on one jamb's side; the latch side keeps its small gap.
                bool hingeOnRight = Mathf.Abs(door.localPosition.x - right) < Mathf.Abs(door.localPosition.x - left);
                float x = hingeOnRight ? left + SecretLatchGap - lo.x : right - SecretLatchGap - hi.x;
                float y = SecretFloor - lo.y;
                float z = side > 0f ? face + SecretOut - hi.z : face - SecretOut - lo.z;
                var move = new Vector3(x, y, z);
                if (move.sqrMagnitude < 1e-8f) continue;
                door.localPosition += move;
                notes.Add($"{way.name}/{door.name} moved {move.x:F4}, {move.y:F4}, {move.z:F4} (shut: in the {right - left:F3} m opening, {SecretOut * 100f:F1} cm out past the frame)");
                fitted++;
            }
            return fitted;
        }

        [MenuItem("Tools/429 Game/Rooms/Fit Secret Door", true)]
        private static bool CanFitSecret() => !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}

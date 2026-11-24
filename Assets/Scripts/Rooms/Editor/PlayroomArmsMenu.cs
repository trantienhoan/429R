using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Rooms > Fit Playroom Arms (user, 2026-11-24: "check the floor in 4 arms of jenga playroom, fix it,
    /// fix the wall at the end of those 4 arms also"). On the living room kit's playroom (the Play set, the Jenga room):
    /// - Each arm's floor (Floor_N/E/S/W) is a whole 3 m floor tile on the room floor's 3 m grid: from the room's 9 m floor
    ///   out along the arm, 3 m wide, unstretched, so its planks run on from the room's floor (it was a tile squeezed to
    ///   the arm, 50% and 85%). It reaches under the arm's walls, as the room's floor reaches under the room's.
    /// - The wall at each arm's end (the old room's breakable wall, Brick_Wall_Breakable) wears its wallpaper the way the
    ///   kit's walls do: the same size of stripes and the panel band at the same height (its own UVs lay the picture over
    ///   3.58 m, the kit's over 3 m). A variant of the wallpaper material (Livingroom_Wall_Spooky_Arm_End) with only its
    ///   tiling and offset changed, worked out from the wall's own UVs. Its broken pieces keep their old look.
    /// - The ceiling (the old breakable Livingroom_Base_Z_Roof_Sub) sits on the walls: its underside on their tops (user,
    ///   2026-11-24, "check the ceiling in the jenga playroom arms too": it floated 8 cm above every wall, a dark groove
    ///   along their tops; it had been lowered to its lamp panel, which hangs 8 cm below the rest). The lamp panel now
    ///   hangs 8 cm below, like the living room's lamp.
    /// The floors showing at all is RoomKit's (they never showed). Running it again changes nothing more. Build Living Room
    /// Kit Room runs it too. The prefab and the material only: no scene change.
    /// </summary>
    public static class PlayroomArmsMenu
    {
        private const string Tag = "[Playroom Arms]";
        private const string Kit = "Assets/Prefabs/Bases/Livingroom_Kit_Base.prefab";
        private const string Paper = "Assets/Materials/Livingroom_Wall_Spooky.mat";
        private const string EndPaper = "Assets/Materials/Livingroom_Wall_Spooky_Arm_End.mat";
        private const string Tile = "LR_Kit_Floor_3m";
        private const float Grid = 3f;
        private static readonly string[] Floors = { "Floor_N", "Floor_E", "Floor_S", "Floor_W" };

        [MenuItem("Tools/429 Game/Rooms/Fit Playroom Arms")]
        public static void Run()
        {
            var notes = new List<string>();
            var root = PrefabUtility.LoadPrefabContents(Kit);
            try
            {
                if (Fit(root, notes) > 0) PrefabUtility.SaveAsPrefabAsset(root, Kit);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            Debug.Log(Tag + " " + (notes.Count > 0 ? string.Join("; ", notes) : "nothing to do") + ". No scene change.");
        }

        /// <summary>Fits the playroom's arm floors and end walls; how many things it changed.</summary>
        public static int Fit(GameObject room, List<string> notes)
        {
            var play = room.transform.Find("Play");
            var floor = room.transform.Find("Floor");
            if (play == null || floor == null)
            {
                notes.Add("no Play set or Floor");
                return 0;
            }
            return FitFloors(play, floor, notes) + FitEnds(play, notes) + FitCeiling(play, notes);
        }

        // The ceiling's underside (its widest downward level) on the kit walls' tops.
        private static int FitCeiling(Transform play, List<string> notes)
        {
            var roof = play.Find("Livingroom_Base_Z_Roof_Sub");
            var rf = roof != null ? roof.GetComponent<MeshFilter>() : null;
            float top = float.MinValue;
            foreach (var mf in play.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || !mf.sharedMesh.name.StartsWith("LR_Kit_Wall")) continue;
                top = Mathf.Max(top, play.InverseTransformPoint(mf.transform.TransformPoint(new Vector3(0f, mf.sharedMesh.bounds.max.y, 0f))).y);
            }
            if (rf == null || rf.sharedMesh == null || top == float.MinValue)
            {
                notes.Add("no playroom ceiling or walls");
                return 0;
            }
            // Area of the downward faces at each height (mm), in the set's space.
            var area = new Dictionary<int, float>();
            using (var data = Mesh.AcquireReadOnlyMeshData(rf.sharedMesh))
            {
                var md = data[0];
                var pos = new Unity.Collections.NativeArray<Vector3>(md.vertexCount, Unity.Collections.Allocator.Temp);
                md.GetVertices(pos);
                var p = new Vector3[pos.Length];
                for (int i = 0; i < p.Length; i++) p[i] = play.InverseTransformPoint(roof.TransformPoint(pos[i]));
                pos.Dispose();
                for (int s = 0; s < md.subMeshCount; s++)
                {
                    var idx = new Unity.Collections.NativeArray<int>(md.GetSubMesh(s).indexCount, Unity.Collections.Allocator.Temp);
                    md.GetIndices(idx, s);
                    for (int t = 0; t + 2 < idx.Length; t += 3)
                    {
                        var a = p[idx[t]];
                        var cross = Vector3.Cross(p[idx[t + 1]] - a, p[idx[t + 2]] - a);
                        if (cross.y > -0.9f * cross.magnitude) continue; // faces down
                        int mm = Mathf.RoundToInt(a.y * 1000f);
                        area[mm] = (area.TryGetValue(mm, out var sum) ? sum : 0f) + cross.magnitude / 2f;
                    }
                    idx.Dispose();
                }
            }
            if (area.Count == 0) return 0;
            int widest = 0;
            float most = -1f;
            foreach (var kv in area)
                if (kv.Value > most) { most = kv.Value; widest = kv.Key; }
            float shift = top - widest / 1000f;
            if (Mathf.Abs(shift) < 0.0005f) return 0;
            roof.localPosition += roof.parent.InverseTransformVector(play.TransformVector(Vector3.up * shift));
            notes.Add($"ceiling moved {shift * 100f:F1} cm: its underside ({most:F0} m2) on the walls' tops at {top:F2} m");
            return 1;
        }

        // Each arm's floor tile: unstretched, on the room floor's 3 m grid, from the room floor's edge out along the arm.
        private static int FitFloors(Transform play, Transform floor, List<string> notes)
        {
            // How far the room's floor reaches (in the set's space; the room floor's tiles are on the same grid).
            float half = 0f;
            foreach (var mf in floor.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var b = mf.sharedMesh.bounds;
                foreach (var c in new[] { b.min, b.max })
                {
                    var p = play.InverseTransformPoint(mf.transform.TransformPoint(c));
                    half = Mathf.Max(half, Mathf.Abs(p.x), Mathf.Abs(p.z));
                }
            }
            int changed = 0;
            foreach (var name in Floors)
            {
                var part = play.Find(name);
                if (part == null) continue;
                MeshFilter tile = null;
                foreach (var mf in part.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.sharedMesh != null && mf.sharedMesh.name == Tile) tile = mf;
                if (tile == null) continue;
                // Along the arm: the part's side of the room; across it: centred.
                bool alongX = Mathf.Abs(part.localPosition.x) > Mathf.Abs(part.localPosition.z);
                float sign = Mathf.Sign(alongX ? part.localPosition.x : part.localPosition.z);
                float a0 = sign > 0f ? half : -half - Grid;
                var min = alongX ? new Vector3(a0, 0f, -Grid / 2f) : new Vector3(-Grid / 2f, 0f, a0);
                // The tile's pivot so its mesh lands there (unturned, unscaled), in the part's space.
                var pivot = min - tile.sharedMesh.bounds.min;
                pivot.y = 0f;
                var local = part.InverseTransformPoint(play.TransformPoint(pivot));
                var t = tile.transform;
                if ((t.localPosition - local).sqrMagnitude < 1e-8f && (t.localScale - Vector3.one).sqrMagnitude < 1e-8f &&
                    t.localRotation == Quaternion.identity) continue;
                t.localRotation = Quaternion.identity;
                t.localScale = Vector3.one;
                t.localPosition = local;
                notes.Add($"{name}: a whole {Grid} m tile, {(alongX ? "x" : "z")} {a0:F2}..{a0 + Grid:F2}");
                changed++;
            }
            return changed;
        }

        // Each arm's end wall: the wallpaper variant that lays the picture as the kit's walls do.
        private static int FitEnds(Transform play, List<string> notes)
        {
            var paper = AssetDatabase.LoadAssetAtPath<Material>(Paper);
            if (paper == null)
            {
                notes.Add("no " + Paper);
                return 0;
            }
            int changed = 0;
            Material variant = null;
            foreach (Transform end in play)
            {
                if (!end.name.StartsWith("Brick_End_")) continue;
                foreach (var r in end.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    var mats = r.sharedMaterials;
                    int slot = -1;
                    for (int i = 0; i < mats.Length; i++)
                        if (mats[i] == paper || (mats[i] != null && AssetDatabase.GetAssetPath(mats[i]) == EndPaper)) slot = i;
                    if (slot < 0) continue;
                    if (variant == null)
                    {
                        if (!FrontMapping(mf, end, out float a, out float b, out float c, out float d))
                        {
                            notes.Add(end.name + ": its wall's front face not found, left as it is");
                            continue;
                        }
                        variant = EndMaterial(paper, a, b, c, d, notes);
                    }
                    if (mats[slot] == variant) continue;
                    mats[slot] = variant;
                    r.sharedMaterials = mats;
                    notes.Add(end.name + ": " + variant.name);
                    changed++;
                }
            }
            return changed;
        }

        // The wall's front (toward the room: the end's +z) wallpaper mapping in the end's space: u = a x + b, v = c y + d,
        // from the triangles lying on that face.
        private static bool FrontMapping(MeshFilter mf, Transform end, out float a, out float b, out float c, out float d)
        {
            a = b = c = d = 0f;
            var room = end.parent;
            bool roomAhead = end.InverseTransformPoint(room.position).z > 0f;
            using (var data = Mesh.AcquireReadOnlyMeshData(mf.sharedMesh))
            {
                var md = data[0];
                int n = md.vertexCount;
                var pos = new Unity.Collections.NativeArray<Vector3>(n, Unity.Collections.Allocator.Temp);
                var uv = new Unity.Collections.NativeArray<Vector2>(n, Unity.Collections.Allocator.Temp);
                md.GetVertices(pos);
                md.GetUVs(0, uv);
                var p = new Vector3[n];
                float front = roomAhead ? float.MinValue : float.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    p[i] = end.InverseTransformPoint(mf.transform.TransformPoint(pos[i]));
                    front = roomAhead ? Mathf.Max(front, p[i].z) : Mathf.Min(front, p[i].z);
                }
                var used = new HashSet<int>();
                for (int s = 0; s < md.subMeshCount; s++)
                {
                    var tris = new Unity.Collections.NativeArray<int>(md.GetSubMesh(s).indexCount, Unity.Collections.Allocator.Temp);
                    md.GetIndices(tris, s);
                    for (int t = 0; t + 2 < tris.Length; t += 3)
                    {
                        bool onFront = true;
                        for (int k = 0; k < 3; k++) onFront &= Mathf.Abs(p[tris[t + k]].z - front) < 0.002f;
                        if (!onFront) continue;
                        for (int k = 0; k < 3; k++) used.Add(tris[t + k]);
                    }
                    tris.Dispose();
                }
                double sx = 0, su = 0, sxx = 0, sxu = 0, sy = 0, sv = 0, syy = 0, syv = 0;
                foreach (var i in used)
                {
                    sx += p[i].x; su += uv[i].x; sxx += p[i].x * p[i].x; sxu += p[i].x * uv[i].x;
                    sy += p[i].y; sv += uv[i].y; syy += p[i].y * p[i].y; syv += p[i].y * uv[i].y;
                }
                int count = used.Count;
                pos.Dispose();
                uv.Dispose();
                double dx = count * sxx - sx * sx, dy = count * syy - sy * sy;
                if (count < 3 || System.Math.Abs(dx) < 1e-9 || System.Math.Abs(dy) < 1e-9) return false;
                a = (float)((count * sxu - sx * su) / dx);
                b = (float)((su - a * sx) / count);
                c = (float)((count * syv - sy * sv) / dy);
                d = (float)((sv - c * sy) / count);
                return Mathf.Abs(a) > 1e-4f && Mathf.Abs(c) > 1e-4f;
            }
        }

        // The variant: the kit's walls lay the picture once over 3 m across (u = -x / 3) and floor to top (v = y / 3).
        private static Material EndMaterial(Material paper, float a, float b, float c, float d, List<string> notes)
        {
            float metres = KitWallpaperMenu.LivingroomPaperMetres;
            var tiling = new Vector2(-1f / (metres * a), 1f / (metres * c));
            var offset = new Vector2(0.5f - tiling.x * b, -tiling.y * d);
            var variant = AssetDatabase.LoadAssetAtPath<Material>(EndPaper);
            if (variant == null)
            {
                variant = new Material(paper) { name = "Livingroom_Wall_Spooky_Arm_End" };
                variant.parent = paper;
                variant.SetTextureScale("_BaseMap", tiling);
                variant.SetTextureOffset("_BaseMap", offset);
                AssetDatabase.CreateAsset(variant, EndPaper);
                notes.Add($"made {EndPaper} (tiling {tiling.x:F3} x {tiling.y:F3}, offset {offset.x:F3}, {offset.y:F3})");
            }
            else if ((variant.GetTextureScale("_BaseMap") - tiling).sqrMagnitude > 1e-8f ||
                     (variant.GetTextureOffset("_BaseMap") - offset).sqrMagnitude > 1e-8f)
            {
                variant.SetTextureScale("_BaseMap", tiling);
                variant.SetTextureOffset("_BaseMap", offset);
                EditorUtility.SetDirty(variant);
                AssetDatabase.SaveAssetIfDirty(variant);
                notes.Add($"set {EndPaper} (tiling {tiling.x:F3} x {tiling.y:F3}, offset {offset.x:F3}, {offset.y:F3})");
            }
            return variant;
        }

        [MenuItem("Tools/429 Game/Rooms/Fit Playroom Arms", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}

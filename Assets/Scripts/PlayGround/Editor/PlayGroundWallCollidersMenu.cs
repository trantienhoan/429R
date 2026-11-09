using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.PlayGround.Editor
{
    /// <summary>
    /// Tools > 429 Game > PlayGround > Fit PlayGround Wall Colliders: each wall bone of Play_Ground_Z (Bone_1 ... Bone_5)
    /// gets a Bone Span Box, so its wall box always reaches from its bone to the bone at the wall's other end (Bone_1_1,
    /// Bone_2_1, Bone_41 ...), whatever length the model gives the wall and while COME_2 / COME3 grow it. The post box at
    /// the far end moves onto that end's bone, so it goes where the post goes. The gaps at both ends are kept as they
    /// are now. Safe to run again.
    /// </summary>
    public static class PlayGroundWallCollidersMenu
    {
        private const string PrefabPath = "Assets/Prefabs/Bases/Play_Ground_Z.prefab";
        private static readonly (string wall, string end)[] Walls =
        {
            ("Bone_1", "Bone_1_1"), ("Bone_2", "Bone_2_1"), ("Bone_3", "Bone_3_1"), ("Bone_4", "Bone_41"), ("Bone_5", "Bone_51"),
        };

        [MenuItem("Tools/429 Game/PlayGround/Fit PlayGround Wall Colliders")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[PlayGround Wall Colliders] Stop play mode first."); return; }
            var notes = new List<string>();
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                foreach (var (wallName, endName) in Walls)
                {
                    var bone = Find(root.transform, wallName);
                    var end = bone != null ? Find(bone, endName) : null;
                    if (bone == null || end == null) { notes.Add($"no {wallName}/{endName}"); continue; }

                    var span = bone.InverseTransformPoint(end.position);
                    int axis = 0;
                    if (Mathf.Abs(span.y) > Mathf.Abs(span[axis])) axis = 1;
                    if (Mathf.Abs(span.z) > Mathf.Abs(span[axis])) axis = 2;
                    float length = Mathf.Abs(span[axis]);
                    float sign = Mathf.Sign(span[axis]);

                    // The wall's box: the longest along the wall.
                    BoxCollider wall = null;
                    foreach (var b in bone.GetComponents<BoxCollider>())
                        if (wall == null || b.size[axis] > wall.size[axis]) wall = b;
                    if (wall == null) { notes.Add($"{wallName}: no box"); continue; }

                    // Its gaps at the two ends as they are now.
                    float startPad = sign * wall.center[axis] - wall.size[axis] * 0.5f;
                    float endPad = length - startPad - wall.size[axis];

                    // A post box at the far end goes onto the far end's bone.
                    int moved = 0;
                    foreach (var b in bone.GetComponents<BoxCollider>())
                    {
                        if (b == wall || sign * b.center[axis] < length - 1f) continue;
                        var post = end.gameObject.AddComponent<BoxCollider>();
                        post.center = end.InverseTransformPoint(bone.TransformPoint(b.center));
                        post.size = b.size;
                        post.isTrigger = b.isTrigger;
                        post.sharedMaterial = b.sharedMaterial;
                        Object.DestroyImmediate(b);
                        moved++;
                    }

                    if (!bone.TryGetComponent<BoneSpanBox>(out var spanBox)) spanBox = bone.gameObject.AddComponent<BoneSpanBox>();
                    spanBox.Set(wall, end, startPad, endPad);
                    notes.Add($"{wallName}->{endName} {length:0.##} long, gaps {startPad:0.##}/{endPad:0.##}" + (moved > 0 ? $", end post moved to {endName}" : ""));
                }
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            // Open in Prefab Mode and not changed there: open it again, so it shows (and later saves) the new boxes.
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == PrefabPath)
            {
                if (stage.scene.isDirty) notes.Add("Play_Ground_Z is open in Prefab Mode with unsaved changes: close it without saving to see the new boxes");
                else
                {
                    StageUtility.GoToMainStage();
                    PrefabStageUtility.OpenPrefab(PrefabPath);
                }
            }
            Debug.Log("[PlayGround Wall Colliders] " + string.Join("; ", notes));
        }

        private static Transform Find(Transform under, string name)
        {
            foreach (var t in under.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }
}

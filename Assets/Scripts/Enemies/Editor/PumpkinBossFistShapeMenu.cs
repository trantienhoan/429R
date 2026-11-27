using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.Enemies.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Enemies > Pumpkin Boss: Fit Fist Hit Shapes (re-runnable; "Pumpkin Boss: Old Fist Spheres" puts
    /// the old spheres back). User 2026-11-27: "check Pumpkin_Boss_Z's punch, I can see that player being pushed delay a
    /// bit compare with the punch". The boss's punch lands when its fist's hit shape (on the Hand.L / Hand.R bones, which
    /// grow about 3 times and fly out in a punch) reaches the player's body. The old hit spheres were made for the first
    /// model (Set Up Pumpkin Boss: radius 0.17, 0.12 along the bone): on the remodelled fist pumpkins they sat 0.0012 bone
    /// units (0.6 m at full punch size) off the fist's middle and were smaller than the fist (0.0017 against a fist about
    /// 0.0021 wide and 0.0029 long), so the fist you see touched the player up to 0.28 s before the push.
    /// Now each fist gets a capsule fitted to its own pumpkin: the vertices skinned to the hand bone (half or more of their
    /// weight), in the model's rest pose, measured in the bone's own space so it grows and turns with the fist. Its middle
    /// is the middle of those vertices, it lies along their longest side, its radius covers 95% of them round that line and
    /// its length 98% along it (a few stray vertices don't make it bigger than the fist). The same shapes are what the
    /// player's own hits land on. pumpkin_boss_Z.prefab only (the Doom copy takes it from there).
    /// </summary>
    internal static class PumpkinBossFistShapeMenu
    {
        private const string BossPath = "Assets/Prefabs/Enemies/pumpkin_boss_Z.prefab";
        // The spheres Set Up Pumpkin Boss made (Hand.L / Hand.R: radius 0.17, middle 0.12 along the bone, in model metres
        // x 0.01), for the undo.
        private const float OldRadius = 0.0017f, OldAlong = 0.0012f;
        private const float MinWeight = 0.5f, RadiusShare = 0.95f, LengthShare = 0.98f;

        [MenuItem("Tools/429 Game/Enemies/Pumpkin Boss: Fit Fist Hit Shapes")]
        private static void Fit() => Edit(true);

        [MenuItem("Tools/429 Game/Enemies/Pumpkin Boss: Old Fist Spheres")]
        private static void Old() => Edit(false);

        private static void Edit(bool fit)
        {
            var notes = new List<string>();
            var root = PrefabUtility.LoadPrefabContents(BossPath);
            try
            {
                var brain = root.GetComponent<PumpkinBossBrain>();
                if (brain == null) { Debug.LogWarning("[Pumpkin Boss] No Pumpkin Boss Brain on " + BossPath); return; }
                var so = new SerializedObject(brain);
                foreach (var field in new[] { "handL", "handR" })
                {
                    var hand = so.FindProperty(field).objectReferenceValue as Transform;
                    if (hand == null) { notes.Add(field + ": not set"); continue; }
                    notes.Add(fit ? FitCapsule(root, hand) : PutSphereBack(hand));
                }
                PrefabUtility.SaveAsPrefabAsset(root, BossPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            Debug.Log("[Pumpkin Boss] Fist hit shapes:\n" + string.Join("\n", notes));
        }

        private static string FitCapsule(GameObject root, Transform hand)
        {
            if (!FistPoints(root, hand, out var points)) return hand.name + ": no vertices skinned to it, left as it is";
            // The middle: half way between the 2% and 98% points on each side (stray vertices don't move it).
            var lo = new Vector3(Percentile(points, 0, 0.02f), Percentile(points, 1, 0.02f), Percentile(points, 2, 0.02f));
            var hi = new Vector3(Percentile(points, 0, 0.98f), Percentile(points, 1, 0.98f), Percentile(points, 2, 0.98f));
            var middle = (lo + hi) * 0.5f;
            var size = hi - lo;
            int axis = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
            var round = new List<float>(points.Count);
            var along = new List<float>(points.Count);
            foreach (var p in points)
            {
                var d = p - middle;
                float a = d[axis];
                along.Add(Mathf.Abs(a));
                d[axis] = 0f;
                round.Add(d.magnitude);
            }
            round.Sort();
            along.Sort();
            float radius = round[Mathf.Clamp(Mathf.RoundToInt(round.Count * RadiusShare), 0, round.Count - 1)];
            float halfLength = Mathf.Max(radius, along[Mathf.Clamp(Mathf.RoundToInt(along.Count * LengthShare), 0, along.Count - 1)]);

            var old = hand.GetComponent<SphereCollider>();
            bool trigger = old != null && old.isTrigger;
            var material = old != null ? old.sharedMaterial : null;
            string was = old != null ? $"sphere r {old.radius:0.#####} at {old.center.ToString("F5")}" : "no sphere";
            foreach (var sphere in hand.GetComponents<SphereCollider>()) Object.DestroyImmediate(sphere, true);
            if (!hand.TryGetComponent(out CapsuleCollider capsule)) capsule = hand.gameObject.AddComponent<CapsuleCollider>();
            capsule.direction = axis;
            capsule.center = middle;
            capsule.radius = radius;
            capsule.height = halfLength * 2f;
            capsule.isTrigger = trigger;
            capsule.sharedMaterial = material;
            hand.gameObject.tag = "Enemy";
            return $"{hand.name}: {was} -> capsule along {"XYZ"[axis]}, middle {middle.ToString("F5")}, radius {radius:0.#####}, " +
                   $"length {halfLength * 2f:0.#####} ({points.Count} fist vertices)";
        }

        private static string PutSphereBack(Transform hand)
        {
            foreach (var capsule in hand.GetComponents<CapsuleCollider>()) Object.DestroyImmediate(capsule, true);
            if (!hand.TryGetComponent(out SphereCollider sphere)) sphere = hand.gameObject.AddComponent<SphereCollider>();
            sphere.radius = OldRadius;
            sphere.center = new Vector3(0f, OldAlong, 0f);
            hand.gameObject.tag = "Enemy";
            return $"{hand.name}: back to the old sphere (r {OldRadius}, {OldAlong} along the bone)";
        }

        // The fist pumpkin: every vertex with at least Min Weight on this bone, in the bone's own space (rest pose).
        private static bool FistPoints(GameObject root, Transform hand, out List<Vector3> points)
        {
            points = new List<Vector3>();
            foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = skin.sharedMesh;
                if (mesh == null) continue;
                int bone = System.Array.IndexOf(skin.bones, hand);
                if (bone < 0) continue;
                var weights = mesh.boneWeights;
                var bind = mesh.bindposes;
                var vertices = mesh.vertices;
                if (bone >= bind.Length || weights.Length != vertices.Length) continue;
                for (int i = 0; i < vertices.Length; i++)
                {
                    var w = weights[i];
                    float share = (w.boneIndex0 == bone ? w.weight0 : 0f) + (w.boneIndex1 == bone ? w.weight1 : 0f) +
                                  (w.boneIndex2 == bone ? w.weight2 : 0f) + (w.boneIndex3 == bone ? w.weight3 : 0f);
                    if (share >= MinWeight) points.Add(bind[bone].MultiplyPoint3x4(vertices[i]));
                }
            }
            return points.Count > 0;
        }

        private static float Percentile(List<Vector3> points, int axis, float share)
        {
            var values = new List<float>(points.Count);
            foreach (var p in points) values.Add(p[axis]);
            values.Sort();
            return values[Mathf.Clamp(Mathf.RoundToInt((values.Count - 1) * share), 0, values.Count - 1)];
        }
    }
}

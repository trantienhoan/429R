using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Gives the pumpkin boss's giant pumpkin butt its own material slot whenever its model is imported (user,
    /// 2026-11-11: the butt cracks, so it needs a material of its own). The Blender file has one slot for the whole boss,
    /// and all its parts share the same spot on the furniture texture, so the butt can't be told apart there. Here it can:
    /// every triangle whose three corners move 100% with the Hips bone is the giant pumpkin (nothing else is weighted only
    /// to Hips), and those go into a second submesh. Both slots show the same material, so nothing looks different; the
    /// boss puts its crack overlay on the second one. It runs again by itself after every re-export.
    /// It also keeps every vertex's rest position (the bind pose, model units) in the mesh's fourth UV channel (Rest Uv
    /// Channel, TEXCOORD3): the crack overlay draws its cracks from it, so they're stuck to the pumpkin and seamless, whatever
    /// the bones do (2026-11-11). Nothing else reads that channel.
    /// </summary>
    public class PumpkinBossButtSplit : AssetPostprocessor
    {
        private const string ModelPath = "Assets/Models/Enemies/Pumpkin_Boss_Z.fbx";
        private const string MeshName = "pumpkin_boss";
        private const string ButtBone = "Hips";

        /// <summary>The butt's submesh (and material slot) on the boss's skinned mesh.</summary>
        public const int ButtSubmesh = 1;

        /// <summary>The UV channel holding each vertex's rest position (TEXCOORD3; the crack overlay reads it).</summary>
        public const int RestUvChannel = 3;

        // Raise this when the split changes, so Unity imports the model again (2: the rest positions).
        public override uint GetVersion() => 2;

        private void OnPostprocessModel(GameObject root)
        {
            if (assetPath != ModelPath) return;
            foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = skin.sharedMesh;
                if (mesh == null || mesh.name != MeshName || mesh.subMeshCount != 1) continue;
                int hips = System.Array.FindIndex(skin.bones, bone => bone != null && bone.name == ButtBone);
                var weights = mesh.boneWeights;
                if (hips < 0 || weights.Length != mesh.vertexCount)
                {
                    Debug.LogWarning($"[Boss Butt Split] {assetPath}: no {ButtBone} bone or no bone weights; left as one submesh.");
                    continue;
                }

                bool OnlyHips(int vertex) => weights[vertex].boneIndex0 == hips && weights[vertex].weight0 >= 0.999f;

                var triangles = mesh.GetTriangles(0);
                var body = new List<int>(triangles.Length);
                var butt = new List<int>();
                for (int i = 0; i + 2 < triangles.Length; i += 3)
                {
                    var into = OnlyHips(triangles[i]) && OnlyHips(triangles[i + 1]) && OnlyHips(triangles[i + 2]) ? butt : body;
                    into.Add(triangles[i]);
                    into.Add(triangles[i + 1]);
                    into.Add(triangles[i + 2]);
                }
                if (butt.Count == 0)
                {
                    Debug.LogWarning($"[Boss Butt Split] {assetPath}: no triangle moves only with {ButtBone}; left as one submesh.");
                    continue;
                }

                mesh.subMeshCount = 2;
                mesh.SetTriangles(body, 0, false);
                mesh.SetTriangles(butt, ButtSubmesh, false);
                // The rest positions for the cracks (the mesh sits at the model's origin, so mesh space is the model's).
                mesh.SetUVs(RestUvChannel, new List<Vector3>(mesh.vertices));
                var materials = skin.sharedMaterials;
                if (materials.Length == 1) skin.sharedMaterials = new[] { materials[0], materials[0] };
                Debug.Log($"[Boss Butt Split] {assetPath}: giant pumpkin = {butt.Count / 3} triangles in submesh {ButtSubmesh}, body = {body.Count / 3}.");
            }
        }
    }
}

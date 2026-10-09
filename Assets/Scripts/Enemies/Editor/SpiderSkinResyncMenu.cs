using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// The spider prefabs hold their own (unpacked) copy of the Spider_Smol_&amp;_Anims.fbx skeleton. When the FBX's bones
    /// change (e.g. a bone added in Blender), its meshes expect a different bone list than the prefab's skinned meshes
    /// give them and the spider renders as nothing. This adds any missing bones to each prefab's skeleton (same place
    /// as in the FBX) and gives every skinned mesh the FBX's bone list again, matched by name
    /// (Tools > 429 Game > Enemies > Resync Spider Skins).
    /// </summary>
    public static class SpiderSkinResyncMenu
    {
        private const string Model = "Assets/Models/Enemies/Spider_Smol_&_Anims.fbx";

        [MenuItem("Tools/429 Game/Enemies/Resync Spider Skins")]
        public static void Resync()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            if (model == null)
            {
                Debug.LogWarning($"[Spider Skins] {Model} not found.");
                return;
            }
            var reference = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).ToDictionary(r => r.name);

            int fixedCount = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!AssetDatabase.GetDependencies(path, false).Any(d => d == Model)) continue;
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (PrefabUtility.GetPrefabAssetType(asset) == PrefabAssetType.Variant) continue;   // follows its base
                if (!asset.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(r => NeedsFix(r, reference))) continue;

                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        if (!NeedsFix(skin, reference)) continue;
                        Rebind(skin, reference[skin.name]);
                        fixedCount++;
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    Debug.Log($"[Spider Skins] Fixed {path}.");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            Debug.Log($"[Spider Skins] {fixedCount} skinned meshes now match {Model}.");
        }

        private static bool NeedsFix(SkinnedMeshRenderer skin, Dictionary<string, SkinnedMeshRenderer> reference)
        {
            if (skin.sharedMesh == null || AssetDatabase.GetAssetPath(skin.sharedMesh) != Model) return false;
            if (!reference.TryGetValue(skin.name, out var source)) return false;
            if (skin.bones.Length != source.bones.Length) return true;
            for (int i = 0; i < skin.bones.Length; i++)
            {
                if (skin.bones[i] == null || skin.bones[i].name != source.bones[i].name) return true;
            }
            return false;
        }

        private static void Rebind(SkinnedMeshRenderer skin, SkinnedMeshRenderer source)
        {
            // The prefab's skeleton: everything under the armature its root bone belongs to.
            var armature = skin.rootBone != null ? skin.rootBone : skin.bones.FirstOrDefault(b => b != null);
            while (armature != null && armature.parent != null && armature.parent != skin.transform.parent) armature = armature.parent;
            var byName = new Dictionary<string, Transform>();
            if (armature != null)
            {
                foreach (var t in armature.GetComponentsInChildren<Transform>(true)) byName[t.name] = t;
            }

            var bones = new Transform[source.bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                bones[i] = Find(source.bones[i], byName);
            }
            skin.bones = bones;
            if (source.rootBone != null && byName.TryGetValue(source.rootBone.name, out var rootBone)) skin.rootBone = rootBone;
        }

        // The prefab's bone of that name; a bone it doesn't have yet is added where the FBX has it.
        private static Transform Find(Transform modelBone, Dictionary<string, Transform> byName)
        {
            if (byName.TryGetValue(modelBone.name, out var bone)) return bone;
            var parent = modelBone.parent != null ? Find(modelBone.parent, byName) : null;
            bone = new GameObject(modelBone.name).transform;
            bone.SetParent(parent, false);
            bone.localPosition = modelBone.localPosition;
            bone.localRotation = modelBone.localRotation;
            bone.localScale = modelBone.localScale;
            byName[modelBone.name] = bone;
            Debug.Log($"[Spider Skins] Added bone {modelBone.name} under {(parent != null ? parent.name : "nothing")}.");
            return bone;
        }
    }
}

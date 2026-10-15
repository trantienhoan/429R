using System.Collections.Generic;
using Game.Weapons;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Combat > Stronger Triggered Hammers: the hammers whose trigger grows the head (Hammer_Boong, its
    /// second copy Hammer_Boong 2ND, and Hammer_Tool) get a Triggered Boost on the head bone (Bone.001): the head grows to
    /// 3x instead of the animation's 2.11x, and hits do up to 1.5x the damage while it's grown. Change the numbers on the
    /// prefabs' Triggered Boost. Running it again only adds it where it's missing.
    /// </summary>
    public static class TriggeredBoostSetupMenu
    {
        private static readonly string[] Hammers =
        {
            "Assets/Prefabs/Weapon/Hammer_Boong.prefab",
            "Assets/Prefabs/Weapon/Hammer_Boong 2ND.prefab",
            "Assets/Prefabs/Weapon/Hammer_Tool.prefab",
        };
        private const string HeadBone = "Bone.001";

        [MenuItem("Tools/429 Game/Combat/Stronger Triggered Hammers")]
        public static void SetUp()
        {
            var done = new List<string>();
            foreach (var path in Hammers)
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { done.Add(name + " missing"); continue; }
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (root.TryGetComponent<TriggeredBoost>(out _)) { done.Add(name + " (already)"); continue; }
                    Transform head = null;
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name == HeadBone) { head = t; break; }
                    }
                    if (head == null) { done.Add(name + " has no " + HeadBone); continue; }

                    var boost = root.AddComponent<TriggeredBoost>();
                    var so = new SerializedObject(boost);
                    so.FindProperty("grows").objectReferenceValue = head;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    done.Add(name);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            Debug.Log("[Triggered Boost] " + string.Join(", ", done) + ".");
        }

        [MenuItem("Tools/429 Game/Combat/Stronger Triggered Hammers", true)]
        private static bool NotPlaying() => !EditorApplication.isPlaying;
    }
}

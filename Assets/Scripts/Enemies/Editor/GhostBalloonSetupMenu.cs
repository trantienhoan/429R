using System.Collections.Generic;
using Game.Enemies;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>Menu command that turns the Ghost Balloon models into ghost balloons (Tools > 429 Game > Enemies).</summary>
    public static class GhostBalloonSetupMenu
    {
        private const string BalloonPath = "Assets/Prefabs/Enemies/PLAYGROUND/Ghost_Balloon_{0}.prefab";
        private const string PopEffectPath = "Assets/Lana Studio/Hyper Casual FX/Prefabs/Confetti/Smoke_Blast.prefab";
        private const string PopSoundPath = "Assets/Audio/SFX/bubble_pop_1.wav";
        private const string WakeSoundPath = "Assets/Audio/SFX/creepy_laugh_1.wav";
        private const string SpiderPath = "Assets/Prefabs/Enemies/Spider_Small.prefab";

        /// <summary>
        /// Gives Ghost_Balloon_1, 2 and 3 a Ghost Balloon, a kinematic Rigidbody and a collider around the balloon part.
        /// 1 and 2 wake when hit, 3 when the player comes near. Settings already on a balloon are kept.
        /// </summary>
        [MenuItem("Tools/429 Game/Enemies/Set Up Ghost Balloons")]
        public static void SetUpGhostBalloons()
        {
            var popEffect = AssetDatabase.LoadAssetAtPath<GameObject>(PopEffectPath);
            var popSound = AssetDatabase.LoadAssetAtPath<AudioClip>(PopSoundPath);
            var wakeSound = AssetDatabase.LoadAssetAtPath<AudioClip>(WakeSoundPath);
            var spider = AssetDatabase.LoadAssetAtPath<GameObject>(SpiderPath);

            var notes = new List<string>();
            for (int n = 1; n <= 3; n++)
            {
                string path = string.Format(BalloonPath, n);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    notes.Add($"no '{path}'");
                    continue;
                }

                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    bool added = !root.TryGetComponent<GhostBalloon>(out var balloon);
                    if (added) balloon = root.AddComponent<GhostBalloon>();

                    if (!root.TryGetComponent<Rigidbody>(out var body)) body = root.AddComponent<Rigidbody>();
                    body.isKinematic = true;
                    body.useGravity = false;
                    body.interpolation = RigidbodyInterpolation.Interpolate;
                    if (root.GetComponentInChildren<Collider>() == null && GhostBalloon.FindBalloon(root, out var center, out float radius))
                    {
                        var sphere = root.AddComponent<SphereCollider>();
                        sphere.center = center;
                        sphere.radius = radius;
                    }

                    var serialized = new SerializedObject(balloon);
                    if (added) serialized.FindProperty("wakeOn").enumValueIndex = (int)(n == 3 ? GhostBalloon.WakeOn.Near : GhostBalloon.WakeOn.Hit);
                    SetIfEmpty(serialized, "popEffect", popEffect);
                    SetIfEmpty(serialized, "popSound", popSound);
                    SetIfEmpty(serialized, "wakeSound", wakeSound);
                    var spiders = serialized.FindProperty("spiders");
                    if (spiders.arraySize == 0 && spider != null)
                    {
                        spiders.arraySize = 1;
                        spiders.GetArrayElementAtIndex(0).objectReferenceValue = spider;
                    }
                    serialized.ApplyModifiedPropertiesWithoutUndo();

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    notes.Add($"{root.name}: {(added ? "set up" : "updated")}, wakes on {(GhostBalloon.WakeOn)serialized.FindProperty("wakeOn").enumValueIndex}");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            Debug.Log("[Ghost Balloons] " + string.Join("; ", notes) + ".");
        }

        private static void SetIfEmpty(SerializedObject serialized, string field, Object value)
        {
            var property = serialized.FindProperty(field);
            if (property != null && property.objectReferenceValue == null && value != null) property.objectReferenceValue = value;
        }
    }
}

using System.Collections.Generic;
using Game.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Enemies > Bee Wing Buzz: puts Wing Buzz on Bee_Smarter (every bee is a variant of it) with its
    /// four wing bones measured from the Bee_Fly clip: the middle of each wing's flap, the axis it swings about and how
    /// far. Safe to run again (it measures again).
    /// </summary>
    public static class BeeWingBuzzSetupMenu
    {
        private const string BeePath = "Assets/Prefabs/Enemies/Enemies_Smarter/Bee_Smarter.prefab";
        private const string AnimsPath = "Assets/Models/Enemies/Bee_Smol_&_Anims.fbx";
        private const string FlyClip = "Bee_Fly";
        private static readonly (string bone, float phase)[] Wings =
        {
            ("Up_Wing_01.L", 0f), ("Up_Wing_01.R", 0f), ("D_Wing_01.L", 0.15f), ("D_Wing_01.R", 0.15f),
        };
        private const int Samples = 120;

        [MenuItem("Tools/429 Game/Enemies/Bee Wing Buzz")]
        public static void Run()
        {
            AnimationClip fly = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(AnimsPath))
            {
                if (asset is AnimationClip clip && clip.name == FlyClip) fly = clip;
            }
            if (fly == null)
            {
                Debug.LogError($"[Wing Buzz] No {FlyClip} clip in {AnimsPath}.");
                return;
            }
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == BeePath)
            {
                Debug.LogWarning("[Wing Buzz] Bee_Smarter is open in Prefab Mode: close it and run again.");
                return;
            }

            // Measured on a copy in a preview scene, so sampling the clip doesn't move the prefab's own bones.
            var preview = EditorSceneManager.NewPreviewScene();
            var copy = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BeePath), preview);
            var root = PrefabUtility.LoadPrefabContents(BeePath);
            try
            {
                var animator = copy.GetComponentInChildren<Animator>(true);
                var measured = new List<string>();
                var buzz = root.GetComponent<WingBuzz>();
                if (buzz == null) buzz = root.AddComponent<WingBuzz>();
                var so = new SerializedObject(buzz);
                var list = so.FindProperty("wings");
                list.arraySize = Wings.Length;
                for (int i = 0; i < Wings.Length; i++)
                {
                    var bone = Find(animator.transform, Wings[i].bone);
                    if (bone == null)
                    {
                        measured.Add(Wings[i].bone + " missing");
                        continue;
                    }
                    Measure(fly, animator.gameObject, bone, out var middle, out var axis, out float swing);
                    var wing = list.GetArrayElementAtIndex(i);
                    wing.FindPropertyRelative("bone").stringValue = Wings[i].bone;
                    wing.FindPropertyRelative("middle").quaternionValue = middle;
                    wing.FindPropertyRelative("axis").vector3Value = axis;
                    wing.FindPropertyRelative("swing").floatValue = swing;
                    wing.FindPropertyRelative("phase").floatValue = Wings[i].phase;
                    measured.Add($"{Wings[i].bone} {swing:0} deg");
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, BeePath);
                Debug.Log("[Wing Buzz] Bee_Smarter: " + string.Join(", ", measured));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        // The two poses of the wing furthest apart in the clip: the middle between them, and the axis and angle between.
        private static void Measure(AnimationClip clip, GameObject animated, Transform bone, out Quaternion middle,
            out Vector3 axis, out float swing)
        {
            var poses = new Quaternion[Samples];
            for (int i = 0; i < Samples; i++)
            {
                clip.SampleAnimation(animated, clip.length * i / Samples);
                poses[i] = bone.localRotation;
            }
            int a = 0, b = 0;
            swing = -1f;
            for (int i = 0; i < Samples; i++)
            {
                for (int j = i + 1; j < Samples; j++)
                {
                    float angle = Quaternion.Angle(poses[i], poses[j]);
                    if (angle <= swing) continue;
                    swing = angle;
                    a = i;
                    b = j;
                }
            }
            (Quaternion.Inverse(poses[a]) * poses[b]).ToAngleAxis(out _, out axis);
            middle = poses[a] * Quaternion.AngleAxis(swing * 0.5f, axis);
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t;
            }
            return null;
        }
    }
}

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Enemies;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Enemies > Set Up Fat Pumpkin Animation. The user's model has no rig: pumpkin_fat_upper and
    /// pumpkin_fat_below, with the upper's origin at the back of the seam, its hinge. The clips only move transforms: the upper
    /// turns on that hinge (the mouth), and Model rocks and hops. They're in Models/Animator/Fat_Pumpkin; to edit one, open
    /// the prefab, select Model and use the Animation window. Clips and the controller that exist are kept: delete one to
    /// have it made again. Run it again after a new export of the model: it fits the prefab to it (on the floor, its collider,
    /// agent and mouth). The model's parts must keep their names, and what's on the upper part (eyes, nose) must be part of it
    /// or under it.
    /// </summary>
    public static class FatPumpkinAnimationMenu
    {
        private const string FatPath = "Assets/Prefabs/Enemies/Fat_Pumpkin_Enemy.prefab";
        private const string Folder = "Assets/Models/Animator/Fat_Pumpkin/";
        private const string ControllerPath = Folder + "Fat_Pumpkin.controller";
        private const string ModelName = "Model";
        private const string JiggleName = "Jiggle";
        private const string LookName = "pumpkin_fat";
        private const string UpperName = "pumpkin_fat_upper";
        private const string MouthName = "Mouth";
        // Relative to Model, where the Animator goes.
        private const string UpperPath = JiggleName + "/" + LookName + "/" + UpperName;
        // Where candy flies to: this far above the seam, and this share of its radius out to the front.
        private const float MouthAboveSeam = 0.08f;
        private const float MouthForward = 0.5f;
        private const float AgentShare = 0.9f;
        // Tilted, its round base would dig into the floor: it's this far off the floor per degree, at least.
        private const float LiftPerDegree = 0.003f;
        private const float BlendSeconds = 0.15f;
        private const float QuickBlendSeconds = 0.04f;

        // A moment of a clip: in seconds; the mouth (upper part, degrees, more negative = wider); Model leaning forward,
        // rocking to the side (degrees) and off the floor (in the prefab's metres, before its scale).
        private readonly struct Pose
        {
            public readonly float at, mouth, lean, rock, hop;

            public Pose(float at, float mouth, float lean = 0f, float rock = 0f, float hop = 0f)
            {
                this.at = at;
                this.mouth = mouth;
                this.lean = lean;
                this.rock = rock;
                this.hop = hop;
            }
        }

        // Closed, breathing.
        private static readonly Pose[] Idle =
        {
            new(0f, 0f), new(1.5f, -2f, -1f, 0f, 0.004f), new(3f, 0f),
        };

        // Shakes, closed; bursts open and cackles, and it's off (as long as Fat Pumpkin's Wake Seconds).
        private static readonly Pose[] Wake =
        {
            new(0f, 0f), new(0.08f, 0f, 4f, -6f), new(0.16f, 0f, 4f, 6f), new(0.24f, 0f, 4f, -6f), new(0.32f, 0f, 4f, 6f),
            new(0.42f, 0f, 6f), new(0.5f, -42f, -8f, 0f, 0.05f), new(0.58f, -24f, -6f, 0f, 0.02f),
            new(0.65f, -40f, -8f, 0f, 0.04f), new(0.72f, -22f, -5f, 0f, 0.02f), new(0.8f, -12f, -2f),
        };

        // Two waddling steps, hungry: a little hop and its mouth working at each.
        private static readonly Pose[] Walk =
        {
            new(0f, 0f, 3f), new(0.225f, -14f, 3f, 9f, 0.03f), new(0.45f, -2f, 3f), new(0.675f, -14f, 3f, -9f, 0.03f), new(0.9f, 0f, 3f),
        };

        // Wide open as the candy flies in (Fat Pumpkin's Eat Seconds, 0.25), snaps shut on it, and chews.
        private static readonly Pose[] Eat =
        {
            new(0f, 0f), new(0.12f, -40f, -6f, 0f, 0.01f), new(0.22f, -42f, -7f, 0f, 0.01f), new(0.27f, 0f, 5f), new(0.33f, -8f, 2f, 0f, 0.012f),
            new(0.4f, 0f), new(0.5f, -10f, -1f, 0f, 0.008f), new(0.6f, 0f), new(0.7f, -7f, -1f, 0f, 0.005f), new(0.85f, 0f),
        };

        private static readonly Pose[] Laugh =
        {
            new(0f, 0f), new(0.15f, -26f, -7f, 0f, 0.02f), new(0.3f, -8f, -4f), new(0.45f, -28f, -8f, 0f, 0.02f), new(0.6f, -8f, -4f),
            new(0.75f, -24f, -6f, 0f, 0.015f), new(0.9f, 0f),
        };

        // Knocked back, mouth flying open, and it rocks back onto its base.
        private static readonly Pose[] Hit =
        {
            new(0f, 0f), new(0.05f, -28f, -18f, 0f, 0.07f), new(0.18f, -14f, 8f), new(0.3f, -18f, -4f, 0f, 0.01f), new(0.42f, -4f, 2f), new(0.55f, 0f),
        };

        [MenuItem("Tools/429 Game/Enemies/Set Up Fat Pumpkin Animation")]
        public static void Run()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == FatPath)
            {
                Debug.LogError($"[Fat Pumpkin] {FatPath} is open in Prefab Mode: close it first. Nothing changed.");
                return;
            }
            Directory.CreateDirectory(Folder);
            var clips = new Dictionary<string, AnimationClip>
            {
                ["Idle"] = MakeClip("Idle", true, Idle),
                ["Wake"] = MakeClip("Wake", false, Wake),
                ["Walk"] = MakeClip("Walk", true, Walk),
                ["Eat"] = MakeClip("Eat", false, Eat),
                ["Laugh"] = MakeClip("Laugh", false, Laugh),
                ["Hit"] = MakeClip("Hit", false, Hit),
            };
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) controller = MakeController(clips);
            AssetDatabase.SaveAssets();
            if (FitPrefab(controller)) Debug.Log($"[Fat Pumpkin] {FatPath} animates with {ControllerPath}.");
        }

        private static AnimationClip MakeClip(string name, bool loop, Pose[] poses)
        {
            string path = Folder + "Fat_Pumpkin_" + name + ".anim";
            var made = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (made != null) return made;
            var clip = new AnimationClip { name = "Fat_Pumpkin_" + name };
            float end = poses[poses.Length - 1].at;
            SetCurve(clip, UpperPath, "localEulerAnglesRaw.x", poses, p => p.mouth);
            SetFlat(clip, UpperPath, "localEulerAnglesRaw.y", end);
            SetFlat(clip, UpperPath, "localEulerAnglesRaw.z", end);
            SetCurve(clip, "", "localEulerAnglesRaw.x", poses, p => p.lean);
            SetFlat(clip, "", "localEulerAnglesRaw.y", end);
            SetCurve(clip, "", "localEulerAnglesRaw.z", poses, p => p.rock);
            SetFlat(clip, "", "m_LocalPosition.x", end);
            SetCurve(clip, "", "m_LocalPosition.y", poses, p => Mathf.Max(p.hop, LiftPerDegree * Mathf.Sqrt(p.lean * p.lean + p.rock * p.rock)));
            SetFlat(clip, "", "m_LocalPosition.z", end);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        private static void SetCurve(AnimationClip clip, string path, string property, Pose[] poses, System.Func<Pose, float> value)
        {
            var curve = new AnimationCurve(poses.Select(p => new Keyframe(p.at, value(p))).ToArray());
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property), curve);
        }

        private static void SetFlat(AnimationClip clip, string path, string property, float end)
        {
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property), AnimationCurve.Constant(0f, end, 0f));
        }

        private static AnimatorController MakeController(Dictionary<string, AnimationClip> clips)
        {
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Walking", AnimatorControllerParameterType.Bool);
            controller.AddParameter(new AnimatorControllerParameter { name = "WalkSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            controller.AddParameter("Eat", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Laugh", AnimatorControllerParameterType.Trigger);
            var machine = controller.layers[0].stateMachine;
            var states = clips.ToDictionary(c => c.Key, c =>
            {
                var state = machine.AddState(c.Key);
                state.motion = c.Value;
                return state;
            });
            machine.defaultState = states["Wake"];
            states["Walk"].speedParameterActive = true;
            states["Walk"].speedParameter = "WalkSpeed";

            ThenBack(states["Wake"], states);
            ThenBack(states["Eat"], states);
            ThenBack(states["Hit"], states);
            ThenBack(states["Laugh"], states);
            When(states["Idle"], states["Walk"], BlendSeconds, "Walking", true);
            When(states["Walk"], states["Idle"], BlendSeconds, "Walking", false);
            foreach (var from in new[] { "Idle", "Walk" })
            {
                var laugh = from == "Idle" ? states["Idle"].AddTransition(states["Laugh"]) : states["Walk"].AddTransition(states["Laugh"]);
                laugh.hasExitTime = false;
                laugh.duration = BlendSeconds;
                laugh.AddCondition(AnimatorConditionMode.If, 0f, "Laugh");
            }
            // A hit goes before a gulp; candies can come one after another.
            Any(machine, states["Hit"], "Hit", false);
            Any(machine, states["Eat"], "Eat", true);
            return controller;
        }

        // When it's done: walking on, or standing.
        private static void ThenBack(AnimatorState from, Dictionary<string, AnimatorState> states)
        {
            var walk = from.AddTransition(states["Walk"]);
            walk.hasExitTime = true;
            walk.exitTime = 1f;
            walk.duration = BlendSeconds;
            walk.AddCondition(AnimatorConditionMode.If, 0f, "Walking");
            var idle = from.AddTransition(states["Idle"]);
            idle.hasExitTime = true;
            idle.exitTime = 1f;
            idle.duration = BlendSeconds;
            idle.AddCondition(AnimatorConditionMode.IfNot, 0f, "Walking");
        }

        private static void When(AnimatorState from, AnimatorState to, float seconds, string parameter, bool value)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = seconds;
            transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, parameter);
        }

        private static void Any(AnimatorStateMachine machine, AnimatorState to, string trigger, bool again)
        {
            var transition = machine.AddAnyStateTransition(to);
            transition.hasExitTime = false;
            transition.duration = QuickBlendSeconds;
            transition.canTransitionToSelf = again;
            transition.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        }

        // On the floor (the model's origin is at its seam), with its body, agent and mouth where the model is now.
        private static bool FitPrefab(AnimatorController controller)
        {
            var root = PrefabUtility.LoadPrefabContents(FatPath);
            try
            {
                var model = root.transform.Find(ModelName);
                var jiggle = model != null ? model.Find(JiggleName) : null;
                var look = jiggle != null ? jiggle.Find(LookName) : null;
                if (look == null || look.Find(UpperName) == null)
                {
                    Debug.LogError($"[Fat Pumpkin] No {ModelName}/{JiggleName}/{LookName}/{UpperName} in the prefab. The prefab wasn't changed.");
                    return false;
                }
                look.localPosition = Vector3.zero;
                var bounds = LocalBounds(root.transform, look);
                float radius = Mathf.Max(bounds.extents.x, bounds.extents.z);
                float height = bounds.size.y;
                float lift = -bounds.min.y;
                look.localPosition = new Vector3(0f, lift, 0f);

                if (!model.TryGetComponent(out Animator animator)) animator = model.gameObject.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                var body = root.GetComponent<SphereCollider>();
                body.radius = radius;
                body.center = new Vector3(0f, height * 0.5f, 0f);
                var agent = root.GetComponent<NavMeshAgent>();
                agent.radius = radius * AgentShare;
                agent.height = height;

                var mouth = jiggle.Find(MouthName);
                if (mouth == null)
                {
                    mouth = new GameObject(MouthName).transform;
                    mouth.SetParent(jiggle, false);
                }
                mouth.localPosition = new Vector3(0f, lift + MouthAboveSeam, radius * MouthForward);

                var so = new SerializedObject(root.GetComponent<FatPumpkin>());
                so.FindProperty("radius").floatValue = radius;
                so.FindProperty("mouth").objectReferenceValue = mouth;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, FatPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // The look's meshes, in the root's own units.
        private static Bounds LocalBounds(Transform root, Transform look)
        {
            var bounds = new Bounds();
            bool any = false;
            foreach (var part in look.GetComponentsInChildren<MeshFilter>(true))
            {
                if (part.sharedMesh == null) continue;
                var box = part.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = box.center + Vector3.Scale(box.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    var at = root.InverseTransformPoint(part.transform.TransformPoint(corner));
                    if (any) bounds.Encapsulate(at);
                    else bounds = new Bounds(at, Vector3.zero);
                    any = true;
                }
            }
            return bounds;
        }
    }
}

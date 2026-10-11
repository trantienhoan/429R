using System.Collections.Generic;
using System.Linq;
using Game.Combat;
using Game.Enemies;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

namespace Game.EditorTools
{
    /// <summary>
    /// Builds the smarter Pumpkin boss (Tools > 429 Game > Enemies > Set Up Pumpkin Boss) from the re-rigged model
    /// Models/Enemies/Pumpkin_Boss_Z.fbx (exported from Blender/Pumpkin_Boss_Z.blend):
    /// - the FBX's clips (looping ones set to loop) and an Animator controller with one state per clip;
    /// - Bug_Pumpkin, the pumpkin full of bugs it throws;
    /// - pumpkin_boss_Z.prefab: the new model under a "Model" child (moved and scaled by the brain), hit spheres on its
    ///   bones, Enemy Hit Damage, and Pumpkin Boss Brain in place of the old Health / Damage FSM logic (both switched
    ///   off; the Health FSM still holds its health).
    /// Run it again any time to rebuild.
    /// </summary>
    public static class PumpkinBossSetupMenu
    {
        private const string FbxPath = "Assets/Models/Enemies/Pumpkin_Boss_Z.fbx";
        private const string ControllerPath = "Assets/Prefabs/Enemies/Pumpkin_Boss_Z_Brain.controller";
        private const string BossPath = "Assets/Prefabs/Enemies/pumpkin_boss_Z.prefab";
        private const string BugPumpkinPath = "Assets/Prefabs/Enemies/Enemies_Smarter/Bug_Pumpkin.prefab";
        private const string PumpkinModelPath = "Assets/Models/Bedroom_Stuffs/pumpkin_0.fbx";
        private const string SmokePath = "Assets/Lana Studio/Hyper Casual FX/Prefabs/Confetti/Smoke_Blast.prefab";
        private const string SmallBurstPath = "Assets/Prefabs/Items/Broken_Stuffs/pumpkin_0_drop.prefab";
        private const string BigBurstPath = "Assets/Prefabs/Items/Broken_Stuffs/Pumpkin_BOSS_Drop.prefab";
        private const string PumpkinDropPath = "Assets/Prefabs/Items/Breakable/Pumpkin_On_Tree.prefab";
        private const string Sfx = "Assets/Audio/SFX/";
        private static readonly string[] BugPaths =
        {
            "Assets/Prefabs/Enemies/Enemies_Smarter/Spider_Small.prefab",
            "Assets/Prefabs/Enemies/Enemies_Smarter/Cockroach_Small.prefab",
            "Assets/Prefabs/Enemies/Enemies_Smarter/Bee_Small.prefab",
        };
        private static readonly string[] States =
        {
            "laugh", "gethit", "come", "move", "attack", "clap", "being_hit", "punch_R", "punch_L", "transform", "run",
            "idle_true", "throw", "jump_slam", "vomit", "cyclone", "dizzy_true", "death",
        };
        private static readonly HashSet<string> Looping = new() { "laugh", "move", "run", "idle_true", "being_hit", "dizzy_true" };
        // Hit spheres on the bones: radius and how far along the bone the middle is, in metres of the model.
        private static readonly (string bone, float radius, float along)[] Spheres =
        {
            ("Head", 0.24f, 0.20f), ("Chest", 0.22f, 0.0f), ("Waist", 0.15f, 0.12f), ("Hips", 0.20f, 0.10f),
            ("Hand.L", 0.17f, 0.12f), ("Hand.R", 0.17f, 0.12f),
        };
        // Model units to the bones' own units (the FBX keeps centimetres under an armature scaled 100).
        private const float BoneUnit = 0.01f;

        [MenuItem("Tools/429 Game/Enemies/Set Up Pumpkin Boss")]
        public static void SetUp()
        {
            var notes = new List<string>();
            if (!SetUpClips(notes)) return;
            var controller = MakeController(notes);
            var bugPumpkin = MakeBugPumpkin(notes);
            if (controller == null || bugPumpkin == null) return;
            BuildBoss(controller, bugPumpkin, notes);
            AssetDatabase.SaveAssets();
            Debug.Log("[Pumpkin Boss] " + string.Join("; ", notes));
        }

        private static bool SetUpClips(List<string> notes)
        {
            if (AssetImporter.GetAtPath(FbxPath) is not ModelImporter importer)
            {
                Debug.LogError($"[Pumpkin Boss] {FbxPath} is missing. Export it from Blender/Pumpkin_Boss_Z.blend first.");
                return false;
            }
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                var name = clip.takeName.Replace("pumpkin_boss_", "");
                clip.name = name;
                clip.loopTime = Looping.Contains(name);
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            notes.Add($"{clips.Length} clips on Pumpkin_Boss_Z.fbx");
            return true;
        }

        private static AnimatorController MakeController(List<string> notes)
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath(FbxPath).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview")).ToDictionary(c => c.name);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
                             ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var machine = controller.layers[0].stateMachine;
            foreach (var name in States)
            {
                if (!clips.TryGetValue(name, out var clip))
                {
                    Debug.LogError($"[Pumpkin Boss] The FBX has no '{name}' clip.");
                    return null;
                }
                var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == name) ?? machine.AddState(name);
                state.motion = clip;
                state.writeDefaultValues = true;
            }
            // States whose clips are gone (renamed or removed in Blender) go too.
            foreach (var stale in machine.states.Select(s => s.state).Where(s => !States.Contains(s.name)).ToList())
                machine.RemoveState(stale);
            machine.defaultState = machine.states.First(s => s.state.name == "laugh").state;
            EditorUtility.SetDirty(controller);
            notes.Add($"Animator controller with {States.Length} states");
            return controller;
        }

        private static GameObject MakeBugPumpkin(List<string> notes)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(PumpkinModelPath);
            if (model == null) { Debug.LogError($"[Pumpkin Boss] {PumpkinModelPath} is missing."); return null; }
            var root = new GameObject("Bug_Pumpkin");
            try
            {
                var look = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                PrefabUtility.UnpackPrefabInstance(look, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                foreach (var c in look.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                var bounds = new Bounds(look.transform.position, Vector3.zero);
                foreach (var r in look.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
                float size = Mathf.Max(0.01f, bounds.size.magnitude);
                look.transform.localScale *= 0.55f / size;
                look.transform.localPosition = -bounds.center * (0.55f / size);
                var sphere = root.AddComponent<SphereCollider>();
                sphere.radius = 0.2f;
                var body = root.AddComponent<Rigidbody>();
                body.mass = 3f;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                var pumpkin = root.AddComponent<BugPumpkin>();
                var so = new SerializedObject(pumpkin);
                so.FindProperty("burstInto").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(SmallBurstPath);
                so.FindProperty("smoke").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(SmokePath);
                so.ApplyModifiedPropertiesWithoutUndo();
                var saved = PrefabUtility.SaveAsPrefabAsset(root, BugPumpkinPath);
                notes.Add("Bug_Pumpkin");
                return saved;
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void BuildBoss(AnimatorController controller, GameObject bugPumpkin, List<string> notes)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            var root = PrefabUtility.LoadPrefabContents(BossPath);
            try
            {
                // The old model's look, to put on the new one.
                Material[] looks = null;
                var oldMesh = root.transform.Find("pumpkin_boss") ?? root.transform.Find("Model/pumpkin_boss");
                if (oldMesh != null && oldMesh.TryGetComponent(out SkinnedMeshRenderer oldSkin)) looks = oldSkin.sharedMaterials;
                foreach (var old in new[] { "pumpkin_boss", "pumpkin_boss_armature", "Model" })
                {
                    var t = root.transform.Find(old);
                    if (t != null) Object.DestroyImmediate(t.gameObject);
                }
                var oldAnimator = root.GetComponent<Animator>();
                if (oldAnimator != null) Object.DestroyImmediate(oldAnimator);

                var model = new GameObject("Model").transform;
                model.SetParent(root.transform, false);
                var animator = model.gameObject.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                foreach (var child in instance.transform.Cast<Transform>().ToList()) child.SetParent(model, false);
                Object.DestroyImmediate(instance);
                var skin = model.GetComponentInChildren<SkinnedMeshRenderer>();
                if (skin != null && looks != null) skin.sharedMaterials = looks;
                if (skin != null) skin.updateWhenOffscreen = true;

                var bones = model.GetComponentsInChildren<Transform>(true).ToDictionary(t => t.name, t => t);
                foreach (var (bone, radius, along) in Spheres)
                {
                    if (!bones.TryGetValue(bone, out var t)) continue;
                    var sphere = t.gameObject.AddComponent<SphereCollider>();
                    sphere.radius = radius * BoneUnit;
                    sphere.center = new Vector3(0f, along * BoneUnit, 0f);
                    t.gameObject.tag = "Enemy";
                }

                foreach (var fsm in root.GetComponents<PlayMakerFSM>())
                {
                    if (fsm.FsmName == "Health" || fsm.FsmName == "Damage") fsm.enabled = false;
                }
                var agent = root.GetComponent<NavMeshAgent>();
                agent.speed = 3f;
                agent.acceleration = 12f;
                agent.angularSpeed = 360f;
                agent.stoppingDistance = 1f;
                agent.radius = 0.6f;
                agent.height = 2f;
                if (root.TryGetComponent(out Rigidbody rb)) { rb.isKinematic = true; rb.useGravity = false; }

                var hits = root.GetComponent<EnemyHitDamage>() ?? root.AddComponent<EnemyHitDamage>();
                var hitSo = new SerializedObject(hits);
                hitSo.FindProperty("toughness").floatValue = 1.5f;
                hitSo.FindProperty("knockBack").boolValue = false;
                hitSo.FindProperty("healthFsm").stringValue = "Health";
                hitSo.ApplyModifiedPropertiesWithoutUndo();

                var brain = root.GetComponent<PumpkinBossBrain>() ?? root.AddComponent<PumpkinBossBrain>();
                var so = new SerializedObject(brain);
                so.FindProperty("animator").objectReferenceValue = animator;
                so.FindProperty("model").objectReferenceValue = model;
                so.FindProperty("head").objectReferenceValue = bones.GetValueOrDefault("Head");
                so.FindProperty("jaw").objectReferenceValue = bones.GetValueOrDefault("Jaw");
                so.FindProperty("handL").objectReferenceValue = bones.GetValueOrDefault("Hand.L");
                so.FindProperty("handR").objectReferenceValue = bones.GetValueOrDefault("Hand.R");
                so.FindProperty("bugPumpkin").objectReferenceValue = bugPumpkin;
                so.FindProperty("pumpkinDrop").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(PumpkinDropPath);
                so.FindProperty("burstInto").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(BigBurstPath);
                so.FindProperty("smoke").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(SmokePath);
                SetArray(so.FindProperty("bugs"), BugPaths.Select(p => (Object)AssetDatabase.LoadAssetAtPath<GameObject>(p)).ToArray());
                SetArray(so.FindProperty("laughs"), new[] { "laughing_pumpkin_1", "laughing_pumpkin_2", "laughing_pumpkin_3", "pumpkin_laugh_1", "pumpkin_laugh_2", "pumpkin_laugh_3", "pumpkin_laugh_4" }
                    .Select(n => (Object)AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + n + ".wav")).ToArray());
                so.FindProperty("attackSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + "pumpkin_boss_attack.wav");
                so.FindProperty("hurtSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + "ouch_pumpkin_2.wav");
                so.FindProperty("roarSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + "pumpkin_boss_COME.wav");
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, BossPath);
                notes.Add("pumpkin_boss_Z rebuilt with the new model and Pumpkin Boss Brain");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void SetArray(SerializedProperty array, Object[] values)
        {
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}

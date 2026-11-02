using System.IO;
using System.Linq;
using Game.Menus;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Menu > Set Up Opening: builds the game's opening, Resources/Opening_Battle (Opening Cutscene, see
    /// there), anew each time; nothing in the scene changes.
    /// - Knight_Opening.fbx (exported from Blender/Knight.blend, with the clips made for this): its clips,
    ///   Opening_Knight.controller, played slower than they were exported (the Mixamo clips are 30 fps in a 60 fps file).
    /// - The Knights in colour: Knight_Red_Mat, Knight_Blue_Mat, Knight_Purple_Mat (Knight_Mat with the re-coloured paint,
    ///   M_Textures/Knight/X/Knight_Diffuse_*.png), and one in the first green.
    /// - The boss (Pumpkin_Boss_Z.fbx, its materials, no brain; Opening_Boss.controller, with a layer that keeps its legs standing
    ///   under its punches, whose clips tuck them away), its pumpkin (the look of its Bug Pumpkin:
    ///   pumpkin_0), Fat_Pumpkin_Drop for what that breaks into, the Pumpkin_On_Tree look on the pumpkins lying everywhere,
    ///   lights, the eyelids' material.
    /// - Exclamation_Marks (Prefabs/Effects): the "!!!" over the boss's head as it notices the player, 3 particles.
    /// - The dream world: Boss_Area's flames only (no FSM, teleport area, colliders, floor or Sky Light), the bonus stage's sky
    ///   wider, a ground nobody sees (429/Depth Only: it only hides what's under it), a soft glow on it, stars lying on it, low
    ///   mist, and dark under each Knight and pumpkin.
    /// </summary>
    public static class OpeningSetupMenu
    {
        private const string PrefabPath = "Assets/Resources/Opening_Battle.prefab";
        private const string KnightFbx = "Assets/Models/Enemies/Knight_Opening.fbx";
        private const string KnightControllerPath = "Assets/Prefabs/Enemies/Opening_Knight.controller";
        private const string KnightMaterialPath = "Assets/Materials/Knight_Mat.mat";
        private const string MaterialFolder = "Assets/Materials/";
        private const string PaintFolder = "Assets/M_Textures/Knight/X/";
        private const string BossFbx = "Assets/Models/Enemies/Pumpkin_Boss_Z.fbx";
        private const string BossPrefab = "Assets/Prefabs/Enemies/pumpkin_boss_Z.prefab";
        private const string BossControllerPath = "Assets/Prefabs/Enemies/Opening_Boss.controller";
        private static readonly string[] BossStates = { "idle_true", "notice", "laugh_true", "cyclone", "dizzy_true", "throw", "throw_L", "punch_L", "punch_R", "jump_slam" };
        // The look of the boss's Bug Pumpkin.
        private const string ThrownPumpkinFbx = "Assets/Models/Bedroom_Stuffs/pumpkin_0.fbx";
        private const string BrokenPumpkinPath = "Assets/Prefabs/Items/Broken_Stuffs/Fat_Pumpkin_Drop.prefab";
        private const string TreePumpkinPath = "Assets/Prefabs/Items/Breakable/Pumpkin_On_Tree.prefab";
        private const string PumpkinMaterialPath = "Assets/Materials/Bedroom_Set_1_Mat.mat";
        private const string ArenaPath = "Assets/Prefabs/Bases/Boss_Area.prefab";
        private const string SoftTexture = "Assets/Lana Studio/Hyper Casual FX/Textures/Circle03.png";
        private const string NoFogShader = "429/Particles/Unlit No Fog";
        // The ground's glow: as wide as this, in metres, and its colour.
        private const float DreamFloorSize = 150f;
        private static readonly Color DreamFloorColor = new(0.13f, 0.06f, 0.26f, 0.5f);
        private const string DepthOnlyShader = "429/Depth Only";
        private const string GroundMaskPath = "Assets/Data/Effects/Opening Ground Mask.mat";
        private const float GroundSize = 500f;
        private const float ShadowStrength = 0.6f;
        private const string SkyPath = "Assets/Prefabs/Bases/Bonus_Stage_Sky.prefab";
        // How much wider the sky is than over the bonus stage (and its stars as much bigger, to look the same).
        private const float SkyScale = 4f;
        // More of them, as they're all round now.
        private const float SkyMore = 1.6f;
        private const string SpinEffectPath = "Assets/Prefabs/Enemies/Boss_FX/Boss_Ground_Blast.prefab";
        private const string SplatEffectPath = "Assets/Lana Studio/Hyper Casual FX/Prefabs/Confetti/Hit_Blast.prefab";
        private const string MarkTexturePath = "Assets/Data/Effects/Exclamation_Mark.png";
        private const string MarksPrefabPath = "Assets/Prefabs/Effects/Exclamation_Marks.prefab";
        private const int MarkTextureSize = 256;
        // Each "!" in metres over the boss, how far apart, and how far after each other they pop up.
        private const float MarksSize = 6.5f;
        private const float MarksApart = 0.6f;
        private const float MarksOneAfterAnother = 0.07f;
        private const float MarksSeconds = 1.5f;
        private const string StunStarsPath = "Assets/Prefabs/Enemies/Hit_FX/Stun_Stars.prefab";
        private const string FistEffectPath = "Assets/Prefabs/Effects/Smoke/Smoke_Flash_Small_Puff.prefab";
        private const int KnightCount = 4;
        private const string LidsMaterialPath = "Assets/Data/Effects/Eye Lids.mat";
        private const string LidsShader = "429/Eye Lids";
        private const string Sfx = "Assets/Audio/SFX/";
        private const string Clangs = "Assets/Audio/Kenney Audio/kenney_sci-fi-sounds/Audio/impactMetal_00{0}.ogg";
        private const int ClangCount = 5;
        private const int AndroidSize = 1024;
        // The pumpkins lying everywhere, and how big they are.
        private const int PumpkinCount = 28;
        private const float PumpkinSize = 2f;
        private const float ThrownPumpkinSize = 5f;
        // The clip's name in the FBX, the state's name, whether it loops, and how fast it plays.
        private static readonly (string clip, string state, bool loop, float speed)[] KnightStates =
        {
            ("Pie_Idle", "Idle", true, 0.6f),
            ("Pie_Get_Up_R", "Get_Up_R", false, 0.6f),
            ("Pie_Get_Up_L", "Get_Up_L", false, 0.6f),
            ("Pie_Slow_Run", "Run", true, 0.85f),
            ("Pie_Atk_2", "Attack", true, 0.6f),
            ("Pie_Stucking_Head_On_Ground", "Stuck", true, 1f),
            ("Pie_Getting_Flung", "Flung", true, 1f),
            ("Pie_Grab_And_Throw", "Grab_Throw", false, 0.8f),
            ("Pie_Roll", "Roll", false, 1.2f),
        };

        [MenuItem("Tools/429 Game/Menu/Set Up Opening")]
        public static void Run()
        {
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == PrefabPath)
            {
                Debug.LogError($"[Opening] {PrefabPath} is open in Prefab Mode: close it first. Nothing changed.");
                return;
            }
            var missing = new[] { KnightFbx, BossFbx, KnightMaterialPath, ArenaPath }.Where(p => AssetDatabase.AssetPathToGUID(p).Length == 0).ToList();
            if (missing.Count > 0)
            {
                Debug.LogError("[Opening] Missing: " + string.Join(", ", missing) + ". Nothing changed.");
                return;
            }
            SetUpKnightImport();
            AddBossClip("notice");
            AddBossClip("laugh_true");
            var controller = MakeKnightController();
            var bossController = MakeBossController();
            var red = MakeKnightMaterial("Red");
            var blue = MakeKnightMaterial("Blue");
            var purple = MakeKnightMaterial("Purple");
            var green = AssetDatabase.LoadAssetAtPath<Material>(KnightMaterialPath);
            var glow = MakeGlowMaterial();
            var shadow = MakeShadowMaterial();
            var lids = MakeLidsMaterial();
            AssetDatabase.SaveAssets();
            MakePrefab(controller, bossController, red, blue, purple, green, glow, shadow, lids);
            Debug.Log($"[Opening] Built {PrefabPath}.");
        }

        // A take new in the boss's FBX: its clip list is written out, so a new take doesn't join it by itself.
        private static void AddBossClip(string name)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(BossFbx);
            var clips = importer.clipAnimations.ToList();
            if (clips.Any(c => c.name == name)) return;
            var take = importer.defaultClipAnimations.FirstOrDefault(c => c.takeName.EndsWith(name));
            if (take == null)
            {
                Debug.LogWarning($"[Opening] No take {name} in {BossFbx}.");
                return;
            }
            take.name = name;
            take.loopTime = false;
            clips.Add(take);
            importer.clipAnimations = clips.ToArray();
            importer.SaveAndReimport();
        }

        private static void SetUpKnightImport()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(KnightFbx);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            // Exactly as made: squeezed, their feet dip into the ground.
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Pie_Knight"), AssetDatabase.LoadAssetAtPath<Material>(KnightMaterialPath));
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                foreach (var (name, _, loop, _) in KnightStates)
                {
                    if (clip.name.EndsWith(name)) clip.loopTime = loop;
                }
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        private static AnimatorController MakeKnightController()
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(KnightControllerPath) != null) AssetDatabase.DeleteAsset(KnightControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(KnightControllerPath);
            var machine = controller.layers[0].stateMachine;
            var clips = AssetDatabase.LoadAllAssetsAtPath(KnightFbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToList();
            AnimatorState idle = null;
            foreach (var (clipName, stateName, _, speed) in KnightStates)
            {
                var clip = clips.FirstOrDefault(c => c.name.EndsWith(clipName));
                if (clip == null) Debug.LogWarning($"[Opening] No clip {clipName} in {KnightFbx}.");
                var state = machine.AddState(stateName);
                state.motion = clip;
                state.speed = speed;
                if (stateName == "Idle") idle = state;
            }
            machine.defaultState = idle;
            // Done getting the player up, it stands there until it's told to run.
            foreach (var child in machine.states)
            {
                if (!child.state.name.StartsWith("Get_Up")) continue;
                var back = child.state.AddTransition(idle);
                back.hasExitTime = true;
                back.exitTime = 1f;
                back.duration = 0.2f;
            }
            return controller;
        }

        // Its moves, and a layer on top that holds its hips and legs in its stance (the Opening Cutscene lets go of it for the moves
        // that use its legs).
        private static AnimatorController MakeBossController()
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(BossControllerPath) != null) AssetDatabase.DeleteAsset(BossControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(BossControllerPath);
            var clips = AssetDatabase.LoadAllAssetsAtPath(BossFbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToList();
            var machine = controller.layers[0].stateMachine;
            foreach (var name in BossStates)
            {
                var clip = clips.FirstOrDefault(c => c.name == name);
                if (clip == null) Debug.LogWarning($"[Opening] No clip {name} in {BossFbx}.");
                machine.AddState(name).motion = clip;
            }
            machine.defaultState = machine.states.First(s => s.state.name == BossStates[0]).state;

            var mask = new AvatarMask { name = "Boss Legs" };
            mask.AddTransformPath(AssetDatabase.LoadAssetAtPath<GameObject>(BossFbx).transform, true);
            for (int i = 0; i < mask.transformCount; i++)
            {
                string bone = Path.GetFileName(mask.GetTransformPath(i));
                mask.SetTransformActive(i, bone is "Root" or "Hips" || bone.StartsWith("Thigh") || bone.StartsWith("Shin") || bone.StartsWith("Foot"));
            }
            AssetDatabase.AddObjectToAsset(mask, controller);
            controller.AddLayer("Legs");
            var layers = controller.layers;
            layers[1].avatarMask = mask;
            layers[1].defaultWeight = 1f;
            layers[1].blendingMode = AnimatorLayerBlendingMode.Override;
            controller.layers = layers;
            layers[1].stateMachine.AddState(BossStates[0]).motion = clips.FirstOrDefault(c => c.name == BossStates[0]);
            return controller;
        }

        private static Material MakeKnightMaterial(string colour)
        {
            string texturePath = PaintFolder + "Knight_Diffuse_" + colour + ".png";
            if (AssetImporter.GetAtPath(texturePath) is TextureImporter importer)
            {
                var android = importer.GetPlatformTextureSettings("Android");
                if (!android.overridden || android.maxTextureSize != AndroidSize)
                {
                    android.overridden = true;
                    android.maxTextureSize = AndroidSize;
                    importer.SetPlatformTextureSettings(android);
                    importer.SaveAndReimport();
                }
            }
            string path = MaterialFolder + "Knight_" + colour + "_Mat.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var source = AssetDatabase.LoadAssetAtPath<Material>(KnightMaterialPath);
            if (material == null)
            {
                material = new Material(source);
                AssetDatabase.CreateAsset(material, path);
            }
            else material.CopyPropertiesFromMaterial(source);
            var paint = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", paint);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", paint);
            // Its glow is its paint too, or the first green shines through.
            if (material.HasProperty("_EmissionMap")) material.SetTexture("_EmissionMap", paint);
            EditorUtility.SetDirty(material);
            return material;
        }

        // A soft glow to stand on, with the stars showing through it.
        private static Material MakeGlowMaterial()
        {
            var material = DangerFlameSetupMenu.MakeMaterial("Opening Dream Floor", SoftTexture, true);
            var noFog = Shader.Find(NoFogShader);
            if (noFog != null) material.shader = noFog;
            material.SetColor("_BaseColor", DreamFloorColor);
            EditorUtility.SetDirty(material);
            return material;
        }

        // The dark under what stands on it.
        private static Material MakeShadowMaterial()
        {
            var material = DangerFlameSetupMenu.MakeMaterial("Opening Shadow", SoftTexture, false);
            var noFog = Shader.Find(NoFogShader);
            if (noFog != null) material.shader = noFog;
            material.SetColor("_BaseColor", new Color(0f, 0f, 0f, ShadowStrength));
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material MakeLidsMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(LidsMaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find(LidsShader));
                AssetDatabase.CreateAsset(material, LidsMaterialPath);
            }
            return material;
        }

        private static void MakePrefab(AnimatorController knights, AnimatorController bossController, Material red, Material blue, Material purple, Material green, Material glow, Material shadow, Material lids)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            var root = new GameObject("Opening_Battle");
            try
            {
                var boss = MakeBoss(root.transform, bossController);
                var lead = MakeKnight(root.transform, "Lead Knight", knights, red);
                var speaker = lead.gameObject.AddComponent<AudioSource>();
                speaker.playOnAwake = false;
                speaker.spatialBlend = 1f;
                speaker.rolloffMode = AudioRolloffMode.Linear;
                speaker.minDistance = 1.5f;
                speaker.maxDistance = 20f;
                var stuck = MakeKnight(root.transform, "Stuck Knight", knights, green);
                var thrower = MakeKnight(root.transform, "Thrower Knight", knights, blue);
                var fighter = MakeKnight(root.transform, "Fighting Knight", knights, purple);
                var thrown = MakeThrownPumpkin(root.transform);
                var pumpkinRoot = new GameObject("Pumpkins").transform;
                pumpkinRoot.SetParent(root.transform, false);
                var light = MakeLight(root.transform);
                MakeMoon(root.transform);
                var arena = MakeArena(root.transform);
                var floor = MakeDreamFloor(root.transform, glow);
                var sky = MakeSky(root.transform);

                var cutscene = root.AddComponent<OpeningCutscene>();
                var so = new SerializedObject(cutscene);
                so.FindProperty("boss").objectReferenceValue = boss;
                so.FindProperty("arena").objectReferenceValue = arena;
                so.FindProperty("dreamFloor").objectReferenceValue = floor;
                so.FindProperty("sky").objectReferenceValue = sky;
                so.FindProperty("fightLight").objectReferenceValue = light;
                so.FindProperty("leadKnight").objectReferenceValue = lead;
                so.FindProperty("stuckKnight").objectReferenceValue = stuck;
                so.FindProperty("throwerKnight").objectReferenceValue = thrower;
                so.FindProperty("fightKnight").objectReferenceValue = fighter;
                var shadows = so.FindProperty("knightShadows");
                shadows.arraySize = KnightCount;
                for (int i = 0; i < KnightCount; i++)
                {
                    var one = MakeShadow(root.transform, shadow);
                    one.name = $"Knight Shadow {i + 1}";
                    shadows.GetArrayElementAtIndex(i).objectReferenceValue = one;
                }
                var pumpkins = so.FindProperty("pumpkins");
                pumpkins.arraySize = PumpkinCount;
                var tree = AssetDatabase.LoadAssetAtPath<GameObject>(TreePumpkinPath);
                for (int i = 0; i < PumpkinCount; i++) pumpkins.GetArrayElementAtIndex(i).objectReferenceValue = MakeLyingPumpkin(pumpkinRoot, tree, shadow, i);
                so.FindProperty("pumpkinLift").floatValue = -tree.GetComponent<MeshFilter>().sharedMesh.bounds.min.y * PumpkinSize;
                so.FindProperty("flyingPumpkin").objectReferenceValue = thrown;
                so.FindProperty("noticeMarks").objectReferenceValue = MakeNoticeMarks(root.transform);
                so.FindProperty("brokenPumpkin").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(BrokenPumpkinPath);
                var splat = AssetDatabase.LoadAssetAtPath<GameObject>(SplatEffectPath);
                so.FindProperty("splashEffect").objectReferenceValue = splat;
                so.FindProperty("splatEffect").objectReferenceValue = splat;
                so.FindProperty("spinEffect").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(SpinEffectPath);
                so.FindProperty("stunStars").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(StunStarsPath);
                so.FindProperty("fistEffect").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(FistEffectPath);
                so.FindProperty("lidsMaterial").objectReferenceValue = lids;
                // A stand-in of the user's own voice, until the real line is recorded.
                so.FindProperty("wakeVoice").objectReferenceValue = Clip("welcome_back_1.wav");
                so.FindProperty("bossRoar").objectReferenceValue = Clip("pumpkin_roar.wav");
                so.FindProperty("spinSound").objectReferenceValue = Clip("pumpkin_boss_attack.wav");
                so.FindProperty("slapSound").objectReferenceValue = Clip("fist_bump_punch.wav");
                so.FindProperty("punchSound").objectReferenceValue = Clip("pumpkin_boss_attack.wav");
                so.FindProperty("flySound").objectReferenceValue = Clip("whoosh.wav");
                so.FindProperty("splatSound").objectReferenceValue = Clip("pumpkin_explode_1.wav");
                // Only its own deep laughs: the others are the small pumpkins'.
                SetClips(so.FindProperty("bossLaughs"), Clip("pumpkin_boss_laugh.wav"), Clip("pumpkin_boss_laugh_2.wav"));
                SetClips(so.FindProperty("hitSounds"), Clip("pumpkin_explode_1.wav"), Clip("bone_bonk_1.wav"));
                var clangs = new AudioClip[ClangCount];
                for (int i = 0; i < ClangCount; i++) clangs[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(string.Format(Clangs, i));
                SetClips(so.FindProperty("clangs"), clangs);
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        // "!!!", one after another, popping up big and settling (Exclamation_Marks, in Prefabs/Effects), and a copy here.
        private static Transform MakeNoticeMarks(Transform parent)
        {
            if (AssetImporter.GetAtPath(MarkTexturePath) is TextureImporter importer)
            {
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                var android = importer.GetPlatformTextureSettings("Android");
                android.overridden = true;
                android.maxTextureSize = MarkTextureSize;
                importer.SetPlatformTextureSettings(android);
                importer.SaveAndReimport();
            }
            var material = DangerFlameSetupMenu.MakeMaterial("Exclamation Mark", MarkTexturePath, false);
            var noFog = Shader.Find(NoFogShader);
            if (noFog != null) material.shader = noFog;
            EditorUtility.SetDirty(material);

            var root = new GameObject("Exclamation_Marks");
            try
            {
                for (int i = 0; i < 3; i++)
                {
                    int place = i - 1;
                    var ps = NewParticles(root.transform, $"Mark {i + 1}", material, ParticleSystemRenderMode.Billboard, 1);
                    ps.transform.localPosition = new Vector3(place * MarksApart, place == 0 ? 0.12f : 0f, 0f);
                    var main = ps.main;
                    main.loop = false;
                    main.prewarm = false;
                    main.playOnAwake = false;
                    main.duration = MarksSeconds;
                    main.startLifetime = MarksSeconds;
                    main.startDelay = i * MarksOneAfterAnother;
                    main.startSize = 1f;
                    main.startRotation = -place * 12f * Mathf.Deg2Rad;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                    main.simulationSpace = ParticleSystemSimulationSpace.Local;
                    var emission = ps.emission;
                    emission.rateOverTime = 0f;
                    emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
                    var shape = ps.shape;
                    shape.enabled = false;
                    var grow = ps.sizeOverLifetime;
                    grow.enabled = true;
                    grow.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.07f, 1.35f), new Keyframe(0.14f, 0.95f), new Keyframe(0.2f, 1.05f), new Keyframe(0.28f, 1f), new Keyframe(1f, 1f)));
                    var colour = ps.colorOverLifetime;
                    colour.enabled = true;
                    var fade = new Gradient();
                    fade.SetKeys(
                        new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                        new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
                    colour.color = fade;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(MarksPrefabPath));
                PrefabUtility.SaveAsPrefabAsset(root, MarksPrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
            var marks = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(MarksPrefabPath), parent);
            marks.name = "Notice Marks";
            marks.transform.localScale = Vector3.one * MarksSize;
            return marks.transform;
        }

        private static Animator MakeBoss(Transform parent, AnimatorController controller)
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BossFbx), parent);
            model.name = "Boss";
            var animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            // The look the game's boss has.
            var game = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefab);
            if (game != null)
            {
                var looks = game.GetComponentsInChildren<Renderer>(true);
                foreach (var part in model.GetComponentsInChildren<Renderer>(true))
                {
                    var same = looks.FirstOrDefault(r => r.name == part.name);
                    if (same != null) part.sharedMaterials = same.sharedMaterials;
                }
            }
            var speaker = model.AddComponent<AudioSource>();
            speaker.playOnAwake = false;
            speaker.spatialBlend = 1f;
            speaker.rolloffMode = AudioRolloffMode.Linear;
            speaker.minDistance = 40f;
            speaker.maxDistance = 120f;
            return animator;
        }

        private static Animator MakeKnight(Transform parent, string name, AnimatorController controller, Material paint)
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(KnightFbx), parent);
            model.name = name;
            var animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            foreach (var part in model.GetComponentsInChildren<Renderer>(true)) part.sharedMaterial = paint;
            return animator;
        }

        private static Transform MakeThrownPumpkin(Transform parent)
        {
            var look = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ThrownPumpkinFbx), parent);
            look.name = "Flying Pumpkin";
            look.transform.localScale = Vector3.one * ThrownPumpkinSize;
            Dress(look);
            return look.transform;
        }

        // Pumpkin_On_Tree's look (no FSMs or physics of its own), and dark on the ground under it.
        private static Transform MakeLyingPumpkin(Transform parent, GameObject tree, Material shadow, int index)
        {
            var look = new GameObject($"Pumpkin {index + 1}");
            look.transform.SetParent(parent, false);
            look.transform.localScale = Vector3.one * PumpkinSize;
            look.AddComponent<MeshFilter>().sharedMesh = tree.GetComponent<MeshFilter>().sharedMesh;
            var renderer = look.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = tree.GetComponent<MeshRenderer>().sharedMaterials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var under = MakeShadow(look.transform, shadow);
            under.name = "Shadow";
            under.localPosition = Vector3.up * (tree.GetComponent<MeshFilter>().sharedMesh.bounds.min.y + 0.02f);
            under.localScale = Vector3.one * 0.85f;
            return look.transform;
        }

        // A soft dark circle lying on the ground.
        private static Transform MakeShadow(Transform parent, Material shadow)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Shadow";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(parent, false);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var look = quad.GetComponent<MeshRenderer>();
            look.sharedMaterial = shadow;
            look.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            look.receiveShadows = false;
            return quad.transform;
        }

        // What they stand on: a ground nobody sees (it hides what's under it, like a head stuck in it), a glow, stars
        // lying on it and low mist.
        private static Transform MakeDreamFloor(Transform parent, Material glow)
        {
            var dream = new GameObject("Dream Floor").transform;
            dream.SetParent(parent, false);
            var mask = AssetDatabase.LoadAssetAtPath<Material>(GroundMaskPath);
            if (mask == null)
            {
                mask = new Material(Shader.Find(DepthOnlyShader));
                AssetDatabase.CreateAsset(mask, GroundMaskPath);
            }
            MakeFlat(dream, "Ground", mask, GroundSize, 0f);
            MakeFlat(dream, "Glow", glow, DreamFloorSize, 0.02f);
            var soft = AssetDatabase.LoadAssetAtPath<Material>("Assets/Data/Effects/Bonus Sky Glow.mat");
            MakeGroundStars(dream, soft);
            MakeMist(dream, soft);
            return dream;
        }

        private static void MakeFlat(Transform parent, string name, Material material, float size, float height)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = Vector3.up * height;
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = Vector3.one * size;
            var look = quad.GetComponent<MeshRenderer>();
            look.sharedMaterial = material;
            look.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            look.receiveShadows = false;
        }

        private static ParticleSystem NewParticles(Transform parent, string name, Material material, ParticleSystemRenderMode mode, int most)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.maxParticles = most;
            main.startSpeed = 0f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = mode;
            renderer.maxParticleSize = 100f;
            return ps;
        }

        private static void FadeInOut(ParticleSystem ps, float strength)
        {
            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(strength, 0.35f), new GradientAlphaKey(strength, 0.65f), new GradientAlphaKey(0f, 1f) });
            colour.color = fade;
        }

        // Little stars lying flat on the ground, coming and going.
        private static void MakeGroundStars(Transform parent, Material material)
        {
            var ps = NewParticles(parent, "Ground Stars", material, ParticleSystemRenderMode.HorizontalBillboard, 320);
            ps.transform.localPosition = Vector3.up * 0.04f;
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.34f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.85f, 0.85f, 1f), new Color(0.7f, 0.55f, 1f));
            var emission = ps.emission;
            emission.rateOverTime = 70f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 55f;
            shape.radiusThickness = 1f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            FadeInOut(ps, 1f);
        }

        // Faint violet and teal mist lying low on the ground.
        private static void MakeMist(Transform parent, Material material)
        {
            var ps = NewParticles(parent, "Mist", material, ParticleSystemRenderMode.Billboard, 20);
            ps.transform.localPosition = Vector3.up * 1.2f;
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(12f, 18f);
            main.startSize = new ParticleSystem.MinMaxCurve(14f, 28f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.5f, 0.22f, 0.85f), new Color(0.14f, 0.5f, 0.7f));
            var emission = ps.emission;
            emission.rateOverTime = 1.2f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(90f, 1.5f, 70f);
            FadeInOut(ps, 0.14f);
        }

        // The tree pumpkins' look, nothing to bump into.
        private static void Dress(GameObject look)
        {
            foreach (var c in look.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            var material = AssetDatabase.LoadAssetAtPath<Material>(PumpkinMaterialPath);
            if (material == null) return;
            foreach (var r in look.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = material;
        }

        // Only Boss_Area's flames (Flame Flicker stays). Its Info FSM (it would take the place of the bonus stage's field),
        // teleport area, floor and Sky Light are left out: this field is a dream.
        private static Transform MakeArena(Transform parent)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ArenaPath);
            var arena = (GameObject)PrefabUtility.InstantiatePrefab(source, parent);
            PrefabUtility.UnpackPrefabInstance(arena, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            arena.name = "Arena";
            foreach (var script in arena.GetComponentsInChildren<MonoBehaviour>(true))
            {
                string kind = script.GetType().Name;
                if (kind == "PlayMakerFSM" || kind.Contains("Teleport")) Object.DestroyImmediate(script);
            }
            foreach (var part in arena.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(part);
            foreach (var speaker in arena.GetComponentsInChildren<AudioSource>(true)) Object.DestroyImmediate(speaker);
            foreach (var name in new[] { "Cube", "Sky Light" })
            {
                var part = arena.transform.Find(name);
                if (part != null) Object.DestroyImmediate(part.gameObject);
            }
            return arena.transform;
        }

        // The bonus stage's sky, far enough off that the boss stands under it, and all round: the stars show under the ground too.
        private static Transform MakeSky(Transform parent)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SkyPath);
            if (source == null) return null;
            var sky = (GameObject)PrefabUtility.InstantiatePrefab(source, parent);
            PrefabUtility.UnpackPrefabInstance(sky, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            sky.name = "Sky";
            sky.transform.localScale = Vector3.one * SkyScale;
            foreach (var ps in sky.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                var size = main.startSize;
                if (size.mode == ParticleSystemCurveMode.Constant) size.constant *= SkyScale;
                else
                {
                    size.constantMin *= SkyScale;
                    size.constantMax *= SkyScale;
                }
                main.startSize = size;
                if (ps.name is "Stars" or "Bright Stars" or "Clouds")
                {
                    var shape = ps.shape;
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.rotation = Vector3.zero;
                    main.maxParticles = Mathf.RoundToInt(main.maxParticles * SkyMore);
                    var emission = ps.emission;
                    var rate = emission.rateOverTime;
                    rate.constant *= SkyMore;
                    emission.rateOverTime = rate;
                }
            }
            return sky.transform;
        }

        // A soft moon over it all, without shadows: the game has none of its own.
        private static void MakeMoon(Transform parent)
        {
            var go = new GameObject("Moon");
            go.transform.SetParent(parent, false);
            go.transform.rotation = Quaternion.Euler(45f, 200f, 0f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.62f, 0.68f, 0.95f);
            light.intensity = 0.7f;
            light.shadows = LightShadows.None;
        }

        // Warm firelight on the fight, in front of the boss (Opening Cutscene puts it there).
        private static Light MakeLight(Transform parent)
        {
            var go = new GameObject("Fight Light");
            go.transform.SetParent(parent, false);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.62f, 0.32f);
            light.range = 45f;
            light.intensity = 60f;
            light.shadows = LightShadows.None;
            return light;
        }

        private static AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + name);

        private static void SetClips(SerializedProperty list, params AudioClip[] clips)
        {
            list.arraySize = clips.Length;
            for (int i = 0; i < clips.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using Game.Combat;
using Game.Enemies;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Game.EditorTools
{
    /// <summary>
    /// Makes the smarter bee the same way as the smarter spider and cockroach (Tools > 429 Game > Enemies):
    /// Bee_Smarter (a copy of Bee with Spider Brain instead of its old "Bee" FSM), its size variants Bee_Small / _Normal /
    /// _Boss, and Bee_Boss_PumpkinButt (a variant of the boss). The bee hovers in its own animations, so the brain moves
    /// it on the NavMesh like the others. Bees fly (Spider Brain > Flying): they attack without waiting for a turn, and at
    /// 40% health they lose their wings, drop to the floor and behave like spiders.
    /// Bee_Boss takes TheBee's place (its pumpkin crown, toughness) and wears the white bee material
    /// (Bee_Smol_White_Painted_Mat); it sets whack_6 in GAMESTAGES when it dies. Bee_Boss_PumpkinButt takes TheBee 1's
    /// (its pumpkin and skull mask, its own look) and sets whack_7. Both come out of the whack barrels (Set Up Bee Barrels).
    /// Run it again any time: whatever is already set up keeps its settings.
    /// </summary>
    public static class SmarterBeeSetupMenu
    {
        private const string Folder = "Assets/Prefabs/Enemies/Enemies_Smarter/";
        private const string OldBeePath = "Assets/Prefabs/Enemies/Bee.prefab";
        private const string BeePath = Folder + "Bee_Smarter.prefab";
        private const string VariantPath = Folder + "Bee_{0}.prefab";
        private const string PumpkinButtPath = Folder + "Bee_Boss_PumpkinButt.prefab";
        private const string TheBeePath = "Assets/Prefabs/Enemies/TheBee.prefab";
        private const string TheBee1Path = "Assets/Prefabs/Enemies/TheBee 1.prefab";
        private const string WhiteMaterialPath = "Assets/Materials/Bee_Smol_White_Painted_Mat.mat";
        private const string OldBrainFsm = "Bee";
        private const string AttackSound = "Assets/Audio/SFX/bug_Attack.wav";
        private const string BeeModelPath = "Assets/Models/Enemies/Bee_Smol_&_Anims.fbx";
        // The whack stages go on when their boss bee is down.
        private const string StagesObject = "GAMESTAGES";
        private const string BossBool = "whack_6";
        private const string PumpkinButtBool = "whack_7";

        [MenuItem("Tools/429 Game/Enemies/Set Up Smarter Bee")]
        public static void SetUp()
        {
            var notes = new List<string>();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BeePath) == null)
            {
                if (!AssetDatabase.CopyAsset(OldBeePath, BeePath))
                {
                    Debug.LogError($"[Smarter Bee] Couldn't copy {OldBeePath}. Nothing changed.");
                    return;
                }
                notes.Add("made Bee_Smarter (a copy of Bee)");
            }

            var root = PrefabUtility.LoadPrefabContents(BeePath);
            try
            {
                var oldBrain = FindFsm(root, OldBrainFsm);
                var damageFsm = FindFsm(root, "Damage");
                if (!root.TryGetComponent(out SpiderBrain brain))
                {
                    brain = root.AddComponent<SpiderBrain>();
                    var models = root.transform.Find("Bee_Models");
                    var s = new SerializedObject(brain);
                    s.FindProperty("models").objectReferenceValue = models;
                    s.FindProperty("animator").objectReferenceValue = models != null ? models.GetComponent<Animator>() : root.GetComponentInChildren<Animator>(true);
                    var smoke = FindDeep(root.transform, "Black_Smoke_Blast");
                    s.FindProperty("hitEffect").objectReferenceValue = smoke != null ? smoke.gameObject : null;
                    SetClips(s.FindProperty("hitSounds"), Clips(damageFsm, "Get_Hit_Sound"));
                    SetClips(s.FindProperty("noticeSounds"), Clips(oldBrain, "Alert_Sound"));

                    // The bee's Animator states (Bee_Animator): two flying loops, no walk back, dodge or appear yet.
                    s.FindProperty("idleAnimation").stringValue = "idle";
                    s.FindProperty("walkAnimation").stringValue = "run2";
                    s.FindProperty("runAnimation").stringValue = "run";
                    s.FindProperty("alertAnimation").stringValue = "alert";
                    s.FindProperty("hitAnimation").stringValue = "hit";
                    s.FindProperty("walkBackAnimation").stringValue = "";
                    s.FindProperty("appearAnimation").stringValue = "";
                    s.FindProperty("dodgeBackAnimation").stringValue = "";
                    s.FindProperty("dodgeLeftAnimation").stringValue = "";
                    s.FindProperty("dodgeRightAnimation").stringValue = "";
                    s.FindProperty("runSpeed").floatValue = 2.8f;
                    s.FindProperty("walkSpeed").floatValue = 0.8f;

                    // Sizes: the old Bee was scale 1 with 5 health, Bee_Small 3, the old bosses 2.2-2.5 with 21.
                    Size(s, "small", new Vector2(0.7f, 0.85f), new Vector2Int(2, 3), 1.1f, 1f, "", 0.5f);
                    Size(s, "normal", new Vector2(0.95f, 1.1f), new Vector2Int(4, 5), 1f, 1f, "", 0.35f);
                    Size(s, "boss", new Vector2(2.2f, 2.5f), new Vector2Int(20, 22), 0.9f, 1.3f, "DamageHeavy", 0.15f);

                    // One sting on its attack animation (a 1 s loop).
                    var attacks = s.FindProperty("attacks");
                    attacks.arraySize = 1;
                    var sting = attacks.GetArrayElementAtIndex(0);
                    sting.FindPropertyRelative("name").stringValue = "Sting";
                    sting.FindPropertyRelative("usedBy").intValue = 7;
                    sting.FindPropertyRelative("animation").stringValue = "attack";
                    sting.FindPropertyRelative("weight").floatValue = 1f;
                    sting.FindPropertyRelative("minDistance").floatValue = 0f;
                    sting.FindPropertyRelative("maxDistance").floatValue = 1.6f;
                    sting.FindPropertyRelative("lunge").floatValue = 0.8f;
                    sting.FindPropertyRelative("hitFrom").floatValue = 0.35f;
                    sting.FindPropertyRelative("hitUntil").floatValue = 0.65f;
                    sting.FindPropertyRelative("duration").floatValue = 1f;
                    sting.FindPropertyRelative("reach").floatValue = 0.3f;
                    sting.FindPropertyRelative("biteRadius").floatValue = 0.25f;
                    sting.FindPropertyRelative("playerEvent").stringValue = "Damage";
                    sting.FindPropertyRelative("sound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(AttackSound);
                    s.ApplyModifiedPropertiesWithoutUndo();
                    notes.Add("added the brain (Spider Brain) with the bee's states, sounds, sizes and one Sting");
                }

                if (SetUpFlying(new SerializedObject(brain))) notes.Add("bees fly: they attack without waiting for a turn, and lose their wings at 40% health");
                var hooked = HookUpAnimations(brain);
                if (hooked.Count > 0) notes.Add("Bee_Animator got " + string.Join(", ", hooked));

                if (oldBrain != null)
                {
                    int unhooked = SwitchOffActionsAimedAt(root, OldBrainFsm);
                    Object.DestroyImmediate(oldBrain, true);
                    notes.Add($"removed the old \"Bee\" FSM and switched off {unhooked} action(s) that pointed at it");
                }
                var bubble = root.transform.Find("ProximityBubble");
                if (bubble != null && bubble.gameObject.activeSelf)
                {
                    bubble.gameObject.SetActive(false);
                    notes.Add("switched off ProximityBubble (the old FSM's sensing sphere)");
                }
                if (root.TryGetComponent(out NavMeshAgent agent) && agent.acceleration < 10f)
                {
                    agent.acceleration = 20f;
                    agent.angularSpeed = 720f;
                    agent.autoBraking = true;
                    agent.stoppingDistance = 0.1f;
                    notes.Add("NavMesh Agent: quick acceleration and turning (the brain sets the speed)");
                }
                PrefabUtility.SaveAsPrefabAsset(root, BeePath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            foreach (var sizeClass in new[] { SpiderBrain.SizeClass.Small, SpiderBrain.SizeClass.Normal, SpiderBrain.SizeClass.Boss })
            {
                if (MakeSizeVariant(sizeClass)) notes.Add($"made Bee_{sizeClass}");
            }
            SetUpBoss(notes);
            if (MakePumpkinButtBoss()) notes.Add($"made Bee_Boss_PumpkinButt (TheBee 1's pumpkin and skull mask; {PumpkinButtBool} in {StagesObject})");

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(BeePath);
            Debug.Log("[Smarter Bee] " + (notes.Count > 0 ? string.Join("; ", notes) : "already set up, nothing to change") + ".");
        }

        // Flying (Spider Brain > Flying): once, when its wing bones aren't set yet. All the bee's animations stay at the
        // root (re-exported 2026-10-10), so the brain alone sets its height. The ground animation names are filled in
        // ahead: once states with those names are in Bee_Animator they're used, until then the flying ones stand in.
        // On the ground it walks on Bee_Walk (ground_walk / ground_run), whose feet move about 0.15 m/s at size 1.
        private static bool SetUpFlying(SerializedObject s)
        {
            var wingBones = s.FindProperty("wingBones");
            if (wingBones.arraySize > 0) return false;
            s.FindProperty("flies").boolValue = true;
            s.FindProperty("groundBelowHealth").floatValue = 0.4f;
            // Both pairs of wings: the lower (D_Wing) and the upper (Up_Wing), each on the spine.
            SetStrings(wingBones, "D_Wing_01.L", "D_Wing_01.R", "Up_Wing_01.L", "Up_Wing_01.R");
            // It starts out flying and never lands while it has its wings: it hovers on Bee_Fly_2 when idle. Bee_Idle
            // (sitting, no wings) is its idle on the ground.
            s.FindProperty("idleAnimation").stringValue = "run2";
            // Dying it plays Bee_Death: on the ground, no wings (a bee killed in the air falls while it plays).
            s.FindProperty("deathAnimation").stringValue = "dead";
            s.FindProperty("wingsOffAnimation").stringValue = "wings_off";
            s.FindProperty("animationHover").floatValue = 0f;
            SetStrings(s.FindProperty("onFloorAnimations"), "idle", "run", "run2", "alert", "hit", "attack");
            s.FindProperty("groundSpeed").floatValue = 0.2f;
            s.FindProperty("groundRunAnimationSpeed").floatValue = 0.15f;
            s.FindProperty("groundIdleAnimation").stringValue = "ground_idle";
            s.FindProperty("groundWalkAnimation").stringValue = "ground_walk";
            s.FindProperty("groundRunAnimation").stringValue = "ground_run";
            s.FindProperty("groundAlertAnimation").stringValue = "ground_alert";
            s.FindProperty("groundHitAnimation").stringValue = "ground_hit";
            // Dodges in the air (0.4 s, made from Bee_Fly_2). A bee without wings doesn't dodge.
            s.FindProperty("dodgeLeftAnimation").stringValue = "dodge_left";
            s.FindProperty("dodgeRightAnimation").stringValue = "dodge_right";
            s.FindProperty("dodgeBackAnimation").stringValue = "dodge_back";
            s.FindProperty("dodgeSeconds").floatValue = 0.4f;

            // The sting is the flying attack; a ground sting takes over once its animation is made.
            var attacks = s.FindProperty("attacks");
            for (int i = 0; i < attacks.arraySize; i++)
            {
                var attack = attacks.GetArrayElementAtIndex(i);
                if (attack.FindPropertyRelative("name").stringValue == "Sting") attack.FindPropertyRelative("usedWhen").enumValueIndex = (int)SpiderBrain.When.Flying;
            }
            attacks.arraySize++;
            var ground = attacks.GetArrayElementAtIndex(attacks.arraySize - 1);
            ground.FindPropertyRelative("name").stringValue = "Ground Sting";
            ground.FindPropertyRelative("usedBy").intValue = 7;
            ground.FindPropertyRelative("usedWhen").enumValueIndex = (int)SpiderBrain.When.Grounded;
            ground.FindPropertyRelative("animation").stringValue = "ground_attack";
            ground.FindPropertyRelative("weight").floatValue = 1f;
            ground.FindPropertyRelative("minDistance").floatValue = 0f;
            ground.FindPropertyRelative("maxDistance").floatValue = 1.6f;
            ground.FindPropertyRelative("lunge").floatValue = 0.8f;
            // Bee_Ground_Attack: cocked at 0.23 s, the stinger strikes from 0.25 s and stays out until 0.5 s.
            ground.FindPropertyRelative("hitFrom").floatValue = 0.25f;
            ground.FindPropertyRelative("hitUntil").floatValue = 0.5f;
            ground.FindPropertyRelative("duration").floatValue = 0.9f;
            ground.FindPropertyRelative("reach").floatValue = 0.3f;
            ground.FindPropertyRelative("biteRadius").floatValue = 0.25f;
            ground.FindPropertyRelative("playerEvent").stringValue = "Damage";
            ground.FindPropertyRelative("sound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(AttackSound);
            ground.FindPropertyRelative("fsmEvent").stringValue = "";
            ground.FindPropertyRelative("leapToFace").boolValue = false;
            ground.FindPropertyRelative("runAwayAfter").boolValue = false;
            s.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        // Animator states for clips in the bee's FBX, each state on its clip: losing its wings, the wingless bee's idle,
        // alert, flinch, walk (Bee_Walk as ground_walk and ground_run) and sting, and its dodges in the air (made from
        // Bee_Fly_2).
        // The ground clips have no wings (their wing bones are scaled to nothing).
        private static readonly (string state, string clip)[] BeeStates =
        {
            ("wings_off", "Bee_Wings_Off"),
            ("ground_idle", "Bee_Idle"),
            ("ground_walk", "Bee_Walk"),
            ("ground_run", "Bee_Walk"),
            ("ground_alert", "Bee_Ground_Alert"),
            ("ground_hit", "Bee_Ground_Hit"),
            ("ground_attack", "Bee_Ground_Attack"),
            ("dodge_left", "Bee_Dodge_Left"),
            ("dodge_right", "Bee_Dodge_Right"),
            ("dodge_back", "Bee_Dodge_Back"),
        };

        // Imports the FBX takes those states need (Unity only imports the takes listed in its clips), then adds each
        // state that's missing next to the others. Returns the states it added.
        private static List<string> HookUpAnimations(SpiderBrain brain)
        {
            var added = new List<string>();
            ImportTakes(BeeStates.Select(b => b.clip).Distinct());
            var animator = new SerializedObject(brain).FindProperty("animator").objectReferenceValue as Animator;
            var controller = animator != null ? animator.runtimeAnimatorController as AnimatorController : null;
            if (controller == null) return added;
            var clips = AssetDatabase.LoadAllAssetsAtPath(BeeModelPath).OfType<AnimationClip>().ToList();
            var machine = controller.layers[0].stateMachine;
            var like = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "run");
            var near = machine.states.FirstOrDefault(s => s.state.name == "run").position;
            int row = 0;
            foreach (var (stateName, clipName) in BeeStates)
            {
                row++;
                var clip = clips.FirstOrDefault(c => c.name == clipName);
                if (clip == null || machine.states.Any(s => s.state.name == stateName)) continue;
                var state = machine.AddState(stateName, near + new Vector3(0f, 60f * row + 60f, 0f));
                state.motion = clip;
                if (like != null) state.writeDefaultValues = like.writeDefaultValues;
                added.Add($"{stateName} ({clipName})");
            }
            if (added.Count > 0) AssetDatabase.SaveAssets();
            return added;
        }

        // Adds clips for FBX takes that aren't imported yet, set up like Bee_Hit (playing once).
        private static void ImportTakes(IEnumerable<string> takes)
        {
            if (!(AssetImporter.GetAtPath(BeeModelPath) is ModelImporter importer)) return;
            var clips = importer.clipAnimations.ToList();
            if (clips.Count == 0) return;   // no list: Unity imports every take already
            var like = clips.FirstOrDefault(c => c.name == "Bee_Hit") ?? clips[0];
            bool changed = false;
            foreach (var take in importer.importedTakeInfos)
            {
                if (!takes.Contains(take.name) || clips.Any(c => c.takeName == take.name)) continue;
                clips.Add(new ModelImporterClipAnimation
                {
                    name = take.name,
                    takeName = take.name,
                    firstFrame = Mathf.Round(take.startTime * take.sampleRate),
                    lastFrame = Mathf.Round(take.stopTime * take.sampleRate),
                    loopTime = false,
                    wrapMode = like.wrapMode,
                    keepOriginalOrientation = like.keepOriginalOrientation,
                    keepOriginalPositionXZ = like.keepOriginalPositionXZ,
                    keepOriginalPositionY = like.keepOriginalPositionY,
                });
                changed = true;
            }
            if (!changed) return;
            importer.clipAnimations = clips.ToArray();
            importer.SaveAndReimport();
        }

        private static void SetStrings(SerializedProperty array, params string[] values)
        {
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).stringValue = values[i];
        }

        private static void Size(SerializedObject s, string name, Vector2 scale, Vector2Int health, float speed, float windUp, string playerEvent, float dodgeChance)
        {
            var p = s.FindProperty(name);
            p.FindPropertyRelative("scale").vector2Value = scale;
            p.FindPropertyRelative("health").vector2IntValue = health;
            p.FindPropertyRelative("speed").floatValue = speed;
            p.FindPropertyRelative("windUp").floatValue = windUp;
            p.FindPropertyRelative("playerEvent").stringValue = playerEvent;
            p.FindPropertyRelative("dodgeChance").floatValue = dodgeChance;
            p.FindPropertyRelative("webs").boolValue = false;
        }

        private static bool MakeSizeVariant(SpiderBrain.SizeClass sizeClass)
        {
            string path = string.Format(VariantPath, sizeClass);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return false;
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BeePath), scene);
                var s = new SerializedObject(instance.GetComponent<SpiderBrain>());
                s.FindProperty("size").enumValueIndex = (int)sizeClass;
                s.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(instance, path);
                return true;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        // Bee_Boss: TheBee's crown, Bulldozer (if it has one) and toughness, the white bee look; whack_6 when it dies.
        private static void SetUpBoss(List<string> notes)
        {
            string path = string.Format(VariantPath, SpiderBrain.SizeClass.Boss);
            var oldBoss = AssetDatabase.LoadAssetAtPath<GameObject>(TheBeePath);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null || oldBoss == null) return;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool changed = false;
                if (root.GetComponent<Bulldozer>() == null && oldBoss.TryGetComponent(out Bulldozer original))
                {
                    EditorUtility.CopySerialized(original, root.AddComponent<Bulldozer>());
                    notes.Add("Bee_Boss got TheBee's Bulldozer");
                    changed = true;
                }
                var brain = root.GetComponent<SpiderBrain>();
                var flags = new SerializedObject(brain);
                var list = flags.FindProperty("setOnDeath");
                if (!HasFlag(list))
                {
                    list.arraySize = 0;
                    SetFlag(list, StagesObject, StagesObject, BossBool);
                    flags.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(brain);
                    notes.Add($"Bee_Boss sets {BossBool} in {StagesObject} when it dies");
                    changed = true;
                }
                if (CopyGear(oldBoss, root) > 0)
                {
                    notes.Add("Bee_Boss wears TheBee's crown");
                    changed = true;
                }
                if (CopyToughness(oldBoss, root)) changed = true;
                var white = AssetDatabase.LoadAssetAtPath<Material>(WhiteMaterialPath);
                if (white != null && Wear(root, white))
                {
                    notes.Add("Bee_Boss wears the white bee material");
                    changed = true;
                }
                if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // A variant of Bee_Boss that takes TheBee 1's place: its pumpkin and skull mask (the boss's own crown is
        // switched off), its own look, its toughness, and whack_7 when it dies.
        private static bool MakePumpkinButtBoss()
        {
            var oldBoss = AssetDatabase.LoadAssetAtPath<GameObject>(TheBee1Path);
            var boss = AssetDatabase.LoadAssetAtPath<GameObject>(string.Format(VariantPath, SpiderBrain.SizeClass.Boss));
            if (oldBoss == null || boss == null || AssetDatabase.LoadAssetAtPath<GameObject>(PumpkinButtPath) != null) return false;
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(boss, scene);
                foreach (var gear in instance.GetComponentsInChildren<Transform>(true))
                {
                    if (IsGear(gear)) gear.gameObject.SetActive(false);
                }
                CopyGear(oldBoss, instance);
                WearOwnLook(oldBoss, instance);
                var brain = instance.GetComponent<SpiderBrain>();
                var s = new SerializedObject(brain);
                var list = s.FindProperty("setOnDeath");
                list.arraySize = 0;
                SetFlag(list, StagesObject, StagesObject, PumpkinButtBool);
                s.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(brain);
                CopyToughness(oldBoss, instance);
                PrefabUtility.SaveAsPrefabAsset(instance, PumpkinButtPath);
                return true;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        // The old boss's own material on the bee models.
        private static bool WearOwnLook(GameObject oldBoss, GameObject target)
        {
            var oldModel = oldBoss.GetComponentInChildren<SkinnedMeshRenderer>(true);
            return oldModel != null && oldModel.sharedMaterial != null && Wear(target, oldModel.sharedMaterial);
        }

        // Puts the material on its model variants (bee_smol, _2, _3). False if they all wear it already.
        private static bool Wear(GameObject root, Material material)
        {
            bool changed = false;
            foreach (var model in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!model.name.StartsWith("bee_smol")) continue;
                var materials = model.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == material) continue;
                    materials[i] = material;
                    changed = true;
                }
                model.sharedMaterials = materials;
                PrefabUtility.RecordPrefabInstancePropertyModifications(model);
            }
            return changed;
        }

        private static bool IsGear(Transform t) => t.name.StartsWith("pumpkin") || t.name.StartsWith("Skull");

        // Copies the old boss's gear (pumpkins, crown, skull masks) onto the same bones, as it is there (on or off).
        private static int CopyGear(GameObject oldBoss, GameObject target)
        {
            int count = 0;
            foreach (var gear in oldBoss.GetComponentsInChildren<Transform>(true))
            {
                if (!IsGear(gear) || IsGear(gear.parent)) continue;
                var bone = target.transform.Find(AnimationUtility.CalculateTransformPath(gear.parent, oldBoss.transform));
                if (bone == null || bone.Find(gear.name) != null && bone.Find(gear.name).gameObject.activeSelf == gear.gameObject.activeSelf) continue;
                var model = PrefabUtility.GetCorrespondingObjectFromOriginalSource(gear.gameObject);
                var copy = model != null && model != gear.gameObject
                    ? (GameObject)PrefabUtility.InstantiatePrefab(model, bone)
                    : Object.Instantiate(gear.gameObject, bone);
                copy.name = gear.name;
                copy.transform.SetLocalPositionAndRotation(gear.localPosition, gear.localRotation);
                copy.transform.localScale = gear.localScale;
                copy.SetActive(gear.gameObject.activeSelf);
                if (gear.TryGetComponent(out MeshRenderer from) && copy.TryGetComponent(out MeshRenderer to)) to.sharedMaterials = from.sharedMaterials;
                count++;
            }
            return count;
        }

        private static bool CopyToughness(GameObject oldBoss, GameObject target)
        {
            if (!oldBoss.TryGetComponent(out EnemyHitDamage oldHits) || !target.TryGetComponent(out EnemyHitDamage hits)) return false;
            var from = new SerializedObject(oldHits);
            var to = new SerializedObject(hits);
            bool changed = false;
            foreach (var field in new[] { "toughness", "knockBackDistance" })
            {
                var a = from.FindProperty(field);
                var b = to.FindProperty(field);
                if (a == null || b == null || Mathf.Approximately(a.floatValue, b.floatValue)) continue;
                b.floatValue = a.floatValue;
                changed = true;
            }
            to.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(hits);
            return changed;
        }

        private static bool HasFlag(SerializedProperty list)
        {
            for (int i = 0; i < list.arraySize; i++)
            {
                if (!string.IsNullOrEmpty(list.GetArrayElementAtIndex(i).FindPropertyRelative("boolName").stringValue)) return true;
            }
            return false;
        }

        private static void SetFlag(SerializedProperty list, string globalObject, string fsm, string boolName)
        {
            list.arraySize++;
            var flag = list.GetArrayElementAtIndex(list.arraySize - 1);
            flag.FindPropertyRelative("globalObject").stringValue = globalObject;
            flag.FindPropertyRelative("fsm").stringValue = fsm;
            flag.FindPropertyRelative("boolName").stringValue = boolName;
            flag.FindPropertyRelative("value").boolValue = true;
        }

        // Switches off Enable FSM and Send Event actions that target an FSM of this name, so removing it doesn't make
        // them log "Missing FsmComponent!".
        private static int SwitchOffActionsAimedAt(GameObject root, string fsmName)
        {
            int count = 0;
            foreach (var fsm in root.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName == fsmName) continue;
                if (!fsm.Fsm.Initialized) fsm.Fsm.Init(fsm);
                bool changed = false;
                foreach (var state in fsm.Fsm.States)
                {
                    state.LoadActions();
                    bool stateChanged = false;
                    foreach (var action in state.Actions)
                    {
                        bool aimed = action switch
                        {
                            EnableFSM enable => enable.fsmName != null && enable.fsmName.Value == fsmName,
                            SendEvent send => send.eventTarget != null && send.eventTarget.fsmName != null && send.eventTarget.fsmName.Value == fsmName,
                            _ => false,
                        };
                        if (!aimed || !action.Enabled) continue;
                        action.Enabled = false;
                        stateChanged = true;
                        count++;
                    }
                    if (!stateChanged) continue;
                    state.SaveActions();
                    changed = true;
                }
                if (changed) EditorUtility.SetDirty(fsm);
            }
            return count;
        }

        private static Transform FindDeep(Transform root, string childName)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == childName) return t;
            }
            return null;
        }

        private static PlayMakerFSM FindFsm(GameObject root, string fsmName)
        {
            foreach (var fsm in root.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName == fsmName) return fsm;
            }
            return null;
        }

        private static List<AudioClip> Clips(PlayMakerFSM fsm, string arrayName)
        {
            var clips = new List<AudioClip>();
            var array = fsm != null ? fsm.FsmVariables.GetFsmArray(arrayName) : null;
            if (array == null) return clips;
            foreach (var value in array.Values)
            {
                if (value is AudioClip clip && clip != null) clips.Add(clip);
            }
            return clips;
        }

        private static void SetClips(SerializedProperty property, List<AudioClip> clips)
        {
            property.arraySize = clips.Count;
            for (int i = 0; i < clips.Count; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
        }
    }
}

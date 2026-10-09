using System.Collections.Generic;
using Game.Combat;
using Game.Enemies;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Game.EditorTools
{
    /// <summary>
    /// Makes the smarter cockroach the same way as the smarter spider (Tools > 429 Game > Enemies):
    /// Cockroach_Smarter (a copy of Cockroach with Spider Brain instead of its old "Cockroach" FSM), its size variants
    /// Cockroach_Small / _Normal / _Boss, and Cockroach_Boss_PumpkinButt (a variant of the boss).
    /// Cockroach_Boss takes TheCockroach's place (its skull mask, Bulldozer, toughness; BigRoachDown in EnemyManager 2),
    /// Cockroach_Boss_PumpkinButt takes TheCockroach 1's (its pumpkin and skull; BigRoach1Down in EnemyManager 2).
    /// Run it again any time: whatever is already set up keeps its settings.
    /// </summary>
    public static class SmarterCockroachSetupMenu
    {
        private const string Folder = "Assets/Prefabs/Enemies/Enemies_Smarter/";
        private const string OldRoachPath = "Assets/Prefabs/Enemies/Cockroach.prefab";
        private const string RoachPath = Folder + "Cockroach_Smarter.prefab";
        private const string VariantPath = Folder + "Cockroach_{0}.prefab";
        private const string PumpkinButtPath = Folder + "Cockroach_Boss_PumpkinButt.prefab";
        private const string TheRoachPath = "Assets/Prefabs/Enemies/TheCockroach.prefab";
        private const string TheRoach1Path = "Assets/Prefabs/Enemies/TheCockroach 1.prefab";
        private const string OldBrainFsm = "Cockroach";
        private const string AttackSound = "Assets/Audio/SFX/bug_Attack.wav";

        [MenuItem("Tools/429 Game/Enemies/Set Up Smarter Cockroach")]
        public static void SetUp()
        {
            var notes = new List<string>();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(RoachPath) == null)
            {
                if (!AssetDatabase.CopyAsset(OldRoachPath, RoachPath))
                {
                    Debug.LogError($"[Smarter Cockroach] Couldn't copy {OldRoachPath}. Nothing changed.");
                    return;
                }
                notes.Add("made Cockroach_Smarter (a copy of Cockroach)");
            }

            var root = PrefabUtility.LoadPrefabContents(RoachPath);
            try
            {
                var oldBrain = FindFsm(root, OldBrainFsm);
                var damageFsm = FindFsm(root, "Damage");
                if (!root.TryGetComponent(out SpiderBrain brain))
                {
                    brain = root.AddComponent<SpiderBrain>();
                    var models = root.transform.Find("Cockroach_Models");
                    var s = new SerializedObject(brain);
                    s.FindProperty("models").objectReferenceValue = models;
                    s.FindProperty("animator").objectReferenceValue = models != null ? models.GetComponent<Animator>() : root.GetComponentInChildren<Animator>(true);
                    var smoke = FindDeep(root.transform, "Black_Smoke_Blast");
                    s.FindProperty("hitEffect").objectReferenceValue = smoke != null ? smoke.gameObject : null;
                    SetClips(s.FindProperty("hitSounds"), Clips(damageFsm, "Get_Hit_Sound"));
                    SetClips(s.FindProperty("noticeSounds"), Clips(oldBrain, "Alert_Sound"));

                    // The cockroach's Animator states (Cockroach_Animator): no walk yet, so it wanders on the quick run.
                    s.FindProperty("idleAnimation").stringValue = "idle";
                    s.FindProperty("walkAnimation").stringValue = "run2";
                    s.FindProperty("runAnimation").stringValue = "run";
                    s.FindProperty("alertAnimation").stringValue = "alert";
                    s.FindProperty("hitAnimation").stringValue = "hit";
                    s.FindProperty("walkBackAnimation").stringValue = "walk_back";
                    s.FindProperty("runSpeed").floatValue = 3.2f;          // cockroaches are quick
                    s.FindProperty("walkSpeed").floatValue = 0.8f;
                    s.FindProperty("runAnimationSpeed").floatValue = 2.5f;  // until its run is re-done

                    // Sizes: the model is ~0.5 m long at scale 1; the old bosses were scale 2.
                    Size(s, "small", new Vector2(0.55f, 0.75f), new Vector2Int(3, 4), 1.1f, 1f, "", 0.5f);
                    Size(s, "normal", new Vector2(0.85f, 1.1f), new Vector2Int(6, 8), 1.15f, 1f, "", 0.35f);
                    Size(s, "boss", new Vector2(1.9f, 2.2f), new Vector2Int(20, 24), 0.95f, 1.3f, "DamageHeavy", 0.15f);

                    // One bite on its current attack animation (1.9 s); the timing gets matched when the attack is re-done.
                    var attacks = s.FindProperty("attacks");
                    attacks.arraySize = 1;
                    var bite = attacks.GetArrayElementAtIndex(0);
                    bite.FindPropertyRelative("name").stringValue = "Bite";
                    bite.FindPropertyRelative("usedBy").intValue = 7;
                    bite.FindPropertyRelative("animation").stringValue = "attack";
                    bite.FindPropertyRelative("weight").floatValue = 1f;
                    bite.FindPropertyRelative("minDistance").floatValue = 0f;
                    bite.FindPropertyRelative("maxDistance").floatValue = 1.8f;
                    bite.FindPropertyRelative("lunge").floatValue = 1f;
                    bite.FindPropertyRelative("hitFrom").floatValue = 0.35f;
                    bite.FindPropertyRelative("hitUntil").floatValue = 0.7f;
                    bite.FindPropertyRelative("duration").floatValue = 1.2f;
                    bite.FindPropertyRelative("reach").floatValue = 0.3f;
                    bite.FindPropertyRelative("biteRadius").floatValue = 0.25f;
                    bite.FindPropertyRelative("playerEvent").stringValue = "Damage";
                    bite.FindPropertyRelative("sound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(AttackSound);
                    s.ApplyModifiedPropertiesWithoutUndo();
                    notes.Add("added the brain (Spider Brain) with the cockroach's states, sounds, sizes and one Bite");
                }

                if (oldBrain != null)
                {
                    int unhooked = SwitchOffActionsAimedAt(root, OldBrainFsm);
                    Object.DestroyImmediate(oldBrain, true);
                    notes.Add($"removed the old \"Cockroach\" FSM and switched off {unhooked} action(s) that pointed at it");
                }
                var bubble = root.transform.Find("ProximityBubble");
                if (bubble != null && bubble.gameObject.activeSelf)
                {
                    bubble.gameObject.SetActive(false);
                    notes.Add("switched off ProximityBubble (the old FSM's sensing sphere)");
                }
                if (root.TryGetComponent(out NavMeshAgent agent) && agent.acceleration < 10f)
                {
                    agent.acceleration = 24f;
                    agent.angularSpeed = 900f;
                    agent.autoBraking = true;
                    agent.stoppingDistance = 0.1f;
                    notes.Add("NavMesh Agent: quick acceleration and turning (the brain sets the speed)");
                }
                PrefabUtility.SaveAsPrefabAsset(root, RoachPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            foreach (var sizeClass in new[] { SpiderBrain.SizeClass.Small, SpiderBrain.SizeClass.Normal, SpiderBrain.SizeClass.Boss })
            {
                if (MakeSizeVariant(sizeClass)) notes.Add($"made Cockroach_{sizeClass}");
            }
            SetUpBoss(notes);
            if (MakePumpkinButtBoss()) notes.Add("made Cockroach_Boss_PumpkinButt (TheCockroach 1's pumpkin and skull; BigRoach1Down)");

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(RoachPath);
            Debug.Log("[Smarter Cockroach] " + (notes.Count > 0 ? string.Join("; ", notes) : "already set up, nothing to change") + ".");
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
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RoachPath), scene);
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

        // Cockroach_Boss: TheCockroach's skull mask, Bulldozer and toughness; BigRoachDown in EnemyManager 2.
        private static void SetUpBoss(List<string> notes)
        {
            string path = string.Format(VariantPath, SpiderBrain.SizeClass.Boss);
            var oldBoss = AssetDatabase.LoadAssetAtPath<GameObject>(TheRoachPath);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null || oldBoss == null) return;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool changed = false;
                if (root.GetComponent<Bulldozer>() == null && oldBoss.TryGetComponent(out Bulldozer original))
                {
                    EditorUtility.CopySerialized(original, root.AddComponent<Bulldozer>());
                    notes.Add("Cockroach_Boss got TheCockroach's Bulldozer");
                    changed = true;
                }
                var brain = root.GetComponent<SpiderBrain>();
                var flags = new SerializedObject(brain);
                var list = flags.FindProperty("setOnDeath");
                if (!HasFlag(list))
                {
                    list.arraySize = 0;
                    SetFlag(list, "EnemyManager2", "BigRoachDown");
                    flags.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(brain);
                    notes.Add("Cockroach_Boss sets BigRoachDown in EnemyManager 2 when it dies");
                    changed = true;
                }
                if (CopyGear(oldBoss, root) > 0)
                {
                    notes.Add("Cockroach_Boss wears TheCockroach's skull mask");
                    changed = true;
                }
                if (CopyToughness(oldBoss, root)) changed = true;
                if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // A variant of Cockroach_Boss that takes TheCockroach 1's place: its pumpkin and skull (the boss's own skull mask
        // is switched off), its toughness, and BigRoach1Down in EnemyManager 2.
        private static bool MakePumpkinButtBoss()
        {
            var oldBoss = AssetDatabase.LoadAssetAtPath<GameObject>(TheRoach1Path);
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
                var brain = instance.GetComponent<SpiderBrain>();
                var s = new SerializedObject(brain);
                var list = s.FindProperty("setOnDeath");
                list.arraySize = 0;
                SetFlag(list, "EnemyManager2", "BigRoach1Down");
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

        private static bool IsGear(Transform t) => t.name.StartsWith("pumpkin") || t.name.StartsWith("Skull");

        // Copies the old boss's gear (pumpkins, skull masks) onto the same bones, as it is there (on or off).
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

        private static void SetFlag(SerializedProperty list, string globalObject, string boolName)
        {
            list.arraySize++;
            var flag = list.GetArrayElementAtIndex(list.arraySize - 1);
            flag.FindPropertyRelative("globalObject").stringValue = globalObject;
            flag.FindPropertyRelative("fsm").stringValue = "EnemyManager";
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

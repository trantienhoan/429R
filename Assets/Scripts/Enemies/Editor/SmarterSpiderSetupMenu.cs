using System.Collections.Generic;
using Game.Enemies;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Game.EditorTools
{
    /// <summary>Menu command that gives the smarter spider its brain (Tools > 429 Game > Enemies).</summary>
    public static class SmarterSpiderSetupMenu
    {
        private const string SpiderPath = "Assets/Prefabs/Enemies/Enemies_Smarter/Spider_Smarter.prefab";
        private const string OldBrainFsm = "Spider";
        private const string AttackSound = "Assets/Audio/SFX/bug_Attack.wav";
        private const string VariantPath = "Assets/Prefabs/Enemies/Enemies_Smarter/Spider_{0}.prefab";
        private const string PumpkinButtPath = "Assets/Prefabs/Enemies/Enemies_Smarter/Spider_Boss_PumpkinButt.prefab";
        // The old bosses these replace: TheSpider 1 (EnemyManager 0) and TheSpider (EnemyManager 1, pumpkin gear).
        private const string TheSpiderPath = "Assets/Prefabs/Enemies/TheSpider.prefab";
        // The white spider's look: the spider's paint turned white (black skull, orange glow kept).
        private const string WhiteMaterialPath = "Assets/Materials/Spider_Smol_White_Painted_Mat.mat";
        // The web shot: a ball made from the web ball model, a puff where it splats, and its sounds.
        private const string WebBallPath = "Assets/Prefabs/Enemies/Enemies_Smarter/Spider_Web_Ball.prefab";
        private const string WebBallModel = "Assets/Prefabs/Items/Breakable/Web_Ball_On_Web.prefab";
        private const string SplatEffect = "Assets/Lana Studio/Hyper Casual FX/Prefabs/Confetti/Smoke_Blast.prefab";
        private const string SpitSound = "Assets/Audio/SFX/magic_hat_spit_card.wav";
        private const string WebHitSound = "Assets/Audio/SFX/Bug_Squish_Bedroom.wav";
        private const string WebSplatSound = "Assets/Audio/SFX/bubble_pop_1.wav";

        /// <summary>
        /// Adds Spider Brain to Spider_Smarter (with the old FSMs' sounds, its animator, models and smoke), removes its
        /// old "Spider" FSM (and switches off the two actions in its other FSMs that pointed at it), switches off the huge
        /// trigger sphere that FSM used to spot the player, and makes its NavMesh Agent quick enough to skitter. Then makes
        /// Spider_Small, Spider_Normal and Spider_Boss next to it: variants that are always that size (Spider_Smarter
        /// itself picks one at random). Spider_Boss takes TheSpider 1's place: it gets a Bulldozer like the old bosses, the
        /// white spider's material (Spider_Smol_White_Painted_Mat) and sets WhiteSpiderDown in EnemyManager 0 when it dies. Spider_Boss_PumpkinButt, a variant of it, takes TheSpider's
        /// place: TheSpider's pumpkin, skull and crown, its toughness, and BigSpiderDown in EnemyManager 1.
        /// Also makes Spider_Web_Ball (the web shot: hurts and slows the player) and gives it to the spiders.
        /// Run it again any time: whatever is already set up keeps its settings.
        /// </summary>
        [MenuItem("Tools/429 Game/Enemies/Set Up Smarter Spider")]
        public static void SetUp()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(SpiderPath) == null)
            {
                Debug.LogError($"[Smarter Spider] No {SpiderPath}. Nothing changed.");
                return;
            }

            var notes = new List<string>();
            var root = PrefabUtility.LoadPrefabContents(SpiderPath);
            try
            {
                var oldBrain = FindFsm(root, OldBrainFsm);
                var damageFsm = FindFsm(root, "Damage");

                if (!root.TryGetComponent(out SpiderBrain brain))
                {
                    brain = root.AddComponent<SpiderBrain>();
                    var models = root.transform.Find("Spider_Models");
                    var serialized = new SerializedObject(brain);
                    serialized.FindProperty("models").objectReferenceValue = models;
                    serialized.FindProperty("animator").objectReferenceValue = models != null ? models.GetComponent<Animator>() : root.GetComponentInChildren<Animator>(true);
                    var smoke = FindDeep(root.transform, "Black_Smoke_Blast");
                    serialized.FindProperty("hitEffect").objectReferenceValue = smoke != null ? smoke.gameObject : null;
                    SetClips(serialized.FindProperty("hitSounds"), Clips(oldBrain, "Hit_Sound"));
                    SetClips(serialized.FindProperty("noticeSounds"), Clips(damageFsm, "Alert_Sound"));
                    var attacks = serialized.FindProperty("attacks");
                    if (attacks.arraySize > 0)
                        attacks.GetArrayElementAtIndex(0).FindPropertyRelative("sound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(AttackSound);
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    notes.Add("added Spider Brain (one attack, Bite, on the 'attack' animation; add more under Attacks)");
                }

                var web = MakeWebBall();
                var webSettings = new SerializedObject(brain);
                if (web != null && webSettings.FindProperty("webBall").objectReferenceValue == null)
                {
                    webSettings.FindProperty("webBall").objectReferenceValue = web;
                    webSettings.FindProperty("webSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(SpitSound);
                    webSettings.ApplyModifiedPropertiesWithoutUndo();
                    notes.Add("spiders shoot Spider_Web_Ball while they wait their turn (Normal and Boss; it hurts and slows the player)");
                }

                if (oldBrain != null)
                {
                    // Its sounds are copied to the brain above; nothing else needs it once these two point elsewhere.
                    int unhooked = SwitchOffActionsAimedAt(root, OldBrainFsm);
                    Object.DestroyImmediate(oldBrain, true);
                    notes.Add($"removed the old \"Spider\" FSM and switched off {unhooked} action(s) in the other FSMs that pointed at it");
                }

                // A trigger that grew to about 36 m across so the old FSM could spot the player through walls.
                var bubble = root.transform.Find("ProximityBubble");
                if (bubble != null && bubble.gameObject.activeSelf)
                {
                    bubble.gameObject.SetActive(false);
                    notes.Add("switched off ProximityBubble (the old FSM's 36 m sensing sphere; ProximityBubble 2 still feels the fairy's shield)");
                }

                if (root.TryGetComponent(out NavMeshAgent agent) && agent.acceleration < 10f)
                {
                    agent.acceleration = 20f;
                    agent.angularSpeed = 720f;
                    agent.autoBraking = true;
                    agent.stoppingDistance = 0.1f;
                    notes.Add("NavMesh Agent: acceleration 1 -> 20, turning 720, braking on, stopping distance 0.1 (the brain sets the speed)");
                }

                PrefabUtility.SaveAsPrefabAsset(root, SpiderPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            foreach (var sizeClass in new[] { SpiderBrain.SizeClass.Small, SpiderBrain.SizeClass.Normal, SpiderBrain.SizeClass.Boss })
            {
                if (MakeSizeVariant(sizeClass)) notes.Add($"made Spider_{sizeClass}");
            }

            SetUpBoss(notes);
            if (MakePumpkinButtBoss()) notes.Add("made Spider_Boss_PumpkinButt (TheSpider's pumpkin, skull and crown; BigSpiderDown in EnemyManager 1)");

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(SpiderPath);
            Debug.Log("[Smarter Spider] " + (notes.Count > 0 ? string.Join("; ", notes) : "already set up, nothing to change") +
                      ". Its senses, speeds, attacks and turn-taking are on Spider Brain.");
        }

        // The web shot's ball: the web ball model with a Rigidbody (for gravity) and Web Ball, no collider.
        private static GameObject MakeWebBall()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(WebBallPath);
            if (existing != null) return existing;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(WebBallModel);
            if (model == null || !model.TryGetComponent(out MeshFilter sourceMesh) || !model.TryGetComponent(out MeshRenderer sourceLook)) return null;

            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var ball = new GameObject("Spider_Web_Ball");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(ball, scene);
                ball.transform.localScale = Vector3.one * 1.3f;
                ball.AddComponent<MeshFilter>().sharedMesh = sourceMesh.sharedMesh;
                ball.AddComponent<MeshRenderer>().sharedMaterials = sourceLook.sharedMaterials;
                var body = ball.AddComponent<Rigidbody>();
                body.mass = 0.3f;
                body.angularVelocity = Vector3.zero;
                var webBall = new SerializedObject(ball.AddComponent<WebBall>());
                webBall.FindProperty("splatEffect").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(SplatEffect);
                webBall.FindProperty("hitSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(WebHitSound);
                webBall.FindProperty("splatSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(WebSplatSound);
                webBall.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(ball, WebBallPath);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        // Spider_Boss replaces TheSpider 1: a Bulldozer like the old bosses, and WhiteSpiderDown in EnemyManager 0.
        private static void SetUpBoss(List<string> notes)
        {
            string path = string.Format(VariantPath, SpiderBrain.SizeClass.Boss);
            var oldBoss = AssetDatabase.LoadAssetAtPath<GameObject>(TheSpiderPath);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return;

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool changed = false;
                if (root.GetComponent<Bulldozer>() == null)
                {
                    var bulldozer = root.AddComponent<Bulldozer>();
                    if (oldBoss != null && oldBoss.TryGetComponent(out Bulldozer original)) EditorUtility.CopySerialized(original, bulldozer);
                    notes.Add("Spider_Boss got a Bulldozer like the old bosses (it shoves loose things out of its way)");
                    changed = true;
                }

                var brain = root.GetComponent<SpiderBrain>();
                var flags = new SerializedObject(brain);
                var list = flags.FindProperty("setOnDeath");
                if (!HasFlag(list))
                {
                    list.arraySize = 0;
                    SetFlag(list, "EnemyManager0", "WhiteSpiderDown");
                    flags.ApplyModifiedPropertiesWithoutUndo();
                    // In a variant, make sure the new entry is saved as an override.
                    PrefabUtility.RecordPrefabInstancePropertyModifications(brain);
                    notes.Add("Spider_Boss sets WhiteSpiderDown in EnemyManager 0 when it dies (it takes TheSpider 1's place)");
                    changed = true;
                }
                var white = AssetDatabase.LoadAssetAtPath<Material>(WhiteMaterialPath);
                if (white != null && Wear(root, white))
                {
                    notes.Add("Spider_Boss wears the white spider's material");
                    changed = true;
                }
                if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // Puts the material on its model variants (Spider_Smol, _2, _3). False if they all wear it already.
        private static bool Wear(GameObject root, Material material)
        {
            bool changed = false;
            foreach (var model in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!model.name.StartsWith("Spider_Smol")) continue;
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

        // A variant of Spider_Boss that takes TheSpider's place: its gear on the same bones, its toughness, and
        // BigSpiderDown in EnemyManager 1.
        private static bool MakePumpkinButtBoss()
        {
            var oldBoss = AssetDatabase.LoadAssetAtPath<GameObject>(TheSpiderPath);
            var boss = AssetDatabase.LoadAssetAtPath<GameObject>(string.Format(VariantPath, SpiderBrain.SizeClass.Boss));
            if (oldBoss == null || boss == null || AssetDatabase.LoadAssetAtPath<GameObject>(PumpkinButtPath) != null) return false;

            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(boss, scene);
                foreach (var gear in oldBoss.GetComponentsInChildren<Transform>(true))
                {
                    if (!gear.name.StartsWith("pumpkin") && gear.name != "Skull") continue;
                    var bone = instance.transform.Find(AnimationUtility.CalculateTransformPath(gear.parent, oldBoss.transform));
                    var model = PrefabUtility.GetCorrespondingObjectFromOriginalSource(gear.gameObject);
                    if (bone == null || model == null) continue;

                    var copy = (GameObject)PrefabUtility.InstantiatePrefab(model, bone);
                    copy.name = gear.name;
                    copy.transform.SetLocalPositionAndRotation(gear.localPosition, gear.localRotation);
                    copy.transform.localScale = gear.localScale;
                    copy.SetActive(gear.gameObject.activeSelf);
                    if (gear.TryGetComponent(out MeshRenderer from) && copy.TryGetComponent(out MeshRenderer to)) to.sharedMaterials = from.sharedMaterials;
                }

                // TheSpider's own look (it wasn't white).
                var oldModel = oldBoss.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (oldModel != null && oldModel.sharedMaterial != null) Wear(instance, oldModel.sharedMaterial);

                var spiderBrain = instance.GetComponent<SpiderBrain>();
                var brain = new SerializedObject(spiderBrain);
                var list = brain.FindProperty("setOnDeath");
                list.arraySize = 0;
                SetFlag(list, "EnemyManager", "BigSpiderDown");
                brain.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(spiderBrain);

                if (oldBoss.TryGetComponent(out Game.Combat.EnemyHitDamage oldHits) && instance.TryGetComponent(out Game.Combat.EnemyHitDamage hits))
                {
                    var toughness = new SerializedObject(hits);
                    toughness.FindProperty("toughness").floatValue = new SerializedObject(oldHits).FindProperty("toughness").floatValue;
                    toughness.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(instance, PumpkinButtPath);
                return true;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        // An entry with a bool name counts; empty ones (e.g. from pressing + in the Inspector) don't.
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

        // A Prefab Variant of Spider_Smarter that's always this size; everything else follows Spider_Smarter.
        private static bool MakeSizeVariant(SpiderBrain.SizeClass sizeClass)
        {
            string path = string.Format(VariantPath, sizeClass);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return false;

            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SpiderPath), scene);
                var serialized = new SerializedObject(instance.GetComponent<SpiderBrain>());
                serialized.FindProperty("size").enumValueIndex = (int)sizeClass;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(instance, path);
                return true;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        // Switches off Enable FSM and Send Event actions that target an FSM of this name, so removing it doesn't make
        // them log "Missing FsmComponent!" (Enable FSM's error pauses PlayMaker in the Editor).
        private static int SwitchOffActionsAimedAt(GameObject root, string fsmName)
        {
            int count = 0;
            foreach (var fsm in root.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName == fsmName) continue;
                bool changed = false;
                foreach (var state in fsm.Fsm.States)
                {
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

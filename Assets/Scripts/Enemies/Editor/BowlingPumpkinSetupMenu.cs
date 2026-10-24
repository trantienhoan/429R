using Game.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Enemies > Set Up Bowling Pumpkin. Makes Bowling_Pumpkin_Enemy (the pumpkin_bowling_enemy ball with
    /// Bowling Pumpkin: it bursts out of a tree pumpkin, winds up, rolls at the player trailing smoke and explodes) and
    /// puts Turns Into Bowler on Pumpkin_On_Tree (with more than 12 tree pumpkins out, now and then one turns into it).
    /// Running it again makes the bowler anew and keeps the numbers already tuned on Pumpkin_On_Tree.
    /// </summary>
    public static class BowlingPumpkinSetupMenu
    {
        private const string BowlerPath = "Assets/Prefabs/Enemies/Bowling_Pumpkin_Enemy.prefab";
        // The ball with the jack-o'-lantern face (the user's model).
        private const string ModelPath = "Assets/Models/Bedroom_Stuffs/pumpkin_bowling_enemy.fbx";
        private const string MaterialPath = "Assets/Materials/Bedroom_Set_1_Mat.mat";
        // The face points 55 degrees round from the model's front: turned this much it looks where the bowler faces
        // (at the player while it winds up).
        private const float FaceYaw = 55f;
        private const string TreePumpkinPath = "Assets/Prefabs/Items/Breakable/Pumpkin_On_Tree.prefab";
        private const string Sfx = "Assets/Audio/SFX/";
        private const string Lana = "Assets/Lana Studio/Hyper Casual FX/Prefabs/";
        // The ball's size: the model is 0.46 m across at scale 1.
        private const float Scale = 1.1f;
        private const float ModelRadius = 0.23f;
        // The NavMesh agent type the bugs (and the game's NavMesh) use.
        private const string BugAgentType = "Spider";

        [MenuItem("Tools/429 Game/Enemies/Set Up Bowling Pumpkin")]
        public static void Run()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && (stage.assetPath == BowlerPath || stage.assetPath == TreePumpkinPath))
            {
                Debug.LogError($"[Bowling Pumpkin] {stage.assetPath} is open in Prefab Mode: close it first. Nothing changed.");
                return;
            }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
            {
                Debug.LogError($"[Bowling Pumpkin] No model at {ModelPath}. Nothing changed.");
                return;
            }
            var bowler = MakeBowler(model);
            AddToTreePumpkin(bowler);
            Debug.Log($"[Bowling Pumpkin] Made {BowlerPath} and put Turns Into Bowler on Pumpkin_On_Tree.");
        }

        private static GameObject MakeBowler(GameObject model)
        {
            var root = new GameObject("Bowling_Pumpkin_Enemy");
            try
            {
                root.tag = "Enemy";
                root.transform.localScale = Vector3.one * Scale;
                var agent = root.AddComponent<NavMeshAgent>();
                // The game's NavMesh is baked for the bugs' agent type.
                agent.agentTypeID = AgentType(BugAgentType);
                agent.radius = ModelRadius * 0.9f;
                agent.height = ModelRadius * 2f;
                agent.baseOffset = 0f;
                agent.speed = 7f;
                agent.angularSpeed = 0f;
                agent.autoBraking = false;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
                var body = root.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                var ball = root.AddComponent<SphereCollider>();
                ball.radius = ModelRadius;
                ball.center = new Vector3(0f, ModelRadius, 0f);
                var speaker = root.AddComponent<AudioSource>();
                speaker.playOnAwake = false;
                speaker.spatialBlend = 1f;
                speaker.rolloffMode = AudioRolloffMode.Linear;
                speaker.minDistance = 1f;
                speaker.maxDistance = 20f;

                // The ball turns round its middle: a pivot there, the model centred on it (its mesh sits 2.7 cm high).
                var pivot = new GameObject("Model").transform;
                pivot.SetParent(root.transform, false);
                pivot.localPosition = new Vector3(0f, ModelRadius, 0f);
                var look = (GameObject)PrefabUtility.InstantiatePrefab(model, pivot);
                look.transform.localPosition = new Vector3(0f, -0.027f, 0f);
                look.transform.localRotation = Quaternion.Euler(0f, FaceYaw, 0f);
                foreach (var c in look.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                // The tree pumpkins' material: the same picture, with their glow and warm tint.
                var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
                if (material != null)
                {
                    foreach (var r in look.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = material;
                }

                var brain = root.AddComponent<BowlingPumpkin>();
                var so = new SerializedObject(brain);
                so.FindProperty("model").objectReferenceValue = pivot;
                so.FindProperty("radius").floatValue = ModelRadius;
                SetList(so.FindProperty("breakOutEffects"), Lana + "Confetti/Hit_Blast.prefab", Lana + "Confetti/Smoke_Blast.prefab");
                so.FindProperty("breakOutSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + "pumpkin_explode_1.wav");
                SetList(so.FindProperty("wakeSounds"), Sfx + "laughing_pumpkin_1.wav", Sfx + "laughing_pumpkin_2.wav", Sfx + "laughing_pumpkin_3.wav");
                so.FindProperty("rollSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + "giant_ball_rolling.wav");
                so.FindProperty("trailPuff").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Boss_FX/Boss_Scrape_Dust.prefab");
                SetList(so.FindProperty("blastEffects"), Lana + "Flash/Flash_round_yellow.prefab", Lana + "Confetti/Hit_Blast.prefab", Lana + "Confetti/Smoke_Blast.prefab");
                so.FindProperty("blastSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + "explosion-312361.wav");
                so.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(root, BowlerPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void AddToTreePumpkin(GameObject bowler)
        {
            var root = PrefabUtility.LoadPrefabContents(TreePumpkinPath);
            try
            {
                var turns = root.GetComponent<TurnsIntoBowler>();
                if (turns == null) turns = root.AddComponent<TurnsIntoBowler>();
                var so = new SerializedObject(turns);
                so.FindProperty("bowler").objectReferenceValue = bowler;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, TreePumpkinPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static int AgentType(string agentName)
        {
            for (int i = 0; i < NavMesh.GetSettingsCount(); i++)
            {
                var settings = NavMesh.GetSettingsByIndex(i);
                if (NavMesh.GetSettingsNameFromID(settings.agentTypeID) == agentName) return settings.agentTypeID;
            }
            Debug.LogWarning($"[Bowling Pumpkin] No NavMesh agent type called {agentName}: the bowler uses the default one.");
            return 0;
        }

        private static void SetList(SerializedProperty list, params string[] paths)
        {
            list.arraySize = paths.Length;
            for (int i = 0; i < paths.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Object>(paths[i]);
        }
    }
}

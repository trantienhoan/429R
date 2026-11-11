using Game.Combat;
using Game.Enemies;
using Game.VFX;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Enemies > Set Up Fat Pumpkin. Makes Fat_Pumpkin_Enemy, next to Bowling_Pumpkin_Enemy (for now the
    /// bowling ball's model made wider: it wakes up where a tree pumpkin sat among the candies, eats every candy on the
    /// floor and bursts at a good punch or throw), and puts Turns Into Fat Pumpkin on Pumpkin_On_Tree. Running it again
    /// leaves the prefab as it is, with whatever model and numbers it has by then.
    /// </summary>
    public static class FatPumpkinSetupMenu
    {
        private const string FatPath = "Assets/Prefabs/Enemies/Fat_Pumpkin_Enemy.prefab";
        // A stand-in until the user's own model: the ball with the jack-o'-lantern face.
        private const string ModelPath = "Assets/Models/Bedroom_Stuffs/pumpkin_bowling_enemy.fbx";
        private const string MaterialPath = "Assets/Materials/Bedroom_Set_1_Mat.mat";
        private const string TreePumpkinPath = "Assets/Prefabs/Items/Breakable/Pumpkin_On_Tree.prefab";
        private const string PiecesSource = "Assets/Prefabs/Items/Broken_Stuffs/pumpkin_0_drop.prefab";
        private const string PiecesPath = "Assets/Prefabs/Items/Broken_Stuffs/Fat_Pumpkin_Drop.prefab";
        private const string CandySpotName = "random_Candy_Pos";
        private const string CandyPath = "Assets/Prefabs/Items/Breakable/Candy_{0}.prefab";
        private const string Sfx = "Assets/Audio/SFX/";
        private const string Lana = "Assets/Lana Studio/Hyper Casual FX/Prefabs/";
        // The face points 55 degrees round from the model's front, and its mesh sits 2.7 cm above its origin.
        private const float FaceYaw = 55f;
        private const float MeshLift = 0.027f;
        // The model is 0.46 m across at scale 1: this wide, this squat, on a root of this size (about 0.84 m across).
        private const float ModelRadius = 0.23f;
        private const float Wider = 1.3f;
        private const float Squat = 0.95f;
        // 20% smaller than first made (user, 2026-11-03), then just a little bigger than the bowling pumpkin (1.1) when it
        // comes out (user, 2026-11-05: "a little too big right at start").
        private const float Scale = 1.06f;
        private const string BugAgentType = "Spider";

        [MenuItem("Tools/429 Game/Enemies/Set Up Fat Pumpkin")]
        public static void Run()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && (stage.assetPath == FatPath || stage.assetPath == TreePumpkinPath))
            {
                Debug.LogError($"[Fat Pumpkin] {stage.assetPath} is open in Prefab Mode: close it first. Nothing changed.");
                return;
            }
            var fat = AssetDatabase.LoadAssetAtPath<GameObject>(FatPath);
            if (fat == null)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
                if (model == null)
                {
                    Debug.LogError($"[Fat Pumpkin] No model at {ModelPath}. Nothing changed.");
                    return;
                }
                fat = MakeFat(model);
            }
            AddToTreePumpkin(fat);
            Debug.Log($"[Fat Pumpkin] {FatPath} is ready and Pumpkin_On_Tree has Turns Into Fat Pumpkin.");
        }

        private static GameObject MakeFat(GameObject model)
        {
            var root = new GameObject("Fat_Pumpkin_Enemy");
            try
            {
                root.transform.localScale = Vector3.one * Scale;
                float bodyRadius = ModelRadius * Wider;
                var agent = root.AddComponent<NavMeshAgent>();
                // The game's NavMesh is baked for the bugs' agent type.
                agent.agentTypeID = BowlingPumpkinSetupMenu.AgentType(BugAgentType);
                agent.radius = bodyRadius * 0.9f;
                agent.height = ModelRadius * Squat * 2f;
                agent.baseOffset = 0f;
                agent.speed = 1.5f;
                agent.angularSpeed = 0f;
                agent.acceleration = 6f;
                agent.stoppingDistance = 0.1f;
                agent.autoBraking = true;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
                var body = root.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                var ball = root.AddComponent<SphereCollider>();
                ball.radius = bodyRadius;
                ball.center = new Vector3(0f, bodyRadius, 0f);
                var speaker = root.AddComponent<AudioSource>();
                speaker.playOnAwake = false;
                speaker.spatialBlend = 1f;
                speaker.rolloffMode = AudioRolloffMode.Linear;
                speaker.minDistance = 1f;
                speaker.maxDistance = 20f;

                // Root > Model (waddles) > Jiggle (wobbles) > the look: the user's model goes under Jiggle later.
                var pivot = new GameObject("Model").transform;
                pivot.SetParent(root.transform, false);
                var wobbly = new GameObject("Jiggle");
                wobbly.transform.SetParent(pivot, false);
                var jiggle = wobbly.AddComponent<Jiggle>();
                var js = new SerializedObject(jiggle);
                // No Health FSM to watch: Fat Pumpkin wobbles it itself.
                js.FindProperty("jiggleWhenHurt").boolValue = false;
                js.ApplyModifiedPropertiesWithoutUndo();
                var look = (GameObject)PrefabUtility.InstantiatePrefab(model, wobbly.transform);
                look.transform.localPosition = new Vector3(0f, (ModelRadius - MeshLift) * Squat, 0f);
                look.transform.localRotation = Quaternion.Euler(0f, FaceYaw, 0f);
                look.transform.localScale = new Vector3(Wider, Squat, Wider);
                foreach (var c in look.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
                if (material != null)
                {
                    foreach (var r in look.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = material;
                }

                var brain = root.AddComponent<FatPumpkin>();
                var so = new SerializedObject(brain);
                so.FindProperty("model").objectReferenceValue = pivot;
                so.FindProperty("jiggle").objectReferenceValue = jiggle;
                so.FindProperty("radius").floatValue = bodyRadius;
                so.FindProperty("hitSettings").objectReferenceValue = FindHitSettings();
                string[] blast = { Lana + "Confetti/Hit_Blast.prefab", "Assets/Prefabs/Effects/Smoke/Smoke_Flash_Small_Puff.prefab" };
                BowlingPumpkinSetupMenu.SetList(so.FindProperty("breakOutEffects"), blast);
                BowlingPumpkinSetupMenu.SetList(so.FindProperty("burstEffects"), blast);
                var pop = AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + "pumpkin_explode_1.wav");
                so.FindProperty("breakOutSound").objectReferenceValue = pop;
                so.FindProperty("burstSound").objectReferenceValue = pop;
                // No laughing (user, 2026-11-04: "remove laughing sound of Fat Pumpkin"): its angry growl was its laugh too.
                so.FindProperty("laughs").arraySize = 0;
                // The user's own clips, standing in until it has its own.
                so.FindProperty("eatSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + "om_nom_nom.wav");
                BowlingPumpkinSetupMenu.SetList(so.FindProperty("hitSounds"), Sfx + "bone_bonk_1.wav", Sfx + "bone_bonk_2.wav");
                so.FindProperty("pieces").objectReferenceValue = MakePieces();
                var candies = System.Array.ConvertAll(Game.Inventory.CandyKinds.Numbers, n => string.Format(CandyPath, n));
                BowlingPumpkinSetupMenu.SetList(so.FindProperty("candies"), candies);
                so.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(root, FatPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // The start pumpkin's pieces without their two random candies: Fat Pumpkin gives back only its own few.
        private static GameObject MakePieces()
        {
            var made = AssetDatabase.LoadAssetAtPath<GameObject>(PiecesPath);
            if (made != null) return made;
            var root = PrefabUtility.LoadPrefabContents(PiecesSource);
            try
            {
                root.name = "Fat_Pumpkin_Drop";
                foreach (var part in root.GetComponentsInChildren<Transform>(true))
                {
                    if (part.name.StartsWith(CandySpotName)) Object.DestroyImmediate(part.gameObject);
                }
                return PrefabUtility.SaveAsPrefabAsset(root, PiecesPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static HitSettings FindHitSettings()
        {
            var found = AssetDatabase.FindAssets("t:HitSettings");
            return found.Length > 0 ? AssetDatabase.LoadAssetAtPath<HitSettings>(AssetDatabase.GUIDToAssetPath(found[0])) : null;
        }

        private static void AddToTreePumpkin(GameObject fat)
        {
            var root = PrefabUtility.LoadPrefabContents(TreePumpkinPath);
            try
            {
                var turns = root.GetComponent<TurnsIntoFatPumpkin>();
                if (turns == null) turns = root.AddComponent<TurnsIntoFatPumpkin>();
                var so = new SerializedObject(turns);
                so.FindProperty("fatPumpkin").objectReferenceValue = fat;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, TreePumpkinPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}

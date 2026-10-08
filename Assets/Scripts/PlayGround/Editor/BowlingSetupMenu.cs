using System.Collections.Generic;
using Game.PlayGround;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.EditorTools
{
    /// <summary>Menu command that sets up the bowling ball and pins (Tools > 429 Game > PlayGround).</summary>
    public static class BowlingSetupMenu
    {
        private const string BallPath = "Assets/Prefabs/PlayGround/pumpkin_bowling.prefab";
        private const string BeehivePath = "Assets/Prefabs/Items/Breakable/beehive.prefab";
        private const string PinPath = "Assets/Prefabs/PlayGround/beehive_pin.prefab";
        private const string PinMaterialPath = "Assets/Prefabs/PlayGround/beehive_pin.physicMaterial";

        // Tried out with pumpkin_bowling in a physics test. The hive is stretched to twice its height: at its own
        // shape it's too squat to tip, so the pins slid into a heap that stopped the ball (2 or 3 down a bowl). Tall,
        // a falling pin reaches the next one, like real pins: a pocket hit knocks 8 to 10 down. A ball weighs about
        // four pins, like real bowling.
        private static readonly Vector3 PinScale = new(1f, 2f, 1f);
        private const float PinMass = 1.5f;
        // The pin stands on a small hidden foot under its tip, like a real pin, so it tips over once leant about 10
        // degrees but stays up on its own.
        private const float FootWidth = 0.06f;
        private const float FootHeight = 0.04f;
        // A little bounce makes the pins scatter into each other.
        private const float PinBounce = 0.35f;

        /// <summary>
        /// Gives pumpkin_bowling the Bowling Roll Assist, and makes beehive_pin (next to it) from the beehive model: a
        /// pin with only physics on it, no breaking or bees, its pivot on the floor in the middle of its foot. Run it
        /// again any time: what's already there keeps its settings.
        /// </summary>
        [MenuItem("Tools/429 Game/PlayGround/Set Up Bowling")]
        public static void SetUpBowling()
        {
            var notes = new List<string>();

            var ball = PrefabUtility.LoadPrefabContents(BallPath);
            try
            {
                if (!ball.TryGetComponent(out BowlingRollAssist _))
                {
                    ball.AddComponent<BowlingRollAssist>();
                    PrefabUtility.SaveAsPrefabAsset(ball, BallPath);
                    notes.Add("pumpkin_bowling rolls when it's let go of near the floor (Bowling Roll Assist)");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(ball);
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PinPath) != null) notes.Add("beehive_pin is already there, kept as it is");
            else if (MakePin()) notes.Add($"made {PinPath}");
            else notes.Add($"no {BeehivePath} to make the pin from");

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PinPath);
            Debug.Log("[Bowling] " + string.Join("; ", notes) + $". Set the pins {PinSpacing:0.00} m apart (centre to centre) " +
                      "in a triangle, and drag the head pin into the ball's Aim At to steer bowls a little toward it.");
        }

        /// <summary>Centre-to-centre distance between pins: the ball doesn't quite fit between two of them.</summary>
        private const float PinSpacing = 0.56f;

        private static bool MakePin()
        {
            var beehive = AssetDatabase.LoadAssetAtPath<GameObject>(BeehivePath);
            if (beehive == null || !beehive.TryGetComponent(out MeshFilter sourceFilter) || !beehive.TryGetComponent(out MeshRenderer sourceRenderer))
                return false;
            var mesh = sourceFilter.sharedMesh;
            var bounds = mesh.bounds;

            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("beehive_pin");
                SceneManager.MoveGameObjectToScene(root, scene);

                var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(PinMaterialPath);
                if (material == null)
                {
                    material = new PhysicsMaterial("beehive_pin")
                    {
                        bounciness = PinBounce,
                        dynamicFriction = 0.4f,
                        staticFriction = 0.5f,
                    };
                    AssetDatabase.CreateAsset(material, PinMaterialPath);
                }

                // The model, its bottom tip on the pivot.
                var model = new GameObject("Model");
                model.transform.SetParent(root.transform, false);
                model.transform.localScale = PinScale;
                model.transform.localPosition = Vector3.Scale(new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z), PinScale);
                model.AddComponent<MeshFilter>().sharedMesh = mesh;
                EditorUtility.CopySerialized(sourceRenderer, model.AddComponent<MeshRenderer>());
                var shell = model.AddComponent<MeshCollider>();
                shell.sharedMesh = mesh;
                shell.convex = true;
                shell.sharedMaterial = material;

                var foot = root.AddComponent<BoxCollider>();
                foot.size = new Vector3(FootWidth, FootHeight, FootWidth);
                foot.center = new Vector3(0f, FootHeight * 0.5f, 0f);
                foot.sharedMaterial = material;

                var body = root.AddComponent<Rigidbody>();
                body.mass = PinMass;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.Continuous;

                PrefabUtility.SaveAsPrefabAsset(root, PinPath);
                return true;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using Game.Enemies;
using Game.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// The things for the bedroom's secret room (user, 2026-11-20), made by Build Bedroom Kit Room:
    /// - Secret_Giant_Pumpkin (Assets/Prefabs/Items/Breakable): the tree pumpkin made giant (Pumpkin Height tall, nearly
    ///   up to the 3 m ceiling), standing still (kinematic), 2 health, only the player's hits count; none of the tree
    ///   pumpkin's own ways (grabbing, cooking, Live Limit, turning into a bowler or a Fat Pumpkin, breaking by itself
    ///   after a while, its random size). Its pieces and what comes out are the Secret Room's.
    /// - Spider_Giant (Assets/Prefabs/Enemies/Enemies_Smarter): a variant of the white boss spider (Spider_Boss), bigger
    ///   (Spider Times Boss), 33 health, no stage flag set when it dies, comes for the player at once, and drops 3 random
    ///   candies when the player kills it (Candies On Death).
    /// And Tools > 429 Game > Items > Random Pumpkin Sizes: Random Pumpkin Size on Pumpkin_On_Tree.
    /// </summary>
    public static class SecretRoomSetupMenu
    {
        public const string GiantPumpkinPath = "Assets/Prefabs/Items/Breakable/Secret_Giant_Pumpkin.prefab";
        public const string GiantSpiderPath = "Assets/Prefabs/Enemies/Enemies_Smarter/Spider_Giant.prefab";
        private const string TreePumpkinPath = "Assets/Prefabs/Items/Breakable/Pumpkin_On_Tree.prefab";
        private const string BossSpiderPath = "Assets/Prefabs/Enemies/Enemies_Smarter/Spider_Boss.prefab";
        public const string PiecesPath = "Assets/Prefabs/Items/Broken_Stuffs/pumpkin_2_Drop.prefab";
        private const float PumpkinHeight = 2.7f;
        private const float SpiderTimesBoss = 1.6f;
        private const int SpiderHealth = 33, SpiderCandies = 3;
        private const float PumpkinHealth = 2f;
        // What makes a tree pumpkin a tree pumpkin: not on the giant one.
        private static readonly string[] NotOnGiant =
            { "XRGrabInteractable", "XRGeneralGrabTransformer", "CookingIngredient", "LiveLimit", "WeaponPower", "TurnsIntoBowler", "TurnsIntoFatPumpkin", "RandomPumpkinSize" };

        [MenuItem("Tools/429 Game/Items/Random Pumpkin Sizes")]
        public static void RandomPumpkinSizes()
        {
            var root = PrefabUtility.LoadPrefabContents(TreePumpkinPath);
            try
            {
                if (root.GetComponent<RandomPumpkinSize>() == null) root.AddComponent<RandomPumpkinSize>();
                PrefabUtility.SaveAsPrefabAsset(root, TreePumpkinPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            Debug.Log("[Random Pumpkin Sizes] Pumpkin_On_Tree: every one a random size (0.75-1.5, the usual one 1), " +
                      "its mass x size^3 and health x size^2 (3 health: 2 to 7). Numbers on its Random Pumpkin Size.");
        }

        [MenuItem("Tools/429 Game/Items/Random Pumpkin Sizes", true)]
        private static bool CanRandomSizes() => !EditorApplication.isPlayingOrWillChangePlaymode;

        /// <summary>The candies in the game (Candy Kinds).</summary>
        public static GameObject[] Candies() =>
            Game.Inventory.CandyKinds.Paths.Select(AssetDatabase.LoadAssetAtPath<GameObject>).Where(c => c != null).ToArray();

        public static GameObject Find(string name, string type = "Prefab")
        {
            foreach (var guid in AssetDatabase.FindAssets(name + " t:" + type))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == name) return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            return null;
        }

        public static AudioClip Sound(string name)
        {
            foreach (var guid in AssetDatabase.FindAssets(name + " t:AudioClip"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == name) return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }
            return null;
        }

        /// <summary>Makes (again) the giant pumpkin and returns it.</summary>
        public static GameObject GiantPumpkin(List<string> notes)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(TreePumpkinPath);
            if (source == null) { notes.Add("no " + TreePumpkinPath + ": no giant pumpkin"); return null; }
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                go.name = "Secret_Giant_Pumpkin";
                for (int pass = 0; pass < 3; pass++)
                    foreach (var c in go.GetComponents<Component>().ToList())
                        if (c != null && NotOnGiant.Contains(c.GetType().Name)) Object.DestroyImmediate(c);

                var mf = go.GetComponent<MeshFilter>();
                float height = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds.size.y : 1f;
                go.transform.localScale = Vector3.one * (PumpkinHeight / Mathf.Max(0.01f, height));

                if (go.TryGetComponent(out Rigidbody body))
                {
                    body.isKinematic = true;
                    body.useGravity = false;
                    body.mass = 5000f;
                }
                foreach (var fsm in go.GetComponentsInChildren<PlayMakerFSM>(true))
                {
                    if (!fsm.Fsm.Initialized) fsm.Fsm.Init(fsm);
                    foreach (var state in fsm.FsmStates)
                    {
                        state.LoadActions();
                        bool edit = false;
                        foreach (var a in state.Actions)
                        {
                            // Its own grow-in (the size is set here), its break by itself after 1-5 minutes, and the small
                            // pieces (the Secret Room drops giant ones).
                            bool off = (fsm.FsmName == "Health" && state.Name == "Random Size" && a is HutongGames.PlayMaker.Actions.TweenScale)
                                       || (fsm.FsmName == "Health" && state.Name == "Compare Health" && a is HutongGames.PlayMaker.Actions.RandomWait)
                                       || (fsm.gameObject.name == "pumpkin_0_drop_pos" && a is HutongGames.PlayMaker.Actions.CreateObject);
                            if (off && a.Enabled) { a.Enabled = false; edit = true; }
                        }
                        if (edit) state.SaveActions();
                    }
                    if (fsm.FsmName == "Health") SetFsmFloat(fsm, "health", PumpkinHealth);
                }
                if (go.TryGetComponent(out Game.Combat.EnemyHitDamage hits))
                {
                    var so = new SerializedObject(hits);
                    so.FindProperty("toughness").floatValue = 1f;
                    var only = so.FindProperty("playerHitsOnly");
                    if (only != null) only.boolValue = true;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                var saved = PrefabUtility.SaveAsPrefabAsset(go, GiantPumpkinPath);
                notes.Add($"giant pumpkin {PumpkinHeight} m tall (x{go.transform.localScale.x:0.00}), {PumpkinHealth} health");
                return saved;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        /// <summary>Makes (again) the Giant Spider and returns it.</summary>
        public static GameObject GiantSpider(List<string> notes)
        {
            var boss = AssetDatabase.LoadAssetAtPath<GameObject>(BossSpiderPath);
            if (boss == null) { notes.Add("no " + BossSpiderPath + ": no Giant Spider"); return null; }
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(boss, scene);
                go.name = "Spider_Giant";
                var brain = go.GetComponent<SpiderBrain>();
                var so = new SerializedObject(brain);
                so.FindProperty("boss.health").vector2IntValue = new Vector2Int(SpiderHealth, SpiderHealth);
                var scale = so.FindProperty("boss.scale");
                var bossScale = scale.vector2Value;
                scale.vector2Value = bossScale * SpiderTimesBoss;
                so.FindProperty("setOnDeath").arraySize = 0;
                so.FindProperty("noticeOnSpawn").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                var drop = go.GetComponent<CandiesOnDeath>();
                if (drop == null) drop = go.AddComponent<CandiesOnDeath>();
                var dso = new SerializedObject(drop);
                dso.FindProperty("count").intValue = SpiderCandies;
                var list = dso.FindProperty("candies");
                var candies = Candies();
                list.arraySize = candies.Length;
                for (int i = 0; i < candies.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = candies[i];
                dso.ApplyModifiedPropertiesWithoutUndo();
                var saved = PrefabUtility.SaveAsPrefabAsset(go, GiantSpiderPath);
                notes.Add($"Giant Spider (Spider_Boss x{SpiderTimesBoss}: scale {bossScale.x:0.00}-{bossScale.y:0.00} -> {bossScale.x * SpiderTimesBoss:0.00}-{bossScale.y * SpiderTimesBoss:0.00}), {SpiderHealth} health, no flag, {SpiderCandies} candies");
                return saved;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        // PlayMaker variables on prefab contents must be written through the serialized FSM (FsmVariables edits don't stick).
        private static void SetFsmFloat(PlayMakerFSM fsm, string name, float value)
        {
            var so = new SerializedObject(fsm);
            var floats = so.FindProperty("fsm.variables.floatVariables");
            for (int i = 0; floats != null && i < floats.arraySize; i++)
            {
                var v = floats.GetArrayElementAtIndex(i);
                if (v.FindPropertyRelative("name").stringValue != name) continue;
                v.FindPropertyRelative("value").floatValue = value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

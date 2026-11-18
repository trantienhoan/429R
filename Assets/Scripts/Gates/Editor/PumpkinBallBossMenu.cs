using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Gates.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Gates > Set Up Pumpkin Ball Boss (user, 2026-11-18: every gate ends with a giant pumpkin ball):
    /// makes Assets/Prefabs/Gates/Pumpkin_Ball_Boss.prefab, a Pumpkin Ball Boss that looks and sounds like the Bowling
    /// Pumpkin (its model grown to the boss's radius, its laughs, roll warning, roll and break-out pieces), lands with the
    /// Fat Pumpkin's stomp and yelps with its hit sounds, with the shared smoke puff, the stun stars and the candies in the
    /// game. Run again: an existing prefab keeps every number tuned on it; only the looks, sounds and candies are refreshed.
    /// Never touches the scene.
    /// </summary>
    public static class PumpkinBallBossMenu
    {
        public const string Folder = "Assets/Prefabs/Gates";
        public const string PrefabPath = Folder + "/Pumpkin_Ball_Boss.prefab";
        private const string BowlerPath = "Assets/Prefabs/Enemies/Bowling_Pumpkin_Enemy.prefab";
        private const string FatPath = "Assets/Prefabs/Enemies/Fat_Pumpkin_Enemy.prefab";
        private const string ModelPath = "Assets/Models/Bedroom_Stuffs/pumpkin_bowling_enemy.fbx";
        private const string StarsPath = "Assets/Prefabs/Enemies/Hit_FX/Stun_Stars.prefab";
        private const string PuffPath = "Assets/Prefabs/Effects/Smoke/Smoke_Flash_Small_Puff.prefab";
        private const string Log = "[Gates]";

        [MenuItem("Tools/429 Game/Gates/Set Up Pumpkin Ball Boss")]
        public static void SetUp() => Run();

        [MenuItem("Tools/429 Game/Gates/Set Up Pumpkin Ball Boss", true)]
        private static bool CanSetUp() => !EditorApplication.isPlaying;

        /// <summary>Makes (or refreshes) the prefab; returns it (null if it couldn't).</summary>
        public static GameObject Run()
        {
            if (EditorApplication.isPlaying) { Debug.LogError($"{Log} Stop play mode first; nothing changed."); return null; }
            var bowler = AssetDatabase.LoadAssetAtPath<GameObject>(BowlerPath);
            var bowling = bowler != null ? bowler.GetComponent<Game.Enemies.BowlingPumpkin>() : null;
            if (bowling == null)
            {
                Debug.LogError($"{Log} No {BowlerPath} with a Bowling Pumpkin (its looks and sounds are the boss's); nothing made.");
                return null;
            }
            var notes = new List<string>();
            var b = new SerializedObject(bowling);

            // The model: the Bowling Pumpkin's (the FBX inside its Model), as it sits there.
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var offset = new Vector3(0f, -0.027f, 0f);
            var euler = new Vector3(0f, 55f, 0f);
            if (b.FindProperty("model").objectReferenceValue is Transform modelRoot && modelRoot.childCount > 0)
            {
                var inner = modelRoot.GetChild(0);
                offset = inner.localPosition;
                euler = inner.localEulerAngles;
                var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(inner.gameObject) as GameObject;
                if (source != null) model = source.transform.root.gameObject;
            }
            if (model == null) notes.Add("NO MODEL (" + ModelPath + "): a plain orange ball stands in");
            float modelRadius = Mathf.Max(0.01f, b.FindProperty("radius").floatValue);

            var fat = AssetDatabase.LoadAssetAtPath<GameObject>(FatPath);
            var fatPumpkin = fat != null ? fat.GetComponent<Game.Enemies.FatPumpkin>() : null;
            var f = fatPumpkin != null ? new SerializedObject(fatPumpkin) : null;
            if (f == null) notes.Add("no Fat Pumpkin: no landing stomp or hit yelps");

            var candies = Game.Inventory.CandyKinds.Paths.Select(AssetDatabase.LoadAssetAtPath<GameObject>).Where(c => c != null).ToArray();
            var stars = AssetDatabase.LoadAssetAtPath<GameObject>(StarsPath);
            var puff = AssetDatabase.LoadAssetAtPath<GameObject>(PuffPath);
            if (stars == null) notes.Add("no " + StarsPath);
            if (puff == null) notes.Add("no " + PuffPath);

            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Prefabs", "Gates");
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
            bool saved;
            if (exists)
            {
                var root = PrefabUtility.LoadPrefabContents(PrefabPath);
                try
                {
                    var boss = root.GetComponent<PumpkinBallBoss>();
                    if (boss == null) boss = root.AddComponent<PumpkinBallBoss>();
                    Fill(boss, model, modelRadius, offset, euler, b, f, candies, stars, puff, false);
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out saved);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                notes.Add("refreshed (its numbers kept)");
            }
            else
            {
                var build = EditorSceneManager.NewPreviewScene();
                try
                {
                    var root = new GameObject("Pumpkin_Ball_Boss");
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, build);
                    var body = root.AddComponent<Rigidbody>();
                    body.isKinematic = true;
                    body.useGravity = false;
                    root.AddComponent<SphereCollider>().enabled = false;
                    var boss = root.AddComponent<PumpkinBallBoss>();
                    Fill(boss, model, modelRadius, offset, euler, b, f, candies, stars, puff, true);
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out saved);
                }
                finally
                {
                    EditorSceneManager.ClosePreviewScene(build);
                }
                notes.Add("made");
            }
            if (!saved)
            {
                Debug.LogError($"{Log} Could not save {PrefabPath}.");
                return null;
            }
            AssetDatabase.SaveAssets();
            var made = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Debug.Log($"{Log} Set Up Pumpkin Ball Boss: {PrefabPath} {string.Join(", ", notes)} ({candies.Length} candies).", made);
            return made;
        }

        // Its looks, sounds, effects and candies (and, new, the radius the model is grown to).
        private static void Fill(PumpkinBallBoss boss, GameObject model, float modelRadius, Vector3 offset, Vector3 euler,
            SerializedObject bowling, SerializedObject fat, GameObject[] candies, GameObject stars, GameObject puff, bool fresh)
        {
            boss.model = model;
            boss.modelRadius = modelRadius;
            boss.modelOffset = offset;
            boss.modelEuler = euler;
            boss.laughSounds = Clips(bowling.FindProperty("wakeSounds"));
            boss.windUpSound = bowling.FindProperty("rollWarning").objectReferenceValue as AudioClip;
            boss.rollSound = bowling.FindProperty("rollSound").objectReferenceValue as AudioClip;
            boss.breakSound = bowling.FindProperty("breakOutSound").objectReferenceValue as AudioClip;
            boss.breakEffects = Objects(bowling.FindProperty("breakOutEffects"));
            if (fat != null)
            {
                boss.landSound = fat.FindProperty("stompSound")?.objectReferenceValue as AudioClip;
                boss.hurtSounds = Clips(fat.FindProperty("hitSounds"));
            }
            boss.puff = puff;
            boss.stunStars = stars;
            boss.candies = candies;
            EditorUtility.SetDirty(boss);
        }

        private static AudioClip[] Clips(SerializedProperty list)
        {
            var clips = new List<AudioClip>();
            if (list == null || !list.isArray) return clips.ToArray();
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue is AudioClip c) clips.Add(c);
            return clips.ToArray();
        }

        private static GameObject[] Objects(SerializedProperty list)
        {
            var objects = new List<GameObject>();
            if (list == null || !list.isArray) return objects.ToArray();
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue is GameObject g) objects.Add(g);
            return objects.ToArray();
        }
    }
}

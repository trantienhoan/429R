using System.Collections.Generic;
using Game.Combat;
using Game.Inventory;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game.PlayGround.Editor
{
    /// <summary>
    /// Tools > 429 Game > PlayGround > Set Up Laughing Pumpkins.
    /// The cauldron's laughing pumpkins (Prefabs/Items/Breakable) get the catch-me game (Laughing Pumpkin):
    /// Pumpkin_8 60 stamina, Pumpkin_9 and Pumpkin_9 1 90, Pumpkin_10 120 (10 used per second of running). They become
    /// grabbable from close range only (Near Grab Only), drop a candy every metre they run, and get a sane mass.
    /// Also makes Pumpkin_Big (a variant of Pumpkin_10, 2.5x size): it never runs, has 100 health that hits take away
    /// (Enemy Hit Damage) and throws out a candy for every 10 it loses. Safe to run again.
    /// </summary>
    public static class LaughingPumpkinSetupMenu
    {
        private const string Folder = "Assets/Prefabs/Items/Breakable/";
        private const string BigPath = Folder + "Pumpkin_Big.prefab";
        private const string Sfx = "Assets/Audio/SFX/";
        private const string Fx = "Assets/Lana Studio/Hyper Casual FX/Prefabs/";
        private const string HitSettingsPath = "Assets/Data/Combat/Hit Settings.asset";
        private static readonly string[] CloseGrabPumpkins = { "Pumpkin_0", "Pumpkin_1", "Pumpkin_2", "Pumpkin_4", "Pumpkin_6" };

        private static readonly (string name, float stamina)[] Runners =
        {
            ("Pumpkin_8", 60f), ("Pumpkin_9", 90f), ("Pumpkin_9 1", 90f), ("Pumpkin_10", 120f),
        };

        [MenuItem("Tools/429 Game/PlayGround/Set Up Laughing Pumpkins")]
        public static void Run()
        {
            var laughs = Clips("laughing_pumpkin_1", "laughing_pumpkin_2", "laughing_pumpkin_3", "laughing_pumpkin_4");
            var hits = Clips("pumpkin_laugh_1", "pumpkin_laugh_2", "pumpkin_laugh_3", "pumpkin_laugh_4", "laughing_pumpkin_5");
            var candies = new List<Object>();
            for (int i = 1; i <= 8; i++)
            {
                var candy = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Candy_" + i + ".prefab");
                if (candy != null) candies.Add(candy);
            }

            var log = new List<string>();
            foreach (var (name, stamina) in Runners)
            {
                string path = Folder + name + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { log.Add(name + " missing"); continue; }
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    SetUpRunner(root, stamina, laughs, hits);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    log.Add($"{name} {stamina}");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            MakeBig(candies, laughs, hits);
            log.Add("Pumpkin_Big (100 health)");
            AssetDatabase.SaveAssets();
            Debug.Log("[Laughing Pumpkins] set up: " + string.Join(", ", log));
        }

        /// <summary>
        /// Tools > 429 Game > Items > Close Grab For Pumpkins: the main game's breakable pumpkins (Pumpkin_0, 1, 2, 4, 6) can
        /// be picked up, but only with a hand right at them (no grabbing from afar with the ray). Safe to run again.
        /// </summary>
        [MenuItem("Tools/429 Game/Items/Close Grab For Pumpkins")]
        public static void CloseGrab()
        {
            var done = new List<string>();
            foreach (var name in CloseGrabPumpkins)
            {
                string path = Folder + name + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { done.Add(name + " missing"); continue; }
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (!root.TryGetComponent<XRGrabInteractable>(out var grab))
                    {
                        grab = root.AddComponent<XRGrabInteractable>();
                        grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
                        grab.throwOnDetach = true;
                        grab.useDynamicAttach = true;
                    }
                    if (!root.TryGetComponent<NearGrabOnly>(out _)) root.AddComponent<NearGrabOnly>();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    done.Add(name);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Close Grab] " + string.Join(", ", done));
        }

        private static void SetUpRunner(GameObject root, float stamina, Object[] laughs, Object[] hits)
        {
            var body = root.GetComponent<Rigidbody>();
            body.mass = 25f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            if (!root.TryGetComponent<XRGrabInteractable>(out var grab)) grab = root.AddComponent<XRGrabInteractable>();
            grab.enabled = true;
            grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            grab.throwOnDetach = true;
            grab.useDynamicAttach = true;
            if (!root.TryGetComponent<NearGrabOnly>(out var near)) near = root.AddComponent<NearGrabOnly>();
            near.enabled = true;

            if (!root.TryGetComponent<LaughingPumpkin>(out var pumpkin)) pumpkin = root.AddComponent<LaughingPumpkin>();
            var so = new SerializedObject(pumpkin);
            so.FindProperty("canRun").boolValue = true;
            so.FindProperty("stamina").floatValue = stamina;
            so.FindProperty("hitSettings").objectReferenceValue = AssetDatabase.LoadAssetAtPath<HitSettings>(HitSettingsPath);
            SetArray(so.FindProperty("handBlastEffects"), new Object[]
            {
                AssetDatabase.LoadAssetAtPath<GameObject>(Fx + "Flash/Flash_round_yellow.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>(Fx + "Confetti/Hit_Blast.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>(Fx + "Confetti/Smoke_Blast.prefab"),
            });
            so.FindProperty("handBlastSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + "pumpkin_explode_1.wav");
            SetArray(so.FindProperty("laughs"), laughs);
            SetArray(so.FindProperty("hitSounds"), hits);
            so.ApplyModifiedPropertiesWithoutUndo();

            if (root.TryGetComponent<CandyTrail>(out var trail))
            {
                var t = new SerializedObject(trail);
                t.FindProperty("spacing").floatValue = 1f;
                t.FindProperty("maxCandies").intValue = 60;
                t.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void MakeBig(List<Object> candies, Object[] laughs, Object[] hits)
        {
            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Pumpkin_10.prefab");
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(BigPath);
            GameObject root;
            var preview = default(UnityEngine.SceneManagement.Scene);
            if (existing != null) root = PrefabUtility.LoadPrefabContents(BigPath);
            else
            {
                // Built in a preview scene so the open scene isn't touched.
                preview = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
                root = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab, preview);
            }
            try
            {
                root.name = "Pumpkin_Big";
                root.transform.localScale = Vector3.one * 3f;

                var body = root.GetComponent<Rigidbody>();
                body.mass = 5000f;
                body.constraints = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ |
                                   RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

                if (root.TryGetComponent<XRGrabInteractable>(out var grab)) grab.enabled = false;
                if (root.TryGetComponent<NearGrabOnly>(out var near)) near.enabled = false;

                if (!root.TryGetComponent<LaughingPumpkin>(out var pumpkin)) pumpkin = root.AddComponent<LaughingPumpkin>();
                var so = new SerializedObject(pumpkin);
                so.FindProperty("canRun").boolValue = false;
                so.FindProperty("candyEveryHealth").floatValue = 5f;
                so.FindProperty("candyLaunchSpeed").floatValue = 3.5f;
                SetArray(so.FindProperty("candies"), candies.ToArray());
                SetArray(so.FindProperty("laughs"), Clips("pumpkin_boss_laugh", "pumpkin_boss_laugh_2", "laughing_pumpkin_1", "laughing_pumpkin_3"));
                SetArray(so.FindProperty("hitSounds"), hits);
                so.ApplyModifiedPropertiesWithoutUndo();

                if (root.TryGetComponent<CandyTrail>(out var trail))
                {
                    var t = new SerializedObject(trail);
                    t.FindProperty("maxCandies").intValue = 0;
                    t.ApplyModifiedPropertiesWithoutUndo();
                }

                if (!root.TryGetComponent<EnemyHitDamage>(out var hitsTaken)) hitsTaken = root.AddComponent<EnemyHitDamage>();
                var h = new SerializedObject(hitsTaken);
                h.FindProperty("toughness").floatValue = 1f;
                h.FindProperty("knockBack").boolValue = false;
                h.FindProperty("exp").intValue = 10;
                h.FindProperty("healthFsm").stringValue = "Health";
                h.ApplyModifiedPropertiesWithoutUndo();

                foreach (var fsm in root.GetComponents<PlayMakerFSM>())
                {
                    if (fsm.FsmName != "Health") continue;
                    var health = fsm.FsmVariables.FindFsmFloat("health");
                    if (health != null) health.Value = 50f; // halved for whole-number damage (2026-10-27)
                    EditorUtility.SetDirty(fsm);
                }

                PrefabUtility.SaveAsPrefabAsset(root, BigPath);
            }
            finally
            {
                if (existing != null) PrefabUtility.UnloadPrefabContents(root);
                else UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static Object[] Clips(params string[] names)
        {
            var list = new List<Object>();
            foreach (var name in names)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + name + ".wav");
                if (clip != null) list.Add(clip);
            }
            return list.ToArray();
        }

        private static void SetArray(SerializedProperty array, Object[] values)
        {
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}

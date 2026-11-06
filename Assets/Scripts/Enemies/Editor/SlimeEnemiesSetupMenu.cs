using System.Collections.Generic;
using System.Linq;
using Game.Bonus;
using Game.Combat;
using Game.Enemies;
using HutongGames.PlayMaker;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Enemies > Set Up Slime Enemies (user 2026-11-05). Builds, as variants of Flying_Slime (so the
    /// model, look and animation follow it):
    /// - Prefabs/Enemies/Slime_Bomber: the small slime the rainbow one poops out; it dive-bombs the player's face (Slime
    ///   Bomber). 1 health: one good punch pops it.
    /// - Prefabs/Enemies/Rainbow_Slime: the rare rainbow slime that takes the white cockroach's place (Rainbow Slime):
    ///   flies about, teases, poops bombers; 2 health; killed = the white cockroach's Candy Helmet reward.
    /// Both get the bee's hit setup (a kinematic Rigidbody, a sphere collider, Enemy Hit Damage with the bee's Hit
    /// Settings and sounds, toughness 1) and a small SlimeHealth FSM holding "health". Then Spider_Smarter,
    /// Cockroach_Smarter and Bee_Smarter (their variants inherit) get Rainbow_Slime as their Anomaly Swap: the rare one
    /// (1 in 100 of any bug) is the rainbow slime. Safe to run again: it remakes the two prefabs (their brains' settings
    /// are kept).
    /// </summary>
    public static class SlimeEnemiesSetupMenu
    {
        private const string SlimePath = "Assets/Prefabs/Enemies/Flying_Slime.prefab";
        private const string BomberPath = "Assets/Prefabs/Enemies/Slime_Bomber.prefab";
        private const string RainbowPath = "Assets/Prefabs/Enemies/Rainbow_Slime.prefab";
        private const string BeePath = "Assets/Prefabs/Enemies/Enemies_Smarter/Bee_Smarter.prefab";
        private const string PaintBeePath = "Assets/Prefabs/Bonus/Paint_Bee.prefab";
        private const string RoundPath = "Assets/Prefabs/Bases/Paint_Bees_Round.prefab";
        private static readonly string[] BugPaths =
        {
            "Assets/Prefabs/Enemies/Enemies_Smarter/Spider_Smarter.prefab",
            "Assets/Prefabs/Enemies/Enemies_Smarter/Cockroach_Smarter.prefab",
            "Assets/Prefabs/Enemies/Enemies_Smarter/Bee_Smarter.prefab",
        };
        private const string HealthFsmName = "SlimeHealth";
        private const float BomberScale = 0.45f;
        private const float RainbowScale = 0.8f;

        [MenuItem("Tools/429 Game/Enemies/Set Up Slime Enemies")]
        public static void SetUp()
        {
            var notes = Build();
            Debug.Log("[Slime Enemies] " + string.Join("\n", notes));
            EditorUtility.DisplayDialog("Set Up Slime Enemies", string.Join("\n", notes), "OK");
        }

        public static List<string> Build()
        {
            var notes = new List<string>();
            var slime = AssetDatabase.LoadAssetAtPath<GameObject>(SlimePath);
            var bee = AssetDatabase.LoadAssetAtPath<GameObject>(BeePath);
            if (slime == null || bee == null || bee.GetComponent<EnemyHitDamage>() == null)
            {
                notes.Add("No Flying_Slime or Bee_Smarter prefab: nothing made.");
                return notes;
            }
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && (stage.assetPath == BomberPath || stage.assetPath == RainbowPath))
            {
                notes.Add("Close " + stage.assetPath + " in Prefab Mode first. Nothing changed.");
                return notes;
            }
            // The white cockroach's reward and lifetime, from the bugs' Spider Brain.
            var roach = AssetDatabase.LoadAssetAtPath<GameObject>(BugPaths[1]);
            var brainSo = roach != null && roach.GetComponent<SpiderBrain>() != null ? new SerializedObject(roach.GetComponent<SpiderBrain>()) : null;
            var round = AssetDatabase.LoadAssetAtPath<GameObject>(RoundPath);
            var splat = round != null && round.GetComponent<PaintBeesRound>() != null
                ? new SerializedObject(round.GetComponent<PaintBeesRound>()).FindProperty("splatMaterial").objectReferenceValue as Material : null;

            var bomberMade = MakeOne(BomberPath, "Slime_Bomber", slime, bee, BomberScale, 1f, 1, false, notes, root =>
            {
                var brain = Keep<SlimeBomber>(root, BomberPath);
                var so = new SerializedObject(brain);
                so.FindProperty("splatMaterial").objectReferenceValue = splat;
                // A short swoosh (user, 2026-11-06: the 3 s falling-bomb whistle was wrong and outlived the slime).
                Clips(so, "diveSounds", "Assets/Audio/SFX/whoosh.wav");
                Clips(so, "faceSounds", "Assets/Audio/SFX/pumpkin_face_splat.wav");
                Clips(so, "popSounds", "Assets/Audio/SFX/Balloon_Pop.wav", "Assets/Audio/SFX/bubble_pop_1.wav");
                so.ApplyModifiedPropertiesWithoutUndo();
            });
            var bomberAsset = AssetDatabase.LoadAssetAtPath<GameObject>(BomberPath);
            var rainbowMade = MakeOne(RainbowPath, "Rainbow_Slime", slime, bee, RainbowScale, 2f, 4, true, notes, root =>
            {
                var brain = Keep<RainbowSlime>(root, RainbowPath);
                var so = new SerializedObject(brain);
                so.FindProperty("bomber").objectReferenceValue = bomberAsset != null ? bomberAsset.GetComponent<SlimeBomber>() : null;
                Clips(so, "laughs", "Assets/Audio/SFX/cute_laugh.wav", "Assets/Audio/SFX/cute_laugh_2.wav");
                Clips(so, "poopSounds", "Assets/Audio/Kenney Audio/kenney_sci-fi-sounds/Audio/slime_000.ogg",
                    "Assets/Audio/Kenney Audio/kenney_sci-fi-sounds/Audio/slime_001.ogg");
                Clips(so, "popSounds", "Assets/Audio/SFX/Balloon_Pop.wav");
                if (brainSo != null)
                {
                    so.FindProperty("helmetSeconds").floatValue = brainSo.FindProperty("anomalyHelmetSeconds").floatValue;
                    so.FindProperty("rewardPop").objectReferenceValue = brainSo.FindProperty("anomalyPop").objectReferenceValue;
                    so.FindProperty("rewardSound").objectReferenceValue = brainSo.FindProperty("anomalyRewardSound").objectReferenceValue;
                    so.FindProperty("lifetime").vector2Value = brainSo.FindProperty("anomalyLifetime").vector2Value;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                RainbowSparkles(root);
            });
            if (!bomberMade || !rainbowMade) return notes;

            // The rare bug of every kind is the rainbow slime.
            var rainbow = AssetDatabase.LoadAssetAtPath<GameObject>(RainbowPath);
            foreach (var path in BugPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || prefab.GetComponent<SpiderBrain>() == null) { notes.Add("No " + path + "."); continue; }
                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var so = new SerializedObject(contents.GetComponent<SpiderBrain>());
                    var swap = so.FindProperty("anomalySwap");
                    if (swap.objectReferenceValue == rainbow) { notes.Add(prefab.name + ": already swaps its rare one for Rainbow_Slime."); continue; }
                    swap.objectReferenceValue = rainbow;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                    notes.Add(prefab.name + ": its rare one is now Rainbow_Slime.");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }
            AssetDatabase.SaveAssets();
            return notes;
        }

        // One slime enemy: a Flying_Slime variant at Scale with the hit setup, its Health FSM (Health) and Exp, then
        // 'brain' adds its own brain and sets it up.
        private static bool MakeOne(string path, string name, GameObject slime, GameObject bee, float scale, float health,
            int exp, bool rainbow, List<string> notes, System.Action<GameObject> brain)
        {
            var build = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = (GameObject)PrefabUtility.InstantiatePrefab(slime, build);
                root.name = name;
                root.tag = "Enemy";
                root.transform.localScale = Vector3.one * scale;

                var body = root.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                var ball = root.AddComponent<SphereCollider>();
                ball.radius = 0.14f;

                AddHealthFsm(root, health);

                var hits = root.AddComponent<EnemyHitDamage>();
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(bee.GetComponent<EnemyHitDamage>()), hits);
                var hso = new SerializedObject(hits);
                hso.FindProperty("toughness").floatValue = 1f;
                hso.FindProperty("exp").intValue = exp;
                hso.FindProperty("healthFsm").stringValue = HealthFsmName;
                hso.FindProperty("healthVariable").stringValue = "health";
                hso.FindProperty("oldDamageFsm").stringValue = "";
                // No NavMesh agent to knock back: the brain bounces it itself.
                hso.FindProperty("knockBack").boolValue = false;
                hso.ApplyModifiedPropertiesWithoutUndo();

                var look = new SerializedObject(root.GetComponent<FlyingSlime>());
                look.FindProperty("rainbow").boolValue = rainbow;
                look.FindProperty("destroyOnPop").boolValue = true;
                look.ApplyModifiedPropertiesWithoutUndo();

                brain(root);
                PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                notes.Add(saved ? "Made " + path + "." : "Couldn't save " + path + ".");
                return saved;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(build);
            }
        }

        // Adds the brain, with the old prefab's brain settings (if there was one) kept.
        private static T Keep<T>(GameObject root, string path) where T : Component
        {
            var brain = root.AddComponent<T>();
            var old = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (old != null && old.TryGetComponent(out T before)) EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(before), brain);
            return brain;
        }

        // A tiny Health FSM: just the "health" Enemy Hit Damage takes the hits off (the brain does the rest).
        private static void AddHealthFsm(GameObject root, float health)
        {
            var component = root.AddComponent<PlayMakerFSM>();
            component.FsmName = HealthFsmName;
            var fsm = component.Fsm;
            fsm.Variables.FloatVariables = new[] { new FsmFloat("health") { Value = health } };
            fsm.Events = new[] { FsmEvent.GetFsmEvent("Damage"), FsmEvent.GetFsmEvent("ShakeSmall") };
            var alive = new FsmState(fsm)
            {
                Name = "Alive",
                Position = new Rect(60f, 120f, 120f, 32f),
                Actions = new FsmStateAction[0],
                Transitions = new FsmTransition[0],
            };
            fsm.States = new[] { alive };
            fsm.StartState = alive.Name;
            alive.SaveActions();
        }

        // The paint bee's glow and sparkles, in every colour.
        private static void RainbowSparkles(GameObject root)
        {
            var beeAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PaintBeePath);
            if (beeAsset == null) return;
            var copy = (GameObject)Object.Instantiate(beeAsset);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(copy, root.scene);
            var auras = new SerializedObject(copy.GetComponent<PaintBee>()).FindProperty("auras");
            var gradient = new Gradient();
            gradient.SetKeys(new[]
                {
                    new GradientColorKey(new Color(1f, 0.2f, 0.2f), 0f), new GradientColorKey(new Color(1f, 0.85f, 0.15f), 0.2f),
                    new GradientColorKey(new Color(0.25f, 0.95f, 0.3f), 0.4f), new GradientColorKey(new Color(0.2f, 0.9f, 1f), 0.6f),
                    new GradientColorKey(new Color(0.25f, 0.4f, 1f), 0.8f), new GradientColorKey(new Color(0.8f, 0.3f, 1f), 1f),
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            for (int i = 0; i < auras.arraySize; i++)
            {
                var aura = auras.GetArrayElementAtIndex(i).objectReferenceValue as ParticleSystem;
                if (aura == null) continue;
                aura.transform.SetParent(root.transform, false);
                var main = aura.main;
                float alpha = main.startColor.mode == ParticleSystemGradientMode.Color ? main.startColor.color.a : 1f;
                var g = new Gradient();
                g.SetKeys(gradient.colorKeys, new[] { new GradientAlphaKey(alpha, 0f), new GradientAlphaKey(alpha, 1f) });
                main.startColor = new ParticleSystem.MinMaxGradient(g) { mode = ParticleSystemGradientMode.RandomColor };
            }
            Object.DestroyImmediate(copy);
        }

        private static void Clips(SerializedObject so, string field, params string[] paths)
        {
            var clips = paths.Select(AssetDatabase.LoadAssetAtPath<AudioClip>).Where(c => c != null).ToArray();
            var list = so.FindProperty(field);
            list.arraySize = clips.Length;
            for (int i = 0; i < clips.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
        }
    }
}

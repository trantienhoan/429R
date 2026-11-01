using System.Collections.Generic;
using System.Linq;
using Game.Combat;
using Game.Weapons;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Weapons > Elon Boong: Smoke Ball Shot (user, 2026-10-30: "check ElonBoong, change the Bullet to a
    /// smoke ball, do small explode damage when hit anything"). The Elon Boong's shot (Elon_Bullet, fired by its trigger)
    /// was a little flying Elon Boong weighing 4,200 that smashed into things and left glass. Now:
    /// - its model is hidden and it's a ball of the game's 3D smoke: its smoke copy (a linked Smoke_Puff_Loop) moved to the
    ///   middle and made dense, moving with it, plus a second linked copy leaving a trail behind (no new smoke prefab);
    /// - a small round collider, 2 kg, and a tiny Weapon Power, so its body barely hits;
    /// - Smoke Ball: the first thing it hits sets off a small blast (the Elon punch's look and sound) that hurts and pushes
    ///   what's around, never the player; flying too long, it puffs out;
    /// - its Fly FSM no longer leaves the glass pieces.
    /// Running it again only sets the numbers again.
    /// </summary>
    public static class ElonSmokeBallMenu
    {
        private const string BulletPath = "Assets/Prefabs/Weapon/Elon_Bullet.prefab";
        private const string LoopPath = "Assets/Prefabs/Effects/Smoke/Smoke_Puff_Loop.prefab";
        private const string SmallPuffPath = "Assets/Prefabs/Effects/Smoke/Smoke_Flash_Small_Puff.prefab";
        private const string FlashPath = "Assets/Lana Studio/Hyper Casual FX/Prefabs/Flash/Flash_round_yellow.prefab";
        private const string HitBlastPath = "Assets/Lana Studio/Hyper Casual FX/Prefabs/Confetti/Hit_Blast.prefab";
        private const string SoundPath = "Assets/Audio/SFX/explosion-312361.wav";
        private const string BallName = "Smoke_Ball";
        private const string OldSmokeName = "Smoke_Blast";
        private const string TrailName = "Smoke_Trail";

        private const float BallRadius = 0.17f;
        private const float BallMass = 2f;
        // Its body's own hit: 55 m/s x this stays under a hit's least power, so only the blast hurts.
        private const float BodyPower = 0.05f;

        [MenuItem("Tools/429 Game/Weapons/Elon Boong: Smoke Ball Shot")]
        public static void Run()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == BulletPath)
            {
                Debug.LogWarning("[Smoke Ball] Elon_Bullet is open in Prefab Mode: close it and run again.");
                return;
            }
            var loop = AssetDatabase.LoadAssetAtPath<GameObject>(LoopPath);
            var root = PrefabUtility.LoadPrefabContents(BulletPath);
            var notes = new List<string>();
            try
            {
                if (root.TryGetComponent(out MeshRenderer model) && model.enabled) { model.enabled = false; notes.Add("model hidden"); }
                foreach (var box in root.GetComponents<BoxCollider>()) { Object.DestroyImmediate(box); notes.Add("box collider off"); }
                if (!root.TryGetComponent(out SphereCollider ball)) { ball = root.AddComponent<SphereCollider>(); notes.Add("round collider"); }
                ball.radius = BallRadius;
                ball.center = Vector3.zero;
                if (root.TryGetComponent(out Rigidbody body))
                {
                    body.mass = BallMass;
                    // Swept, not speculative: at 55 m/s a speculative contact "touched" the floor half a metre below the
                    // gun, so the ball went off at the muzzle.
                    body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                }
                if (root.TryGetComponent(out WeaponPower power))
                {
                    var so = new SerializedObject(power);
                    so.FindProperty("power").floatValue = BodyPower;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                // The ball: the bullet's own smoke copy, in the middle, dense, moving with it.
                var smoke = root.transform.Find(BallName) ?? root.transform.Find(OldSmokeName);
                if (smoke == null && loop != null)
                {
                    smoke = ((GameObject)PrefabUtility.InstantiatePrefab(loop, root.transform)).transform;
                    notes.Add("smoke ball added");
                }
                if (smoke != null)
                {
                    smoke.name = BallName;
                    smoke.localPosition = Vector3.zero;
                    smoke.localRotation = Quaternion.identity;
                    MakeBall(smoke.GetComponent<ParticleSystem>());
                    // The old copy had an FSM that switched it off the moment it appeared (so the old smoke never showed).
                    foreach (var fsm in smoke.GetComponents<PlayMakerFSM>())
                    {
                        if (!fsm.enabled) continue;
                        fsm.enabled = false;
                        notes.Add("ball's switch-off FSM off");
                    }

                    // The trail: another linked copy, looking the same, left behind in the world.
                    var trail = root.transform.Find(TrailName);
                    if (trail == null && loop != null)
                    {
                        trail = ((GameObject)PrefabUtility.InstantiatePrefab(loop, root.transform)).transform;
                        trail.name = TrailName;
                        notes.Add("trail added");
                    }
                    if (trail != null)
                    {
                        trail.localPosition = Vector3.zero;
                        trail.localRotation = Quaternion.identity;
                        var from = smoke.GetComponent<ParticleSystem>();
                        var to = trail.GetComponent<ParticleSystem>();
                        EditorUtility.CopySerialized(from, to);
                        EditorUtility.CopySerialized(from.GetComponent<ParticleSystemRenderer>(), to.GetComponent<ParticleSystemRenderer>());
                        MakeTrail(to);
                    }
                }

                if (!root.TryGetComponent(out SmokeBall shot)) { shot = root.AddComponent<SmokeBall>(); notes.Add("Smoke Ball added"); }
                var settings = new SerializedObject(shot);
                var effects = settings.FindProperty("effects");
                var list = new[] { FlashPath, HitBlastPath, SmallPuffPath }.Select(AssetDatabase.LoadAssetAtPath<GameObject>).Where(e => e != null).ToArray();
                effects.arraySize = list.Length;
                for (int i = 0; i < list.Length; i++) effects.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
                settings.FindProperty("sound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(SoundPath);
                settings.FindProperty("volume").floatValue = 0.45f;
                settings.FindProperty("fizzleEffect").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(SmallPuffPath);
                settings.ApplyModifiedPropertiesWithoutUndo();

                if (NoGlass(root)) notes.Add("no glass pieces");
                PrefabUtility.SaveAsPrefabAsset(root, BulletPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            Debug.Log("[Smoke Ball] Elon_Bullet: " + (notes.Count > 0 ? string.Join(", ", notes) : "numbers set again") + ".");
        }

        [MenuItem("Tools/429 Game/Weapons/Elon Boong: Smoke Ball Shot", true)]
        private static bool NotPlaying() => !EditorApplication.isPlaying;

        // A churning ball about 35 cm across: short-lived puffs made all the time in a small sphere, moving with the shot.
        private static void MakeBall(ParticleSystem system)
        {
            if (system == null) return;
            var main = system.main;
            main.loop = true;
            main.duration = 1f;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.24f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
            main.maxParticles = 80;
            var emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = 50f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(new ParticleSystem.Burst[0]);
            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.06f;
        }

        // Smaller puffs left behind as it flies (so many a metre), fading where they were made.
        private static void MakeTrail(ParticleSystem system)
        {
            var main = system.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.05f);
            main.maxParticles = 300;
            var emission = system.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 5f;
            emission.SetBursts(new ParticleSystem.Burst[0]);
            var shape = system.shape;
            shape.radius = 0.03f;
        }

        // The Fly FSM's Activate Parts made the glass pieces (Glass_drop): off. True if it changed.
        private static bool NoGlass(GameObject root)
        {
            var fly = root.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "Fly");
            if (fly == null) return false;
            if (!fly.Fsm.Initialized) fly.Fsm.Init(fly);
            var state = fly.Fsm.GetState("Activate Parts");
            state?.LoadActions();
            if (state == null || state.ActionData.ActionCount != state.Actions.Length) return false;
            bool changed = false;
            foreach (var make in state.Actions.OfType<CreateObject>())
            {
                if (!make.Enabled) continue;
                make.Enabled = false;
                changed = true;
            }
            if (changed)
            {
                state.SaveActions();
                EditorUtility.SetDirty(fly);
            }
            return changed;
        }
    }
}

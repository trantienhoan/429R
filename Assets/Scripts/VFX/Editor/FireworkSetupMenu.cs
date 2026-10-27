using Game.VFX;
using UnityEditor;
using UnityEngine;
using static UnityEngine.ParticleSystem;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > VFX > Build Win Fireworks: builds the firework shows for a win, in Assets/Prefabs/Light:
    /// - Firework_Win: six rockets go up from where it's put (in about a second) with glowing trails, each a random
    ///   colour (gold, pink, cyan, green, orange, violet), and burst high up: a flash, a ball of streaking sparks that
    ///   slow down and droop, and a crackle of twinkles as they go out. About 2.5 m tall and 3 m across (scale it down
    ///   for a room: 0.6 fits under a ceiling). The picture puzzle's win uses it.
    /// - Firework_Big: the same show 1.6 times as big (higher, wider, bigger sparks), for out in the open: the end's
    ///   Thank You For Playing.
    /// Sounds: a whistle per rocket, a bang per burst (Firework Sounds). Each plays once, by itself, as it appears, and
    /// keeps going while the game is stopped (unscaled time). Destroy it after about 5 s. Running it again rebuilds the
    /// prefabs and their materials (Assets/Data/Effects/Firework *.mat).
    /// Tools > 429 Game > VFX > Fireworks For Thank You For Playing puts a Firework Show (with Firework_Big) on
    /// Thank_You_GAME_OVER, the banner the end chest lets out: volleys round it through the Thank You For Playing song,
    /// and a finale as it flies away. It builds the fireworks first if they aren't made yet.
    /// Tools > 429 Game > VFX > Fireworks For Nesting Pumpkin Bonus gives Bonus_Round (the nesting pumpkin bonus) its
    /// Win Fireworks (Firework_Big: the stage is open once the room's walls are gone): they go up only when every
    /// pumpkin is broken.
    /// </summary>
    public static class FireworkSetupMenu
    {
        public const string PrefabPath = "Assets/Prefabs/Light/Firework_Win.prefab";
        public const string BigPath = "Assets/Prefabs/Light/Firework_Big.prefab";
        private const float BigSize = 1.6f;
        private const string ThankYouPath = "Assets/Prefabs/Items/Thank_You_GAME_OVER.prefab";
        private const string BonusRoundPath = "Assets/Prefabs/Bases/Bonus_Round.prefab";
        private const string SoftTexture = "Assets/Lana Studio/Hyper Casual FX/Textures/Circle01.png";
        private const string StarTexture = "Assets/Lana Studio/Hyper Casual FX/Textures/Flare02.png";
        private const string Sfx = "Assets/Audio/SFX/";
        // URP's particle shader without fog: the scene fog (6-22 m) would dull fireworks bursting a few metres off.
        private const string NoFogShader = "429/Particles/Unlit No Fog";
        // The Thank You banner is about 8 m wide and faces the player: fireworks behind it, past its ends and in the middle.
        private static readonly Vector3[] ThankYouSpots = { new(-5f, -1.2f, -2.5f), new(5f, -1.2f, -2.5f), new(0f, -1.4f, -4f) };

        [MenuItem("Tools/429 Game/VFX/Build Win Fireworks")]
        public static void Run()
        {
            Build();
            BuildBig();
            AssetDatabase.SaveAssets();
            Debug.Log("[Fireworks] built " + PrefabPath + " and " + BigPath);
        }

        [MenuItem("Tools/429 Game/VFX/Fireworks For Thank You For Playing")]
        public static void ThankYou()
        {
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == ThankYouPath)
            {
                Debug.LogWarning("[Fireworks] Thank_You_GAME_OVER is open in Prefab Mode: close it, then run this again.");
                return;
            }
            var fireworks = AssetDatabase.LoadAssetAtPath<GameObject>(BigPath);
            if (fireworks == null) fireworks = BuildBig();
            var root = PrefabUtility.LoadPrefabContents(ThankYouPath);
            try
            {
                var show = root.GetComponent<FireworkShow>();
                bool added = show == null;
                if (added) show = root.AddComponent<FireworkShow>();
                var so = new SerializedObject(show);
                so.FindProperty("firework").objectReferenceValue = fireworks;
                var spots = so.FindProperty("spots");
                spots.arraySize = ThankYouSpots.Length;
                for (int i = 0; i < ThankYouSpots.Length; i++) spots.GetArrayElementAtIndex(i).vector3Value = ThankYouSpots[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, ThankYouPath);
                Debug.Log("[Fireworks] Thank_You_GAME_OVER: Firework Show " + (added ? "added" : "set up again") + " with Firework_Big");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/429 Game/VFX/Fireworks For Nesting Pumpkin Bonus")]
        public static void NestingBonus()
        {
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == BonusRoundPath)
            {
                Debug.LogWarning("[Fireworks] Bonus_Round is open in Prefab Mode: close it, then run this again.");
                return;
            }
            var fireworks = AssetDatabase.LoadAssetAtPath<GameObject>(BigPath);
            if (fireworks == null) fireworks = BuildBig();
            var root = PrefabUtility.LoadPrefabContents(BonusRoundPath);
            try
            {
                var round = root.GetComponentInChildren<Game.Bonus.BonusRound>(true);
                if (round == null)
                {
                    Debug.LogWarning("[Fireworks] Bonus_Round has no Bonus Round.");
                    return;
                }
                var so = new SerializedObject(round);
                so.FindProperty("winFireworks").objectReferenceValue = fireworks;
                so.FindProperty("fireworkSpread").vector2Value = new Vector2(3f, 2.8f);
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, BonusRoundPath);
                Debug.Log("[Fireworks] Bonus_Round: Win Fireworks set (" + so.FindProperty("count").intValue + " pumpkins)");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
        }

        public static GameObject Build() => Build(PrefabPath, "Firework_Win", 1f);

        public static GameObject BuildBig() => Build(BigPath, "Firework_Big", BigSize);

        // The show at 'size' times its heights, widths and spark sizes (the same timing).
        private static GameObject Build(string path, string name, float size)
        {
            var glow = NoFog(DangerFlameSetupMenu.MakeMaterial("Firework Glow", SoftTexture, true));
            var star = NoFog(DangerFlameSetupMenu.MakeMaterial("Firework Twinkle", StarTexture, true));
            var root = new GameObject(name);
            try
            {
                var rockets = Rockets(DangerFlameSetupMenu.Child(root, "Rockets"), glow, size);
                var burst = Burst(DangerFlameSetupMenu.Child(rockets.gameObject, "Burst"), glow, size);
                var flash = Flash(DangerFlameSetupMenu.Child(rockets.gameObject, "Flash"), glow, size);
                var twinkle = Twinkle(DangerFlameSetupMenu.Child(burst.gameObject, "Twinkle"), star, size);

                var subs = rockets.subEmitters;
                subs.enabled = true;
                subs.AddSubEmitter(burst, ParticleSystemSubEmitterType.Death, ParticleSystemSubEmitterProperties.InheritColor);
                subs.AddSubEmitter(flash, ParticleSystemSubEmitterType.Death, ParticleSystemSubEmitterProperties.InheritColor);
                var crackle = burst.subEmitters;
                crackle.enabled = true;
                crackle.AddSubEmitter(twinkle, ParticleSystemSubEmitterType.Death, ParticleSystemSubEmitterProperties.InheritColor, 0.35f);

                foreach (var ps in root.GetComponentsInChildren<ParticleSystem>())
                {
                    var main = ps.main;
                    main.useUnscaledTime = true;
                    main.simulationSpace = ParticleSystemSimulationSpace.World;
                    main.prewarm = false;
                    main.loop = false;
                    main.duration = 1.6f;
                    main.playOnAwake = true;
                    main.stopAction = ParticleSystemStopAction.None;
                }

                var sounds = root.AddComponent<FireworkSounds>();
                var so = new SerializedObject(sounds);
                so.FindProperty("rockets").objectReferenceValue = rockets;
                so.FindProperty("launchSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + "firework_launch.wav");
                var bangs = so.FindProperty("bangSounds");
                bangs.arraySize = 2;
                bangs.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + "firework_bang_1.wav");
                bangs.GetArrayElementAtIndex(1).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + "firework_bang_2.wav");
                so.ApplyModifiedPropertiesWithoutUndo();

                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Material NoFog(Material material)
        {
            var shader = Shader.Find(NoFogShader);
            if (shader == null) return material;
            if (material.shader != shader) material.shader = shader;
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        // The colours a rocket can be (picked at random, one each).
        private static MinMaxGradient Colours()
        {
            var gradient = new Gradient { mode = GradientMode.Fixed };
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.8f, 0.3f), 0.16f), new GradientColorKey(new Color(1f, 0.35f, 0.75f), 0.33f),
                    new GradientColorKey(new Color(0.35f, 0.9f, 1f), 0.5f), new GradientColorKey(new Color(0.5f, 1f, 0.4f), 0.66f),
                    new GradientColorKey(new Color(1f, 0.5f, 0.15f), 0.83f), new GradientColorKey(new Color(0.7f, 0.45f, 1f), 1f),
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return new MinMaxGradient(gradient) { mode = ParticleSystemGradientMode.RandomColor };
        }

        // Six rockets shooting up, a little spread, each trailing a glow.
        private static ParticleSystem Rockets(GameObject go, Material material, float size)
        {
            var ps = DangerFlameSetupMenu.NewSystem(go, material, ParticleSystemRenderMode.Billboard, 12);
            var main = ps.main;
            main.startLifetime = new MinMaxCurve(0.55f, 0.8f);
            main.startSpeed = new MinMaxCurve(4.6f * size, 6f * size);
            main.startSize = new MinMaxCurve(0.06f * size, 0.08f * size);
            main.startColor = Colours();
            main.gravityModifier = 0.6f * size;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new Burst(0f, 2), new Burst(0.3f, 1), new Burst(0.6f, 2), new Burst(0.95f, 1) });
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 22f;
            shape.radius = 0.15f * size;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var trails = ps.trails;
            trails.enabled = true;
            trails.lifetime = new MinMaxCurve(0.3f);
            trails.minVertexDistance = 0.04f * size;
            trails.widthOverTrail = new MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));
            trails.inheritParticleColor = true;
            trails.dieWithParticles = false;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            trails.colorOverTrail = new MinMaxGradient(fade);
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.trailMaterial = material;
            return ps;
        }

        // The burst: a ball of sparks streaking out, slowing, drooping and going out.
        private static ParticleSystem Burst(GameObject go, Material material, float size)
        {
            var ps = DangerFlameSetupMenu.NewSystem(go, material, ParticleSystemRenderMode.Stretch, 600);
            var main = ps.main;
            main.startLifetime = new MinMaxCurve(1.0f, 1.6f);
            main.startSpeed = new MinMaxCurve(1.8f * size, 2.4f * size);
            main.startSize = new MinMaxCurve(0.035f * size, 0.06f * size);
            main.gravityModifier = 0.35f * size;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new Burst(0f, 90) });
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f * size;
            var drag = ps.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.limit = 100f;
            drag.drag = 1.4f;
            drag.multiplyDragByParticleSize = false;
            drag.multiplyDragByParticleVelocity = false;
            DangerFlameSetupMenu.Fade(ps,
                new[] { DangerFlameSetupMenu.Key(1f, 1f, 1f, 0f), DangerFlameSetupMenu.Key(1f, 1f, 1f, 0.5f), DangerFlameSetupMenu.Key(1f, 0.85f, 0.7f, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0f, 1f) });
            DangerFlameSetupMenu.Shrink(ps, 1f, 1f, 0.2f);
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.velocityScale = 0.12f;
            renderer.lengthScale = 1.6f;
            return ps;
        }

        // A soft flash of the rocket's colour as it bursts.
        private static ParticleSystem Flash(GameObject go, Material material, float size)
        {
            var ps = DangerFlameSetupMenu.NewSystem(go, material, ParticleSystemRenderMode.Billboard, 12);
            var main = ps.main;
            main.startLifetime = 0.2f;
            main.startSize = new MinMaxCurve(1f * size, 1.4f * size);
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new Burst(0f, 1) });
            var shape = ps.shape;
            shape.enabled = false;
            DangerFlameSetupMenu.Fade(ps,
                new[] { DangerFlameSetupMenu.Key(1f, 1f, 1f, 0f), DangerFlameSetupMenu.Key(1f, 1f, 1f, 1f) },
                new[] { new GradientAlphaKey(0.75f, 0f), new GradientAlphaKey(0f, 1f) });
            DangerFlameSetupMenu.Shrink(ps, 0.6f, 1f, 1f);
            ps.GetComponent<ParticleSystemRenderer>().maxParticleSize = 2f;
            return ps;
        }

        // The crackle: little stars popping on and off where sparks go out.
        private static ParticleSystem Twinkle(GameObject go, Material material, float size)
        {
            var ps = DangerFlameSetupMenu.NewSystem(go, material, ParticleSystemRenderMode.Billboard, 300);
            var main = ps.main;
            main.startLifetime = new MinMaxCurve(0.2f, 0.45f);
            main.startSize = new MinMaxCurve(0.05f * size, 0.09f * size);
            main.startRotation = new MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new MinMaxGradient(Color.white, new Color(1f, 0.95f, 0.8f));
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new Burst(0f, 1) });
            var shape = ps.shape;
            shape.enabled = false;
            var twinkle = ps.sizeOverLifetime;
            twinkle.enabled = true;
            twinkle.size = new MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.2f, 1f), new Keyframe(0.5f, 0.3f), new Keyframe(0.7f, 0.9f), new Keyframe(1f, 0f)));
            return ps;
        }
    }
}

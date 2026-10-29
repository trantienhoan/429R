using System.Collections.Generic;
using System.Linq;
using Game.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Enemies > Build Pumpkin Boss Smoke FX: makes the Pumpkin boss's smoke and dust (in
    /// Prefabs/Enemies/Boss_FX) from the game's own cartoon smoke (the Lana Hyper Casual FX sprites and their Circles_AB
    /// material, the one every smoke in the game uses), then puts Pumpkin Boss Fx on pumpkin_boss_Z with them and its
    /// sounds. Run it again any time to rebuild (the prefabs keep their GUIDs).
    /// - Boss_Ground_Blast: a boom, a shockwave ring along the floor, dust rolling out, a smoke column and flying dirt.
    ///   About 7 m across at size 1. Under 2.2 s (the Effect Pool hands a copy out again after 3 s).
    /// - Boss_Hit_Blast: a smaller boom, ring and puffs, for a blow landing on the player.
    /// - Boss_Scrape_Dust: a few puffs of dust and dirt (while it spins).
    /// - Boss_Vomit_Stream: black smoke pouring out of the mouth, falling and spreading on the floor (a collision plane).
    /// - Boss_Vomit_Cloud: the black cloud that rolls out where the vomited bugs come out (puffs up to about 1.5 m).
    /// Puffs grow, then shrink away while still solid, like the game's other cartoon smoke. All particles are kept to
    /// half the view at most, like the Effect Pool's, for the headset's sake (the floor ring gets more, in the game).
    /// Their smoke and dust (not the booms, rings or dirt) are then drawn as the shared Smoke_Puff's 3D puffs, the black
    /// vomit with its dark material (user, 2026-10-29: "the Pumpkin_Boss_Z still use the old smoke"): Tools > 429 Game >
    /// Enemies > Pumpkin Boss Smoke FX: 3D Puffs does that to the prefabs as they are.
    /// </summary>
    public static class PumpkinBossFxMenu
    {
        private const string Folder = "Assets/Prefabs/Enemies/Boss_FX";
        private const string BossPath = "Assets/Prefabs/Enemies/pumpkin_boss_Z.prefab";
        private const string MaterialPath = "Assets/Lana Studio/Hyper Casual FX/Materials/Circles_AB.mat";
        private const string Textures = "Assets/Lana Studio/Hyper Casual FX/Textures/";
        private const string Kenney = "Assets/Audio/Kenney Audio/kenney_sci-fi-sounds/Audio/";
        private const float MaxScreen = 0.5f;

        private static Material material;

        // The smoke and dust parts, drawn as the shared 3D puffs.
        private static readonly string[] SmokeParts = { "Dust Ring", "Smoke Column", "Puffs", "Dust", "Boss_Vomit_Stream", "Boss_Vomit_Cloud" };
        private static readonly string[] Effects = { "Boss_Ground_Blast", "Boss_Hit_Blast", "Boss_Scrape_Dust", "Boss_Vomit_Stream", "Boss_Vomit_Cloud" };

        [MenuItem("Tools/429 Game/Enemies/Build Pumpkin Boss Smoke FX")]
        public static void Build()
        {
            material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null) { Debug.LogError("Pumpkin boss FX: no " + MaterialPath); return; }
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Prefabs/Enemies", "Boss_FX");

            var ground = Save(MakeGroundBlast(), "Boss_Ground_Blast");
            var hit = Save(MakeHitBlast(), "Boss_Hit_Blast");
            var scrape = Save(MakeScrapeDust(), "Boss_Scrape_Dust");
            var stream = Save(MakeVomitStream(), "Boss_Vomit_Stream");
            var cloud = Save(MakeVomitCloud(), "Boss_Vomit_Cloud");
            string set = SetUpBoss(ground, hit, scrape, stream, cloud);
            Debug.Log($"Pumpkin boss FX built in {Folder}. {set} {MakePuffs3D()}");
        }

        [MenuItem("Tools/429 Game/Enemies/Pumpkin Boss Smoke FX: 3D Puffs")]
        public static void MakePuffs3DMenu() => Debug.Log(MakePuffs3D());

        /// <summary>The boss effects' smoke and dust drawn as the shared 3D puffs, in the prefabs as they are.</summary>
        public static string MakePuffs3D()
        {
            var made = new List<string>();
            foreach (var name in Effects)
            {
                string path = $"{Folder}/{name}.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                if (stage != null && stage.assetPath == path) { made.Add(name + " open in Prefab Mode, skipped"); continue; }
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int count = 0;
                    foreach (var system in root.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        if (SmokeParts.Contains(system.name) && Smoke3DMenu.UseSharedPuffs(system)) count++;
                    }
                    if (count == 0) continue;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    made.Add($"{name} ({count})");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            return made.Count > 0 ? "3D puffs: " + string.Join(", ", made) + "." : "No 3D puffs (is Smoke_Puff 3D?).";
        }

        // ---------- the effects ----------

        private static GameObject MakeGroundBlast()
        {
            var root = new GameObject("Boss_Ground_Blast");

            // The boom: a spiky cartoon flash, gone in a blink.
            var boom = Child(root, "Boom", new Vector3(0f, 0.9f, 0f));
            Main(boom, life: (0.12f, 0.16f), size: (2.0f, 2.6f), speed: (0f, 0f), color: (C(1f, 0.86f, 0.45f), C(1f, 0.68f, 0.28f)));
            Burst(boom, 1);
            Shape(boom, null);
            SizeOver(boom, Curve((0f, 0.55f), (0.35f, 1f), (1f, 1.15f)));
            FadeOver(boom, Alpha((0f, 0.9f), (0.4f, 0.75f), (1f, 0f)));
            Look(boom, ParticleSystemRenderMode.Billboard, Sprites("Dust02"));

            // The shockwave: a ring racing out along the floor.
            var ring = Child(root, "Shockwave", new Vector3(0f, 0.06f, 0f));
            Main(ring, life: (0.5f, 0.5f), size: (10f, 10f), speed: (0f, 0f), color: (C(0.95f, 0.9f, 0.78f), C(0.95f, 0.9f, 0.78f)), rotate: false);
            Burst(ring, 1);
            Shape(ring, null);
            SizeOver(ring, Curve((0f, 0.1f), (0.3f, 0.7f), (1f, 1f)));
            FadeOver(ring, Alpha((0f, 1f), (0.45f, 0.75f), (1f, 0f)));
            Look(ring, ParticleSystemRenderMode.HorizontalBillboard, Sprites("Circle06"));

            // Dust rolling out along the floor in a ring, billowing, then shrinking away like the game's other cartoon
            // smoke (opaque to the end, not big see-through discs).
            var dust = Child(root, "Dust Ring", new Vector3(0f, 0.6f, 0f));
            Main(dust, life: (1.3f, 1.9f), size: (1.0f, 1.6f), speed: (7f, 11f), color: (C(0.55f, 0.5f, 0.45f), C(0.74f, 0.69f, 0.62f)), gravity: -0.03f);
            Burst(dust, 20);
            Shape(dust, s => { s.shapeType = ParticleSystemShapeType.Circle; s.radius = 0.7f; s.rotation = new Vector3(90f, 0f, 0f); });
            Slow(dust, 0.6f, 0.085f);
            SizeOver(dust, Curve((0f, 0.6f), (0.45f, 1.5f), (1f, 0.6f)));
            FadeOver(dust, Alpha((0f, 0f), (0.05f, 1f), (0.8f, 1f), (1f, 0f)));
            Look(dust, ParticleSystemRenderMode.VerticalBillboard, Sprites("Dust01"));

            // Dark smoke thrown up out of the hit.
            var column = Child(root, "Smoke Column", new Vector3(0f, 0.3f, 0f));
            Main(column, life: (1.4f, 2.0f), size: (1.3f, 2.0f), speed: (3f, 6.5f), color: (C(0.2f, 0.19f, 0.2f), C(0.36f, 0.34f, 0.33f)), gravity: -0.08f);
            Burst(column, 9);
            Burst(column, 4, 0.08f);
            Shape(column, s => { s.shapeType = ParticleSystemShapeType.Cone; s.angle = 30f; s.radius = 0.5f; s.rotation = new Vector3(-90f, 0f, 0f); });
            Slow(column, 0.5f, 0.07f);
            SizeOver(column, Curve((0f, 0.7f), (0.55f, 1.6f), (1f, 0.5f)));
            FadeOver(column, Alpha((0f, 0f), (0.04f, 1f), (0.8f, 1f), (1f, 0f)));
            Look(column, ParticleSystemRenderMode.VerticalBillboard, Sprites("Dust01"));

            // Clods of dirt and bits of pumpkin flying out and falling back.
            var dirt = Child(root, "Dirt", new Vector3(0f, 0.2f, 0f));
            Main(dirt, life: (0.7f, 1.0f), size: (0.14f, 0.34f), speed: (6f, 11f), color: (C(0.24f, 0.15f, 0.08f), C(0.9f, 0.48f, 0.12f)), gravity: 2.2f);
            Burst(dirt, 18);
            Shape(dirt, s => { s.shapeType = ParticleSystemShapeType.Cone; s.angle = 55f; s.radius = 0.6f; s.rotation = new Vector3(-90f, 0f, 0f); });
            Spin(dirt, 360f);
            FadeOver(dirt, Alpha((0f, 1f), (0.85f, 1f), (1f, 0f)));
            Look(dirt, ParticleSystemRenderMode.Billboard, Sprites("Dust01"));
            return root;
        }

        private static GameObject MakeHitBlast()
        {
            var root = new GameObject("Boss_Hit_Blast");

            var boom = Child(root, "Boom", Vector3.zero);
            Main(boom, life: (0.1f, 0.14f), size: (1.0f, 1.3f), speed: (0f, 0f), color: (C(1f, 0.86f, 0.45f), C(1f, 0.68f, 0.28f)));
            Burst(boom, 1);
            Shape(boom, null);
            SizeOver(boom, Curve((0f, 0.5f), (0.4f, 1f), (1f, 1.1f)));
            FadeOver(boom, Alpha((0f, 1f), (0.5f, 0.9f), (1f, 0f)));
            Look(boom, ParticleSystemRenderMode.Billboard, Sprites("Dust02"));

            var ring = Child(root, "Ring", Vector3.zero);
            Main(ring, life: (0.25f, 0.25f), size: (2.6f, 2.6f), speed: (0f, 0f), color: (C(1f, 0.93f, 0.8f), C(1f, 0.93f, 0.8f)), rotate: false);
            Burst(ring, 1);
            Shape(ring, null);
            SizeOver(ring, Curve((0f, 0.2f), (0.4f, 0.8f), (1f, 1f)));
            FadeOver(ring, Alpha((0f, 0.85f), (1f, 0f)));
            Look(ring, ParticleSystemRenderMode.Billboard, Sprites("Circle06"));

            var puffs = Child(root, "Puffs", Vector3.zero);
            Main(puffs, life: (0.5f, 0.8f), size: (0.5f, 0.8f), speed: (3f, 6f), color: (C(0.5f, 0.48f, 0.46f), C(0.7f, 0.67f, 0.63f)), gravity: -0.05f);
            Burst(puffs, 10);
            Shape(puffs, s => { s.shapeType = ParticleSystemShapeType.Sphere; s.radius = 0.15f; });
            Slow(puffs, 0.3f, 0.12f);
            SizeOver(puffs, Curve((0f, 0.6f), (0.5f, 1.5f), (1f, 0.7f)));
            FadeOver(puffs, Alpha((0f, 0f), (0.06f, 1f), (0.8f, 1f), (1f, 0f)));
            Look(puffs, ParticleSystemRenderMode.VerticalBillboard, Sprites("Dust01"));
            return root;
        }

        // A few puffs of dust and dirt kicked up off the floor (while it spins).
        private static GameObject MakeScrapeDust()
        {
            var root = new GameObject("Boss_Scrape_Dust");

            var dust = Child(root, "Dust", new Vector3(0f, 0.35f, 0f));
            Main(dust, life: (0.6f, 0.9f), size: (0.6f, 1.0f), speed: (2f, 4f), color: (C(0.55f, 0.5f, 0.45f), C(0.74f, 0.69f, 0.62f)), gravity: -0.05f);
            Burst(dust, 7);
            Shape(dust, s => { s.shapeType = ParticleSystemShapeType.Circle; s.radius = 0.4f; s.rotation = new Vector3(90f, 0f, 0f); });
            Slow(dust, 0.4f, 0.1f);
            SizeOver(dust, Curve((0f, 0.6f), (0.45f, 1.4f), (1f, 0.6f)));
            FadeOver(dust, Alpha((0f, 0f), (0.06f, 1f), (0.8f, 1f), (1f, 0f)));
            Look(dust, ParticleSystemRenderMode.VerticalBillboard, Sprites("Dust01"));

            var dirt = Child(root, "Dirt", new Vector3(0f, 0.15f, 0f));
            Main(dirt, life: (0.5f, 0.7f), size: (0.1f, 0.22f), speed: (3f, 6f), color: (C(0.24f, 0.15f, 0.08f), C(0.9f, 0.48f, 0.12f)), gravity: 2.2f);
            Burst(dirt, 5);
            Shape(dirt, s => { s.shapeType = ParticleSystemShapeType.Cone; s.angle = 45f; s.radius = 0.3f; s.rotation = new Vector3(-90f, 0f, 0f); });
            Spin(dirt, 360f);
            FadeOver(dirt, Alpha((0f, 1f), (0.85f, 1f), (1f, 0f)));
            Look(dirt, ParticleSystemRenderMode.Billboard, Sprites("Dust01"));
            return root;
        }

        private static GameObject MakeVomitStream()
        {
            // The system is the root: the boss moves it to its mouth every frame and points it out of the mouth.
            var root = new GameObject("Boss_Vomit_Stream");
            var stream = root.AddComponent<ParticleSystem>();
            Main(stream, life: (1.7f, 2.3f), size: (0.35f, 0.65f), speed: (2.5f, 4f), color: (C(0.02f, 0.015f, 0.03f), C(0.13f, 0.09f, 0.12f)), gravity: 0.55f);
            var main = stream.main;
            main.loop = true;
            main.duration = 1f;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 150;
            var emission = stream.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            Shape(stream, s => { s.shapeType = ParticleSystemShapeType.Cone; s.angle = 14f; s.radius = 0.08f; });
            SizeOver(stream, Curve((0f, 1f), (0.35f, 2.2f), (0.75f, 2.5f), (1f, 1.1f)));
            FadeOver(stream, Alpha((0f, 0f), (0.06f, 1f), (0.8f, 1f), (1f, 0f)));
            Look(stream, ParticleSystemRenderMode.VerticalBillboard, Sprites("Dust01"));

            // It falls onto the floor (a plane the boss keeps at floor height) and spreads out along it.
            var floor = new GameObject("Floor");
            floor.transform.SetParent(root.transform, false);
            var hits = stream.collision;
            hits.enabled = true;
            hits.type = ParticleSystemCollisionType.Planes;
            hits.SetPlane(0, floor.transform);
            hits.dampen = 0.55f;
            hits.bounce = 0.05f;
            hits.lifetimeLoss = 0f;
            hits.radiusScale = 0.35f;
            hits.enableDynamicColliders = false;
            hits.sendCollisionMessages = false;
            return root;
        }

        private static GameObject MakeVomitCloud()
        {
            var root = new GameObject("Boss_Vomit_Cloud");
            var cloud = root.AddComponent<ParticleSystem>();
            // Puffs up to about 1.5 m high: they hide the bugs on the floor, and a player standing nearby can still see
            // over them.
            Main(cloud, life: (2.4f, 3.0f), size: (1.1f, 1.6f), speed: (0.6f, 1.8f), color: (C(0.02f, 0.015f, 0.03f), C(0.13f, 0.09f, 0.12f)), gravity: -0.03f);
            var main = cloud.main;
            main.loop = true;
            main.duration = 1f;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 60;
            var emission = cloud.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            // A flat oval on the floor, 4.5 m across and 3.5 m deep, puffs rolling outwards.
            Shape(cloud, s =>
            {
                s.shapeType = ParticleSystemShapeType.Circle;
                s.radius = 1.6f;
                s.radiusThickness = 1f;
                s.position = new Vector3(0f, 0.5f, 0f);
                s.rotation = new Vector3(90f, 0f, 0f);
                s.scale = new Vector3(1.4f, 1.1f, 1f);
            });
            Slow(cloud, 0.2f, 0.05f);
            SizeOver(cloud, Curve((0f, 0.55f), (0.3f, 1f), (0.75f, 1.1f), (1f, 0.6f)));
            FadeOver(cloud, Alpha((0f, 0f), (0.06f, 1f), (0.8f, 1f), (1f, 0f)));
            Look(cloud, ParticleSystemRenderMode.VerticalBillboard, Sprites("Dust01"));
            return root;
        }

        // ---------- the boss ----------

        private static string SetUpBoss(GameObject ground, GameObject hit, GameObject scrape, GameObject stream, GameObject cloud)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == BossPath)
                return "pumpkin_boss_Z is open in Prefab Mode: close it and run this again to set the boss up.";
            var boss = PrefabUtility.LoadPrefabContents(BossPath);
            try
            {
                var fx = boss.GetComponent<PumpkinBossFx>();
                if (fx == null) fx = boss.AddComponent<PumpkinBossFx>();
                var so = new SerializedObject(fx);
                so.FindProperty("groundBlast").objectReferenceValue = ground;
                so.FindProperty("hitBlast").objectReferenceValue = hit;
                so.FindProperty("scrapeDust").objectReferenceValue = scrape;
                so.FindProperty("vomitStream").objectReferenceValue = stream.GetComponent<ParticleSystem>();
                so.FindProperty("vomitCloud").objectReferenceValue = cloud.GetComponent<ParticleSystem>();
                so.FindProperty("bigBoom").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(Kenney + "lowFrequency_explosion_001.ogg");
                so.FindProperty("mediumBoom").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(Kenney + "explosionCrunch_000.ogg");
                so.FindProperty("vomitSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(Kenney + "slime_000.ogg");
                // Values from the first version move to the new ones; anything changed by hand is kept.
                Renew(so, "streamRate", 26f, 16f);
                Renew(so, "streamPerRetch", 8, 5);
                Renew(so, "cloudFrom", 0.6f, 0.9f);
                Renew(so, "cloudUntil", 1.7f, 1.9f);
                Renew(so, "cloudBurst", 12, 8);
                Renew(so, "cloudRate", 9f, 12f);
                so.ApplyModifiedPropertiesWithoutUndo();

                var brain = boss.GetComponent<PumpkinBossBrain>();
                if (brain != null)
                {
                    var b = new SerializedObject(brain);
                    b.FindProperty("fx").objectReferenceValue = fx;
                    Renew(b, "vomitBugDelay", 0.3f, 0.5f);
                    b.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(boss, BossPath);
                return "pumpkin_boss_Z: Pumpkin Boss Fx set up" + (brain == null ? " (no brain found!)" : ".");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(boss);
            }
        }

        // ---------- building blocks ----------

        private static GameObject Save(GameObject root, string name)
        {
            try { return PrefabUtility.SaveAsPrefabAsset(root, $"{Folder}/{name}.prefab"); }
            finally { Object.DestroyImmediate(root); }
        }

        private static void Renew(SerializedObject so, string field, float from, float to)
        {
            var p = so.FindProperty(field);
            if (p != null && Mathf.Approximately(p.floatValue, from)) p.floatValue = to;
        }

        private static void Renew(SerializedObject so, string field, int from, int to)
        {
            var p = so.FindProperty(field);
            if (p != null && p.intValue == from) p.intValue = to;
        }

        private static ParticleSystem Child(GameObject root, string name, Vector3 at)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = at;
            return go.AddComponent<ParticleSystem>();
        }

        private static void Main(ParticleSystem ps, (float, float) life, (float, float) size, (float, float) speed,
            (Color, Color) color, float gravity = 0f, bool rotate = true)
        {
            var main = ps.main;
            main.duration = 2f;
            main.loop = false;
            main.playOnAwake = true;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life.Item1, life.Item2);
            main.startSize = new ParticleSystem.MinMaxCurve(size.Item1, size.Item2);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.Item1, speed.Item2);
            main.startColor = new ParticleSystem.MinMaxGradient(color.Item1, color.Item2);
            main.startRotation = rotate ? new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f) : new ParticleSystem.MinMaxCurve(0f);
            main.gravityModifier = gravity;
            main.maxParticles = 64;
            main.stopAction = ParticleSystemStopAction.None;
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new ParticleSystem.Burst[0]);
        }

        private static void Burst(ParticleSystem ps, int count, float at = 0f)
        {
            var emission = ps.emission;
            var bursts = new ParticleSystem.Burst[emission.burstCount + 1];
            emission.GetBursts(bursts);
            bursts[^1] = new ParticleSystem.Burst(at, (short)count);
            emission.SetBursts(bursts);
        }

        private static void Shape(ParticleSystem ps, System.Action<ParticleSystem.ShapeModule> set)
        {
            var shape = ps.shape;
            shape.enabled = set != null;
            set?.Invoke(shape);
        }

        // Slows down to this speed: Dampen is how much of the speed over it goes each frame.
        private static void Slow(ParticleSystem ps, float limit, float dampen)
        {
            var slow = ps.limitVelocityOverLifetime;
            slow.enabled = true;
            slow.limit = limit;
            slow.dampen = dampen;
        }

        private static void Spin(ParticleSystem ps, float degreesPerSecond)
        {
            var spin = ps.rotationOverLifetime;
            spin.enabled = true;
            float r = degreesPerSecond * Mathf.Deg2Rad;
            spin.z = new ParticleSystem.MinMaxCurve(-r, r);
        }

        private static void SizeOver(ParticleSystem ps, AnimationCurve curve)
        {
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        private static void FadeOver(ParticleSystem ps, GradientAlphaKey[] alphas)
        {
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, alphas);
            fade.color = new ParticleSystem.MinMaxGradient(g);
        }

        // Circles_AB, one of the sprites at random (the sprites carry their own texture).
        private static void Look(ParticleSystem ps, ParticleSystemRenderMode mode, List<Sprite> sprites)
        {
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = material;
            r.renderMode = mode;
            r.maxParticleSize = MaxScreen;
            r.minParticleSize = 0f;
            r.sortMode = ParticleSystemSortMode.Distance;
            r.alignment = ParticleSystemRenderSpace.View;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var sheet = ps.textureSheetAnimation;
            if (sprites.Count == 0) { sheet.enabled = false; return; }
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Sprites;
            while (sheet.spriteCount > 0) sheet.RemoveSprite(0);
            foreach (var s in sprites) sheet.AddSprite(s);
            sheet.timeMode = ParticleSystemAnimationTimeMode.Lifetime;
            // One sprite per particle, picked at random, kept for its life.
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f, sprites.Count > 1 ? 0.999f : 0f);
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0f);
            sheet.cycleCount = 1;
        }

        private static List<Sprite> Sprites(string texture)
        {
            return AssetDatabase.LoadAllAssetsAtPath(Textures + texture + ".png").OfType<Sprite>().OrderBy(s => s.name).ToList();
        }

        private static Color C(float r, float g, float b) => new(r, g, b, 1f);

        private static AnimationCurve Curve(params (float t, float v)[] keys)
        {
            var c = new AnimationCurve(keys.Select(k => new Keyframe(k.t, k.v)).ToArray());
            for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0f);
            return c;
        }

        private static GradientAlphaKey[] Alpha(params (float t, float a)[] keys) =>
            keys.Select(k => new GradientAlphaKey(k.a, k.t)).ToArray();
    }
}

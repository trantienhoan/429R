using System.Collections.Generic;
using System.Linq;
using Game.Combat;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Enemies > Build Enemy Hit FX: makes the small pop every enemy shows where a hit hurts it (in
    /// Prefabs/Enemies/Hit_FX), from the game's own cartoon sprites (Lana Hyper Casual FX, Circles_AB material), and puts
    /// them in the Hit Settings asset (Hit effect). Run it again any time to rebuild them (the prefabs keep their GUIDs;
    /// hand edits on the two prefabs are lost, so change the numbers here instead).
    /// - Enemy_Hit_Pop (a hit that hurts): a cream spiky flash, 3 little white puffs and 3 yellow chips. About 0.15 m.
    /// - Enemy_Hit_Big (a big hit or a knock-out): a bigger golden flash, a ring, 5 puffs and 7 chips. About 0.3 m.
    /// Everything is gone in under half a second, and no particle is drawn bigger than a fifth of the view, so a hit
    /// right in front of the face doesn't cover it.
    /// </summary>
    public static class EnemyHitFxMenu
    {
        private const string Folder = "Assets/Prefabs/Enemies/Hit_FX";
        private const string SettingsPath = "Assets/Data/Combat/Hit Settings.asset";
        private const string MaterialPath = "Assets/Lana Studio/Hyper Casual FX/Materials/Circles_AB.mat";
        private const string Textures = "Assets/Lana Studio/Hyper Casual FX/Textures/";
        private const float MaxScreen = 0.2f;

        private static Material material;

        [MenuItem("Tools/429 Game/Enemies/Build Enemy Hit FX")]
        public static void Build()
        {
            material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null) { Debug.LogError("Enemy hit FX: no " + MaterialPath); return; }
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Prefabs/Enemies", "Hit_FX");

            var pop = Save(MakePop(), "Enemy_Hit_Pop");
            var big = Save(MakeBig(), "Enemy_Hit_Big");
            Debug.Log($"Enemy hit FX built in {Folder}. {SetUpSettings(pop, big)}");
        }

        // ---------- the effects ----------

        private static GameObject MakePop()
        {
            var root = new GameObject("Enemy_Hit_Pop");

            // A spiky cream flash, gone in a blink.
            var flash = Child(root, "Flash");
            Main(flash, life: (0.08f, 0.1f), size: (0.14f, 0.18f), speed: (0f, 0f), color: (C(1f, 0.965f, 0.855f), C(1f, 0.965f, 0.855f)));
            Burst(flash, 1);
            Shape(flash, null);
            SizeOver(flash, Curve((0f, 0.5f), (1f, 1.1f)));
            FadeOver(flash, Alpha((0f, 1f), (1f, 0f)));
            Look(flash, Sprites("Blast_spiky"));

            // Three little white puffs, popping out and shrinking away.
            var puff = Child(root, "Puff");
            Main(puff, life: (0.3f, 0.4f), size: (0.07f, 0.11f), speed: (0.4f, 0.9f), color: (C(0.93f, 0.906f, 0.863f), C(1f, 1f, 1f)));
            Burst(puff, 3);
            Shape(puff, s => { s.shapeType = ParticleSystemShapeType.Sphere; s.radius = 0.03f; });
            Slow(puff, 0.15f, 0.3f);
            SizeOver(puff, Curve((0f, 0.6f), (0.4f, 1.2f), (1f, 0.8f)));
            FadeOver(puff, Alpha((0f, 0f), (0.05f, 1f), (0.7f, 1f), (1f, 0f)));
            Look(puff, Sprites("Dust01"));

            // Three yellow chips flying off and dropping.
            var chips = Child(root, "Chips");
            Main(chips, life: (0.2f, 0.3f), size: (0.02f, 0.035f), speed: (0.9f, 1.6f), color: (C(1f, 0.886f, 0.478f), C(1f, 0.965f, 0.769f)), gravity: 1.5f);
            Burst(chips, 3);
            Shape(chips, s => { s.shapeType = ParticleSystemShapeType.Sphere; s.radius = 0.02f; });
            FadeOver(chips, Alpha((0f, 1f), (0.7f, 1f), (1f, 0f)));
            Look(chips, Sprites("Circle01"));
            return root;
        }

        private static GameObject MakeBig()
        {
            var root = new GameObject("Enemy_Hit_Big");

            var flash = Child(root, "Flash");
            Main(flash, life: (0.1f, 0.12f), size: (0.26f, 0.32f), speed: (0f, 0f), color: (C(1f, 0.847f, 0.42f), C(1f, 0.914f, 0.659f)));
            Burst(flash, 1);
            Shape(flash, null);
            SizeOver(flash, Curve((0f, 0.5f), (1f, 1.1f)));
            FadeOver(flash, Alpha((0f, 1f), (1f, 0f)));
            Look(flash, Sprites("Blast_spiky"));

            // A thin ring racing out.
            var ring = Child(root, "Ring");
            Main(ring, life: (0.18f, 0.18f), size: (0.4f, 0.4f), speed: (0f, 0f), color: (C(1f, 0.953f, 0.839f), C(1f, 0.953f, 0.839f)), rotate: false);
            Burst(ring, 1);
            Shape(ring, null);
            SizeOver(ring, Curve((0f, 0.2f), (1f, 1f)));
            FadeOver(ring, Alpha((0f, 0.8f), (1f, 0f)));
            Look(ring, Sprites("Circle06"));

            var puff = Child(root, "Puff");
            Main(puff, life: (0.3f, 0.42f), size: (0.1f, 0.16f), speed: (0.6f, 1.2f), color: (C(0.93f, 0.906f, 0.863f), C(1f, 1f, 1f)));
            Burst(puff, 5);
            Shape(puff, s => { s.shapeType = ParticleSystemShapeType.Sphere; s.radius = 0.04f; });
            Slow(puff, 0.2f, 0.3f);
            SizeOver(puff, Curve((0f, 0.6f), (0.4f, 1.2f), (1f, 0.8f)));
            FadeOver(puff, Alpha((0f, 0f), (0.05f, 1f), (0.7f, 1f), (1f, 0f)));
            Look(puff, Sprites("Dust01"));

            var chips = Child(root, "Chips");
            Main(chips, life: (0.22f, 0.32f), size: (0.025f, 0.04f), speed: (1.2f, 2f), color: (C(1f, 0.886f, 0.478f), C(1f, 0.965f, 0.769f)), gravity: 1.5f);
            Burst(chips, 7);
            Shape(chips, s => { s.shapeType = ParticleSystemShapeType.Sphere; s.radius = 0.03f; });
            FadeOver(chips, Alpha((0f, 1f), (0.7f, 1f), (1f, 0f)));
            Look(chips, Sprites("Circle01"));
            return root;
        }

        // ---------- the Hit Settings ----------

        // Fills the two slots, unless one already holds something else (chosen by hand).
        private static string SetUpSettings(GameObject pop, GameObject big)
        {
            var asset = AssetDatabase.LoadAssetAtPath<HitSettings>(SettingsPath);
            if (asset == null) return "No " + SettingsPath + ": put the two prefabs in your Hit Settings (Hit effect) by hand.";
            var so = new SerializedObject(asset);
            string kept = "";
            kept += Fill(so, "hitEffect", pop);
            kept += Fill(so, "bigHitEffect", big);
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            return "Hit Settings: hit effects set." + kept;
        }

        private static string Fill(SerializedObject so, string field, GameObject prefab)
        {
            var p = so.FindProperty(field);
            if (p == null) return $" (no field {field}!)";
            var now = p.objectReferenceValue as GameObject;
            if (now != null && now != prefab && AssetDatabase.GetAssetPath(now).StartsWith(Folder) == false)
                return $" {field} kept as {now.name} (set by hand).";
            p.objectReferenceValue = prefab;
            return "";
        }

        // ---------- building blocks ----------

        private static GameObject Save(GameObject root, string name)
        {
            try { return PrefabUtility.SaveAsPrefabAsset(root, $"{Folder}/{name}.prefab"); }
            finally { Object.DestroyImmediate(root); }
        }

        private static ParticleSystem Child(GameObject root, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            return go.AddComponent<ParticleSystem>();
        }

        private static void Main(ParticleSystem ps, (float, float) life, (float, float) size, (float, float) speed,
            (Color, Color) color, float gravity = 0f, bool rotate = true)
        {
            var main = ps.main;
            main.duration = 0.45f;
            main.loop = false;
            main.playOnAwake = true;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            // Local: the whole pop grows and shrinks with its size (the pool never moves a copy while it plays).
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life.Item1, life.Item2);
            main.startSize = new ParticleSystem.MinMaxCurve(size.Item1, size.Item2);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.Item1, speed.Item2);
            main.startColor = new ParticleSystem.MinMaxGradient(color.Item1, color.Item2);
            main.startRotation = rotate ? new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f) : new ParticleSystem.MinMaxCurve(0f);
            main.gravityModifier = gravity;
            main.maxParticles = 16;
            main.stopAction = ParticleSystemStopAction.None;
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new ParticleSystem.Burst[0]);
        }

        private static void Burst(ParticleSystem ps, int count)
        {
            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
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

        // Circles_AB, one of the sprites at random, facing the eyes.
        private static void Look(ParticleSystem ps, List<Sprite> sprites)
        {
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = material;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.maxParticleSize = MaxScreen;
            r.minParticleSize = 0f;
            r.sortMode = ParticleSystemSortMode.None;
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

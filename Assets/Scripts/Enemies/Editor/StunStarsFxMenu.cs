using System.Collections.Generic;
using System.Linq;
using Game.Combat;
using Game.VFX;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Enemies > Build Stun Stars FX (user, 2026-10-27): the dizzy stars over a stunned mini boss or
    /// the dizzy pumpkin boss, made from the game's own cartoon sprites (Lana Hyper Casual FX, Circles_AB material), saved
    /// as Prefabs/Enemies/Hit_FX/Stun_Stars and put in the Hit Settings (Stunned). Run it again to rebuild it (hand
    /// edits on the prefab are lost, so change the numbers here).
    /// Everything is measured in rings: Stun Stars scales the copy to the ring's radius in metres.
    /// - Ring: a faint golden circle lying level, turning slowly.
    /// - Stars: five chubby gold stars spaced round the ring, circling on it, spinning and twinkling.
    /// - Sparkles: little white-gold twinkles left along the ring behind them, fading fast.
    /// </summary>
    public static class StunStarsFxMenu
    {
        private const string Folder = "Assets/Prefabs/Enemies/Hit_FX";
        private const string SettingsPath = "Assets/Data/Combat/Hit Settings.asset";
        private const string MaterialPath = "Assets/Lana Studio/Hyper Casual FX/Materials/Circles_AB.mat";
        private const string Textures = "Assets/Lana Studio/Hyper Casual FX/Textures/";
        // Round the head in about 1.6 s (radians a second).
        private const float Orbit = 4f;
        // Particles that last as long as the stars are up (the copy is destroyed when the stun ends).
        private const float Forever = 1000f;

        private static Material material;

        [MenuItem("Tools/429 Game/Enemies/Build Stun Stars FX")]
        public static void Build()
        {
            material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null) { Debug.LogError("Stun stars FX: no " + MaterialPath); return; }
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Prefabs/Enemies", "Hit_FX");

            var root = new GameObject("Stun_Stars");
            root.AddComponent<StunStars>();
            MakeRing(root);
            MakeStars(root);
            MakeSparkles(root);
            GameObject prefab;
            try { prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{Folder}/Stun_Stars.prefab"); }
            finally { Object.DestroyImmediate(root); }

            var settings = AssetDatabase.LoadAssetAtPath<HitSettings>(SettingsPath);
            string note = "put it in your Hit Settings (Stunned) by hand";
            if (settings != null)
            {
                var so = new SerializedObject(settings);
                so.FindProperty("stunStars").objectReferenceValue = prefab;
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                note = "Hit Settings: Stun Stars set";
            }
            Debug.Log($"[Stun Stars] Built {Folder}/Stun_Stars.prefab; {note}.");
        }

        private static void MakeRing(GameObject root)
        {
            var ring = Child(root, "Ring");
            Main(ring, life: Forever, size: (3.8f, 3.8f), color: (C(1f, 0.9f, 0.55f, 0.45f), C(1f, 0.9f, 0.55f, 0.45f)), loop: false);
            Burst(ring, 1);
            Shape(ring, null);
            var spin = ring.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(1.2f);
            Look(ring, Sprites("Circle06"), ParticleSystemRenderMode.HorizontalBillboard, 0.6f);
        }

        private static void MakeStars(GameObject root)
        {
            var stars = Child(root, "Stars");
            Main(stars, life: Forever, size: (0.42f, 0.48f), color: (C(1f, 0.82f, 0.25f), C(1f, 0.93f, 0.5f)), loop: false);
            Burst(stars, 5);
            OnTheRing(stars, true);
            Circle(stars);
            var spin = stars.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-2.5f, 2.5f);
            // Twinkle: their size breathes in and out a little, each its own way (no drift: position strength 0).
            var twinkle = stars.noise;
            twinkle.enabled = true;
            twinkle.strength = 0f;
            twinkle.frequency = 1.5f;
            twinkle.scrollSpeed = 1.2f;
            twinkle.sizeAmount = new ParticleSystem.MinMaxCurve(0.35f);
            Look(stars, Sprites("Star01"), ParticleSystemRenderMode.Billboard, 0.25f);
        }

        private static void MakeSparkles(GameObject root)
        {
            var sparks = Child(root, "Sparkles");
            Main(sparks, life: 0.55f, size: (0.1f, 0.17f), color: (C(1f, 1f, 0.9f), C(1f, 0.85f, 0.4f)), loop: true, lifeMin: 0.35f);
            var emission = sparks.emission;
            emission.rateOverTime = 26f;
            OnTheRing(sparks, false);
            Circle(sparks);
            var shrink = sparks.sizeOverLifetime;
            shrink.enabled = true;
            shrink.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.2f)));
            var fade = sparks.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0f, 1f) });
            fade.color = new ParticleSystem.MinMaxGradient(g);
            Look(sparks, Sprites("Star02"), ParticleSystemRenderMode.Billboard, 0.15f);
        }

        // ---------- building blocks ----------

        private static ParticleSystem Child(GameObject root, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            return go.AddComponent<ParticleSystem>();
        }

        private static void Main(ParticleSystem ps, float life, (float, float) size, (Color, Color) color, bool loop, float lifeMin = -1f)
        {
            var main = ps.main;
            main.duration = 1f;
            main.loop = loop;
            main.playOnAwake = true;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            // Local: the ring and its stars ride along over the head.
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = lifeMin >= 0f ? new ParticleSystem.MinMaxCurve(lifeMin, life) : new ParticleSystem.MinMaxCurve(life);
            main.startSize = new ParticleSystem.MinMaxCurve(size.Item1, size.Item2);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f);
            main.startColor = new ParticleSystem.MinMaxGradient(color.Item1, color.Item2);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 0f;
            main.maxParticles = 40;
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

        // Born on the level ring of radius 1 (the circle shape lies flat: turned to the ground plane). Spread: evenly
        // spaced round it (the stars), or anywhere on it (the sparkles).
        private static void OnTheRing(ParticleSystem ps, bool spread)
        {
            Shape(ps, s =>
            {
                s.shapeType = ParticleSystemShapeType.Circle;
                s.radius = 1f;
                s.radiusThickness = 0f;
                s.arc = 360f;
                s.arcMode = spread ? ParticleSystemShapeMultiModeValue.BurstSpread : ParticleSystemShapeMultiModeValue.Random;
                s.rotation = new Vector3(90f, 0f, 0f);
            });
        }

        // Round and round the middle, staying on the ring.
        private static void Circle(ParticleSystem ps)
        {
            var orbit = ps.velocityOverLifetime;
            orbit.enabled = true;
            orbit.space = ParticleSystemSimulationSpace.Local;
            orbit.x = new ParticleSystem.MinMaxCurve(0f);
            orbit.y = new ParticleSystem.MinMaxCurve(0f);
            orbit.z = new ParticleSystem.MinMaxCurve(0f);
            orbit.orbitalX = new ParticleSystem.MinMaxCurve(0f);
            orbit.orbitalY = new ParticleSystem.MinMaxCurve(Orbit);
            orbit.orbitalZ = new ParticleSystem.MinMaxCurve(0f);
            orbit.radial = new ParticleSystem.MinMaxCurve(0f);
        }

        // Circles_AB with the sprites of one texture, one at random per particle. Max Screen: the most of the view one
        // particle may cover, so the stars never block it when a boss is close.
        private static void Look(ParticleSystem ps, List<Sprite> sprites, ParticleSystemRenderMode mode, float maxScreen)
        {
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = material;
            r.renderMode = mode;
            r.maxParticleSize = maxScreen;
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
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0f);
            sheet.cycleCount = 1;
        }

        private static List<Sprite> Sprites(string texture)
        {
            return AssetDatabase.LoadAllAssetsAtPath(Textures + texture + ".png").OfType<Sprite>().OrderBy(s => s.name).ToList();
        }

        private static Color C(float r, float g, float b, float a = 1f) => new(r, g, b, a);
    }
}

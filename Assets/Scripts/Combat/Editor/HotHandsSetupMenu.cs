using Game.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static UnityEngine.ParticleSystem;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Player > Hot Hands And Punch Shockwave (the user's asks, 2026-10-27):
    /// - Builds Assets/Prefabs/Player/Flying_Punch_Shockwave, the burst at a flying punch's wrist in place of the smoke
    ///   puff: a clean ring snapping outwards round the arm, across the punch's path (a sonic boom), a faint spiky ring
    ///   inside it, a second ring left trailing behind, and air streaks rushing back past the wrist. Its "Star" flash
    ///   shows only with Explosive Fist points (as the smoke's star did). See-through rings, so nothing blocks the view.
    ///   Made in metres; the fists play it at Wrist Smoke Size 1 (bigger flying punches, bigger rings).
    /// - Builds Hot_Hands_Embers: sparks rising off each hand and a faint red heat glow round it, while the hands are hot.
    /// - In the open scene: both fists' Wrist Smoke becomes the shockwave (size 1), and the XR Origin gets Hot Hands
    ///   (5 kills in 3 s overheat the hands for 3 s, +50% damage) with the embers and the whoosh. The scene is saved if
    ///   it had nothing else unsaved; otherwise it's left for you to save.
    /// Running it again rebuilds both prefabs and their materials (Assets/Data/Effects/Punch Shockwave *.mat, Hot Hands
    /// *.mat).
    /// </summary>
    public static class HotHandsSetupMenu
    {
        private const string ShockwavePath = "Assets/Prefabs/Player/Flying_Punch_Shockwave.prefab";
        private const string EmbersPath = "Assets/Prefabs/Player/Hot_Hands_Embers.prefab";
        private const string Textures = "Assets/Lana Studio/Hyper Casual FX/Textures/";
        private const string StartSound = "Assets/Audio/SFX/whoosh.wav";

        private static readonly Color Air = new(0.78f, 0.92f, 1f);
        private static readonly Color Warm = new(1f, 0.9f, 0.7f);
        private static readonly Color Yellow = new(1f, 0.82f, 0.25f);
        private static readonly Color Orange = new(1f, 0.42f, 0.08f);
        private static readonly Color Red = new(1f, 0.12f, 0.05f);

        [MenuItem("Tools/429 Game/Player/Hot Hands And Punch Shockwave")]
        public static void Run()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            bool wasClean = !scene.isDirty;
            var shockwave = BuildShockwave();
            var embers = BuildEmbers();
            var notes = SetInScene(shockwave, embers);
            AssetDatabase.SaveAssets();
            if (notes.changed)
            {
                if (wasClean && EditorSceneManager.SaveScene(scene)) notes.text += "; scene saved";
                else notes.text += "; the scene had other unsaved changes: save it yourself (Ctrl+S)";
            }
            Debug.Log("[Hot Hands] Built " + ShockwavePath + " and " + EmbersPath + "; " + notes.text);
        }

        // ---- The flying punch's shockwave (ridden along at the wrist; its Z points back along the punch) ----

        private static GameObject BuildShockwave()
        {
            var root = new GameObject("Flying_Punch_Shockwave");

            // The ring: snaps out round the arm, flat across the punch's path, and fades.
            var ring = MakeSystem(root, "Punch Shockwave Ring", "Ring", "Circle06", true, ParticleSystemRenderMode.Billboard, 2);
            OneShot(ring, 0.22f, 1, 0f);
            Across(ring);
            var main = ring.main;
            main.startSize = 0.34f;
            Grow(ring, 0.2f);
            DangerFlameSetupMenu.Fade(ring, new[] { Key(Color.white, 0f), Key(Air, 1f) }, Alpha(0.75f, 0.45f, 0f));

            // A faint spiky ring inside it, for the punch.
            var spikes = MakeSystem(root, "Punch Shockwave Spikes", "Spikes", "Shockwave_spiky", true, ParticleSystemRenderMode.Billboard, 2);
            OneShot(spikes, 0.16f, 1, 0.02f);
            Across(spikes);
            main = spikes.main;
            main.startSize = 0.26f;
            main.startRotation = new MinMaxCurve(0f, Mathf.PI * 2f);
            Grow(spikes, 0.3f);
            DangerFlameSetupMenu.Fade(spikes, new[] { Key(Color.white, 0f), Key(Warm, 1f) }, Alpha(0.45f, 0.25f, 0f));

            // A second ring a moment later, left trailing behind the fist.
            var echo = MakeSystem(root, "Punch Shockwave Ring", "Echo Ring", "Circle06", true, ParticleSystemRenderMode.Billboard, 2);
            OneShot(echo, 0.2f, 1, 0.06f);
            Across(echo);
            main = echo.main;
            main.startSize = 0.26f;
            main.startSpeed = 1.4f;
            Grow(echo, 0.25f);
            DangerFlameSetupMenu.Fade(echo, new[] { Key(Color.white, 0f), Key(Air, 1f) }, Alpha(0.4f, 0.22f, 0f));

            // Air streaks rushing back past the wrist.
            var streaks = MakeSystem(root, "Punch Shockwave Streak", "Streaks", "spark01", true, ParticleSystemRenderMode.Stretch, 10);
            OneShot(streaks, 0.14f, 6, 0f);
            main = streaks.main;
            main.startLifetime = new MinMaxCurve(0.09f, 0.14f);
            main.startSpeed = new MinMaxCurve(2f, 3.5f);
            main.startSize = new MinMaxCurve(0.006f, 0.01f);
            main.startColor = new MinMaxGradient(Color.white, Air);
            var shape = streaks.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = 0.035f;
            var renderer = streaks.GetComponent<ParticleSystemRenderer>();
            renderer.velocityScale = 0.03f;
            renderer.lengthScale = 1.2f;
            DangerFlameSetupMenu.Shrink(streaks, 1f, 1f, 0.3f);

            // The Explosive Fist star (shown only with its points): a quick white flash at the wrist.
            var star = MakeSystem(root, "Punch Shockwave Star", "Star", "Flare02", true, ParticleSystemRenderMode.Billboard, 2);
            OneShot(star, 0.1f, 1, 0f);
            main = star.main;
            main.startSize = 0.12f;
            main.startRotation = new MinMaxCurve(0f, Mathf.PI * 2f);
            DangerFlameSetupMenu.Shrink(star, 1f, 0.9f, 0.2f);
            DangerFlameSetupMenu.Fade(star, new[] { Key(Color.white, 0f), Key(Yellow, 1f) }, Alpha(0.9f, 0.8f, 0f));

            var saved = PrefabUtility.SaveAsPrefabAsset(root, ShockwavePath);
            Object.DestroyImmediate(root);
            return saved;
        }

        // ---- The embers off a hot hand (made under the hand's model; they stay where they rise) ----

        private static GameObject BuildEmbers()
        {
            var root = new GameObject("Hot_Hands_Embers");

            var embers = MakeSystem(root, "Hot Hands Ember", "Embers", "spark01", true, ParticleSystemRenderMode.Stretch, 40);
            Looping(embers, 26f);
            var main = embers.main;
            main.startLifetime = new MinMaxCurve(0.35f, 0.6f);
            main.startSpeed = new MinMaxCurve(0.2f, 0.6f);
            main.startSize = new MinMaxCurve(0.006f, 0.012f);
            main.gravityModifier = -0.25f;
            main.startColor = new MinMaxGradient(Yellow, Orange);
            Sphere(embers, 0.06f);
            var renderer = embers.GetComponent<ParticleSystemRenderer>();
            renderer.velocityScale = 0.04f;
            renderer.lengthScale = 1.4f;
            DangerFlameSetupMenu.Fade(embers, new[] { Key(Yellow, 0f), Key(Orange, 0.5f), Key(Red, 1f) }, Alpha(1f, 0.85f, 0f));
            DangerFlameSetupMenu.Shrink(embers, 1f, 1f, 0.3f);
            DangerFlameSetupMenu.Wobble(embers, 0.25f, 2f);

            var heat = MakeSystem(root, "Hot Hands Glow", "Heat Glow", "Circle01", true, ParticleSystemRenderMode.Billboard, 10);
            Looping(heat, 7f);
            main = heat.main;
            main.startLifetime = new MinMaxCurve(0.25f, 0.35f);
            main.startSize = new MinMaxCurve(0.12f, 0.17f);
            main.startColor = new MinMaxGradient(Orange, Red);
            Sphere(heat, 0.03f);
            DangerFlameSetupMenu.Fade(heat, new[] { Key(Orange, 0f), Key(Red, 1f) }, Alpha(0f, 0.28f, 0f));

            var saved = PrefabUtility.SaveAsPrefabAsset(root, EmbersPath);
            Object.DestroyImmediate(root);
            return saved;
        }

        // ---- In the scene ----

        private static (bool changed, string text) SetInScene(GameObject shockwave, GameObject embers)
        {
            bool changed = false;
            var text = new System.Collections.Generic.List<string>();
            foreach (var fist in Object.FindObjectsByType<Fist>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var so = new SerializedObject(fist);
                so.FindProperty("wristSmoke").objectReferenceValue = shockwave;
                so.FindProperty("wristSmokeSize").floatValue = 1f;
                if (so.ApplyModifiedProperties()) changed = true;
                text.Add(fist.name + " shockwave");
            }
            var origin = Object.FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>(FindObjectsInactive.Include);
            if (origin == null) text.Add("no XR Origin in the open scene: add Hot Hands by hand");
            else
            {
                var hot = origin.GetComponent<HotHands>();
                if (hot == null)
                {
                    hot = Undo.AddComponent<HotHands>(origin.gameObject);
                    changed = true;
                }
                var so = new SerializedObject(hot);
                so.FindProperty("embers").objectReferenceValue = embers;
                so.FindProperty("startSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(StartSound);
                if (so.ApplyModifiedProperties()) changed = true;
                text.Add("Hot Hands on " + origin.name);
            }
            if (changed) EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            return (changed, string.Join(", ", text));
        }

        // ---- helpers ----

        private static ParticleSystem MakeSystem(GameObject root, string material, string name, string texture, bool additive,
            ParticleSystemRenderMode mode, int max)
        {
            var look = DangerFlameSetupMenu.MakeMaterial(material, Textures + texture + ".png", additive);
            var ps = DangerFlameSetupMenu.NewSystem(DangerFlameSetupMenu.Child(root, name), look, mode, max);
            ps.GetComponent<ParticleSystemRenderer>().maxParticleSize = 0.6f;
            return ps;
        }

        // One burst (at 'at' seconds), living 'seconds'; in its own space (it rides with the fist).
        private static void OneShot(ParticleSystem ps, float seconds, int count, float at)
        {
            var main = ps.main;
            main.loop = false;
            main.prewarm = false;
            main.duration = seconds + at;
            main.startLifetime = seconds;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.playOnAwake = false;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new Burst(at, (short)count) });
            var shape = ps.shape;
            shape.enabled = false;
        }

        private static void Looping(ParticleSystem ps, float rate)
        {
            var main = ps.main;
            main.loop = true;
            main.prewarm = false;
            main.duration = 1f;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            var emission = ps.emission;
            emission.rateOverTime = rate;
        }

        // Flat across its own Z (the punch's path), not facing the eyes.
        private static void Across(ParticleSystem ps)
        {
            ps.GetComponent<ParticleSystemRenderer>().alignment = ParticleSystemRenderSpace.Local;
        }

        // Size over its life: from this share of its start size, snapping out fast, easing to full.
        private static void Grow(ParticleSystem ps, float from)
        {
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, from, 0f, 4f), new Keyframe(1f, 1f, 0.2f, 0f)));
        }

        private static void Sphere(ParticleSystem ps, float radius)
        {
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
        }

        private static GradientColorKey Key(Color colour, float time) => new(colour, time);

        private static GradientAlphaKey[] Alpha(float start, float middle, float end) =>
            new[] { new GradientAlphaKey(start, 0f), new GradientAlphaKey(middle, 0.5f), new GradientAlphaKey(end, 1f) };
    }
}

using Game.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static UnityEngine.ParticleSystem;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Player > Punchier Fist Bump: builds Fist_Bump_Impact (Assets/Prefabs/Player), a hard, comic hit
    /// where the fists meet: a white-hot four-point flash, a jagged orange blast, a quick spiky shockwave, rays shooting
    /// out, spinning yellow cartoon stars, hot embers and a puff of smoke. All sizes are in metres at Size 1 and kept
    /// small (it goes off half a metre from the player's eyes); a double bump plays it twice as big. Then it puts it on
    /// the XR Origin's Fist Bump with the fist_bump_punch thump under the gauntlet clank (the scene needs saving after).
    /// Running it again rebuilds the prefab and its materials (Assets/Data/Effects/Fist Bump *.mat).
    /// </summary>
    public static class FistBumpFxSetupMenu
    {
        private const string PrefabPath = "Assets/Prefabs/Player/Fist_Bump_Impact.prefab";
        private const string Textures = "Assets/Lana Studio/Hyper Casual FX/Textures/";
        private const string PunchSound = "Assets/Audio/SFX/fist_bump_punch.wav";

        private static readonly Color Hot = new(1f, 0.95f, 0.75f);
        private static readonly Color Yellow = new(1f, 0.82f, 0.2f);
        private static readonly Color Orange = new(1f, 0.45f, 0.08f);

        [MenuItem("Tools/429 Game/Player/Punchier Fist Bump")]
        public static void Run()
        {
            var prefab = Build();
            string result = SetOnXrOrigin(prefab);
            AssetDatabase.SaveAssets();
            Debug.Log("[Fist Bump] Built " + PrefabPath + "; " + result);
        }

        private static GameObject Build()
        {
            var root = new GameObject("Fist_Bump_Impact");

            // A white-hot four-point flash, gone in a tenth of a second.
            var flash = MakeSystem(root, "Flash", "Flare02", true, ParticleSystemRenderMode.Billboard, 2);
            OneShot(flash, 0.1f, 1);
            var main = flash.main;
            main.startSize = 0.3f;
            main.startRotation = new MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = Color.white;
            DangerFlameSetupMenu.Shrink(flash, 1f, 0.9f, 0.15f);
            DangerFlameSetupMenu.Fade(flash, new[] { Key(Color.white, 0f), Key(Yellow, 1f) }, Alpha(1f, 1f, 0f));

            // A jagged blast, bursting out and burning from yellow to orange.
            var burst = MakeSystem(root, "Burst", "Blast_spiky", true, ParticleSystemRenderMode.Billboard, 2);
            OneShot(burst, 0.18f, 1);
            main = burst.main;
            main.startSize = 0.32f;
            main.startRotation = new MinMaxCurve(0f, Mathf.PI * 2f);
            DangerFlameSetupMenu.Shrink(burst, 0.5f, 1.15f, 1.25f);
            DangerFlameSetupMenu.Fade(burst, new[] { Key(Hot, 0f), Key(Yellow, 0.35f), Key(Orange, 1f) }, Alpha(1f, 0.9f, 0f));

            // A spiky ring snapping outwards.
            var ring = MakeSystem(root, "Shockwave", "Shockwave_spiky", true, ParticleSystemRenderMode.Billboard, 2);
            OneShot(ring, 0.22f, 1);
            main = ring.main;
            main.startSize = 0.55f;
            main.startRotation = new MinMaxCurve(0f, Mathf.PI * 2f);
            var size = ring.sizeOverLifetime;
            size.enabled = true;
            size.size = new MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.15f, 0f, 3f), new Keyframe(1f, 1f, 0.3f, 0f)));
            DangerFlameSetupMenu.Fade(ring, new[] { Key(Hot, 0f), Key(Yellow, 1f) }, Alpha(0.9f, 0.6f, 0f));

            // Rays: fast streaks shooting out in every direction.
            var rays = MakeSystem(root, "Rays", "spark01", true, ParticleSystemRenderMode.Stretch, 24);
            OneShot(rays, 0.14f, 16);
            main = rays.main;
            main.startLifetime = new MinMaxCurve(0.07f, 0.12f);
            main.startSpeed = new MinMaxCurve(3.5f, 6f);
            main.startSize = new MinMaxCurve(0.014f, 0.022f);
            main.startColor = new MinMaxGradient(Color.white, Yellow);
            Sphere(rays, 0.02f);
            Stretch(rays, 0.025f, 1.5f);
            DangerFlameSetupMenu.Shrink(rays, 1f, 1f, 0.2f);

            // Cartoon stars spinning out of the hit.
            var stars = MakeSystem(root, "Stars", "Star02", false, ParticleSystemRenderMode.Billboard, 10);
            OneShot(stars, 0.55f, 6);
            main = stars.main;
            main.startLifetime = new MinMaxCurve(0.4f, 0.55f);
            main.startSpeed = new MinMaxCurve(1.2f, 2.2f);
            main.startSize = new MinMaxCurve(0.055f, 0.085f);
            main.startRotation = new MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 0.35f;
            main.startColor = new MinMaxGradient(Yellow, new Color(1f, 0.92f, 0.45f));
            Sphere(stars, 0.03f);
            var spin = stars.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new MinMaxCurve(-12f, 12f);
            DangerFlameSetupMenu.Shrink(stars, 0.6f, 1.2f, 0f);

            // Hot embers flying off and falling.
            var embers = MakeSystem(root, "Embers", "spark01", true, ParticleSystemRenderMode.Stretch, 16);
            OneShot(embers, 0.45f, 9);
            main = embers.main;
            main.startLifetime = new MinMaxCurve(0.25f, 0.45f);
            main.startSpeed = new MinMaxCurve(1.5f, 4f);
            main.startSize = new MinMaxCurve(0.008f, 0.014f);
            main.gravityModifier = 1.2f;
            main.startColor = new MinMaxGradient(Yellow, Orange);
            Sphere(embers, 0.03f);
            Stretch(embers, 0.04f, 1.5f);
            DangerFlameSetupMenu.Shrink(embers, 1f, 1f, 0f);

            // A little puff of smoke left hanging where they met.
            var puff = MakeSystem(root, "Puff", "Dust01", false, ParticleSystemRenderMode.Billboard, 6);
            OneShot(puff, 0.4f, 2);
            main = puff.main;
            main.startLifetime = new MinMaxCurve(0.3f, 0.4f);
            main.startSpeed = new MinMaxCurve(0.15f, 0.35f);
            main.startSize = new MinMaxCurve(0.08f, 0.12f);
            main.startRotation = new MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.75f, 0.72f, 0.7f);
            Sphere(puff, 0.03f);
            DangerFlameSetupMenu.Shrink(puff, 0.6f, 1f, 1.4f);
            DangerFlameSetupMenu.Fade(puff, new[] { Key(Color.white, 0f), Key(Color.white, 1f) }, Alpha(0.22f, 0.15f, 0f));

            var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return saved;
        }

        // The XR Origin's Fist Bump plays it at Size 1 (a double bump at 2) with the punch thump under the clank.
        private static string SetOnXrOrigin(GameObject prefab)
        {
            var bump = Object.FindAnyObjectByType<FistBump>(FindObjectsInactive.Include);
            if (bump == null) return "no Fist Bump in the open scene: set Bump Effect by hand";
            var so = new SerializedObject(bump);
            so.FindProperty("bumpEffect").objectReferenceValue = prefab;
            so.FindProperty("bumpEffectSize").floatValue = 1f;
            so.FindProperty("punchSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(PunchSound);
            so.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(bump.gameObject.scene);
            return "set on " + bump.name + " (save the scene)";
        }

        // ---- helpers ----

        private static ParticleSystem MakeSystem(GameObject root, string name, string texture, bool additive,
            ParticleSystemRenderMode mode, int max)
        {
            var material = DangerFlameSetupMenu.MakeMaterial("Fist Bump " + name, Textures + texture + ".png", additive);
            var ps = DangerFlameSetupMenu.NewSystem(DangerFlameSetupMenu.Child(root, name), material, mode, max);
            ps.GetComponent<ParticleSystemRenderer>().maxParticleSize = 0.5f;
            return ps;
        }

        private static void OneShot(ParticleSystem ps, float seconds, int count)
        {
            var main = ps.main;
            main.loop = false;
            main.prewarm = false;
            main.duration = seconds;
            main.startLifetime = seconds;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new Burst(0f, (short)count) });
            var shape = ps.shape;
            shape.enabled = false;
        }

        private static void Sphere(ParticleSystem ps, float radius)
        {
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
        }

        private static void Stretch(ParticleSystem ps, float velocityScale, float lengthScale)
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.velocityScale = velocityScale;
            renderer.lengthScale = lengthScale;
        }

        private static GradientColorKey Key(Color colour, float time) => new(colour, time);

        private static GradientAlphaKey[] Alpha(float start, float middle, float end) =>
            new[] { new GradientAlphaKey(start, 0f), new GradientAlphaKey(middle, 0.5f), new GradientAlphaKey(end, 1f) };
    }
}

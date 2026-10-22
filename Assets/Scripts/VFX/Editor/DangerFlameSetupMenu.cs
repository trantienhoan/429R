using System.Collections.Generic;
using Game.VFX;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using static UnityEngine.ParticleSystem;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > VFX > Make Danger Flames In Boss Area: builds the Danger_Flame prefab (Assets/Prefabs/Light), a
    /// big fire you can see from far: a bright core, a red outer flame, rising embers, dark smoke, a glow on the ground and
    /// a flickering light. All unlit, so it glows in the dark rooms. Then it swaps Boss_Area's four corner CandleFlames
    /// for Danger_Flames standing on the floor where they were (Bonus_Area, its variant, gets them too), sized to undo
    /// Boss_Area's stretched root. Running it again rebuilds the prefab and materials (Assets/Data/Effects/Danger *.mat)
    /// and only re-sizes the flames already in Boss_Area.
    /// </summary>
    public static class DangerFlameSetupMenu
    {
        private const string PrefabPath = "Assets/Prefabs/Light/Danger_Flame.prefab";
        private const string BossAreaPath = "Assets/Prefabs/Bases/Boss_Area.prefab";
        private const string EffectsFolder = "Assets/Data/Effects/";
        private const string FlameTexture = "Assets/XRI_Examples/Props/Textures/CandleFlame.png";
        private const string GlowTexture = "Assets/Lana Studio/Hyper Casual FX/Textures/Circle01.png";
        private const string SmokeTexture = "Assets/Lana Studio/Hyper Casual FX/Textures/Dust01.png";
        private const string OldFlameName = "CandleFlame";

        [MenuItem("Tools/429 Game/VFX/Make Danger Flames In Boss Area")]
        public static void Run()
        {
            var notes = new List<string>();
            var prefab = BuildPrefab();
            notes.Add("built " + PrefabPath);
            notes.Add(SwapBossAreaFlames(prefab));
            AssetDatabase.SaveAssets();
            Debug.Log("[Danger Flame] " + string.Join("; ", notes));
        }

        private static GameObject BuildPrefab()
        {
            var flameMat = MakeMaterial("Danger Flame", FlameTexture, true);
            var glowMat = MakeMaterial("Danger Glow", GlowTexture, true);
            var smokeMat = MakeMaterial("Danger Smoke", SmokeTexture, false);

            var root = new GameObject("Danger_Flame");
            try
            {
                Core(Child(root, "Flame Core"), flameMat);
                Outer(Child(root, "Flame Outer"), flameMat);
                Embers(Child(root, "Embers"), glowMat);
                Smoke(Child(root, "Smoke"), smokeMat);
                GroundGlow(Child(root, "Ground Glow"), glowMat);

                var lightObject = Child(root, "Fire Light");
                lightObject.transform.localPosition = new Vector3(0f, 0.9f, 0f);
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = 9f;
                light.intensity = 9f;
                light.color = new Color(1f, 0.42f, 0.16f);
                light.shadows = LightShadows.None;
                light.lightmapBakeType = LightmapBakeType.Realtime;
                lightObject.AddComponent<FlameFlicker>();

                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // The bright heart of the fire: yellow-white at the bottom, orange, then deep red as it rises and shrinks.
        private static void Core(GameObject go, Material material)
        {
            var ps = NewSystem(go, material, ParticleSystemRenderMode.VerticalBillboard, 60);
            var main = ps.main;
            main.startLifetime = new MinMaxCurve(0.7f, 1.0f);
            main.startSize = new MinMaxCurve(0.7f, 1.0f);
            main.startRotation = new MinMaxCurve(-0.25f, 0.25f);
            var emission = ps.emission;
            emission.rateOverTime = 32f;
            Disc(ps, 0.16f, 0f);
            Rise(ps, 1.8f, 2.4f, 0.05f);
            Fade(ps, new[] { Key(1f, 0.95f, 0.65f, 0f), Key(1f, 0.62f, 0.16f, 0.25f), Key(0.95f, 0.22f, 0.03f, 0.6f), Key(0.45f, 0.03f, 0f, 1f) },
                 new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.85f, 0.6f), new GradientAlphaKey(0f, 1f) });
            Shrink(ps, 0.7f, 1f, 0.25f);
            Wobble(ps, 0.2f, 1.5f);
        }

        // A wider, darker red flame round the core, licking higher.
        private static void Outer(GameObject go, Material material)
        {
            var ps = NewSystem(go, material, ParticleSystemRenderMode.VerticalBillboard, 40);
            var main = ps.main;
            main.startLifetime = new MinMaxCurve(0.8f, 1.1f);
            main.startSize = new MinMaxCurve(1.1f, 1.5f);
            main.startRotation = new MinMaxCurve(-0.35f, 0.35f);
            var emission = ps.emission;
            emission.rateOverTime = 15f;
            Disc(ps, 0.28f, 0f);
            Rise(ps, 1.3f, 1.8f, 0.06f);
            Fade(ps, new[] { Key(1f, 0.45f, 0.1f, 0f), Key(0.85f, 0.12f, 0.02f, 0.5f), Key(0.3f, 0.01f, 0f, 1f) },
                 new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.6f, 0.15f), new GradientAlphaKey(0.4f, 0.6f), new GradientAlphaKey(0f, 1f) });
            Shrink(ps, 0.8f, 1f, 0.4f);
            Wobble(ps, 0.25f, 1.1f);
            ps.GetComponent<ParticleSystemRenderer>().sortingFudge = 5f;
        }

        // Sparks spitting up and swirling away.
        private static void Embers(GameObject go, Material material)
        {
            var ps = NewSystem(go, material, ParticleSystemRenderMode.Billboard, 40);
            var main = ps.main;
            main.startLifetime = new MinMaxCurve(1.0f, 2.0f);
            main.startSize = new MinMaxCurve(0.03f, 0.07f);
            var emission = ps.emission;
            emission.rateOverTime = 14f;
            Disc(ps, 0.3f, 0.2f);
            Rise(ps, 1.5f, 3.0f, 0.35f);
            Fade(ps, new[] { Key(1f, 0.85f, 0.4f, 0f), Key(1f, 0.4f, 0.08f, 0.5f), Key(0.8f, 0.1f, 0f, 1f) },
                 new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            Wobble(ps, 0.8f, 2f);
        }

        // Dark smoke rolling up out of the top of the fire.
        private static void Smoke(GameObject go, Material material)
        {
            var ps = NewSystem(go, material, ParticleSystemRenderMode.Billboard, 20);
            var main = ps.main;
            main.startLifetime = new MinMaxCurve(2.5f, 3.5f);
            main.startSize = new MinMaxCurve(0.6f, 0.9f);
            main.startRotation = new MinMaxCurve(0f, Mathf.PI * 2f);
            var emission = ps.emission;
            emission.rateOverTime = 3f;
            Disc(ps, 0.2f, 1.8f);
            Rise(ps, 0.7f, 1.1f, 0.15f);
            Fade(ps, new[] { Key(0.16f, 0.05f, 0.04f, 0f), Key(0.06f, 0.05f, 0.05f, 1f) },
                 new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.45f, 0.2f), new GradientAlphaKey(0f, 1f) });
            Shrink(ps, 1f, 1.6f, 2.2f);
            var spin = ps.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new MinMaxCurve(-0.5f, 0.5f);
            // Dust01 is 2 x 2 puffs: each smoke particle picks one.
            var sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.numTilesX = 2;
            sheet.numTilesY = 2;
            sheet.startFrame = new MinMaxCurve(0f, 3.99f);
            sheet.frameOverTime = new MinMaxCurve(0f);
            // Drawn behind the flames.
            ps.GetComponent<ParticleSystemRenderer>().sortingFudge = 20f;
        }

        // A red glow pulsing on the floor under the fire.
        private static void GroundGlow(GameObject go, Material material)
        {
            go.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            var ps = NewSystem(go, material, ParticleSystemRenderMode.HorizontalBillboard, 4);
            var main = ps.main;
            main.startLifetime = 1.4f;
            main.startSize = new MinMaxCurve(2.6f, 3.2f);
            var emission = ps.emission;
            emission.rateOverTime = 2f;
            var shape = ps.shape;
            shape.enabled = false;
            Fade(ps, new[] { Key(1f, 0.3f, 0.06f, 0f), Key(1f, 0.2f, 0.03f, 1f) },
                 new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.45f, 0.5f), new GradientAlphaKey(0f, 1f) });
            ps.GetComponent<ParticleSystemRenderer>().maxParticleSize = 10f;
        }

        // ---- helpers (Barrel Seal uses them too) ----

        internal static GameObject Child(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        internal static ParticleSystem NewSystem(GameObject go, Material material, ParticleSystemRenderMode mode, int max)
        {
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 2f;
            main.loop = true;
            main.prewarm = true;
            main.startSpeed = 0f;
            main.startColor = Color.white;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = mode;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.maxParticleSize = 3f;
            return ps;
        }

        // Particles start on a flat disc (radius, at height y).
        private static void Disc(ParticleSystem ps, float radius, float y)
        {
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.rotation = new Vector3(90f, 0f, 0f);
            shape.position = new Vector3(0f, y, 0f);
        }

        // Upward speed (min..max m/s) with a little sideways drift.
        private static void Rise(ParticleSystem ps, float min, float max, float drift)
        {
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new MinMaxCurve(-drift, drift);
            velocity.y = new MinMaxCurve(min, max);
            velocity.z = new MinMaxCurve(-drift, drift);
        }

        internal static void Fade(ParticleSystem ps, GradientColorKey[] colours, GradientAlphaKey[] alphas)
        {
            var gradient = new Gradient();
            gradient.SetKeys(colours, alphas);
            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            colour.color = new MinMaxGradient(gradient);
        }

        internal static GradientColorKey Key(float r, float g, float b, float time) => new(new Color(r, g, b), time);

        // Size over its life: start, peak at 20%, end (shares of the start size).
        internal static void Shrink(ParticleSystem ps, float start, float peak, float end)
        {
            float top = Mathf.Max(start, peak, end);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new MinMaxCurve(top, new AnimationCurve(new Keyframe(0f, start / top), new Keyframe(0.2f, peak / top), new Keyframe(1f, end / top)));
        }

        internal static void Wobble(ParticleSystem ps, float strength, float frequency)
        {
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = strength;
            noise.frequency = frequency;
            noise.scrollSpeed = 0.6f;
            noise.quality = ParticleSystemNoiseQuality.Low;
            noise.octaveCount = 1;
        }

        // An unlit URP particle material: additive (glows, adds light) or alpha-blended (smoke).
        internal static Material MakeMaterial(string name, string texturePath, bool additive)
        {
            string path = EffectsFolder + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", additive ? 2f : 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_ALPHAMODULATE_ON");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        // Boss_Area's corner CandleFlames become Danger_Flames on the floor below them.
        private static string SwapBossAreaFlames(GameObject flamePrefab)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == BossAreaPath) return "Boss_Area is open in Prefab Mode: close it and run again";
            var root = PrefabUtility.LoadPrefabContents(BossAreaPath);
            try
            {
                var old = new List<Transform>();
                foreach (Transform child in root.transform)
                {
                    if (child.name == OldFlameName && child.GetComponent<ParticleSystem>() != null) old.Add(child);
                }
                foreach (var flame in old)
                {
                    var spot = flame.localPosition;
                    int index = flame.GetSiblingIndex();
                    Object.DestroyImmediate(flame.gameObject);
                    var fresh = (GameObject)PrefabUtility.InstantiatePrefab(flamePrefab, root.transform);
                    fresh.transform.localPosition = new Vector3(spot.x, 0f, spot.z);
                    fresh.transform.SetSiblingIndex(index);
                }
                // Boss_Area's root is stretched (about 0.69 x 1.74 x 0.72): each flame undoes that, so it's its true size
                // and shape.
                var area = root.transform.localScale;
                var trueSize = new Vector3(1f / area.x, 1f / area.y, 1f / area.z);
                int flames = 0;
                foreach (Transform child in root.transform)
                {
                    if (child.name != flamePrefab.name) continue;
                    child.localScale = trueSize;
                    flames++;
                }
                if (flames == 0) return "no CandleFlames or Danger_Flames in Boss_Area";
                PrefabUtility.SaveAsPrefabAsset(root, BossAreaPath);
                return "Boss_Area: " + old.Count + " CandleFlames swapped; " + flames + " Danger_Flames at true size";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}

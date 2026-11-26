using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.Story.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Story > Seeds Into Seed Pieces (user 2026-11-26: "turn all the yellow seed to A piece of the
    /// Seed, change its aura particle to light green, not as strong as the Seed"). Re-runnable; prefabs + settings only.
    /// The room chest seeds (Seed_Bracer, Seed_Bracer_Cauldron, Seed_Shield, Seed_Head, Seed_Chest, Seed_Boot: the Yellow
    /// Seed item) get their aura from the Seed itself (The_Seed's GrowthParticles, stars, circles and shadow), made lighter
    /// (Lighten toward white), weaker (Strength of its see-through) and smaller (Size): pieces of it, not the whole.
    /// Everything is worked out from The_Seed each time, so running it again gives the same. Story Seed Settings' main-game
    /// glow becomes the same light green.
    /// </summary>
    public static class StorySeedPiecesMenu
    {
        private const string Tag = "[Seed Pieces]";
        private const string Folder = "Assets/Prefabs/Items/Chest/";
        private const string WholeSeed = "The_Seed";
        private static readonly string[] Pieces = { "Seed_Bracer", "Seed_Bracer_Cauldron", "Seed_Shield", "Seed_Head", "Seed_Chest", "Seed_Boot" };
        private static readonly string[] Parts = { "GrowthParticles", "stars", "circles", "shadow" };
        private const string SettingsPath = "Assets/Resources/Story Seed Settings.asset";

        // A piece next to the whole Seed: this much lighter, this strong, this big.
        private const float Lighten = 0.35f;
        private const float Strength = 0.6f;
        private const float Size = 0.75f;
        private static readonly Color MainGameGlow = new Color(0.72f, 1f, 0.62f, 1f);

        [MenuItem("Tools/429 Game/Story/Seeds Into Seed Pieces")]
        public static void Run()
        {
            var whole = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + WholeSeed + ".prefab");
            if (whole == null)
            {
                Debug.LogError($"{Tag} {Folder}{WholeSeed}.prefab is missing; nothing changed.");
                return;
            }
            var sources = new Dictionary<string, ParticleSystem>();
            foreach (var ps in whole.GetComponentsInChildren<ParticleSystem>(true))
                if (System.Array.IndexOf(Parts, ps.name) >= 0 && !sources.ContainsKey(ps.name)) sources[ps.name] = ps;

            var done = new List<string>();
            foreach (var name in Pieces)
            {
                string path = Folder + name + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int changed = 0;
                    foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        if (!sources.TryGetValue(ps.name, out var source)) continue;
                        Copy(source, ps);
                        changed++;
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    done.Add($"{name} ({changed} parts)");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            var settings = AssetDatabase.LoadAssetAtPath<ScriptableObject>(SettingsPath);
            if (settings != null)
            {
                var so = new SerializedObject(settings);
                var glow = so.FindProperty("glow");
                if (glow != null)
                {
                    glow.colorValue = MainGameGlow;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    done.Add("Story Seed Settings glow light green");
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"{Tag} {string.Join("; ", done)}.");
        }

        [MenuItem("Tools/429 Game/Story/Seeds Into Seed Pieces", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

        // The whole Seed's colours and size for this part, as a piece: lighter, weaker, smaller.
        private static void Copy(ParticleSystem source, ParticleSystem target)
        {
            var from = source.main;
            var main = target.main;
            main.startColor = Piece(from.startColor);
            var start = from.startSize;
            main.startSize = start.mode == ParticleSystemCurveMode.TwoConstants
                ? new ParticleSystem.MinMaxCurve(start.constantMin * Size, start.constantMax * Size)
                : start.mode == ParticleSystemCurveMode.Constant ? new ParticleSystem.MinMaxCurve(start.constant * Size) : start;
            var overLife = target.colorOverLifetime;
            var sourceOverLife = source.colorOverLifetime;
            overLife.enabled = sourceOverLife.enabled;
            if (sourceOverLife.enabled) overLife.color = Piece(sourceOverLife.color);
        }

        private static ParticleSystem.MinMaxGradient Piece(ParticleSystem.MinMaxGradient colors)
        {
            switch (colors.mode)
            {
                case ParticleSystemGradientMode.Color:
                    return new ParticleSystem.MinMaxGradient(Piece(colors.color));
                case ParticleSystemGradientMode.TwoColors:
                    return new ParticleSystem.MinMaxGradient(Piece(colors.colorMin), Piece(colors.colorMax));
                case ParticleSystemGradientMode.Gradient:
                    return new ParticleSystem.MinMaxGradient(Piece(colors.gradient));
                case ParticleSystemGradientMode.TwoGradients:
                    return new ParticleSystem.MinMaxGradient(Piece(colors.gradientMin), Piece(colors.gradientMax));
                default:
                    return colors;
            }
        }

        private static Color Piece(Color color)
        {
            var light = Color.Lerp(color, Color.white, Lighten);
            light.a = color.a * Strength;
            return light;
        }

        private static Gradient Piece(Gradient gradient)
        {
            var g = new Gradient { mode = gradient.mode };
            var colours = gradient.colorKeys;
            for (int i = 0; i < colours.Length; i++)
            {
                var c = Color.Lerp(colours[i].color, Color.white, Lighten);
                colours[i].color = c;
            }
            var alphas = gradient.alphaKeys;
            for (int i = 0; i < alphas.Length; i++) alphas[i].alpha *= Strength;
            g.SetKeys(colours, alphas);
            return g;
        }
    }
}

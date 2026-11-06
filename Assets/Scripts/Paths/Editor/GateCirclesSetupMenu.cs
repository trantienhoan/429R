using System.Collections.Generic;
using Game.Paths;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Paths > Set Up Themed Gate Circles (user, 2026-11-06). Each rooftop gate's circle (the Path Gate
    /// a Seed Lock opens on the floor) shows what's behind it: copies of the blue Area_circles_blue (same shape and size),
    /// recoloured: gate 1 the cooking PlayGround, soup green with orange sparks; gate 2 the Queen Bee bowling, honey
    /// gold; gate 3 the slime punching, rainbow (every bit its own colour). Stone_Cube_Dice 1/2/3's Gate Effect points at
    /// them. Not Area_star_ellow: its tall glow is the Fairy light the user found annoying. Safe to run again.
    /// </summary>
    public static class GateCirclesSetupMenu
    {
        private const string Source = "Assets/Lana Studio/Hyper Casual FX/Prefabs/Area/Area_circles_blue.prefab";
        private const string Folder = "Assets/Prefabs/Effects/Gates";

        private struct Theme
        {
            public string name, dice;
            public Color disc, ring, bigBits, smallBits;
            public bool rainbow;
        }

        private static readonly Theme[] Themes =
        {
            new Theme { name = "Gate_Circle_Cooking", dice = "Assets/Prefabs/Items/Stone_Cube_Dice 1.prefab",
                disc = new Color(0.45f, 0.95f, 0.25f, 0.7f), ring = new Color(0.6f, 1f, 0.2f, 1f), bigBits = new Color(1f, 0.6f, 0.15f, 0.45f), smallBits = new Color(0.85f, 1f, 0.4f, 0.7f) },
            new Theme { name = "Gate_Circle_Bowling", dice = "Assets/Prefabs/Items/Stone_Cube_Dice 2.prefab",
                disc = new Color(1f, 0.72f, 0.15f, 0.7f), ring = new Color(1f, 0.85f, 0.3f, 1f), bigBits = new Color(1f, 0.95f, 0.6f, 0.45f), smallBits = new Color(1f, 0.65f, 0.1f, 0.7f) },
            new Theme { name = "Gate_Circle_Slime", dice = "Assets/Prefabs/Items/Stone_Cube_Dice 3.prefab", rainbow = true,
                disc = new Color(1f, 1f, 1f, 0.6f), ring = new Color(1f, 1f, 1f, 1f), bigBits = new Color(1f, 1f, 1f, 0.45f), smallBits = new Color(1f, 1f, 1f, 0.7f) },
        };

        [MenuItem("Tools/429 Game/Paths/Set Up Themed Gate Circles")]
        public static void Menu()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[Gate Circles] Stop play mode first."); return; }
            Debug.Log("[Gate Circles] " + string.Join("\n", Run()));
        }

        public static List<string> Run()
        {
            var notes = new List<string>();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
            if (source == null) { notes.Add("No " + Source); return notes; }
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Prefabs/Effects", "Gates");
            foreach (var theme in Themes)
            {
                var path = Folder + "/" + theme.name + ".prefab";
                var made = (GameObject)PrefabUtility.InstantiatePrefab(source);
                try
                {
                    made.name = theme.name;
                    foreach (var ps in made.GetComponentsInChildren<ParticleSystem>(true)) Recolour(ps, theme);
                    PrefabUtility.SaveAsPrefabAsset(made, path);
                }
                finally { Object.DestroyImmediate(made); }
                var effect = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var dice = PrefabUtility.LoadPrefabContents(theme.dice);
                try
                {
                    var seedLock = dice.GetComponent<SeedLock>();
                    var so = new SerializedObject(seedLock);
                    so.FindProperty("gateEffect").objectReferenceValue = effect;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(dice, theme.dice);
                    notes.Add(System.IO.Path.GetFileNameWithoutExtension(theme.dice) + " (" + so.FindProperty("gateBool").stringValue + "): " + theme.name);
                }
                finally { PrefabUtility.UnloadPrefabContents(dice); }
            }
            return notes;
        }

        // The disc, the ring and the two kinds of bits in the theme's colours; their fade keeps its alpha but goes white,
        // so the start colour shows. Rainbow: every particle a random colour round the rainbow.
        private static void Recolour(ParticleSystem ps, Theme theme)
        {
            var main = ps.main;
            string n = ps.name;
            Color want = n.StartsWith("Area_") ? theme.disc : n == "circle" ? theme.ring : n.Contains("large") ? theme.bigBits : theme.smallBits;
            if (theme.rainbow)
            {
                var rainbow = new Gradient();
                rainbow.SetKeys(
                    new[] { new GradientColorKey(new Color(1f, 0.25f, 0.3f), 0f), new GradientColorKey(new Color(1f, 0.75f, 0.15f), 0.2f), new GradientColorKey(new Color(0.35f, 1f, 0.3f), 0.4f),
                        new GradientColorKey(new Color(0.2f, 0.75f, 1f), 0.6f), new GradientColorKey(new Color(0.6f, 0.35f, 1f), 0.8f), new GradientColorKey(new Color(1f, 0.35f, 0.85f), 1f) },
                    new[] { new GradientAlphaKey(want.a, 0f), new GradientAlphaKey(want.a, 1f) });
                main.startColor = new ParticleSystem.MinMaxGradient(rainbow) { mode = ParticleSystemGradientMode.RandomColor };
            }
            else main.startColor = want;
            var fade = ps.colorOverLifetime;
            if (fade.enabled)
            {
                var g = fade.color.gradient;
                if (g != null)
                {
                    var white = new Gradient();
                    white.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, g.alphaKeys);
                    fade.color = new ParticleSystem.MinMaxGradient(white);
                }
            }
        }
    }
}

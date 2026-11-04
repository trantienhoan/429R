using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Effects > Smoke > 11. Smoke Never Shrinks (user, 2026-11-04: "the smoke should never go scale down
    /// to the root, smoke blast should keep fly out after blasting"). The 3D puffs sit their size times the scatter pivot
    /// (0.95) away from their particle, so a puff that shrinks at the end of its life slides back into the middle of the
    /// blast. Now every 3D puff (Mesh mode with a Smoke Puff 3D material) only grows: its size curve rises to its peak as
    /// before, then keeps growing gently (After Peak Growth more by the end) so it drifts on outward, and it fades out over
    /// the end of its life instead (colour over lifetime's alpha goes to 0). The shared smokes (Prefabs/Effects/Smoke) and
    /// every prefab's own puffs first, then linked copies that still shrink by their own settings, then the open scene's
    /// (save it after). Safe to run again: a curve that doesn't shrink is left as it is.
    /// </summary>
    public static class SmokeNeverShrinkMenu
    {
        // How much bigger a puff gets from its peak to its end, as a share of its peak.
        private const float AfterPeakGrowth = 0.25f;
        // It's fully there until this share of its life, then fades to nothing at the end.
        private const float FadeFrom = 0.6f;
        // A curve ending this much under its peak counts as shrinking.
        private const float Shrinks = 0.02f;
        private const int Steps = 16;

        [MenuItem("Tools/429 Game/Effects/Smoke/11. Smoke Never Shrinks")]
        public static void Menu() => Debug.Log(Run());

        /// <summary>Every 3D puff only grows and fades out at the end. What was done, as a sentence.</summary>
        public static string Run()
        {
            if (EditorApplication.isPlaying) return "[Smoke Never Shrinks] Stop play mode first.";
            var materials = new HashSet<Material>(AssetDatabase.FindAssets("Smoke Puff 3D t:Material")
                .Select(g => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g))).Where(m => m != null));
            var paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !p.Contains("/_Recovery/")).ToList();
            // The shared smokes first, so the copies linked to them follow.
            paths = paths.OrderBy(p => p.StartsWith("Assets/Prefabs/Effects/Smoke/") ? 0 : 1).ThenBy(p => p).ToList();
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            int prefabs = 0, own = 0, copies = 0;
            var skipped = new List<string>();
            // Twice: first the puffs that are the prefab's own, then the linked copies that still shrink (their own settings).
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (var path in paths)
                {
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (asset == null || !asset.GetComponentsInChildren<ParticleSystemRenderer>(true).Any(r => IsPuff(r, materials) && Shrinking(r.GetComponent<ParticleSystem>()))) continue;
                    if (stage != null && stage.assetPath == path) { if (pass == 0) skipped.Add(asset.name); continue; }
                    var root = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        int fixedHere = 0;
                        foreach (var r in root.GetComponentsInChildren<ParticleSystemRenderer>(true))
                        {
                            if (!IsPuff(r, materials)) continue;
                            bool linked = PrefabUtility.IsPartOfPrefabInstance(r);
                            if (linked != (pass == 1)) continue;
                            if (!Fix(r.GetComponent<ParticleSystem>())) continue;
                            fixedHere++;
                            if (linked) copies++;
                            else own++;
                        }
                        if (fixedHere > 0)
                        {
                            PrefabUtility.SaveAsPrefabAsset(root, path);
                            prefabs++;
                        }
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                    }
                }
            }

            int inScene = 0;
            foreach (var r in Object.FindObjectsByType<ParticleSystemRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!r.gameObject.scene.IsValid() || !IsPuff(r, materials)) continue;
                var ps = r.GetComponent<ParticleSystem>();
                Undo.RecordObject(ps, "Smoke Never Shrinks");
                if (!Fix(ps)) continue;
                inScene++;
                EditorSceneManager.MarkSceneDirty(r.gameObject.scene);
            }
            AssetDatabase.SaveAssets();
            return $"[Smoke Never Shrinks] {own} puffs and {copies} linked copies in {prefabs} prefabs, and {inScene} in the open scene, " +
                   "now only grow (and fade out at the end)" + (inScene > 0 ? "; save the scene" : "") +
                   (skipped.Count > 0 ? $"; open in Prefab Mode, skipped: {string.Join(", ", skipped)}" : "") + ".";
        }

        private static bool IsPuff(ParticleSystemRenderer r, HashSet<Material> materials) =>
            r != null && r.renderMode == ParticleSystemRenderMode.Mesh && r.sharedMaterial != null && materials.Contains(r.sharedMaterial);

        private static bool Shrinking(ParticleSystem ps)
        {
            var grow = ps.sizeOverLifetime;
            if (!grow.enabled) return false;
            return grow.separateAxes ? Shrinking(grow.x) || Shrinking(grow.y) || Shrinking(grow.z) : Shrinking(grow.size);
        }

        private static bool Shrinking(ParticleSystem.MinMaxCurve c)
        {
            if (c.mode == ParticleSystemCurveMode.Curve) return Shrinking(c.curve);
            if (c.mode == ParticleSystemCurveMode.TwoCurves) return Shrinking(c.curveMin) || Shrinking(c.curveMax);
            return false;
        }

        private static bool Shrinking(AnimationCurve k)
        {
            if (k == null || k.length == 0) return false;
            float peak = 0f;
            for (int i = 0; i <= Steps; i++) peak = Mathf.Max(peak, k.Evaluate((float)i / Steps));
            return peak > 0f && k.Evaluate(1f) < peak * (1f - Shrinks);
        }

        /// <summary>Its size only grows (to its peak, then a little more) and it fades out at the end. True if it changed.</summary>
        public static bool Fix(ParticleSystem ps)
        {
            if (ps == null || !Shrinking(ps)) return false;
            var grow = ps.sizeOverLifetime;
            if (grow.separateAxes)
            {
                grow.x = Grown(grow.x);
                grow.y = Grown(grow.y);
                grow.z = Grown(grow.z);
            }
            else grow.size = Grown(grow.size);
            FadeOut(ps);
            return true;
        }

        private static ParticleSystem.MinMaxCurve Grown(ParticleSystem.MinMaxCurve c)
        {
            if (c.mode == ParticleSystemCurveMode.Curve) return new ParticleSystem.MinMaxCurve(c.curveMultiplier, NeverShrinks(c.curve));
            if (c.mode == ParticleSystemCurveMode.TwoCurves)
                return new ParticleSystem.MinMaxCurve(c.curveMultiplier, NeverShrinks(c.curveMin), NeverShrinks(c.curveMax));
            return c;
        }

        /// <summary>The curve up to its peak as it was, then on up, After Peak Growth more by the end: it never shrinks.</summary>
        public static AnimationCurve NeverShrinks(AnimationCurve k)
        {
            if (k == null || k.length == 0) return k;
            var values = new float[Steps + 1];
            int peakAt = 0;
            for (int i = 0; i <= Steps; i++)
            {
                values[i] = k.Evaluate((float)i / Steps);
                if (values[i] > values[peakAt]) peakAt = i;
            }
            float peak = values[peakAt];
            var grown = new AnimationCurve();
            for (int i = 0; i <= Steps; i++)
            {
                float t = (float)i / Steps;
                float v;
                if (i <= peakAt) v = i > 0 ? Mathf.Max(values[i], grown.keys[i - 1].value) : values[i];
                else v = peak * (1f + AfterPeakGrowth * (t - (float)peakAt / Steps) / Mathf.Max(0.01f, 1f - (float)peakAt / Steps));
                grown.AddKey(t, v);
            }
            for (int i = 0; i < grown.length; i++) grown.SmoothTangents(i, 0f);
            return grown;
        }

        // Fully there until Fade From of its life, then fading to nothing: it goes by fading, not by shrinking.
        private static void FadeOut(ParticleSystem ps)
        {
            var colour = ps.colorOverLifetime;
            if (!colour.enabled)
            {
                colour.enabled = true;
                colour.color = new ParticleSystem.MinMaxGradient(Fading(null));
                return;
            }
            var c = colour.color;
            if (c.mode == ParticleSystemGradientMode.Gradient) colour.color = new ParticleSystem.MinMaxGradient(Fading(c.gradient));
            else if (c.mode == ParticleSystemGradientMode.TwoGradients)
                colour.color = new ParticleSystem.MinMaxGradient(Fading(c.gradientMin), Fading(c.gradientMax));
        }

        // Its colours as they were; its see-through as it was until Fade From, then down to 0 at the end.
        private static Gradient Fading(Gradient was)
        {
            var colours = was != null ? was.colorKeys : new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) };
            var alphas = new List<GradientAlphaKey>();
            if (was != null) alphas.AddRange(was.alphaKeys.Where(a => a.time < FadeFrom).Take(6));
            if (alphas.Count == 0) alphas.Add(new GradientAlphaKey(1f, 0f));
            alphas.Add(new GradientAlphaKey(was != null ? was.Evaluate(FadeFrom).a : 1f, FadeFrom));
            alphas.Add(new GradientAlphaKey(0f, 1f));
            var g = new Gradient { mode = was != null ? was.mode : GradientMode.Blend };
            g.SetKeys(colours, alphas.ToArray());
            return g;
        }
    }
}

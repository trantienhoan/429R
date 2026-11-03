using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Effects > Chaos Pumpkin Aura (user, 2026-11-03: "Give Chaos pumpkin a red aura particle to make
    /// them look more special ... don't overdo the particle, just a little"): builds Assets/Prefabs/Effects/Chaos_Aura (a
    /// faint red glow that throbs slowly round the pumpkin, and a few red wisps drifting up off it; about ten particles,
    /// made for a pumpkin 1 m across, scaling with its parent) and puts one on each Chaos runner pumpkin (Pumpkin_1, 2, 4
    /// and 6 _Chaos), sized to its pumpkin, at its middle, under its root (not under Jiggle, so the jiggle doesn't squash
    /// it). Safe to run again: the aura is rebuilt and each pumpkin's is replaced.
    /// </summary>
    public static class ChaosAuraMenu
    {
        private const string AuraPath = "Assets/Prefabs/Effects/Chaos_Aura.prefab";
        private const string PumpkinFolder = "Assets/Prefabs/Items/Breakable/";
        private const string GlowTexture = "Assets/Lana Studio/Hyper Casual FX/Textures/Circle03.png";
        private const string AuraName = "Chaos_Aura";
        private static readonly string[] Pumpkins = { "Pumpkin_1_Chaos", "Pumpkin_2_Chaos", "Pumpkin_4_Chaos", "Pumpkin_6_Chaos" };
        private static readonly Color DeepRed = new(0.85f, 0.05f, 0.03f, 1f);
        private static readonly Color HotRed = new(1f, 0.25f, 0.08f, 1f);

        [MenuItem("Tools/429 Game/Effects/Chaos Pumpkin Aura")]
        public static void Run()
        {
            var aura = BuildAura();
            var notes = new List<string>();
            foreach (var name in Pumpkins)
            {
                string path = PumpkinFolder + name + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { notes.Add(name + ": missing"); continue; }
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                if (stage != null && stage.assetPath == path) { notes.Add(name + ": open in Prefab Mode, skipped"); continue; }
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    notes.Add(name + ": " + PutOn(root, aura));
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Chaos Aura] Built " + AuraPath + "; " + string.Join("; ", notes));
        }

        // The aura at the pumpkin's middle, as wide as it.
        private static string PutOn(GameObject root, GameObject aura)
        {
            foreach (var old in root.GetComponentsInChildren<Transform>(true).Where(t => t.name == AuraName).ToArray())
                Object.DestroyImmediate(old.gameObject);
            // The pumpkin itself (pumpkin_N), not its hat (a cowboy hat is wider than the pumpkin) or its crack spots; else
            // its biggest mesh.
            var meshes = root.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null).ToArray();
            var body = meshes.FirstOrDefault(f => f.name.StartsWith("pumpkin_") && !f.name.Contains("crack")) ??
                       meshes.OrderByDescending(f => Vector3.Scale(f.sharedMesh.bounds.size, f.transform.lossyScale).sqrMagnitude).FirstOrDefault();
            if (body == null) return "no pumpkin mesh found";
            var b = body.sharedMesh.bounds;
            var middle = root.transform.InverseTransformPoint(body.transform.TransformPoint(b.center));
            var size = Vector3.Scale(b.size, body.transform.lossyScale);
            float across = Mathf.Max(size.x, size.z);
            var made = (GameObject)PrefabUtility.InstantiatePrefab(aura, root.transform);
            made.name = AuraName;
            made.transform.localPosition = middle;
            made.transform.localRotation = Quaternion.identity;
            made.transform.localScale = Vector3.one * across / Mathf.Max(0.001f, root.transform.lossyScale.x);
            return $"aura {across:0.00} m across";
        }

        private static GameObject BuildAura()
        {
            var material = DangerFlameSetupMenu.MakeMaterial("Chaos Aura Glow", GlowTexture, true);
            var root = new GameObject(AuraName);
            try
            {
                // A faint red glow round it, throbbing slowly.
                var glow = MakeSystem(root, "Glow", material, ParticleSystemSimulationSpace.Local);
                var main = glow.main;
                main.startLifetime = 1.4f;
                main.startSpeed = 0f;
                main.startSize = 1.5f;
                main.maxParticles = 4;
                var emission = glow.emission;
                emission.rateOverTime = 1.6f;
                var shape = glow.shape;
                shape.enabled = false;
                Fade(glow, DeepRed, DeepRed, 0.28f);
                var grow = glow.sizeOverLifetime;
                grow.enabled = true;
                grow.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.85f), new Keyframe(1f, 1.1f)));

                // A few red wisps drifting up off it.
                var wisps = MakeSystem(root, "Wisps", material, ParticleSystemSimulationSpace.World);
                main = wisps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.2f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
                main.gravityModifier = -0.03f;
                main.maxParticles = 12;
                emission = wisps.emission;
                emission.rateOverTime = 6f;
                shape = wisps.shape;
                shape.shapeType = ParticleSystemShapeType.Hemisphere;
                shape.radius = 0.45f;
                shape.radiusThickness = 0.15f;
                shape.rotation = new Vector3(-90f, 0f, 0f);
                Fade(wisps, HotRed, DeepRed, 0.55f);
                grow = wisps.sizeOverLifetime;
                grow.enabled = true;
                grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.4f));

                return PrefabUtility.SaveAsPrefabAsset(root, AuraPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // A looping system that plays by itself and scales with what it's on.
        private static ParticleSystem MakeSystem(GameObject parent, string name, Material material, ParticleSystemSimulationSpace space)
        {
            var system = new GameObject(name).AddComponent<ParticleSystem>();
            system.transform.SetParent(parent.transform, false);
            var main = system.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = space;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return system;
        }

        // In softly, out softly, from one colour to the other, at most this see-through.
        private static void Fade(ParticleSystem system, Color from, Color to, float alpha)
        {
            var colour = system.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(alpha, 0.3f), new GradientAlphaKey(alpha * 0.8f, 0.7f), new GradientAlphaKey(0f, 1f) });
            colour.color = gradient;
        }
    }
}

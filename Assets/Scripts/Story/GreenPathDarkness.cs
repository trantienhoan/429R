using System.Collections.Generic;
using UnityEngine;

namespace Game.Story
{
    /// <summary>
    /// The dark at the teleport zone (user 2026-11-27: "when player reach that teleport zone, everything should be dark
    /// there only the green light path"): like the story's dark walk, the ambient light, the sky, the fog's colour and
    /// every light in the scene fade down (Green Path Settings: Ambient / Sky / Lights Left) and every self-lit particle
    /// goes, except what is kept (the path and its zone, the tube: Keep) and the player's own (hands, HUD). End brings it
    /// all back; leaving play or the scene puts everything back too. While the tutorial's own dark walk is on, this
    /// does nothing (it already is dark, and the two would fight over the same settings).
    /// </summary>
    public class GreenPathDarkness : MonoBehaviour
    {
        private static readonly int ExposureId = Shader.PropertyToID("_Exposure");
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private static GreenPathDarkness current;
        private static readonly List<Transform> kept = new List<Transform>();

        /// <summary>How dark it is now: 0 = as normal, 1 = the full dark.</summary>
        public static float Darkness => current != null ? current.dark : 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            current = null;
            kept.Clear();
        }

        /// <summary>This and everything under it keeps its light in the dark.</summary>
        public static void Keep(Transform root)
        {
            if (root != null && !kept.Contains(root)) kept.Add(root);
        }

        /// <summary>Fade into the dark over these seconds.</summary>
        public static void Begin(float seconds)
        {
            if (StoryDarkWalk.Darkness > 0.01f) return;
            if (current == null) current = new GameObject("Green_Path_Dark").AddComponent<GreenPathDarkness>();
            current.target = 1f;
            current.fadeSeconds = Mathf.Max(0.01f, seconds);
        }

        /// <summary>Fade back to normal over these seconds (nothing happens if it is not dark).</summary>
        public static void End(float seconds)
        {
            if (current == null) return;
            current.target = 0f;
            current.fadeSeconds = Mathf.Max(0.01f, seconds);
        }

        private GreenPathSettings settings;
        private float dark, target = 1f, fadeSeconds = 0.5f, nextScan;
        private Color ambientWas, skyColourWas, equatorWas, groundWas, fogWas;
        private Material skyWas, skyCopy;
        private float exposureWas = 1f;
        private Color tintWas = Color.white;
        private readonly Dictionary<Light, Color> lights = new Dictionary<Light, Color>();
        private readonly List<Renderer> flames = new List<Renderer>();
        private readonly Dictionary<Renderer, Color> domes = new Dictionary<Renderer, Color>();
        private MaterialPropertyBlock block;
        private bool restored;

        private void Awake()
        {
            settings = GreenPathSettings.Instance;
            ambientWas = RenderSettings.ambientLight;
            skyColourWas = RenderSettings.ambientSkyColor;
            equatorWas = RenderSettings.ambientEquatorColor;
            groundWas = RenderSettings.ambientGroundColor;
            fogWas = RenderSettings.fogColor;
            skyWas = RenderSettings.skybox;
            if (skyWas != null)
            {
                // A copy, so the sky's own material (an asset) is never changed.
                skyCopy = new Material(skyWas) { name = skyWas.name + " (green path dark)" };
                if (skyCopy.HasProperty(ExposureId)) exposureWas = skyCopy.GetFloat(ExposureId);
                if (skyCopy.HasProperty(TintId)) tintWas = skyCopy.GetColor(TintId);
                RenderSettings.skybox = skyCopy;
            }
        }

        private void LateUpdate()
        {
            dark = Mathf.MoveTowards(dark, target, Time.unscaledDeltaTime / fadeSeconds);
            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + 0.25f;
                Scan();
            }
            Apply();
            if (dark <= 0f && target <= 0f)
            {
                Restore();
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            Restore();
            if (current == this)
            {
                current = null;
                kept.Clear();
            }
        }

        private bool Kept(Transform t)
        {
            for (; t != null; t = t.parent)
            {
                if (kept.Contains(t)) return true;
                if (t.GetComponent<Unity.XR.CoreUtils.XROrigin>() != null) return true;
            }
            return false;
        }

        private void Scan()
        {
            kept.RemoveAll(t => t == null);
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (lights.ContainsKey(l) || Kept(l.transform)) continue;
                lights[l] = l.color;
            }
            foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (domes.ContainsKey(r) || !NamedSkydome(r.transform)) continue;
                var m = r.sharedMaterial;
                if (m == null || !m.HasProperty(EmissionId) || !m.IsKeywordEnabled("_EMISSION")) continue;
                domes[r] = m.GetColor(EmissionId);
            }
            foreach (var r in FindObjectsByType<ParticleSystemRenderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || flames.Contains(r) || Kept(r.transform)) continue;
                flames.Add(r);
            }
        }

        private void Apply()
        {
            float ambient = Mathf.Lerp(1f, settings.AmbientLeft, dark);
            RenderSettings.ambientLight = ambientWas * ambient;
            RenderSettings.ambientSkyColor = skyColourWas * ambient;
            RenderSettings.ambientEquatorColor = equatorWas * ambient;
            RenderSettings.ambientGroundColor = groundWas * ambient;
            RenderSettings.fogColor = fogWas * ambient;
            float sky = Mathf.Lerp(1f, settings.SkyLeft, dark);
            if (skyCopy != null)
            {
                if (skyCopy.HasProperty(ExposureId)) skyCopy.SetFloat(ExposureId, exposureWas * sky);
                else if (skyCopy.HasProperty(TintId)) skyCopy.SetColor(TintId, tintWas * sky);
            }
            if (domes.Count > 0)
            {
                if (block == null) block = new MaterialPropertyBlock();
                foreach (var kv in domes)
                {
                    if (kv.Key == null) continue;
                    kv.Key.GetPropertyBlock(block);
                    block.SetColor(EmissionId, kv.Value * sky);
                    kv.Key.SetPropertyBlock(block);
                }
            }
            float rest = Mathf.Lerp(1f, settings.LightsLeft, dark);
            foreach (var kv in lights)
                if (kv.Key != null) kv.Key.color = kv.Value * rest;
            bool flamesOn = dark < 0.5f;
            foreach (var r in flames)
                if (r != null && r.enabled != flamesOn) r.enabled = flamesOn;
        }

        private void Restore()
        {
            if (restored) return;
            restored = true;
            RenderSettings.ambientLight = ambientWas;
            RenderSettings.ambientSkyColor = skyColourWas;
            RenderSettings.ambientEquatorColor = equatorWas;
            RenderSettings.ambientGroundColor = groundWas;
            RenderSettings.fogColor = fogWas;
            if (skyWas != null && RenderSettings.skybox == skyCopy) RenderSettings.skybox = skyWas;
            if (skyCopy != null) Destroy(skyCopy);
            foreach (var kv in lights)
                if (kv.Key != null) kv.Key.color = kv.Value;
            foreach (var r in flames)
                if (r != null) r.enabled = true;
            if (block == null) block = new MaterialPropertyBlock();
            foreach (var kv in domes)
            {
                if (kv.Key == null) continue;
                kv.Key.GetPropertyBlock(block);
                block.SetColor(EmissionId, kv.Value);
                kv.Key.SetPropertyBlock(block);
            }
        }

        private static bool NamedSkydome(Transform t)
        {
            for (int i = 0; t != null && i < 3; i++, t = t.parent)
                if (t.name.StartsWith("Skydome")) return true;
            return false;
        }
    }
}

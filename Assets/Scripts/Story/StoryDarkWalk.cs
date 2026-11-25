using System.Collections.Generic;
using UnityEngine;

namespace Game.Story
{
    /// <summary>
    /// The story's dark walk at the start of a run (user 2026-11-25: "make everything dark except the fairy, so player
    /// have to walk in the dark mostly with only fairy is the light source"). Begin (GAMESTAGES "Walk Tuttorial", the
    /// Story Darkness action) fades the world dark: the ambient light, the sky (moon and stars), the fog's colour, every
    /// light in the scene (the rooftop's lamp, the candles: their colour, so their own flicker animations go on) and every
    /// self-lit particle more than a few metres from her (candle flames, the dream smoke round the player) and the dream
    /// sky dome's own glow; the
    /// weak Fairy gets a green light of her own that breathes and nearly goes out with her glow (Story Weak Fairy), so
    /// she is the one light to walk to. End (GAMESTAGES "Send death", when she is reached) fades it all back: "the floor
    /// starts to show up". It also ends by itself when GAMESTAGES leaves the tutorial's states any other way (Dark States in
    /// the settings), so a run is never left dark. Lights that come on during the dark (the rooftop spawning) are caught
    /// within 0.25 s. Leaving play or the scene puts everything back. Settings: Resources/Story Dark Walk Settings.
    /// </summary>
    public class StoryDarkWalk : MonoBehaviour
    {
        private static readonly int ExposureId = Shader.PropertyToID("_Exposure");
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private static StoryDarkWalk current;

        /// <summary>How dark it is now: 0 = as normal, 1 = the full dark.</summary>
        public static float Darkness => current != null ? current.dark : 0f;

        /// <summary>The Fairy's light, while there is one (for tests).</summary>
        public static Light FairyLight => current != null ? current.fairyLight : null;

        /// <summary>Fade into the dark over these seconds.</summary>
        public static void Begin(float seconds)
        {
            if (current == null) current = new GameObject("Story_Dark_Walk").AddComponent<StoryDarkWalk>();
            current.target = 1f;
            current.fadeSeconds = Mathf.Max(0.01f, seconds);
        }

        /// <summary>
        /// Later runs (Story Start: the Fairy met before): dark at once, then the rooftop path lights up slowly over these
        /// seconds; no light of hers (she is missing or free).
        /// </summary>
        public static void LightUp(float seconds)
        {
            if (current == null) current = new GameObject("Story_Dark_Walk").AddComponent<StoryDarkWalk>();
            current.noFairyLight = true;
            current.dark = 1f;
            current.target = 0f;
            current.fadeSeconds = Mathf.Max(0.01f, seconds);
        }

        /// <summary>Fade back to normal over these seconds (nothing happens if it is not dark).</summary>
        public static void End(float seconds)
        {
            if (current == null) return;
            current.target = 0f;
            current.fadeSeconds = Mathf.Max(0.01f, seconds);
        }

        private StoryDarkWalkSettings settings;
        private float dark, target = 1f, fadeSeconds = 1f, nextScan;
        private Color ambientWas, skyColourWas, equatorWas, groundWas, fogWas;
        private float ambientIntensityWas;
        private Material skyWas, skyCopy;
        private float exposureWas = 1f;
        private Color tintWas = Color.white;
        private readonly Dictionary<Light, Color> lights = new Dictionary<Light, Color>();
        private readonly List<Renderer> flames = new List<Renderer>();
        private readonly Dictionary<Renderer, Color> domes = new Dictionary<Renderer, Color>();
        private MaterialPropertyBlock block;
        private StoryWeakFairy fairy;
        private Light fairyLight;
        private bool restored;
        private bool noFairyLight;

        private void Awake()
        {
            settings = StoryDarkWalkSettings.Instance;
            ambientWas = RenderSettings.ambientLight;
            skyColourWas = RenderSettings.ambientSkyColor;
            equatorWas = RenderSettings.ambientEquatorColor;
            groundWas = RenderSettings.ambientGroundColor;
            ambientIntensityWas = RenderSettings.ambientIntensity;
            fogWas = RenderSettings.fogColor;
            skyWas = RenderSettings.skybox;
            if (skyWas != null)
            {
                // A copy, so the sky's own material (an asset) is never changed.
                skyCopy = new Material(skyWas) { name = skyWas.name + " (dark walk)" };
                if (skyCopy.HasProperty(ExposureId)) exposureWas = skyCopy.GetFloat(ExposureId);
                if (skyCopy.HasProperty(TintId)) tintWas = skyCopy.GetColor(TintId);
                RenderSettings.skybox = skyCopy;
            }
        }

        private void LateUpdate()
        {
            // Out of the tutorial's states without "Send death" (a checkpoint jump, a shortcut): the dark goes by itself.
            if (target > 0f && !InDarkState())
            {
                target = 0f;
                fadeSeconds = settings.SelfEndSeconds;
            }
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
            if (current == this) current = null;
        }

        private bool InDarkState()
        {
            var stages = GameStages.Fsm();
            if (stages == null) return true;
            string state = stages.ActiveStateName;
            if (string.IsNullOrEmpty(state)) return true;
            foreach (var s in settings.DarkStates)
                if (s == state) return true;
            return false;
        }

        // New lights and flames since the last look, and the Fairy.
        private void Scan()
        {
            if (fairy == null) fairy = FindAnyObjectByType<StoryWeakFairy>();
            if (fairy != null && fairyLight == null && !noFairyLight)
            {
                // She came after the dark began: what glows near her (her beam, her ring) shines again.
                for (int i = flames.Count - 1; i >= 0; i--)
                {
                    var r = flames[i];
                    if (r != null && !NearHer(r)) continue;
                    if (r != null) r.enabled = true;
                    flames.RemoveAt(i);
                }
                var go = new GameObject("Story_Fairy_Light");
                go.transform.SetParent(fairy.transform, false);
                go.transform.localPosition = Vector3.up * 0.15f;
                fairyLight = go.AddComponent<Light>();
                fairyLight.type = LightType.Point;
                fairyLight.color = settings.FairyColour;
                fairyLight.range = settings.FairyRange;
                fairyLight.shadows = LightShadows.None;
                fairyLight.intensity = 0f;
            }
            var head = Camera.main != null ? Camera.main.transform.root : null;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l == fairyLight || lights.ContainsKey(l) || Under(l.transform, fairy != null ? fairy.transform : null) || Under(l.transform, head)) continue;
                lights[l] = l.color;
            }
            // The dream sky dome's shards glow by themselves (emission): they dim with the sky.
            foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (domes.ContainsKey(r) || !NamedSkydome(r.transform)) continue;
                var m = r.sharedMaterial;
                if (m == null || !m.HasProperty(EmissionId) || !m.IsKeywordEnabled("_EMISSION")) continue;
                domes[r] = m.GetColor(EmissionId);
            }
            if (!settings.HideFlames) return;
            // Particles light themselves: in the dark only hers (and those within a few metres of her: her beam, her
            // ring) stay; candle flames and the dream smoke round the player go.
            foreach (var r in FindObjectsByType<ParticleSystemRenderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || flames.Contains(r)) continue;
                if (fairy != null && (Under(r.transform, fairy.transform) || NearHer(r))) continue;
                if (Under(r.transform, head)) continue;
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
            if (skyCopy != null)
            {
                float sky = Mathf.Lerp(1f, settings.SkyLeft, dark);
                if (skyCopy.HasProperty(ExposureId)) skyCopy.SetFloat(ExposureId, exposureWas * sky);
                else if (skyCopy.HasProperty(TintId)) skyCopy.SetColor(TintId, tintWas * sky);
            }
            if (domes.Count > 0)
            {
                if (block == null) block = new MaterialPropertyBlock();
                float sky = Mathf.Lerp(1f, settings.SkyLeft, dark);
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
            if (fairyLight != null)
            {
                float share = 1f;
                if (settings.FollowHerGlow && fairy != null && fairy.BaseGlow > 0f)
                    share = Mathf.Max(settings.LeastShare, fairy.GlowNow / fairy.BaseGlow);
                // Her light is hers: it stays when the room comes back (it goes with her).
                fairyLight.intensity = settings.FairyIntensity * share;
            }
        }

        private void Restore()
        {
            if (restored) return;
            restored = true;
            RenderSettings.ambientLight = ambientWas;
            RenderSettings.ambientSkyColor = skyColourWas;
            RenderSettings.ambientEquatorColor = equatorWas;
            RenderSettings.ambientGroundColor = groundWas;
            RenderSettings.ambientIntensity = ambientIntensityWas;
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

        // Its particles are near her: where they are (their bounds), not where the system stands (the dream smoke's
        // system stands at the cage but its puffs fill 40 m round the player).
        private bool NearHer(Renderer r)
        {
            if (fairy == null) return false;
            var b = r.bounds;
            float keep = settings.KeepNearHer;
            return Vector3.Distance(b.center, fairy.transform.position) < keep && b.extents.magnitude < keep * 1.5f;
        }

        private static bool NamedSkydome(Transform t)
        {
            for (int i = 0; t != null && i < 3; i++, t = t.parent)
                if (t.name.StartsWith("Skydome")) return true;
            return false;
        }

        private static bool Under(Transform t, Transform root)
        {
            if (root == null) return false;
            for (; t != null; t = t.parent)
                if (t == root) return true;
            return false;
        }
    }
}

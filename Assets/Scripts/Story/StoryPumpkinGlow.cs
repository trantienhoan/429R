using Game.Combat;
using Game.PlayGround;
using UnityEngine;

namespace Game.Story
{
    /// <summary>
    /// A room's laughing pumpkin glows green (user's story, 2026-11-25: it holds a small piece of the Green Seed and runs
    /// and hides until it has taken in the power of the Green, then it turns into a giant pumpkin monster). The candle
    /// light in its eyes and grin turns green and the seed's glow seeps out around it (seen from every side): faint when
    /// its countdown to the giant starts, full at the end, throbbing in the last minute, flaring with each laugh. Its
    /// countdown comes from the Warn Before Transform action in its Wait and Transform FSM; with no countdown running (a
    /// bonus round's pumpkins, or the FSM off) it doesn't glow. Broken: the glow goes at once (its mini boss pops out
    /// with the piece). Settings: Resources/Story Pumpkin Glow Settings. On the runner prefabs' roots (setup menu).
    /// </summary>
    public class StoryPumpkinGlow : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        // The countdown counts as running this long after it was last heard.
        private const float FedFor = 0.3f;
        // The seed's glow particles at their least, as a share of their own rate.
        private const float LeastRate = 0.3f;

        private StoryPumpkinGlowSettings settings;
        private RunAwayPumpkin runner;
        private HitBlink blink;
        private Renderer face;
        private MaterialPropertyBlock block;
        private Transform seed;
        private ParticleSystem[] seedParts;
        private float[] seedRates;
        private Vector3 seedSize;
        private float progress, left = float.MaxValue, fedAt = -100f, flareAt = -100f, presence, grown;
        private bool built;

        /// <summary>How strong the glow is now (0 = none; 1 = full at the giant; more while it flares). For tests.</summary>
        public float Level => presence * grown;

        /// <summary>Its countdown to the giant: the share done (0 to 1) and the seconds left. Fed every frame by Warn
        /// Before Transform.</summary>
        public void Countdown(float share, float secondsLeft)
        {
            progress = Mathf.Clamp01(share);
            left = secondsLeft;
            fedAt = Time.time;
        }

        private void Start()
        {
            // After every Awake, so the runner's own lists of its looks and colliders don't take in the glow's.
            settings = StoryPumpkinGlowSettings.Instance;
            runner = GetComponent<RunAwayPumpkin>();
            if (settings == null)
            {
                enabled = false;
                return;
            }
            Build();
        }

        private void OnEnable() => RunAwayPumpkin.Laughed += OnLaugh;

        private void OnDisable() => RunAwayPumpkin.Laughed -= OnLaugh;

        private void OnLaugh(RunAwayPumpkin who)
        {
            if (who == runner) flareAt = Time.time;
        }

        private void Build()
        {
            built = true;
            MeshRenderer body = null;
            foreach (var look in GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!look.gameObject.name.StartsWith("pumpkin", System.StringComparison.OrdinalIgnoreCase)) continue;
                body = look;
                break;
            }
            if (body != null && settings.FaceMesh != null && settings.FaceMaterial != null)
            {
                var go = new GameObject("Story_Face_Glow");
                go.transform.SetParent(body.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = settings.FaceMesh;
                var look = go.AddComponent<MeshRenderer>();
                look.sharedMaterial = settings.FaceMaterial;
                look.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                look.receiveShadows = false;
                look.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                look.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                look.enabled = false;
                face = look;
            }
            if (settings.SeedGlow != null)
            {
                var seedSettings = StorySeedSettings.Instance;
                // Inside its body, in its own space: the pumpkin may still be growing in (small) now.
                var middle = body != null ? transform.InverseTransformPoint(body.bounds.center) : Vector3.zero;
                var go = Instantiate(settings.SeedGlow, transform);
                go.transform.localPosition = middle;
                go.transform.localRotation = Quaternion.identity;
                go.name = "Story_Seed_Glow";
                StorySeedsFlight.OnlyShows(go, seedSettings.GlowParts);
                StorySeed.TintGlow(go, settings.Glow, seedSettings.GlowParts);
                // Only its glow: the seed itself stays hidden inside.
                foreach (var look in go.GetComponentsInChildren<Renderer>(true))
                {
                    if (!(look is ParticleSystemRenderer)) look.enabled = false;
                }
                var parts = go.GetComponentsInChildren<ParticleSystem>(true);
                seedParts = parts;
                seedRates = new float[parts.Length];
                for (int i = 0; i < parts.Length; i++) seedRates[i] = parts[i].emission.rateOverTimeMultiplier;
                seed = go.transform;
                // Its size goes with the pumpkin's (the seed's own scale at the pumpkin's full size).
                seedSize = settings.SeedGlow.transform.localScale;
                seed.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (!built) return;
            bool broken = runner != null && runner.Current == RunAwayPumpkin.Mood.Done;
            bool counting = !broken && Time.time - fedAt <= FedFor;
            if (broken) presence = 0f;
            else presence = Mathf.MoveTowards(presence, counting ? 1f : 0f, Time.deltaTime / settings.FadeSeconds);

            if (counting)
            {
                grown = Mathf.Pow(progress, settings.Growth);
                // The last minute: it throbs, faster and harder as the giant comes.
                if (settings.ThrobLast > 0f && left < settings.ThrobLast)
                {
                    float anger = 1f - Mathf.Clamp01(left / settings.ThrobLast);
                    float speed = Mathf.Lerp(settings.ThrobSpeed.x, settings.ThrobSpeed.y, anger);
                    grown += settings.Throb * anger * (0.5f + 0.5f * Mathf.Sin(Time.time * speed * Mathf.PI * 2f));
                }
            }
            float flare = 1f - (Time.time - flareAt) / settings.FlareSeconds;
            float shine = grown + (flare > 0f ? settings.LaughFlare * flare : 0f);
            Show(shine);
        }

        private void Show(float shine)
        {
            bool on = presence > 0.001f;
            if (face != null)
            {
                if (face.enabled != on) face.enabled = on;
                // A red hit blink on the pumpkin takes the face for its moment.
                if (blink == null) TryGetComponent(out blink);
                if (on && (blink == null || !blink.enabled))
                {
                    block ??= new MaterialPropertyBlock();
                    face.GetPropertyBlock(block);
                    var brightness = settings.FaceBrightness;
                    var colour = settings.Glow * (Mathf.LerpUnclamped(brightness.x, brightness.y, shine) * presence);
                    colour.a = 1f;
                    block.SetColor(BaseColorId, colour);
                    face.SetPropertyBlock(block);
                }
            }
            if (seed != null)
            {
                if (seed.gameObject.activeSelf != on) seed.gameObject.SetActive(on);
                if (!on) return;
                var size = settings.SeedGlowSize;
                seed.localScale = seedSize * (Mathf.LerpUnclamped(size.x, size.y, shine) * presence);
                float rate = Mathf.LerpUnclamped(LeastRate, 1f, shine) * presence;
                for (int i = 0; i < seedParts.Length; i++)
                {
                    if (seedParts[i] == null) continue;
                    var emission = seedParts[i].emission;
                    emission.rateOverTimeMultiplier = seedRates[i] * rate;
                }
            }
        }
    }
}

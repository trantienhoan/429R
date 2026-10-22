using Game.Leveling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.VFX
{
    /// <summary>
    /// High Eye: while the player is high on a boong (Player Buffs: High) the view gets a gentle dreamy look. The colours
    /// get a little richer and slowly drift in hue, a soft vignette takes the boong's colour, and a faint colour fringe
    /// at the edges breathes in and out. It fades in and out over a couple of seconds. Nothing moves, warps or blurs (VR
    /// comfort). Players switch it on and off in the pause menu (High Eye: on/off, saved); untick this component to switch
    /// it off for everyone. It makes its own global Volume on start, above the scene's own post-processing, and steps
    /// aside while the death look (Hide While On) is on.
    /// Tools > 429 Game > Player > Add High Eye puts it in the scene and the High Eye option in the pause menu.
    /// </summary>
    [DisallowMultipleComponent]
    public class HighEye : MonoBehaviour
    {
        [Tooltip("Seconds it takes to fade in and out.")]
        [Min(0.1f)]
        [SerializeField] private float fadeSeconds = 2f;
        [Tooltip("While this is on (the death look, Post Processing_Dead), High Eye fades out.")]
        [SerializeField] private GameObject hideWhileOn;

        [Header("Colour")]
        [Tooltip("How colourful the view gets, in place of the scene's own Saturation (-5): 15 is a little richer.")]
        [Range(-20f, 60f)]
        [SerializeField] private float saturation = 15f;
        [Tooltip("How far the colours drift around the colour wheel, in degrees each way.")]
        [Range(0f, 30f)]
        [SerializeField] private float hueSwing = 10f;
        [Tooltip("Seconds for one slow drift there and back.")]
        [Min(1f)]
        [SerializeField] private float hueSeconds = 8f;

        [Header("Vignette")]
        [Tooltip("How dark the soft edge of the view gets, in place of the scene's own (0.2).")]
        [Range(0f, 0.6f)]
        [SerializeField] private float vignette = 0.28f;
        [Range(0.01f, 1f)]
        [SerializeField] private float vignetteSmoothness = 0.6f;
        [Tooltip("How much of the boong's colour the vignette takes (0 = black edges).")]
        [Range(0f, 1f)]
        [SerializeField] private float tint = 0.6f;

        [Header("Colour fringe")]
        [Tooltip("The faint coloured fringe at the edges of the view (chromatic aberration).")]
        [Range(0f, 0.6f)]
        [SerializeField] private float fringe = 0.15f;
        [Tooltip("How much more it gets as it breathes in.")]
        [Range(0f, 0.4f)]
        [SerializeField] private float fringeBreath = 0.08f;
        [Tooltip("Seconds for one breath.")]
        [Min(0.5f)]
        [SerializeField] private float breathSeconds = 3f;

        [Header("Volume")]
        [Tooltip("Above the scene's own post-processing Volume.")]
        [SerializeField] private float priority = 50f;

        private const string EnabledKey = "429 Game.HighEye";
        private static readonly Buff[] Boongs = { Buff.ElonBoong, Buff.BoneBoong, Buff.GreenBoong, Buff.BusBoong };
        private static bool? chosen;

        /// <summary>The player's choice (pause menu): show High Eye while high. On unless switched off; saved.</summary>
        public static bool Enabled
        {
            get => chosen ??= PlayerPrefs.GetInt(EnabledKey, 1) != 0;
            set
            {
                if (value == Enabled) return;
                chosen = value;
                PlayerPrefs.SetInt(EnabledKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>The pause menu's button text.</summary>
        public static string Label() => Enabled ? "High Eye: on" : "High Eye: off";

        /// <summary>Forgets the cached choice (after the saved options were wiped), so it's read again: on.</summary>
        public static void ForgetChoice() => chosen = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => chosen = null;

        private Volume volume;
        private VolumeProfile profile;
        private ColorAdjustments colour;
        private Vignette edge;
        private ChromaticAberration aberration;
        private float weight;
        private Color boongColour = Color.black;

        private void Awake()
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            colour = profile.Add<ColorAdjustments>();
            colour.saturation.overrideState = true;
            colour.hueShift.overrideState = true;
            edge = profile.Add<Vignette>();
            edge.intensity.overrideState = true;
            edge.smoothness.overrideState = true;
            edge.color.overrideState = true;
            aberration = profile.Add<ChromaticAberration>();
            aberration.intensity.overrideState = true;

            volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = priority;
            volume.weight = 0f;
            volume.sharedProfile = profile;
        }

        private void OnDisable()
        {
            weight = 0f;
            if (volume != null) volume.weight = 0f;
        }

        private void OnDestroy()
        {
            if (volume != null) Destroy(volume);
            if (profile != null) Destroy(profile);
        }

        // Unscaled time: it fades and drifts the same while the game is paused.
        private void Update()
        {
            bool show = Enabled && PlayerBuffs.High && (hideWhileOn == null || !hideWhileOn.activeInHierarchy);
            weight = Mathf.MoveTowards(weight, show ? 1f : 0f, Time.unscaledDeltaTime / fadeSeconds);
            volume.weight = weight;
            if (weight <= 0f) return;

            float t = Time.unscaledTime;
            colour.saturation.value = saturation;
            colour.hueShift.value = hueSwing * Mathf.Sin(t * 2f * Mathf.PI / hueSeconds);
            edge.intensity.value = vignette;
            edge.smoothness.value = vignetteSmoothness;
            // Fading out after the last boong ran out, it keeps that boong's colour.
            if (PlayerBuffs.High) boongColour = BoongColour();
            edge.color.value = Color.Lerp(Color.black, boongColour, tint);
            aberration.intensity.value = fringe + fringeBreath * (0.5f + 0.5f * Mathf.Sin(t * 2f * Mathf.PI / breathSeconds));
        }

        // The colour of the boong with the most time left.
        private static Color BoongColour()
        {
            var best = Buff.GreenBoong;
            float most = -1f;
            foreach (var buff in Boongs)
            {
                float left = PlayerBuffs.SecondsLeft(buff);
                if (left > most) { most = left; best = buff; }
            }
            return PlayerBuffs.ColorOf(best);
        }
    }
}

using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;

namespace Game.Locomotion
{
    /// <summary>
    /// How strongly the comfort vignette closes in while the player moves or turns: Off, Low, Medium or High. The
    /// player picks it in the pause menu and it's kept between sessions (PlayerPrefs, so per device). Goes next to the
    /// Tunneling Vignette Controller; Tools > 429 Game > Locomotion > Add Comfort Vignette adds it.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TunnelingVignetteController))]
    public class ComfortVignetteLevel : MonoBehaviour
    {
        public enum Level
        {
            Off,
            Low,
            Medium,
            High,
        }

        [Tooltip("How much of the view stays open while moving at Low, Medium and High: 1 = all of it, 0.7 = XRI's " +
                 "default, smaller = darker edges.")]
        [Range(0f, 1f)]
        [SerializeField] private float lowAperture = 0.9f;
        [Range(0f, 1f)]
        [SerializeField] private float mediumAperture = 0.8f;
        [Range(0f, 1f)]
        [SerializeField] private float highAperture = 0.7f;
        [Tooltip("How soft the dark edge is at Low, Medium and High.")]
        [Range(0f, 1f)]
        [SerializeField] private float lowFeathering = 0.3f;
        [Range(0f, 1f)]
        [SerializeField] private float mediumFeathering = 0.25f;
        [Range(0f, 1f)]
        [SerializeField] private float highFeathering = 0.2f;

        // Where the player's choice is kept, and what a new player gets.
        private const string LevelKey = "429 Game.ComfortVignette";
        private const Level DefaultLevel = Level.Low;

        /// <summary>Raised when the level changes.</summary>
        public static event Action Changed;

        /// <summary>The player's chosen level; setting it saves it and updates every vignette.</summary>
        public static Level Current
        {
            get
            {
                int saved = PlayerPrefs.GetInt(LevelKey, (int)DefaultLevel);
                return saved >= (int)Level.Off && saved <= (int)Level.High ? (Level)saved : DefaultLevel;
            }
            set
            {
                if (value == Current) return;
                PlayerPrefs.SetInt(LevelKey, (int)value);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Changed = null;
        }

        private TunnelingVignetteController vignette;

        private void Awake()
        {
            vignette = GetComponent<TunnelingVignetteController>();
        }

        private void OnEnable()
        {
            Changed += Apply;
            Apply();
        }

        private void OnDisable()
        {
            Changed -= Apply;
        }

        // Off keeps the view fully open, so nothing is drawn (Hide Vignette When Open switches the shell off).
        private void Apply()
        {
            var (aperture, feathering) = Current switch
            {
                Level.Off => (1f, 0f),
                Level.Low => (lowAperture, lowFeathering),
                Level.Medium => (mediumAperture, mediumFeathering),
                _ => (highAperture, highFeathering),
            };
            Set(vignette.defaultParameters, aperture, feathering);
            foreach (var provider in vignette.locomotionVignetteProviders)
            {
                if (provider.overrideDefaultParameters) Set(provider.overrideParameters, aperture, feathering);
            }
        }

        private static void Set(VignetteParameters parameters, float aperture, float feathering)
        {
            if (parameters == null) return;
            parameters.apertureSize = aperture;
            parameters.featheringEffect = feathering;
        }

        public static string Label(Level level)
        {
            return "Comfort vignette: " + level switch
            {
                Level.Off => "off",
                Level.Low => "low",
                Level.Medium => "medium",
                _ => "high",
            };
        }
    }
}
